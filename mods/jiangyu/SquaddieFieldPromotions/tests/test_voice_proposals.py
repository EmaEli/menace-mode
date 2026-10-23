"""Synthetic fixtures only: no Workshop transcript or real audio mapping."""
import copy
import contextlib
import io
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('voice_proposals',
    Path(__file__).resolve().parents[1] / 'tools/curate_voice_proposals.py')
curation = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(curation)


class VoiceProposalTests(unittest.TestCase):
    def setUp(self):
        self.native = {'routes': {f'{speaker}|{family}|{index}': []
            for speaker in ('john', 'jane') for family, count in
            [('ready', 1), ('moving', 2), ('kill', 1), ('enemy_damaged', 1)]
            for index in range(count)}}
        self.baseline = {'profiles': [
            dict(id='sample', sourcePack='SamplePack', routes={'ready|0':
                 dict(assets=['xcom_sample_legacy'], subtitle='Legacy synthetic caption.')}),
            dict(id='unchanged', sourcePack='UnchangedPack', routes={'ready|0':
                 dict(assets=['xcom_unchanged_legacy'], subtitle='Untouched fixture.')})]}
        self.groups = [dict(id='test-movement', sourceEvents=['source_move'],
            nativeFamilies=['moving'], texts=['Synthetic advance confirmed'],
            reason='Synthetic fixture for the exact text/context contract')]

    def row(self, number=1, **kwargs):
        row = dict(relative_path=f'Collection/SamplePack/Audio/clip{number}.ogg',
            filename=f'clip{number}.ogg', voice_pack='SamplePack',
            evento_normalizzato='source_move', text='Synthetic advance confirmed!',
            status='ok', priorita_ascolto='P4', indizi='',
            avg_logprob='-0.2', no_speech_prob='0.01', duration_sec='1.5')
        row.update(kwargs)
        return row

    def sources(self, rows):
        return [dict(relative_path=row['relative_path'], source_sha256=f'{index+1:064x}',
                     bytes='100', state='present') for index, row in enumerate(rows)]

    def build(self, rows, sources=None):
        return curation.build_proposal(rows, self.sources(rows) if sources is None else sources,
                                       self.baseline, self.native, self.groups)

    def test_exact_context_adds_only_real_native_slots_and_paired_captions(self):
        proposal, audit, _, _, changes, recordings = self.build([self.row()])
        self.assertEqual({'ready|0', 'moving|0', 'moving|1'}, set(proposal['profiles'][0]['routes']))
        self.assertEqual('propose', audit[0]['decision'])
        self.assertEqual(2, len(changes))
        self.assertEqual(1, len(recordings))
        for key in ('moving|0', 'moving|1'):
            route = proposal['profiles'][0]['routes'][key]
            self.assertEqual(['Synthetic advance confirmed!'], route['assetSubtitles'])
            self.assertEqual(route['subtitle'], route['assetSubtitles'][0])

    def test_does_not_mutate_baseline_or_profiles_without_source_evidence(self):
        before = copy.deepcopy(self.baseline)
        proposal, *_ = self.build([self.row()])
        self.assertEqual(before, self.baseline)
        self.assertEqual(before['profiles'][1], proposal['profiles'][1])
        self.assertEqual(before['profiles'][0]['routes']['ready|0'],
                         proposal['profiles'][0]['routes']['ready|0'])

    def test_negation_or_extra_claim_is_not_a_substring_match(self):
        for text in ('Synthetic advance not confirmed',
                     'Synthetic advance confirmed, but target is dead'):
            with self.subTest(text=text):
                _, audit, _, _, changes, _ = self.build([self.row(text=text)])
                self.assertEqual([], changes)
                self.assertEqual('no_exact_text_and_event_approval', audit[0]['reason'])

    def test_right_words_wrong_source_event_remain_review(self):
        _, audit, _, _, changes, _ = self.build([self.row(evento_normalizzato='source_death')])
        self.assertEqual([], changes)
        self.assertEqual('review', audit[0]['decision'])

    def test_uncertain_invalid_or_flagged_asr_cannot_be_selected(self):
        cases = [dict(status='review'), dict(priorita_ascolto='P2'), dict(indizi='prompt_echo'),
                 dict(avg_logprob='nan'), dict(no_speech_prob='inf'), dict(duration_sec=''),
                 dict(duration_sec='-1'), dict(no_speech_prob='0.9'), dict(avg_logprob='-0.8'),
                 dict(text='Synthetic advance confirmed!\n'), dict(duration_sec='9')]
        for case in cases:
            with self.subTest(case=case):
                _, audit, _, _, changes, _ = self.build([self.row(**case)])
                self.assertEqual([], changes)
                self.assertEqual('review', audit[0]['decision'])

    def test_same_wording_aliases_do_not_pad_variety(self):
        rows = [self.row(1), self.row(2, avg_logprob='-0.1')]
        _, audit, _, _, _, recordings = self.build(rows)
        self.assertEqual(1, len(recordings))
        self.assertEqual(rows[1]['relative_path'], recordings[0]['sourceRelative'])
        self.assertEqual(['review', 'propose'], [r['decision'] for r in audit])

    def test_existing_source_is_reused_without_counting_it_as_new_audio(self):
        rows = [self.row()]
        self.baseline['profiles'][0]['routes']['ready|0']['subtitle'] = rows[0]['text']
        manifest = {'clips': [dict(assetName='xcom_sample_legacy', profileId='sample',
                                   sourceRelative=rows[0]['relative_path'])]}
        proposal, _, _, _, changes, recordings = curation.build_proposal(
            rows, self.sources(rows), self.baseline, self.native, self.groups, manifest)
        self.assertEqual([], recordings)
        self.assertEqual(2, len(changes))
        self.assertTrue(all(change['new_recordings'] == 0 for change in changes))
        route = proposal['profiles'][0]['routes']['moving|0']
        self.assertEqual(['xcom_sample_legacy'], route['assets'])

    def test_existing_source_caption_conflict_requires_review_not_a_duplicate_asset(self):
        rows = [self.row()]
        manifest = {'clips': [dict(assetName='xcom_sample_legacy', profileId='sample',
                                   sourceRelative=rows[0]['relative_path'])]}
        _, audit, _, _, changes, recordings = curation.build_proposal(
            rows, self.sources(rows), self.baseline, self.native, self.groups, manifest)
        self.assertEqual([], changes)
        self.assertEqual([], recordings)
        self.assertEqual('baseline_caption_conflict', audit[0]['reason'])

    def test_missing_or_wrong_profile_baseline_provenance_fails(self):
        rows = [self.row()]
        for manifest in ({'clips': []}, {'clips': [dict(assetName='xcom_sample_legacy',
                          profileId='wrong', sourceRelative=rows[0]['relative_path'])]}):
            with self.assertRaises(ValueError):
                curation.build_proposal(rows, self.sources(rows), self.baseline,
                                        self.native, self.groups, manifest)

    def test_identical_audio_with_conflicting_transcripts_is_quarantined(self):
        rows = [self.row(1), self.row(2, text='Synthetic advance not confirmed')]
        sources = self.sources(rows)
        sources[1]['source_sha256'] = sources[0]['source_sha256']
        _, audit, _, _, changes, _ = self.build(rows, sources)
        self.assertEqual([], changes)
        self.assertTrue(all(r['reason'] == 'conflicting_transcripts_for_identical_bytes' for r in audit))

    def test_missing_source_is_not_silently_approved(self):
        rows = [self.row()]
        sources = self.sources(rows)
        sources[0].update(state='missing', source_sha256='', bytes='0')
        _, audit, _, _, changes, _ = self.build(rows, sources)
        self.assertEqual([], changes)
        self.assertEqual('missing_source', audit[0]['reason'])

    def test_unassigned_source_pack_is_accounted_but_not_added_as_a_profile(self):
        rows = [self.row(voice_pack='RobotPack', relative_path='Collection/RobotPack/clip1.ogg')]
        proposal, audit, *_ = self.build(rows)
        self.assertEqual(self.baseline, proposal)
        self.assertEqual('excluded', audit[0]['decision'])

    def test_duplicate_paths_in_csv_or_index_fail(self):
        row = self.row()
        for rows, sources in [([row, row], self.sources([row])),
                              ([row], self.sources([row]) * 2)]:
            with self.assertRaises(ValueError):
                self.build(rows, sources)

    def test_path_escape_absolute_path_and_pack_mismatch_fail(self):
        for path in ('../SamplePack/clip1.ogg', '/Collection/SamplePack/clip1.ogg',
                     'C:/SamplePack/clip1.ogg', 'Collection/OtherPack/clip1.ogg',
                     'Collection/SamplePack/../clip1.ogg', 'Collection\\SamplePack\\clip1.ogg'):
            with self.subTest(path=path), self.assertRaises(ValueError):
                self.build([self.row(relative_path=path)])

    def test_missing_or_extra_index_entries_fail(self):
        rows = [self.row()]
        for sources in ([], self.sources([self.row(1), self.row(2)])):
            with self.assertRaises(ValueError):
                self.build(rows, sources)

    def test_approval_for_unknown_family_or_ambiguous_context_fails(self):
        self.groups[0]['nativeFamilies'] = ['not_a_native_family']
        with self.assertRaises(ValueError):
            self.build([self.row()])
        self.groups[0]['nativeFamilies'] = ['moving']
        self.groups.append(copy.deepcopy(self.groups[0]))
        with self.assertRaises(ValueError):
            self.build([self.row()])

    def test_native_contract_mismatch_or_gaps_fail(self):
        del self.native['routes']['jane|moving|1']
        with self.assertRaises(ValueError):
            self.build([self.row()])
        del self.native['routes']['john|moving|1']
        self.native['routes']['john|moving|2'] = []
        self.native['routes']['jane|moving|2'] = []
        with self.assertRaises(ValueError):
            self.build([self.row()])

    def test_content_identity_must_be_valid_when_source_is_present(self):
        rows = [self.row()]
        for update in (dict(source_sha256='bad'), dict(bytes='0')):
            sources = self.sources(rows)
            sources[0].update(update)
            with self.assertRaises(ValueError):
                self.build(rows, sources)

    def test_empty_change_report_is_valid_and_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'changes.csv'
            curation.write_csv(path, [], ['profile_id', 'route'])
            self.assertEqual('profile_id,route\n', path.read_text())
            with self.assertRaises(FileExistsError):
                curation.write_csv(path, [], ['profile_id', 'route'])

    def test_cli_pins_inputs_and_refuses_output_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            rows = [self.row()]
            self.baseline['profiles'][0]['routes']['ready|0']['subtitle'] = rows[0]['text']
            curation.write_csv(root / 'csv.csv', rows)
            curation.write_csv(root / 'source_index.csv', self.sources(rows))
            manifest = {'clips': [dict(assetName='xcom_sample_legacy', profileId='sample',
                                       sourceRelative=rows[0]['relative_path'])]}
            for name, value in [('baseline', self.baseline), ('native', self.native), ('baseline_sources', manifest)]:
                (root / (name + '.json')).write_text(json.dumps(value))
            paths = {name: root / (name + suffix) for name, suffix in
                     [('csv', '.csv'), ('source_index', '.csv'), ('baseline', '.json'),
                      ('native', '.json'), ('baseline_sources', '.json')]}
            approvals = dict(schemaVersion=1, inputs={name: curation.digest(path) for name, path in paths.items()},
                             groups=self.groups)
            (root / 'approvals.json').write_text(json.dumps(approvals))
            argv = ['curate_voice_proposals.py', '--approvals', str(root / 'approvals.json'), '--output', str(root / 'output')]
            for name, path in paths.items():
                argv += ['--' + name.replace('_', '-'), str(path)]
            with patch('sys.argv', argv), contextlib.redirect_stdout(io.StringIO()):
                curation.main()
            summary_path = root / 'output/summary.json'
            summary = json.loads(summary_path.read_text())
            self.assertFalse(summary['deploymentReady'])
            self.assertEqual(0, summary['newRecordings'])
            original_hash = curation.digest(summary_path)
            with patch('sys.argv', argv), self.assertRaises(FileExistsError):
                curation.main()
            self.assertEqual(original_hash, curation.digest(summary_path))
            with paths['csv'].open('a') as handle:
                handle.write('\n')
            argv[argv.index('--output') + 1] = str(root / 'other-output')
            with patch('sys.argv', argv), self.assertRaisesRegex(ValueError, 'hashes differ'):
                curation.main()
            self.assertFalse((root / 'other-output').exists())


if __name__ == '__main__':
    unittest.main()
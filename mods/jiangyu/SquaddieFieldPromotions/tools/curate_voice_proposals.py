"""Build PRIVATE, additive voice proposals from exact text/context approvals.

This does not install or publish anything. ASR thresholds are rejection gates,
never listening verification. Approval data, source indices and outputs must
stay outside tracked/public directories.
"""
import argparse
import copy
import csv
import hashlib
import json
import math
import re
from collections import Counter, defaultdict
from pathlib import Path, PurePosixPath


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(text):
    return re.sub(r'[^a-z0-9]+', ' ', text.casefold()).strip()


def read_csv(path):
    with path.open(encoding='utf-8-sig', newline='') as handle:
        return list(csv.DictReader(handle))


def write_csv(path, rows, fields=None):
    with path.open('x', encoding='utf-8', newline='') as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]) if rows else fields)
        writer.writeheader()
        writer.writerows(rows)


def native_keys(native):
    john = {key[5:] for key in native['routes'] if key.startswith('john|')}
    jane = {key[5:] for key in native['routes'] if key.startswith('jane|')}
    if not john or john != jane:
        raise ValueError('Native voice slot contracts differ')
    families = defaultdict(list)
    for key in john:
        family, index = key.split('|')
        families[family].append(int(index))
    if any(sorted(indices) != list(range(len(indices))) for indices in families.values()):
        raise ValueError('Native slots must have contiguous indices')
    return sorted(john, key=lambda key: (key.split('|')[0], int(key.split('|')[1])))


def validate_sources(rows, sources):
    if not rows or not sources:
        raise ValueError('Empty source inventory')
    by_path = {}
    for source in sources:
        path = source['relative_path']
        if path in by_path:
            raise ValueError('Duplicate source-index path')
        by_path[path] = source
    seen = set()
    for row in rows:
        path = row['relative_path']
        parts = PurePosixPath(path).parts
        if (not parts or PurePosixPath(path).is_absolute() or '..' in parts or
                '\\' in path or ':' in path or len(parts) < 3 or
                parts[1] != row['voice_pack'] or parts[-1] != row['filename'] or
                PurePosixPath(path).suffix.lower() not in ('.ogg', '.wav')):
            raise ValueError('Unsafe or inconsistent source path')
        if path in seen or path not in by_path:
            raise ValueError('Duplicate CSV path or missing source-index entry')
        seen.add(path)
        source = by_path[path]
        if source['state'] == 'present' and (
                not re.fullmatch('[a-f0-9]{64}', source['source_sha256']) or int(source['bytes']) <= 0):
            raise ValueError('Invalid source content identity')
    if seen != set(by_path):
        raise ValueError('Source index and CSV inventories differ')
    return by_path


def approval_lookup(groups, families):
    lookup = {}
    for group in groups:
        events, targets = group['sourceEvents'], group['nativeFamilies']
        if (not events or not targets or not set(targets) <= families or
                not group['reason'].strip() or not group['id'].strip()):
            raise ValueError('Invalid semantic approval group')
        for text in group['texts']:
            text = normalized(text)
            if not text:
                raise ValueError('Empty approval text')
            for event in events:
                key = (event, text)
                if key in lookup:
                    raise ValueError('Ambiguous text/context approval')
                lookup[key] = group
    return lookup


def asr_gate(row):
    text = row['text']
    if not text.strip() or len(text) > 400 or any(ord(ch) < 32 for ch in text):
        return 'invalid_caption'
    if row['status'] != 'ok' or row['priorita_ascolto'] != 'P4' or row['indizi'].strip():
        return 'asr_or_triage_review'
    try:
        score, no_speech, duration = (float(row[key]) for key in
                                     ('avg_logprob', 'no_speech_prob', 'duration_sec'))
    except (ValueError, TypeError):
        return 'invalid_asr_metrics'
    if not all(math.isfinite(n) for n in (score, no_speech, duration)):
        return 'invalid_asr_metrics'
    if not (-0.5 <= score <= 0 and 0 <= no_speech <= 0.2 and 0.25 <= duration <= 8):
        return 'asr_quality_gate'
    return ''


def baseline_identities(profiles, source_index, manifest):
    """Reuse retained assets by byte identity, not by their arbitrary names."""
    result = {}
    if manifest is None:
        return result
    clips = {clip['assetName']: clip for clip in manifest['clips']}
    if len(clips) != len(manifest['clips']):
        raise ValueError('Duplicate baseline manifest asset')
    sourced_packs = {PurePosixPath(path).parts[1] for path in source_index}
    for profile in profiles.values():
        if profile['sourcePack'] not in sourced_packs:
            continue  # An unchanged profile outside this CSV, e.g. BlackOps.
        for route in profile['routes'].values():
            captions = route.get('assetSubtitles') or [route['subtitle']] * len(route['assets'])
            for asset, caption in zip(route['assets'], captions):
                clip = clips.get(asset)
                if not clip or clip.get('profileId') != profile['id']:
                    raise ValueError('Missing/wrong-profile baseline source provenance')
                source = source_index.get(clip['sourceRelative'])
                if (not source or source['state'] != 'present' or
                        PurePosixPath(clip['sourceRelative']).parts[1] != profile['sourcePack']):
                    raise ValueError('Baseline source is missing or belongs to another pack')
                identity = (profile['id'], source['source_sha256'])
                entry = result.setdefault(identity, dict(assets=set(), captions=set()))
                entry['assets'].add(asset)
                entry['captions'].add(caption)
    return result


def build_proposal(rows, sources, baseline, native, groups, baseline_sources=None):
    keys = native_keys(native)
    families = {key.split('|')[0] for key in keys}
    approvals = approval_lookup(groups, families)
    source_index = validate_sources(rows, sources)
    profiles = {profile['id']: profile for profile in baseline['profiles']}
    pack_ids = {p['sourcePack']: p['id'] for p in profiles.values()}
    if len(pack_ids) != len(profiles):
        raise ValueError('Ambiguous profile/source pack identity')
    for profile in profiles.values():
        if not set(profile['routes']) <= set(keys):
            raise ValueError('Baseline contains a non-native route')
    retained = baseline_identities(profiles, source_index, baseline_sources)
    # Identical bytes with inconsistent transcripts are not independent evidence.
    wordings = defaultdict(set)
    for row in rows:
        source = source_index[row['relative_path']]
        if source['state'] == 'present':
            wordings[(row['voice_pack'], source['source_sha256'])].add(normalized(row['text']))
    audit, pools = [], defaultdict(dict)
    for row in sorted(rows, key=lambda row: row['relative_path']):
        source = source_index[row['relative_path']]
        profile_id = pack_ids.get(row['voice_pack'], '')
        group = approvals.get((row['evento_normalizzato'], normalized(row['text'])))
        reason = ('excluded_profile' if not profile_id else
                  'missing_source' if source['state'] != 'present' else asr_gate(row))
        if not reason and len(wordings[(row['voice_pack'], source['source_sha256'])]) > 1:
            reason = 'conflicting_transcripts_for_identical_bytes'
        existing = retained.get((profile_id, source['source_sha256']))
        if not reason and existing and (
                len(existing['captions']) != 1 or
                {normalized(text) for text in existing['captions']} != {normalized(row['text'])}):
            reason = 'baseline_caption_conflict'
        if not reason and not group:
            reason = 'no_exact_text_and_event_approval'
        item = dict(row, profile_id=profile_id, source_sha256=source['source_sha256'],
                    decision='excluded' if reason == 'excluded_profile' else 'review',
                    reason=reason, approval_id=group['id'] if group else '',
                    proposed_families='', selected_asset='', human_listening_verified=False)
        audit.append(item)
        if reason:
            continue
        item['proposed_families'] = ';'.join(group['nativeFamilies'])
        item['reason'] = 'eligible_text_review_only; ' + group['reason']
        for family in group['nativeFamilies']:
            pool = pools[(profile_id, family)]
            wording = normalized(row['text'])
            # Pick one recording per wording/family. Different byte hashes alone
            # do not prove useful variety; do not inflate pools with aliases.
            rank = (float(row['avg_logprob']), -float(row['no_speech_prob']))
            if wording not in pool or rank > pool[wording][0]:
                pool[wording] = (rank, item)
    proposal = copy.deepcopy(baseline)
    selected, changes, slots = {}, [], []
    for profile in proposal['profiles']:
        profile_id = profile['id']
        before = profiles[profile_id]['routes']
        for family in sorted(families):
            family_keys = [key for key in keys if key.split('|')[0] == family]
            pool = [value[1] for _, value in sorted(pools[(profile_id, family)].items())]
            if not pool:
                continue
            for index, key in enumerate(family_keys):
                assigned = pool[index::len(family_keys)] or [pool[index % len(pool)]]
                route = profile['routes'].get(key)
                assets = list(route['assets']) if route else []
                captions = list(route.get('assetSubtitles') or [route['subtitle']] * len(assets)) if route else []
                old_words = {normalized(caption) for caption in captions}
                added = 0
                for item in assigned:
                    if normalized(item['text']) in old_words:
                        continue
                    identity = (profile_id, item['source_sha256'])
                    existing = retained.get(identity)
                    asset = (sorted(existing['assets'])[0] if existing else
                             'xcom_' + profile_id + '_' + item['source_sha256'][:20])
                    # Retained exact captions stay authoritative after normalized
                    # agreement; a duplicate source is not new audio under a new ID.
                    caption = next(iter(existing['captions'])) if existing else item['text']
                    if asset in assets:
                        continue
                    if asset in selected and selected[asset]['identity'] != identity:
                        raise ValueError('Truncated content hash asset collision')
                    if asset in selected and selected[asset]['caption'] != caption:
                        # Even punctuation-only alias disagreement must not produce
                        # conflicting per-recording captions in the runtime catalog.
                        caption = selected[asset]['caption']
                    if not existing:
                        selected.setdefault(asset, dict(identity=identity, assetName=asset,
                            profileId=profile_id, sourceRelative=item['relative_path'],
                            sourceSha256=item['source_sha256'], caption=caption))
                    assets.append(asset)
                    captions.append(caption)
                    old_words.add(normalized(caption))
                    item['decision'], item['selected_asset'] = 'propose', asset
                    added += 1
                if added:
                    profile['routes'][key] = dict(assets=assets, subtitle=captions[0], assetSubtitles=captions)
                    changes.append(dict(profile_id=profile_id, route=key,
                                        change='variants' if route else 'new_route',
                                        added_choices=added,
                                        new_recordings=sum(asset in selected for asset in assets)))
        for key in keys:
            route = profile['routes'].get(key)
            slots.append(dict(profile_id=profile_id, route=key,
                state=('new_text_reviewed' if key not in before else 'baseline_retained') if route else 'silent',
                variants=len(route['assets']) if route else 0,
                new_recordings=sum(asset in selected for asset in route['assets']) if route else 0,
                human_listening_verified=False))
    # Record eligible-but-unselected rows honestly, rather than labelling them use.
    for item in audit:
        if not item['proposed_families'] or item['decision'] == 'propose':
            continue
        item['reason'] = 'equivalent_wording_already_retained; ' + item['reason']
    inventory = []
    for profile in proposal['profiles']:
        profile_id = profile['id']
        originals = [row for row in rows if row['voice_pack'] == profile['sourcePack']]
        before = profiles[profile_id]['routes']
        after = profile['routes']
        old_assets = {asset for route in before.values() for asset in route['assets']}
        new_assets = {asset for route in after.values() for asset in route['assets']}
        old_contents = {sha for (speaker, sha) in retained if speaker == profile_id}
        added_contents = {item['sourceSha256'] for item in selected.values() if item['profileId'] == profile_id}
        inventory.append(dict(profile_id=profile_id, source_rows=len(originals),
            source_contents=len({source_index[r['relative_path']]['source_sha256'] for r in originals
                                 if source_index[r['relative_path']]['state'] == 'present'}),
            asr_review_rows=sum(r['status'] != 'ok' for r in originals),
            baseline_slots=len(before), proposed_slots=len(after), silent_slots=len(keys)-len(after),
            baseline_assets=len(old_assets), proposed_assets=len(new_assets),
            baseline_source_contents=len(old_contents) if old_contents else '',
            proposed_source_contents=len(old_contents | added_contents) if old_contents else '',
            added_recording_assets=len(new_assets-old_assets),
            captioned_variant_routes=sum(len(r['assets']) > 1 for r in after.values()),
            listening_verified=False))
    recordings = [{k: v for k, v in value.items() if k != 'identity'} for _, value in sorted(selected.items())]
    return proposal, audit, inventory, slots, changes, recordings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('csv', 'source-index', 'approvals', 'baseline', 'baseline-sources', 'native', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    approvals = json.loads(args.approvals.read_text(encoding='utf-8'))
    pins = {name: digest(getattr(args, name)) for name in
            ('csv', 'source_index', 'baseline', 'baseline_sources', 'native')}
    if approvals.get('schemaVersion') != 1 or approvals.get('inputs') != pins:
        raise ValueError('Approval input hashes differ; re-review against the actual inputs')
    result = build_proposal(read_csv(args.csv), read_csv(args.source_index),
        json.loads(args.baseline.read_text()), json.loads(args.native.read_text()), approvals['groups'],
        json.loads(args.baseline_sources.read_text()))
    proposal, audit, inventory, slots, changes, recordings = result
    # Never write an apparently deployable runtime package. Separate canonical
    # audio authoring, source/PCM checks and build/deploy gates are still required.
    args.output.mkdir(parents=True, exist_ok=False)
    for filename, data, fields in [
            ('source-decisions.csv', audit, None), ('per-voice.csv', inventory, None),
            ('per-slot.csv', slots, None),
            ('route-changes.csv', changes, ['profile_id', 'route', 'change', 'added_choices', 'new_recordings']),
            ('proposed-recordings.csv', recordings,
             ['assetName', 'profileId', 'sourceRelative', 'sourceSha256', 'caption'])]:
        write_csv(args.output / filename, data, fields)
    (args.output / 'proposed_voice_profiles.json').write_text(json.dumps(proposal, indent=2) + '\n')
    summary = dict(schemaVersion=1, inputs=dict(pins, approvals=digest(args.approvals)),
        engineSha256=digest(Path(__file__)), proposalSha256=digest(args.output / 'proposed_voice_profiles.json'),
        sourceRows=len(audit), decisions=dict(Counter(row['decision'] for row in audit)),
        profilesWithSources=sum(row['source_rows'] > 0 for row in inventory),
        profilesImproved=len({row['profile_id'] for row in changes}),
        newRoutes=sum(row['change'] == 'new_route' for row in changes),
        newRecordings=len(recordings), newSourceContents=len({row['sourceSha256'] for row in recordings}),
        nativeSlotsPerVoice=len(native_keys(json.loads(args.native.read_text()))),
        deploymentReady=False, humanListeningVerified=False,
        limitations=['Text/context proposals, not audio listening acceptance.',
                     'Current source hashes cannot retroactively prove identity of the original ASR inputs.',
                     'Baseline mappings are retained, not retrospectively approved.',
                     'Missing mappings stay silent; no delivery/target identity is inferred.',
                     'Audio bundle authoring, PCM verification and canonical deployment have not run.'])
    (args.output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print(json.dumps(summary, sort_keys=True))


if __name__ == '__main__':
    main()
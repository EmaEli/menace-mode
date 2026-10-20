"""Synthetic log regressions; never require or redistribute private game logs."""
import importlib.util
from pathlib import Path
import unittest

TOOL = Path(__file__).resolve().parents[1] / 'tools/analyze_ai_orders.py'
SPEC = importlib.util.spec_from_file_location('analyze_ai_orders', TOOL)
ORDERS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ORDERS)


def event(kind, detail, round_id=3, map_id='AAA', faction=7):
    return (f'[12:00:00.000] Larger Maps: AI ROUTE {kind} map={map_id} '
            f'round={round_id} {detail} faction={faction}\n')


class OrderAnalysisTests(unittest.TestCase):
    def test_polls_are_not_execution_and_positive_actors_are_unique(self):
        rows, _ = ORDERS.parse_lines([
            event('CHOICE', 'id=A chosen=[Move#Move:score=50]'),
            event('MOVE-EVAL', 'id=A score=50'),
            event('MOVE-EVAL', 'id=A score=40'),
            event('MOVE-EVAL', 'id=B score=0'),
        ], 7)
        self.assertEqual(rows[('AAA', 3)]['positive'], [{'A'}, set()])
        self.assertEqual(rows[('AAA', 3)]['moves'], [0, 0])

    def test_mid_round_contact_boundary_keeps_previous_positive_polls(self):
        rows, _ = ORDERS.parse_lines([
            event('STATE', 'id=A phase=round-start knownOpponents=0'),
            event('MOVE-EVAL', 'id=A score=50'),
            event('MOVE', 'id=A returned=True'),
            event('STATE', 'id=A phase=actor-end knownOpponents=1'),
            event('MOVE-EVAL', 'id=B score=0'),
            event('MOVE-EVAL', 'id=C score=2'),
            event('MOVE', 'id=C returned=False'),
        ], 7)
        row = rows[('AAA', 3)]
        self.assertEqual(row['first_known_line'], 4)
        self.assertEqual(row['start_known'], {0})
        self.assertEqual(row['positive'], [{'A'}, {'C'}])
        self.assertEqual(row['moves'], [1, 1])

    def test_civilian_ttl_zero_is_known_negative_is_not(self):
        rows, limits = ORDERS.parse_lines([
            event('ROUND', 'opponents=2 shown=2 [A:civilian.worker known=True ttl=0;'
                  'B:player_squad.test known=False ttl=-1].'),
        ], 7)
        row = rows[('AAA', 3)]
        self.assertEqual(row['contacts'], [('civilian.worker', 0)])
        self.assertTrue(row['contacts_complete'])
        self.assertIn('civilian.worker:TTL=0 (complete)', ORDERS.report(rows, limits))

    def test_incomplete_and_missing_snapshots_are_explicit(self):
        rows, limits = ORDERS.parse_lines([
            event('ROUND', 'opponents=65 shown=1 [A:player_squad.test known=False ttl=-1].'),
            event('MOVE-EVAL', 'id=A score=1', round_id=4),
            'AI ROUTE LIMIT map=AAA round=4 id=A template=civilian.worker kind=STATE\n',
        ], 7)
        text = ORDERS.report(rows, limits)
        self.assertIn('INCOMPLETE', text)
        self.assertIn('missing round snapshot', text)
        self.assertIn('(all factions): 1', text)
        self.assertIn('Missing events are not zero activity', text)

    def test_other_factions_and_maps_do_not_contaminate_boundary(self):
        rows, _ = ORDERS.parse_lines([
            event('STATE', 'id=A knownOpponents=1', faction=3),
            event('STATE', 'id=A knownOpponents=1', map_id='BBB'),
            event('MOVE-EVAL', 'id=A score=20'),
            event('MOVE-EVAL', 'id=A score=20', round_id=4),
        ], 7)
        self.assertIsNone(rows[('AAA', 3)]['first_known_line'])
        self.assertIsNone(rows[('AAA', 4)]['first_known_line'])
        self.assertEqual(rows[('BBB', 3)]['first_known_line'], 2)

    def test_malformed_scores_and_unrelated_lines_are_ignored(self):
        rows, _ = ORDERS.parse_lines([
            'not an AI record', event('MOVE-EVAL', 'id=A score=<null>'),
            event('MOVE-EVAL', 'id=A score=-3'),
        ], 7)
        self.assertEqual(rows[('AAA', 3)]['positive'], [set(), set()])
        self.assertIn('No matching faction records', ORDERS.report({}, 0))

    def test_contact_mismatch_and_pointer_reuse_are_not_silent(self):
        line = event('ROUND', 'opponents=1 shown=1 [A:civilian.worker known=True ttl=-1].')
        rows, _ = ORDERS.parse_lines([line, line], 7)
        warnings = ' '.join(rows[('AAA', 3)]['warnings'])
        self.assertIn('known/TTL mismatch', warnings)
        self.assertIn('session/pointer reuse', warnings)


if __name__ == '__main__':
    unittest.main()
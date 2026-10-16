"""Regression contracts after actual v1.6 failed to modify any skill templates."""
import json
import re
import unittest
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
COMBAT=(ROOT/'src/LargerMaps/SimplifiedCombatPlanning.cs').read_text()
IDS=(ROOT/'src/LargerMaps/KnownCombatSkillIds.cs').read_text()

class TestKnownCombatFallback(unittest.TestCase):
    def test_current_dump_contains_244_relevant_attacks(self):
        archive=Path('/mnt/data/ExtractedData(1).zip')
        if not archive.exists(): self.skipTest('game skill export not available')
        with zipfile.ZipFile(archive) as z:
            templates=json.loads(z.read('ExtractedData/SkillTemplate.json'))
        actual={t['name'] for t in templates if t.get('IsAttack') and
                (t.get('AIConfig') or {}).get('InfluencesPositioning',0)}
        in_source=set(re.findall(r'"([^"\n]+)"',IDS))
        self.assertTrue(actual.issubset(in_source))
        self.assertGreaterEqual(len(actual),200)
        self.assertEqual(len(actual),244)

    def test_uses_known_working_loader_overload(self):
        self.assertIn('method.Name != "Get"',COMBAT)
        self.assertIn('parameters[0].ParameterType == typeof(string)',COMBAT)
        self.assertIn('parameters[1].ParameterType == typeof(bool)',COMBAT)
        self.assertIn('getById.Invoke(null, new object[] { id, false })',COMBAT)

    def test_only_opt_in_large_maps_and_no_global_hook(self):
        self.assertIn('TargetSize > ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true)',COMBAT)
        self.assertIn('attacks + buffs == 0',COMBAT)
        self.assertIn('if (!isAttack && !isBuff) return;',COMBAT)
        self.assertIn('RestoreSimplifiedCombatPlanning()',COMBAT)
        for forbidden in ('Harmony.Patch', 'Thread.Sleep', 'FindPath(', 'Task.Run'):
            self.assertNotIn(forbidden,COMBAT)

    def test_version(self):
        self.assertEqual(json.loads((ROOT/'../jiangyu/LargerMaps/jiangyu.json').read_text())['version'],'2.2.0')
        self.assertNotIn('private const string Version', (ROOT/'src/LargerMaps/Plugin.cs').read_text())

if __name__=='__main__': unittest.main()

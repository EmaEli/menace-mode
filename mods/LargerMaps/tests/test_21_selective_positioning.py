"""Global positioning guards for the Jiangyu port; no native benchmark is claimed."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMBAT = (ROOT / "src/LargerMaps/SimplifiedCombatPlanning.cs").read_text()
PLUGIN = (ROOT / "../jiangyu/AIPerformance/code/Plugin.cs").read_text()
PACKAGE = ROOT.parent / "jiangyu/LargerMaps"


class GlobalPositioningTests(unittest.TestCase):
    def test_default_global_scope_and_existing_lifecycle(self):
        self.assertIn('TargetSize >= ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true)', COMBAT)
        self.assertNotIn('PreserveBasicInfantryFirePositioning', COMBAT + PLUGIN)
        self.assertIn('foreach (string id in KnownCombatSkillIds)', COMBAT)
        self.assertIn('foreach (string id in KnownSupportSkillIds)', COMBAT)
        self.assertIn('ApplySimplifiedCombatPlanning();', PLUGIN)
        self.assertIn('OnUnload() { RestoreSimplifiedCombatPlanning();', PLUGIN)

    def test_every_eligible_template_is_modified_without_faction_filter(self):
        self.assertIn('foreach (object template in EnumerateSkillTemplates(templates))', COMBAT)
        self.assertIn('TryModifySkillPlanning(template, null, ref attacks, ref buffs)', COMBAT)
        self.assertIn('if (!isAttack && !isBuff) return;', COMBAT)
        self.assertNotIn('_preservedInfantryFireProfiles', COMBAT)
        self.assertNotIn('CandidateInfantryFireSkills', COMBAT)
        self.assertIn('_combatPlanningRestores.Clear();', COMBAT)

    def test_no_hot_hooks_sleep_orders_or_custom_pathfinder(self):
        for forbidden in ('FindPath(', 'harmony.Patch', 'Harmony.Patch', 'OnUpdate(', 'OnTurnStart(',
                          'Thread.Sleep', 'Task.Run', 'SetSleeping(', 'WakeUp(', 'SetOrder('):
            self.assertNotIn(forbidden, COMBAT)

    def test_current_jiangyu_versions_not_old_draft_modpack(self):
        self.assertEqual(json.loads((PACKAGE / 'jiangyu.json').read_text())['version'], '2.6.0')
        self.assertEqual(json.loads((PACKAGE / 'compiled/jiangyu.json').read_text())['version'], '2.6.0')
        self.assertIn('<Version>2.6.0</Version>', (PACKAGE / 'code/LargerMaps.csproj').read_text())


if __name__ == '__main__':
    unittest.main()
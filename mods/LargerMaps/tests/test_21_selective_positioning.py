"""PR #18 Jiangyu port guards. No native execution or CPU benchmark is claimed."""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMBAT = (ROOT / "src/LargerMaps/SimplifiedCombatPlanning.cs").read_text()
PLUGIN = (ROOT / "src/LargerMaps/Plugin.cs").read_text()
PACKAGE = ROOT.parent / "jiangyu/LargerMaps"


class SelectivePositioningTests(unittest.TestCase):
    def test_off_default_above_64_only_and_existing_lifecycle(self):
        self.assertIn('TargetSize > ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true)', COMBAT)
        self.assertIn('ReadBool("PreserveBasicInfantryFirePositioning", false)', COMBAT)
        self.assertIn('OFF; restart required)", false)', PLUGIN)
        self.assertIn('ApplySimplifiedCombatPlanning();', PLUGIN)
        self.assertIn('OnUnload() { RestoreSimplifiedCombatPlanning();', PLUGIN)

    def test_exact_five_ids_and_shared_native_profile_identity(self):
        ids = re.findall(r'"(active\.[^"]+)"', COMBAT.split('CandidateInfantryFireSkills =', 1)[1].split('};', 1)[0])
        self.assertEqual(ids, ["active.fire_pirate_assault_rifle", "active.fire_pirate_pdw",
                              "active.fire_pirate_outcast_pipe_gun", "active.fire_assault_rifle_tier1_556",
                              "active.fire_battle_rifle_tier_1"])
        self.assertIn('GetMember(profile, "Pointer")', COMBAT)
        self.assertIn('_preservedInfantryFireProfiles.Contains(NativeProfilePointer(config))', COMBAT)
        self.assertLess(COMBAT.index('CollectPreservedInfantryFireProfiles(getById)'),
                        COMBAT.index('foreach (object template in EnumerateSkillTemplates(templates))'))

    def test_preservation_never_enables_disabled_or_nonattack_profiles(self):
        selection = COMBAT.split('private static void CollectPreservedInfantryFireProfiles(', 1)[1].split('private static void TryModifyKnownId(', 1)[0]
        for member in ('"IsAttack"', '"IsLineOfFireNeeded"', '"InfluencesPositioning"'):
            self.assertIn(member, selection)
        self.assertIn('pointer != IntPtr.Zero', selection)
        self.assertNotIn('TrySetMember(', selection)
        self.assertIn('_preservedInfantryFireProfiles.Clear();', COMBAT)
        self.assertIn('_combatPlanningRestores.Clear();', COMBAT)

    def test_no_hot_hooks_sleep_orders_or_custom_pathfinder(self):
        for forbidden in ('FindPath(', 'harmony.Patch', 'Harmony.Patch', 'OnUpdate(', 'OnTurnStart(',
                          'Thread.Sleep', 'Task.Run', 'SetSleeping(', 'WakeUp(', 'SetOrder('):
            self.assertNotIn(forbidden, COMBAT)

    def test_current_jiangyu_versions_not_old_draft_modpack(self):
        self.assertEqual(json.loads((PACKAGE / 'jiangyu.json').read_text())['version'], '2.1.0')
        self.assertEqual(json.loads((PACKAGE / 'compiled/jiangyu.json').read_text())['version'], '2.1.0')
        self.assertIn('<Version>2.1.0</Version>', (PACKAGE / 'code/LargerMaps.csproj').read_text())


if __name__ == '__main__':
    unittest.main()
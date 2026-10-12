"""Offline safety contracts and native behavioral evidence for the 1.6 experiment."""
import json
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
PLUGIN=(ROOT/'src/LargerMaps/Plugin.cs').read_text()
COMBAT=(ROOT/'src/LargerMaps/SimplifiedCombatPlanning.cs').read_text()
NATIVE=(ROOT/'src/LargerMaps/ExtendedPathfinding.cs').read_text()

class TestCombatPlanning(unittest.TestCase):
    def test_feature_gated_to_large_maps_and_toggle(self):
        self.assertIn('TargetSize > ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true)',COMBAT)
        self.assertIn('AddToggle("SimplifyCombatAIPlanning"', PLUGIN)

    def test_uses_game_native_flag_not_pathfinder_or_move_hooks(self):
        self.assertIn('GetMember(template, "AIConfig")', COMBAT)
        self.assertIn('GetMember(config, "InfluencesPositioning")', COMBAT)
        self.assertIn('TrySetMember(config, "InfluencesPositioning", false)', COMBAT)
        for forbidden in ('FindPath(', 'MovementSystem', 'harmony.Patch', 'HarmonyPrefix', 'Thread.Sleep', 'Task.Run'):
            self.assertNotIn(forbidden, COMBAT)

    def test_protects_noncombat_skills(self):
        self.assertIn('GetMember(template, "IsAttack")', COMBAT)
        self.assertIn('bool isBuff', COMBAT)
        self.assertIn('if (!isAttack && !isBuff) return;', COMBAT)
        self.assertIn('active.rogue_officer_whistle', COMBAT)
        self.assertIn('active.use_target_designator', COMBAT)

    def test_scene_scoped_restore_and_no_false_success(self):
        self.assertIn('RestoreSimplifiedCombatPlanning()', PLUGIN)
        self.assertIn('OnUnload() { RestoreSimplifiedCombatPlanning();',PLUGIN)
        self.assertIn('RestoreSimplifiedCombatPlanning();', COMBAT)
        self.assertIn('_combatPlanningRestores.Clear();', COMBAT)
        self.assertIn('no eligible skill templates updated', COMBAT)
        self.assertIn('TryModifyKnownId(getById, id,', COMBAT)
        self.assertIn('getById.Invoke(null, new object[] { id, false })', COMBAT)
        self.assertIn('combat AI setup failed', COMBAT)

    def test_native_binary_reference_uses_flag_before_attack_and_assist_scoring(self):
        binary=Path('/mnt/data/GameAssembly.dll')
        if not binary.exists(): self.skipTest('Binary only available in authoring environment')
        data=binary.read_bytes()
        # GameAssembly.dll native il2cpp section: section RVA .text offset 0x4bf400;
        # code file offset = RVA (0x7B2342) - 0x4C0000 + 0x4bf400.
        # Verify isolated 64-bit byte comparisons of AIConfig.InfluencesPositioning at +0x14.
        to_offset=lambda rva:0x4bf400 + rva - 0x4c0000
        self.assertEqual(data[to_offset(0x7b2342):to_offset(0x7b2342)+4], bytes.fromhex('44387814'))
        self.assertEqual(data[to_offset(0x7aea69):to_offset(0x7aea69)+4], bytes.fromhex('80781400'))

    def test_versions_and_native_backend_unchanged(self):
        self.assertIn('private const string Version = "1.7.1"', PLUGIN)
        self.assertEqual(json.loads((ROOT/'modpack/LargerMaps/modpack.json').read_text())['version'],'1.7.1')
        reference=Path('/mnt/data/local_routing_work/LargerMaps-1.5-local-tactical-routing-authoring/src/LargerMaps/ExtendedPathfinding.cs')
        if reference.exists(): self.assertEqual(NATIVE,reference.read_text())

if __name__=='__main__':unittest.main()

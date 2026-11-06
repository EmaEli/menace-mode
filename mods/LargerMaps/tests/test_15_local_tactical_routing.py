"""Offline 1.5 contracts. These do NOT replace a live MENACE runtime test."""
import json
import re
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
PLUGIN=(ROOT/'../jiangyu/AIPerformance/code/Plugin.cs').read_text()
ROUTER=(ROOT/'src/LargerMaps/LocalTacticalRouting.cs').read_text()
NATIVE=(ROOT/'src/LargerMaps/ExtendedPathfinding.cs').read_text()

class TestNativeAiCandidateLimits(unittest.TestCase):
    def test_feature_only_for_large_maps(self):
        self.assertIn('TargetSize >= ZoneReferenceSize && ReadBool("UseLocalTacticalRouting", false)',ROUTER)
    def test_existing_ai_config_not_replaced(self):
        self.assertIn('GetField("WEIGHTS"',ROUTER)
        self.assertIn('GetMember(weights, "NearTileLimit")',ROUTER)
        self.assertIn('GetMember(weights, "CullTilesDistances")',ROUTER)
    def test_no_global_movement_patch(self):
        for dangerous in ('GetPath(', 'MovementSystem', 'PathfindingProcess', 'harmony.Patch', 'HarmonyPrefix'):
            self.assertNotIn(dangerous, ROUTER)
        self.assertNotIn('TryInstall(', ROUTER)
    def test_local_ai_settings_reversible(self):
        self.assertIn('RestoreLocalTacticalRouting()',ROUTER)
        self.assertIn('TrySetMember(_localTacticalWeights, "NearTileLimit", _localTacticalOriginalNear)',ROUTER)
        self.assertIn('TrySetMember(_localTacticalWeights, "CullTilesDistances", _localTacticalOriginalCull)',ROUTER)
        self.assertIn('public void OnUnload() { RestoreSimplifiedCombatPlanning(); RestoreLocalTacticalRouting(); AdvanceRecoveryReset(); }',PLUGIN)
    def test_runtime_hook_only_scene_callback(self):
        tactical = PLUGIN.split('if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))', 1)[1].split('else if', 1)[0]
        self.assertIn('ApplySimplifiedCombatPlanning();', tactical)
        self.assertIn('ApplyLocalTacticalRouting();', tactical)
        self.assertNotIn('nameof(LocalTactical',PLUGIN)
    def test_settings_default(self):
        self.assertIn('AddToggle("UseLocalTacticalRouting"',PLUGIN)
        self.assertIn('"LocalNearTileLimit", "Nearby destinations inspected", 12f, 64f, 20f',PLUGIN)
        self.assertIn('"LocalCullTileDistance", "Cull distance (tiles)", 10f, 20f, 12f',PLUGIN)
    def test_prior_native_path_backend_retains_reference_stride_site(self):
        # Version-specific native plans are exercised by test_extended_pathfinding.
        self.assertIn('new(NativeMethod.PathfindingProcessFindPath, 0x2FE, 2, "C1E106", 0x07, 0x07',NATIVE)
    def test_version(self):
        self.assertNotIn('private const string Version', PLUGIN)
        self.assertEqual(json.loads((ROOT/'../jiangyu/LargerMaps/jiangyu.json').read_text())['version'],'2.6.2')

if __name__=='__main__': unittest.main()

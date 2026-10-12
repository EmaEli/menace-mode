"""Offline 1.5 contracts. These do NOT replace a live MENACE runtime test."""
import json
import re
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
PLUGIN=(ROOT/'src/LargerMaps/Plugin.cs').read_text()
ROUTER=(ROOT/'src/LargerMaps/LocalTacticalRouting.cs').read_text()
NATIVE=(ROOT/'src/LargerMaps/ExtendedPathfinding.cs').read_text()
REFERENCE=Path('/mnt/data/local_routing_work/LargerMaps-1.4.13-conservative-areas-authoring/src/LargerMaps/ExtendedPathfinding.cs')

class TestNativeAiCandidateLimits(unittest.TestCase):
    def test_feature_only_for_large_maps(self):
        self.assertIn('TargetSize > ZoneReferenceSize && ReadBool("UseLocalTacticalRouting", false)',ROUTER)
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
        self.assertIn('public void OnUnload() { RestoreSimplifiedCombatPlanning(); RestoreLocalTacticalRouting(); }',PLUGIN)
    def test_runtime_hook_only_scene_callback(self):
        self.assertIn('if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))\n        {\n            ApplySimplifiedCombatPlanning();\n            ApplyLocalTacticalRouting();',PLUGIN)
        self.assertNotIn('nameof(LocalTactical',PLUGIN)
    def test_settings_default(self):
        self.assertIn('AddToggle("UseLocalTacticalRouting"',PLUGIN)
        self.assertIn('"LocalNearTileLimit", "Maximum nearby destinations inspected for AI movement", 12f, 64f, 20f',PLUGIN)
        self.assertIn('"LocalCullTileDistance", "Cull distant tactical positions (tiles)", 10f, 20f, 12f',PLUGIN)
    def test_prior_native_path_backend_exact(self):
        if REFERENCE.exists():
            self.assertEqual(NATIVE,REFERENCE.read_text())
        self.assertIn('new(0x69C9AE, 2, "C1E106", 0x07, 0x07',NATIVE)
    def test_version(self):
        self.assertIn('private const string Version = "1.8";', PLUGIN)
        self.assertEqual(json.loads((ROOT/'modpack/LargerMaps/modpack.json').read_text())['version'],'1.8')

if __name__=='__main__': unittest.main()

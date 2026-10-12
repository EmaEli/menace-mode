import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / 'src/LargerMaps/Plugin.cs').read_text()
STAMP = (ROOT / 'src/LargerMaps/TerrainStampDensity.cs').read_text()

class StampDensityTests(unittest.TestCase):
    def test_default_off_and_optional_diagnostics(self):
        self.assertIn('AddToggle("IncreaseTerrainStampDensity"', PLUGIN)
        self.assertIn('false);', PLUGIN)
        self.assertIn('AddToggle("DiagnoseTerrainStampDensity"', PLUGIN)
        self.assertIn('PatchTerrainStampDensity(harmony);', PLUGIN)
        self.assertIn('if (!_increaseTerrainStamps && !_diagnoseTerrainStamps) return;', STAMP)

    def test_only_stamp_attempt_argument_changes(self):
        self.assertIn('m.Name != "SpawnStampsFromList"', STAMP)
        self.assertIn('private static void StampCountPrefix(ref int __1)', STAMP)
        self.assertIn('__1 = ScaleStampAttempts(__1, TargetSize, _terrainStampMultiplier);', STAMP)
        self.assertNotIn('TrySetMember(', STAMP)
        self.assertNotIn('SpawnStamp(', STAMP)
        self.assertIn('Math.Min(MaximumStampAttemptsPerGroup, scaled)', STAMP)

    def test_unchanged_64x64_and_clamped_ramp(self):
        self.assertIn('mapSize <= StampDensityReferenceSize', STAMP)
        self.assertIn('float ramp = Math.Clamp(', STAMP)
        self.assertIn('Math.Clamp(configuredMultiplier, 1f, 1.5f)', STAMP)
        self.assertIn('MaximumStampDensityDiagnosticLines = 10', STAMP)

if __name__ == '__main__':
    unittest.main()

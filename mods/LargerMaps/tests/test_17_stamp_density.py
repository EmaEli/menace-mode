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

    def test_harmony_type_disambiguated(self):
        self.assertIn('PatchTerrainStampDensity(HarmonyLib.Harmony harmony)', STAMP)
        self.assertNotIn('PatchTerrainStampDensity(Harmony harmony)', STAMP)

    def test_only_stamp_attempt_argument_changes(self):
        self.assertIn('m.Name != "SpawnStampsFromList"', STAMP)
        self.assertIn('private static void StampCountPrefix(ref int __1)', STAMP)
        self.assertIn('__1 = ScaleStampAttempts(__1, TargetSize, _terrainStampMultiplier);', STAMP)
        self.assertNotIn('TrySetMember(', STAMP)
        self.assertNotIn('SpawnStamp(', STAMP)
        self.assertIn('Math.Min(MaximumStampAttemptsPerGroup, scaled)', STAMP)

    def test_high_density_slider_and_safety_cap(self):
        self.assertIn('private const string Version = "1.8";', PLUGIN)
        self.assertIn('AddSlider("TerrainStampDensityMultiplier"', PLUGIN)
        self.assertIn('1f, 5f, 1.5f);', PLUGIN)
        self.assertIn('ReadFloat("TerrainStampDensityMultiplier", 1.5f)', STAMP)
        self.assertIn('MaximumStampAttemptsPerGroup = 200;', STAMP)
        self.assertIn('Math.Max(original, Math.Min(MaximumStampAttemptsPerGroup, scaled))', STAMP)
        self.assertIn('MaximumStampDensityDiagnosticLines = 10', STAMP)

    def test_5x_stamps_interpolate_by_size(self):
        # Reference arithmetic to guard the declared behavior of the C# scaler.
        # This does not execute the native game or validate map quality.
        def scale(original, size, multiplier):
            if original <= 0 or size <= 64 or multiplier <= 1:
                return original
            ramp = max(0.0, min(1.0, (size - 64) / 32.0))
            effective = 1.0 + (max(1.0, min(5.0, multiplier)) - 1.0) * ramp
            return max(original, min(200, int(original * effective + 0.5)))

        self.assertEqual(scale(4, 96, 5.0), 20)
        self.assertEqual(scale(10, 96, 5.0), 50)
        self.assertEqual(scale(4, 96, 3.0), 12)
        self.assertEqual(scale(10, 96, 3.0), 30)
        self.assertEqual(scale(10, 96, 1.5), 15)
        self.assertEqual(scale(10, 80, 5.0), 30)
        self.assertEqual(scale(10, 64, 5.0), 10)
        self.assertEqual(scale(0, 96, 5.0), 0)
        self.assertEqual(scale(100, 96, 5.0), 200)
        self.assertEqual(scale(250, 96, 5.0), 250)  # native count never reduced

    def test_unchanged_64x64_and_clamped_ramp(self):
        self.assertIn('mapSize <= StampDensityReferenceSize', STAMP)
        self.assertIn('float ramp = Math.Clamp(', STAMP)
        self.assertIn('Math.Clamp(configuredMultiplier, 1f, 5f)', STAMP)
        self.assertIn('Math.Clamp(configured, 1f, 5f)', STAMP)
        self.assertIn('MaximumStampDensityDiagnosticLines = 10', STAMP)

if __name__ == '__main__':
    unittest.main()

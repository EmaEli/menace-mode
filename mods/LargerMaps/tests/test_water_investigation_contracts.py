"""MEN-008 coordinate/source guards, NOT water/shoreline rendering verification."""
import unittest
from pathlib import Path

SOURCE = (Path(__file__).resolve().parents[1] / "src/LargerMaps/Plugin.cs").read_text()
RESOLVER = SOURCE.split("private static bool TryResolveTerrainPointManaged(", 1)[1].split("private static", 1)[0]
GAMEPLAY = SOURCE.split("private static bool GameplayPointReprojectionPrefix(", 1)[1].split("private static", 1)[0]


class WaterInvestigationContracts(unittest.TestCase):
    def test_managed_and_gameplay_paths_sample_heightmap_coordinates(self):
        for section in (RESOLVER, GAMEPLAY):
            self.assertIn("float heightScale = (heightMapRes - 1f) / totalWorld;", section)
            self.assertIn("Math.Clamp(", section)
            self.assertIn("GetWorldHeightBilinear(sampleX, sampleZ)", section)
            self.assertIn("heightMapRes < 2", section)

    def test_managed_point_reprojection_is_flagged_not_unconditional(self):
        self.assertIn("TerrainPointFlags.CanBeReprojected", RESOLVER)
        self.assertIn("TerrainPointFlags.IsInfill", RESOLVER)
        self.assertLess(RESOLVER.index("if (canBeReprojected || isInfill)"), RESOLVER.index("float newY"))
        for field in ("source.Orientation", "source.Scale", "source.AssetTypeID", "source.Flags"):
            self.assertIn(field, RESOLVER)
        self.assertIn("__result = __0.Position.y;", GAMEPLAY)

    def test_coordinate_model_endpoints_and_bounds_not_world_y(self):
        # Values are coordinate-model inputs, not measurements from a lake prefab.
        for resolution in (129, 513, 1025):
            for total_world in (512, 688, 944):
                border = 88
                def sample(world):
                    return max(0, min(resolution - 1, (world + border) * (resolution - 1) / total_world))
                self.assertEqual(sample(-border), 0)
                self.assertAlmostEqual(sample(total_world / 2 - border), (resolution - 1) / 2)
                self.assertEqual(sample(total_world - border), resolution - 1)
                self.assertEqual(sample(-10000), 0)
                self.assertEqual(sample(10000), resolution - 1)

    def test_existing_diagnostics_and_density_are_opt_in_and_capped(self):
        self.assertIn('AddToggle("IncreaseTerrainStampDensity", "More terrain stamps on maps above 64x64", false)', SOURCE)
        self.assertIn('AddToggle("DiagnoseTerrainStampDensity", "Log terrain stamp counts", false)', SOURCE)
        self.assertIn("Debug() && _terrainPointFixLogs < 24", RESOLVER)
        self.assertIn("Debug() && _gameplayPointReprojectionLogs < 16", GAMEPLAY)


if __name__ == "__main__":
    unittest.main()
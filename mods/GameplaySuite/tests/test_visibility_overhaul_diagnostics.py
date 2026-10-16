"""Issue #46 source guards; they do not validate native MENACE visibility behavior."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / "src/VisibilityOverhaul/Plugin.cs").read_text()
PATCHES = (ROOT / "src/VisibilityOverhaul/Patches.cs").read_text()
DIAGNOSTICS = (ROOT / "src/VisibilityOverhaul/PirateVisibilityDiagnostics.cs").read_text()
PACKAGE = ROOT.parent / "jiangyu/VisibilityOverhaul"


class VisibilityOverhaulDiagnosticsTests(unittest.TestCase):
    def test_diagnostics_are_opt_in_and_guarded_by_debug_logging(self):
        self.assertIn('AddToggle("DebugLogging", "Debug logging", false)', PLUGIN)
        self.assertIn('AddToggle("PirateVisibilityDiagnostics", "Pirate visibility diagnostics", false)', PLUGIN)
        self.assertIn("RuntimeSettings.DebugLogging && RuntimeSettings.PirateVisibilityDiagnostics", DIAGNOSTICS)
        self.assertIn("if (!Enabled || observer == null || target == null) return;", DIAGNOSTICS)

    def test_diagnostics_observe_native_calls_and_are_bounded(self):
        self.assertIn('nameof(Actor.HasLineOfSightTo), new[] { typeof(Entity), typeof(Tile), typeof(Tile) }', PLUGIN)
        self.assertIn('"IsHiddenToPlayerAtTile", new[] { typeof(Tile) }', PLUGIN)
        self.assertIn("PirateVisibilityDiagnostics.ObserveVisibilityCheck", PATCHES)
        self.assertIn("PirateVisibilityDiagnostics.ObserveFinalVisibility", PATCHES)
        self.assertIn("MaxPirateSamples = 3", DIAGNOSTICS)
        self.assertIn("MaxEvents = 100", DIAGNOSTICS)
        self.assertIn('"missionSeed=unavailable activeMods=unavailable', DIAGNOSTICS)
        self.assertIn('terrain=unavailable cover=unavailable finalUiVisibility=unavailable turn=unavailable', DIAGNOSTICS)
        self.assertIn('geometricLOS=unavailable', DIAGNOSTICS)
        self.assertIn('coordinateTileSpan={(tileSpan >= 0 ? tileSpan.ToString() : "unavailable")}', DIAGNOSTICS)
        self.assertIn('properties=0x{snapshot.PropertiesPointer.ToInt64():X},skillUpdateCall={snapshot.UpdateCall}', DIAGNOSTICS)
        self.assertIn('voVisionApplicationsInUpdateCall=', DIAGNOSTICS)
        self.assertIn('voConcealmentApplicationsInUpdateCall=', DIAGNOSTICS)

    def test_property_observation_does_not_replace_gameplay_modifiers(self):
        self.assertIn("_properties.Vision + visionBonus", PATCHES)
        self.assertIn("StationaryConcealmentBonus\n                           - moved * RuntimeSettings.ConcealmentLossPerTile", PATCHES)
        self.assertIn("_properties.Concealment += modifier", PATCHES)
        self.assertIn("properties.GetVision()", DIAGNOSTICS)
        self.assertIn("properties.GetDetection()", DIAGNOSTICS)
        self.assertIn("properties.GetConcealment()", DIAGNOSTICS)
        self.assertNotIn("UpdateVisibilityForActor", PLUGIN + PATCHES)

    def test_versions_match_for_the_diagnostic_feature(self):
        version = json.loads((PACKAGE / "jiangyu.json").read_text())["version"]
        project = (PACKAGE / "code/VisibilityOverhaul.csproj").read_text()
        self.assertIn(f'AssemblyVersion("{version}.0")', PLUGIN)
        self.assertIn(f'AssemblyFileVersion("{version}.0")', PLUGIN)
        self.assertIn(f'internal const string Version = "{version}"', PLUGIN)
        self.assertIn(f"<Version>{version}</Version>", project)


if __name__ == "__main__":
    unittest.main()
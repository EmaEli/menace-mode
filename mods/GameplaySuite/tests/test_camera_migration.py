"""Camera-only/Jiangyu settings wiring; real camera and UI behavior need the game."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CAM = ROOT / "mods/ThirdPersonCamera"
JY = ROOT / "mods/jiangyu/CinematicActionCamera"


class CameraMigrationTests(unittest.TestCase):
    def test_single_camera_owner_and_preserved_identity(self):
        source = (CAM / "src/ThirdPersonCameraMod.cs").read_text()
        companion = (JY / "code/CameraSettingsSystem.cs").read_text()
        self.assertIn("class ThirdPersonCameraMod : MelonMod", source)
        self.assertIn('SettingsCategory = "MenaceThirdPersonCamera"', source)
        self.assertIn("public static bool IsCinematicViewActive", source)
        self.assertIn("class CameraSettingsSystem : JiangyuSystem", companion)
        for forbidden in ("new Harmony", "Skill.Use", "OnLateUpdate", "SetActionPoints"):
            self.assertNotIn(forbidden, companion)
        self.assertEqual(companion.count("s.AddSlider("), 17)
        self.assertEqual(companion.count("s.AddToggle("), 2)

    def test_settings_priority_and_bounded_lifecycle(self):
        source = (CAM / "src/ThirdPersonCameraMod.cs").read_text()
        self.assertLess(source.index('"MenaceModSettings.ModSettings"'), source.index('"Menace.SDK.ModSettings"'))
        companion = (JY / "code/CameraSettingsSystem.cs").read_text()
        self.assertIn("attempt < 120", companion)
        self.assertIn("Context.Coroutines.Stop(_migration)", companion)
        self.assertIn("ModSettings.ImportMissing(Group, values)", companion)
        self.assertIn("GetLegacySettingsValues", source)

    def test_versions_are_aligned(self):
        version = json.loads((JY / "jiangyu.json").read_text())["version"]
        for project in (CAM / "ThirdPersonCamera.csproj", JY / "code/CinematicActionCamera.csproj"):
            self.assertIn(f"<Version>{version}</Version>", project.read_text())
        self.assertIn('"Cinematic Action Camera", "' + version + '"', (CAM / "src/ThirdPersonCameraMod.cs").read_text())


if __name__ == "__main__":
    unittest.main()
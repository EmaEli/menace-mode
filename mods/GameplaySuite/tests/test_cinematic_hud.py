"""MEN-006: both rendering and input geometry must observe the optional camera."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = ROOT.parent / "jiangyu"


class CinematicHudTests(unittest.TestCase):
    def test_camera_check_is_in_shared_render_and_input_provider(self):
        for module, provider, next_method in (
            ("Overwatch", "GetHudRect", "private void DrawGUI"),
            ("CoordinatedFire", "GetHudLayout", "private Rect? GetInputRect"),
        ):
            with self.subTest(module=module):
                source = (ROOT / f"src/{module}/Plugin.cs").read_text()
                body = source[source.index(f" {provider}()\n"):]
                body = body[:body.index(next_method)]
                self.assertIn("if (ThirdPersonCameraBridge.IsActive) return null;", body)
                self.assertLess(body.index("ThirdPersonCameraBridge.IsActive"),
                                body.index("TacticalManager.Get()"))
                self.assertIn(f"var {'rect' if module == 'Overwatch' else 'currentLayout'} = {provider}();", source)
                self.assertIn("HudInputGuard.Register(this, Get", source)
                if module == "CoordinatedFire":
                    self.assertIn("var layout = GetHudLayout();", source)

    def test_shipped_versions_match_metadata(self):
        for module in ("Overwatch", "CoordinatedFire"):
            with self.subTest(module=module):
                version = json.loads((PACKAGES / module / "jiangyu.json").read_text())["version"]
                project = (PACKAGES / module / f"code/{module}.csproj").read_text()
                source = (ROOT / f"src/{module}/Plugin.cs").read_text()
                self.assertIn(f"<Version>{version}</Version>", project)
                self.assertIn(f'private const string Version = "{version}"', source)
                assembly_version = version if len(version.split(".")) == 4 else version + ".0"
                self.assertIn(f'AssemblyVersion("{assembly_version}")', source)


if __name__ == "__main__":
    unittest.main()
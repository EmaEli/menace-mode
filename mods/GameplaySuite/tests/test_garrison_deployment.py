"""Exercise production native-deployment hooks with managed native API stand-ins."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class GarrisonDeploymentTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK is not installed")
    def test_production_native_deployment_transitions(self):
        project = ROOT / "mods/GameplaySuite/tests/GarrisonDeploymentChecks/GarrisonDeploymentChecks.csproj"
        result = subprocess.run(
            [DOTNET, "run", "--project", str(project), "-c", "Release", "-v", "quiet"],
            cwd=ROOT, text=True, capture_output=True, timeout=120,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Garrison deployment checks passed:", result.stdout)

    def test_attack_only_accuracy_override_is_removed(self):
        source = (ROOT / "mods/GameplaySuite/src/GarrisonableBuildings/Plugin.cs").read_text()
        self.assertIn("InstallGarrisonDeploymentHooks(harmony)", source)
        self.assertNotIn("GetHitchance", source)
        self.assertNotIn("GarrisonAccuracy", source)


if __name__ == "__main__":
    unittest.main()
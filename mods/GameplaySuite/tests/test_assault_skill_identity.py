"""Exercise the production identity guard; stand-ins model a miswrapped native receiver."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class AssaultSkillIdentityTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK is not available")
    def test_production_identity_guard(self):
        project = ROOT / "mods/GameplaySuite/tests/AssaultIdentityChecks/AssaultIdentityChecks.csproj"
        result = subprocess.run(
            [DOTNET, "run", "--project", str(project), "-c", "Release"],
            cwd=ROOT, text=True, capture_output=True, timeout=60,
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Assault identity checks passed: 20", result.stdout)


if __name__ == "__main__":
    unittest.main()
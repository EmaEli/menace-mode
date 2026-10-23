import os
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class GrenadeRuntimeTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK is not installed")
    def test_production_runtime_hooks_against_native_field_standins(self):
        project = ROOT / "mods/GameplaySuite/tests/GrenadeRuntimeChecks/GrenadeRuntimeChecks.csproj"
        result = subprocess.run(
            [DOTNET, "run", "--project", str(project), "-c", "Release"],
            cwd=ROOT, text=True, capture_output=True, timeout=120,
            env={**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"},
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Grenade runtime checks passed:", result.stdout)


if __name__ == "__main__":
    unittest.main()
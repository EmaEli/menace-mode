"""Execute production construction hooks and overlay with managed native/Unity stand-ins."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class SandbagConstructionTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), "requires the existing .NET SDK")
    def test_production_construction_and_markers(self):
        project = ROOT / "mods/jiangyu/InfantrySandbagCover/tests/OfflineChecks/OfflineChecks.csproj"
        result = subprocess.run([DOTNET, "run", "--project", str(project), "-c", "Release", "--tl:off"],
                                capture_output=True, text=True, cwd=ROOT, timeout=90)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("sandbag production runtime/marker checks", result.stdout)


if __name__ == "__main__":
    unittest.main()
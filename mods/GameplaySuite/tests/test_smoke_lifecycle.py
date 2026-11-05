"""Execute production observation logic against managed native-object stand-ins."""
import os
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]


class SmokeLifecycleTests(unittest.TestCase):
    def test_production_lifecycle_observations(self):
        dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
        if not Path(dotnet).is_file():
            self.skipTest(".NET SDK unavailable")
        result = subprocess.run(
            [dotnet, "run", "--project", str(Path(__file__).parent / "SmokeLifecycleChecks/SmokeLifecycleChecks.csproj"), "-c", "Release"],
            cwd=ROOT, text=True, capture_output=True, timeout=120,
            env={**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("PASS: 19 production smoke lifecycle", result.stdout)

    def test_delivery_templates_use_native_casts(self):
        plugin = (ROOT / "mods/GameplaySuite/src/SmokeOverhaul/Plugin.cs").read_text()
        self.assertIn("?.TryCast<SpawnTileEffect>()", plugin)
        self.assertEqual(plugin.count("?.TryCast<Cooldown>()"), 2)
        self.assertIn("!IsSmoke(spawn.EffectToSpawn)", plugin)
        self.assertIn("postfix: nameof(Patches.TileEffectHandler_AssignToTile_Postfix)", plugin)
        self.assertIn("UpdateSmokeLifecycleDiagnostics(now)", plugin)


if __name__ == "__main__":
    unittest.main()
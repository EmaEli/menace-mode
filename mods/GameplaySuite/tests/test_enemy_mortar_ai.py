"""Execute the production mortar AI hooks against managed native stand-ins."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class EnemyMortarAiTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_runtime_and_mission_lifecycle(self):
        result = subprocess.run([DOTNET, "run", "--project",
                                 str(ROOT / "tests/EnemyMortarAiChecks/EnemyMortarAiChecks.csproj"),
                                 "-c", "Release", "--nologo", "-v", "quiet"],
                                capture_output=True, text=True, timeout=90)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("enemy mortar AI managed checks", result.stdout)

    def test_settings_and_scene_cleanup_are_wired(self):
        source = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()
        self.assertIn('s.AddToggle("EnemyMortarFirePriority", "Enemy mortars prefer sustained fire to movement", true)', source)
        self.assertIn('s.AddSlider("EnemyMortarFireRounds",', source)
        scene = source.split("public void OnSceneLoaded", 1)[1].split("public void OnUpdate", 1)[0]
        self.assertIn("EnemyMortarMissions.Clear();", scene)
        self.assertIn("if (!_enabled || !_enemyMortarFirePriority) EnemyMortarMissions.Clear();", source)
"""Execute actual inventory debug sources with managed native stand-ins (#65)."""
import json
from pathlib import Path
import shutil
import subprocess
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
MOD = ROOT / "mods/jiangyu/MenaceModSettings"
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class InventoryDebugTests(unittest.TestCase):
    def test_runtime_with_native_stand_ins(self):
        if not Path(DOTNET).is_file():
            self.skipTest(".NET SDK unavailable")
        result = subprocess.run([
            DOTNET, "run", "--project", str(MOD / "tests/InventoryDebugChecks/InventoryDebugChecks.csproj"),
            "-c", "Release"], cwd=ROOT, text=True, capture_output=True, timeout=120)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Inventory debug checks:", result.stdout)

    def test_manual_confirmation_and_version_identity(self):
        source = (MOD / "code/SettingsWindowSystem.cs").read_text()
        self.assertIn('if (current.Name == OwnGroup) RenderInventoryDebug();', source)
        self.assertIn('_inventoryConfirmation != state.Pointer', source)
        self.assertIn('_inventoryStatus = InventoryDebugActions.Grant(confirmed);', source)
        self.assertIn('button.Root.SetEnabled(available);', source)
        self.assertIn('new TextButton("CANCEL")', source)
        self.assertNotIn('InventoryDebugActions.Grant', source.split('public override void OnInit()')[1].split('public override void OnUnload()')[0])
        project = ET.parse(MOD / "code/MenaceModSettings.csproj")
        self.assertEqual(project.findtext("PropertyGroup/AssemblyVersion"), "1.0.0.0")
        self.assertEqual(project.findtext("PropertyGroup/Version"), json.loads((MOD / "jiangyu.json").read_text())["version"])


if __name__ == "__main__":
    unittest.main()
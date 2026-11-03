"""Source/packaging safety guards; runtime behavior lives in the probe's C# tests."""
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[3]
MOD = ROOT / "mods/jiangyu/TacticalRoundCheckpoint"
TOOLS = ROOT / "mods/GameplaySuite/tools"


class TacticalRoundCheckpointChecks(unittest.TestCase):
    def test_probe_is_opt_in_and_capture_only(self):
        catalog = json.loads((TOOLS / "distribution.json").read_text())
        self.assertIn("TacticalRoundCheckpoint", catalog["experimentalModules"])
        self.assertNotIn("TacticalRoundCheckpoint", catalog["modules"])
        source = (MOD / "code/RoundCaptureSystem.cs").read_text()
        self.assertIn(catalog["gameAssemblySha256"], source)
        self.assertIn('"NextRound", Type.EmptyTypes', source)
        self.assertIn("priority = Priority.Last", source)
        self.assertIn("RequireMainThread()", source)
        self.assertIn("CAPTURE ONLY, NOT AUTOSAVE", source)
        self.assertIn("_MENACE_Diagnostics", source)
        self.assertNotRegex(source, r"SaveSystem|SaveState|\.Set[A-Z]|\.OnRoundStart\(")

    def test_installer_preserves_installed_companions(self):
        spec = importlib.util.spec_from_file_location("checkpoint_build_kit", TOOLS / "build_kit.py")
        builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(builder)
        payload, versions = builder.collect(ROOT, "TacticalRoundCheckpoint", True)
        self.assertEqual(versions, {"TacticalRoundCheckpoint": "0.1.0"})
        self.assertFalse(any("/MenaceModSettings/" in name for name in payload))
        launcher = payload["INSTALL-TacticalRoundCheckpoint.cmd"].decode()
        self.assertIn("-Modules TacticalRoundCheckpoint -UseInstalledSettings", launcher)
        installer = payload["source/mods/GameplaySuite/tools/Install-IntegratedSuite.ps1"].decode()
        supported = re.search(r"\$supportedSettingsModules = @\(([^\n]+)\)", installer).group(1)
        self.assertIn("'TacticalRoundCheckpoint'", supported)
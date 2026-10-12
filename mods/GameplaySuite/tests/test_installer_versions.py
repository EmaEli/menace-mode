"""Regression tests for installer version preflight (MEN-001, installer-only fix).

Runs without MENACE or PowerShell; Windows end-to-end install remains mandatory.
"""
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src/SmokeOverhaul/SmokeOverhaul.csproj"
MANIFEST = ROOT / "packaging/standalone/SmokeOverhaul/modpack.json"
SRC = ROOT / "src/SmokeOverhaul/Plugin.cs"
PS = ROOT / "tools/Install-Component.ps1"
CMD = ROOT / "INSTALL-MEN001-ONECLICK.cmd"


class InstallerVersionRegression(unittest.TestCase):
    def test_xml_project_manifest_and_assembly_agree(self):
        version = str(json.loads(MANIFEST.read_text(encoding="utf-8"))["version"])
        project = ET.parse(PROJECT)
        self.assertEqual(project.findtext("PropertyGroup/Version"), version)
        expected_dll_version = version + ".0"
        code = SRC.read_text(encoding="utf-8")
        self.assertIn(f'AssemblyVersion("{expected_dll_version}")', code)
        self.assertIn(f'internal const string Version = "{version}";', code)

    def test_ps_preflight_reads_actual_project_version_not_a_stale_literal(self):
        installer = PS.read_text(encoding="utf-8-sig")
        self.assertIn("[xml]$projectXml = Get-Content -LiteralPath $project -Raw", installer)
        self.assertIn("$projectXml.SelectSingleNode('/Project/PropertyGroup/Version')", installer)
        self.assertIn("$projectVersion -ne $Version", installer)
        self.assertIn("$ExpectedAssemblyVersion = $ExpectedVersion + '.0'", installer)
        self.assertNotIn("notmatch '<Version>", installer)
        self.assertNotIn("<Version>0\\.6\\.1</Version>", installer)
        self.assertIn("$expectedCodeVersion", installer)
        self.assertIn("$expectedAssemblyAttribute", installer)
        self.assertEqual(installer.count("Assert-DllVersion "), 4)  # definition + 3 validations

    def test_single_launcher_pins_sources_and_preserves_windows_diagnostics(self):
        installer = PS.read_text(encoding="utf-8-sig")
        launcher = CMD.read_text(encoding="utf-8-sig")
        version = str(json.loads(MANIFEST.read_text(encoding="utf-8"))["version"])
        self.assertIn(f"-ExpectedVersion {version}", launcher)
        self.assertRegex(launcher, r'set "PIN=[0-9a-f]{40}"')
        self.assertRegex(launcher, r'set "BOOTSTRAP_HASH=[A-F0-9]{64}"')
        self.assertIn('cmd.exe /k call', launcher)
        self.assertIn('notepad.exe', launcher)
        self.assertIn('Start-Transcript', installer)
        self.assertIn('_MENACE_Backups', installer)
        self.assertIn("ROLLBACK SUCCEEDED", installer)
        self.assertIn("Get-FileHash", installer)
        self.assertIn("Invoke-WebRequest", installer)


if __name__ == "__main__":
    unittest.main()

"""ZIP builder and installer source guards; these do not execute PowerShell or Windows."""
import importlib.util
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TOOLS = ROOT / "mods/GameplaySuite/tools"
SPEC = importlib.util.spec_from_file_location("build_kit", TOOLS / "build_kit.py")
BUILDER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BUILDER)
import sys
sys.modules["build_kit"] = BUILDER
VERIFY_SPEC = importlib.util.spec_from_file_location("verify_kit", TOOLS / "verify_kit.py")
VERIFIER = importlib.util.module_from_spec(VERIFY_SPEC)
VERIFY_SPEC.loader.exec_module(VERIFIER)


class DistributionTests(unittest.TestCase):
    def fixture(self, root):
        def write(name, content):
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content)
        write("mods/GameplaySuite/tools/distribution.json", json.dumps({"modules": ["Example"]}))
        manifest = json.dumps({"name": "Example", "version": "1.2.3"})
        write("mods/jiangyu/Example/jiangyu.json", manifest)
        write("mods/jiangyu/Example/compiled/jiangyu.json", manifest)
        write("mods/jiangyu/Example/code/Example.csproj", '<Project><PropertyGroup><Version>1.2.3</Version></PropertyGroup></Project>')
        write("mods/jiangyu/Example/compiled/code/Example.dll", "test stand-in; real PE metadata is checked by the C# runner")
        write("mods/jiangyu/Example/code/local.props", "machine-local fixture, must not ship")
        write("mods/jiangyu/Example/code/bin/ignored.cs", "generated")
        write("mods/jiangyu/Example/compiled/code/Example.pdb", "debug fixture")
        for file in ("Install-IntegratedSuite.ps1", "Install.Common.ps1", "Install.Deployment.ps1"):
            write("mods/GameplaySuite/tools/" + file, "# test fixture")
        write("mods/GameplaySuite/INSTALL-SUITE.cmd", "@echo off\n")
        write("mods/GameplaySuite/docs/INSTALLING.md", "Offline fixture")
        write("docs/MIGRATION_STATUS.md", "Offline migration fixture")
        for file in ("ThirdPersonCamera.csproj", "src/ThirdPersonCameraMod.cs", "README.md"):
            write("mods/ThirdPersonCamera/" + file, "test fixture")

    def test_archive_is_reproducible_and_fully_inventoried(self):
        payload = {"source/test.cs": b"example", "INSTALL-SUITE.cmd": b"@echo off"}
        first, kit = BUILDER.make_archive(payload, {"Example": "1.2.3"}, "test-base")
        second, _ = BUILDER.make_archive(payload, {"Example": "1.2.3"}, "test-base")
        self.assertEqual(first, second)
        with zipfile.ZipFile(io.BytesIO(first)) as archive:
            self.assertIsNone(archive.testzip())
            self.assertEqual(set(archive.namelist()), set(payload) | {"kit.json"})
            for entry in kit["files"]:
                self.assertEqual(BUILDER.digest(archive.read(entry["path"])), entry["sha256"])

    def test_unsafe_archive_paths_are_rejected(self):
        for name in ("../bad", "/absolute", "source/../bad", "C:/absolute", "source\\bad"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                BUILDER.make_archive({name: b"test"}, {}, "test-base")

    def test_collection_excludes_machine_and_debug_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            self.fixture(root)
            payload, versions = BUILDER.collect(root)
            self.assertEqual(versions, {"Example": "1.2.3"})
            self.assertFalse(any("local.props" in n or "/bin/" in n or n.endswith(".pdb") for n in payload))
            self.assertIn("source/mods/jiangyu/Example/compiled/code/Example.dll", payload)
            self.assertIn("source/docs/MIGRATION_STATUS.md", payload)
            self.assertEqual(payload["INSTALL-SUITE.cmd"], b"@echo off\r\n")

    def test_manifest_mismatch_and_extra_dll_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            self.fixture(root)
            path = root / "mods/jiangyu/Example/compiled/jiangyu.json"
            original = path.read_text()
            path.write_text(json.dumps({"name": "Example", "version": "9.0.0"}))
            with self.assertRaisesRegex(ValueError, "Manifest mismatch"):
                BUILDER.collect(root)
            path.write_text(original)
            (path.parent / "code/Other.dll").write_text("unexpected")
            with self.assertRaisesRegex(ValueError, "unexpected compiled DLLs"):
                BUILDER.collect(root)

    def test_symlinks_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            self.fixture(root)
            (root / "mods/jiangyu/Example/code/link.cs").symlink_to(root / "mods/ThirdPersonCamera/src/ThirdPersonCameraMod.cs")
            with self.assertRaisesRegex(ValueError, "Symlink"):
                BUILDER.collect(root)

    def test_catalog_covers_all_current_modules(self):
        catalog = json.loads((TOOLS / "distribution.json").read_text())
        actual = {p.parent.name for p in (ROOT / "mods/jiangyu").glob("*/jiangyu.json")}
        self.assertEqual(set(catalog["modules"] + catalog.get("experimentalModules", [])), actual)
        self.assertEqual(catalog["modules"][0], "MenaceModSettings")
        self.assertIn("CinematicActionCamera", actual)
        self.assertNotIn("SquaddieFieldPromotions", catalog["modules"])

    def test_preview_uses_canonical_standalone_launcher_without_replacing_settings(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            self.fixture(root)
            catalog_path = root / "mods/GameplaySuite/tools/distribution.json"
            catalog = json.loads(catalog_path.read_text())
            catalog["experimentalModules"] = ["SquaddieFieldPromotions"]
            catalog_path.write_text(json.dumps(catalog))
            preview = root / "mods/jiangyu/SquaddieFieldPromotions"
            (preview / "compiled").mkdir(parents=True)
            manifest = json.dumps({"name": "SquaddieFieldPromotions", "version": "0.8.0"})
            (preview / "jiangyu.json").write_text(manifest)
            (preview / "compiled/jiangyu.json").write_text(manifest)
            (root / "mods/GameplaySuite/INSTALL-SUITE.cmd").write_text('cmd.exe /k\npowershell -BundleRoot "%~dp0."\n')
            with self.assertRaisesRegex(ValueError, "use-installed-settings"):
                BUILDER.collect(root, "SquaddieFieldPromotions")
            payload, versions = BUILDER.collect(root, "SquaddieFieldPromotions", True)
            self.assertEqual(versions, {"SquaddieFieldPromotions": "0.8.0"})
            launcher = payload["INSTALL-SquaddieFieldPromotions.cmd"].decode()
            self.assertIn("-Product Standalone -Modules SquaddieFieldPromotions -UseInstalledSettings", launcher)
            self.assertNotIn("INSTALL-SUITE.cmd", payload)
            self.assertFalse(any("/Example/" in path or "/MenaceModSettings/" in path for path in payload))
            with self.assertRaisesRegex(ValueError, "Unknown requested"):
                BUILDER.collect(root, "Unknown")
            with self.assertRaisesRegex(ValueError, "only by"):
                BUILDER.collect(root, "Example", True)

    def test_installed_settings_are_validated_without_being_staged(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        helper = (TOOLS / "Install.Common.ps1").read_text()
        self.assertIn("Get-InstalledSettingsReference $GamePath $minimum", source)
        self.assertIn("if (-not $UseInstalledSettings)", source)
        self.assertIn("if ($UseInstalledSettings) { Assert-Hash $settingsDll $settingsHash }", source)
        self.assertIn("-p:MenaceModSettingsPath=$settingsDll", source)
        self.assertIn("Assert-Dll $dll $manifest.version '1.0.0.0'", helper)
        self.assertIn("[version]$manifest.version -lt [version]$MinimumVersion", helper)

    def test_bootstrap_requires_all_three_hashes_before_execution(self):
        launcher = (ROOT / "mods/GameplaySuite/INSTALL-SUITE.cmd").read_text()
        self.assertIn("cmd.exe /k", launcher)
        self.assertIn("$f.Count -ne 1", launcher)
        for name in ("Install-IntegratedSuite.ps1", "Install.Common.ps1", "Install.Deployment.ps1"):
            self.assertIn(name, launcher)
        self.assertLess(launcher.index("Get-FileHash"), launcher.index(' -File "%~dp0source'))
        self.assertLess(launcher.index('starting.>"%LOG%"'), launcher.index("powershell.exe"))
        self.assertIn("notepad.exe", launcher)

    def test_builds_and_staging_precede_live_install(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        install = source.index("Install-StagedMods $GamePath")
        self.assertLess(source.index("Assert-Dll $dll $cameraVersion"), install)
        self.assertLess(source.index("Assert-NoUnknownDuplicates"), install)
        self.assertLess(source.index("if ($BuildOnly)"), install)
        self.assertIn("SelectSingleNode('/Project/PropertyGroup/Version')", source)
        self.assertIn("Missing project Version", source)
        self.assertNotIn("Menace.ModpackLoader.dll", source)
        self.assertIn("if (-not $BuildOnly -or $RollbackPath) { Assert-GameClosed }", source)
        self.assertIn("if ($BuildOnly -and -not $RollbackPath) { $lockRoot = $BundleRoot }", source)

    def test_private_sdk_pins_are_recorded_in_kit_manifest(self):
        pin = json.loads((TOOLS / "distribution.json").read_text())["dotnetSdk"]
        self.assertEqual(pin["channel"], "10.0")
        self.assertRegex(pin["version"], r"^10\.0\.1[0-9]{2}$")
        self.assertRegex(pin["archiveSha512"], r"^[a-f0-9]{128}$")
        self.assertRegex(pin["installScriptSha256"], r"^[a-f0-9]{64}$")
        self.assertEqual(pin["metadataUrl"], "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json")
        self.assertIn("/" + pin["version"] + "/dotnet-sdk-", pin["archiveUrl"])
        self.assertRegex(pin["installScriptUrl"], r"/dotnet/install-scripts/[a-f0-9]{40}/src/dotnet-install\.ps1$")
        archive, kit = BUILDER.make_archive({"source/example.cs": b"fixture"}, {}, "test-base", pin)
        with zipfile.ZipFile(io.BytesIO(archive)) as data:
            self.assertEqual(json.loads(data.read("kit.json"))["dotnetSdk"], pin)
        self.assertEqual(kit["dotnetSdk"], pin)

    def test_sdk_bootstrap_is_automatic_logged_and_before_builds(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        bootstrap = source.index("Initialize-PrivateDotnet $BundleRoot $kit.dotnetSdk")
        self.assertLess(source.index("Start-Transcript"), bootstrap)
        self.assertLess(bootstrap, source.index("Invoke-Dotnet @('build'"))
        self.assertIn("Kit/catalog SDK pin mismatch", source)
        self.assertIn("$processEnvironment = Save-BuildEnvironment", source)
        self.assertIn("Restore-BuildEnvironment $processEnvironment", source[source.index("finally {"):])
        self.assertNotIn("Install the .NET 10 SDK before running", source)

    def test_sdk_script_consumes_only_verified_local_archive(self):
        source = (TOOLS / "Install.Common.ps1").read_text()
        script = source.index("& $installer -Channel")
        self.assertLess(source.index("Get-VerifiedDownload $Pin.installScriptUrl"), script)
        self.assertLess(source.index("Get-VerifiedDownload $Pin.archiveUrl $archive $Pin.archiveSha512 'SHA512'"), script)
        self.assertLess(source.index("$drive.AvailableFreeSpace"), source.index("Get-VerifiedDownload $Pin.installScriptUrl"))
        self.assertIn("-InstallDir $root -NoPath -AzureFeed $feed", source)
        self.assertIn("Pinned private SDK already present; no download/extraction required", source)
        self.assertIn("$actual -ne $Pin.version", source)
        self.assertIn("Cached download integrity failed", source)

    def test_sdk_environment_is_process_scoped_and_restorable(self):
        source = (TOOLS / "Install.Common.ps1").read_text()
        self.assertIn("Join-Path $KitRoot 'tools'", source)
        self.assertIn("Join-Path $tools 'dotnet'", source)
        for variable in ("DOTNET_ROOT", "PATH", "DOTNET_CLI_TELEMETRY_OPTOUT", "DOTNET_NOLOGO", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE"):
            self.assertIn("$env:" + variable + " =", source)
        self.assertIn("[Environment]::SetEnvironmentVariable($name, $Saved[$name], 'Process')", source)
        for forbidden in ("msiexec", "Set-ItemProperty", "SetEnvironmentVariable($name, $Saved[$name], 'User')", "global.json"):
            self.assertNotIn(forbidden, source)

    def test_partial_copies_and_journal_errors_are_in_rollback_scope(self):
        source = (TOOLS / "Install.Deployment.ps1").read_text()
        self.assertLess(source.index("$entry.installed = $true"), source.index("Copy-Item -LiteralPath (Join-Path $Stage"))
        self.assertIn("Restore-ModBackup $Game $backup -Journal $journal", source)
        self.assertIn("Assert-Inventory $live @($entry.oldFiles)", source)
        self.assertIn("Rollback incomplete", source)
        self.assertIn("[switch]$CheckCurrent", source)
        self.assertNotIn("Remove-Item", source)
        self.assertEqual(source.count("    Assert-GameClosed"), 2)

    def test_version_padding_and_unknown_sources_guard(self):
        source = (TOOLS / "Install.Common.ps1").read_text()
        self.assertIn("while ($parts.Count -lt 4)", source)
        self.assertIn("Uninventoried source files", source)
        self.assertIn("Duplicate source inventory entry", source)
        self.assertIn("Renamed duplicate package", source)

    def test_verifier_detects_modified_payload(self):
        data, _ = BUILDER.make_archive({"source/example.cs": b"good"}, {}, "test-base")
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "kit.zip"
            path.write_bytes(data)
            files, _ = VERIFIER.read_archive(path)
            self.assertEqual(files["source/example.cs"], b"good")
            output = io.BytesIO()
            with zipfile.ZipFile(io.BytesIO(data)) as old, zipfile.ZipFile(output, "w") as new:
                for name in old.namelist():
                    new.writestr(name, b"changed" if name == "source/example.cs" else old.read(name))
            path.write_bytes(output.getvalue())
            with self.assertRaisesRegex(ValueError, "SHA256 mismatch"):
                VERIFIER.read_archive(path)


if __name__ == "__main__":
    unittest.main()
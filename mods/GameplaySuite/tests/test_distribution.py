"""ZIP builder and installer source guards; these do not execute PowerShell or Windows."""
import ast
import importlib.util
import io
import json
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch

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


def native_contract_modules():
    for node in ast.walk(ast.parse((TOOLS / "verify_kit.py").read_text())):
        if (isinstance(node, ast.If) and isinstance(node.test, ast.Compare)
                and isinstance(node.test.left, ast.Name) and node.test.left.id == "module"
                and isinstance(node.test.comparators[0], ast.Set)
                and any(isinstance(statement, ast.Assign)
                        and any(isinstance(target, ast.Name) and target.id == "contracts"
                                for target in statement.targets) for statement in node.body)):
            return ast.literal_eval(node.test.comparators[0])
    raise AssertionError("Missing native contract module gate")


class DistributionTests(unittest.TestCase):
    def test_larger_maps_is_standalone_without_ai_companion(self):
        for reuse in (False, True):
            with self.subTest(use_installed_settings=reuse):
                payload, versions = BUILDER.collect(ROOT, "LargerMaps", reuse)
                self.assertEqual(set(versions), {"LargerMaps"} | (set() if reuse else {"MenaceModSettings"}))
                self.assertFalse(any(p.startswith("source/mods/jiangyu/AIPerformance/") for p in payload))
                manifest = json.loads(payload["source/mods/jiangyu/LargerMaps/jiangyu.json"])
                self.assertFalse(any(d.startswith("AIPerformance") for d in manifest['depends']))
                self.assertIn(b"-Modules LargerMaps", payload["INSTALL-LargerMaps.cmd"])
                if reuse:
                    self.assertIn(b"-UseInstalledSettings", payload["INSTALL-LargerMaps.cmd"])
                    self.assertFalse(any(p.startswith("source/mods/jiangyu/MenaceModSettings/") for p in payload))

    def test_ai_recovery_is_independent_and_bundles_its_behavior_checks(self):
        payload, versions = BUILDER.collect(ROOT, "AIPerformance", True)
        self.assertEqual(set(versions), {"AIPerformance"})
        self.assertIn("source/mods/jiangyu/AIPerformance/code/AdvanceRecovery.cs", payload)
        self.assertIn("source/mods/LargerMaps/tests/AdvanceRecoveryChecks/Program.cs", payload)
        self.assertFalse(any(p.startswith("source/mods/jiangyu/LargerMaps/") for p in payload))
        self.assertFalse(any(p.startswith("source/mods/GameplaySuite/src/") for p in payload))
        installer = (TOOLS / 'Install-IntegratedSuite.ps1').read_text()
        self.assertNotIn("$compatibilityModules += 'AIPerformance'", installer)
        self.assertNotIn("+ @('AIPerformance', 'LargerMaps')", installer)

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
        for file in ("ThirdPersonCamera.csproj", "src/ThirdPersonCameraMod.cs", "src/CameraAutoFocus.cs", "README.md"):
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
        self.assertNotIn("LightPersonnelCarrier", catalog["modules"])
        self.assertIn("LightPersonnelCarrier", catalog["experimentalModules"])

    def test_jiangyu_runtime_pin_is_separate_from_existing_compiled_payloads(self):
        catalog = json.loads((TOOLS / "distribution.json").read_text())
        self.assertEqual(catalog["jiangyuVersion"], "1.4.10")
        self.assertEqual(catalog["compiledForJiangyuVersion"], "1.4.8")
        self.assertEqual(catalog["compiledForJiangyuOverrides"], {
            "SquaddieFieldPromotions": "1.4.10",
            "InfantrySandbagCover": "1.4.10",
        })
        self.assertEqual(
            catalog["cliUrl"],
            "https://github.com/antistrategie/jiangyu/releases/download/v1.4.10/jiangyu-cli-win-x64.zip",
        )
        self.assertEqual(catalog["cliSha256"], "54b4552dcd6850a2b796ba25f07107c8780af3ec025c517376d85dfb3d3990da")
        self.assertEqual(catalog["loaderSha256"], "d35b7ac7e97926392f10bfea60def143151006f4d4fd798754ee28b49efe0838")
        for name in catalog["modules"] + catalog.get("experimentalModules", []):
            compiled = json.loads((ROOT / "mods/jiangyu" / name / "compiled/jiangyu.json").read_text())
            expected = catalog.get("compiledForJiangyuOverrides", {}).get(name, catalog["compiledForJiangyuVersion"])
            self.assertEqual(compiled["compiledForJiangyu"], expected, name)

    def test_light_personnel_carrier_utility_is_compiled_opt_in_data_package(self):
        payload, versions = BUILDER.collect(ROOT, "LightPersonnelCarrier")
        self.assertEqual(set(versions), {"MenaceModSettings", "LightPersonnelCarrier"})
        self.assertEqual(versions["LightPersonnelCarrier"], "0.1.2")
        compiled_path = "source/mods/jiangyu/LightPersonnelCarrier/compiled/"
        self.assertIn(compiled_path + "templates.json", payload)
        self.assertIn("source/mods/jiangyu/LightPersonnelCarrier/templates/light_personnel_carrier.kdl", payload)

        manifest = json.loads(payload[compiled_path + "jiangyu.json"])
        self.assertEqual(manifest["compiledForJiangyu"], "1.4.8")
        self.assertEqual(manifest["compiledForUnity"], "6000.0.82f1")
        templates = json.loads(payload[compiled_path + "templates.json"])
        patches = {patch["templateId"]: patch for patch in templates["templatePatches"]}
        self.assertEqual(set(patches), {
            "modular_vehicle.light_personnel_carrier",
            "player_vehicle.light_personnel_carrier",
            "vehicle.chassis_light_personnel_carrier",
        })
        slot_ops = [operation for operation in patches["modular_vehicle.light_personnel_carrier"]["set"]
                    if operation["fieldPath"] == "Slots"]
        self.assertEqual([operation["op"] for operation in slot_ops], ["Clear", "Append"])
        kdl = payload["source/mods/jiangyu/LightPersonnelCarrier/templates/light_personnel_carrier.kdl"].decode()
        display_name = "Light Infantry Carrier (Utility)"
        self.assertEqual(kdl.count('"' + display_name + '"'), 2)
        self.assertIn("stripped-down utility variant of the Light Infantry Carrier", kdl)
        self.assertEqual(json.dumps(templates).count(display_name), 2)
        self.assertIn('append "Slots" {', kdl)
        self.assertIn('set "SlotType" enum="ModularVehicleSlotType" "Light"', kdl)
        self.assertIn('set "TransformName" "chassis_mount_02"', kdl)
        light_slot = slot_ops[1]["value"]["composite"]
        self.assertEqual(light_slot["typeName"], "Il2CppMenace.Strategy.ModularVehicleSlot")
        slot_values = {operation["fieldPath"]: operation["value"]
                       for operation in light_slot["operations"]}
        self.assertEqual(slot_values["SlotType"]["enumValue"], "Light")
        self.assertEqual(slot_values["TransformName"]["string"], "chassis_mount_02")
        self.assertFalse(slot_values["AllowsLightWeaponToBeIntegrated"]["boolean"])
        self.assertFalse(slot_values["UseRelativeAimAnchor"]["boolean"])
        prefabs = next(operation for operation in patches["player_vehicle.light_personnel_carrier"]["set"]
                       if operation["fieldPath"] == "Prefabs" and operation["op"] == "Append")
        self.assertEqual(prefabs["value"]["asset"]["name"], "carrier_light_static")
        cost = next(operation for operation in patches["vehicle.chassis_light_personnel_carrier"]["set"]
                    if operation["fieldPath"] == "m_Supplies")
        self.assertEqual(cost["value"]["int32"], 0)

        def assert_int(template_id, field, expected, descent=None):
            operation = next(op for op in patches[template_id]["set"]
                             if op["fieldPath"] == field and op.get("descent") == descent)
            self.assertEqual(operation["op"], "Set")
            self.assertEqual(operation["value"]["int32"], expected)

        properties = [{"field": "Properties"}]
        for field, value in (("Armor", 113), ("ArmorSide", 99), ("ArmorBack", 81)):
            assert_int("player_vehicle.light_personnel_carrier", field, value, properties)
        for field, value in (("AccessorySlots", 0), ("Rarity", 0),
                             ("MinCampaignProgress", 0), ("BlackMarketMaxQuantity", 10)):
            assert_int("vehicle.chassis_light_personnel_carrier", field, value)

    def test_infantry_sandbag_cover_is_scoped_to_player_infantry_and_reuses_companions(self):
        payload, versions = BUILDER.collect(ROOT, "InfantrySandbagCover", True)
        self.assertEqual(versions, {"InfantrySandbagCover": "0.3.1"})
        compiled_path = "source/mods/jiangyu/InfantrySandbagCover/compiled/"
        self.assertIn(compiled_path + "templates.json", payload)
        self.assertNotIn("source/mods/jiangyu/MenaceModSettings/jiangyu.json", payload)
        self.assertFalse(any("/MenaceModSettings/" in path for path in payload))
        self.assertIn(compiled_path + "code/InfantrySandbagCover.dll", payload)

        manifest = json.loads(payload["source/mods/jiangyu/InfantrySandbagCover/jiangyu.json"])
        self.assertEqual(manifest["depends"], ["Jiangyu >= 1.4.10", "MenaceModSettings >= 0.4.1"])
        compiled_manifest = json.loads(payload[compiled_path + "jiangyu.json"])
        self.assertEqual(compiled_manifest["compiledForJiangyu"], "1.4.10")
        templates = json.loads(payload[compiled_path + "templates.json"])
        self.assertEqual(templates["templateClones"], [{
            "templateType": "SkillTemplate",
            "sourceId": "offmap.deploy_sandbag_cover",
            "cloneId": "mod.infantry_sandbag_cover",
        }])

        patches = {patch["templateId"]: patch for patch in templates["templatePatches"]}
        skill = patches["mod.infantry_sandbag_cover"]
        operations = {operation["fieldPath"]: operation for operation in skill["set"]}
        self.assertEqual(operations["Type"]["value"]["enumValue"], "Active")
        self.assertEqual(operations["ActionPointCost"]["value"]["int32"], 80)
        self.assertFalse(operations["IsLimitedUses"]["value"]["boolean"])
        self.assertFalse(operations["IsTargeted"]["value"]["boolean"])
        self.assertEqual(operations["TargetsAllowed"]["value"]["enumValue"], "Self")
        self.assertEqual(operations["SelectableTiles"]["value"]["int32"], 0)
        self.assertNotIn("EventHandlers", operations)
        self.assertEqual(operations["ProjectileData"]["value"]["kind"], "Null")
        native_skills = json.loads((ROOT / "game_files/extracted/ExtractedData/SkillTemplate.json").read_text())
        donor = next(skill for skill in native_skills if skill["name"] == "offmap.deploy_sandbag_cover")
        self.assertEqual(donor["TargetsAllowed"], 1)
        self.assertEqual(donor["SelectableTiles"], 3)
        self.assertEqual(donor["EventHandlers"], [
            {"_type": "SpawnHalfCover", "CoverConfig": "military_half_cover_sandbags"},
            {"_type": "DestroyEnvironmentFeatures"},
        ])
        self.assertEqual(donor["ProjectileData"]["Prefab"], "civilian_barricades_crate_2b")

        player_entities = {
            "allied.player_local_forces_conscripts",
            "allied.player_local_forces_regular_infantry",
            "player_squad.carda", "player_squad.corlain", "player_squad.darby",
            "player_squad.greifinger", "player_squad.kubra", "player_squad.lim",
            "player_squad.pike", "player_squad.pike_tutorial", "player_squad.sachin",
            "player_squad.sy", "player_squad.tech", "player_squad.test",
            "player_squad.vamplew", "player_squad.weiwu", "player_squad.wetteroth",
            "player_squad.yaz",
        }
        native_entities = json.loads((ROOT / "game_files/extracted/ExtractedData/EntityTemplate.json").read_text())
        self.assertTrue(player_entities <= {entity["name"] for entity in native_entities})
        self.assertEqual(set(patches) - {"mod.infantry_sandbag_cover"}, player_entities)
        for entity in player_entities:
            append = patches[entity]["set"][0]
            self.assertEqual(append["fieldPath"], "Skills")
            self.assertEqual(append["value"]["reference"]["templateId"], "mod.infantry_sandbag_cover")

        launcher = payload["INSTALL-InfantrySandbagCover.cmd"].decode()
        self.assertIn("-Product Standalone -Modules InfantrySandbagCover -UseInstalledSettings", launcher)
        catalog = json.loads((TOOLS / "distribution.json").read_text())
        self.assertIn("InfantrySandbagCover", catalog["standaloneGameBuilds"][0]["modules"])
        self.assertEqual(catalog["compiledForJiangyuOverrides"]["InfantrySandbagCover"], "1.4.10")
        readme = payload["README.txt"].decode()
        self.assertIn("Requires existing MenaceModSettings and Jiangyu.Loader; does NOT replace either.", readme)
        installer = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        self.assertIn("'InfantrySandbagCover'", installer)
        self.assertIn("Assert-Hash (Join-Path $GamePath 'Mods\\Jiangyu.Loader.dll') $catalog.loaderSha256", installer)
        gate = installer.index("if ($name -eq 'InfantrySandbagCover')")
        self.assertLess(gate, installer.index("Copy-Item -LiteralPath $compiled -Destination (Join-Path $stage $name)"))
        self.assertIn("InfantrySandbagCover", native_contract_modules())

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

    def test_veterancy_candidate_reuses_installed_settings_without_bundling_them(self):
        payload, versions = BUILDER.collect(ROOT, "SquaddieVeterancy", True)
        manifest = json.loads((ROOT / "mods/jiangyu/SquaddieVeterancy/jiangyu.json").read_text())
        self.assertEqual(versions, {"SquaddieVeterancy": manifest["version"]})
        self.assertIn("SquaddieVeterancy", native_contract_modules())
        self.assertIn("source/mods/jiangyu/SquaddieVeterancy/tests/NativeContractChecks/Program.cs", payload)
        self.assertIn("source/mods/jiangyu/SquaddieVeterancy/compiled/code/SquaddieProbe.dll", payload)
        project = (ROOT / "mods/jiangyu/SquaddieVeterancy/code/SquaddieVeterancy.csproj").read_text()
        self.assertIn("<MenaceModSettingsPath Condition=", project)
        self.assertIn("<HintPath>$(MenaceModSettingsPath)</HintPath>", project)
        self.assertNotIn("source/mods/jiangyu/MenaceModSettings/jiangyu.json", payload)
        self.assertFalse(any("/MenaceModSettings/" in path for path in payload))
        launcher = payload["INSTALL-SquaddieVeterancy.cmd"].decode()
        self.assertIn("-Product Standalone -Modules SquaddieVeterancy -UseInstalledSettings", launcher)

    def test_coordinated_candidate_preserves_installed_companions(self):
        payload, versions = BUILDER.collect(ROOT, "CoordinatedFire", True)
        manifest = json.loads((ROOT / "mods/jiangyu/CoordinatedFire/jiangyu.json").read_text())
        self.assertEqual(versions, {"CoordinatedFire": manifest["version"]})
        self.assertFalse(any("/MenaceModSettings/" in path for path in payload))
        launcher = payload["INSTALL-CoordinatedFire.cmd"].decode()
        self.assertIn("-Product Standalone -Modules CoordinatedFire -UseInstalledSettings", launcher)
        project = (ROOT / "mods/jiangyu/CoordinatedFire/code/CoordinatedFire.csproj").read_text()
        self.assertIn("<HintPath>$(MenaceModSettingsPath)</HintPath>", project)
        installer = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        self.assertIn("'TacticalRoundCheckpoint', 'CoordinatedFire'", installer)

    def test_wmr_candidate_reuses_installed_settings_without_bundling_them(self):
        payload, versions = BUILDER.collect(ROOT, "WeaponMechanicsRebalanced", True)
        manifest = json.loads((ROOT / "mods/jiangyu/WeaponMechanicsRebalanced/jiangyu.json").read_text())
        self.assertEqual(versions, {"WeaponMechanicsRebalanced": manifest["version"]})
        self.assertFalse(any("/MenaceModSettings/" in path for path in payload))
        launcher = payload["INSTALL-WeaponMechanicsRebalanced.cmd"].decode()
        self.assertIn(
            "-Product Standalone -Modules WeaponMechanicsRebalanced -UseInstalledSettings",
            launcher,
        )
        readme = payload["README.txt"].decode()
        self.assertIn("Requires existing MenaceModSettings and Jiangyu.Loader", readme)
        self.assertIn("does NOT replace either", readme)
        project = (
            ROOT / "mods/jiangyu/WeaponMechanicsRebalanced/code/WeaponMechanicsRebalanced.csproj"
        ).read_text()
        self.assertIn("<MenaceModSettingsPath Condition=", project)
        self.assertIn("<HintPath>$(MenaceModSettingsPath)</HintPath>", project)
        installer = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        self.assertIn("'TacticalRoundCheckpoint', 'CoordinatedFire', 'WeaponMechanicsRebalanced'", installer)

    def test_clean_verifier_uses_an_external_settings_reference_for_veterancy(self):
        payload, _ = BUILDER.collect(ROOT, "SquaddieVeterancy", True)
        settings_dll = ROOT / "mods/jiangyu/MenaceModSettings/compiled/code/MenaceModSettings.dll"
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            game = folder / "game"
            sdk = folder / "sdk"
            game.mkdir()
            sdk.mkdir()
            commands = []

            def fake_run(arguments, **kwargs):
                commands.append(arguments)
                if len(arguments) > 2 and arguments[1] == "build":
                    project = Path(arguments[2])
                    output = project.parent / "bin/Release/SquaddieProbe.dll"
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(b"mock built DLL")
                elif arguments[1] == "run" and "OfflineChecks.csproj" in arguments[3]:
                    extracted = Path(arguments[arguments.index("--") + 1])
                    # Without the project witness ArtifactChecks mistakes the
                    # external code companion for a data package and fails.
                    self.assertTrue((extracted / "mods/jiangyu/MenaceModSettings/code/MenaceModSettings.csproj").is_file())

            with patch.object(VERIFIER.subprocess, "run", side_effect=fake_run), redirect_stdout(io.StringIO()):
                VERIFIER.compile_clean(payload, game, sdk, "dotnet")

        build = next(arguments for arguments in commands if arguments[1] == "build")
        self.assertIn(f"-p:MenaceModSettingsPath={settings_dll}", build)
        self.assertEqual(sum(arguments[1] == "build" for arguments in commands), 1)
        native = next(arguments for arguments in commands if arguments[1] == "run" and "NativeContractChecks.csproj" in arguments[3])
        self.assertTrue(native[-1].endswith("compiled/code/SquaddieProbe.dll"))
        offline = next(arguments for arguments in commands if arguments[1] == "run" and "OfflineChecks.csproj" in arguments[3])
        self.assertEqual(offline[-1], "MenaceModSettings,SquaddieVeterancy")

    def test_clean_verifier_uses_an_external_settings_reference_for_wmr(self):
        payload, _ = BUILDER.collect(ROOT, "WeaponMechanicsRebalanced", True)
        settings_dll = ROOT / "mods/jiangyu/MenaceModSettings/compiled/code/MenaceModSettings.dll"
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            game = folder / "game"
            sdk = folder / "sdk"
            game.mkdir()
            sdk.mkdir()
            commands = []

            def fake_run(arguments, **kwargs):
                commands.append(arguments)
                if len(arguments) > 2 and arguments[1] == "build":
                    project = Path(arguments[2])
                    output = project.parent / "bin/Release/WeaponMechanicsRebalanced.dll"
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(b"mock built DLL")
                elif arguments[1] == "run" and "NativeContractChecks.csproj" not in arguments[3]:
                    extracted = Path(arguments[arguments.index("--") + 1])
                    self.assertTrue(
                        (extracted / "mods/jiangyu/MenaceModSettings/code/MenaceModSettings.csproj").is_file()
                    )

            with patch.object(VERIFIER.subprocess, "run", side_effect=fake_run), redirect_stdout(io.StringIO()):
                VERIFIER.compile_clean(payload, game, sdk, "dotnet")

        build = next(arguments for arguments in commands if arguments[1] == "build")
        self.assertIn(f"-p:MenaceModSettingsPath={settings_dll}", build)
        self.assertEqual(sum(arguments[1] == "build" for arguments in commands), 1)
        runs = [arguments for arguments in commands if arguments[1] == "run"]
        self.assertEqual(len(runs), 2)
        self.assertIn("NativeContractChecks.csproj", runs[0][3])
        self.assertEqual(runs[1][-1], "MenaceModSettings,WeaponMechanicsRebalanced")

    def test_clean_verifier_uses_external_settings_reference_for_sandbags(self):
        payload, _ = BUILDER.collect(ROOT, "InfantrySandbagCover", True)
        settings_dll = ROOT / "mods/jiangyu/MenaceModSettings/compiled/code/MenaceModSettings.dll"
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            game = folder / "game"
            sdk = folder / "sdk"
            game.mkdir()
            sdk.mkdir()
            commands = []

            def fake_run(arguments, **kwargs):
                commands.append(arguments)
                if len(arguments) > 2 and arguments[1] == "build":
                    project = Path(arguments[2])
                    output = project.parent / "bin/Release/InfantrySandbagCover.dll"
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(b"mock built DLL")
                elif (arguments[1] == "run" and "NativeContractChecks.csproj" not in arguments[3]
                      and "InfantrySandbagCover/tests/OfflineChecks/OfflineChecks.csproj" not in arguments[3]):
                    extracted = Path(arguments[arguments.index("--") + 1])
                    self.assertTrue(
                        (extracted / "mods/jiangyu/MenaceModSettings/code/MenaceModSettings.csproj").is_file()
                    )

            with patch.object(VERIFIER.subprocess, "run", side_effect=fake_run), redirect_stdout(io.StringIO()):
                VERIFIER.compile_clean(payload, game, sdk, "dotnet")

        build = next(arguments for arguments in commands if arguments[1] == "build")
        self.assertIn(f"-p:MenaceModSettingsPath={settings_dll}", build)
        runs = [arguments for arguments in commands if arguments[1] == "run"]
        self.assertEqual(len(runs), 3)
        self.assertIn("NativeContractChecks.csproj", runs[0][3])
        self.assertIn("InfantrySandbagCover/tests/OfflineChecks/OfflineChecks.csproj", runs[1][3])
        self.assertEqual(runs[2][-1], "MenaceModSettings,InfantrySandbagCover")

    def test_installed_settings_are_validated_without_being_staged(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        helper = (TOOLS / "Install.Common.ps1").read_text()
        self.assertIn("'SquaddieFieldPromotions', 'SquaddieVeterancy'", source)
        self.assertIn("'CoordinatedFire', 'WeaponMechanicsRebalanced'", source)
        self.assertIn("'InfantrySandbagCover'", source)
        self.assertIn("$selected = @($Modules[0])", source)
        self.assertIn("Get-Content -LiteralPath (Join-Path $BundleRoot ('source\\mods\\jiangyu\\' + $Modules[0]", source)
        self.assertIn("Get-InstalledSettingsReference $GamePath $minimum", source)
        self.assertIn("$requirements.Count -ne $declaredSettings.Count", source)
        self.assertIn("if (-not $UseInstalledSettings)", source)
        self.assertIn("if ($settingsDll) { Assert-Hash $settingsDll $settingsHash }", source)
        self.assertIn("Assert-Hash (Join-Path $GamePath 'Mods\\Jiangyu.Loader.dll') $catalog.loaderSha256", source)
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

    def test_camera_native_contract_gate_is_bundled_and_precedes_staging(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        gate = source.index("mods\\jiangyu\\CinematicActionCamera\\tests\\NativeContractChecks\\NativeContractChecks.csproj")
        self.assertLess(gate, source.index("Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'MenaceThirdPersonCamera.dll')"))
        builder = (TOOLS / "build_kit.py").read_text()
        self.assertIn('"src/CameraAutoFocus.cs"', builder)
        self.assertIn('tree(folder / "tests",', builder)
        self.assertIn('CinematicActionCamera/tests/NativeContractChecks/NativeContractChecks.csproj', (TOOLS / "verify_kit.py").read_text())

    def test_mortar_native_contract_gate_precedes_staging(self):
        source = (TOOLS / "Install-IntegratedSuite.ps1").read_text()
        gate = source.index("if ($name -eq 'WeaponMechanicsRebalanced')")
        self.assertLess(gate, source.index('Copy-Item -LiteralPath $compiled -Destination (Join-Path $stage $name)'))
        self.assertIn("WeaponMechanicsRebalanced", native_contract_modules())
        catalog = json.loads((TOOLS / "distribution.json").read_text())
        self.assertIn("WeaponMechanicsRebalanced", catalog["standaloneGameBuilds"][0]["modules"])

    def test_module_candidate_scopes_artifact_checks_to_selected_packages(self):
        builder = (TOOLS / "build_kit.py").read_text()
        program = (ROOT / "mods/GameplaySuite/tests/OfflineChecks/Program.cs").read_text()
        checks = (ROOT / "mods/GameplaySuite/tests/OfflineChecks/ArtifactChecks.cs").read_text()
        self.assertIn("selected_modules = list(versions)", builder)
        self.assertIn('selected_modules.insert(0, "MenaceModSettings")', builder)
        self.assertIn('offline_args.extend(["--modules", ",".join(selected_modules)])', builder)
        self.assertIn('args.module is not None and args.module != "SquaddieFieldPromotions"', builder)
        self.assertIn('args[1] != "--modules"', program)
        self.assertIn("ArtifactChecks.Run(Check, args[0], selectedModules)", program)
        self.assertIn("selectedModules?.Distinct(StringComparer.Ordinal).ToArray()", checks)
        self.assertIn("modules.Concat(experimentalModules)", checks)
        self.assertIn("selected.IsSubsetOf(allModules)", checks)
        self.assertIn("foreach (string module in modulesToCheck)", checks)
        self.assertIn('!selected.Contains("CinematicActionCamera")', checks)

    def test_clean_build_checks_only_packages_present_in_standalone(self):
        from unittest.mock import patch
        from contextlib import redirect_stdout
        from io import StringIO
        files = {
            "source/mods/GameplaySuite/tools/distribution.json": json.dumps({
                "modules": ["MenaceModSettings", "LargerMaps", "Unbundled"]
            }).encode(),
            "source/mods/jiangyu/MenaceModSettings/jiangyu.json": b'{}',
            "source/mods/jiangyu/LargerMaps/jiangyu.json": b'{}',
        }
        with patch.object(VERIFIER.subprocess, "run") as run, redirect_stdout(StringIO()):
            VERIFIER.compile_clean(files, Path("/unused-game"), Path("/unused-sdk"), "dotnet")
        args = run.call_args.args[0]
        self.assertEqual(args[-2:], ["--modules", "MenaceModSettings,LargerMaps"])
        self.assertNotIn("Unbundled", args)

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

    def test_verifier_bounds_private_voice_bundles_without_relaxing_source_limit(self):
        voice_root = "source/mods/jiangyu/SquaddieFieldPromotions/runtime_assets/voice/"
        xcom = voice_root + "sfp_xcom_voicepacks.sfpbundle"
        john_jane = voice_root + "sfp_john_jane_voice.sfpbundle"
        data, _ = BUILDER.make_archive(
            {xcom: b"xcom", john_jane: b"john-jane", "source/example.cs": b"source"},
            {}, "test-base")
        for xcom_mib, john_jane_mib, source_mib, accepted in (
            (200, 120, 90, True), (200, 121, 1, False), (1, 1, 101, False)
        ):
            with self.subTest(xcom_mib=xcom_mib, john_jane_mib=john_jane_mib,
                              source_mib=source_mib):
                with zipfile.ZipFile(io.BytesIO(data)) as archive:
                    entries = archive.infolist()
                for entry in entries:
                    if entry.filename == xcom:
                        entry.file_size = xcom_mib * 1024 * 1024
                    elif entry.filename == john_jane:
                        entry.file_size = john_jane_mib * 1024 * 1024
                    elif entry.filename == "source/example.cs":
                        entry.file_size = source_mib * 1024 * 1024
                with patch.object(zipfile.ZipFile, "infolist", return_value=entries):
                    if accepted:
                        files, _ = VERIFIER.read_archive(io.BytesIO(data))
                        self.assertEqual(files[xcom], b"xcom")
                    else:
                        with self.assertRaisesRegex(ValueError, "Unexpectedly large"):
                            VERIFIER.read_archive(io.BytesIO(data))

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
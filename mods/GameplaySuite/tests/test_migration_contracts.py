"""Cross-module source guards, not proof of runtime restoration or save compatibility."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "mods/GameplaySuite/src"


class MigrationContractTests(unittest.TestCase):
    def test_matrix_covers_every_catalogued_package_at_current_version(self):
        matrix = (ROOT / "docs/MIGRATION_STATUS.md").read_text()
        catalog = json.loads((ROOT / "mods/GameplaySuite/tools/distribution.json").read_text())
        packages = ROOT / "mods/jiangyu"
        experimental = catalog.get("experimentalModules", [])
        self.assertFalse(set(catalog["modules"]) & set(experimental))
        self.assertEqual(set(catalog["modules"]) | set(experimental), {p.parent.name for p in packages.glob("*/jiangyu.json")})
        for name in catalog["modules"]:
            manifest = json.loads((packages / name / "jiangyu.json").read_text())
            self.assertIn(f'| {name} | {manifest["version"]} |', matrix)

    def test_standalone_settings_camera_and_legacy_cutover_wiring(self):
        source = (ROOT / "mods/GameplaySuite/tools/Install-IntegratedSuite.ps1").read_text()
        self.assertIn("$selected = @($catalog.modules)", source)
        self.assertIn("$selected = @('MenaceModSettings') + @($Modules", source)
        self.assertIn("$available = @($catalog.modules)", source)
        self.assertIn("$available += @($catalog.experimentalModules)", source)
        self.assertIn("$available -notcontains $name", source)
        self.assertIn("Join-Path $root 'runtime_assets'", source)
        self.assertIn("if ($selected -contains 'CinematicActionCamera')", source)
        self.assertIn("MenaceThirdPersonCamera.dll", source)
        self.assertIn("Legacy monolithic Suite detected", source)

    def test_veterancy_keeps_existing_sidecar_location_and_integer_ids(self):
        source = (SRC / "SquaddieVeterancy/ExperienceStore.cs").read_text()
        self.assertIn('Path.Combine(gameRoot, "UserData", "SquaddieProbe")', source)
        self.assertIn('Path.Combine(_root, "Saves")', source)
        self.assertIn('SafeFileName(savePath) + ".squaddieveterancy.json"', source)
        self.assertIn("Dictionary<int, Record> _records", source)
        self.assertIn("_records[identity.Id]", source)
        self.assertIn("File.Move(temp, sidecar, true)", source)

    def test_proxy_lookup_uses_real_game_assembly_and_template_loader(self):
        source = (SRC / "Shared/Jiangyu/MenaceSdk.cs").read_text()
        self.assertIn('name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)', source)
        self.assertIn("a == typeof(DataTemplate).Assembly", source)
        self.assertIn('m.Name == "GetAll" && m.IsGenericMethodDefinition', source)
        self.assertIn('m.Name == "TryGet" && m.IsGenericMethodDefinition', source)
        self.assertLess(source.index("foreach (IntPtr pointer in LoaderGetAll(type))"), source.index("Resources.FindObjectsOfTypeAll", source.index("public static GameObj[] FindAll")))

    def test_garrison_reads_cover_keys_and_falls_back_when_disabled(self):
        source = (SRC / "GarrisonableBuildings/Plugin.cs").read_text()
        reader = source.split("private static float ReadEffectiveBuildingAccuracyPercent()", 1)[1].split("private static bool IsStrongerCoverLoaded()", 1)[0]
        self.assertIn('ReadPercent("BuildingAccuracy", 40f)', reader)
        self.assertIn("if (!IsStrongerCoverLoaded()) return baseValue;", reader)
        self.assertIn('"EnableGameplay", true)) return baseValue;', reader)
        self.assertIn('"BuildingCover", 30f)', reader)
        self.assertIn('key is ("EnableGameplay" or "BuildingCover")', source)

    def test_cover_restore_uses_original_values_not_a_fixed_default(self):
        source = (ROOT / "mods/jiangyu/StrongerCover/code/InsideCoverTemplates.cs").read_text()
        self.assertIn("CoverSettings.TryGetGarrisonBuildingBase(out float buildingBase)", source)
        self.assertIn("!CoverSettings.Enabled && Original.TryGetValue", source)
        self.assertIn("template.AccuracyMult = original;", source)

    def test_wrr_override_migration_never_replaces_modern_file(self):
        source = (SRC / "WeaponRangesRebalanced/Plugin.cs").read_text()
        reader = source.split("private static string ResolveOverrideFilePath()", 1)[1].split("private static void EnsureOverrideFileExists()", 1)[0]
        self.assertIn('Path.Combine(userData, "WeaponRangesRebalanced", OverrideFileName)', reader)
        self.assertIn("if (!File.Exists(target) && File.Exists(legacy))", reader)
        self.assertIn("File.Copy(legacy, target);", reader)
        self.assertNotIn("File.Copy(legacy, target, true)", reader)

    def test_wrr_restoration_only_touches_owned_keys(self):
        source = (SRC / "WeaponRangesRebalanced/Plugin.cs").read_text()
        self.assertIn("!shouldOwnValue && TouchedKeys.Contains(key)", source)
        self.assertIn("!shouldOwn && TouchedSkillKeys.Contains(key)", source)
        self.assertIn('RestoreType("ModularVehicleWeaponTemplate")', source)
        self.assertIn("RestoreLinkedSkills();", source)
        self.assertIn('_debugLogging = Setting("DebugLogging", false)', source)
        self.assertIn('_logWeaponTable = Setting("DebugWeaponTable", false)', source)

    def test_sharing_disable_and_unload_clear_pending_work(self):
        source = (SRC / "FieldLogistics/Plugin.cs").read_text()
        disabled = source.split('key, "EnableAmmoSharing"', 1)[1].split("GameState.RunDelayed", 1)[0]
        unloaded = source.split("public void OnUnload()", 1)[1].split("private static void OnSettingChanged", 1)[0]
        for field in ("_ammoSnapshots", "_pendingAmmoCredits", "_pendingRedistributions", "_pendingModeSyncs"):
            self.assertIn(field + ".Clear();", disabled)
            self.assertIn(field + ".Clear();", unloaded)


if __name__ == "__main__":
    unittest.main()
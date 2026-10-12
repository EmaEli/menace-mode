using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("2.8.1.0")]
[assembly: AssemblyFileVersion("2.8.1.0")]

namespace MenaceWeaponRangesRebalanced;

public sealed class Plugin : IModpackPlugin
{
    private const string SettingsGroup = "Weapon Ranges Rebalanced";
    private const string Version = "2.8.1";
    private const string OverrideFileName = "weapon-overrides.json";
    private const string ModpackId = "luca.weaponrangesrebalanced";
    private const string SuiteModpackId = "luca.menacegameplaysuite";

    private enum WeaponFamily
    {
        CloseQuarters,
        SmgPdw,
        Rifles,
        Precision,
        MachineGuns,
        Launchers,
        Artillery,
        VehicleTurret,
        Exotic,
        Other
    }

    private enum BalanceSource
    {
        Vanilla,
        Realistic,
        Auto,
        Custom
    }

    private sealed class Result
    {
        public string TypeName = string.Empty;
        public string Name = string.Empty;
        public int MinRange;
        public int IdealRange;
        public int Vanilla;
        public int Target;
        public int Applied;
        public WeaponFamily Family;
        public BalanceSource Source;
        public bool Written;
    }

    private sealed class SkillResult
    {
        public string Name = string.Empty;
        public string ParentWeapon = string.Empty;
        public int Vanilla;
        public int Target;
        public int Applied;
        public bool Written;
    }

    // Long-range indirect attacks can make MENACE's AI enumerate a huge number of
    // move-to-fire origins. On enlarged 64x64 maps this can become extremely expensive
    // and previously caused Attack.OnCollect timeouts for the medium mortar. WRR keeps
    // the realistic ranges, but disables only attack-specific repositioning for the
    // indirect-fire families below. The attacks remain fully usable whenever normal AI
    // movement leaves the unit in a valid firing position.
    private static readonly string[] IndirectFireAiIdTokens =
    {
        "mortar",
        "artillery",
        "howitzer",
        "rocket_organ",
        "indirect_fire",
        "barrage"
    };

    private static readonly Dictionary<string, bool> OriginalIndirectFireInfluencesPositioning =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> RealisticWeaponTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["turret.construct_gunslinger_twin_heavy_auto_repeater"] = 18,
        ["turret.construct_heavy_auto_repeater"] = 18,
        ["turret.construct_rocket_launcher"] = 15,
        ["turret.multi_rocket_pod_gunslinger"] = 16,
        ["turret.construct_gunslinger_disruptor"] = 15,
        ["turret.construct_gunslinger_laser_rifle"] = 18,
        ["turret.construct_laser_lance"] = 20,
        ["turret.local_forces_heavy_tank_01_15cm_howitzer"] = 22,
        ["turret.medium_machinegun_heavy_tank"] = 16,
        ["turret.pirate_heavy_flamethrower"] = 5,
        ["turret.pirate_heavy_mg_truck_version"] = 19,
        ["turret.pirate_laser_lance"] = 18,
        ["turret.pirate_medium_chaingun"] = 18,
        ["turret.pirate_rocket_organ"] = 26,
        ["turret.walker_heavy_flamethrower"] = 5,
        ["turret.walker_twin_heavy_machine_gun"] = 19,
        ["weapon.construct_flamethrower"] = 4,
        ["weapon.construct_heavy_repeater"] = 11,
        ["weapon.construct_heavy_repeater_phantom"] = 13,
        ["weapon.construct_micro_repeater"] = 9,
        ["weapon.alien_acid_spitter"] = 11,
        ["weapon.construct_acid_stalker"] = 11,
        ["weapon.construct_plasma_flamethrower"] = 4,
        ["weapon.construct_plasma_rifle"] = 13,
        ["weapon.construct_plasma_rifle_soldier_t2"] = 11,
        ["weapon.crafted_enhanced_electro_magnetic_assault_rifle"] = 14,
        ["weapon.generic_assault_rifle_tier1_ARC_762"] = 9,
        ["weapon.generic_assault_rifle_tier1_ARC_762_ACC"] = 11,
        ["weapon.generic_assault_rifle_tier1_ARC_762_CSP"] = 9,
        ["weapon.generic_assault_rifle_tier1_ARC_762_SOCC"] = 9,
        ["weapon.generic_assault_rifle_tier1_kpac"] = 11,
        ["weapon.generic_assault_rifle_tier1_kpac_CQB"] = 8,
        ["weapon.generic_assault_rifle_tier1_kpac_grenade_launcher"] = 11,
        ["weapon.generic_assault_rifle_tier1_kpac_RANGE"] = 13,
        ["weapon.generic_assault_rifle_tier1_kpac_SOF"] = 11,
        ["weapon.generic_assault_rifle_tier2"] = 12,
        ["weapon.generic_assault_rifle_tier3_arx"] = 14,
        ["weapon.generic_battle_rifle_tier1_crowbar"] = 13,
        ["weapon.generic_battle_rifle_tier1_crowbar_carbine"] = 12,
        ["weapon.generic_battle_rifle_tier1_crowbar_marksman"] = 14,
        ["weapon.generic_battle_rifle_tier1_crowbar_sup"] = 13,
        ["weapon.generic_battle_rifle_tier2_hamer"] = 14,
        ["weapon.generic_battle_rifle_tier3_brx"] = 16,
        ["weapon.generic_carbine_tier1_spc"] = 9,
        ["weapon.generic_combat_shotgun_tier_1_cs185"] = 6,
        ["weapon.generic_heavy_assault_rifle_tier2_har"] = 10,
        ["weapon.generic_heavy_smg_tier2_kruger_hmp"] = 8,
        ["weapon.generic_pdw_tier1_scp"] = 7,
        ["weapon.generic_smg_tier1_ppp"] = 6,
        ["weapon.generic_smg_tier1_ppp_HiCap"] = 6,
        ["weapon.generic_smg_tier1_ppp_sof"] = 6,
        ["weapon.generic_smg_tier2_griffin"] = 7,
        ["weapon.generic_smg_tier3_oktagon"] = 9,
        ["weapon.laser_rifle"] = 11,
        ["weapon.pirate_assault_rifle"] = 10,
        ["weapon.pirate_corridor_sweeper"] = 6,
        ["weapon.pirate_outcast_pipe_gun"] = 9,
        ["weapon.pirate_pdw_tier_1"] = 7,
        ["weapon.pirate_pdw_tier_1_captain"] = 9,
        ["construct.weapon.skirmisher_anti_tank_rifle"] = 16,
        ["specialweapon.airburst_grenade_launcher_XGr_20"] = 11,
        ["specialweapon.anti_materiel_rifle_tier1"] = 16,
        ["specialweapon.crafted_maal_he_rocket_launcher"] = 13,
        ["specialweapon.crafted_ndl_stun_bomb_launcher"] = 11,
        ["specialweapon.flamethrower"] = 4,
        ["specialweapon.generic_designated_marksman_rifle_tier1"] = 15,
        ["specialweapon.generic_designated_marksman_rifle_tier2"] = 17,
        ["specialweapon.generic_designated_marksman_rifle_tier3"] = 19,
        ["specialweapon.grenade_launcher_gr331"] = 11,
        ["specialweapon.grenade_launcher_tier3"] = 13,
        ["specialweapon.infantry_automatic_rifle_tier2"] = 13,
        ["specialweapon.light_machinegun_tier1_m10E"] = 13,
        ["specialweapon.light_machinegun_tier2_m320"] = 14,
        ["specialweapon.light_machinegun_tier2_m320_SOC"] = 14,
        ["specialweapon.light_machinegun_tier3"] = 16,
        ["specialweapon.light_mortar"] = 34,
        ["specialweapon.medium_machinegun_tier1_cmg50"] = 15,
        ["specialweapon.medium_machinegun_tier2_m80"] = 17,
        ["specialweapon.medium_machinegun_tier3"] = 19,
        ["specialweapon.mining_laser"] = 7,
        ["specialweapon.mining_laser_construct"] = 7,
        ["specialweapon.pirate_emp_rifle"] = 6,
        ["specialweapon.pirate_light_chaingun"] = 13,
        ["specialweapon.plasma_rifle"] = 11,
        ["specialweapon.rmc_rocket_launcher_tier2_L31"] = 14,
        ["specialweapon.rocket_launcher_tier1_pal"] = 11,
        ["specialweapon.rogue_army_sniper_rifle"] = 16,
        ["specialweapon.rpg_launcher_tier_1"] = 10,
        ["specialweapon.rpg_launcher_tier_2"] = 12,
        ["specialweapon.sniper_rifle_sr41"] = 18,
        ["specialweapon.sniper_rifle_tier1"] = 16,
        ["specialweapon.sniper_rifle_tier3"] = 20,
        ["specialweapon.tripod_anti_tank_gun_rgl"] = 16,
        ["specialweapon.tripod_auto_grenade_launcher"] = 15,
        ["specialweapon.tripod_autocannon"] = 15,
        ["specialweapon.tripod_heavy_machinegun"] = 19,
        ["specialweapon.tripod_laser_lance"] = 15,
        ["specialweapon.tripod_medium_machinegun"] = 16,
        ["specialweapon.tripod_medium_mortar_80mm"] = 42,
        ["specialweapon.tripod_minigun"] = 11,
        ["specialweapon.tripod_rocketlauncher_atgm"] = 18,
    };

    private static readonly Dictionary<string, int> RealisticVehicleTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mod_weapon.heavy.cannon_long"] = 22,
        ["mod_weapon.heavy.cannon_short"] = 19,
        ["mod_weapon.heavy.mortar"] = 48,
        ["mod_weapon.heavy.twin_hmg"] = 19,
        ["mod_weapon.heavy.twin_minigun"] = 12,
        ["mod_weapon.light.anti_materiel_rifle"] = 16,
        ["mod_weapon.light.auto_grenade_launcher"] = 15,
        ["mod_weapon.light.chaingun"] = 18,
        ["mod_weapon.light.flamethrower"] = 4,
        ["mod_weapon.light.light_mg"] = 13,
        ["mod_weapon.light.medium_mg"] = 15,
        ["mod_weapon.light.plasma_rifle"] = 13,
        ["mod_weapon.medium.anti_tank_gun"] = 21,
        ["mod_weapon.medium.atgm_launcher"] = 24,
        ["mod_weapon.medium.auto_laser"] = 18,
        ["mod_weapon.medium.autocannon"] = 20,
        ["mod_weapon.medium.heavy_flamer"] = 5,
        ["mod_weapon.medium.heavy_mg"] = 19,
        ["mod_weapon.medium.laser_lance"] = 20,
        ["mod_weapon.medium.minigun"] = 12,
        ["mod_weapon.medium.rocket_launcher"] = 15,
        ["mod_weapon.medium.rocket_organ"] = 26,
        ["mod_weapon.medium.scanner_ground_radar"] = 30,
        ["mod_weapon.medium.swarm_airburst_launcher"] = 15,
    };

    // Some MENACE weapons grant attack skills whose SkillTemplate explicitly overrides
    // the weapon range. In those cases changing WeaponTemplate.MaxRange updates the item
    // tooltip but not the actual targeting distance. Keep these current v0.7.14 primary
    // firing skills linked to their parent weapon so gameplay follows the WRR range too.
    private static readonly Dictionary<string, string> LinkedSkillParents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active.mod.fire_120mm_mortar_autoloader"] = "mod_weapon.heavy.mortar",
        ["active.mod.twinfire.fire_120mm_mortar_autoloader"] = "mod_weapon.heavy.mortar",

        ["active.mod.atgm.direct_fire"] = "mod_weapon.medium.atgm_launcher",
        ["active.mod.atgm.indirect_fire"] = "mod_weapon.medium.atgm_launcher",
        ["active.mod.twinfire.atgm.direct_fire"] = "mod_weapon.medium.atgm_launcher",
        ["active.mod.twinfire.atgm.indirect_fire"] = "mod_weapon.medium.atgm_launcher",

        ["active.mod.heavy_flamethrower"] = "mod_weapon.medium.heavy_flamer",
        ["active.mod.twinfire.heavy_flamethrower"] = "mod_weapon.medium.heavy_flamer",

        ["active.pirate_heavy_flamethrower"] = "turret.pirate_heavy_flamethrower",
        ["active.pirate_truck_version_turret_heavy_machinegun.suppressive"] = "turret.pirate_heavy_mg_truck_version",
        ["active.heavy_flamethrower_walker"] = "turret.walker_heavy_flamethrower",

        ["active.tripod_atgm.direct_fire"] = "specialweapon.tripod_rocketlauncher_atgm",
        ["active.tripod_atgm.indirect_fire"] = "specialweapon.tripod_rocketlauncher_atgm",

        ["active.fire_acid_spitter"] = "weapon.alien_acid_spitter",
    };

    // Current v0.7.15 secondary fire modes that explicitly override range but were
    // not covered by the old primary-skill list. They keep their shorter/longer role:
    // WRR scales them by the same parent-range ratio instead of forcing them to the
    // exact primary max range.
    private static readonly Dictionary<string, string> ScaledSecondarySkillParents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active.launch_underbarrel_frag_grenade"] = "weapon.generic_assault_rifle_tier1_kpac_grenade_launcher",
        ["active.fire_battle_rifle_tier_1_spray"] = "weapon.generic_battle_rifle_tier1_crowbar",
        ["active.fire_battle_rifle_tier_1_aimed_fire_marksman"] = "weapon.generic_battle_rifle_tier1_crowbar_marksman",
        ["active.fire_battle_rifle_tier_1_spray_suppressed"] = "weapon.generic_battle_rifle_tier1_crowbar_sup",
        ["active.fire_battle_rifle_tier_2_aimed_fire"] = "weapon.generic_battle_rifle_tier2_hamer",
        ["active.fire_heavy_smg_tier_2_tap_fire"] = "weapon.generic_heavy_smg_tier2_kruger_hmp",
        ["active.fire_pdw_tier_1_tap_fire"] = "weapon.generic_pdw_tier1_scp",
        ["active.fire_smg_tier_1_tap_fire"] = "weapon.generic_smg_tier1_ppp",
        ["active.fire_smg_tap_fire_hi_cap"] = "weapon.generic_smg_tier1_ppp_HiCap",
        ["active.fire_smg_tap_fire_suppressed"] = "weapon.generic_smg_tier1_ppp_sof",
        ["active.fire_smg_tier_2_tap_fire"] = "weapon.generic_smg_tier2_griffin",
    };

    // Range-overriding attack skills that do not belong to WeaponTemplate or
    // ModularVehicleWeaponTemplate parents. VMAT is granted by a vehicle accessory,
    // so it must be balanced directly at the SkillTemplate level.
    private static readonly Dictionary<string, (string SourceId, int Target, WeaponFamily Family)> DirectSkillTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active.launch_vmat_missile"] = ("accessory.vehicle_vmat_missile", 18, WeaponFamily.Launchers),
    };

    private static readonly Dictionary<string, int> OriginalRanges = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> OriginalSkillRanges = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TouchedKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TouchedSkillKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> CustomOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> UnmatchedRanged = new();
    private static readonly List<Result> LastResults = new();
    private static readonly List<SkillResult> LastSkillResults = new();

    private static MelonLogger.Instance? _logger;
    private static bool _enabled = true;
    private static bool _autoBalanceUnlisted = true;
    private static bool _applyWeaponTemplates = true;
    private static bool _applyVehicleTemplates = true;
    private static bool _logWeaponTable;
    private static bool _debugLogging;
    private static bool _templatesSeen;
    private static float _overallStrength = 100f;
    private static float _closeStrength = 100f;
    private static float _smgStrength = 100f;
    private static float _rifleStrength = 100f;
    private static float _precisionStrength = 100f;
    private static float _machineGunStrength = 100f;
    private static float _launcherStrength = 100f;
    private static float _artilleryStrength = 100f;
    private static float _vehicleStrength = 100f;
    private static float _exoticStrength = 100f;
    private static float _otherStrength = 100f;
    private static bool _enableJsonOverrides = true;
    private static string _overrideFilePath = string.Empty;
    private static string _overrideFileSignature = string.Empty;
    private static DateTime _nextOverridePollUtc = DateTime.MinValue;
    private static bool _overrideLoadEnabledState;

    private static int _regularCount;
    private static int _vehicleCount;
    private static int _realisticCount;
    private static int _autoCount;
    private static int _customCount;
    private static int _writeFailures;
    private static int _linkedSkillCount;
    private static int _skillWriteFailures;
    private static string _lastApplyReason = "not applied yet";
    private static int? _lastLoggedTableFingerprint;
    private static string _lastWriteFailureSignature = string.Empty;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("General");
            s.AddToggle("EnableGameplay", "Enable Weapon Ranges Rebalanced", true);
            s.AddSlider("OverallStrength", "Overall rebalance strength (%)", 0f, 150f, 100f);
            s.AddToggle("AutoBalanceUnlisted", "Auto-balance new / unlisted weapons", true);
            s.AddToggle("ApplyWeaponTemplates", "Apply infantry / special / turret weapons", true);
            s.AddToggle("ApplyVehicleTemplates", "Apply modular vehicle weapons", true);

            s.AddHeader("Family Strength");
            s.AddSlider("CloseStrength", "Shotguns / flamers / close weapons (%)", 0f, 200f, 100f);
            s.AddSlider("SmgStrength", "SMG / PDW (%)", 0f, 200f, 100f);
            s.AddSlider("RifleStrength", "Rifles / carbines (%)", 0f, 200f, 100f);
            s.AddSlider("PrecisionStrength", "DMR / sniper / anti-materiel (%)", 0f, 200f, 100f);
            s.AddSlider("MachineGunStrength", "Machine guns / autocannons (%)", 0f, 200f, 100f);
            s.AddSlider("LauncherStrength", "Grenade / rocket / AT launchers (%)", 0f, 200f, 100f);
            s.AddSlider("ArtilleryStrength", "Mortars / artillery / rocket organs (%)", 0f, 200f, 100f);
            s.AddSlider("VehicleStrength", "Vehicle / turret weapons (%)", 0f, 200f, 100f);
            s.AddSlider("ExoticStrength", "Energy / alien / construct weapons (%)", 0f, 200f, 100f);
            s.AddSlider("OtherStrength", "Other matched weapons (%)", 0f, 200f, 100f);

            s.AddHeader("Exact Overrides");
            s.AddToggle("EnableJsonOverrides", "Enable weapon-overrides.json", true);

            s.AddHeader("Diagnostics");
            s.AddToggle("DebugWeaponTable", "Log full weapon table (debug)", false);
            s.AddToggle("DebugLogging", "Debug logging", false);
        });

        _overrideFilePath = ResolveOverrideFilePath();
        EnsureOverrideFileExists();
        ReloadSettings();
        LoadCustomOverrides(force: true);
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);

        DebugLog($"{Version}: initialized. Targets={RealisticWeaponTargets.Count} weapons + {RealisticVehicleTargets.Count} modular vehicle weapons; linked skills={LinkedSkillParents.Count + DirectSkillTargets.Count}.");
        DebugLog($"Exact overrides file: {_overrideFilePath}");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Match StrongerCover's current MelonLoader timing pattern: wait a few frames
        // after the Unity scene transition before touching IL2CPP-backed templates.
        MelonCoroutines.Start(DelayedSceneApply(sceneName));
    }

    private static IEnumerator DelayedSceneApply(string sceneName)
    {
        for (int i = 0; i < 10; i++)
            yield return null;

        ApplyAll("scene loaded: " + sceneName);
    }

    public void OnUpdate()
    {
        if (!_enableJsonOverrides || string.IsNullOrWhiteSpace(_overrideFilePath)) return;

        DateTime now = DateTime.UtcNow;
        if (now < _nextOverridePollUtc) return;
        _nextOverridePollUtc = now.AddSeconds(1);

        string signature = GetOverrideFileSignature();
        if (string.Equals(signature, _overrideFileSignature, StringComparison.Ordinal)) return;

        if (_templatesSeen)
            ApplyAll(OverrideFileName + " changed");
        else
            LoadCustomOverrides(force: true);
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, SettingsGroup, StringComparison.Ordinal)) return;

        ReloadSettings();

        if (string.Equals(key, "DebugWeaponTable", StringComparison.Ordinal))
            _lastLoggedTableFingerprint = null;

        if (_templatesSeen)
            ApplyAll("setting changed: " + key);

        DebugLog($"Setting changed: {key}={value}.");
    }

    private static void ReloadSettings()
    {
        _enabled = Setting("EnableGameplay", true);
        _autoBalanceUnlisted = Setting("AutoBalanceUnlisted", true);
        _applyWeaponTemplates = Setting("ApplyWeaponTemplates", true);
        _applyVehicleTemplates = Setting("ApplyVehicleTemplates", true);
        _logWeaponTable = Setting("DebugWeaponTable", false);
        _debugLogging = Setting("DebugLogging", false);
        _enableJsonOverrides = Setting("EnableJsonOverrides", true);

        _overallStrength = ClampPercent(Setting("OverallStrength", 100f), 0f, 150f);
        _closeStrength = ClampPercent(Setting("CloseStrength", 100f), 0f, 200f);
        _smgStrength = ClampPercent(Setting("SmgStrength", 100f), 0f, 200f);
        _rifleStrength = ClampPercent(Setting("RifleStrength", 100f), 0f, 200f);
        _precisionStrength = ClampPercent(Setting("PrecisionStrength", 100f), 0f, 200f);
        _machineGunStrength = ClampPercent(Setting("MachineGunStrength", 100f), 0f, 200f);
        _launcherStrength = ClampPercent(Setting("LauncherStrength", 100f), 0f, 200f);
        _artilleryStrength = ClampPercent(Setting("ArtilleryStrength", 100f), 0f, 200f);
        _vehicleStrength = ClampPercent(Setting("VehicleStrength", 100f), 0f, 200f);
        _exoticStrength = ClampPercent(Setting("ExoticStrength", 100f), 0f, 200f);
        _otherStrength = ClampPercent(Setting("OtherStrength", 100f), 0f, 200f);
    }

    private static T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private static float ClampPercent(float value, float min, float max) => Math.Clamp(value, min, max);

    private static string ResolveOverrideFilePath()
    {
        try
        {
            string assemblyLocation = typeof(Plugin).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(assemblyLocation))
            {
                DirectoryInfo? dir = new FileInfo(assemblyLocation).Directory;
                for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
                {
                    string manifest = Path.Combine(dir.FullName, "modpack.json");
                    if (File.Exists(manifest))
                    {
                        string text = File.ReadAllText(manifest);
                        if (text.Contains(ModpackId, StringComparison.OrdinalIgnoreCase) ||
                            text.Contains(SuiteModpackId, StringComparison.OrdinalIgnoreCase))
                            return Path.Combine(dir.FullName, OverrideFileName);
                    }
                }
            }
        }
        catch
        {
        }

        string modsRoot = Path.Combine(Environment.CurrentDirectory, "Mods");
        try
        {
            if (Directory.Exists(modsRoot))
            {
                foreach (string dir in Directory.GetDirectories(modsRoot))
                {
                    string manifest = Path.Combine(dir, "modpack.json");
                    if (!File.Exists(manifest)) continue;

                    string text = File.ReadAllText(manifest);
                    if (text.Contains(ModpackId, StringComparison.OrdinalIgnoreCase) ||
                        text.Contains(SuiteModpackId, StringComparison.OrdinalIgnoreCase))
                        return Path.Combine(dir, OverrideFileName);
                }
            }
        }
        catch
        {
        }

        return Path.Combine(modsRoot, "WeaponRangesRebalanced", OverrideFileName);
    }

    private static void EnsureOverrideFileExists()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_overrideFilePath) || File.Exists(_overrideFilePath)) return;

            string? directory = Path.GetDirectoryName(_overrideFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_overrideFilePath, "{}\n");
        }
        catch (Exception ex)
        {
            Warn($"Could not create {OverrideFileName}: {ex.Message}");
        }
    }

    private static string GetOverrideFileSignature()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_overrideFilePath) || !File.Exists(_overrideFilePath))
                return "missing";

            var info = new FileInfo(_overrideFilePath);
            return info.LastWriteTimeUtc.Ticks + ":" + info.Length;
        }
        catch
        {
            return "unreadable";
        }
    }

    private static void LoadCustomOverrides(bool force = false)
    {
        if (string.IsNullOrWhiteSpace(_overrideFilePath))
            _overrideFilePath = ResolveOverrideFilePath();

        string signature = GetOverrideFileSignature();
        bool enabledStateChanged = _overrideLoadEnabledState != _enableJsonOverrides;

        if (!_enableJsonOverrides)
        {
            CustomOverrides.Clear();
            _overrideFileSignature = signature;
            _overrideLoadEnabledState = false;
            return;
        }

        if (!force && !enabledStateChanged && string.Equals(signature, _overrideFileSignature, StringComparison.Ordinal))
            return;

        if (!File.Exists(_overrideFilePath))
        {
            CustomOverrides.Clear();
            _overrideFileSignature = signature;
            _overrideLoadEnabledState = true;
            if (_debugLogging)
                DebugLog($"{OverrideFileName} is missing; no exact overrides are active.");
            return;
        }

        try
        {
            string json = File.ReadAllText(_overrideFilePath);
            var parsed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!TryParseOverrideJson(json, parsed, out string error))
            {
                _overrideFileSignature = signature;
                _overrideLoadEnabledState = true;
                Warn($"Could not load {OverrideFileName}: {error}. Keeping the previous valid overrides.");
                return;
            }

            CustomOverrides.Clear();
            foreach (var pair in parsed)
                CustomOverrides[pair.Key] = pair.Value;

            _overrideFileSignature = signature;
            _overrideLoadEnabledState = true;
            if (CustomOverrides.Count > 0)
                DebugLog($"Loaded {CustomOverrides.Count} exact weapon override(s) from {OverrideFileName}.");
        }
        catch (Exception ex)
        {
            _overrideFileSignature = signature;
            _overrideLoadEnabledState = true;
            Warn($"Could not read {OverrideFileName}: {ex.Message}. Keeping the previous valid overrides.");
        }
    }

    private static bool TryParseOverrideJson(string json, Dictionary<string, int> output, out string error)
    {
        error = string.Empty;
        int i = 0;
        SkipJsonWhitespace(json, ref i);
        if (i >= json.Length || json[i] != '{')
        {
            error = "expected a JSON object beginning with '{'";
            return false;
        }
        i++;
        SkipJsonWhitespace(json, ref i);

        if (i < json.Length && json[i] == '}')
        {
            i++;
            SkipJsonWhitespace(json, ref i);
            if (i != json.Length)
            {
                error = "unexpected content after the JSON object";
                return false;
            }
            return true;
        }

        while (i < json.Length)
        {
            if (!TryReadJsonString(json, ref i, out string id, out error))
                return false;

            SkipJsonWhitespace(json, ref i);
            if (i >= json.Length || json[i] != ':')
            {
                error = $"expected ':' after '{id}'";
                return false;
            }
            i++;
            SkipJsonWhitespace(json, ref i);

            int numberStart = i;
            if (i < json.Length && (json[i] == '-' || json[i] == '+')) i++;
            while (i < json.Length && char.IsDigit(json[i])) i++;
            if (numberStart == i || (i == numberStart + 1 && (json[numberStart] == '-' || json[numberStart] == '+')))
            {
                error = $"range for '{id}' must be an integer";
                return false;
            }

            string numberText = json.Substring(numberStart, i - numberStart);
            if (!int.TryParse(numberText, out int range) || range < 1 || range > 60)
            {
                error = $"range for '{id}' must be an integer from 1 to 60";
                return false;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                error = "weapon IDs cannot be empty";
                return false;
            }
            output[id] = range;

            SkipJsonWhitespace(json, ref i);
            if (i >= json.Length)
            {
                error = "unexpected end of file; expected ',' or '}'";
                return false;
            }
            if (json[i] == '}')
            {
                i++;
                SkipJsonWhitespace(json, ref i);
                if (i != json.Length)
                {
                    error = "unexpected content after the JSON object";
                    return false;
                }
                return true;
            }
            if (json[i] != ',')
            {
                error = "expected ',' between override entries";
                return false;
            }
            i++;
            SkipJsonWhitespace(json, ref i);
        }

        error = "unexpected end of file";
        return false;
    }

    private static void SkipJsonWhitespace(string json, ref int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
    }

    private static bool TryReadJsonString(string json, ref int i, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;
        SkipJsonWhitespace(json, ref i);
        if (i >= json.Length || json[i] != '"')
        {
            error = "expected a quoted weapon ID";
            return false;
        }
        i++;

        int start = i;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                value = json.Substring(start, i - start);
                i++;
                return true;
            }

            if (c == '\\')
            {
                error = "escape sequences are not supported in weapon IDs";
                return false;
            }

            if (char.IsControl(c))
            {
                error = "control characters are not allowed in weapon IDs";
                return false;
            }

            i++;
        }

        error = "unterminated quoted weapon ID";
        return false;
    }

    private static void ApplyAll(string reason)
    {
        try
        {
            ReloadSettings();
            LoadCustomOverrides();

            LastResults.Clear();
            LastSkillResults.Clear();
            UnmatchedRanged.Clear();
            _regularCount = 0;
            _vehicleCount = 0;
            _realisticCount = 0;
            _autoCount = 0;
            _customCount = 0;
            _writeFailures = 0;
            _linkedSkillCount = 0;
            _skillWriteFailures = 0;
            _lastApplyReason = reason;

            // ModularVehicleWeaponTemplate derives from WeaponTemplate in the current game API.
            // Exclude those IDs from the base WeaponTemplate pass so vehicle weapons are processed
            // exactly once with the vehicle table/settings and their true pre-WRR range is captured.
            HashSet<string> modularVehicleNames = GetTemplateNames("ModularVehicleWeaponTemplate");

            if (_applyWeaponTemplates)
                ProcessType("WeaponTemplate", RealisticWeaponTargets, false, modularVehicleNames);
            else
                RestoreType("WeaponTemplate", modularVehicleNames);

            if (_applyVehicleTemplates)
                ProcessType("ModularVehicleWeaponTemplate", RealisticVehicleTargets, true);
            else
                RestoreType("ModularVehicleWeaponTemplate");

            ProcessLinkedSkillRanges();
            ApplyIndirectFireAiPositioningPolicy();

            _templatesSeen = (_regularCount + _vehicleCount) > 0;

            if (!_templatesSeen)
            {
                DebugLog("Weapon templates are not loaded yet; retrying on a later scene load.");
                return;
            }

            DebugLog($"Applied range pass ({reason}). Found {_regularCount} WeaponTemplate + {_vehicleCount} ModularVehicleWeaponTemplate. realistic={_realisticCount}, auto={_autoCount}, custom={_customCount}, unmatched ranged={UnmatchedRanged.Count}, linked skills={_linkedSkillCount}, write failures={_writeFailures}, skill write failures={_skillWriteFailures}, enabled={_enabled}.");

            if (UnmatchedRanged.Count > 0 && _debugLogging)
            {
                string preview = string.Join(", ", UnmatchedRanged.Take(8));
                DebugLog($"{UnmatchedRanged.Count} ranged templates were not recognized and were kept at their original range. First entries: {preview}");
            }

            WarnWriteFailuresIfChanged();

            if (_logWeaponTable)
                LogWeaponTableIfChanged();
        }
        catch (Exception ex)
        {
            Error("Failed to apply weapon ranges: " + ex);
        }
    }

    private static void ProcessType(string typeName, Dictionary<string, int> realisticTargets, bool isVehicleType, HashSet<string>? excludedNames = null)
    {
        var templates = Templates.FindAll(typeName);
        if (templates == null || templates.Length == 0) return;

        int processedCount = 0;

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string name = template.GetName() ?? string.Empty;
            if (name.Length == 0) continue;
            if (excludedNames != null && excludedNames.Contains(name)) continue;

            processedCount++;

            string key = typeName + "|" + name;
            int minRange = ReadIntField(template, "MinRange");
            int idealRange = ReadIntField(template, "IdealRange");
            int current = ReadIntField(template, "MaxRange");
            if (current < 0) continue;

            if (!OriginalRanges.TryGetValue(key, out int vanilla))
            {
                vanilla = current;
                OriginalRanges[key] = vanilla;
            }

            WeaponFamily family = ClassifyFamily(name, typeName);
            BalanceSource source = BalanceSource.Vanilla;
            int target = vanilla;

            if (CustomOverrides.TryGetValue(name, out int exact))
            {
                target = exact;
                source = BalanceSource.Custom;
                _customCount++;
            }
            else if (realisticTargets.TryGetValue(name, out int realistic))
            {
                target = realistic;
                source = BalanceSource.Realistic;
                _realisticCount++;
            }
            else if (_autoBalanceUnlisted && TryInferTarget(name, typeName, vanilla, out int inferred, out WeaponFamily inferredFamily))
            {
                target = inferred;
                family = inferredFamily;
                source = BalanceSource.Auto;
                _autoCount++;
            }
            else if (vanilla >= 3 && LooksLikeRangedWeapon(name))
            {
                UnmatchedRanged.Add(name);
            }

            int desired;
            if (!_enabled)
            {
                desired = vanilla;
            }
            else if (source == BalanceSource.Custom)
            {
                desired = target;
            }
            else if (source == BalanceSource.Vanilla)
            {
                desired = vanilla;
            }
            else
            {
                float strength = GetEffectiveStrength(family);
                desired = InterpolateRange(vanilla, target, strength);
            }

            bool shouldOwnValue = _enabled && source != BalanceSource.Vanilla;
            bool shouldRestoreOwnedValue = !shouldOwnValue && TouchedKeys.Contains(key);
            bool written = true;

            if (shouldOwnValue)
            {
                if (current != desired)
                    written = Templates.WriteField(template, "MaxRange", desired);
                if (written)
                    TouchedKeys.Add(key);
            }
            else if (shouldRestoreOwnedValue)
            {
                desired = vanilla;
                if (current != desired)
                    written = Templates.WriteField(template, "MaxRange", desired);
                if (written)
                    TouchedKeys.Remove(key);
            }
            else
            {
                // WRR has never owned this value. Leave it exactly as found so unrelated
                // mods that alter unrecognized templates are not overwritten.
                desired = current;
            }

            int readBack = ReadIntField(template, "MaxRange");
            if (!written || (readBack >= 0 && readBack != desired))
                _writeFailures++;

            LastResults.Add(new Result
            {
                TypeName = typeName,
                Name = name,
                MinRange = minRange,
                IdealRange = idealRange,
                Vanilla = vanilla,
                Target = target,
                Applied = readBack >= 0 ? readBack : desired,
                Family = family,
                Source = source,
                Written = written && (readBack < 0 || readBack == desired)
            });
        }

        if (isVehicleType) _vehicleCount = processedCount;
        else _regularCount = processedCount;
    }

    private static MemberInfo? FindMemberRecursive(Type? type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? current = type; current != null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(name, flags);
            if (field != null) return field;

            PropertyInfo? property = current.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0) return property;
        }
        return null;
    }

    private static void ApplyIndirectFireAiPositioningPolicy()
    {
        var templates = Templates.FindAll("SkillTemplate");
        if (templates == null || templates.Length == 0) return;

        int matched = 0;
        int disabled = 0;
        int restored = 0;
        int failures = 0;

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string name = template.GetName() ?? string.Empty;
            if (!IsLongRangeIndirectFireAiSkill(template, name)) continue;
            matched++;

            try
            {
                object? aiConfig = Templates.ReadField(template, "AIConfig");
                if (aiConfig == null)
                {
                    failures++;
                    if (_debugLogging) Warn($"[INDIRECT_AI_POLICY] skill={name} AIConfig is null; positioning policy was not applied.");
                    continue;
                }

                MemberInfo? member = FindMemberRecursive(aiConfig.GetType(), "InfluencesPositioning");
                if (!TryReadBoolMember(member, aiConfig, out bool current))
                {
                    failures++;
                    if (_debugLogging)
                        Warn($"[INDIRECT_AI_POLICY] skill={name} could not read InfluencesPositioning from runtime wrapper {aiConfig.GetType().FullName}.");
                    continue;
                }

                if (!OriginalIndirectFireInfluencesPositioning.ContainsKey(name))
                    OriginalIndirectFireInfluencesPositioning[name] = current;

                bool desired = _enabled ? false : OriginalIndirectFireInfluencesPositioning[name];
                bool writeOk = current == desired || TryWriteBoolMember(member, aiConfig, desired);
                bool readBackOk = TryReadBoolMember(member, aiConfig, out bool finalValue);

                if (!writeOk || !readBackOk || finalValue != desired)
                {
                    failures++;
                    Warn(
                        $"Indirect-fire AI positioning policy verification failed for {name}: " +
                        $"wanted InfluencesPositioning={desired}, " +
                        $"readBack={(readBackOk ? finalValue.ToString() : "unreadable")}.");
                    continue;
                }

                if (_enabled) disabled++;
                else restored++;
            }
            catch (Exception ex)
            {
                failures++;
                if (_debugLogging)
                    Warn($"[INDIRECT_AI_POLICY] skill={name} update failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (_debugLogging)
            DebugLog($"[INDIRECT_AI_POLICY] matched={matched} disabled={disabled} restored={restored} failures={failures} enabled={_enabled}.");
    }

    private static bool IsLongRangeIndirectFireAiSkill(GameObj template, string name)
    {
        if (!name.StartsWith("active.", StringComparison.OrdinalIgnoreCase)) return false;

        bool familyMatch = false;
        foreach (string token in IndirectFireAiIdTokens)
        {
            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) continue;
            familyMatch = true;
            break;
        }
        if (!familyMatch) return false;

        object? isAttackRaw = Templates.ReadField(template, "IsAttack");
        object? needsLofRaw = Templates.ReadField(template, "IsLineOfFireNeeded");
        if (!TryConvertBool(isAttackRaw, out bool isAttack) || !isAttack) return false;
        if (!TryConvertBool(needsLofRaw, out bool needsLineOfFire)) return false;

        // Direct-fire artillery (for example the 15 cm assault howitzer) must retain
        // normal AI positioning. This policy is intentionally restricted to attacks
        // that do not require line of fire.
        return !needsLineOfFire;
    }

    private static bool TryConvertBool(object? raw, out bool value)
    {
        value = false;
        if (raw is bool b)
        {
            value = b;
            return true;
        }
        if (raw is byte by)
        {
            value = by != 0;
            return true;
        }
        if (raw is int i)
        {
            value = i != 0;
            return true;
        }
        return false;
    }

    private static bool TryReadBoolMember(MemberInfo? member, object instance, out bool value)
    {
        value = false;
        if (member == null) return false;

        try
        {
            object? raw = member switch
            {
                FieldInfo field => field.GetValue(instance),
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(instance),
                _ => null
            };

            if (raw is bool b)
            {
                value = b;
                return true;
            }
            if (raw is byte by)
            {
                value = by != 0;
                return true;
            }
            if (raw is int i)
            {
                value = i != 0;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteBoolMember(MemberInfo? member, object instance, bool value)
    {
        if (member == null) return false;

        try
        {
            if (member is FieldInfo field)
            {
                if (field.FieldType == typeof(bool)) field.SetValue(instance, value);
                else if (field.FieldType == typeof(byte)) field.SetValue(instance, value ? (byte)1 : (byte)0);
                else if (field.FieldType == typeof(int)) field.SetValue(instance, value ? 1 : 0);
                else return false;
                return true;
            }

            if (member is PropertyInfo property && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                if (property.PropertyType == typeof(bool)) property.SetValue(instance, value);
                else if (property.PropertyType == typeof(byte)) property.SetValue(instance, value ? (byte)1 : (byte)0);
                else if (property.PropertyType == typeof(int)) property.SetValue(instance, value ? 1 : 0);
                else return false;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void ProcessLinkedSkillRanges()
    {
        var templates = Templates.FindAll("SkillTemplate");
        if (templates == null || templates.Length == 0)
        {
            RestoreLinkedSkills();
            return;
        }

        var parentResults = new Dictionary<string, Result>(StringComparer.OrdinalIgnoreCase);
        foreach (Result result in LastResults)
            parentResults[result.Name] = result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string skillName = template.GetName() ?? string.Empty;
            bool isLinked = LinkedSkillParents.TryGetValue(skillName, out string parentWeapon);
            bool isScaledSecondary = ScaledSecondarySkillParents.TryGetValue(skillName, out string scaledParentWeapon);
            bool isDirect = DirectSkillTargets.TryGetValue(skillName, out var directTarget);
            if (!isLinked && !isScaledSecondary && !isDirect) continue;

            string sourceId = isDirect ? directTarget.SourceId : (isScaledSecondary ? scaledParentWeapon : parentWeapon);
            seen.Add(skillName);
            _linkedSkillCount++;

            string key = "SkillTemplate|" + skillName;
            int current = ReadIntField(template, "MaxRange");
            if (current < 0) continue;

            if (!OriginalSkillRanges.TryGetValue(key, out int vanilla))
            {
                vanilla = current;
                OriginalSkillRanges[key] = vanilla;
            }

            int target = vanilla;
            int desired = vanilla;
            bool shouldOwn = false;

            if (isDirect)
            {
                target = directTarget.Target;
                if (_enabled)
                {
                    float strength = GetEffectiveStrength(directTarget.Family);
                    if (strength > 0f)
                    {
                        desired = InterpolateRange(vanilla, target, strength);
                        shouldOwn = true;
                    }
                }
            }
            else if (isScaledSecondary && parentResults.TryGetValue(scaledParentWeapon, out Result? scaledParent) && _enabled && scaledParent.Source != BalanceSource.Vanilla)
            {
                target = ScaleSecondaryRange(vanilla, scaledParent.Vanilla, scaledParent.Target);
                desired = ScaleSecondaryRange(vanilla, scaledParent.Vanilla, scaledParent.Applied);
                shouldOwn = true;
            }
            else if (isLinked && parentResults.TryGetValue(parentWeapon, out Result? parent) && _enabled && parent.Source != BalanceSource.Vanilla)
            {
                target = parent.Target;

                if (parent.Source == BalanceSource.Custom)
                {
                    desired = target;
                    shouldOwn = true;
                }
                else
                {
                    float strength = GetEffectiveStrength(parent.Family);
                    if (strength > 0f)
                    {
                        desired = InterpolateRange(vanilla, target, strength);
                        shouldOwn = true;
                    }
                }
            }

            bool written = true;
            bool shouldRestore = !shouldOwn && TouchedSkillKeys.Contains(key);

            if (shouldOwn)
            {
                if (current != desired)
                    written = Templates.WriteField(template, "MaxRange", desired);
                if (written)
                    TouchedSkillKeys.Add(key);
            }
            else if (shouldRestore)
            {
                desired = vanilla;
                if (current != desired)
                    written = Templates.WriteField(template, "MaxRange", desired);
                if (written)
                    TouchedSkillKeys.Remove(key);
            }
            else
            {
                desired = current;
            }

            int readBack = ReadIntField(template, "MaxRange");
            if (!written || (readBack >= 0 && readBack != desired))
                _skillWriteFailures++;

            LastSkillResults.Add(new SkillResult
            {
                Name = skillName,
                ParentWeapon = sourceId,
                Vanilla = vanilla,
                Target = target,
                Applied = readBack >= 0 ? readBack : desired,
                Written = written && (readBack < 0 || readBack == desired)
            });
        }

        // If a linked skill disappears because its owning template/group is unavailable,
        // restore only values WRR previously touched.
        foreach (string skillName in LinkedSkillParents.Keys.Concat(ScaledSecondarySkillParents.Keys).Concat(DirectSkillTargets.Keys))
        {
            if (seen.Contains(skillName)) continue;
            RestoreLinkedSkill(skillName);
        }
    }

    private static void RestoreLinkedSkills()
    {
        foreach (string skillName in LinkedSkillParents.Keys.Concat(ScaledSecondarySkillParents.Keys).Concat(DirectSkillTargets.Keys))
            RestoreLinkedSkill(skillName);
    }

    private static void RestoreLinkedSkill(string skillName)
    {
        string key = "SkillTemplate|" + skillName;
        if (!TouchedSkillKeys.Contains(key)) return;
        if (!OriginalSkillRanges.TryGetValue(key, out int vanilla)) return;

        var templates = Templates.FindAll("SkillTemplate");
        if (templates == null) return;

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string name = template.GetName() ?? string.Empty;
            if (!string.Equals(name, skillName, StringComparison.OrdinalIgnoreCase)) continue;

            if (Templates.WriteField(template, "MaxRange", vanilla))
                TouchedSkillKeys.Remove(key);
            return;
        }
    }

    private static HashSet<string> GetTemplateNames(string typeName)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var templates = Templates.FindAll(typeName);
        if (templates == null) return names;

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string name = template.GetName() ?? string.Empty;
            if (name.Length > 0) names.Add(name);
        }

        return names;
    }

    private static void RestoreType(string typeName, HashSet<string>? excludedNames = null)
    {
        var templates = Templates.FindAll(typeName);
        if (templates == null || templates.Length == 0) return;

        int processedCount = 0;

        foreach (var template in templates)
        {
            if (template.IsNull) continue;
            string name = template.GetName() ?? string.Empty;
            if (name.Length == 0) continue;
            if (excludedNames != null && excludedNames.Contains(name)) continue;
            processedCount++;
            string key = typeName + "|" + name;
            if (!TouchedKeys.Contains(key))
                continue;

            if (OriginalRanges.TryGetValue(key, out int original) && Templates.WriteField(template, "MaxRange", original))
                TouchedKeys.Remove(key);
        }

        if (typeName == "WeaponTemplate") _regularCount = processedCount;
        else if (typeName == "ModularVehicleWeaponTemplate") _vehicleCount = processedCount;
    }

    private static int ReadIntField(GameObj template, string fieldName)
    {
        try
        {
            object value = Templates.ReadField(template, fieldName);
            return value == null ? -1 : Convert.ToInt32(value);
        }
        catch
        {
            return -1;
        }
    }

    private static float GetEffectiveStrength(WeaponFamily family)
    {
        float familyStrength = family switch
        {
            WeaponFamily.CloseQuarters => _closeStrength,
            WeaponFamily.SmgPdw => _smgStrength,
            WeaponFamily.Rifles => _rifleStrength,
            WeaponFamily.Precision => _precisionStrength,
            WeaponFamily.MachineGuns => _machineGunStrength,
            WeaponFamily.Launchers => _launcherStrength,
            WeaponFamily.Artillery => _artilleryStrength,
            WeaponFamily.VehicleTurret => _vehicleStrength,
            WeaponFamily.Exotic => _exoticStrength,
            _ => _otherStrength
        };

        return Math.Clamp((_overallStrength * familyStrength) / 100f, 0f, 300f);
    }

    private static int ScaleSecondaryRange(int skillVanilla, int parentVanilla, int parentRange)
    {
        if (skillVanilla < 1 || parentVanilla < 1) return skillVanilla;
        double ratio = (double)parentRange / parentVanilla;
        int scaled = (int)Math.Round(skillVanilla * ratio, MidpointRounding.AwayFromZero);
        return Math.Clamp(scaled, 1, 60);
    }

    private static int InterpolateRange(int vanilla, int target, float strengthPercent)
    {
        float t = strengthPercent / 100f;
        return Math.Clamp((int)Math.Round(vanilla + ((target - vanilla) * t)), 1, 60);
    }

    private static WeaponFamily ClassifyFamily(string name, string typeName)
    {
        string n = name.ToLowerInvariant();

        if (typeName == "ModularVehicleWeaponTemplate" || n.StartsWith("turret."))
            return WeaponFamily.VehicleTurret;
        if (ContainsAny(n, "mortar", "howitzer", "rocket_organ", "rocket organ"))
            return WeaponFamily.Artillery;
        if (ContainsAny(n, "atgm", "rpg", "rocket_launcher", "rocketlauncher", "grenade_launcher", "airburst"))
            return WeaponFamily.Launchers;
        if (ContainsAny(n, "sniper", "designated_marksman", "anti_materiel", "anti-materiel", "anti_tank_rifle", "anti-tank_rifle", "dmr"))
            return WeaponFamily.Precision;
        if (ContainsAny(n, "machinegun", "machine_gun", "light_mg", "medium_mg", "heavy_mg", "twin_hmg", "minigun", "chaingun", "autocannon", "automatic_rifle"))
            return WeaponFamily.MachineGuns;
        if (ContainsAny(n, "smg", "pdw"))
            return WeaponFamily.SmgPdw;
        if (ContainsAny(n, "shotgun", "flamethrower", "flamer", "corridor_sweeper", "mining_laser"))
            return WeaponFamily.CloseQuarters;
        if (ContainsAny(n, "laser", "plasma", "repeater", "acid", "disruptor", "electro_magnetic", "electromagnetic", "alien_", "construct_"))
            return WeaponFamily.Exotic;
        if (ContainsAny(n, "rifle", "carbine"))
            return WeaponFamily.Rifles;

        return WeaponFamily.Other;
    }

    private static bool TryInferTarget(string name, string typeName, int vanilla, out int target, out WeaponFamily family)
    {
        string n = name.ToLowerInvariant();
        family = ClassifyFamily(name, typeName);
        target = vanilla;

        // Never auto-rebalance obvious melee/very-short-range templates.
        if (vanilla <= 2 || ContainsAny(n, "melee", "claw", "bite", "talon", "ram_attack"))
            return false;

        if (typeName == "ModularVehicleWeaponTemplate" || n.StartsWith("turret."))
        {
            if (ContainsAny(n, "scanner", "radar")) target = 30;
            else if (ContainsAny(n, "flamethrower", "flamer")) target = n.Contains("heavy") ? 5 : 4;
            else if (n.Contains("rocket_organ")) target = 26;
            else if (n.Contains("mortar")) target = n.Contains("medium") ? 42 : 48;
            else if (n.Contains("howitzer")) target = 22;
            else if (ContainsAny(n, "atgm", "guided_missile")) target = 24;
            else if (n.Contains("cannon_long")) target = 22;
            else if (n.Contains("cannon_short")) target = 19;
            else if (ContainsAny(n, "anti_materiel", "anti-materiel")) target = 16;
            else if (n.Contains("anti_tank_gun")) target = 21;
            else if (n.Contains("autocannon")) target = 20;
            else if (ContainsAny(n, "heavy_mg", "heavy_machinegun", "twin_hmg", "twin_heavy_machine")) target = 19;
            else if (ContainsAny(n, "medium_mg", "medium_machinegun")) target = n.StartsWith("turret.") ? 16 : 15;
            else if (ContainsAny(n, "light_mg", "light_machinegun")) target = 13;
            else if (n.Contains("minigun")) target = 12;
            else if (n.Contains("chaingun")) target = 18;
            else if (n.Contains("laser_lance")) target = 20;
            else if (n.Contains("laser_rifle")) target = 18;
            else if (n.Contains("auto_laser")) target = 18;
            else if (n.Contains("plasma_cannon")) target = 20;
            else if (n.Contains("plasma_rifle")) target = 13;
            else if (ContainsAny(n, "heavy_auto_repeater", "twin_heavy_auto_repeater")) target = 18;
            else if (n.Contains("disruptor")) target = 15;
            else if (n.Contains("multi_rocket_pod")) target = 16;
            else if (n.Contains("rocket_launcher")) target = 15;
            else if (n.Contains("airburst")) target = 15;
            else if (ContainsAny(n, "cannon", "gun", "laser", "plasma", "rocket")) target = Math.Clamp(vanilla + Math.Max(2, vanilla / 4), 4, 30);
            else return false;

            family = WeaponFamily.VehicleTurret;
            return true;
        }

        if (ContainsAny(n, "flamethrower", "plasma_flamethrower")) target = 4;
        else if (ContainsAny(n, "shotgun", "corridor_sweeper")) target = 6;
        else if (n.Contains("mining_laser")) target = 7;
        else if (n.Contains("pirate_emp_rifle")) target = 6;
        else if (n.Contains("heavy_smg")) target = 8;
        else if (n.Contains("smg")) target = TierTarget(n, 6, 7, 9);
        else if (n.Contains("pdw")) target = n.Contains("captain") ? 9 : 7;
        else if (n.Contains("heavy_assault_rifle")) target = 10;
        else if (n.Contains("assault_rifle")) target = TierTarget(n, 11, 12, 14);
        else if (n.Contains("battle_rifle")) target = TierTarget(n, 13, 14, 16);
        else if (n.Contains("carbine")) target = 9;
        else if (ContainsAny(n, "designated_marksman", "dmr")) target = TierTarget(n, 15, 17, 19);
        else if (n.Contains("sniper")) target = TierTarget(n, 16, 18, 20);
        else if (ContainsAny(n, "anti_materiel", "anti-materiel", "anti_tank_rifle", "anti-tank_rifle")) target = 16;
        else if (ContainsAny(n, "light_machinegun", "light_machine_gun", "light_mg")) target = TierTarget(n, 13, 14, 16);
        else if (ContainsAny(n, "medium_machinegun", "medium_machine_gun", "medium_mg")) target = TierTarget(n, 15, 17, 19);
        else if (ContainsAny(n, "heavy_machinegun", "heavy_machine_gun", "heavy_mg")) target = 19;
        else if (n.Contains("infantry_automatic_rifle")) target = 13;
        else if (n.Contains("minigun")) target = 11;
        else if (n.Contains("chaingun")) target = 13;
        else if (n.Contains("autocannon")) target = 15;
        else if (n.Contains("grenade_launcher")) target = n.Contains("tripod") ? 15 : (n.Contains("tier3") ? 13 : 11);
        else if (n.Contains("rocket_organ")) target = 24;
        else if (ContainsAny(n, "atgm", "guided_missile")) target = 18;
        else if (n.Contains("rpg")) target = n.Contains("tier_2") || n.Contains("tier2") ? 12 : 10;
        else if (n.Contains("rocket_launcher")) target = n.Contains("tier2") ? 14 : (n.Contains("crafted") ? 13 : 11);
        else if (n.Contains("mortar")) target = n.Contains("medium") ? 42 : (n.Contains("heavy") ? 48 : 34);
        else if (n.Contains("laser_lance")) target = n.Contains("construct") ? 18 : (n.Contains("pirate") ? 14 : 15);
        else if (n.Contains("laser_rifle")) target = n.Contains("gunslinger") ? 16 : (n.Contains("crafted") || n.Contains("tier2") ? 13 : 11);
        else if (n.Contains("plasma_rifle")) target = n.Contains("construct") ? 13 : 11;
        else if (n.Contains("repeater")) target = n.Contains("phantom") ? 13 : (n.Contains("micro") ? 9 : 11);
        else if (n.Contains("disruptor")) target = 12;
        else if (ContainsAny(n, "acid_spitter", "acid_stalker", "acid")) target = 11;
        else if (ContainsAny(n, "electro_magnetic", "electromagnetic")) target = 14;
        else if (n.Contains("alien") && LooksLikeRangedWeapon(n)) target = Math.Clamp(vanilla + 3, vanilla, 18);
        else if (n.Contains("construct") && LooksLikeRangedWeapon(n)) target = Math.Clamp(vanilla + 2, vanilla, 18);
        else return false;

        family = ClassifyFamily(name, typeName);
        return true;
    }

    private static int TierTarget(string name, int tier1, int tier2, int tier3)
    {
        if (ContainsAny(name, "tier3", "tier_3", "t3")) return tier3;
        if (ContainsAny(name, "tier2", "tier_2", "t2")) return tier2;
        return tier1;
    }

    private static bool LooksLikeRangedWeapon(string name)
    {
        string n = name.ToLowerInvariant();
        return ContainsAny(n,
            "weapon", "turret", "rifle", "gun", "launcher", "mortar", "cannon", "howitzer",
            "laser", "plasma", "repeater", "spitter", "acid", "rocket", "flame", "smg", "pdw",
            "shotgun", "autocannon", "chaingun", "minigun", "machinegun", "machine_gun");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (string needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void WarnWriteFailuresIfChanged()
    {
        if (_writeFailures <= 0 && _skillWriteFailures <= 0)
        {
            _lastWriteFailureSignature = string.Empty;
            return;
        }

        string templatePreview = string.Join(", ", LastResults.Where(r => !r.Written).Select(r => r.Name).Take(6));
        string skillPreview = string.Join(", ", LastSkillResults.Where(r => !r.Written).Select(r => r.Name).Take(6));
        string signature = $"{_writeFailures}|{_skillWriteFailures}|{templatePreview}|{skillPreview}";
        if (string.Equals(signature, _lastWriteFailureSignature, StringComparison.Ordinal))
            return;

        _lastWriteFailureSignature = signature;
        string details = string.Empty;
        if (templatePreview.Length > 0) details += " templates: " + templatePreview;
        if (skillPreview.Length > 0) details += " skills: " + skillPreview;
        Warn($"Range write failures detected: templates={_writeFailures}, skills={_skillWriteFailures}.{details}");
    }

    private static int ComputeTableFingerprint()
    {
        unchecked
        {
            int hash = 17;
            foreach (Result result in LastResults.OrderBy(r => r.TypeName).ThenBy(r => r.Name))
            {
                hash = hash * 31 + result.TypeName.GetHashCode();
                hash = hash * 31 + result.Name.GetHashCode();
                hash = hash * 31 + result.Vanilla;
                hash = hash * 31 + result.Target;
                hash = hash * 31 + result.Applied;
                hash = hash * 31 + result.Source.GetHashCode();
                hash = hash * 31 + result.Written.GetHashCode();
            }

            foreach (SkillResult result in LastSkillResults.OrderBy(r => r.Name))
            {
                hash = hash * 31 + result.Name.GetHashCode();
                hash = hash * 31 + result.ParentWeapon.GetHashCode();
                hash = hash * 31 + result.Vanilla;
                hash = hash * 31 + result.Target;
                hash = hash * 31 + result.Applied;
                hash = hash * 31 + result.Written.GetHashCode();
            }
            return hash;
        }
    }

    private static void LogWeaponTableIfChanged()
    {
        int fingerprint = ComputeTableFingerprint();
        if (_lastLoggedTableFingerprint.HasValue && _lastLoggedTableFingerprint.Value == fingerprint)
            return;

        _lastLoggedTableFingerprint = fingerprint;
        Log("[WRR_TABLE] BEGIN");
        foreach (Result result in LastResults.OrderBy(r => r.TypeName).ThenBy(r => r.Name))
        {
            Log($"[WRR_TABLE] {result.TypeName} | {result.Name} | min={result.MinRange} | ideal={result.IdealRange} | originalMax={result.Vanilla} | targetMax={result.Target} | gameMax={result.Applied} | source={result.Source} | family={result.Family} | write={(result.Written ? "ok" : "failed")}");
        }
        Log("[WRR_TABLE] END");

        if (LastSkillResults.Count > 0)
        {
            Log("[WRR_SKILL] BEGIN");
            foreach (SkillResult result in LastSkillResults.OrderBy(r => r.Name))
            {
                Log($"[WRR_SKILL] SkillTemplate | {result.Name} | parent={result.ParentWeapon} | originalMax={result.Vanilla} | targetMax={result.Target} | gameMax={result.Applied} | write={(result.Written ? "ok" : "failed")}");
            }
            Log("[WRR_SKILL] END");
        }
    }

    private static void DrawDebug(Rect area)
    {
        float y = area.y;
        const float h = 22f;

        void Line(string text)
        {
            GUI.Label(new Rect(area.x, y, area.width, h), text);
            y += h;
        }

        Line($"Weapon Ranges Rebalanced {Version}");
        Line("Enabled: " + (_enabled ? "yes" : "no"));
        Line($"Templates: Weapon {_regularCount}, Modular Vehicle {_vehicleCount}");
        Line($"Coverage: realistic {_realisticCount}, auto {_autoCount}, exact {_customCount}, unmatched {UnmatchedRanged.Count}");
        Line($"JSON overrides: {(_enableJsonOverrides ? "enabled" : "disabled")}, loaded {CustomOverrides.Count}");
        Line($"Linked/scaled/direct attack skills: {_linkedSkillCount}/{LinkedSkillParents.Count + ScaledSecondarySkillParents.Count + DirectSkillTargets.Count}");
        Line($"Write failures: templates {_writeFailures}, skills {_skillWriteFailures}");
        Line($"Overall strength: {_overallStrength:0}%");
        Line("Last pass: " + _lastApplyReason);

        if (UnmatchedRanged.Count > 0)
        {
            Line("Unmatched (kept original):");
            foreach (string name in UnmatchedRanged.Take(6))
                Line("- " + name);
        }
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);

        try
        {
            HashSet<string> modularVehicleNames = GetTemplateNames("ModularVehicleWeaponTemplate");
            RestoreType("WeaponTemplate", modularVehicleNames);
            RestoreType("ModularVehicleWeaponTemplate");
            RestoreLinkedSkills();
        }
        catch
        {
        }
    }

    private static void Log(string message) => _logger?.Msg(message);
    private static void DebugLog(string message)
    {
        if (_debugLogging) _logger?.Msg("[DEBUG] " + message);
    }
    private static void Warn(string message) => _logger?.Warning(message);
    private static void Error(string message) => _logger?.Error(message);
}

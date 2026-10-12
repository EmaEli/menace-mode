using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical.AI;
using HarmonyLib;
using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tools;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.Tactical.TileEffects;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("0.6.2.0")]
[assembly: AssemblyFileVersion("0.6.2.0")]

namespace SmokeOverhaul;

public sealed class Plugin : IModpackPlugin
{
    internal const string ModName = "Smoke Overhaul";
    internal const string Version = "0.6.2";

    private static MelonLogger.Instance? _log;
    private static readonly Dictionary<string, PatchedTemplate> PatchedTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, EntityTemplate> FreeGrenadeTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, EntityTemplate> FreeFragGrenadeTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, PatchedGrenadeSkill> PatchedGrenadeSkills = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> HandThrownGrenadeSkillIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "active.throw_anti_tank_grenade_tier1_grenade_bundle",
        "active.throw_anti_tank_grenade_tier2",
        "active.throw_emp_grenade",
        "active.throw_flashbang",
        "active.throw_frag_grenade_tier2",
        "active.throw_frag_grenade_tier3",
        "active.throw_incendiary_grenade",
        "active.throw_nano_thermite_grenade",
        "active.throw_scrap_bomb_frag_tier1",
        "active.throw_smoke_grenade",
        "active.throw_smoke_grenade_limited",
        "active.throw_corrosive_grenade",
        "active.throw_nova_grenade",
        "active.throw_plasma_grenade"
    };
    private static SkillTemplate? _freeSmokeGrenade;
    private static SkillTemplate? _freeFragGrenade;
    private static bool _grenadeResolveErrorLogged;
    private static bool _fragGrenadeResolveErrorLogged;

    private const string MediumMortarItemId = "specialweapon.tripod_medium_mortar_80mm";
    private static readonly string[] MortarSmokeSkillIds =
    {
        "active.launch_mortar_smoke",
        "active.mod.launch_mortar_smoke_autoloader",
        "active.mod.twinfire.launch_mortar_smoke_autoloader"
    };
    private static readonly Dictionary<string, Il2CppMenace.Tactical.AI.Data.SkillBehavior?> OriginalMortarAiConfigs = new(StringComparer.OrdinalIgnoreCase);
    // The same native CreateLOSBlocker profile can be shared by infantry and mortar skills.
    // Snapshot once per *native pointer*, never once per skill ID, so restoring cannot
    // accidentally leave a shared profile disabled when settings change or the mod unloads.
    private static readonly Dictionary<IntPtr, SmokeAiPositioningSnapshot> OriginalSmokeAiPositioning = new();
    private static readonly string[] FastSmokeAiSkillIds =
    {
        "active.throw_smoke_grenade",
        "active.throw_smoke_grenade_limited",
        "active.launch_mortar_smoke",
        "active.mod.launch_mortar_smoke_autoloader",
        "active.mod.twinfire.launch_mortar_smoke_autoloader"
    };
    private static bool _fastSmokeAiSummaryLogged;
    private static readonly string[] InfantrySmokeSkillIds =
    {
        "active.throw_smoke_grenade",
        "active.throw_smoke_grenade_limited"
    };
    private static readonly Dictionary<IntPtr, PinnedSmokePermissionSnapshot> OriginalPinnedSmokePermissions = new();
    private static bool _pinnedSmokeErrorLogged;
    private static bool _fastSmokeAiErrorLogged;
    private static readonly HashSet<string> MortarAiCooldownAddedByUs = new(StringComparer.OrdinalIgnoreCase);
    private static Cooldown? _nativeSmokeAiCooldown;
    private static SkillTemplate? _sharedMortarSmokeSkill;
    private static WeaponTemplate? _mediumMortarTemplate;
    private static bool _mediumMortarSmokeGrantedByUs;
    private static SpawnTileEffect? _sharedMortarSmokeSpawnEffect;
    private static int? _originalSharedMortarSmokeChancePerTile;
    private static bool _smokeDeliveryResolveErrorLogged;

    private float _nextSettingsRefresh;
    private string _lastSettingsSummary = string.Empty;
    private bool _restoreOnSafeScene;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _log = logger;
        RegisterSettings();
        RefreshSettings(force: true);

        int hooks = 0;
        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(TileEffectHandler), nameof(TileEffectHandler.AssignToTile), new[] { typeof(Tile) }),
            prefix: nameof(Patches.TileEffectHandler_AssignToTile_Prefix),
            postfix: null,
            label: "TileEffectHandler.AssignToTile");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(
                typeof(SpawnTileEffectHandler),
                nameof(SpawnTileEffectHandler.OnApply),
                new[] { typeof(Actor), typeof(Tile), typeof(Tile), typeof(Tile), typeof(Element), typeof(bool) }),
            prefix: nameof(Patches.SpawnTileEffectHandler_OnApply_Prefix),
            postfix: null,
            label: "SpawnTileEffectHandler.OnApply");

        MethodBase? getAoeTiles = AccessTools.Method(
            typeof(Skill),
            nameof(Skill.GetAoeTiles),
            new[]
            {
                typeof(Tile),
                typeof(Tile),
                typeof(Il2CppSystem.Collections.Generic.List<Tile>),
                typeof(bool)
            });
        hooks += TryPatch(
            harmony,
            getAoeTiles,
            prefix: null,
            postfix: nameof(Patches.Skill_GetAoeTiles_Postfix),
            label: "Skill.GetAoeTiles");

        MethodBase? getUnsortedAoeTiles = AccessTools.Method(
            typeof(Skill),
            nameof(Skill.GetUnsortedAoeTiles),
            new[]
            {
                typeof(Tile),
                typeof(Tile),
                typeof(Il2CppSystem.Collections.Generic.List<Tile>),
                typeof(bool)
            });
        hooks += TryPatch(
            harmony,
            getUnsortedAoeTiles,
            prefix: null,
            postfix: nameof(Patches.Skill_GetAoeTiles_Postfix),
            label: "Skill.GetUnsortedAoeTiles");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(Skill), nameof(Skill.Use), new[] { typeof(Tile), typeof(UsageParameter) }),
            prefix: nameof(Patches.Skill_Use_Prefix),
            postfix: null,
            label: "Skill.Use");

        // Native Pinned Down can remove every AP. This hook changes the cost
        // only for the two hand-thrown smoke skills while actually pinned.
        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(Skill), nameof(Skill.GetActionPointCost), Type.EmptyTypes),
            prefix: null,
            postfix: nameof(Patches.Skill_GetActionPointCost_Postfix),
            label: "Skill.GetActionPointCost");

        // MEN-001: native LOS scoring is not sufficient to make smoke an AI
        // action. Integrate a *single local decision* into native Agent.Execute.
        // This hook is later than Infantry Assault and does not override it.
        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(Agent), nameof(Agent.Execute), Type.EmptyTypes),
            prefix: nameof(Patches.Agent_Execute_Prefix), postfix: null,
            label: "Agent.Execute");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(Agent), nameof(Agent.OnTurnStart), Type.EmptyTypes),
            prefix: null, postfix: nameof(Patches.Agent_OnTurnStart_Postfix),
            label: "Agent.OnTurnStart");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(
                typeof(Skill),
                nameof(Skill.GetHitchance),
                new[]
                {
                    typeof(Tile), typeof(Tile), typeof(EntityProperties), typeof(EntityProperties),
                    typeof(bool), typeof(Entity), typeof(bool)
                }),
            prefix: null,
            postfix: nameof(Patches.Skill_GetHitchance_Postfix),
            label: "Skill.GetHitchance");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(SkillContainer), nameof(SkillContainer.AddInherentSkills), new[] { typeof(EntityTemplate) }),
            prefix: nameof(Patches.SkillContainer_AddInherentSkills_Prefix),
            postfix: null,
            label: "SkillContainer.AddInherentSkills");

        hooks += TryPatch(
            harmony,
            AccessTools.Method(typeof(SkillContainer), nameof(SkillContainer.Add), new[] { typeof(BaseSkill) }),
            prefix: nameof(Patches.SkillContainer_Add_Prefix),
            postfix: null,
            label: "SkillContainer.Add");

        ApplyThrownGrenadeActionPointCosts();
        ApplyFreeInfantrySmokeGrenades();
        ApplyFreeInfantryFragGrenades();
        ApplySmokeDeliveryProfiles();

        DevConsole.RegisterPanel(ModName, DrawDebug);
        DebugMsg($"{ModName} {Version}: initialized with {hooks}/11 hooks.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        Patches.ResetCounters();
        _smokeActedThisTurn.Clear();
        if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase) &&
            RuntimeSettings.DebugLogging && _aiSmokeExecutions > 0)
            DebugMsg($"AI smoke mission summary: execute={_aiSmokeExecutions}, pressure={_aiSmokeEligible}, skill={_aiSmokeSkillsAvailable}, target={_aiSmokeValidTargets}, used={_aiSmokeUsesSucceeded}.");
        if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
        {
            _aiSmokeExecutions = _aiSmokeEligible = _aiSmokeSkillsAvailable = _aiSmokeValidTargets = _aiSmokeUsesSucceeded = 0;
            _aiSmokeDirectionDebugLines = 0;
        }

        if (RuntimeSettings.Enabled)
            ApplyThrownGrenadeActionPointCosts();
        else
            RestoreThrownGrenadeActionPointCosts();

        if (RuntimeSettings.Enabled && RuntimeSettings.GiveInfantryFreeSmokeGrenade)
            ApplyFreeInfantrySmokeGrenades();
        else if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
            RestoreFreeInfantrySmokeGrenades();

        if (RuntimeSettings.Enabled && RuntimeSettings.GiveInfantryFreeFragGrenade)
            ApplyFreeInfantryFragGrenades();
        else if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
            RestoreFreeInfantryFragGrenades();

        if (RuntimeSettings.Enabled)
            ApplySmokeDeliveryProfiles();
        else if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
            RestoreSmokeDeliveryProfiles();

        // Duration can be restored immediately because it does not participate in MENACE's
        // LOS stack. LOS itself remains deferred until a non-tactical scene if needed.
        if (!RuntimeSettings.Enabled)
            RestoreSmokeDurations();
        else
            ReapplySmokeDurations();

        if (!RuntimeSettings.Enabled || !RuntimeSettings.AllowLineOfSightThroughSmoke)
        {
            if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
            {
                RestoreSmokeLineOfSight();
                _restoreOnSafeScene = false;
                if (!RuntimeSettings.Enabled)
                    PatchedTemplates.Clear();
            }
            else
            {
                _restoreOnSafeScene = true;
            }
        }
        else
        {
            _restoreOnSafeScene = false;
        }

        DebugMsg($"Scene loaded: {sceneName} (build {buildIndex}).");
    }

    public void OnUpdate()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _nextSettingsRefresh)
            return;

        _nextSettingsRefresh = now + 0.5f;
        // Runtime settings still refresh periodically, but smoke-delivery template mutations
        // are applied only at initialization, scene load, or when their settings actually
        // change. Re-resolving/re-touching mortar SkillTemplates every 0.5 seconds during a
        // tactical scene is unnecessary and can disturb other mortar runtime systems.
        RefreshSettings(force: false);
    }

    public void OnGUI() { }

    public void OnUnload()
    {
        RestoreThrownGrenadeActionPointCosts();
        RestoreSmokeDurations();
        RestoreSmokeDeliveryProfiles();

        // Do not restore LOS during a tactical scene: existing smoke handlers may later call
        // UnblockLineOfSight based on the template flag and corrupt the LOS stack.
        _restoreOnSafeScene = true;
    }

    private static void RegisterSettings()
    {
        ModSettings.Register(ModName, s =>
        {
            s.AddHeader($"{ModName} v{Version}");
            s.AddToggle("EnableGameplay", "Enable Smoke Overhaul", true);

            s.AddHeader("Line of sight");
            s.AddToggle("AllowLineOfSightThroughSmoke", "Smoke no longer fully blocks line of sight", true);

            s.AddHeader("Fire through smoke");
            s.AddSlider("AccuracyPenaltyPerTile", "Accuracy penalty per intervening smoke tile (%)", 0f, 50f, 20f);
            s.AddSlider("MaximumAccuracyPenalty", "Maximum smoke accuracy penalty (%)", 0f, 100f, 60f);

            s.AddHeader("Smoke duration");
            s.AddSlider("SmokeDurationMultiplier", "Smoke duration multiplier", 1f, 4f, 2f);

            s.AddHeader("Grenades");
            s.AddSlider("ThrownGrenadeApCost", "AP cost for thrown grenade skills", 0f, 60f, 20f);

            s.AddHeader("Infantry smoke grenades");
            s.AddToggle("GiveInfantryFreeSmokeGrenade", "Give every infantry unit 3 smoke grenades", true);

            s.AddHeader("Infantry fragmentation grenades");
            s.AddToggle("GiveInfantryFreeFragGrenade", "Give every infantry unit 3 Tier 2 fragmentation grenades", true);

            s.AddHeader("Smoke delivery");
            s.AddToggle("CaliberBasedSmokeFootprints", "Caliber-based smoke footprints (infantry/light 1 tile, 80mm cross, 120mm 3x3)", true);
            s.AddToggle("EnableAISmokeUsage", "Let AI actively use smoke with MENACE's native LOS-blocker behavior", true);
            s.AddToggle("FastAISmokeSelection", "Fast AI smoke selection (current position only)", true);
            s.AddToggle("AllowSmokeWhilePinnedDown", "Allow smoke throws while Pinned Down (0 AP in that state only)", true);
            s.AddToggle("SimpleAISmokeRules", "Lightweight AI smoke: defensive throws and exposed flamethrowers", true);

            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });
    }

    private void RefreshSettings(bool force)
    {
        bool previousEnabled = RuntimeSettings.Enabled;
        bool previousLos = RuntimeSettings.AllowLineOfSightThroughSmoke;
        int previousDurationMultiplier = RuntimeSettings.SmokeDurationMultiplier;
        int previousGrenadeApCost = RuntimeSettings.ThrownGrenadeApCost;
        bool previousFreeGrenade = RuntimeSettings.GiveInfantryFreeSmokeGrenade;
        bool previousFreeFragGrenade = RuntimeSettings.GiveInfantryFreeFragGrenade;
        bool previousFootprints = RuntimeSettings.CaliberBasedSmokeFootprints;
        bool previousAiSmoke = RuntimeSettings.EnableAISmokeUsage;
        bool previousFastAiSmoke = RuntimeSettings.FastAISmokeSelection;
        bool previousPinnedSmoke = RuntimeSettings.AllowSmokeWhilePinnedDown;

        bool enabled = Setting("EnableGameplay", true);
        bool losThroughSmoke = Setting("AllowLineOfSightThroughSmoke", true);
        int perTile = SliderInt("AccuracyPenaltyPerTile", 20, 0, 50);
        int maximum = SliderInt("MaximumAccuracyPenalty", 60, 0, 100);
        int durationMultiplier = SliderInt("SmokeDurationMultiplier", 2, 1, 4);
        int grenadeApCost = SliderInt("ThrownGrenadeApCost", 20, 0, 60);
        bool freeGrenade = Setting("GiveInfantryFreeSmokeGrenade", true);
        bool freeFragGrenade = Setting("GiveInfantryFreeFragGrenade", true);
        bool caliberFootprints = Setting("CaliberBasedSmokeFootprints", true);
        bool aiSmoke = Setting("EnableAISmokeUsage", true);
        bool fastAiSmoke = Setting("FastAISmokeSelection", true);
        bool pinnedSmoke = Setting("AllowSmokeWhilePinnedDown", true);
        bool simpleAiRules = Setting("SimpleAISmokeRules", true);
        bool debug = Setting("DebugLogging", false);

        RuntimeSettings.Configure(enabled, losThroughSmoke, perTile, maximum, durationMultiplier, grenadeApCost, freeGrenade, freeFragGrenade, caliberFootprints, aiSmoke, fastAiSmoke, pinnedSmoke, simpleAiRules, debug);

        if ((previousEnabled && !enabled) || (previousLos && !losThroughSmoke))
            _restoreOnSafeScene = true;
        else if (enabled && losThroughSmoke)
            _restoreOnSafeScene = false;

        if (enabled && (force || !previousEnabled || previousGrenadeApCost != grenadeApCost))
            ApplyThrownGrenadeActionPointCosts();
        else if (!enabled && previousEnabled)
            RestoreThrownGrenadeActionPointCosts();

        if (enabled && (force || !previousEnabled || previousDurationMultiplier != durationMultiplier))
            ReapplySmokeDurations();
        else if (!enabled && previousEnabled)
            RestoreSmokeDurations();

        if (enabled && freeGrenade && (!previousEnabled || !previousFreeGrenade))
            ApplyFreeInfantrySmokeGrenades();
        else if ((!enabled || !freeGrenade) && (previousEnabled && previousFreeGrenade))
            _restoreOnSafeScene = true;

        if (enabled && freeFragGrenade && (!previousEnabled || !previousFreeFragGrenade))
            ApplyFreeInfantryFragGrenades();
        else if ((!enabled || !freeFragGrenade) && (previousEnabled && previousFreeFragGrenade))
            RestoreFreeInfantryFragGrenades();

        if (enabled && (force || !previousEnabled || previousFootprints != caliberFootprints || previousAiSmoke != aiSmoke || previousFastAiSmoke != fastAiSmoke || previousPinnedSmoke != pinnedSmoke))
            ApplySmokeDeliveryProfiles();
        else if (!enabled && previousEnabled)
            RestoreSmokeDeliveryProfiles();

        string summary = RuntimeSettings.Summary;
        bool changed = !string.Equals(summary, _lastSettingsSummary, StringComparison.Ordinal);
        if (force || (debug && changed))
            DebugMsg("Settings: " + summary);
        _lastSettingsSummary = summary;
    }

    private static int TryPatch(global::HarmonyLib.Harmony harmony, MethodBase? original, string? prefix, string? postfix, string label)
    {
        if (original == null)
        {
            Error($"Could not resolve {label}.");
            return 0;
        }

        try
        {
            HarmonyMethod? pre = prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix);
            HarmonyMethod? post = postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix);
            // Let any earlier AI override (e.g. Infantry Assault) take precedence.
            if (prefix == nameof(Patches.Agent_Execute_Prefix) && pre != null)
                pre.priority = global::HarmonyLib.Priority.Last;
            harmony.Patch(original, prefix: pre, postfix: post);
            DebugMsg($"Patched {label}.");
            return 1;
        }
        catch (Exception ex)
        {
            Error($"Failed to patch {label}: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    private T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(ModName, key); }
        catch { return fallback; }
    }

    private int SliderInt(string key, int fallback, int min, int max)
    {
        float raw = Setting(key, (float)fallback);
        return global::System.Math.Clamp((int)global::System.Math.Round(raw), min, max);
    }

    private void DrawDebug(Rect area)
    {
        float y = area.y;
        const float h = 22f;
        void Line(string text)
        {
            GUI.Label(new Rect(area.x, y, area.width, h), text);
            y += h;
        }

        Line($"{ModName} {Version}");
        Line("Enabled: " + (RuntimeSettings.Enabled ? "yes" : "no"));
        Line("LOS through smoke: " + (RuntimeSettings.AllowLineOfSightThroughSmoke ? "yes" : "no"));
        Line($"Intervening smoke penalty: -{RuntimeSettings.AccuracyPenaltyPerTile}%/tile, cap -{RuntimeSettings.MaximumAccuracyPenalty}%");
        Line($"Smoke duration: x{RuntimeSettings.SmokeDurationMultiplier}");
        Line($"Thrown grenade AP cost: {RuntimeSettings.ThrownGrenadeApCost}");
        Line("Thrown grenade templates patched: " + PatchedGrenadeSkills.Count);
        Line("Free infantry smoke grenade: " + (RuntimeSettings.GiveInfantryFreeSmokeGrenade ? "yes" : "no"));
        Line("Infantry templates granted smoke: " + FreeGrenadeTemplates.Count);
        Line("Free infantry Tier 2 frag grenades: " + (RuntimeSettings.GiveInfantryFreeFragGrenade ? "yes (3 uses)" : "no"));
        Line("Infantry templates granted frag: " + FreeFragGrenadeTemplates.Count);
        Line("Caliber smoke footprints: " + (RuntimeSettings.CaliberBasedSmokeFootprints ? "yes" : "no"));
        Line("AI smoke usage: " + (RuntimeSettings.EnableAISmokeUsage ? "yes" : "no"));
        Line("AI smoke direct rules: " + (RuntimeSettings.SimpleAISmokeRules ? "yes" : "no"));
        Line($"AI actions / eligible / smoke skill / valid target / smoke thrown: {_aiSmokeExecutions} / {_aiSmokeEligible} / {_aiSmokeSkillsAvailable} / {_aiSmokeValidTargets} / {_aiSmokeUsesSucceeded}");
        Line("Fast AI smoke selection: " + (RuntimeSettings.FastAISmokeSelection ? "yes" : "no"));
        Line("Optimized native smoke AI profiles: " + OriginalSmokeAiPositioning.Count);
        Line("80mm mortar smoke granted: " + (_mediumMortarSmokeGrantedByUs ? "yes" : "no/already present"));
        Line("Smoke templates patched: " + PatchedTemplates.Count);
        Line("Hit-chance queries adjusted: " + Patches.AdjustedHitChanceQueries);
        Line("Last smoke tiles crossed: " + Patches.LastSmokeTilesCrossed);
        Line("Last applied penalty: -" + Patches.LastAppliedPenalty + "%");
        if (_restoreOnSafeScene)
            Line("Template restore pending next non-tactical scene.");
    }

    private static void ApplyThrownGrenadeActionPointCosts()
    {
        if (!RuntimeSettings.Enabled)
            return;

        try
        {
            SkillTemplate[] templates = Resources.FindObjectsOfTypeAll<SkillTemplate>();
            int changed = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                SkillTemplate template = templates[i];
                if (template == null || !IsThrownGrenadeSkill(template))
                    continue;

                string id = GetSkillTemplateId(template);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!PatchedGrenadeSkills.TryGetValue(id, out PatchedGrenadeSkill? entry))
                {
                    entry = new PatchedGrenadeSkill(template, template.ActionPointCost);
                    PatchedGrenadeSkills[id] = entry;
                }

                if (template.ActionPointCost != RuntimeSettings.ThrownGrenadeApCost)
                {
                    template.ActionPointCost = RuntimeSettings.ThrownGrenadeApCost;
                    changed++;
                }
            }

            if (changed > 0)
                DebugMsg($"Set AP cost to {RuntimeSettings.ThrownGrenadeApCost} for {changed} thrown grenade skill template(s).");
        }
        catch (Exception ex)
        {
            Error($"Failed to rebalance thrown grenade AP costs: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RestoreThrownGrenadeActionPointCosts()
    {
        if (PatchedGrenadeSkills.Count == 0)
            return;

        int restored = 0;
        foreach (PatchedGrenadeSkill entry in PatchedGrenadeSkills.Values)
        {
            try
            {
                if (entry.Template != null && entry.Template.ActionPointCost != entry.OriginalActionPointCost)
                {
                    entry.Template.ActionPointCost = entry.OriginalActionPointCost;
                    restored++;
                }
            }
            catch { }
        }

        PatchedGrenadeSkills.Clear();
        if (restored > 0)
            DebugMsg($"Restored AP cost for {restored} thrown grenade skill template(s).");
    }

    private static bool IsThrownGrenadeSkill(SkillTemplate template)
    {
        string id = GetSkillTemplateId(template);
        return HandThrownGrenadeSkillIds.Contains(id);
    }

    private static void ApplyFreeInfantrySmokeGrenades()
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.GiveInfantryFreeSmokeGrenade)
            return;

        if (!TryResolveFreeSmokeGrenade())
            return;

        try
        {
            EntityTemplate[] templates = Resources.FindObjectsOfTypeAll<EntityTemplate>();
            int added = 0;
            for (int templateIndex = 0; templateIndex < templates.Length; templateIndex++)
            {
                EntityTemplate template = templates[templateIndex];
                if (EnsureFreeGrenadeOnTemplate(template))
                    added++;
            }

            if (added > 0)
                DebugMsg($"Granted three-use smoke grenade to {added} human infantry template(s).");
        }
        catch (Exception ex)
        {
            if (!_grenadeResolveErrorLogged)
            {
                _grenadeResolveErrorLogged = true;
                Error($"Failed to grant free infantry smoke grenades: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool TryResolveFreeSmokeGrenade()
    {
        if (_freeSmokeGrenade != null)
            return true;

        try
        {
            _freeSmokeGrenade = DataTemplateLoader.Get<SkillTemplate>("active.throw_smoke_grenade", false);
        }
        catch (Exception ex)
        {
            if (!_grenadeResolveErrorLogged)
            {
                _grenadeResolveErrorLogged = true;
                Error($"Could not resolve vanilla skill 'active.throw_smoke_grenade': {ex.GetType().Name}: {ex.Message}");
            }
            return false;
        }

        if (_freeSmokeGrenade != null)
            return true;

        if (!_grenadeResolveErrorLogged)
        {
            _grenadeResolveErrorLogged = true;
            Error("Could not resolve vanilla skill 'active.throw_smoke_grenade'; free infantry smoke grenades were not applied.");
        }
        return false;
    }

    internal static void EnsureFreeGrenadeBeforeInherentSkills(EntityTemplate? template)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.GiveInfantryFreeSmokeGrenade || template == null)
            return;

        if (!TryResolveFreeSmokeGrenade())
            return;

        try
        {
            EnsureFreeGrenadeOnTemplate(template);
        }
        catch (Exception ex)
        {
            if (!_grenadeResolveErrorLogged)
            {
                _grenadeResolveErrorLogged = true;
                Error($"Failed to prepare free infantry smoke grenade before inherent skills were built: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool EnsureFreeGrenadeOnTemplate(EntityTemplate? template)
    {
        if (template == null || _freeSmokeGrenade == null || template.Skills == null)
            return false;

        // ActorType.Infantry also covers aliens, constructs, drones and decoys. The vanilla
        // infantry_default skill group is the reliable marker for normal humanoid combat
        // infantry (player, allies, pirates and rogue-army troops), which is the intended scope.
        if (!template.IsInfantry() || !HasDefaultInfantrySkillGroup(template))
            return false;

        string id = GetEntityTemplateId(template);
        bool previouslyGrantedByUs = FreeGrenadeTemplates.ContainsKey(id);

        for (int i = 0; i < template.Skills.Count; i++)
        {
            SkillTemplate? existing = template.Skills[i];
            if (existing == null)
                continue;

            string existingId = GetSkillTemplateId(existing);
            if (string.Equals(existingId, "active.throw_smoke_grenade_limited", StringComparison.OrdinalIgnoreCase))
            {
                // A limited smoke skill already exists on this template. Do not add another
                // smoke ability on top of it.
                if (previouslyGrantedByUs)
                    FreeGrenadeTemplates.Remove(id);
                return false;
            }

            if (string.Equals(existingId, "active.throw_smoke_grenade", StringComparison.OrdinalIgnoreCase))
            {
                // The intended three-use smoke skill is already present. If this is our
                // earlier grant, keep tracking it so disabling the option can restore it.
                if (previouslyGrantedByUs)
                    FreeGrenadeTemplates[id] = template;
                return false;
            }
        }

        // Do not trust the tracking dictionary as proof the skill is still present. The
        // Modpack Loader can replace EntityTemplate.Skills after our early initialization;
        // that was the reason 0.2.2 reported a grant internally while no grenade appeared
        // in the tactical skill bar. Re-add whenever the live list no longer contains it.
        template.Skills.Add(_freeSmokeGrenade);
        FreeGrenadeTemplates[id] = template;
        return true;
    }

    private static void ApplyFreeInfantryFragGrenades()
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.GiveInfantryFreeFragGrenade)
            return;

        if (!TryResolveFreeFragGrenade())
            return;

        try
        {
            EntityTemplate[] templates = Resources.FindObjectsOfTypeAll<EntityTemplate>();
            int added = 0;
            for (int templateIndex = 0; templateIndex < templates.Length; templateIndex++)
            {
                EntityTemplate template = templates[templateIndex];
                if (EnsureFreeFragGrenadeOnTemplate(template))
                    added++;
            }

            if (added > 0)
                DebugMsg($"Granted three-use Tier 2 fragmentation grenade to {added} human infantry template(s).");
        }
        catch (Exception ex)
        {
            if (!_fragGrenadeResolveErrorLogged)
            {
                _fragGrenadeResolveErrorLogged = true;
                Error($"Failed to grant free infantry fragmentation grenades: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool TryResolveFreeFragGrenade()
    {
        if (_freeFragGrenade != null)
            return true;

        try
        {
            _freeFragGrenade = DataTemplateLoader.Get<SkillTemplate>("active.throw_frag_grenade_tier2", false);
        }
        catch (Exception ex)
        {
            if (!_fragGrenadeResolveErrorLogged)
            {
                _fragGrenadeResolveErrorLogged = true;
                Error($"Could not resolve vanilla skill 'active.throw_frag_grenade_tier2': {ex.GetType().Name}: {ex.Message}");
            }
            return false;
        }

        if (_freeFragGrenade != null)
            return true;

        if (!_fragGrenadeResolveErrorLogged)
        {
            _fragGrenadeResolveErrorLogged = true;
            Error("Could not resolve vanilla skill 'active.throw_frag_grenade_tier2'; free infantry fragmentation grenades were not applied.");
        }
        return false;
    }

    internal static void EnsureFreeFragGrenadeBeforeInherentSkills(EntityTemplate? template)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.GiveInfantryFreeFragGrenade || template == null)
            return;

        if (!TryResolveFreeFragGrenade())
            return;

        try
        {
            EnsureFreeFragGrenadeOnTemplate(template);
        }
        catch (Exception ex)
        {
            if (!_fragGrenadeResolveErrorLogged)
            {
                _fragGrenadeResolveErrorLogged = true;
                Error($"Failed to prepare free infantry fragmentation grenade before inherent skills were built: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool EnsureFreeFragGrenadeOnTemplate(EntityTemplate? template)
    {
        if (template == null || _freeFragGrenade == null || template.Skills == null)
            return false;

        if (!template.IsInfantry() || !HasDefaultInfantrySkillGroup(template))
            return false;

        string id = GetEntityTemplateId(template);
        bool previouslyGrantedByUs = FreeFragGrenadeTemplates.ContainsKey(id);

        for (int i = 0; i < template.Skills.Count; i++)
        {
            SkillTemplate? existing = template.Skills[i];
            if (existing == null)
                continue;

            if (string.Equals(GetSkillTemplateId(existing), "active.throw_frag_grenade_tier2", StringComparison.OrdinalIgnoreCase))
            {
                if (previouslyGrantedByUs)
                    FreeFragGrenadeTemplates[id] = template;
                return false;
            }
        }

        // Some vanilla infantry templates already carry the Tier 2 frag accessory as default
        // equipment. Its ItemTemplate grants the same skill, so do not add a second button.
        if (template.Items != null)
        {
            for (int itemIndex = 0; itemIndex < template.Items.Count; itemIndex++)
            {
                var item = template.Items[itemIndex];
                if (item?.SkillsGranted == null)
                    continue;

                for (int skillIndex = 0; skillIndex < item.SkillsGranted.Count; skillIndex++)
                {
                    SkillTemplate? granted = item.SkillsGranted[skillIndex];
                    if (granted != null && string.Equals(GetSkillTemplateId(granted), "active.throw_frag_grenade_tier2", StringComparison.OrdinalIgnoreCase))
                    {
                        if (previouslyGrantedByUs)
                            FreeFragGrenadeTemplates.Remove(id);
                        return false;
                    }
                }
            }
        }

        template.Skills.Add(_freeFragGrenade);
        FreeFragGrenadeTemplates[id] = template;
        return true;
    }

    internal static bool AllowGrenadeSkillAdd(SkillContainer? container, BaseSkill? incoming)
    {
        if (container == null || incoming == null)
            return true;

        string incomingId;
        try { incomingId = incoming.GetID() ?? string.Empty; }
        catch { return true; }

        bool incomingSmoke = string.Equals(incomingId, "active.throw_smoke_grenade", StringComparison.OrdinalIgnoreCase);
        bool incomingSmokeLimited = string.Equals(incomingId, "active.throw_smoke_grenade_limited", StringComparison.OrdinalIgnoreCase);
        bool incomingFrag = string.Equals(incomingId, "active.throw_frag_grenade_tier2", StringComparison.OrdinalIgnoreCase);
        if (!incomingSmoke && !incomingSmokeLimited && !incomingFrag)
            return true;

        // MENACE can add the same grenade through separate inherent/item passes.
        // Check both the live list and the pending queue so only one button survives.
        if (incomingFrag)
            return FindContainerSkill(container, "active.throw_frag_grenade_tier2") == null;

        BaseSkill? normalSmoke = FindContainerSkill(container, "active.throw_smoke_grenade");
        BaseSkill? limitedSmoke = FindContainerSkill(container, "active.throw_smoke_grenade_limited");

        if (incomingSmokeLimited)
            return normalSmoke == null && limitedSmoke == null;

        if (normalSmoke != null)
            return false;

        // Prefer the requested vanilla three-use smoke skill over the limited variant.
        if (limitedSmoke != null)
        {
            try { container.Remove(limitedSmoke); }
            catch { }
        }

        return true;
    }

    private static BaseSkill? FindContainerSkill(SkillContainer container, string skillId)
    {
        try
        {
            var live = container.GetAllSkills();
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    BaseSkill? skill = live[i];
                    if (skill == null) continue;
                    string id;
                    try { id = skill.GetID() ?? string.Empty; }
                    catch { continue; }
                    if (string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase))
                        return skill;
                }
            }
        }
        catch { }

        try
        {
            var queued = container.GetSkillsInAddQueue();
            if (queued != null)
            {
                for (int i = 0; i < queued.Count; i++)
                {
                    BaseSkill? skill = queued[i];
                    if (skill == null) continue;
                    string id;
                    try { id = skill.GetID() ?? string.Empty; }
                    catch { continue; }
                    if (string.Equals(id, skillId, StringComparison.OrdinalIgnoreCase))
                        return skill;
                }
            }
        }
        catch { }

        return null;
    }

    private static bool HasDefaultInfantrySkillGroup(EntityTemplate template)
    {
        if (template.SkillGroups == null)
            return false;

        for (int i = 0; i < template.SkillGroups.Count; i++)
        {
            SkillGroup? group = template.SkillGroups[i];
            if (group != null && string.Equals(group.name, "infantry_default", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void RestoreFreeInfantrySmokeGrenades()
    {
        if (_freeSmokeGrenade == null || FreeGrenadeTemplates.Count == 0)
            return;

        int removed = 0;
        foreach (EntityTemplate template in FreeGrenadeTemplates.Values)
        {
            try
            {
                if (template?.Skills != null && template.Skills.Remove(_freeSmokeGrenade))
                    removed++;
            }
            catch { }
        }

        FreeGrenadeTemplates.Clear();
        if (removed > 0)
            DebugMsg($"Removed free smoke grenade grant from {removed} infantry template(s).");
    }

    private static void RestoreFreeInfantryFragGrenades()
    {
        if (_freeFragGrenade == null || FreeFragGrenadeTemplates.Count == 0)
            return;

        int removed = 0;
        foreach (EntityTemplate template in FreeFragGrenadeTemplates.Values)
        {
            try
            {
                if (template?.Skills != null && template.Skills.Remove(_freeFragGrenade))
                    removed++;
            }
            catch { }
        }

        FreeFragGrenadeTemplates.Clear();
        if (removed > 0)
            DebugMsg($"Removed free fragmentation grenade grant from {removed} infantry template(s).");
    }

    private static string GetEntityTemplateId(EntityTemplate template)
    {
        try
        {
            string? id = template.GetID();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }
        catch { }

        return template.name ?? "unknown_infantry";
    }

    private static string GetSkillTemplateId(SkillTemplate template)
    {
        try
        {
            string? id = template.GetID();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }
        catch { }

        return template.name ?? string.Empty;
    }

    private static void ApplySmokeDeliveryProfiles()
    {
        if (!RuntimeSettings.Enabled)
            return;

        try
        {
            _sharedMortarSmokeSkill ??= DataTemplateLoader.Get<SkillTemplate>("active.launch_mortar_smoke", false);
            _mediumMortarTemplate ??= DataTemplateLoader.Get<WeaponTemplate>(MediumMortarItemId, false);

            if (RuntimeSettings.CaliberBasedSmokeFootprints && _sharedMortarSmokeSkill != null && _mediumMortarTemplate?.SkillsGranted != null)
            {
                bool found = false;
                for (int i = 0; i < _mediumMortarTemplate.SkillsGranted.Count; i++)
                {
                    SkillTemplate? existing = _mediumMortarTemplate.SkillsGranted[i];
                    if (existing != null && string.Equals(GetSkillTemplateId(existing), "active.launch_mortar_smoke", StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _mediumMortarTemplate.SkillsGranted.Add(_sharedMortarSmokeSkill);
                    _mediumMortarSmokeGrantedByUs = true;
                    DebugMsg("Granted smoke-shell skill to the 80mm medium mortar.");
                }
            }
            else if (!RuntimeSettings.CaliberBasedSmokeFootprints)
            {
                RemoveMediumMortarSmokeGrant();
            }

            ConfigureSharedMortarSmokeSpawnEffect();

            if (RuntimeSettings.AllowSmokeWhilePinnedDown)
                ApplyPinnedSmokePermission();
            else
                RestorePinnedSmokePermission();

            if (RuntimeSettings.EnableAISmokeUsage)
                ApplyNativeSmokeAiToMortars();
            else
                RestoreMortarSmokeAi();

            // MENACE's native Assist.OnCollect first checks AIConfig.InfluencesPositioning.
            // Turning it off skips the extremely expensive future-position smoke search,
            // while Assist.OnEvaluate still considers using smoke from the CURRENT tile.
            // The skill, range, AoE, cooldown, cover scoring and player use are untouched.
            if (RuntimeSettings.FastAISmokeSelection)
                ApplyFastAiSmokeSelection();
            else
                RestoreFastAiSmokeSelection();
        }
        catch (Exception ex)
        {
            if (!_smokeDeliveryResolveErrorLogged)
            {
                _smokeDeliveryResolveErrorLogged = true;
                Error($"Failed to configure smoke delivery profiles: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ConfigureSharedMortarSmokeSpawnEffect()
    {
        if (_sharedMortarSmokeSkill?.EventHandlers == null)
            return;

        if (_sharedMortarSmokeSpawnEffect == null)
        {
            for (int i = 0; i < _sharedMortarSmokeSkill.EventHandlers.Count; i++)
            {
                SpawnTileEffect? spawn = _sharedMortarSmokeSkill.EventHandlers[i] as SpawnTileEffect;
                if (spawn == null)
                    continue;
                _sharedMortarSmokeSpawnEffect = spawn;
                _originalSharedMortarSmokeChancePerTile ??= spawn.ChancePerTileFromCenter;
                break;
            }
        }

        if (_sharedMortarSmokeSpawnEffect == null)
            return;

        // Light mortar keeps only the center tile, while the 80mm mortar keeps a five-tile
        // cross. Make every retained tile deterministic; the footprint filter decides which
        // tiles survive. The heavy 120mm smoke skills are separate templates and remain vanilla.
        int desired = RuntimeSettings.CaliberBasedSmokeFootprints
            ? 0
            : (_originalSharedMortarSmokeChancePerTile ?? _sharedMortarSmokeSpawnEffect.ChancePerTileFromCenter);
        if (_sharedMortarSmokeSpawnEffect.ChancePerTileFromCenter != desired)
            _sharedMortarSmokeSpawnEffect.ChancePerTileFromCenter = desired;
    }

    private sealed class SmokeAiPositioningSnapshot
    {
        public SmokeAiPositioningSnapshot(Il2CppMenace.Tactical.AI.Data.SkillBehavior behavior, bool originallyInfluencedPositioning)
        {
            Behavior = behavior;
            OriginallyInfluencedPositioning = originallyInfluencedPositioning;
        }

        public Il2CppMenace.Tactical.AI.Data.SkillBehavior Behavior { get; }
        public bool OriginallyInfluencedPositioning { get; }
    }

    private static void ApplyFastAiSmokeSelection()
    {
        // Template modification happens only on init/scene/settings change, not in
        // Agent.Evaluate, CreateLOSBlocker.Collect, GetPath, or any per-frame callback.
        int changed = 0;
        try
        {
            foreach (string id in FastSmokeAiSkillIds)
            {
                SkillTemplate? template = null;
                try { template = DataTemplateLoader.Get<SkillTemplate>(id, false); }
                catch { }
                Il2CppMenace.Tactical.AI.Data.SkillBehavior? profile = template?.AIConfig;
                if (profile == null)
                    continue;

                IntPtr key = profile.Pointer;
                if (key == IntPtr.Zero || OriginalSmokeAiPositioning.ContainsKey(key))
                    continue;

                // An already non-positioning profile must be left alone. Other mods,
                // including Larger Maps, may have disabled it intentionally.
                if (!profile.InfluencesPositioning)
                    continue;

                OriginalSmokeAiPositioning.Add(key, new SmokeAiPositioningSnapshot(profile, true));
                profile.InfluencesPositioning = false;
                changed++;
            }

            if (changed > 0 && !_fastSmokeAiSummaryLogged)
            {
                _log?.Msg($"[SmokeOverhaul] Fast AI smoke selection active: {OriginalSmokeAiPositioning.Count} native profile(s); speculative smoke positioning disabled, normal smoke use retained.");
                _fastSmokeAiSummaryLogged = true;
            }
        }
        catch (Exception ex)
        {
            if (!_fastSmokeAiErrorLogged)
            {
                _fastSmokeAiErrorLogged = true;
                Error($"Failed to optimize AI smoke selection: {ex.GetType().Name}: {ex.Message}");
            }
            // Restore any profiles that were modified before the error, avoiding a
            // partially active optimization whose effects cannot be diagnosed.
            RestoreFastAiSmokeSelection();
        }
    }

    private static void RestoreFastAiSmokeSelection()
    {
        if (OriginalSmokeAiPositioning.Count == 0)
            return;

        foreach (SmokeAiPositioningSnapshot entry in OriginalSmokeAiPositioning.Values)
        {
            try
            {
                if (entry.Behavior != null && entry.Behavior.Pointer != IntPtr.Zero)
                    entry.Behavior.InfluencesPositioning = entry.OriginallyInfluencedPositioning;
            }
            catch { }
        }
        OriginalSmokeAiPositioning.Clear();
        DebugMsg("Restored native AI smoke positioning flags.");
    }

    private sealed class PinnedSmokePermissionSnapshot
    {
        public PinnedSmokePermissionSnapshot(SkillTemplate template, bool original)
        {
            Template = template;
            Original = original;
        }

        public SkillTemplate Template { get; }
        public bool Original { get; }
    }

    private static bool IsHandThrownSmokeSkillId(string? id)
        => string.Equals(id, "active.throw_smoke_grenade", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(id, "active.throw_smoke_grenade_limited", StringComparison.OrdinalIgnoreCase);

    private static void ApplyPinnedSmokePermission()
    {
        try
        {
            foreach (string id in InfantrySmokeSkillIds)
            {
                SkillTemplate? template = DataTemplateLoader.Get<SkillTemplate>(id, false);
                if (template == null || template.Pointer == IntPtr.Zero || template.IsUsableWhilePinnedDown)
                    continue;

                if (!OriginalPinnedSmokePermissions.ContainsKey(template.Pointer))
                    OriginalPinnedSmokePermissions.Add(template.Pointer, new PinnedSmokePermissionSnapshot(template, false));

                template.IsUsableWhilePinnedDown = true;
            }
        }
        catch (Exception ex)
        {
            if (_pinnedSmokeErrorLogged)
                return;
            _pinnedSmokeErrorLogged = true;
            Error($"Failed to allow pinned smoke throws: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RestorePinnedSmokePermission()
    {
        foreach (PinnedSmokePermissionSnapshot snapshot in OriginalPinnedSmokePermissions.Values)
        {
            try
            {
                if (snapshot.Template != null && snapshot.Template.Pointer != IntPtr.Zero)
                    snapshot.Template.IsUsableWhilePinnedDown = snapshot.Original;
            }
            catch { }
        }
        OriginalPinnedSmokePermissions.Clear();
    }

    internal static void ApplyPinnedSmokeApCost(Skill? skill, ref int cost)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.AllowSmokeWhilePinnedDown || skill == null || cost <= 0)
            return;

        try
        {
            Actor? consumer = skill.GetAPConsumer();
            if (consumer == null || consumer.GetSuppressionState() != SuppressionState.PinnedDown)
                return;

            if (IsHandThrownSmokeSkillId(skill.GetID()))
                cost = 0;
        }
        catch (Exception ex)
        {
            if (_pinnedSmokeErrorLogged)
                return;
            _pinnedSmokeErrorLogged = true;
            Error($"Failed to determine pinned smoke AP cost: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // One emergency smoke decision per actor per turn, with a constant upper bound
    // on candidate tiles. Agent.Execute is an AI action transition, not a frame hook.
    private static readonly HashSet<IntPtr> _smokeActedThisTurn = new();
    // Max known-opponent checks per eligible unit per turn. Avoid map-wide searches.
    private const int MaxSmokeThreatChecks = 24;
    private static int _aiSmokeExecutions;
    private static int _aiSmokeEligible;
    private static int _aiSmokeSkillsAvailable;
    private static int _aiSmokeValidTargets;
    private static int _aiSmokeUsesSucceeded;
    private static int _aiSmokeDirectionDebugLines;

    internal static void ResetAiSmokeTurn(Agent? agent)
    {
        try
        {
            Actor? actor = agent?.GetActor();
            if (actor != null && actor.Pointer != IntPtr.Zero)
                _smokeActedThisTurn.Remove(actor.Pointer);
        }
        catch { }
    }

    internal static bool TryAiSmokeAction(Agent? agent)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.EnableAISmokeUsage ||
            !RuntimeSettings.SimpleAISmokeRules || agent == null)
            return false;

        _aiSmokeExecutions++;
        try
        {
            if (agent.IsSleeping() || agent.IsDeploymentPhase() || TacticalManager.IsSkillBusy())
                return false;

            Actor? actor = agent.GetActor();
            if (actor == null || !actor.IsInfantry() || actor.IsDying() ||
                actor.IsMoving() || actor.IsTurnDone() || actor.Pointer == IntPtr.Zero)
                return false;

            IntPtr actorKey = actor.Pointer;
            if (_smokeActedThisTurn.Contains(actorKey))
                return false;

            Tile? origin = actor.GetTile();
            if (origin == null || TileHasSmoke(origin))
                return false;

            SuppressionState pressure = actor.GetSuppressionState();
            bool pinned = pressure == SuppressionState.PinnedDown;
            bool suppressed = pressure == SuppressionState.Suppressed;
            if (pinned && !RuntimeSettings.AllowSmokeWhilePinnedDown)
                return false;

            bool flamethrowerAdvance = !pinned && !suppressed &&
                origin.GetCoverMask() == 0 &&
                (agent.GetNumThreatsFaced() > 0 || agent.GetFaction()?.HasKnownOpponent() == true);
            if (!pinned && !suppressed && !flamethrowerAdvance)
                return false;

            _smokeActedThisTurn.Add(actorKey);
            _aiSmokeEligible++;

            var rawSkills = actor.GetSkills()?.GetAllSkills();
            if (rawSkills == null)
                return false;
            Skill? smoke = null;
            bool carriesFlamethrower = false;
            for (int i = 0; i < rawSkills.Count; i++)
            {
                BaseSkill? raw = rawSkills[i];
                if (raw == null)
                    continue;
                string id = raw.GetID() ?? string.Empty;
                if (id.IndexOf("flamethrower", StringComparison.OrdinalIgnoreCase) >= 0)
                    carriesFlamethrower = true;
                if (smoke != null || !IsHandThrownSmokeSkillId(id))
                    continue;
                try
                {
                    Skill? cast = raw.TryCast<Skill>();
                    if (cast != null && !cast.IsOutOfUses() &&
                        cast.IsUsable() && cast.IsAffordable())
                        smoke = cast;
                }
                catch { }
            }
            if (smoke == null || (flamethrowerAdvance && !carriesFlamethrower))
                return false;

            _aiSmokeSkillsAvailable++;

            // Don't guess where the enemy is from actor facing. The unit may be
            // turned away from incoming fire, or still facing its last movement.
            // Only use opponents already detected by MENACE's own faction AI.
            if (!TryGetDetectedSmokeThreat(agent, actor, origin, pinned || suppressed,
                out int threatX, out int threatZ))
                return false;

            Map? map = TacticalManager.Get()?.GetMap();
            if (map == null)
                return false;

            // Aim at the known threat, NEVER at unit facing. Three narrow
            // forward candidates on each of two distances: six tiles maximum.
            // Always move closer to the threat; reject side/rear or beyond-enemy smoke.
            int stepX = global::System.Math.Sign(threatX);
            int stepZ = global::System.Math.Sign(threatZ);
            int threatDistanceSquared = threatX * threatX + threatZ * threatZ;
            for (int reachIndex = 0; reachIndex < 2; reachIndex++)
            {
                int reach = flamethrowerAdvance ? 2 - reachIndex : 1 + reachIndex;
                for (int candidate = 0; candidate < 3; candidate++)
                {
                    int dirX, dirZ;
                    if (candidate == 0)
                    {
                        dirX = stepX;
                        dirZ = stepZ;
                    }
                    else if (stepX != 0 && stepZ != 0)
                    {
                        dirX = candidate == 1 ? stepX : 0;
                        dirZ = candidate == 1 ? 0 : stepZ;
                    }
                    else if (stepX != 0)
                    {
                        dirX = stepX;
                        dirZ = candidate == 1 ? 1 : -1;
                    }
                    else
                    {
                        dirX = candidate == 1 ? 1 : -1;
                        dirZ = stepZ;
                    }

                    int remainingX = threatX - dirX * reach;
                    int remainingZ = threatZ - dirZ * reach;
                    if (remainingX * remainingX + remainingZ * remainingZ >= threatDistanceSquared)
                        continue;

                    int x = origin.GetX() + dirX * reach;
                    int z = origin.GetZ() + dirZ * reach;
                    if (!map.IsInBounds(x, z))
                        continue;

                    Tile? target = map.GetTile(x, z);
                    if (target == null || !target.IsEmpty() || TileHasSmoke(target) ||
                        (flamethrowerAdvance && !target.IsValidMovementDestination()))
                        continue;

                    if (!smoke.IsUsableOn(target, origin))
                        continue;

                    _aiSmokeValidTargets++;
                    if (!smoke.Use(target, UsageParameter.AutoAudioVolume))
                        return false;

                    _aiSmokeUsesSucceeded++;
                    // One line per actual forced smoke grenade, DEBUG ON only.
                    // Coordinates prove whether a reported bad throw was caused
                    // by this mod or MENACE's normal smoke AI. Cap per mission.
                    if (RuntimeSettings.DebugLogging && _aiSmokeDirectionDebugLines < 24)
                    {
                        _aiSmokeDirectionDebugLines++;
                        DebugMsg($"AI smoke thrown: origin=({origin.GetX()},{origin.GetZ()}) " +
                            $"threat=({origin.GetX() + threatX},{origin.GetZ() + threatZ}) " +
                            $"target=({x},{z}) mode={(flamethrowerAdvance ? "advance" : "defensive")}");
                    }
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            // Never flood the log from a repeated agent evaluation. Fail closed.
            if (!_aiSmokeActionErrorLogged)
            {
                _aiSmokeActionErrorLogged = true;
                Error($"AI emergency smoke decision failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return false;
    }

    private static bool TryGetDetectedSmokeThreat(
        Agent agent, Actor actor, Tile origin, bool defensive,
        out int dx, out int dz)
    {
        dx = dz = 0;
        AIFaction? faction = agent.GetFaction();
        var opponents = faction?.GetOpponents();
        if (faction == null || opponents == null)
            return false;

        // A recent attacker is tactically useful but must STILL be known and
        // presently detected. Do not query the true position of a hidden enemy.
        Actor? recentAttacker = defensive ? actor.GetLastAttacker()?.ToActor() : null;
        IntPtr attackerPointer = recentAttacker?.Pointer ?? IntPtr.Zero;
        int nearDist2 = int.MaxValue, attackerDist2 = int.MaxValue;
        int nearX = 0, nearZ = 0, attackerX = 0, attackerZ = 0;
        int count = global::System.Math.Min(opponents.Count, MaxSmokeThreatChecks);

        for (int i = 0; i < count; i++)
        {
            Opponent? opponent = opponents[i];
            if (opponent == null || !opponent.IsKnown())
                continue;
            Actor? enemy = opponent.Actor;
            if (enemy == null || enemy.Pointer == actor.Pointer ||
                enemy.IsDying() || !enemy.IsAlive() ||
                !enemy.IsDetectedByFaction(faction.GetIndex()))
                continue;

            Tile? enemyTile = enemy.GetTile();
            if (enemyTile == null)
                continue;
            int ex = enemyTile.GetX() - origin.GetX();
            int ez = enemyTile.GetZ() - origin.GetZ();
            int d2 = ex * ex + ez * ez;
            if (d2 == 0)
                continue;

            if (d2 < nearDist2)
            {
                nearDist2 = d2;
                nearX = ex;
                nearZ = ez;
            }
            if (attackerPointer != IntPtr.Zero && enemy.Pointer == attackerPointer)
            {
                attackerDist2 = d2;
                attackerX = ex;
                attackerZ = ez;
            }
        }

        if (nearDist2 == int.MaxValue)
            return false;

        // Prefer the recent shooter only if reasonably close to the nearest
        // threat; avoid looking away toward a distant previous attacker.
        if (defensive && attackerDist2 != int.MaxValue &&
            attackerDist2 <= nearDist2 * 4)
        {
            dx = attackerX;
            dz = attackerZ;
        }
        else
        {
            dx = nearX;
            dz = nearZ;
        }
        return true;
    }

    private static bool _aiSmokeActionErrorLogged;

    private static void ApplyNativeSmokeAiToMortars()
    {
        SkillTemplate? infantrySmoke = null;
        try { infantrySmoke = DataTemplateLoader.Get<SkillTemplate>("active.throw_smoke_grenade", false); }
        catch { }
        if (infantrySmoke?.AIConfig == null)
            return;

        if (_nativeSmokeAiCooldown == null && infantrySmoke.EventHandlers != null)
        {
            for (int i = 0; i < infantrySmoke.EventHandlers.Count; i++)
            {
                if (infantrySmoke.EventHandlers[i] is Cooldown cooldown && cooldown.AIOnly)
                {
                    _nativeSmokeAiCooldown = cooldown;
                    break;
                }
            }
        }

        for (int i = 0; i < MortarSmokeSkillIds.Length; i++)
        {
            string id = MortarSmokeSkillIds[i];
            SkillTemplate? mortarSmoke = null;
            try { mortarSmoke = DataTemplateLoader.Get<SkillTemplate>(id, false); }
            catch { }
            if (mortarSmoke == null)
                continue;

            if (!OriginalMortarAiConfigs.ContainsKey(id))
                OriginalMortarAiConfigs[id] = mortarSmoke.AIConfig;

            if (mortarSmoke.AIConfig == null)
            {
                mortarSmoke.AIConfig = infantrySmoke.AIConfig;
                DebugMsg($"Enabled native CreateLOSBlocker AI behavior for '{id}'.");
            }

            // The infantry smoke skill has a native two-round AI-only cooldown. Reuse the same
            // handler template for mortar smoke so the new AI behavior cannot spend every
            // available smoke round on consecutive AI activations. Player use is unaffected.
            if (_nativeSmokeAiCooldown != null && mortarSmoke.EventHandlers != null)
            {
                bool hasAiCooldown = false;
                for (int handlerIndex = 0; handlerIndex < mortarSmoke.EventHandlers.Count; handlerIndex++)
                {
                    if (mortarSmoke.EventHandlers[handlerIndex] is Cooldown existingCooldown && existingCooldown.AIOnly)
                    {
                        hasAiCooldown = true;
                        break;
                    }
                }

                if (!hasAiCooldown)
                {
                    mortarSmoke.EventHandlers.Add(_nativeSmokeAiCooldown);
                    MortarAiCooldownAddedByUs.Add(id);
                    DebugMsg($"Added native two-round AI-only smoke cooldown to '{id}'.");
                }
            }
        }
    }

    private static void RestoreMortarSmokeAi()
    {
        if (_nativeSmokeAiCooldown != null && MortarAiCooldownAddedByUs.Count > 0)
        {
            foreach (string id in MortarAiCooldownAddedByUs)
            {
                try
                {
                    SkillTemplate? template = DataTemplateLoader.Get<SkillTemplate>(id, false);
                    template?.EventHandlers?.Remove(_nativeSmokeAiCooldown);
                }
                catch { }
            }
            MortarAiCooldownAddedByUs.Clear();
        }

        if (OriginalMortarAiConfigs.Count == 0)
            return;

        foreach (var pair in OriginalMortarAiConfigs)
        {
            try
            {
                SkillTemplate? template = DataTemplateLoader.Get<SkillTemplate>(pair.Key, false);
                if (template != null)
                    template.AIConfig = pair.Value;
            }
            catch { }
        }
        OriginalMortarAiConfigs.Clear();
    }

    private static void RemoveMediumMortarSmokeGrant()
    {
        if (!_mediumMortarSmokeGrantedByUs || _mediumMortarTemplate?.SkillsGranted == null || _sharedMortarSmokeSkill == null)
            return;

        try
        {
            if (_mediumMortarTemplate.SkillsGranted.Remove(_sharedMortarSmokeSkill))
                DebugMsg("Removed Smoke Overhaul's 80mm mortar smoke-shell grant.");
        }
        catch { }
        _mediumMortarSmokeGrantedByUs = false;
    }

    private static void RestoreSmokeDeliveryProfiles()
    {
        // Restore the shared native AI profile BEFORE unhooking mortar templates.
        RestoreFastAiSmokeSelection();
        RestorePinnedSmokePermission();
        RemoveMediumMortarSmokeGrant();
        RestoreMortarSmokeAi();
        if (_sharedMortarSmokeSpawnEffect != null && _originalSharedMortarSmokeChancePerTile.HasValue)
        {
            try { _sharedMortarSmokeSpawnEffect.ChancePerTileFromCenter = _originalSharedMortarSmokeChancePerTile.Value; }
            catch { }
        }
    }

    private enum SmokeFootprint
    {
        Vanilla,
        CenterOnly,
        Cross
    }

    private static SmokeFootprint GetSmokeFootprint(Skill? skill)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.CaliberBasedSmokeFootprints || skill == null)
            return SmokeFootprint.Vanilla;

        string skillId;
        try { skillId = skill.GetID() ?? string.Empty; }
        catch { return SmokeFootprint.Vanilla; }

        if (string.Equals(skillId, "active.throw_smoke_grenade", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId, "active.throw_smoke_grenade_limited", StringComparison.OrdinalIgnoreCase))
            return SmokeFootprint.CenterOnly;

        if (string.Equals(skillId, "active.launch_mortar_smoke", StringComparison.OrdinalIgnoreCase))
        {
            string itemId = string.Empty;
            try { itemId = skill.GetItem()?.GetID() ?? string.Empty; }
            catch { }

            if (string.Equals(itemId, MediumMortarItemId, StringComparison.OrdinalIgnoreCase))
                return SmokeFootprint.Cross;

            // This vanilla smoke skill belongs to the light mortar in current MENACE data.
            // Unknown/no-item instances are kept center-only rather than accidentally restoring
            // the original 3x3 footprint.
            return SmokeFootprint.CenterOnly;
        }

        // 120mm modular mortar smoke keeps its vanilla 3x3 footprint.
        return SmokeFootprint.Vanilla;
    }

    private static bool IsTileInSmokeFootprint(Skill? skill, Tile? tile, Tile? center)
    {
        SmokeFootprint footprint = GetSmokeFootprint(skill);
        if (footprint == SmokeFootprint.Vanilla || tile == null || center == null)
            return true;

        int dx = global::System.Math.Abs(tile.GetX() - center.GetX());
        int dz = global::System.Math.Abs(tile.GetZ() - center.GetZ());
        return footprint switch
        {
            SmokeFootprint.CenterOnly => dx == 0 && dz == 0,
            SmokeFootprint.Cross => dx + dz <= 1,
            _ => true
        };
    }

    internal static bool AllowSmokeTileEffect(SpawnTileEffectHandler? handler, Tile? targetTile, Tile? centerTargetTile)
    {
        if (handler == null)
            return true;

        Skill? parent = null;
        try { parent = handler.ParentSkill; }
        catch { }
        return IsTileInSmokeFootprint(parent, targetTile, centerTargetTile);
    }

    internal static void FilterSmokeAoeTiles(Skill? skill, Tile? center, Il2CppSystem.Collections.Generic.List<Tile>? tiles)
    {
        SmokeFootprint footprint = GetSmokeFootprint(skill);
        if (footprint == SmokeFootprint.Vanilla || center == null || tiles == null)
            return;

        try
        {
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                Tile? tile = tiles[i];
                if (!IsTileInSmokeFootprint(skill, tile, center))
                    tiles.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            Patches.LogFootprintErrorOnce(ex);
        }
    }

    internal static void LogAiSmokeUse(Skill? skill, Tile? target, UsageParameter parameters)
    {
        if (!RuntimeSettings.DebugLogging || skill == null || target == null)
            return;
        if ((parameters & UsageParameter.AutoAudioVolume) == 0)
            return;

        string skillId;
        try { skillId = skill.GetID() ?? string.Empty; }
        catch { return; }
        bool smoke = string.Equals(skillId, "active.throw_smoke_grenade", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(skillId, "active.throw_smoke_grenade_limited", StringComparison.OrdinalIgnoreCase) ||
                     MortarSmokeSkillIds.Contains(skillId, StringComparer.OrdinalIgnoreCase);
        if (!smoke)
            return;

        string itemId = string.Empty;
        try { itemId = skill.GetItem()?.GetID() ?? string.Empty; }
        catch { }
        DebugMsg($"AI SMOKE USE: skill={skillId} item={(string.IsNullOrWhiteSpace(itemId) ? "-" : itemId)} target=({target.GetX()},{target.GetZ()}) footprint={GetSmokeFootprint(skill)}.");
    }

    internal static void PrepareSmokeTemplate(TileEffectHandler? handler)
    {
        if (!RuntimeSettings.Enabled || handler == null)
            return;

        try
        {
            TileEffectTemplate? template = handler.GetTemplate();
            if (template == null || !IsSmoke(template))
                return;

            string id = GetTemplateId(template);
            if (!PatchedTemplates.TryGetValue(id, out PatchedTemplate? entry))
            {
                entry = new PatchedTemplate(template, template.BlockLineOfSight, template.RemoveAfterRounds);
                PatchedTemplates[id] = entry;
            }

            if (RuntimeSettings.AllowLineOfSightThroughSmoke && template.BlockLineOfSight)
            {
                template.BlockLineOfSight = false;
                Patches.SmokeTemplatesChanged++;
                DebugMsg($"Disabled full LOS blocking for smoke tile effect '{id}'.");
            }

            ApplySmokeDuration(entry);
        }
        catch (Exception ex)
        {
            Patches.LogTemplateErrorOnce(ex);
        }
    }

    private static void ApplySmokeDuration(PatchedTemplate entry)
    {
        try
        {
            TileEffectTemplate? template = entry.Template;
            if (template == null || !template.HasTimeLimit || entry.OriginalRemoveAfterRounds <= 0)
                return;

            int desired = global::System.Math.Clamp(
                entry.OriginalRemoveAfterRounds * RuntimeSettings.SmokeDurationMultiplier,
                1,
                100);
            if (template.RemoveAfterRounds != desired)
            {
                template.RemoveAfterRounds = desired;
                DebugMsg($"Smoke duration '{GetTemplateId(template)}': {entry.OriginalRemoveAfterRounds} -> {desired} rounds.");
            }
        }
        catch { }
    }

    private static void ReapplySmokeDurations()
    {
        if (!RuntimeSettings.Enabled)
            return;

        foreach (PatchedTemplate entry in PatchedTemplates.Values)
            ApplySmokeDuration(entry);
    }

    private static void RestoreSmokeDurations()
    {
        foreach (PatchedTemplate entry in PatchedTemplates.Values)
        {
            try
            {
                if (entry.Template != null)
                    entry.Template.RemoveAfterRounds = entry.OriginalRemoveAfterRounds;
            }
            catch { }
        }
    }

    private static void RestoreSmokeLineOfSight()
    {
        foreach (PatchedTemplate entry in PatchedTemplates.Values)
        {
            try
            {
                if (entry.Template != null)
                    entry.Template.BlockLineOfSight = entry.OriginalBlockLineOfSight;
            }
            catch { }
        }
    }

    private static void RestoreSmokeTemplates()
    {
        RestoreSmokeLineOfSight();
        RestoreSmokeDurations();

        if (PatchedTemplates.Count > 0)
            DebugMsg($"Restored {PatchedTemplates.Count} smoke template(s) to their original LOS and duration settings.");
        PatchedTemplates.Clear();
    }

    internal static void ApplySmokeAccuracyPenalty(Tile? from, Tile? target, ref HitChance result)
    {
        if (!RuntimeSettings.Enabled || RuntimeSettings.AccuracyPenaltyPerTile <= 0 || RuntimeSettings.MaximumAccuracyPenalty <= 0)
            return;
        if (from == null || target == null || result.AlwaysHits)
            return;

        int smokeTiles = CountInterveningSmokeTiles(from, target);
        Patches.LastSmokeTilesCrossed = smokeTiles;
        if (smokeTiles <= 0)
        {
            Patches.LastAppliedPenalty = 0;
            return;
        }

        int penalty = global::System.Math.Min(smokeTiles * RuntimeSettings.AccuracyPenaltyPerTile, RuntimeSettings.MaximumAccuracyPenalty);
        float before = result.FinalValue;
        result.FinalValue = global::System.Math.Max(0f, before - penalty);
        Patches.AdjustedHitChanceQueries++;
        Patches.LastAppliedPenalty = penalty;
    }

    private static int CountInterveningSmokeTiles(Tile from, Tile target)
    {
        TacticalManager? manager = TacticalManager.Get();
        Map? map = manager?.GetMap();
        if (map == null)
            return 0;

        int x0 = from.GetX();
        int z0 = from.GetZ();
        int x1 = target.GetX();
        int z1 = target.GetZ();

        int dx = global::System.Math.Abs(x1 - x0);
        int dz = global::System.Math.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;
        int count = 0;

        while (true)
        {
            // Deliberately exclude attacker and defender tiles. Units actually standing in
            // smoke already receive vanilla effect.smoke modifiers; this penalty represents
            // only smoke lying between shooter and target.
            if (!(x0 == from.GetX() && z0 == from.GetZ()) && !(x0 == x1 && z0 == z1))
            {
                if (map.IsInBounds(x0, z0))
                {
                    Tile? tile = map.GetTile(x0, z0);
                    if (tile != null && TileHasSmoke(tile))
                        count++;
                }
            }

            if (x0 == x1 && z0 == z1)
                break;

            int e2 = 2 * err;
            if (e2 > -dz)
            {
                err -= dz;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                z0 += sz;
            }
        }

        return count;
    }

    private static bool TileHasSmoke(Tile tile)
    {
        try
        {
            var effects = tile.GetEffects();
            if (effects == null)
                return false;

            for (int i = 0; i < effects.Count; i++)
            {
                TileEffectHandler? handler = effects[i];
                TileEffectTemplate? template = handler?.GetTemplate();
                if (template != null && IsSmoke(template))
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static bool IsSmoke(TileEffectTemplate template)
        => (template.Type & TileEffectType.Smoke) != 0;

    private static string GetTemplateId(TileEffectTemplate template)
    {
        try
        {
            string? id = template.GetID();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }
        catch { }

        return template.name ?? template.GetType().Name;
    }

    internal static void DebugMsg(string message)
    {
        if (_log != null && RuntimeSettings.DebugLogging)
            _log.Msg("[SmokeOverhaul] " + message);
    }

    internal static void Error(string message)
    {
        _log?.Error("[SmokeOverhaul] " + message);
    }

    private sealed class PatchedTemplate
    {
        public PatchedTemplate(TileEffectTemplate template, bool originalBlockLineOfSight, int originalRemoveAfterRounds)
        {
            Template = template;
            OriginalBlockLineOfSight = originalBlockLineOfSight;
            OriginalRemoveAfterRounds = originalRemoveAfterRounds;
        }

        public TileEffectTemplate Template { get; }
        public bool OriginalBlockLineOfSight { get; }
        public int OriginalRemoveAfterRounds { get; }
    }

    private sealed class PatchedGrenadeSkill
    {
        public PatchedGrenadeSkill(SkillTemplate template, int originalActionPointCost)
        {
            Template = template;
            OriginalActionPointCost = originalActionPointCost;
        }

        public SkillTemplate Template { get; }
        public int OriginalActionPointCost { get; }
    }
}

internal static class Patches
{
    internal static int SmokeTemplatesChanged;
    internal static int AdjustedHitChanceQueries;
    internal static int LastSmokeTilesCrossed;
    internal static int LastAppliedPenalty;

    public static void TileEffectHandler_AssignToTile_Prefix(TileEffectHandler __instance)
    {
        Plugin.PrepareSmokeTemplate(__instance);
    }

    public static bool SpawnTileEffectHandler_OnApply_Prefix(SpawnTileEffectHandler __instance, Tile _targetTile, Tile _centerTargetTile)
    {
        return Plugin.AllowSmokeTileEffect(__instance, _targetTile, _centerTargetTile);
    }

    public static void Skill_GetAoeTiles_Postfix(
        Skill __instance,
        Tile __1,
        Il2CppSystem.Collections.Generic.List<Tile> __2)
    {
        Plugin.FilterSmokeAoeTiles(__instance, __1, __2);
    }

    public static bool Agent_Execute_Prefix(Agent __instance, ref bool __result, bool __runOriginal)
    {
        // Respect other AI prefixes that already suppressed the original action.
        if (!__runOriginal || !Plugin.TryAiSmokeAction(__instance))
            return true;

        __result = true;
        return false;
    }

    public static void Agent_OnTurnStart_Postfix(Agent __instance)
    {
        Plugin.ResetAiSmokeTurn(__instance);
    }

    public static void Skill_GetActionPointCost_Postfix(Skill __instance, ref int __result)
    {
        Plugin.ApplyPinnedSmokeApCost(__instance, ref __result);
    }

    public static void Skill_Use_Prefix(Skill __instance, Tile _targetTile, UsageParameter _parameters)
    {
        Plugin.LogAiSmokeUse(__instance, _targetTile, _parameters);
    }

    public static void SkillContainer_AddInherentSkills_Prefix(EntityTemplate _template)
    {
        Plugin.EnsureFreeGrenadeBeforeInherentSkills(_template);
        Plugin.EnsureFreeFragGrenadeBeforeInherentSkills(_template);
    }

    public static bool SkillContainer_Add_Prefix(SkillContainer __instance, BaseSkill _skill, ref bool __result)
    {
        if (Plugin.AllowGrenadeSkillAdd(__instance, _skill))
            return true;

        __result = false;
        return false;
    }

    public static void Skill_GetHitchance_Postfix(object[] __args, ref HitChance __result)
    {
        try
        {
            Tile? from = __args != null && __args.Length > 0 ? __args[0] as Tile : null;
            Tile? target = __args != null && __args.Length > 1 ? __args[1] as Tile : null;
            Plugin.ApplySmokeAccuracyPenalty(from, target, ref __result);
        }
        catch (Exception ex)
        {
            LogHitChanceErrorOnce(ex);
        }
    }

    private static bool _templateErrorLogged;
    private static bool _hitChanceErrorLogged;
    private static bool _footprintErrorLogged;

    internal static void LogTemplateErrorOnce(Exception ex)
    {
        if (_templateErrorLogged) return;
        _templateErrorLogged = true;
        Plugin.Error($"Smoke template patch failed: {ex.GetType().Name}: {ex.Message}");
    }

    private static void LogHitChanceErrorOnce(Exception ex)
    {
        if (_hitChanceErrorLogged) return;
        _hitChanceErrorLogged = true;
        Plugin.Error($"Hit-chance postfix failed: {ex.GetType().Name}: {ex.Message}");
    }

    internal static void LogFootprintErrorOnce(Exception ex)
    {
        if (_footprintErrorLogged) return;
        _footprintErrorLogged = true;
        Plugin.Error($"Smoke-footprint filtering failed: {ex.GetType().Name}: {ex.Message}");
    }

    internal static void ResetCounters()
    {
        AdjustedHitChanceQueries = 0;
        LastSmokeTilesCrossed = 0;
        LastAppliedPenalty = 0;
    }
}

internal static class RuntimeSettings
{
    public static bool Enabled { get; private set; } = true;
    public static bool AllowLineOfSightThroughSmoke { get; private set; } = true;
    public static int AccuracyPenaltyPerTile { get; private set; } = 20;
    public static int MaximumAccuracyPenalty { get; private set; } = 60;
    public static int SmokeDurationMultiplier { get; private set; } = 2;
    public static int ThrownGrenadeApCost { get; private set; } = 20;
    public static bool GiveInfantryFreeSmokeGrenade { get; private set; } = true;
    public static bool GiveInfantryFreeFragGrenade { get; private set; } = true;
    public static bool CaliberBasedSmokeFootprints { get; private set; } = true;
    public static bool EnableAISmokeUsage { get; private set; } = true;
    public static bool FastAISmokeSelection { get; private set; } = true;
    public static bool AllowSmokeWhilePinnedDown { get; private set; } = true;
    public static bool SimpleAISmokeRules { get; private set; } = true;
    public static bool DebugLogging { get; private set; }

    public static string Summary =>
        $"enabled={Enabled}, losThroughSmoke={AllowLineOfSightThroughSmoke}, " +
        $"accuracyPenalty=-{AccuracyPenaltyPerTile}%/tile, cap=-{MaximumAccuracyPenalty}%, " +
        $"smokeDuration=x{SmokeDurationMultiplier}, thrownGrenadeAp={ThrownGrenadeApCost}, " +
        $"freeInfantryGrenade={GiveInfantryFreeSmokeGrenade}, freeInfantryFragGrenade={GiveInfantryFreeFragGrenade}, " +
        $"caliberSmoke={CaliberBasedSmokeFootprints}, aiSmoke={EnableAISmokeUsage}, fastAiSmoke={FastAISmokeSelection}, " +
        $"pinnedSmoke={AllowSmokeWhilePinnedDown}, simpleAiRules={SimpleAISmokeRules}, debug={DebugLogging}";

    public static void Configure(bool enabled, bool allowLineOfSightThroughSmoke, int accuracyPenaltyPerTile, int maximumAccuracyPenalty, int smokeDurationMultiplier, int thrownGrenadeApCost, bool giveInfantryFreeSmokeGrenade, bool giveInfantryFreeFragGrenade, bool caliberBasedSmokeFootprints, bool enableAiSmokeUsage, bool fastAiSmokeSelection, bool allowSmokeWhilePinnedDown, bool simpleAiSmokeRules, bool debugLogging)
    {
        Enabled = enabled;
        AllowLineOfSightThroughSmoke = allowLineOfSightThroughSmoke;
        AccuracyPenaltyPerTile = global::System.Math.Clamp(accuracyPenaltyPerTile, 0, 50);
        MaximumAccuracyPenalty = global::System.Math.Clamp(maximumAccuracyPenalty, 0, 100);
        SmokeDurationMultiplier = global::System.Math.Clamp(smokeDurationMultiplier, 1, 4);
        ThrownGrenadeApCost = global::System.Math.Clamp(thrownGrenadeApCost, 0, 60);
        GiveInfantryFreeSmokeGrenade = giveInfantryFreeSmokeGrenade;
        GiveInfantryFreeFragGrenade = giveInfantryFreeFragGrenade;
        CaliberBasedSmokeFootprints = caliberBasedSmokeFootprints;
        EnableAISmokeUsage = enableAiSmokeUsage;
        FastAISmokeSelection = fastAiSmokeSelection;
        AllowSmokeWhilePinnedDown = allowSmokeWhilePinnedDown;
        SimpleAISmokeRules = simpleAiSmokeRules;
        DebugLogging = debugLogging;
    }
}

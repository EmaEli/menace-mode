using MelonLoader;
using Il2CppInterop.Runtime.InteropTypes;
using MenaceModSettings;
using Menace.SDK;
using MenaceModSuite.Shared;
using UnityEngine;

[assembly: AssemblyVersion("0.4.9.0")]
[assembly: AssemblyFileVersion("0.4.9.0")]
[assembly: AssemblyInformationalVersion("0.4.9")]

namespace MenaceWeaponMechanicsRebalanced;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/WeaponMechanicsRebalanced/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string SettingsGroup = "Weapon Mechanics Rebalanced";
    private const string Version = "0.4.9";

    private sealed class MortarSkillSpec
    {
        public int Scatter;
        public int ScatterChance;
        public float ActionPointCostMult = 0.5f;
        public float DamageMult = 0.5f;
        public float DamageToArmorDurabilityMult = 0.5f;
        public float UsesMult = 2f;
        public int ExtraSuppressionRadius = 1;
        public float ExtraSuppressionMult = 0.5f;
        public float SuppressionDropoffAOE;
        public float? AccuracyBonus;
        public float? AccuracyMult;
    }

    // Mortars stay single-shot (except skills that are natively multi-shot, such as
    // twin-fire vehicle mortars). We only make them less point-accurate and spread
    // suppression over a wider beaten zone. Do not use SkillTemplate.Repetitions to
    // create salvos: MENACE reuses one skill execution/aim cycle for those repetitions.
    private static readonly Dictionary<string, MortarSkillSpec> MortarSkillSpecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active.launch_light_mortar_frag"] = new()
        {
            Scatter = 3, ScatterChance = 85, SuppressionDropoffAOE = -2f
        },
        ["active.launch_medium_mortar_frag"] = new()
        {
            Scatter = 4, ScatterChance = 80, SuppressionDropoffAOE = -3f
        },
        ["active.mod.fire_120mm_mortar_autoloader"] = new()
        {
            Scatter = 5, ScatterChance = 85, SuppressionDropoffAOE = -12f, AccuracyBonus = 0f, AccuracyMult = 0.80f
        },
        ["active.mod.twinfire.fire_120mm_mortar_autoloader"] = new()
        {
            Scatter = 5, ScatterChance = 85, SuppressionDropoffAOE = -12f, AccuracyBonus = 0f, AccuracyMult = 0.75f
        },
    };

    // Smoke mortar fire follows the same action-economy rebalance as HE: half AP
    // per fire mission and double limited uses. Keep smoke dispersion/VFX/effects native.
    private static readonly string[] MortarSmokeSkillIds =
    {
        "active.launch_mortar_smoke",
        "active.mod.launch_mortar_smoke_autoloader",
        "active.mod.twinfire.launch_mortar_smoke_autoloader",
    };

    // Ranging fire: each physical mortar remembers every intended target tile while
    // its firing position remains established. Each prior shot tightens the dispersion
    // radius by one Scatter point, but misses can never improve below Scatter=2. An exact
    // impact finds the firing solution and unlocks Scatter=1. ScatterChance is never
    // changed by ranging. Infantry mortar memory lasts only for the current deployed
    // stance; non-deployable vehicle mortars retain solutions only while stationary.
    // The shared SkillTemplate is modified only around the current shot and restored.
    private static readonly HashSet<string> MortarRangingSkillIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "active.launch_light_mortar_frag",
        "active.launch_medium_mortar_frag",
        "active.mod.fire_120mm_mortar_autoloader",
        "active.mod.twinfire.fire_120mm_mortar_autoloader",
        "active.launch_mortar_smoke",
        "active.mod.launch_mortar_smoke_autoloader",
        "active.mod.twinfire.launch_mortar_smoke_autoloader",
    };

    private sealed class MortarRangingState
    {
        public int Shots;
        public bool SolutionFound;
        public int LastBaseScatter = -1;
        public int LastEffectiveScatter = -1;
    }

    private sealed class MortarApplyState
    {
        public long SkillKey;
        public (long Actor, long Weapon) RangingKey;
        public (int X, int Z) TileKey;
        public Il2CppMenace.Tactical.Skills.SkillTemplate? Template;
        public int OriginalScatter = -1;
        public int OriginalScatterChance = -1;
        public int EffectiveScatter = -1;
        public bool DirectHit;
        public int ImpactX;
        public int ImpactZ;
    }

    private static readonly Dictionary<string, object> OriginalMortarTemplateValues = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, object> OriginalMortarHandlerValues = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(long Actor, long Weapon), Dictionary<(int X, int Z), MortarRangingState>> MortarRangingSolutions = new();

    private static readonly Dictionary<string, object> OriginalFireModeTemplateValues = new(StringComparer.OrdinalIgnoreCase);

    // High-volume alternate fire modes trade ammunition for tempo. They cost the same
    // AP as the normal rifle shot but consume two shared weapon uses per activation.
    // The concrete current-game IDs are explicit so unrelated skills named "spray" or
    // "sustained" are never modified accidentally.
    private static readonly HashSet<string> HighVolumeFireSkillIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "active.fire_assault_rifle_sustained_fire_rmc_mrs_556",
        "active.fire_assault_rifle_sustained_fire_tier1_556",
        "active.fire_assault_rifle_sustained_fire_tier1_556_suppressed",
        "active.fire_assault_rifle_sustained_fire_tier1_762",
        "active.fire_assault_rifle_sustained_fire_tier1_762_suppressed",
        "active.fire_em_enhanced_assault_rifle_sustained_fire",
        "active.fire_battle_rifle_tier_1_spray",
        "active.fire_battle_rifle_tier_1_spray_suppressed",
    };

    // Aimed Fire is deliberately not charged extra ammunition. Current v0.7.15
    // templates already cost 40 AP; the cap only protects future/rebalanced variants
    // from exceeding the requested maximum of 50 AP.
    private static readonly HashSet<string> AimedFireSkillIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "active.fire_battle_rifle_tier_1_aimed_fire_marksman",
        "active.fire_battle_rifle_tier_2_aimed_fire",
    };

    private static ModLog? _logger;
    private static bool _enabled = true;
    private static bool _debugLogging;
    private const int MortarScatterReductionPerPriorShot = 1;
    private const int MortarUnsolvedScatterFloor = 2;
    private const int MortarSolvedScatter = 1;
    private static GUIStyle? _mortarBonusStyle;
    private static GUIStyle? _mortarBonusShadowStyle;
    private static bool _mortarOverlayErrorLogged;
    private readonly struct MortarHighlightBlend
    {
        public readonly byte Red;
        public readonly byte Green;
        public readonly int EffectiveScatter;
        public readonly bool SolutionFound;

        public MortarHighlightBlend(byte red, byte green, int effectiveScatter, bool solutionFound)
        {
            Red = red;
            Green = green;
            EffectiveScatter = effectiveScatter;
            SolutionFound = solutionFound;
        }
    }

    private sealed class MortarWorldHighlight
    {
        public GameObject Root = null!;
        public LineRenderer OuterLine = null!;
        public LineRenderer InnerLine = null!;
        public MortarHighlightBlend Blend;
    }

    private static readonly Dictionary<(int X, int Z), MortarWorldHighlight> MortarWorldHighlightTiles = new();
    private static Material? _mortarWorldLineMaterial;
    private static string _mortarWorldLineShaderName = string.Empty;
    private static bool _mortarWorldRendererLogged;
    private static bool _mortarWorldRendererErrorLogged;
    private static long _mortarWorldHighlightActorKey;
    [ThreadStatic] private static MortarApplyState? _activeMortarApplyState;

    public void Initialize(ModLog logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("General");
            s.AddToggle("EnableGameplay", "Enable Weapon Mechanics Rebalanced", true);

            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });

        ReloadSettings();
        InstallMortarSuppressionRadiusPatch(harmony);
        InstallMortarRangingPatch(harmony);
        InstallFireModeEconomyPatches(harmony);
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);
        MelonCoroutines.Start(DelayedApply("initialize"));
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        ClearMortarWorldHighlights();
        MortarRangingSolutions.Clear();
        _mortarOverlayErrorLogged = false;
        _mortarWorldRendererLogged = false;
        _mortarWorldRendererErrorLogged = false;
        _activeMortarApplyState = null;
        MelonCoroutines.Start(DelayedApply("scene loaded: " + sceneName));
    }

    public void OnUpdate()
    {
        UpdateMortarWorldHighlights();
    }

    public void OnGUI()
    {
        DrawMortarRangingOverlay();
    }

    private static IEnumerator DelayedApply(string reason)
    {
        for (int i = 0; i < 10; i++)
            yield return null;
        ApplyAll(reason);
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, SettingsGroup, StringComparison.Ordinal)) return;
        ReloadSettings();
        ApplyAll("setting changed: " + key);
    }

    private static void ReloadSettings()
    {
        _enabled = Setting("EnableGameplay", true);
        _debugLogging = Setting("DebugLogging", false);
        if (!_enabled) MortarRangingSolutions.Clear();
    }

    private static T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private static void ApplyAll(string reason)
    {
        try
        {
            ReloadSettings();
            ApplyMortarBallisticsPolicy();
            ApplyFireModeEconomyPolicy();
            DebugLog($"Applied mechanics pass ({reason}), enabled={_enabled}, mortarRanging=scatter -1 per prior shot (unsolved floor 2; exact hit solves to 1), ScatterChance unchanged.");
        }
        catch (Exception ex)
        {
            Error("Failed to apply weapon mechanics: " + ex);
        }
    }

    private static void DrawDebug(Rect area)
    {
        GUILayout.BeginArea(area);
        GUILayout.Label($"Weapon Mechanics Rebalanced {Version}");
        GUILayout.Label("Enabled: " + _enabled);
        GUILayout.Label("Mortar ranging: Scatter -1 per prior shot; misses stop at Scatter 2; exact hit finds solution -> Scatter 1; ScatterChance unchanged");
        GUILayout.Label("Remembered mortar firing positions: " + MortarRangingSolutions.Count);
        GUILayout.Label("Alternate rifle fire: Sustained/Spray 40 AP + 2 ammo; Aimed Fire max 50 AP + 1 ammo");
        GUILayout.EndArea();
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);
        try
        {
            ClearMortarWorldHighlights();
            _enabled = false;
            MortarRangingSolutions.Clear();
            _activeMortarApplyState = null;
            ApplyMortarBallisticsPolicy();
            ApplyFireModeEconomyPolicy();
        }
        catch
        {
        }
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

    private static void InstallFireModeEconomyPatches(global::HarmonyLib.Harmony harmony)
    {
        try
        {
            Type? skillType = global::HarmonyLib.AccessTools.TypeByName("Il2CppMenace.Tactical.Skills.Skill");
            if (skillType == null)
            {
                Warn("Alternate-fire economy patch unavailable: Skill type was not found.");
                return;
            }

            MethodInfo? use = global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType)
                .FirstOrDefault(m => string.Equals(m.Name, "Use", StringComparison.Ordinal)
                    && m.ReturnType == typeof(bool)
                    && m.GetParameters().Length == 2);
            if (use == null)
            {
                Warn("Alternate-fire economy patch unavailable: Skill.Use/2 was not found.");
                return;
            }

            harmony.Patch(
                use,
                prefix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(HighVolumeFireUsePrefix)),
                postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(HighVolumeFireUsePostfix)));

            int usabilityHooks = 0;
            foreach (MethodInfo method in global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType))
            {
                if (!string.Equals(method.Name, "IsUsable", StringComparison.Ordinal) || method.ReturnType != typeof(bool))
                    continue;

                harmony.Patch(
                    method,
                    postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(HighVolumeFireIsUsablePostfix)));
                usabilityHooks++;
            }

            DebugLog($"Alternate-fire economy patches installed: Skill.Use + {usabilityHooks} IsUsable overload(s).");
            InstallAmmoLedgerPatches(harmony, skillType);
        }
        catch (Exception ex)
        {
            Warn($"Alternate-fire economy patch failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // MEN-004 ledger: debug-only trace of every change to a limited-use skill's counter that
    // does not go through Skill.Use (direct SetUses/ConsumeUse callers, turn-boundary handlers).
    // The hooks are installed once; with Debug logging OFF they return immediately.
    private static readonly string[] AmmoLedgerMethods = { "SetUses", "ConsumeUse", "OnTurnStart", "OnTurnEnd", "OnMissionStarted" };

    private static void InstallAmmoLedgerPatches(global::HarmonyLib.Harmony harmony, Type skillType)
    {
        int hooks = 0;
        foreach (MethodInfo method in global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType))
        {
            if (Array.IndexOf(AmmoLedgerMethods, method.Name) < 0) continue;
            if (method.Name == "SetUses" ? method.GetParameters().Length != 1 : method.GetParameters().Length != 0) continue;

            harmony.Patch(
                method,
                prefix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(AmmoLedgerPrefix)),
                postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(AmmoLedgerPostfix)));
            hooks++;
        }
        DebugLog($"Ammo ledger hooks installed: {hooks}.");
    }

    private static void AmmoLedgerPrefix(Il2CppMenace.Tactical.Skills.Skill __instance, ref int __state)
    {
        __state = int.MinValue;
        if (!_debugLogging || __instance == null) return;
        try
        {
            if (__instance.HasLimitedUses()) __state = __instance.GetUses();
        }
        catch { }
    }

    private static void AmmoLedgerPostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        int __state,
        MethodBase __originalMethod)
    {
        if (__state == int.MinValue || !_debugLogging || __instance == null) return;
        try
        {
            int after = __instance.GetUses();
            string op = __originalMethod?.Name ?? "?";
            // Turn/mission callbacks run for every skill; only report them when ammo changed.
            if (after == __state && op != "SetUses" && op != "ConsumeUse") return;

            string actorName = "?";
            long actorKey = 0;
            try
            {
                Il2CppMenace.Tactical.Actor? actor = __instance.GetActor();
                if (actor != null) { actorName = actor.DebugName; actorKey = NativeKey(actor); }
            }
            catch { }

            string callers = "native";
            if (op == "SetUses" || op == "ConsumeUse")
            {
                var frames = new System.Diagnostics.StackTrace(2, false).GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>();
                var managed = frames
                    .Select(f => f.GetMethod())
                    .Where(m => m?.DeclaringType != null)
                    .Select(m => m!.DeclaringType!.FullName + "." + m.Name)
                    .Where(n => !n.StartsWith("Il2Cpp", StringComparison.Ordinal)
                             && !n.StartsWith("System.", StringComparison.Ordinal)
                             && !n.StartsWith("HarmonyLib.", StringComparison.Ordinal)
                             && !n.StartsWith("MonoMod.", StringComparison.Ordinal)
                             && !n.Contains("AmmoLedger", StringComparison.Ordinal)
                             && !n.StartsWith("DMD<", StringComparison.Ordinal))
                    .Take(3)
                    .ToList();
                if (managed.Count > 0) callers = string.Join(" <- ", managed);
            }

            DebugLog($"[MEN-004 LEDGER] op={op} actor='{actorName}' actorPtr=0x{actorKey:X} skill={__instance.GetID()} uses={__state}->{after} max={__instance.GetMaxUses()} callers={callers}");
        }
        catch (Exception ex)
        {
            Warn($"Ammo ledger failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // One debug-only snapshot per high-volume Skill.Use activation (never per projectile/frame).
    // Kept separate from gameplay values so enabling diagnostics cannot change ammo costs.
    private struct HighVolumeAmmoState
    {
        public int BeforeUses;
        public int BeforeMaxUses;
        public int BeforeUsesConsumed;
        public int BeforeCharges;
        public int BeforeChargesPerUse;
        public int UsageFlags;
        public bool BlockedByGuard;
        public string? ActorName;
        public string? ActorTemplateId;
        public string? ItemTemplateId;
        public long ActorKey;
        public long ItemKey;
    }

    // Simulated or free usages (AI evaluation, previews, scripted fire) never spend
    // ammunition natively, so the extra alternate-fire cost must not apply either.
    private const Il2CppMenace.Tactical.Skills.UsageParameter NoAmmoUsageMask =
        Il2CppMenace.Tactical.Skills.UsageParameter.Fake |
        Il2CppMenace.Tactical.Skills.UsageParameter.NoUsesConsumed;

    private static void CaptureHighVolumeAmmoBefore(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Skills.UsageParameter usage,
        ref HighVolumeAmmoState state)
    {
        // Diagnostics are strictly optional: errors reading metadata must not affect fire.
        state.UsageFlags = (int)usage;
        state.BeforeMaxUses = -1;
        state.BeforeUsesConsumed = -1;
        state.BeforeCharges = -1;
        state.BeforeChargesPerUse = -1;
        try
        {
            state.BeforeMaxUses = skill.GetMaxUses();
            state.BeforeUsesConsumed = skill.GetUsesConsumed();
            state.BeforeCharges = skill.GetCharges();
            state.BeforeChargesPerUse = skill.GetChargesPerUse();
        }
        catch { }

        try
        {
            Il2CppMenace.Tactical.Actor? actor = skill.GetActor();
            if (actor != null)
            {
                state.ActorKey = NativeKey(actor);
                state.ActorName = actor.DebugName;
                state.ActorTemplateId = actor.GetTemplate()?.GetID();
            }
        }
        catch { }

        try
        {
            Il2CppMenace.Items.Item? item = skill.GetItem();
            if (item != null)
            {
                state.ItemKey = NativeKey(item);
                state.ItemTemplateId = item.GetTemplate()?.GetID();
            }
        }
        catch { }
    }

    private static bool HighVolumeFireUsePrefix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        Il2CppMenace.Tactical.Skills.UsageParameter __1,
        ref bool __result,
        ref HighVolumeAmmoState __state)
    {
        __state = new HighVolumeAmmoState { BeforeUses = -1 };
        if (!_enabled || __instance == null) return true;

        try
        {
            string skillId = __instance.GetID();
            if (!HighVolumeFireSkillIds.Contains(skillId)) return true;
            if (!__instance.HasLimitedUses()) return true;
            if ((__1 & NoAmmoUsageMask) != 0) return true;

            int before = __instance.GetUses();
            __state.BeforeUses = before;
            if (_debugLogging)
                CaptureHighVolumeAmmoBefore(__instance, __1, ref __state);

            if (before >= 2) return true;

            // The UI/AI usability hook normally prevents this path. Keep this hard
            // guard as well because other mods can call Skill.Use with usability checks
            // explicitly bypassed.
            __state.BlockedByGuard = true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            if (_debugLogging)
                Warn($"Alternate-fire pre-use check failed: {ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    private static void HighVolumeFireUsePostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        bool __result,
        HighVolumeAmmoState __state)
    {
        if (!_enabled || __instance == null || __state.BeforeUses < 0) return;
        if (!__result && !_debugLogging) return;

        try
        {
            string skillId = __instance.GetID();
            if (!HighVolumeFireSkillIds.Contains(skillId) || !__instance.HasLimitedUses()) return;

            int afterNative = __instance.GetUses();
            int nativeDelta = __state.BeforeUses - afterNative;
            int extraToConsume = __result ? Math.Max(0, 2 - Math.Max(0, nativeDelta)) : 0;
            int afterNativeCharges = -1;
            int afterNativeUsesConsumed = -1;
            if (_debugLogging)
            {
                try
                {
                    afterNativeCharges = __instance.GetCharges();
                    afterNativeUsesConsumed = __instance.GetUsesConsumed();
                }
                catch { }
            }

            int extraCalls = 0;
            if (__result)
            {
                for (int i = 0; i < extraToConsume && __instance.GetUses() > 0; i++)
                {
                    __instance.ConsumeUse();
                    extraCalls++;
                }

                if (extraToConsume > 0)
                    __instance.SynchronizeUses();
            }

            if (_debugLogging)
            {
                int afterMod = __instance.GetUses();
                int afterModMax = -1;
                int afterModCharges = -1;
                int afterModUsesConsumed = -1;
                try
                {
                    afterModMax = __instance.GetMaxUses();
                    afterModCharges = __instance.GetCharges();
                    afterModUsesConsumed = __instance.GetUsesConsumed();
                }
                catch { }

                DebugLog(
                    $"[MEN-004 AMMO] actor='{__state.ActorName ?? "?"}' actorTemplate={__state.ActorTemplateId ?? "?"} actorPtr=0x{__state.ActorKey:X} " +
                    $"item={__state.ItemTemplateId ?? "?"} itemPtr=0x{__state.ItemKey:X} skill={skillId} flags={(Il2CppMenace.Tactical.Skills.UsageParameter)__state.UsageFlags} " +
                    $"success={__result} blocked={__state.BlockedByGuard} uses={__state.BeforeUses}->{afterNative}->{afterMod} " +
                    $"maxUses={__state.BeforeMaxUses}->{afterModMax} charges={__state.BeforeCharges}->{afterNativeCharges}->{afterModCharges} " +
                    $"consumedPerUse={__state.BeforeUsesConsumed}->{afterNativeUsesConsumed}->{afterModUsesConsumed} " +
                    $"chargesPerUse={__state.BeforeChargesPerUse} nativeDelta={nativeDelta} extraRequested={extraToConsume} extraCalls={extraCalls}.");
            }
        }
        catch (Exception ex)
        {
            if (_debugLogging)
                Warn($"Alternate-fire extra ammo consumption/diagnostics failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void HighVolumeFireIsUsablePostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        ref bool __result)
    {
        if (!_enabled || !__result || __instance == null) return;

        try
        {
            string skillId = __instance.GetID();
            if (!HighVolumeFireSkillIds.Contains(skillId)) return;
            if (__instance.HasLimitedUses() && __instance.GetUses() < 2)
                __result = false;
        }
        catch
        {
            // Keep native usability if a transient IL2CPP object is unavailable.
        }
    }

    private static void ApplyFireModeEconomyPolicy()
    {
        foreach (string skillId in HighVolumeFireSkillIds)
        {
            GameObj template = Templates.Find("SkillTemplate", skillId);
            if (template.IsNull) continue;

            try
            {
                ApplyFireModeActionPointPolicy(template, skillId, 40, capOnly: false);
            }
            catch (Exception ex)
            {
                if (_debugLogging)
                    Warn($"Could not update alternate-fire AP cost for {skillId}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (string skillId in AimedFireSkillIds)
        {
            GameObj template = Templates.Find("SkillTemplate", skillId);
            if (template.IsNull) continue;

            try
            {
                ApplyFireModeActionPointPolicy(template, skillId, 50, capOnly: true);
            }
            catch (Exception ex)
            {
                if (_debugLogging)
                    Warn($"Could not cap Aimed Fire AP cost for {skillId}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ApplyFireModeActionPointPolicy(GameObj template, string skillId, int value, bool capOnly)
    {
        const string fieldName = "ActionPointCost";
        string key = skillId + "|template|" + fieldName;
        object? current = Templates.ReadField(template, fieldName);
        if (current == null) return;

        if (!OriginalFireModeTemplateValues.ContainsKey(key))
            OriginalFireModeTemplateValues[key] = current;

        object original = OriginalFireModeTemplateValues[key];
        int originalCost;
        try { originalCost = Convert.ToInt32(original); }
        catch { return; }

        int enabledCost = capOnly ? Math.Min(originalCost, value) : value;
        object desired = _enabled ? enabledCost : original;

        if (!NumericEquivalent(current, desired))
        {
            bool written = Templates.WriteField(template, fieldName, desired);
            object? readBack = Templates.ReadField(template, fieldName);
            if (!written || readBack == null || !NumericEquivalent(readBack, desired))
                Warn($"Fire-mode AP write failed: {skillId}.{fieldName} desired={desired} readBack={readBack ?? "<null>"}.");
        }
    }

    private static void InstallMortarRangingPatch(global::HarmonyLib.Harmony harmony)
    {
        try
        {
            Type? skillType = global::HarmonyLib.AccessTools.TypeByName("Il2CppMenace.Tactical.Skills.Skill");
            Type? actorType = global::HarmonyLib.AccessTools.TypeByName("Il2CppMenace.Tactical.Actor");
            MethodInfo? applyToTile = skillType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType)
                    .FirstOrDefault(m => string.Equals(m.Name, "ApplyToTile", StringComparison.Ordinal)
                        && m.GetParameters().Length == 2);
            MethodInfo? scatter = skillType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType)
                    .FirstOrDefault(m => string.Equals(m.Name, "Scatter", StringComparison.Ordinal)
                        && m.GetParameters().Length == 1
                        && string.Equals(m.ReturnType.FullName, "Il2CppMenace.Tactical.Tile", StringComparison.Ordinal));
            MethodInfo? setStance = actorType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(actorType)
                    .FirstOrDefault(m => string.Equals(m.Name, "SetStance", StringComparison.Ordinal)
                        && m.GetParameters().Length == 2);
            MethodInfo? moveTo = actorType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(actorType)
                    .FirstOrDefault(m => string.Equals(m.Name, "MoveTo", StringComparison.Ordinal)
                        && m.ReturnType == typeof(bool)
                        && m.GetParameters().Length == 3);
            MethodInfo? moveToWithSkill = actorType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(actorType)
                    .FirstOrDefault(m => string.Equals(m.Name, "MoveToWithSkill", StringComparison.Ordinal)
                        && m.ReturnType == typeof(bool)
                        && m.GetParameters().Length == 4);

            if (applyToTile == null)
            {
                Warn("Mortar ranging-fire patch unavailable: Skill.ApplyToTile/2 was not found.");
                return;
            }

            // ApplyToTile is the stable public execution boundary used by MENACE for the
            // actual shot. Ranging state is now applied and recorded here so feedback does
            // not depend on a private helper being reached through the IL2CPP detour path.
            harmony.Patch(
                applyToTile,
                prefix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingApplyToTilePrefix)),
                postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingApplyToTilePostfix)));

            // Scatter is only a direct-hit probe now. If a future game update changes this
            // helper, progressive scatter tightening still works; only the immediate Scatter=1 jump
            // is unavailable until the probe is updated.
            if (scatter != null)
            {
                harmony.Patch(
                    scatter,
                    postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingScatterProbePostfix)));
            }
            else
            {
                Warn("Mortar direct-hit probe unavailable: Skill.Scatter/1 was not found. Progressive ranging remains active.");
            }

            if (setStance != null)
            {
                harmony.Patch(
                    setStance,
                    prefix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingSetStancePrefix)),
                    postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingSetStancePostfix)));
            }
            else
            {
                Warn("Mortar ranging-fire deployment reset unavailable: Actor.SetStance/2 was not found.");
            }

            if (moveTo != null)
            {
                harmony.Patch(
                    moveTo,
                    postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingMovePostfix)));
            }

            if (moveToWithSkill != null)
            {
                harmony.Patch(
                    moveToWithSkill,
                    postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarRangingMovePostfix)));
            }

            DebugLog("Mortar ranging patches installed: prior shots tighten Scatter to a miss-only floor of 2; exact impact unlocks Scatter=1; ScatterChance is unchanged.");
        }
        catch (Exception ex)
        {
            Warn($"Mortar ranging-fire patch failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MortarRangingApplyToTilePrefix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        Il2CppMenace.Tactical.Tile __0,
        out MortarApplyState? __state)
    {
        __state = null;
        if (!_enabled || __instance == null || __0 == null)
            return;

        try
        {
            string skillId = __instance.GetID();
            if (!MortarRangingSkillIds.Contains(skillId)) return;

            Il2CppMenace.Tactical.Skills.SkillTemplate template = __instance.GetTemplate();
            Il2CppMenace.Tactical.Actor actor = __instance.GetActor();
            if (template == null || actor == null) return;

            if (template.IsDeploymentRequired && !actor.IsDeployed())
            {
                ClearMortarRangingForActor(actor);
                return;
            }

            var key = MortarRangingKey(__instance, actor);
            if (key.Actor == 0) return;

            var tileKey = (__0.GetX(), __0.GetZ());
            int originalScatter = Math.Max(0, template.Scatter);
            int originalScatterChance = template.ScatterChance;
            int effectiveScatter = originalScatter;
            if (MortarRangingSolutions.TryGetValue(key, out Dictionary<(int X, int Z), MortarRangingState>? exactSolutions)
                && exactSolutions.TryGetValue(tileKey, out MortarRangingState? exactRanging)
                && exactRanging != null)
            {
                effectiveScatter = GetMortarEffectiveScatter(exactRanging, originalScatter);
            }

            if (effectiveScatter != originalScatter)
                template.Scatter = effectiveScatter;

            __state = new MortarApplyState
            {
                SkillKey = NativeKey(__instance),
                RangingKey = key,
                TileKey = tileKey,
                Template = template,
                OriginalScatter = originalScatter,
                OriginalScatterChance = originalScatterChance,
                EffectiveScatter = effectiveScatter,
                ImpactX = int.MinValue,
                ImpactZ = int.MinValue,
            };
            _activeMortarApplyState = __state;

            DebugLog($"MORTAR APPLY actor=0x{key.Actor:X} weapon=0x{key.Weapon:X} target=({tileKey.Item1},{tileKey.Item2}) baseScatter={originalScatter} effectiveScatter={effectiveScatter} scatterChance={originalScatterChance}%.");
        }
        catch (Exception ex)
        {
            __state = null;
            _activeMortarApplyState = null;
            if (_debugLogging)
                Warn($"Mortar ranging ApplyToTile prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MortarRangingScatterProbePostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        Il2CppMenace.Tactical.Tile __0,
        Il2CppMenace.Tactical.Tile __result)
    {
        if (__instance == null || __0 == null || __result == null) return;

        try
        {
            MortarApplyState? state = _activeMortarApplyState;
            if (state == null || state.SkillKey == 0 || NativeKey(__instance) != state.SkillKey)
                return;
            if (__0.GetX() != state.TileKey.X || __0.GetZ() != state.TileKey.Z)
                return;

            state.ImpactX = __result.GetX();
            state.ImpactZ = __result.GetZ();
            if (state.ImpactX == state.TileKey.X && state.ImpactZ == state.TileKey.Z)
                state.DirectHit = true;
        }
        catch (Exception ex)
        {
            if (_debugLogging)
                Warn($"Mortar direct-hit probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MortarRangingApplyToTilePostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        MortarApplyState? __state)
    {
        if (__state == null) return;

        try
        {
            if (__state.Template != null && __state.OriginalScatter >= 0)
                __state.Template.Scatter = __state.OriginalScatter;

            if (!_enabled || __state.RangingKey.Actor == 0) return;

            if (!MortarRangingSolutions.TryGetValue(__state.RangingKey, out Dictionary<(int X, int Z), MortarRangingState>? solutions))
            {
                solutions = new Dictionary<(int X, int Z), MortarRangingState>();
                MortarRangingSolutions[__state.RangingKey] = solutions;
            }

            if (!solutions.TryGetValue(__state.TileKey, out MortarRangingState? ranging) || ranging == null)
            {
                ranging = new MortarRangingState();
                solutions[__state.TileKey] = ranging;
            }

            if (ranging.Shots < 1000000)
                ranging.Shots++;
            if (__state.DirectHit)
                ranging.SolutionFound = true;

            ranging.LastBaseScatter = Math.Max(0, __state.OriginalScatter);
            ranging.LastEffectiveScatter = GetMortarEffectiveScatter(ranging, ranging.LastBaseScatter);

            string impact = __state.ImpactX == int.MinValue
                ? "unknown"
                : $"({__state.ImpactX},{__state.ImpactZ})";
            DebugLog($"MORTAR RESULT actor=0x{__state.RangingKey.Actor:X} weapon=0x{__state.RangingKey.Weapon:X} target=({__state.TileKey.X},{__state.TileKey.Z}) impact={impact} directHit={__state.DirectHit} shotScatter={__state.EffectiveScatter} nextScatter={ranging.LastEffectiveScatter} scatterChance={__state.OriginalScatterChance}% shots={ranging.Shots} solution={ranging.SolutionFound}.");
        }
        catch (Exception ex)
        {
            if (_debugLogging)
                Warn($"Mortar ranging ApplyToTile finalization failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (__state.Template != null && __state.OriginalScatter >= 0)
            {
                try { __state.Template.Scatter = __state.OriginalScatter; }
                catch { }
            }
            if (ReferenceEquals(_activeMortarApplyState, __state))
                _activeMortarApplyState = null;
        }
    }

    private static long NativeKey(Il2CppObjectBase? value)
    {
        try { return value?.Pointer.ToInt64() ?? 0; }
        catch { return 0; }
    }

    private static (long Actor, long Weapon) MortarRangingKey(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Actor actor)
    {
        // IL2CPP wrapper objects can be recreated between calls, so managed GetHashCode()
        // is not a stable identity. Use native actor/item pointers. HE and smoke from the
        // same physical weapon therefore share one solution.
        long actorKey = NativeKey(actor);
        long weaponKey = 0;
        try
        {
            Il2CppMenace.Items.Item item = skill.GetItem();
            weaponKey = NativeKey(item);
        }
        catch
        {
        }

        return (actorKey, weaponKey);
    }

    private static int GetMortarEffectiveScatter(MortarRangingState ranging, int baseScatter)
    {
        baseScatter = Math.Max(0, baseScatter);
        if (baseScatter <= 1) return baseScatter;
        if (ranging != null && ranging.SolutionFound)
            return Math.Min(baseScatter, MortarSolvedScatter);

        int shots = ranging == null ? 0 : Math.Max(0, ranging.Shots);
        int tightened = baseScatter - shots * MortarScatterReductionPerPriorShot;
        int floor = Math.Min(baseScatter, MortarUnsolvedScatterFloor);
        return Math.Max(floor, tightened);
    }

    private static float GetMortarRegistrationProgress(MortarRangingState ranging)
    {
        if (ranging == null || ranging.Shots <= 0) return 0f;
        if (ranging.SolutionFound) return 1f;

        int baseScatter = ranging.LastBaseScatter;
        if (baseScatter <= MortarUnsolvedScatterFloor) return 0f;
        int effective = ranging.LastEffectiveScatter > 0
            ? ranging.LastEffectiveScatter
            : GetMortarEffectiveScatter(ranging, baseScatter);

        int totalUnsolvedSteps = Math.Max(1, baseScatter - MortarUnsolvedScatterFloor);
        int completedSteps = Math.Clamp(baseScatter - effective, 1, totalUnsolvedSteps);
        if (totalUnsolvedSteps <= 1) return 0f;

        // First ranging correction is deliberately red. Subsequent tightening moves
        // through amber/yellow toward green, while full green is reserved for Scatter=1.
        return Mathf.Clamp01((completedSteps - 1) / (float)(totalUnsolvedSteps - 1)) * 0.78f;
    }

    private static MortarHighlightBlend GetMortarHighlightBlend(MortarRangingState ranging)
    {
        float progress = GetMortarRegistrationProgress(ranging);
        float strength = ranging.SolutionFound ? 15f : Mathf.Lerp(9f, 14f, progress);
        byte red = (byte)Math.Clamp((int)Math.Round(strength * (1f - progress)), 0, 15);
        byte green = (byte)Math.Clamp((int)Math.Round(strength * progress), 0, 15);
        if (ranging.SolutionFound)
        {
            red = 0;
            green = 15;
        }
        else if (red == 0 && green == 0)
        {
            red = 1;
        }

        int effectiveScatter = ranging.LastEffectiveScatter > 0
            ? ranging.LastEffectiveScatter
            : Math.Max(MortarUnsolvedScatterFloor, ranging.LastBaseScatter);
        return new MortarHighlightBlend(red, green, effectiveScatter, ranging.SolutionFound);
    }

    private static void MortarRangingSetStancePrefix(
        Il2CppMenace.Tactical.Actor __instance,
        out bool __state)
    {
        __state = false;
        if (__instance == null) return;
        try { __state = __instance.IsDeployed(); }
        catch { }
    }

    private static void MortarRangingSetStancePostfix(
        Il2CppMenace.Tactical.Actor __instance,
        bool __state)
    {
        if (__instance == null || !__state) return;

        bool stillDeployed = false;
        try { stillDeployed = __instance.IsDeployed(); }
        catch { return; }
        if (stillDeployed) return;

        DebugLog($"MORTAR RESET actor=0x{NativeKey(__instance):X} reason=actual undeploy transition.");
        ClearMortarRangingForActor(__instance);
    }

    private static void MortarRangingMovePostfix(
        Il2CppMenace.Tactical.Actor __instance,
        bool __result)
    {
        // Vehicle-mounted mortars have no deployed stance. Their solution survives
        // while stationary and is discarded as soon as the platform actually moves.
        if (__instance == null || !__result) return;
        DebugLog($"MORTAR RESET actor=0x{NativeKey(__instance):X} reason=movement committed.");
        ClearMortarRangingForActor(__instance);
    }

    private static void ClearMortarRangingForActor(Il2CppMenace.Tactical.Actor actor)
    {
        if (actor == null || MortarRangingSolutions.Count == 0) return;

        long actorKey = NativeKey(actor);
        if (actorKey == 0) return;

        List<(long Actor, long Weapon)>? remove = null;
        foreach (var key in MortarRangingSolutions.Keys)
        {
            if (key.Actor != actorKey) continue;
            remove ??= new List<(long Actor, long Weapon)>();
            remove.Add(key);
        }

        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++)
            MortarRangingSolutions.Remove(remove[i]);
    }

    private static Material? GetMortarWorldLineMaterial()
    {
        if (_mortarWorldLineMaterial != null) return _mortarWorldLineMaterial;

        // Do not reuse MENACE TileHighlighter channels here: delayed-AoE channels are
        // shared with off-map markers such as Supply Drop. This renderer is completely
        // independent and deliberately drawn on top of terrain instead.
        string[] shaderNames =
        {
            "Hidden/Internal-Colored",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Color",
        };

        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader == null) continue;

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 5000,
            };

            // Hidden/Internal-Colored exposes these controls. The fallbacks may not;
            // HasProperty keeps the setup harmless on those shaders.
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", 8); // Always
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);   // Off
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", 5); // SrcAlpha
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", 10); // OneMinusSrcAlpha
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", Texture2D.whiteTexture);

            _mortarWorldLineMaterial = material;
            _mortarWorldLineShaderName = shaderNames[i];
            return _mortarWorldLineMaterial;
        }

        return null;
    }

    private static LineRenderer AddMortarWorldLine(GameObject root, Material material, int sortingOrder, string childName)
    {
        // Unity only permits one Renderer on a GameObject. In 0.4.3 the outer and
        // inner strokes were both added to `root`, so the second AddComponent failed
        // and the highlight was never registered. Give each stroke its own child.
        var lineObject = new GameObject(childName)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        lineObject.transform.SetParent(root.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.enabled = true;
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.sharedMaterial = material;
        line.numCornerVertices = 4;
        line.numCapVertices = 0;
        line.sortingOrder = sortingOrder;
        return line;
    }

    private static MortarWorldHighlight? CreateMortarWorldHighlight(
        Il2CppMenace.Tactical.Tile tile,
        MortarHighlightBlend blend)
    {
        Material? material = GetMortarWorldLineMaterial();
        if (material == null)
        {
            if (_debugLogging && !_mortarWorldRendererLogged)
            {
                _mortarWorldRendererLogged = true;
                Warn("MORTAR HIGHLIGHT: no compatible Unity line shader was found; world-space ranging border cannot be created.");
            }
            return null;
        }

        var root = new GameObject("WMR Mortar Ranging Tile")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        try
        {
            var result = new MortarWorldHighlight
            {
                Root = root,
                OuterLine = AddMortarWorldLine(root, material, 32760, "WMR Mortar Ranging Outer"),
                InnerLine = AddMortarWorldLine(root, material, 32761, "WMR Mortar Ranging Inner"),
                Blend = blend,
            };
            UpdateMortarWorldHighlight(result, tile, blend);

            if (_debugLogging && !_mortarWorldRendererLogged)
            {
                _mortarWorldRendererLogged = true;
                DebugLog($"MORTAR HIGHLIGHT renderer=dual-LineRenderer separate-children shader={_mortarWorldLineShaderName} depth=always sorting=32760/32761.");
            }

            return result;
        }
        catch
        {
            // Avoid leaking partial renderers when Unity refuses a component.
            UnityEngine.Object.Destroy(root);
            throw;
        }
    }

    private static void SetMortarWorldLinePositions(LineRenderer line, Vector3 center)
    {
        const float halfTile = 3.86f;
        float y = center.y + 0.55f;
        line.SetPosition(0, new Vector3(center.x - halfTile, y, center.z - halfTile));
        line.SetPosition(1, new Vector3(center.x + halfTile, y, center.z - halfTile));
        line.SetPosition(2, new Vector3(center.x + halfTile, y, center.z + halfTile));
        line.SetPosition(3, new Vector3(center.x - halfTile, y, center.z + halfTile));
    }

    private static void UpdateMortarWorldHighlight(
        MortarWorldHighlight worldHighlight,
        Il2CppMenace.Tactical.Tile tile,
        MortarHighlightBlend blend)
    {
        if (worldHighlight == null
            || worldHighlight.OuterLine == null
            || worldHighlight.InnerLine == null
            || tile == null)
        {
            return;
        }

        Vector3 center = tile.GetPos();
        SetMortarWorldLinePositions(worldHighlight.OuterLine, center);
        SetMortarWorldLinePositions(worldHighlight.InnerLine, center);

        // A thick dark under-stroke makes the square readable over snow, sand, grass,
        // smoke and bright effects. The inner stroke carries the ranging colour.
        Color color = GetMortarOverlayColor(blend);
        Color outer = new Color(0.015f, 0.015f, 0.015f, 0.96f);

        LineRenderer outerLine = worldHighlight.OuterLine;
        outerLine.startColor = outer;
        outerLine.endColor = outer;
        float outerWidth = blend.SolutionFound ? 0.82f : 0.68f;
        outerLine.startWidth = outerWidth;
        outerLine.endWidth = outerWidth;

        LineRenderer innerLine = worldHighlight.InnerLine;
        innerLine.startColor = color;
        innerLine.endColor = color;
        float innerWidth = blend.SolutionFound ? 0.48f : 0.38f;
        innerLine.startWidth = innerWidth;
        innerLine.endWidth = innerWidth;

        worldHighlight.Blend = blend;
    }

    private static void UpdateMortarWorldHighlights()
    {
        try
        {
            Il2CppMenace.Tactical.TacticalManager manager = Il2CppMenace.Tactical.TacticalManager.Get();
            Il2CppMenace.Tactical.Actor? actor = manager == null ? null : manager.GetActiveActor();
            long actorKey = NativeKey(actor);

            if (!_enabled || manager == null || actor == null || actorKey == 0 || MortarRangingSolutions.Count == 0)
            {
                ClearMortarWorldHighlights();
                return;
            }

            var desired = new Dictionary<(int X, int Z), MortarHighlightBlend>();
            foreach (var pair in MortarRangingSolutions)
            {
                if (pair.Key.Actor != actorKey) continue;
                foreach (var tilePair in pair.Value)
                {
                    MortarRangingState? ranging = tilePair.Value;
                    if (ranging == null || ranging.Shots <= 0) continue;

                    MortarHighlightBlend blend = GetMortarHighlightBlend(ranging);
                    if (!desired.TryGetValue(tilePair.Key, out MortarHighlightBlend existing)
                        || blend.SolutionFound && !existing.SolutionFound
                        || blend.EffectiveScatter < existing.EffectiveScatter)
                    {
                        desired[tilePair.Key] = blend;
                    }
                }
            }

            if (desired.Count == 0)
            {
                ClearMortarWorldHighlights();
                return;
            }

            Il2CppMenace.Tactical.Map map = manager.GetMap();
            if (map == null) return;

            if (_mortarWorldHighlightActorKey != 0 && _mortarWorldHighlightActorKey != actorKey)
                ClearMortarWorldHighlights();

            if (MortarWorldHighlightTiles.Count > 0)
            {
                var stale = new List<(int X, int Z)>();
                foreach (var pair in MortarWorldHighlightTiles)
                {
                    if (desired.ContainsKey(pair.Key)) continue;
                    if (pair.Value?.Root != null) UnityEngine.Object.Destroy(pair.Value.Root);
                    stale.Add(pair.Key);
                }
                for (int i = 0; i < stale.Count; i++) MortarWorldHighlightTiles.Remove(stale[i]);
            }

            foreach (var pair in desired)
            {
                Il2CppMenace.Tactical.Tile tile = map.GetTile(pair.Key.X, pair.Key.Z);
                if (tile == null) continue;

                if (!MortarWorldHighlightTiles.TryGetValue(pair.Key, out MortarWorldHighlight? worldHighlight)
                    || worldHighlight == null
                    || worldHighlight.OuterLine == null
                    || worldHighlight.InnerLine == null)
                {
                    MortarWorldHighlight? created = CreateMortarWorldHighlight(tile, pair.Value);
                    if (created != null) MortarWorldHighlightTiles[pair.Key] = created;
                    continue;
                }

                MortarHighlightBlend previous = worldHighlight.Blend;
                if (previous.Red != pair.Value.Red
                    || previous.Green != pair.Value.Green
                    || previous.EffectiveScatter != pair.Value.EffectiveScatter
                    || previous.SolutionFound != pair.Value.SolutionFound)
                {
                    UpdateMortarWorldHighlight(worldHighlight, tile, pair.Value);
                }
            }

            _mortarWorldHighlightActorKey = actorKey;
        }
        catch (Exception ex)
        {
            // This runs every frame. Emit at most one diagnostic and only with Debug ON.
            if (_debugLogging && !_mortarWorldRendererErrorLogged)
            {
                _mortarWorldRendererErrorLogged = true;
                Warn($"Mortar world-space tile highlight failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ClearMortarWorldHighlights()
    {
        foreach (var pair in MortarWorldHighlightTiles)
        {
            try
            {
                if (pair.Value?.Root != null) UnityEngine.Object.Destroy(pair.Value.Root);
            }
            catch
            {
            }
        }

        MortarWorldHighlightTiles.Clear();
        _mortarWorldHighlightActorKey = 0;
    }

    private static void DrawMortarRangingOverlay()
    {
        if (_mortarOverlayErrorLogged || !_enabled || Event.current == null || Event.current.type != EventType.Repaint || MortarRangingSolutions.Count == 0)
            return;

        try
        {
            Il2CppMenace.Tactical.TacticalManager manager = Il2CppMenace.Tactical.TacticalManager.Get();
            if (manager == null) return;

            Il2CppMenace.Tactical.Actor? actor = manager.GetActiveActor();
            long actorKey = NativeKey(actor);
            if (actor == null || actorKey == 0) return;

            // If a mortar skill is actively selected, prefer that physical weapon.
            // Otherwise show every registered solution belonging to the selected mortar
            // actor. This makes the feedback visible as soon as the mortar unit itself is
            // selected, matching the normal tactical selection flow.
            var solutionMaps = new List<Dictionary<(int X, int Z), MortarRangingState>>();
            Il2CppMenace.States.TacticalState state = Il2CppMenace.States.TacticalState.Get();
            Il2CppMenace.Tactical.Skills.Skill? selectedSkill = state == null ? null : state.GetSelectedSkill();
            if (selectedSkill != null && MortarRangingSkillIds.Contains(selectedSkill.GetID()))
            {
                Il2CppMenace.Tactical.Actor? selectedSkillActor = selectedSkill.GetActor();
                if (NativeKey(selectedSkillActor) == actorKey)
                {
                    var exactKey = MortarRangingKey(selectedSkill, actor);
                    if (MortarRangingSolutions.TryGetValue(exactKey, out Dictionary<(int X, int Z), MortarRangingState>? exact) && exact.Count > 0)
                        solutionMaps.Add(exact);
                }
            }

            if (solutionMaps.Count == 0)
            {
                foreach (var pair in MortarRangingSolutions)
                {
                    if (pair.Key.Actor == actorKey && pair.Value.Count > 0)
                        solutionMaps.Add(pair.Value);
                }
            }

            if (solutionMaps.Count == 0) return;

            Il2CppMenace.Tactical.Map map = manager.GetMap();
            Il2CppMenace.Tactical.TacticalCamera tacticalCamera = manager.GetCamera();
            Camera? camera = tacticalCamera == null ? null : tacticalCamera.GetCamera();
            if (map == null || camera == null) return;

            EnsureMortarOverlayStyles();

            // Merge multiple physical mortars on one actor by taking the tightest
            // registered dispersion on each tile. The optional label reports the
            // effective Scatter value for the next shot (S1 is a solved firing solution).
            var merged = new Dictionary<(int X, int Z), MortarHighlightBlend>();
            for (int m = 0; m < solutionMaps.Count; m++)
            {
                foreach (var pair in solutionMaps[m])
                {
                    MortarRangingState ranging = pair.Value;
                    if (ranging == null || ranging.Shots <= 0) continue;

                    MortarHighlightBlend blend = GetMortarHighlightBlend(ranging);
                    if (!merged.TryGetValue(pair.Key, out MortarHighlightBlend existing)
                        || blend.SolutionFound && !existing.SolutionFound
                        || blend.EffectiveScatter < existing.EffectiveScatter)
                    {
                        merged[pair.Key] = blend;
                    }
                }
            }

            foreach (var pair in merged)
            {
                Il2CppMenace.Tactical.Tile tile = map.GetTile(pair.Key.X, pair.Key.Z);
                if (tile == null) continue;

                Color color = GetMortarOverlayColor(pair.Value);
                DrawMortarTileBorder(camera, tile, pair.Value.SolutionFound ? 3f : 2f, color);
                DrawMortarTileScatter(camera, tile, pair.Value.EffectiveScatter, color);
            }
        }
        catch (Exception ex)
        {
            // OnGUI runs every frame. Disable this optional overlay after the first
            // scene-local failure so Debug mode cannot create log spam or frame-time churn.
            _mortarOverlayErrorLogged = true;
            if (_debugLogging)
                Warn($"Mortar ranging overlay disabled for this scene after an error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void EnsureMortarOverlayStyles()
    {
        if (_mortarBonusStyle != null && _mortarBonusShadowStyle != null) return;

        _mortarBonusStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15,
            wordWrap = false
        };
        _mortarBonusStyle.normal.textColor = Color.white;

        _mortarBonusShadowStyle = new GUIStyle(_mortarBonusStyle);
        _mortarBonusShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
    }

    private static Color GetMortarOverlayColor(MortarHighlightBlend blend)
    {
        float total = Math.Max(1f, blend.Red + blend.Green);
        float greenMix = blend.Green / total;
        Color low = new Color(1.00f, 0.08f, 0.04f, 0.98f);
        Color high = blend.SolutionFound
            ? new Color(0.18f, 1.00f, 0.22f, 1f)
            : new Color(0.20f, 0.92f, 0.18f, 0.98f);
        return Color.Lerp(low, high, greenMix);
    }

    private static void DrawMortarTileBorder(Camera camera, Il2CppMenace.Tactical.Tile tile, float thickness, Color color)
    {
        const float halfTile = 4f;
        Vector3 center = tile.GetPos();
        float y = center.y + 0.08f;
        Vector3[] world =
        {
            new Vector3(center.x - halfTile, y, center.z - halfTile),
            new Vector3(center.x + halfTile, y, center.z - halfTile),
            new Vector3(center.x + halfTile, y, center.z + halfTile),
            new Vector3(center.x - halfTile, y, center.z + halfTile),
        };

        Vector2[] screen = new Vector2[4];
        for (int i = 0; i < world.Length; i++)
        {
            Vector3 p = camera.WorldToScreenPoint(world[i]);
            if (p.z <= 0f) return;
            screen[i] = new Vector2(p.x, Screen.height - p.y);
        }

        Color oldColor = GUI.color;
        GUI.color = color;
        for (int i = 0; i < 4; i++)
            DrawGuiLine(screen[i], screen[(i + 1) % 4], thickness);
        GUI.color = oldColor;
    }

    private static void DrawMortarTileScatter(Camera camera, Il2CppMenace.Tactical.Tile tile, int effectiveScatter, Color color)
    {
        if (_mortarBonusStyle == null || _mortarBonusShadowStyle == null) return;

        Vector3 center = tile.GetPos();
        center.y += 0.18f;
        Vector3 p = camera.WorldToScreenPoint(center);
        if (p.z <= 0f) return;

        const float width = 62f;
        const float height = 22f;
        float x = p.x - width * 0.5f;
        float y = Screen.height - p.y - height * 0.5f;
        string text = "S" + Math.Max(0, effectiveScatter).ToString();

        Color old = _mortarBonusStyle.normal.textColor;
        _mortarBonusStyle.normal.textColor = color;
        GUI.Label(new Rect(x + 1f, y + 1f, width, height), text, _mortarBonusShadowStyle);
        GUI.Label(new Rect(x, y, width, height), text, _mortarBonusStyle);
        _mortarBonusStyle.normal.textColor = old;
    }

    private static void DrawGuiLine(Vector2 from, Vector2 to, float thickness)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0.5f) return;

        Matrix4x4 oldMatrix = GUI.matrix;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.matrix = oldMatrix;
    }

    private static void InstallMortarSuppressionRadiusPatch(global::HarmonyLib.Harmony harmony)
    {
        try
        {
            Type? skillType = global::HarmonyLib.AccessTools.TypeByName("Il2CppMenace.Tactical.Skills.Skill");
            MethodInfo? applySuppression = skillType == null
                ? null
                : global::HarmonyLib.AccessTools.GetDeclaredMethods(skillType)
                    .FirstOrDefault(m => string.Equals(m.Name, "ApplySuppression", StringComparison.Ordinal) && m.GetParameters().Length == 5);

            if (applySuppression == null)
            {
                Warn("Mortar suppression-radius patch unavailable: Skill.ApplySuppression/5 was not found.");
                return;
            }

            harmony.Patch(
                applySuppression,
                postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(MortarSuppressionRadiusPostfix)));

            DebugLog("Mortar suppression-radius patch installed (+1 tile suppression-only outer ring).");
        }
        catch (Exception ex)
        {
            Warn($"Mortar suppression-radius patch failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Native mortar AoE remains untouched for damage/armor effects. After MENACE applies
    // its normal suppression, add one suppression-only ring around the native affected
    // tiles. This gives indirect fire a wider beaten/suppression zone without increasing
    // the physical blast radius. The outer ring receives 50% of the native suppression
    // value and still goes through Actor.ApplySuppression, so normal resistance/modifiers
    // remain authoritative.
    private static void MortarSuppressionRadiusPostfix(
        Il2CppMenace.Tactical.Skills.Skill __instance,
        object[] __args)
    {
        if (!_enabled || __instance == null || __args == null || __args.Length < 4)
            return;

        var origin = __args[0] as Il2CppMenace.Tactical.Tile;
        var target = __args[1] as Il2CppMenace.Tactical.Tile;
        float value;
        bool directOnly;
        try
        {
            value = Convert.ToSingle(__args[2]);
            directOnly = Convert.ToBoolean(__args[3]);
        }
        catch
        {
            return;
        }

        if (directOnly || value <= 0f || origin == null || target == null)
            return;

        try
        {
            string skillId = __instance.GetID();
            if (string.IsNullOrEmpty(skillId) || !MortarSkillSpecs.TryGetValue(skillId, out MortarSkillSpec? spec))
                return;
            if (spec.ExtraSuppressionRadius <= 0 || spec.ExtraSuppressionMult <= 0f)
                return;

            var affected = __instance.GetAffectedTiles(origin, target);
            if (affected == null || affected.Count == 0)
                return;

            var nativeKeys = new HashSet<long>();
            var seenOuterKeys = new HashSet<long>();
            var frontier = new List<Il2CppMenace.Tactical.Tile>();

            for (int i = 0; i < affected.Count; i++)
            {
                Il2CppMenace.Tactical.Tile tile = affected[i];
                if (tile == null) continue;
                nativeKeys.Add(MortarTileKey(tile));
                frontier.Add(tile);
            }

            var rings = new List<List<Il2CppMenace.Tactical.Tile>>();
            for (int ring = 0; ring < spec.ExtraSuppressionRadius && frontier.Count > 0; ring++)
            {
                var nextFrontier = new List<Il2CppMenace.Tactical.Tile>();
                for (int i = 0; i < frontier.Count; i++)
                {
                    Il2CppMenace.Tactical.Tile tile = frontier[i];
                    for (int d = 0; d < 8; d++)
                    {
                        Il2CppMenace.Tactical.Tile next = tile.GetNextTile((Il2CppMenace.Tactical.Direction)d);
                        if (next == null) continue;

                        long key = MortarTileKey(next);
                        if (nativeKeys.Contains(key) || !seenOuterKeys.Add(key))
                            continue;

                        nextFrontier.Add(next);
                    }
                }

                rings.Add(nextFrontier);
                frontier = nextFrontier;
            }

            Il2CppMenace.Tactical.Actor suppressor = __instance.GetActor();
            if (suppressor == null) return;

            float ringSuppression = value;
            for (int ring = 0; ring < rings.Count; ring++)
            {
                ringSuppression *= spec.ExtraSuppressionMult;
                if (ringSuppression <= 0f) break;

                List<Il2CppMenace.Tactical.Tile> tiles = rings[ring];
                for (int i = 0; i < tiles.Count; i++)
                {
                    Il2CppMenace.Tactical.Actor actor = tiles[i].GetActor();
                    if (actor == null) continue;
                    actor.ApplySuppression(ringSuppression, false, suppressor, __instance);
                }
            }
        }
        catch (Exception ex)
        {
            if (_debugLogging)
                Warn($"Mortar suppression outer-ring application failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static long MortarTileKey(Il2CppMenace.Tactical.Tile tile)
    {
        return ((long)tile.GetX() << 32) ^ (uint)tile.GetZ();
    }

    private static void ApplyMortarBallisticsPolicy()
    {
        foreach (var pair in MortarSkillSpecs)
        {
            GameObj template = Templates.Find("SkillTemplate", pair.Key);
            if (template.IsNull) continue;

            MortarSkillSpec spec = pair.Value;
            try
            {
                ApplyMortarTemplateValue(template, pair.Key, "Scatter", spec.Scatter);
                ApplyMortarTemplateValue(template, pair.Key, "ScatterChance", spec.ScatterChance);
                ApplyMortarTemplateScaledValue(template, pair.Key, "ActionPointCost", spec.ActionPointCostMult);
                ApplyMortarTemplateScaledValue(template, pair.Key, "Uses", spec.UsesMult);

                object? handlers = Templates.ReadField(template, "EventHandlers");
                int index = 0;
                foreach (object handler in EnumerateInteropItems(handlers))
                {
                    // Attack handlers expose these members; unrelated handlers are skipped.
                    if (FindMemberRecursive(handler.GetType(), "SuppressionDropoffAOE") == null)
                    {
                        index++;
                        continue;
                    }

                    ApplyMortarHandlerScaledValue(pair.Key, index, handler, "DamageMult", spec.DamageMult);
                    ApplyMortarHandlerScaledValue(pair.Key, index, handler, "DamageToArmorDurabilityMult", spec.DamageToArmorDurabilityMult);
                    ApplyMortarHandlerValue(pair.Key, index, handler, "SuppressionDropoffAOE", spec.SuppressionDropoffAOE);
                    if (spec.AccuracyBonus.HasValue)
                        ApplyMortarHandlerValue(pair.Key, index, handler, "AccuracyBonus", spec.AccuracyBonus.Value);
                    if (spec.AccuracyMult.HasValue)
                        ApplyMortarHandlerValue(pair.Key, index, handler, "AccuracyMult", spec.AccuracyMult.Value);
                    break;
                }
            }
            catch (Exception ex)
            {
                if (_debugLogging)
                    Warn($"Could not update mortar ballistics balance for {pair.Key}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (string skillId in MortarSmokeSkillIds)
        {
            GameObj template = Templates.Find("SkillTemplate", skillId);
            if (template.IsNull) continue;

            try
            {
                ApplyMortarTemplateScaledValue(template, skillId, "ActionPointCost", 0.5f);
                ApplyMortarTemplateScaledValue(template, skillId, "Uses", 2f);
            }
            catch (Exception ex)
            {
                if (_debugLogging)
                    Warn($"Could not update smoke-mortar action economy for {skillId}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ApplyMortarTemplateValue(GameObj template, string skillId, string fieldName, object enabledValue)
    {
        string key = skillId + "|template|" + fieldName;
        object? current = Templates.ReadField(template, fieldName);
        if (current == null) return;
        if (!OriginalMortarTemplateValues.ContainsKey(key))
            OriginalMortarTemplateValues[key] = current;

        object desired = _enabled ? enabledValue : OriginalMortarTemplateValues[key];
        if (!NumericEquivalent(current, desired))
        {
            bool written = Templates.WriteField(template, fieldName, desired);
            object? readBack = Templates.ReadField(template, fieldName);
            if (!written || readBack == null || !NumericEquivalent(readBack, desired))
                Warn($"Mortar balance write failed: {skillId}.{fieldName} desired={desired} readBack={readBack ?? "<null>"}.");
        }
    }

    private static void ApplyMortarTemplateScaledValue(GameObj template, string skillId, string fieldName, float enabledMultiplier)
    {
        string key = skillId + "|template|" + fieldName;
        object? current = Templates.ReadField(template, fieldName);
        if (current == null) return;
        if (!OriginalMortarTemplateValues.ContainsKey(key))
            OriginalMortarTemplateValues[key] = current;

        object original = OriginalMortarTemplateValues[key];
        double originalNumber;
        try { originalNumber = Convert.ToDouble(original); }
        catch { return; }

        object desired;
        if (!_enabled)
        {
            desired = original;
        }
        else if (original is int)
        {
            desired = Math.Max(0, (int)Math.Round(originalNumber * enabledMultiplier, MidpointRounding.AwayFromZero));
        }
        else if (original is float)
        {
            desired = (float)(originalNumber * enabledMultiplier);
        }
        else if (original is double)
        {
            desired = originalNumber * enabledMultiplier;
        }
        else
        {
            desired = Convert.ChangeType(originalNumber * enabledMultiplier, original.GetType());
        }

        if (!NumericEquivalent(current, desired))
        {
            bool written = Templates.WriteField(template, fieldName, desired);
            object? readBack = Templates.ReadField(template, fieldName);
            if (!written || readBack == null || !NumericEquivalent(readBack, desired))
                Warn($"Mortar balance scaled write failed: {skillId}.{fieldName} desired={desired} readBack={readBack ?? "<null>"}.");
        }
    }

    private static void ApplyMortarHandlerScaledValue(string skillId, int handlerIndex, object handler, string fieldName, float enabledMultiplier)
    {
        MemberInfo? member = FindMemberRecursive(handler.GetType(), fieldName);
        if (member == null) return;
        object? current = ReadMemberValue(member, handler);
        if (current == null) return;

        string key = skillId + "|handler" + handlerIndex + "|" + fieldName;
        if (!OriginalMortarHandlerValues.ContainsKey(key))
            OriginalMortarHandlerValues[key] = current;

        object original = OriginalMortarHandlerValues[key];
        double originalNumber;
        try { originalNumber = Convert.ToDouble(original); }
        catch { return; }
        object desired = _enabled ? (object)(float)(originalNumber * enabledMultiplier) : original;

        if (!NumericEquivalent(current, desired))
        {
            bool written = TryWriteNumericMember(member, handler, desired);
            object? readBack = ReadMemberValue(member, handler);
            if (!written || readBack == null || !NumericEquivalent(readBack, desired))
                Warn($"Mortar balance scaled handler write failed: {skillId}.{fieldName} desired={desired} readBack={readBack ?? "<null>"}.");
        }
    }

    private static void ApplyMortarHandlerValue(string skillId, int handlerIndex, object handler, string fieldName, float enabledValue)
    {
        MemberInfo? member = FindMemberRecursive(handler.GetType(), fieldName);
        if (member == null) return;
        object? current = ReadMemberValue(member, handler);
        if (current == null) return;

        string key = skillId + "|handler" + handlerIndex + "|" + fieldName;
        if (!OriginalMortarHandlerValues.ContainsKey(key))
            OriginalMortarHandlerValues[key] = current;

        object desired = _enabled ? enabledValue : OriginalMortarHandlerValues[key];
        if (!NumericEquivalent(current, desired))
        {
            bool written = TryWriteNumericMember(member, handler, desired);
            object? readBack = ReadMemberValue(member, handler);
            if (!written || readBack == null || !NumericEquivalent(readBack, desired))
                Warn($"Mortar balance handler write failed: {skillId}.{fieldName} desired={desired} readBack={readBack ?? "<null>"}.");
        }
    }

    private static IEnumerable<object> EnumerateInteropItems(object? collection)
    {
        if (collection == null) yield break;
        if (collection is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
                if (item != null) yield return item;
            yield break;
        }

        MethodInfo? getEnumerator = collection.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        object? enumerator = getEnumerator?.Invoke(collection, null);
        if (enumerator == null) yield break;
        MethodInfo? moveNext = enumerator.GetType().GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        PropertyInfo? current = enumerator.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
        if (moveNext == null || current == null) yield break;

        while (Convert.ToBoolean(moveNext.Invoke(enumerator, null)))
        {
            object? value = current.GetValue(enumerator, null);
            if (value != null) yield return value;
        }
    }

    private static object? ReadMemberValue(MemberInfo member, object instance)
    {
        try
        {
            return member switch
            {
                FieldInfo field => field.GetValue(instance),
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(instance),
                _ => null
            };
        }
        catch { return null; }
    }

    private static bool TryWriteNumericMember(MemberInfo member, object instance, object value)
    {
        try
        {
            Type targetType;
            if (member is FieldInfo field) targetType = field.FieldType;
            else if (member is PropertyInfo property && property.CanWrite && property.GetIndexParameters().Length == 0) targetType = property.PropertyType;
            else return false;

            object converted = targetType.IsEnum
                ? Enum.ToObject(targetType, Convert.ToInt32(value))
                : Convert.ChangeType(value, targetType);

            if (member is FieldInfo f) f.SetValue(instance, converted);
            else ((PropertyInfo)member).SetValue(instance, converted);
            return true;
        }
        catch { return false; }
    }

    private static bool NumericEquivalent(object a, object b)
    {
        try { return Math.Abs(Convert.ToDouble(a) - Convert.ToDouble(b)) < 0.0001; }
        catch { return Equals(a, b); }
    }

    private static void DebugLog(string message)
    {
        if (_debugLogging) _logger?.Msg("[DEBUG] " + message);
    }

    private static void Warn(string message) => _logger?.Warning(message);
    private static void Error(string message) => _logger?.Error(message);
}

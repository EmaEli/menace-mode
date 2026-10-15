using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MenaceModSettings;
using MenaceModSuite.Shared;
using UnityEngine;

namespace GarrisonableBuildings;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/GarrisonableBuildings/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string ModName = "Garrisonable Buildings";
    private const string Version = "1.0.4";
    private const string BuildingCoverProfileName = "cover_inside_building";
    private const string StrongerCoverSettingsGroup = "StrongerCover";
    private const int VanillaContainerAP = 30;
    private const int RetryEveryUpdates = 120;
    private const int MaxAttempts = 30;

    // Conservative whitelist. Every entry is validated against the runtime
    // footprint before it is changed. This prevents a misleading name from
    // turning a differently-sized prop into a container.
    private sealed class CandidateSpec
    {
        public CandidateSpec(string id, string name, int width, int height, int extraAP, bool optional3x3 = false)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            ExtraAP = extraAP;
            Optional3x3 = optional3x3;
        }

        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int ExtraAP { get; }
        public bool Optional3x3 { get; }

        public bool MatchesFootprint(int width, int height)
        {
            return (width == Width && height == Height) ||
                   (width == Height && height == Width);
        }
    }

    private static readonly CandidateSpec[] Candidates =
    {
        // 1x1: vanilla container AP cost.
        new CandidateSpec("building_civilian_shelter_01", "Small Civilian Shelter", 1, 1, 0),
        new CandidateSpec("building_civilian_water_treatment_building_01", "Control Building", 1, 1, 0),
        new CandidateSpec("building_pirate_shelter_01", "Pirate Shelter", 1, 1, 0),

        // 1x2: small surcharge (+10 AP).
        new CandidateSpec("building_civilian_shelter_1x2_01", "Civilian Shelter", 1, 2, 10),
        new CandidateSpec("building_civilian_water_storage_plant_1x2", "Water Storage Plant", 1, 2, 10),
        new CandidateSpec("building_pirate_makeshift_storage_01_1x2", "Makeshift Storage", 1, 2, 10),

        // Other small footprints. Runtime footprint checks remain authoritative.
        new CandidateSpec("building_civilian_water_storage_plant_1x3", "Water Treatment Plant 1x3", 1, 3, 20),
        new CandidateSpec("building_civilian_water_storage_plant_2x2", "Water Treatment Plant 2x2", 2, 2, 20),
        new CandidateSpec("building_civilian_water_storage_plant_2x3", "Water Treatment Plant 2x3", 2, 3, 30),

        // 3x3 is deliberately experimental and OFF by default in Mod Settings.
        new CandidateSpec("building_civilian_greenhouse_01", "Greenhouse", 3, 3, 40, optional3x3: true),
        new CandidateSpec("building_civilian_storage_01", "Storage Facility", 3, 3, 40, optional3x3: true)
    };

    private static readonly Dictionary<string, CandidateSpec> CandidateById =
        Candidates.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> _enabledExtraAP = new(StringComparer.OrdinalIgnoreCase);

    private sealed class APCostOverrideState
    {
        public object Properties;
        public int OriginalEnter;
        public int OriginalLeave;
        public bool ChangedEnter;
        public bool ChangedLeave;
    }

    private static ModLog _log;
    private static Type _dataTemplateLoaderType;
    private static Type _entityTemplateType;
    private static MethodInfo _dataTemplateGetGeneric;
    private static Type _actorType;
    private static Type _movementSystemType;
    private static object _buildingInsideCover;

    private static readonly HashSet<string> _handled = new(StringComparer.OrdinalIgnoreCase);
    private static int _updateCounter;
    private static int _attempts;
    private static bool _finished;
    private static int _apOverrideDepth;
    private static bool _debugLogging;

    public void Initialize(ModLog logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Building sizes");
            settings.AddToggle(
                "Enable3x3Buildings",
                "Enable selected 3x3 buildings as garrisons (experimental; OFF by default)",
                false);

            settings.AddHeader("Inside cover");
            settings.AddSlider("BuildingAccuracy", "Base building hit chance (%)", 5f, 100f, 40f);
            settings.AddSlider("BuildingDamage", "Building damage multiplier (%)", 5f, 100f, 60f);
            settings.AddSlider("BuildingSuppression", "Building suppression multiplier (%)", 5f, 100f, 60f);
            settings.AddSlider("BuildingConcealment", "Building concealment bonus", 0f, 10f, 2f);

            settings.AddHeader("Diagnostics");
            settings.AddToggle(
                "DebugLogging",
                "Enable diagnostic logging (OFF by default)",
                false);
        });

        _debugLogging = ReadBool("DebugLogging", false);
        ModSettings.OnSettingChanged += OnSettingChanged;
        DebugMsg($"{ModName} {Version}: initializing. 3x3 enabled={Enable3x3Buildings()}.");

        ResolveTypes();
        InstallAPCostHooks(harmony);
        TryApply("initialize");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (_finished) return;
        TryApply($"scene:{sceneName}");
    }

    public void OnUpdate()
    {
        if (_finished) return;

        _updateCounter++;
        if (_updateCounter < RetryEveryUpdates) return;
        _updateCounter = 0;

        TryApply("update-retry");
    }

    public void OnGUI() { }
    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
    }

    private static void ResolveTypes()
    {
        _dataTemplateLoaderType =
            FindType("Il2CppMenace.Tools.DataTemplateLoader") ??
            FindType("Menace.Tools.DataTemplateLoader");

        _entityTemplateType =
            FindType("Il2CppMenace.Tactical.EntityTemplate") ??
            FindType("Menace.Tactical.EntityTemplate");

        _actorType =
            FindType("Il2CppMenace.Tactical.Actor") ??
            FindType("Menace.Tactical.Actor");

        _movementSystemType =
            FindType("Il2CppTactical.MovementSystem") ??
            FindType("Il2CppMenace.Tactical.MovementSystem") ??
            FindType("Tactical.MovementSystem") ??
            FindType("Menace.Tactical.MovementSystem");

        if (_dataTemplateLoaderType == null)
        {
            DebugError($"{ModName}: DataTemplateLoader type not found.");
            return;
        }

        if (_entityTemplateType == null)
        {
            DebugError($"{ModName}: EntityTemplate type not found.");
            return;
        }

        _dataTemplateGetGeneric = _dataTemplateLoaderType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name == "Get" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2);

        if (_dataTemplateGetGeneric == null)
        {
            DebugError($"{ModName}: DataTemplateLoader.Get<T>(string,bool) not found.");
            return;
        }

        DebugMsg($"{ModName}: resolved {_dataTemplateLoaderType.FullName} and {_entityTemplateType.FullName}.");
    }

    private static void TryApply(string reason)
    {
        if (_finished) return;
        _attempts++;

        if (_dataTemplateLoaderType == null || _entityTemplateType == null || _dataTemplateGetGeneric == null)
        {
            ResolveTypes();
            if (_dataTemplateLoaderType == null || _entityTemplateType == null || _dataTemplateGetGeneric == null)
            {
                StopIfTimedOut();
                return;
            }
        }

        object bunker = GetTemplate("building_military_1x1_bunker") ?? GetTemplate("bunker");
        if (bunker == null)
        {
            if (_attempts == 1 || _attempts % 5 == 0)
                DebugWarning($"{ModName}: vanilla bunker template not ready yet ({reason}); retrying.");
            StopIfTimedOut();
            return;
        }

        object bunkerInsideCover = GetMember(bunker, "ProvidesCoverInside");
        if (bunkerInsideCover == null)
        {
            if (_attempts == 1 || _attempts % 5 == 0)
                DebugWarning($"{ModName}: vanilla bunker has no readable ProvidesCoverInside yet ({reason}); retrying.");
            StopIfTimedOut();
            return;
        }

        object buildingInsideCover = EnsureBuildingCoverProfile(bunkerInsideCover);
        if (buildingInsideCover == null)
        {
            if (_attempts == 1 || _attempts % 5 == 0)
                DebugWarning($"{ModName}: dedicated building inside-cover profile is not ready yet ({reason}); retrying.");
            StopIfTimedOut();
            return;
        }

        foreach (CandidateSpec candidate in Candidates)
        {
            string id = candidate.Id;
            if (_handled.Contains(id)) continue;

            if (candidate.Optional3x3 && !Enable3x3Buildings())
            {
                _handled.Add(id);
                DebugMsg($"{ModName}: 3x3 candidate disabled by Mod Settings: {id} ({candidate.Name}).");
                continue;
            }

            object template = GetTemplate(id);
            if (template == null)
            {
                if (_attempts == 1 || _attempts % 5 == 0)
                    DebugWarning($"{ModName}: template not available yet: {id}.");
                continue;
            }

            int width = ToInt(InvokeNoArg(template, "GetBlockedTilesWidth"), -1);
            int height = ToInt(InvokeNoArg(template, "GetBlockedTilesHeight"), -1);

            if (!candidate.MatchesFootprint(width, height))
            {
                _handled.Add(id);
                DebugWarning($"{ModName}: SKIP {id} ({candidate.Name}): runtime footprint is {width}x{height}, expected {candidate.Width}x{candidate.Height}.");
                continue;
            }

            int currentContainer = ToInt(GetMember(template, "ContainerType"), 0);
            if (currentContainer != 0 && currentContainer != 1)
            {
                _handled.Add(id);
                DebugWarning($"{ModName}: SKIP {id} ({candidate.Name}): already uses non-building container type {currentContainer}.");
                continue;
            }

            bool containerSet = currentContainer == 1 ||
                                TrySetEnumMember(template, "ContainerType", 1); // EntityContainerType.Building

            // Always assign the dedicated cover profile, even when an earlier pass/version
            // already changed ContainerType. This also upgrades old bunker-profile installs.
            bool coverSet = TrySetMember(template, "ProvidesCoverInside", buildingInsideCover);

            if (!containerSet || !coverSet)
            {
                DebugError($"{ModName}: failed to patch {id}: ContainerType={containerSet}, ProvidesCoverInside={coverSet}.");
                continue;
            }

            int readbackContainer = ToInt(GetMember(template, "ContainerType"), -1);
            object readbackCover = GetMember(template, "ProvidesCoverInside");

            if (readbackContainer != 1 || readbackCover == null)
            {
                DebugError($"{ModName}: patch did not stick for {id}: ContainerType={readbackContainer}, cover={(readbackCover == null ? "null" : "ok")}.");
                continue;
            }

            _handled.Add(id);
            if (candidate.ExtraAP > 0) _enabledExtraAP[id] = candidate.ExtraAP;

            string apNote = candidate.ExtraAP > 0
                ? $" entry/exit surcharge=+{candidate.ExtraAP} AP (target {VanillaContainerAP + candidate.ExtraAP} AP with vanilla infantry)."
                : string.Empty;
            DebugMsg($"{ModName}: ENABLED {id} ({candidate.Name}) footprint={width}x{height} ContainerType=Building cover={BuildingCoverProfileName}.{apNote}");
        }

        if (_handled.Count == Candidates.Length)
        {
            _finished = true;
            DebugMsg($"{ModName} {Version}: initialization complete. {_handled.Count}/{Candidates.Length} candidates resolved; {_enabledExtraAP.Count} enabled building template(s) use size-based AP surcharges; 3x3 enabled={Enable3x3Buildings()}.");
            return;
        }

        StopIfTimedOut();
    }

    private static object EnsureBuildingCoverProfile(object bunkerInsideCover)
    {
        if (_buildingInsideCover != null)
        {
            ApplyAllBuildingCoverSettings();
            return _buildingInsideCover;
        }

        try
        {
            if (bunkerInsideCover is not UnityEngine.Object source)
            {
                DebugError($"{ModName}: bunker inside-cover object is not a UnityEngine.Object ({bunkerInsideCover.GetType().FullName}).");
                return null;
            }

            object clone = CloneUnityObjectPreservingRuntimeType(source);
            if (clone == null)
            {
                DebugError($"{ModName}: UnityEngine.Object.Instantiate<T> returned null for the bunker inside-cover profile.");
                return null;
            }

            if (clone is UnityEngine.Object unityClone)
                unityClone.name = BuildingCoverProfileName;
            else
                TrySetMember(clone, "name", BuildingCoverProfileName);

            _buildingInsideCover = clone;
            ApplyAllBuildingCoverSettings();

            float baseAccuracy = ReadPercent("BuildingAccuracy", 40f);
            float effectiveAccuracy = ReadEffectiveBuildingAccuracyPercent();
            string accuracyNote = Math.Abs(baseAccuracy - effectiveAccuracy) > 0.01f
                ? $"Accuracy={effectiveAccuracy:0}% (base {baseAccuracy:0}%, StrongerCover active)"
                : $"Accuracy={baseAccuracy:0}%";
            DebugMsg($"{ModName}: created dedicated inside-cover profile '{BuildingCoverProfileName}' " +
                     $"{accuracyNote} Damage={ReadPercent("BuildingDamage", 60f):0}% " +
                     $"Suppression={ReadPercent("BuildingSuppression", 60f):0}% Concealment=+{ReadInt("BuildingConcealment", 2)}.");
            return _buildingInsideCover;
        }
        catch (Exception ex)
        {
            DebugError($"{ModName}: failed to clone dedicated building inside-cover profile: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }


    private static object CloneUnityObjectPreservingRuntimeType(UnityEngine.Object source)
    {
        if (source == null) return null;

        try
        {
            Type runtimeType = source.GetType();
            MethodInfo genericInstantiate = typeof(UnityEngine.Object)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m =>
                    m.Name == "Instantiate" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 1);

            if (genericInstantiate != null)
            {
                MethodInfo closed = genericInstantiate.MakeGenericMethod(runtimeType);
                object typedClone = closed.Invoke(null, new object[] { source });
                if (typedClone != null && runtimeType.IsInstanceOfType(typedClone))
                    return typedClone;
            }

            // Fallback for runtimes where the generic overload cannot be reflected.
            // Reject an untyped wrapper rather than assigning an incompatible object
            // to EntityTemplate.ProvidesCoverInside.
            UnityEngine.Object fallback = UnityEngine.Object.Instantiate(source);
            return fallback != null && runtimeType.IsInstanceOfType(fallback) ? fallback : null;
        }
        catch (Exception ex)
        {
            DebugError($"{ModName}: typed inside-cover clone failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void ApplyAllBuildingCoverSettings()
    {
        if (_buildingInsideCover == null) return;

        TrySetMember(_buildingInsideCover, "AccuracyMult", ReadEffectiveBuildingAccuracyPercent() / 100f);
        TrySetMember(_buildingInsideCover, "DamageMult", ReadPercent("BuildingDamage", 60f) / 100f);
        TrySetMember(_buildingInsideCover, "SuppressionMult", ReadPercent("BuildingSuppression", 60f) / 100f);
        TrySetMember(_buildingInsideCover, "Concealment", ReadInt("BuildingConcealment", 2));
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (string.Equals(modName, ModName, StringComparison.Ordinal) &&
            string.Equals(key, "DebugLogging", StringComparison.Ordinal))
        {
            try { _debugLogging = value != null && Convert.ToBoolean(value); }
            catch { _debugLogging = false; }

            if (_debugLogging)
                DebugMsg($"{ModName}: debug logging enabled.");
            return;
        }

        if (_buildingInsideCover == null) return;

        if (string.Equals(modName, ModName, StringComparison.Ordinal))
        {
            bool changed = key switch
            {
                "BuildingAccuracy" => TrySetMember(_buildingInsideCover, "AccuracyMult", ReadEffectiveBuildingAccuracyPercent() / 100f),
                "BuildingDamage" => TrySetMember(_buildingInsideCover, "DamageMult", ReadPercent("BuildingDamage", 60f) / 100f),
                "BuildingSuppression" => TrySetMember(_buildingInsideCover, "SuppressionMult", ReadPercent("BuildingSuppression", 60f) / 100f),
                "BuildingConcealment" => TrySetMember(_buildingInsideCover, "Concealment", ReadInt("BuildingConcealment", 2)),
                _ => false
            };

            if (changed)
                DebugMsg($"{ModName}: updated {key} on '{BuildingCoverProfileName}'.");
            return;
        }

        // Optional compatibility path. StrongerCover 2.2 exposes BuildingCover;
        // older StrongerCover builds have no such setting, so the 30% fallback still
        // gives garrisoned buildings the expected stronger protection when detected.
        if (string.Equals(modName, StrongerCoverSettingsGroup, StringComparison.Ordinal) &&
            key is ("EnableGameplay" or "BuildingCover"))
        {
            bool changed = TrySetMember(_buildingInsideCover, "AccuracyMult", ReadEffectiveBuildingAccuracyPercent() / 100f);
            if (changed)
                DebugMsg($"{ModName}: synchronized building AccuracyMult with StrongerCover ({ReadEffectiveBuildingAccuracyPercent():0}%).");
        }
    }

    private static float ReadEffectiveBuildingAccuracyPercent()
    {
        float baseValue = ReadPercent("BuildingAccuracy", 40f);
        if (!IsStrongerCoverLoaded()) return baseValue;
        if (!ReadExternalBool(StrongerCoverSettingsGroup, "EnableGameplay", true)) return baseValue;
        return ReadExternalPercent(StrongerCoverSettingsGroup, "BuildingCover", 30f);
    }

    private static bool IsStrongerCoverLoaded()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (string.Equals(assembly.GetName().Name, "StrongerCover", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
        }
        return false;
    }

    private static float ReadExternalPercent(string group, string key, float fallback)
    {
        try
        {
            object value = ModSettings.Get(group, key);
            return value == null ? fallback : Math.Clamp(Convert.ToSingle(value), 5f, 100f);
        }
        catch { return fallback; }
    }

    private static bool ReadExternalBool(string group, string key, bool fallback)
    {
        try
        {
            object value = ModSettings.Get(group, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch { return fallback; }
    }

    private static float ReadPercent(string key, float fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Math.Clamp(Convert.ToSingle(value), 5f, 100f);
        }
        catch { return fallback; }
    }

    private static int ReadInt(string key, int fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Math.Clamp((int)Math.Round(Convert.ToDouble(value)), 0, 10);
        }
        catch { return fallback; }
    }

    private static void InstallAPCostHooks(HarmonyLib.Harmony harmony)
    {
        try
        {
            if (_actorType == null || _movementSystemType == null)
            {
                DebugWarning($"{ModName}: AP surcharge hooks not installed because Actor or MovementSystem type was not resolved.");
                return;
            }

            MethodInfo getPath = _movementSystemType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "GetPath" && m.GetParameters().Length == 4);

            if (getPath != null)
            {
                harmony.Patch(
                    getPath,
                    prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MovementGetPathPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                    postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(APCostOverridePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                DebugMsg($"{ModName}: hooked {_movementSystemType.FullName}.GetPath for size-based AP surcharge previews/pathing.");
            }
            else
            {
                DebugWarning($"{ModName}: could not find {_movementSystemType.FullName}.GetPath/4; size-based AP preview surcharge will be unavailable.");
            }

            MethodInfo moveTo = _actorType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "MoveTo" && m.GetParameters().Length == 3);

            if (moveTo != null)
            {
                harmony.Patch(
                    moveTo,
                    prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(ActorMoveToPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                    postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(APCostOverridePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                DebugMsg($"{ModName}: hooked {_actorType.FullName}.MoveTo for size-based AP surcharge execution.");
            }
            else
            {
                DebugWarning($"{ModName}: could not find {_actorType.FullName}.MoveTo/3; size-based AP execution surcharge will be unavailable.");
            }
        }
        catch (Exception ex)
        {
            DebugError($"{ModName}: failed to install size-based AP surcharge hooks: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MovementGetPathPrefix(object[] __args, out APCostOverrideState __state)
    {
        __state = null;
        try
        {
            if (__args == null || __args.Length < 2) return;

            object actor = __args[0];
            object destinationTile = __args[1];
            if (actor == null) return;

            int enterExtra = GetManagedExtraAP(GetEntityFromTile(destinationTile));
            int leaveExtra = GetManagedExtraAP(InvokeNoArg(actor, "GetContainerEntity"));

            if (enterExtra <= 0 && leaveExtra <= 0) return;
            __state = ApplyTemporaryAPCost(actor, enterExtra, leaveExtra);
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: GetPath AP surcharge prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ActorMoveToPrefix(object __instance, object[] __args, out APCostOverrideState __state)
    {
        __state = null;
        try
        {
            if (__instance == null || __args == null || __args.Length < 2) return;

            object destinationTile = __args[0];
            int action = ToInt(__args[1], 0);

            bool actionEnter = (action & 1) != 0; // MovementAction.Enter
            bool actionLeave = (action & 2) != 0; // MovementAction.Leave

            int enterExtra = actionEnter ? GetManagedExtraAP(GetEntityFromTile(destinationTile)) : 0;
            int leaveExtra = actionLeave ? GetManagedExtraAP(InvokeNoArg(__instance, "GetContainerEntity")) : 0;

            if (enterExtra <= 0 && leaveExtra <= 0) return;

            __state = ApplyTemporaryAPCost(__instance, enterExtra, leaveExtra);
            if (__state != null)
            {
                string bits = enterExtra > 0 && leaveExtra > 0
                    ? $"leave +{leaveExtra} AP, enter +{enterExtra} AP"
                    : enterExtra > 0
                        ? $"enter +{enterExtra} AP"
                        : $"leave +{leaveExtra} AP";
                DebugMsg($"{ModName}: applying size-based garrison surcharge: {bits}.");
            }
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: MoveTo AP surcharge prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static APCostOverrideState ApplyTemporaryAPCost(object actor, int enterExtra, int leaveExtra)
    {
        // Actor.MoveTo may call path-cost code internally. If an outer hook has
        // already raised the actor's costs, an inner hook must not add the surcharge again.
        if (_apOverrideDepth > 0) return null;

        object properties = InvokeNoArg(actor, "GetCurrentProperties");
        if (properties == null) return null;

        int originalEnter = ToInt(GetMember(properties, "APEnterCost"), VanillaContainerAP);
        int originalLeave = ToInt(GetMember(properties, "APLeaveCost"), VanillaContainerAP);
        int targetEnterCost = originalEnter + Math.Max(0, enterExtra);
        int targetLeaveCost = originalLeave + Math.Max(0, leaveExtra);

        APCostOverrideState state = new APCostOverrideState
        {
            Properties = properties,
            OriginalEnter = originalEnter,
            OriginalLeave = originalLeave
        };

        if (enterExtra > 0)
        {
            state.ChangedEnter = TrySetMember(properties, "APEnterCost", targetEnterCost);
        }

        if (leaveExtra > 0)
        {
            state.ChangedLeave = TrySetMember(properties, "APLeaveCost", targetLeaveCost);
        }

        if (!state.ChangedEnter && !state.ChangedLeave)
            return null;

        _apOverrideDepth++;
        return state;
    }

    private static void APCostOverridePostfix(APCostOverrideState __state)
    {
        if (__state == null || __state.Properties == null) return;

        try
        {
            if (__state.ChangedEnter)
                TrySetMember(__state.Properties, "APEnterCost", __state.OriginalEnter);
            if (__state.ChangedLeave)
                TrySetMember(__state.Properties, "APLeaveCost", __state.OriginalLeave);
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: failed to restore temporary AP costs: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (_apOverrideDepth > 0) _apOverrideDepth--;
        }
    }

    private static object GetEntityFromTile(object tile)
    {
        return tile == null ? null : InvokeNoArg(tile, "GetEntity");
    }

    private static int GetManagedExtraAP(object entity)
    {
        if (entity == null || _enabledExtraAP.Count == 0) return 0;

        object template = InvokeNoArg(entity, "GetTemplate");
        if (template == null) return 0;

        string id = InvokeNoArg(template, "GetID") as string;
        if (string.IsNullOrEmpty(id)) id = GetMember(template, "m_ID") as string;
        if (string.IsNullOrEmpty(id)) return 0;

        return _enabledExtraAP.TryGetValue(id, out int extraAP) ? extraAP : 0;
    }

    private static bool Enable3x3Buildings() => ReadBool("Enable3x3Buildings", false);

    private static void DebugMsg(string message)
    {
        if (_debugLogging) _log?.Msg(message);
    }

    private static void DebugWarning(string message)
    {
        if (_debugLogging) _log?.Warning(message);
    }

    private static void DebugError(string message)
    {
        if (_debugLogging) _log?.Error(message);
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch { return fallback; }
    }

    private static object GetTemplate(string id)
    {
        try
        {
            MethodInfo closed = _dataTemplateGetGeneric.MakeGenericMethod(_entityTemplateType);
            return closed.Invoke(null, new object[] { id, false });
        }
        catch (TargetInvocationException ex)
        {
            Exception inner = ex.InnerException ?? ex;
            if (_attempts == 1 || _attempts % 5 == 0)
                DebugWarning($"{ModName}: GetTemplate('{id}') failed: {inner.GetType().Name}: {inner.Message}");
            return null;
        }
        catch (Exception ex)
        {
            if (_attempts == 1 || _attempts % 5 == 0)
                DebugWarning($"{ModName}: GetTemplate('{id}') failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string FriendlyName(string id)
    {
        return CandidateById.TryGetValue(id, out CandidateSpec value) ? value.Name : id;
    }

    private static void StopIfTimedOut()
    {
        if (_attempts < MaxAttempts) return;

        _finished = true;
        foreach (CandidateSpec candidate in Candidates)
        {
            if (!_handled.Contains(candidate.Id))
                DebugWarning($"{ModName}: unresolved after {MaxAttempts} attempts: {candidate.Id} ({candidate.Name}).");
        }

        DebugWarning($"{ModName}: stopped retrying. Send Latest.log if any candidate stayed unresolved.");
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            catch { }
        }

        return null;
    }

    private static object InvokeNoArg(object obj, string methodName)
    {
        if (obj == null) return null;

        try
        {
            MethodInfo method = obj.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            return method?.Invoke(obj, null);
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: {obj.GetType().Name}.{methodName}() failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static object GetMember(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    return property.GetValue(obj);
            }
            catch { }

            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(obj);
            }
            catch { }
        }

        return null;
    }

    private static bool TrySetMember(object obj, string name, object value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(obj, value);
                    return true;
                }
            }
            catch { }

            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(obj, value);
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static bool TrySetEnumMember(object obj, string name, int numericValue)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanWrite)
                {
                    object converted = ConvertNumericValue(numericValue, property.PropertyType);
                    property.SetValue(obj, converted);
                    return true;
                }
            }
            catch { }

            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null && !field.IsInitOnly)
                {
                    object converted = ConvertNumericValue(numericValue, field.FieldType);
                    field.SetValue(obj, converted);
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static object ConvertNumericValue(int value, Type destinationType)
    {
        if (destinationType.IsEnum)
            return Enum.ToObject(destinationType, value);

        return Convert.ChangeType(value, destinationType);
    }

    private static int ToInt(object value, int fallback)
    {
        if (value == null) return fallback;
        try { return Convert.ToInt32(value); }
        catch { return fallback; }
    }
}

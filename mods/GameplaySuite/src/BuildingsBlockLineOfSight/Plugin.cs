using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MenaceModSettings;
using MenaceModSuite.Shared;

namespace BuildingsBlockLineOfSight;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/BuildingsBlockLineOfSight/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string ModName = "Buildings Block Line of Sight";
    private const string Version = "0.2.2";
    private const int RetryEveryUpdates = 120;
    private const int MaxAttempts = 30;

    // Explicit scope combines the original enclosed-building proof of concept
    // with every vanilla structure that is currently enterable as a Building or Tower,
    // plus the optional Garrisonable Buildings 3x3 candidates. Runtime container-type
    // detection below also catches compatible buildings made enterable by other mods.
    private static readonly HashSet<string> TargetTemplateIds = new(StringComparer.OrdinalIgnoreCase)
    {
        // Original enclosed-building scope / Garrisonable Buildings default candidates.
        "building_civilian_shelter_01",
        "building_civilian_water_treatment_building_01",
        "building_pirate_shelter_01",
        "building_civilian_shelter_1x2_01",
        "building_civilian_water_storage_plant_1x2",
        "building_pirate_makeshift_storage_01_1x2",
        "building_civilian_water_storage_plant_1x3",
        "building_civilian_water_storage_plant_2x2",
        "building_civilian_water_storage_plant_2x3",

        // Optional Garrisonable Buildings 3x3 candidates. These already block LoS in
        // current vanilla data, so the mod only verifies them and leaves their matrix intact.
        "building_civilian_greenhouse_01",
        "building_civilian_storage_01",

        // Large stacked/corner cargo-container obstacles. The singular 1x1 Cargo Container
        // is intentionally excluded because its low/small profile should not fully block LoS.
        "object_civilian_stacked_containers_01",
        "obstacle_pirate_container_02_corner",

        // Vanilla enterable Building containers.
        "bunker",
        "building_military_1x1_bunker",
        "building_military_1x1_bunker_corner",
        "building_military_1x1_bunker_corner_machinegun",
        "building_military_1x1_bunker_straight_wall",
        "building_military_1x1_bunker_straight_wall_rogue_infantry",

        // Vanilla enterable Tower container.
        "building_military_1x1_watchtower"
    };

    private static readonly HashSet<string> Handled = new(StringComparer.OrdinalIgnoreCase);

    private static ModLog _log;
    private static Type _dataTemplateLoaderType;
    private static Type _entityTemplateType;
    private static Type _structureType;
    private static MethodInfo _dataTemplateGetGeneric;
    private static int _updateCounter;
    private static int _attempts;
    private static bool _finished;
    private static bool _debugLogging;

    public void Initialize(ModLog logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Diagnostics");
            settings.AddToggle(
                "DebugLogging",
                "Enable diagnostic logging (OFF by default)",
                false);
        });

        _debugLogging = ReadBool("DebugLogging", false);
        ModSettings.OnSettingChanged += OnSettingChanged;

        ResolveTypes();
        InstallStructureCreateHook(harmony);
        TryApplyTemplates("initialize");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (!_finished)
            TryApplyTemplates($"scene:{sceneName}");
    }

    public void OnUpdate()
    {
        if (_finished) return;

        _updateCounter++;
        if (_updateCounter < RetryEveryUpdates) return;
        _updateCounter = 0;

        TryApplyTemplates("update-retry");
    }

    public void OnGUI() { }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
    }

    private static void ResolveTypes()
    {
        _dataTemplateLoaderType ??=
            FindType("Il2CppMenace.Tools.DataTemplateLoader") ??
            FindType("Menace.Tools.DataTemplateLoader");

        _entityTemplateType ??=
            FindType("Il2CppMenace.Tactical.EntityTemplate") ??
            FindType("Menace.Tactical.EntityTemplate");

        _structureType ??=
            FindType("Il2CppMenace.Tactical.Structure") ??
            FindType("Menace.Tactical.Structure");

        if (_dataTemplateLoaderType != null && _dataTemplateGetGeneric == null)
        {
            _dataTemplateGetGeneric = _dataTemplateLoaderType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "Get" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 2);
        }
    }

    private static void InstallStructureCreateHook(HarmonyLib.Harmony harmony)
    {
        try
        {
            if (_structureType == null)
            {
                DebugWarning($"{ModName}: Structure type was not resolved; relying on template initialization only.");
                return;
            }

            MethodInfo create = _structureType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 4);

            if (create == null)
            {
                DebugWarning($"{ModName}: Structure.Create/4 was not found; relying on template initialization only.");
                return;
            }

            MethodInfo prefix = typeof(Plugin).GetMethod(
                nameof(StructureCreatePrefix),
                BindingFlags.Static | BindingFlags.NonPublic);

            harmony.Patch(create, prefix: new HarmonyMethod(prefix));
            DebugMsg($"{ModName}: hooked {_structureType.FullName}.Create/4.");
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: failed to install Structure.Create hook: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Fallback/guarantee: whenever a Structure is created, patch explicit targets and
    // any runtime template that is currently enterable as a Building or Tower. This makes
    // the mod compatible with Garrisonable Buildings even though that mod loads later.
    private static void StructureCreatePrefix(object[] __args)
    {
        try
        {
            if (__args == null || __args.Length == 0 || __args[0] == null) return;
            TryEnableBlocking(__args[0], "Structure.Create");
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: Structure.Create prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TryApplyTemplates(string reason)
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

        foreach (string id in TargetTemplateIds)
        {
            if (Handled.Contains(id)) continue;

            object template = GetTemplate(id);
            if (template == null) continue;

            if (TryEnableBlocking(template, reason))
                Handled.Add(id);
        }

        if (Handled.Count == TargetTemplateIds.Count)
        {
            _finished = true;
            DebugMsg($"{ModName} {Version}: {Handled.Count}/{TargetTemplateIds.Count} target building templates patched.");
            return;
        }

        StopIfTimedOut();
    }

    private static bool TryEnableBlocking(object template, string reason)
    {
        string id = GetTemplateId(template);
        if (string.IsNullOrEmpty(id))
            return false;

        // EntityContainerType: Building=1, Tower=4. Do not include Vehicle=2 or Forest=3.
        int containerType = ToInt(GetMember(template, "ContainerType"), 0);
        bool isEnterableStructure = containerType == 1 || containerType == 4;
        bool isExplicitTarget = TargetTemplateIds.Contains(id);
        if (!isExplicitTarget && !isEnterableStructure)
            return false;

        // Preserve vanilla/custom LoS geometry if the template already blocks correctly.
        // Some bunker variants and the optional 3x3 buildings already have a dedicated
        // BlockedLineOfSight matrix that may differ from BlockedTiles.
        bool alreadyBlocks = ToBool(GetMember(template, "IsBlockingLineOfSight"), false);
        object existingLosMatrix = GetMember(template, "BlockedLineOfSight");
        if (alreadyBlocks && existingLosMatrix != null)
        {
            DebugMsg($"{ModName}: {id} already blocks LoS; leaving existing matrix unchanged ({reason}).");
            return true;
        }

        object blockedTiles = GetMember(template, "BlockedTiles");
        if (blockedTiles == null)
        {
            DebugWarning($"{ModName}: {id} has no readable BlockedTiles yet ({reason}).");
            return false;
        }

        bool losFlagSet = TrySetMember(template, "IsBlockingLineOfSight", true);
        bool losMatrixSet = TrySetMember(template, "BlockedLineOfSight", blockedTiles);

        if (!losFlagSet || !losMatrixSet)
        {
            DebugWarning($"{ModName}: failed to patch {id}: flag={losFlagSet}, matrix={losMatrixSet} ({reason}).");
            return false;
        }

        bool readbackFlag = ToBool(GetMember(template, "IsBlockingLineOfSight"), false);
        object readbackMatrix = GetMember(template, "BlockedLineOfSight");
        if (!readbackFlag || readbackMatrix == null)
        {
            DebugWarning($"{ModName}: patch did not stick for {id} ({reason}).");
            return false;
        }

        DebugMsg($"{ModName}: enabled native LoS blocking for {id} (ContainerType={containerType}, {reason}).");
        return true;
    }

    private static string GetTemplateId(object template)
    {
        object value = InvokeNoArg(template, "GetID");
        if (value is string methodId && !string.IsNullOrEmpty(methodId))
            return methodId;

        return GetMember(template, "m_ID") as string;
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
            DebugWarning($"{ModName}: GetTemplate('{id}') failed: {inner.GetType().Name}: {inner.Message}");
            return null;
        }
        catch (Exception ex)
        {
            DebugWarning($"{ModName}: GetTemplate('{id}') failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void StopIfTimedOut()
    {
        if (_attempts < MaxAttempts) return;

        _finished = true;
        DebugWarning($"{ModName}: stopped retrying after {MaxAttempts} attempts; resolved {Handled.Count}/{TargetTemplateIds.Count} templates.");
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, ModName, StringComparison.Ordinal) ||
            !string.Equals(key, "DebugLogging", StringComparison.Ordinal))
            return;

        try { _debugLogging = value != null && Convert.ToBoolean(value); }
        catch { _debugLogging = false; }
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch
        {
            return fallback;
        }
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
        catch
        {
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

    private static int ToInt(object value, int fallback)
    {
        if (value == null) return fallback;
        try { return Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static bool ToBool(object value, bool fallback)
    {
        if (value == null) return fallback;
        try { return Convert.ToBoolean(value); }
        catch { return fallback; }
    }

    private static void DebugMsg(string message)
    {
        if (_debugLogging) _log?.Msg(message);
    }

    private static void DebugWarning(string message)
    {
        if (_debugLogging) _log?.Warning(message);
    }
}

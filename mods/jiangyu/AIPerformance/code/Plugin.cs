using System;
using System.Reflection;
using MenaceModSettings;

namespace AIPerformance;

// Owns AI planning/recovery. No map generation, native byte edits or per-frame polling.
public sealed partial class Plugin
{
    private const string ModName = "AI Performance";
    private const int ZoneReferenceSize = 64;
    private static int TargetSize = 64;
    private static PerformanceLog _log;

    public void Initialize(Action<string> info, Action<string> warn, Action<string> error,
        HarmonyLib.Harmony harmony)
    {
        _log = new PerformanceLog(info, warn, error);
        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("AI performance (restart required)");
            settings.AddToggle("SimplifyCombatAIPlanning", "Simplified attack planning (includes 64x64 maps)", true);
            settings.AddToggle("UseLocalTacticalRouting", "Local tactical routing (limit destination search)", false);
            settings.AddSlider("LocalNearTileLimit", "Nearby destinations inspected", 12f, 64f, 20f);
            settings.AddSlider("LocalCullTileDistance", "Cull distance (tiles)", 10f, 20f, 12f);
            settings.AddToggle("RecoverStalledAdvance", "Recover advance after two idle rounds (maps above 64x64)", true);
            settings.AddToggle("DiagnoseAdvanceRecovery", "Log bounded advance recovery checks", false);
            settings.AddHeader("Experimental AI behavior (restart required)");
            settings.AddToggle("ConvergeOnLastKnownContacts", "Unassigned infantry approach recent contacts (0.7.18)", false);
            settings.AddToggle("DiagnoseContactConvergence", "Log bounded contact approaches", false);
            settings.AddToggle("PrioritizeMissionCivilians", "Prioritize visible civilian mission targets (0.7.18)", true);
            settings.AddToggle("OffensiveCombatPlanning", "Bounded firing positions and attack-first choices (pirate/Rogue)", true);
            settings.AddToggle("CombinedArmsSupport", "Threat-driven vehicle, fire and smoke support (0.7.18)", false);
            settings.AddToggle("DiagnoseCombinedArmsSupport", "Log bounded combined-arms planning diagnostics", false);
        });
        PatchAiTaskLifetime(harmony);
        PatchAdvanceRecovery(harmony);
        PatchContactConvergence(harmony);
        PatchMissionCivilianPriority(harmony);
        PatchCombinedArms(harmony);
        PatchOffensivePlanning(harmony);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
        {
            BindSupportSmoke();
            // Planning covers native and enlarged maps; recovery checks the actual map.
            ApplySimplifiedCombatPlanning();
            ApplyLocalTacticalRouting();
        }
        else if (string.Equals(sceneName, "Strategy", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "Title", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "MissionPreparation", StringComparison.OrdinalIgnoreCase))
        {
            RestoreSimplifiedCombatPlanning();
            RestoreLocalTacticalRouting();
            AdvanceRecoveryReset();
            ContactConvergenceReset();
            OffensivePlanningReset();
            CombinedArmsReset();
        }
    }

    public void OnUnload()
    {
        _contactConvergenceEnabled = false;
        _missionCivilianPriorityEnabled = false;
        _offensivePlanningEnabled = false; OffensivePlanningReset();
        _combinedArmsEnabled = false; CombinedArmsReset();
        RestoreSimplifiedCombatPlanning(); RestoreLocalTacticalRouting(); AdvanceRecoveryReset(); ContactConvergenceReset();
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try { return ModSettings.Get(ModName, key) is object value ? Convert.ToBoolean(value) : fallback; }
        catch { return fallback; }
    }

    private static float ReadFloat(string key, float fallback)
    {
        try { return ModSettings.Get(ModName, key) is object value ? Convert.ToSingle(value) : fallback; }
        catch { return fallback; }
    }

    private static int ToInt(object value, int fallback)
    {
        try { return value == null ? fallback : Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null) return type;
        }
        return null;
    }

    private static object GetMember(object obj, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type type = obj?.GetType(); type != null; type = type.BaseType)
        {
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property?.CanRead == true && property.GetIndexParameters().Length == 0)
                    return property.GetValue(obj);
            }
            catch { }
            try { if (type.GetField(name, flags) is FieldInfo field) return field.GetValue(obj); }
            catch { }
        }
        return null;
    }

    private static bool TrySetMember(object obj, string name, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            PropertyInfo property = obj?.GetType().GetProperty(name, flags);
            if (property?.CanWrite == true) { property.SetValue(obj, value); return true; }
        }
        catch { }
        try
        {
            FieldInfo field = obj?.GetType().GetField(name, flags);
            if (field != null && !field.IsInitOnly) { field.SetValue(obj, value); return true; }
        }
        catch { }
        return false;
    }

    private sealed class PerformanceLog
    {
        private readonly Action<string> _info, _warn, _error;
        internal PerformanceLog(Action<string> info, Action<string> warn, Action<string> error)
            => (_info, _warn, _error) = (info, warn, error);
        internal void Msg(string message) => _info(message);
        internal void Warning(string message) => _warn(message);
        internal void Error(string message) => _error(message);
    }
}
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("3.1.9.0")]
[assembly: AssemblyFileVersion("3.1.9.0")]

namespace VisibilityOverhaul;

public sealed class Plugin : IModpackPlugin
{
    internal const string SettingsGroup = "Visibility Overhaul";
    internal const string Version = "3.1.9";

    private MelonLogger.Instance? _logger;
    private float _nextSettingsRefresh;
    private string _lastSettingsSummary = string.Empty;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        RegisterSettings();
        RefreshSettings(force: true);

        int applied = 0;
        applied += TryPatch(
            harmony,
            AccessTools.Method(typeof(SkillContainer), nameof(SkillContainer.Update)),
            prefix: nameof(Patches.SkillContainer_Update_Prefix),
            postfix: null,
            label: "SkillContainer.Update");

        applied += TryPatch(
            harmony,
            AccessTools.Method(typeof(Skill), nameof(Skill.OnUpdate), new[] { typeof(EntityProperties) }),
            prefix: null,
            postfix: nameof(Patches.Skill_OnUpdate_Postfix),
            label: "Skill.OnUpdate(EntityProperties)");

        DevConsole.RegisterPanel("Visibility Overhaul", DrawDebug);
        LogDebug($"Visibility Overhaul v{Version} initialized. {applied}/2 runtime hooks applied. Vision now participates in MENACE's native EntityProperties rebuild; no direct map-visibility hooks are used.");
        if (applied < 2)
            LogDebug("One or more hooks could not be resolved. The remaining features will continue to run where possible.", error: true);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        Patches.ResetRuntimeState();

        if (RuntimeSettings.DebugLogging)
            LogDebug($"Scene loaded: {sceneName} (build {buildIndex}). Runtime counters reset.");
    }

    public void OnUpdate()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _nextSettingsRefresh)
            return;

        _nextSettingsRefresh = now + 0.25f;
        RefreshSettings(force: false);
    }

    public void OnGUI() { }

    private void RegisterSettings()
    {
        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader($"Visibility Overhaul v{Version}");
            s.AddToggle("EnableGameplay", "Enable Visibility Overhaul", true);

            s.AddHeader("Vision");
            s.AddSlider("VisionBonus", "Flat vision bonus", 0f, 30f, 10f);

            s.AddHeader("Infantry concealment");
            s.AddToggle("EnableMovementConcealment", "Movement-based infantry concealment", true);
            s.AddSlider("StationaryConcealmentBonus", "Stationary concealment bonus", 0f, 30f, 10f);
            s.AddSlider("ConcealmentLossPerTile", "Concealment lost per tile moved", 0f, 3f, 1f);
            s.AddSlider("MaxBelowVanillaPenalty", "Maximum concealment penalty below vanilla", 0f, 20f, 10f);

            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });
    }

    private void RefreshSettings(bool force)
    {
        bool enabled = Setting("EnableGameplay", true);
        int visionBonus = SliderInt("VisionBonus", 10, 0, 30);
        bool movementConcealment = Setting("EnableMovementConcealment", true);
        int stationaryBonus = SliderInt("StationaryConcealmentBonus", 10, 0, 30);
        int lossPerTile = SliderInt("ConcealmentLossPerTile", 1, 0, 3);
        int maxPenalty = SliderInt("MaxBelowVanillaPenalty", 10, 0, 20);
        bool debug = Setting("DebugLogging", false);

        RuntimeSettings.Configure(
            enabled,
            visionBonus,
            movementConcealment,
            stationaryBonus,
            lossPerTile,
            maxPenalty,
            debug);

        string summary = RuntimeSettings.Summary;
        bool changed = !string.Equals(summary, _lastSettingsSummary, StringComparison.Ordinal);

        if (force || (debug && changed))
            LogDebug("Settings: " + summary);

        _lastSettingsSummary = summary;
    }

    private int TryPatch(global::HarmonyLib.Harmony harmony, MethodBase? original, string? prefix, string? postfix, string label)
    {
        if (original == null)
        {
            LogDebug($"Could not resolve {label}.", error: true);
            return 0;
        }

        try
        {
            HarmonyMethod? pre = prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix);
            HarmonyMethod? post = postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix);
            harmony.Patch(original, prefix: pre, postfix: post);
            LogDebug($"Patched {label}.");
            return 1;
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to patch {label}: {ex}", error: true);
            return 0;
        }
    }

    private T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private int SliderInt(string key, int fallback, int min, int max)
    {
        float raw = Setting(key, (float)fallback);
        return Math.Clamp((int)Math.Round(raw), min, max);
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

        Line($"Visibility Overhaul {Version}");
        Line("Enabled: " + (RuntimeSettings.Enabled ? "yes" : "no"));
        Line("Vision bonus: +" + RuntimeSettings.VisionBonus);
        Line("Vision integration: native EntityProperties rebuild");
        Line("Movement concealment: " + (RuntimeSettings.MovementConcealmentEnabled ? "on" : "off"));
        Line($"Concealment: +{RuntimeSettings.StationaryConcealmentBonus} stationary, -{RuntimeSettings.ConcealmentLossPerTile}/tile, floor -{RuntimeSettings.MaxBelowVanillaPenalty}");
        Line("Vision property rebuilds adjusted: " + Patches.VisionAdjustments);
        Line("Concealment recalculations adjusted: " + Patches.ConcealmentAdjustments);
        Line("Last movement modifier: " + Patches.LastConcealmentModifier);
    }

    private void LogDebug(string message, bool error = false)
    {
        if (_logger == null || !RuntimeSettings.DebugLogging) return;
        if (error) _logger.Error("[VisibilityOverhaul] " + message);
        else _logger.Msg("[VisibilityOverhaul] " + message);
    }
}

internal static class RuntimeSettings
{
    public static bool Enabled { get; private set; } = true;
    public static int VisionBonus { get; private set; } = 10;
    public static bool MovementConcealmentEnabled { get; private set; } = true;
    public static int StationaryConcealmentBonus { get; private set; } = 10;
    public static int ConcealmentLossPerTile { get; private set; } = 1;
    public static int MaxBelowVanillaPenalty { get; private set; } = 10;
    public static bool DebugLogging { get; private set; }

    public static string Summary =>
        $"enabled={Enabled}, vision=+{VisionBonus}, " +
        $"movementConcealment={MovementConcealmentEnabled}, stationary=+{StationaryConcealmentBonus}, " +
        $"lossPerTile={ConcealmentLossPerTile}, maxBelowVanilla=-{MaxBelowVanillaPenalty}, debug={DebugLogging}";

    public static void Configure(
        bool enabled,
        int visionBonus,
        bool movementConcealmentEnabled,
        int stationaryConcealmentBonus,
        int concealmentLossPerTile,
        int maxBelowVanillaPenalty,
        bool debugLogging)
    {
        Enabled = enabled;
        VisionBonus = Math.Clamp(visionBonus, 0, 30);
        MovementConcealmentEnabled = movementConcealmentEnabled;
        StationaryConcealmentBonus = Math.Clamp(stationaryConcealmentBonus, 0, 30);
        ConcealmentLossPerTile = Math.Clamp(concealmentLossPerTile, 0, 3);
        MaxBelowVanillaPenalty = Math.Clamp(maxBelowVanillaPenalty, 0, 20);
        DebugLogging = debugLogging;
    }
}

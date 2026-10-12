using System.Reflection;
using Il2CppMenace.Tactical;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using MenaceModSuite.Shared;
using UnityEngine;
using Path = global::System.IO.Path;

[assembly: AssemblyVersion("0.32.3.3")]
[assembly: AssemblyFileVersion("0.32.3.3")]
[assembly: AssemblyInformationalVersion("0.32.3.3")]

namespace MenaceOverwatch;

public sealed class Plugin : IModpackPlugin
{
    private const string SettingsGroup = "Overwatch";
    private const string Version = "0.32.3.3";
    private Runtime? _runtime;
    private global::HarmonyLib.Harmony? _harmony;
    private MelonLogger.Instance? _logger;
    private float _bindAt;
    private bool _attempted;
    private Rect _hudRect;
    private GUIStyle? _reason;
    private bool _updateErrorLogged;
    private bool _guiErrorLogged;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;
        TacticalHudVisibility.TryInstall("menace.overwatch.cinematic-hud",
            message => _logger?.Msg("WARNING: " + message));
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "UserData", "Overwatch"));
        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("EnableGameplay", "Enable Overwatch", true);
            s.AddToggle("TriggerOnEnemyActions", "Trigger on enemy attacks", true);
            s.AddSlider("PlayerHitChance", "Player minimum hit chance", 20f, 95f, 60f);
            s.AddSlider("OverwatchAimPenalty", "Overwatch aim penalty", 0f, 40f, 10f);
            s.AddToggle("EnableVehicleOverwatch", "Enable vehicle Overwatch", true);
            s.AddToggle("EnableAIOverwatch", "AI uses Overwatch", true);
            s.AddToggle("EnableAIVehicleOverwatch", "AI vehicles use Overwatch", false);
            s.AddSlider("AIHitChance", "AI minimum hit chance", 20f, 95f, 55f);
            s.AddHeader("Interface");
            s.AddToggle("ShowHUD", "Show tactical button", true);
            s.AddToggle("ShowUnavailableHUD", "Show panel when selected unit cannot Overwatch", true);
            s.AddDropdown("HUDPosition", "Button position", new[] { "Bottom Center", "Bottom Left", "Top Left", "Top Right" }, "Bottom Center");
            s.AddSlider("HUDScale", "Button scale", 0.75f, 1.5f, 1.0f);
            s.AddToggle("StackWithCoordinatedFire", "Stack above Coordinated Fire", true);
            // Keep the original ShowWorldMarkers key for settings compatibility, but the
            // only remaining marker implementation is the native healthbar reticle row.
            s.AddToggle("ShowWorldMarkers", "Show Overwatch indicators", true);
            s.AddToggle("HighContrastIndicators", "High contrast Overwatch indicators", true);
            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });
        DevConsole.RegisterPanel("Overwatch", DrawDebug);
        _bindAt = Time.realtimeSinceStartup + 2f;
        DebugLog($"{Version}: initialized. Overwatch provocation is whitelisted to movement completion and native attack skills only; utility/transport actions such as passenger ejection cannot provoke reaction fire. Indirect-fire weapons remain excluded.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        TacticalHudVisibility.OnSceneLoaded();
        _updateErrorLogged = false;
        _guiErrorLogged = false;
        _runtime?.SceneChanged();
        if (_runtime == null) { _attempted = false; _bindAt = Time.realtimeSinceStartup + 2f; }
    }

    public void OnUpdate()
    {
        if (!Setting("EnableGameplay", true)) { _runtime?.Dispose(); _runtime = null; return; }
        bool cf = IsCoordinatedFireInstalled();
        bool alphaStrike = IsAlphaStrikeInstalled();
        if (!_attempted && Time.realtimeSinceStartup >= _bindAt)
        {
            _attempted = true;
            try
            {
                if (_harmony == null) throw new InvalidOperationException("Harmony unavailable.");
                _runtime = new Runtime(_harmony, DebugLog, WarningLog, ErrorLog, Setting("DebugLogging", false), Setting("EnableAIOverwatch", true),
                    Setting("TriggerOnEnemyActions", true), Setting("EnableVehicleOverwatch", true), Setting("EnableAIVehicleOverwatch", false),
                    Setting("ShowWorldMarkers", true), Setting("HighContrastIndicators", true),
                    Threshold("PlayerHitChance", 60), Threshold("AIHitChance", 55),
                    Penalty(), cf, alphaStrike);
            }
            catch (Exception e) { ErrorLog("Runtime bind failed: " + e); }
        }
        _runtime?.Configure(Setting("EnableAIOverwatch", true), Setting("TriggerOnEnemyActions", true), Setting("EnableVehicleOverwatch", true),
            Setting("EnableAIVehicleOverwatch", false), Setting("ShowWorldMarkers", true), Setting("HighContrastIndicators", true),
            Threshold("PlayerHitChance", 60), Threshold("AIHitChance", 55), Penalty(), cf, alphaStrike, Setting("DebugLogging", false));
        try { _runtime?.Update(); }
        catch (Exception e)
        {
            if (!_updateErrorLogged)
            {
                _updateErrorLogged = true;
                ErrorLog("Runtime update failed; further identical frame errors are suppressed until the next scene: " + e);
            }
        }
    }

    public void OnGUI()
    {
        if (_guiErrorLogged) return;
        try { DrawGUI(); }
        catch (Exception e)
        {
            _guiErrorLogged = true;
            ErrorLog("HUD disabled for this scene after an error; repeated OnGUI spam is suppressed: " + e);
        }
    }

    private void DrawGUI()
    {
        if (!TacticalHudVisibility.IsVisible || _runtime == null || !_runtime.InMission || !Setting("ShowHUD", true)) return;
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager) || !manager.IsPlayerTurn() || manager.IsPaused()) return;

        // No selected player unit means there is nothing for the panel to operate on.
        // Hide it unconditionally; "Show button when unavailable" only applies to a
        // currently selected unit that cannot arm Overwatch for some other reason.
        if (!_runtime.HasSelectedPlayerActor) return;
        if (!_runtime.CanArmCurrent && !_runtime.Armed && !Setting("ShowUnavailableHUD", true)) return;

        float scale = Math.Clamp(Setting("HUDScale", 1.0f), 0.75f, 1.5f);
        float width = 390f * scale, height = 78f * scale;
        string pos = Setting("HUDPosition", "Bottom Center");
        float x, y;
        switch (pos)
        {
            case "Bottom Left": x = 18f; y = Screen.height - height - 165f * scale; break;
            case "Top Left": x = 18f; y = 90f; break;
            case "Top Right": x = Screen.width - width - 18f; y = 90f; break;
            default: x = (Screen.width - width) / 2f; y = Screen.height - height - 165f * scale; break;
        }
        if (Setting("StackWithCoordinatedFire", true) && IsCoordinatedFireInstalled() && pos.StartsWith("Bottom", StringComparison.Ordinal))
            y -= 84f * scale;
        else if (Setting("StackWithCoordinatedFire", true) && IsCoordinatedFireInstalled() && pos.StartsWith("Top", StringComparison.Ordinal))
            y += 84f * scale;

        _hudRect = new Rect(Math.Clamp(x, 4, Math.Max(4, Screen.width - width - 4)),
            Math.Clamp(y, 4, Math.Max(4, Screen.height - height - 4)), width, height);
        GUI.Box(_hudRect, GUIContent.none);

        float pad = 6f * scale, chipH = 22f * scale, gap = 4f * scale;
        bool old = GUI.enabled;
        GUI.enabled = _runtime.CanEditCurrentPlan;

        if (_runtime.IsSelectedVehicle)
        {
            int columns = _runtime.AlphaStrikeAvailable ? 2 : 1;
            float chipW = (width - 2 * pad - gap * (columns - 1)) / columns;
            float cx = _hudRect.x + pad;
            string vehicleText = "VEH: " + Trim(_runtime.VehicleName, 25);
            var vehicleRect = new Rect(cx, _hudRect.y + pad, chipW, chipH);
            if (ModeButton(vehicleRect, vehicleText, _runtime.Mode == OverwatchMode.Vehicle)) _runtime.SelectVehicle();

            if (_runtime.AlphaStrikeAvailable)
            {
                cx += chipW + gap;
                string alphaText = "ALPHA: " + Trim(_runtime.AlphaStrikeName, 19);
                var alphaRect = new Rect(cx, _hudRect.y + pad, chipW, chipH);
                if (ModeButton(alphaRect, alphaText, _runtime.Mode == OverwatchMode.AlphaStrike)) _runtime.SelectAlphaStrike();
            }
        }
        else
        {
            int columns = _runtime.BothAvailable ? 3 : 2;
            float chipW = (width - 2 * pad - gap * (columns - 1)) / columns;
            float cx = _hudRect.x + pad;

            string squadText = "INF: " + Trim(_runtime.SquadName, 20);
            string leaderText = "SPECIAL: " + Trim(_runtime.LeaderName, 20);
            var squadRect = new Rect(cx, _hudRect.y + pad, chipW, chipH);
            if (ModeButton(squadRect, squadText, _runtime.Mode == OverwatchMode.Squad)) _runtime.SelectSquad();
            cx += chipW + gap;
            var leaderRect = new Rect(cx, _hudRect.y + pad, chipW, chipH);
            if (ModeButton(leaderRect, leaderText, _runtime.Mode == OverwatchMode.Leader)) _runtime.SelectLeader();
            if (_runtime.BothAvailable)
            {
                cx += chipW + gap;
                var bothRect = new Rect(cx, _hudRect.y + pad, chipW, chipH);
                if (ModeButton(bothRect, "BOTH", _runtime.Mode == OverwatchMode.Both)) _runtime.SelectBoth();
            }
        }

        var button = new Rect(_hudRect.x + pad, _hudRect.y + pad + chipH + 4f * scale, width - 2 * pad, 29f * scale);
        string text;
        if (_runtime.Armed && _runtime.CarriedIntoFreshActivation)
        {
            int planned = _runtime.PlannedReactions;
            text = planned > 0
                ? $"RE-ARM OVERWATCH  -  {planned} REACTION{(planned == 1 ? "" : "S")}  -  END TURN"
                : "OVERWATCH CARRIED";
        }
        else if (_runtime.Armed) text = "OVERWATCH ARMED";
        else if (_runtime.PlannedReactions > 0) text = $"OVERWATCH  -  {_runtime.PlannedReactions} REACTION{(_runtime.PlannedReactions == 1 ? "" : "S")}  -  END TURN";
        else text = "OVERWATCH";
        GUI.enabled = _runtime.CanArmCurrent;
        if (GUI.Button(button, text)) _runtime.TogglePlayer();
        GUI.enabled = old;

        _reason ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        _reason.fontSize = Math.Max(9, (int)(10 * scale));
        GUI.Label(new Rect(_hudRect.x + pad, button.yMax + 1f * scale, width - 2 * pad, 15f * scale), _runtime.Status, _reason);
    }

    private static bool ModeButton(Rect rect, string text, bool selected)
    {
        Color oldBackground = GUI.backgroundColor;
        Color oldContent = GUI.contentColor;
        if (selected)
        {
            GUI.backgroundColor = new Color(0.28f, 0.78f, 0.45f, 1f);
            GUI.contentColor = Color.white;
            text = "> " + text;
        }

        bool clicked = GUI.Button(rect, text);
        GUI.backgroundColor = oldBackground;
        GUI.contentColor = oldContent;
        return clicked;
    }

    private static bool IsCoordinatedFireInstalled()
        => ModPresence.IsLoadedOrInstalled("CoordinatedFire", "CoordinatedFire");

    private static bool IsAlphaStrikeInstalled()
        => ModPresence.IsLoadedOrInstalled("AlphaStrike", "AlphaStrike");

    private int Threshold(string key, int fallback) => Math.Clamp((int)Math.Round(Setting(key, (float)fallback)), 1, 100);
    private int Penalty() => Math.Clamp((int)Math.Round(Setting("OverwatchAimPenalty", 10f)), 0, 40);
    private T Setting<T>(string key, T fallback)
    { try { return ModSettings.Get<T>(SettingsGroup, key); } catch { return fallback; } }
    private static string Trim(string text, int max) => string.IsNullOrEmpty(text) ? "-" : text.Length <= max ? text : text[..Math.Max(1, max - 1)] + "…";

    private void DrawDebug(Rect area)
    {
        float y = area.y; const float h = 22f;
        void Line(string t) { GUI.Label(new Rect(area.x, y, area.width, h), t); y += h; }
        Line($"Overwatch {Version}");
        Line("Runtime: " + (_runtime?.Status ?? "not bound"));
        Line("Mode: " + (_runtime?.Mode.ToString() ?? "-"));
        Line("Infantry: " + (_runtime?.SquadName ?? "-"));
        Line("Special: " + (_runtime?.LeaderName ?? "-"));
        Line("Vehicle: " + (_runtime?.VehicleName ?? "-"));
        Line("Alpha Strike: " + (_runtime?.AlphaStrikeName ?? "-"));
        Line("Armed units: " + (_runtime?.ArmedCount ?? 0));
        Line("Enemy attack triggers: " + (Setting("TriggerOnEnemyActions", true) ? "on" : "off"));
        Line("Vehicle Overwatch: " + (Setting("EnableVehicleOverwatch", true) ? "on" : "off"));
        Line("AI vehicle Overwatch: " + (Setting("EnableAIVehicleOverwatch", false) ? "on" : "off"));
        Line("Overwatch indicators: " + (Setting("ShowWorldMarkers", true) ? "on" : "off"));
        Line("High contrast Overwatch indicators: " + (Setting("HighContrastIndicators", true) ? "on" : "off"));
        Line("Debug logging: " + (Setting("DebugLogging", false) ? "on" : "off"));
        Line("Player threshold: " + Threshold("PlayerHitChance", 60) + "%");
        Line("Aim penalty: -" + Penalty() + " points");
        Line("AI threshold: " + Threshold("AIHitChance", 55) + "%");
        Line("Coordinated Fire detected: " + (IsCoordinatedFireInstalled() ? "yes" : "no"));
        Line("Alpha Strike detected: " + (IsAlphaStrikeInstalled() ? "yes" : "no"));
    }

    public void OnUnload()
    {
        DevConsole.RemovePanel("Overwatch");
        TacticalHudVisibility.Uninstall();
        _runtime?.Dispose();
        _runtime = null;
    }

    private void DebugLog(string message)
    {
        if (Setting("DebugLogging", false)) _logger?.Msg(message);
    }

    // Keep non-debug output to rare diagnostics only. Routine gameplay, AI, HUD,
    // eligibility and reaction-flow messages are completely silent by default.
    private void WarningLog(string message) => _logger?.Msg("WARNING: " + message);
    private void ErrorLog(string message) => _logger?.Msg("ERROR: " + message);
}

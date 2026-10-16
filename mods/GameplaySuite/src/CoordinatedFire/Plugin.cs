using System.Reflection;
using System.Text;
using CoordinatedFire.Core;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using MenaceModSettings;
using MenaceModSuite.Shared;
using UnityEngine;
using Path = global::System.IO.Path;

[assembly: AssemblyVersion("1.6.10.0")]
[assembly: AssemblyFileVersion("1.6.10.0")]
[assembly: AssemblyInformationalVersion("1.6.10")]

namespace CoordinatedFire;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/CoordinatedFire/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string SettingsGroup = "Coordinated Fire";
    private const string Version = "1.6.10";
    private ModLog? _logger;
    private global::HarmonyLib.Harmony? _harmony;
    private Runtime? _runtime;
    private string _data = "";
    private string _status = "Initializing...";
    private bool _attempted;
    private float _bindAt;
    private Rect _hudRect;
    private GUIStyle? _small;
    private GUIStyle? _reason;
    private bool _alphaStrikeWarningShown;

    public void Initialize(ModLog logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;
        _data = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "CoordinatedFire");
        Directory.CreateDirectory(_data);

        ModSettings.Register(SettingsGroup, settings =>
        {
            settings.AddHeader("Gameplay");
            settings.AddToggle("EnableGameplay", "Enable Coordinated Fire", true);
            settings.AddToggle("EnableAICombinedFire", "AI uses Coordinated Fire", true);
            settings.AddToggle("EnableVehicleCombinedFire", "Vehicle Coordinated Fire (OFF with Alpha Strike)", false);
            settings.AddToggle("SynchronizedPlayerFire", "Synchronized player volley (0.10s stagger, experimental)", false);

            settings.AddHeader("Interface");
            settings.AddToggle("ShowHUD", "Show tactical button", true);
            settings.AddToggle("ShowUnavailableHUD", "Show HUD when unavailable", true);
            settings.AddDropdown("HUDPosition", "HUD position",
                new[] { "Bottom Center", "Bottom Left", "Bottom Right", "Top Center", "Top Left", "Top Right", "Center (Custom Offset)" },
                "Bottom Center");
            settings.AddSlider("HUDOffsetX", "Horizontal offset (px)", -600f, 600f, 0f);
            settings.AddSlider("HUDOffsetY", "Vertical offset (px)", -400f, 400f, 0f);
            settings.AddSlider("HUDScale", "HUD scale", HudLayout.MinScale, HudLayout.MaxScale, 1.0f);
            settings.AddToggle("CompactHUD", "Compact HUD (main button only)", false);
            settings.AddToggle("ResetHUDLayout", "Reset HUD layout to defaults", false);

            settings.AddHeader("Support");
            settings.AddToggle("VerboseLogging", "Debug logging", false);
        });
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel("Coordinated Fire", DrawDebugPanel);
        HudInputGuard.Register(this, GetInputRect);

        _bindAt = Time.realtimeSinceStartup + 2f;
        Log($"{Version}: initialized. Compact tactical UI and ModSettings enabled.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _runtime?.SceneChanged();
        if (_runtime == null)
        {
            _attempted = false;
            _bindAt = Time.realtimeSinceStartup + 2f;
        }
    }

    public void OnUpdate()
    {
        bool gameplay = Setting("EnableGameplay", true);
        if (!gameplay)
        {
            if (_runtime != null)
            {
                _runtime.Dispose();
                _runtime = null;
            }
            _status = "Disabled in Mod Settings.";
            return;
        }

        if (!_attempted && Time.realtimeSinceStartup >= _bindAt)
        {
            _attempted = true;
            try
            {
                if (_harmony == null) throw new InvalidOperationException("Modpack Loader Harmony instance is unavailable.");
                _runtime = new Runtime(45, Setting("EnableAICombinedFire", true), Setting("EnableVehicleCombinedFire", false), Setting("SynchronizedPlayerFire", false), Log, Error, _harmony);
                _status = "Ready";
            }
            catch (Exception e)
            {
                _status = "Bindings failed: " + e.Message;
                Error(e.ToString());
                WriteReport();
            }
        }

        if (_runtime != null)
        {
            _runtime.SetAIEnabled(Setting("EnableAICombinedFire", true));
            _runtime.SetVehicleEnabled(Setting("EnableVehicleCombinedFire", false));
            _runtime.SetSynchronizedFireEnabled(Setting("SynchronizedPlayerFire", false));
            if (Setting("EnableVehicleCombinedFire", false) && AlphaStrikeLoaded() && !_alphaStrikeWarningShown)
            {
                _alphaStrikeWarningShown = true;
                Log("WARNING: Alpha Strike is loaded while vehicle Coordinated Fire is enabled. Use only one combined-fire system for vehicles to avoid overlapping behavior.");
            }
            try { _runtime.Update(); }
            catch (Exception e) { _runtime.Fail(e); }
        }
    }

    private HudRect? GetHudLayout()
    {
        if (_runtime == null || !Setting("EnableGameplay", true) || !Setting("ShowHUD", true) || !TacticalManager.IsMissionRunning()) return null;
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager) || !manager.IsPlayerTurn() || manager.IsPaused()) return null;

        bool hasPair = _runtime.SquadName != "-" && _runtime.LeaderName != "-";
        if (!hasPair && !Setting("ShowUnavailableHUD", true)) return null;

        return HudLayout.Compute(
            Screen.width, Screen.height,
            Setting("HUDScale", 1.0f),
            Setting("CompactHUD", false),
            ParseAnchor(Setting("HUDPosition", "Bottom Center")),
            Setting("HUDOffsetX", 0f),
            Setting("HUDOffsetY", 0f));
    }

    private Rect? GetInputRect()
    {
        var layout = GetHudLayout();
        return layout.HasValue ? new Rect(layout.Value.X, layout.Value.Y, layout.Value.Width, layout.Value.Height) : null;
    }

    public void OnGUI()
    {
        var currentLayout = GetHudLayout();
        if (!currentLayout.HasValue) return;
        var layout = currentLayout.Value;
        bool compact = Setting("CompactHUD", false);
        float scale = layout.Scale;
        float width = layout.Width;
        float height = layout.Height;
        float x = layout.X;
        float y = layout.Y;
        _hudRect = new Rect(x, y, width, height);

        _small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        _reason ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        _small.fontSize = Math.Max(10, (int)(11 * scale));
        _reason.fontSize = Math.Max(9, (int)(10 * scale));

        GUI.Box(_hudRect, GUIContent.none);
        float pad = 6f * scale;
        float chipH = 22f * scale;
        float gap = 5f * scale;

        bool oldEnabled = GUI.enabled;
        float buttonY;
        if (!compact)
        {
            float chipW = (width - 2 * pad - gap) / 2f;
            var squadRect = new Rect(x + pad, y + pad, chipW, chipH);
            var leaderRect = new Rect(squadRect.xMax + gap, y + pad, chipW, chipH);
            GUI.enabled = !_runtime.Locked && !_runtime.Armed;
            if (GUI.Button(squadRect, TrimMode(_runtime.SquadName, 36))) _runtime.Cycle(WeaponRole.Squad);
            if (GUI.Button(leaderRect, TrimMode(_runtime.LeaderName, 36))) _runtime.Cycle(WeaponRole.Leader);
            buttonY = y + pad + chipH + 4f * scale;
        }
        else
        {
            buttonY = y + pad;
        }

        var buttonRect = new Rect(x + pad, buttonY, width - 2 * pad, 29f * scale);
        var plan = _runtime.Current.Plan;
        string mainText = _runtime.IsActive ? $"COORDINATED FIRE  -  {_runtime.ReservedCost} AP  -  FIRING" :
            _runtime.Armed ? "CANCEL COORDINATED TARGETING" :
            plan != null ? $"COORDINATED FIRE  -  {plan.Cost} AP" : "COORDINATED FIRE";
        GUI.enabled = !_runtime.Locked && (_runtime.Armed || _runtime.Current.Allowed);
        if (GUI.Button(buttonRect, mainText))
        {
            try
            {
                if (_runtime.Armed) _runtime.CancelTargeting("Combined targeting cancelled.");
                else _runtime.Arm();
            }
            catch (Exception e) { _runtime.Fail(e); }
        }
        GUI.enabled = oldEnabled;

        if (!compact)
        {
            string reason = _runtime.IsActive ? "Both weapons are resolving through MENACE's native attack flow." :
                _runtime.Current.Allowed ? "Both weapons will fire. AP cost = the more expensive attack." : _runtime.Current.Reason;
            var reasonRect = new Rect(x + pad, buttonRect.yMax + 1f * scale, width - 2 * pad, 15f * scale);
            GUI.Label(reasonRect, reason, _reason);
        }
    }

    private void DrawDebugPanel(Rect area)
    {
        float y = area.y;
        const float h = 22f;
        void Line(string text)
        {
            GUI.Label(new Rect(area.x, y, area.width, h), text);
            y += h;
        }

        Line($"Coordinated Fire {Version}");
        Line("Runtime: " + (_runtime?.Status ?? _status));
        bool vehicle = _runtime?.IsVehicleMode == true;
        Line((vehicle ? "Vehicle weapon A: " : "Squad: ") + (_runtime?.SquadName ?? "-"));
        Line((vehicle ? "Vehicle weapon B: " : "Leader: ") + (_runtime?.LeaderName ?? "-"));
        Line("Decision: " + (_runtime?.Current.Reason ?? "unavailable"));
        Line("AI combined fire: " + (Setting("EnableAICombinedFire", true) ? "enabled" : "disabled"));
        Line("Vehicle combined fire: " + (Setting("EnableVehicleCombinedFire", false) ? "enabled" : "disabled"));
        Line("Synchronized player volley: " + (Setting("SynchronizedPlayerFire", false) ? "enabled" : "disabled"));
        Line("Alpha Strike detected: " + (AlphaStrikeLoaded() ? "yes" : "no"));
        if (GUI.Button(new Rect(area.x, y + 4, 180, 26), "Write diagnostic file")) WriteReport();
    }

    public void OnUnload()
    {
        HudInputGuard.Unregister(this);
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel("Coordinated Fire");
        _runtime?.Dispose();
        _runtime = null;
    }

    private void OnSettingChanged(string modName, string key, object value)
    {
        if (modName != SettingsGroup) return;
        if (key == "EnableAICombinedFire" && _runtime != null && value is bool enabled)
            _runtime.SetAIEnabled(enabled);
        if (key == "EnableVehicleCombinedFire" && _runtime != null && value is bool vehicleEnabled)
            _runtime.SetVehicleEnabled(vehicleEnabled);
        if (key == "SynchronizedPlayerFire" && _runtime != null && value is bool syncEnabled)
        {
            _runtime.SetSynchronizedFireEnabled(syncEnabled);
            Log(syncEnabled
                ? "Synchronized player volley enabled: the second native attack is attempted after a 0.10s stagger; clean rejection falls back to sequential fire. AI remains sequential."
                : "Synchronized player volley disabled: using proven sequential native attack flow.");
        }
        if (key == "ResetHUDLayout" && value is bool reset && reset)
        {
            ModSettings.Set(SettingsGroup, "HUDPosition", "Bottom Center");
            ModSettings.Set(SettingsGroup, "HUDOffsetX", 0f);
            ModSettings.Set(SettingsGroup, "HUDOffsetY", 0f);
            ModSettings.Set(SettingsGroup, "HUDScale", 1.0f);
            ModSettings.Set(SettingsGroup, "CompactHUD", false);
            ModSettings.Set(SettingsGroup, "ResetHUDLayout", false);
            Log("HUD layout reset to defaults.");
        }
        Log($"Setting changed: {key}={value}");
    }

    private void WriteReport()
    {
        try
        {
            var text = new StringBuilder();
            text.AppendLine($"Coordinated Fire {Version} -- diagnostic");
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Runtime: " + (_runtime?.Status ?? _status));
            text.AppendLine("AI combined fire: " + (_runtime?.AIEnabled == true ? "enabled" : "disabled"));
            text.AppendLine("Vehicle combined fire: " + (_runtime?.VehicleEnabled == true ? "enabled" : "disabled"));
            text.AppendLine("Synchronized player volley: " + (_runtime?.SynchronizedFireEnabled == true ? "enabled (0.10s experimental stagger)" : "disabled"));
            text.AppendLine("Alpha Strike detected: " + AlphaStrikeLoaded());
            bool vehicleMode = _runtime?.IsVehicleMode == true;
            text.AppendLine((vehicleMode ? "Selected vehicle weapon A: " : "Selected squad mode: ") + (_runtime?.SquadName ?? "-"));
            text.AppendLine((vehicleMode ? "Selected vehicle weapon B: " : "Selected leader mode: ") + (_runtime?.LeaderName ?? "-"));
            text.AppendLine("Decision: " + (_runtime?.Current.Reason ?? "unavailable"));
            text.AppendLine($"Screen={Screen.width}x{Screen.height}; HUDPosition={Setting("HUDPosition", "Bottom Center")}; " +
                $"HUDOffsetX={Setting("HUDOffsetX", 0f)}; HUDOffsetY={Setting("HUDOffsetY", 0f)}; " +
                $"HUDScale={Setting("HUDScale", 1.0f)}; CompactHUD={Setting("CompactHUD", false)}; HUDRect={_hudRect}");
            if (TacticalManager.IsMissionRunning())
            {
                var actor = TacticalManager.Get().GetActiveActor();
                if (Native.Valid(actor))
                {
                    text.AppendLine("AP=" + actor.GetActionPoints() + "; morale=" + actor.GetMoraleState());
                    var list = actor.GetAllAttacks();
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        var t = s.GetTemplate();
                        Native.RequirementState(s, actor, out var reqDeploy, out var deployOk, out var reqSetup, out var setupOk);
                        text.AppendLine($"ATTACK {s.GetID()} title={s.GetTitle()} role={Native.Role(s)} " +
                            $"candidate={Native.Supported(s)} active={s.IsActive()} targeted={s.IsTargeted()} attack={s.IsAttack()} " +
                            $"requiresDeploy={reqDeploy} deploySatisfied={deployOk} requiresSetup={reqSetup} setupSatisfied={setupOk} " +
                            $"AP={s.GetActionPointCost()} uses={s.GetUses()}/{s.GetMaxUses()} limited={s.HasLimitedUses()} out={s.IsOutOfUses()} " +
                            $"usable={s.IsUsable(TargetUsageParams.Player)} affordable={s.IsAffordable()} range={s.GetMinRange()}-{s.GetMaxRange()} " +
                            $"lofFlag={t.IsLineOfFireNeeded} selectableTiles={t.SelectableTiles}");
                    }
                }
            }
            var path = Path.Combine(_data, "diagnostic.txt");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            Log("Diagnostic written: " + path);
        }
        catch (Exception e) { Error("Diagnostic failed: " + e.Message); }
    }


    private static HudAnchor ParseAnchor(string value) => value switch
    {
        "Bottom Left" => HudAnchor.BottomLeft,
        "Bottom Right" => HudAnchor.BottomRight,
        "Top Center" => HudAnchor.TopCenter,
        "Top Left" => HudAnchor.TopLeft,
        "Top Right" => HudAnchor.TopRight,
        "Center (Custom Offset)" => HudAnchor.Center,
        _ => HudAnchor.BottomCenter
    };

    private static string TrimMode(string value, int max)
        => value.Length <= max ? value : value[..Math.Max(1, max - 1)] + "...";

    private static bool AlphaStrikeLoaded()
        => ModPresence.IsLoadedOrInstalled("AlphaStrike", "AlphaStrike");

    private static T Setting<T>(string key, T fallback)
    {
        var value = ModSettings.Get<T>(SettingsGroup, key);
        return value is null ? fallback : value;
    }

    private void Log(string text)
    {
        if (!Setting("VerboseLogging", false)) return;
        _logger?.Msg("[CoordinatedFire] " + text);
    }

    private void Error(string text)
    {
        _logger?.Msg("[CoordinatedFire] ERROR: " + text);
    }
}

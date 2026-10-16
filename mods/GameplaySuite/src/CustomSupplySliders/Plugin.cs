using System;
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Menu;
using MenaceModSettings;
using MenaceModSuite.Shared;

[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]
[assembly: AssemblyInformationalVersion("0.2.1")]

namespace MenaceCustomSupplySliders;

/// <summary>
/// Widens the Player/Enemy Supply Points sliders of New Game -> Customize Difficulty.
/// Normal/Hard/Expert templates are untouched; only the custom-difficulty slider bounds change.
/// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/CustomSupplySliders/code.
/// </summary>
public sealed partial class Plugin : IHostedPlugin
{
    private const string ModName = "Custom Supply Sliders";

    // Confirmed from MENACE native code (0.7.15 .. 0.7.17):
    // NewGameWindow creates Player Supply with 67..150 and Enemy Supply with 67..200.
    private const int VanillaSupplyMin = 67;
    private const int VanillaPlayerMax = 150;
    private const int VanillaEnemyMax = 200;

    private static ModLog? _log;
    private static bool _debug;

    private static int _playerMin;
    private static int _playerMax = 2000;
    private static int _enemyMin;
    private static int _enemyMax = 2000;

    public void Initialize(ModLog log, HarmonyLib.Harmony harmony)
    {
        _log = log;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Custom difficulty supply sliders");
            settings.AddNumber("PlayerMin", "Player Supply Points minimum (%)", 0, 2000, 0, 100);
            settings.AddNumber("PlayerMax", "Player Supply Points maximum (%)", 150, 10000, 2000, 100);
            settings.AddNumber("EnemyMin", "Enemy Supply Points minimum (%)", 0, 2000, 0, 100);
            settings.AddNumber("EnemyMax", "Enemy Supply Points maximum (%)", 200, 10000, 2000, 100);

            settings.AddHeader("Diagnostics");
            settings.AddToggle("DebugLogging", "Debug logging", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;

        int installed = 0;

        // Primary hook: the New Game helper, with explicit by-reference ints so the
        // rewritten bounds reach the native call frame.
        MethodInfo? addSlider = AccessTools.Method(typeof(NewGameWindow), "AddCustomDifficultySlider");
        if (InstallHook(harmony, addSlider, nameof(AddCustomDifficultySliderPrefix), "NewGameWindow.AddCustomDifficultySlider"))
            installed++;

        // Independent fallback: LabeledIntSlider.Init(int, int, bool) receives the same
        // bounds right before constructing the SliderInt. Matching the unique
        // 67..150 / 67..200 pairs keeps unrelated sliders untouched.
        MethodInfo? init = AccessTools.Method(typeof(LabeledIntSlider), "Init", new[] { typeof(int), typeof(int), typeof(bool) });
        if (InstallHook(harmony, init, nameof(LabeledIntSliderInitPrefix), "LabeledIntSlider.Init"))
            installed++;

        if (installed == 0)
            _log.Error($"{ModName}: no compatible slider hook target was found on this MENACE build.");
        else
            DebugLog($"Installed {installed} supply-slider range hook(s).");
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
    }

    private static bool InstallHook(HarmonyLib.Harmony harmony, MethodInfo? target, string patchMethod, string label)
    {
        try
        {
            MethodInfo? patch = AccessTools.Method(typeof(Plugin), patchMethod);
            if (target == null || patch == null)
            {
                _log?.Warning($"{ModName}: missing {label} patch target; hook unavailable on this MENACE build.");
                return false;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(patch));
            return true;
        }
        catch (Exception ex)
        {
            _log?.Warning($"{ModName}: {label} hook could not be installed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // NewGameWindow.AddCustomDifficultySlider(string _tooltipId, int _min, int _max, Func<int,string> _formatValue).
    // Positional names (__1 / __2) avoid relying on IL2CPP generated parameter names.
    private static void AddCustomDifficultySliderPrefix(ref int __1, ref int __2)
    {
        RewriteSupplyRange(ref __1, ref __2, "NewGameWindow.AddCustomDifficultySlider");
    }

    // LabeledIntSlider.Init(int _minValue, int _maxValue, bool _active).
    private static void LabeledIntSliderInitPrefix(ref int __0, ref int __1)
    {
        RewriteSupplyRange(ref __0, ref __1, "LabeledIntSlider.Init");
    }

    private static void RewriteSupplyRange(ref int min, ref int max, string source)
    {
        if (min != VanillaSupplyMin)
            return;

        if (max == VanillaPlayerMax)
        {
            min = Math.Max(0, _playerMin);
            max = Math.Max(min, _playerMax);
            DebugLog($"Player Supply range rewritten by {source}: {min}-{max}.");
            return;
        }

        if (max == VanillaEnemyMax)
        {
            min = Math.Max(0, _enemyMin);
            max = Math.Max(min, _enemyMax);
            DebugLog($"Enemy Supply range rewritten by {source}: {min}-{max}.");
        }
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, ModName, StringComparison.Ordinal)) return;
        ReloadSettings();
        DebugLog($"Setting changed: {key}. Reopen New Game to rebuild the sliders.");
    }

    private static void ReloadSettings()
    {
        _playerMin = ReadInt("PlayerMin", 0, 0, 2000);
        _playerMax = ReadInt("PlayerMax", 2000, 150, 10000);
        _enemyMin = ReadInt("EnemyMin", 0, 0, 2000);
        _enemyMax = ReadInt("EnemyMax", 2000, 200, 10000);
        _debug = ReadBool("DebugLogging", false);
    }

    private static int ReadInt(string key, int fallback, int min, int max)
    {
        try
        {
            object? value = ModSettings.Get(ModName, key);
            if (value == null) return fallback;
            return Math.Max(min, Math.Min(Convert.ToInt32(value), max));
        }
        catch
        {
            return fallback;
        }
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try
        {
            object? value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static void DebugLog(string message)
    {
        if (_debug)
            _log?.Msg($"[{ModName} debug] {message}");
    }
}

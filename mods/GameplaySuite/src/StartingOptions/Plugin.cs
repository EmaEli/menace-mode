using System;
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.Items;
using Il2CppMenace.Strategy;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Menu;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1")]

namespace MenaceStartingOptions;

/// <summary>
/// Configures MENACE's native New Game screen. No patches run during tactical play.
/// Both settings affect newly created campaigns only.
/// </summary>
public sealed class Plugin : IModpackPlugin
{
    private const string ModName = "Starting Options";
    private const string SparePartsTemplateId = "commodity.spare_parts";
    private const int VanillaLeaderMaximum = 6;
    private const int MaximumSupportedLeaders = 12;
    private const int VanillaHeadstartCount = 36;
    private const int MaximumHeadstartCount = 1000;

    private static MelonLogger.Instance? _logger;
    private static int _leaderMaximum = MaximumSupportedLeaders;
    private static int _headstartCount = VanillaHeadstartCount;
    private static bool _debug;
    private static readonly PropertyInfo? BlackMarketHeadstartProperty =
        AccessTools.Property(typeof(NewGameWindow), "m_BlackMarketHeadstartToggle");
    private static readonly FieldInfo? BlackMarketHeadstartField =
        AccessTools.Field(typeof(NewGameWindow), "m_BlackMarketHeadstartToggle");

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _logger = logger;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("New Campaign: Leaders");
            settings.AddNumber(
                "InitialLeaderMaximum",
                "Maximum selectable starting leaders (reopen New Game)",
                VanillaLeaderMaximum, MaximumSupportedLeaders, MaximumSupportedLeaders);

            settings.AddHeader("New Campaign: Black Market Headstart");
            settings.AddNumber(
                "HeadstartSparePartsCount",
                "Starting Spare Parts (only when Black Market Headstart is checked)",
                0, MaximumHeadstartCount, VanillaHeadstartCount);

            settings.AddHeader("Diagnostics");
            settings.AddToggle("DebugLogging", "Enable diagnostic logging (OFF by default)", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;

        // The vanilla NewGameWindow creates exactly one custom slider with
        // min=1, max=6 (Initial Leaders); another slider uses min=0, max=6.
        InstallHook(harmony, "AddCustomDifficultySlider", nameof(StartingLeaderSliderPrefix), true);

        // The native method creates the initial extra-item list ONLY when the
        // Black Market Headstart checkbox is enabled. Adjust that list once,
        // after native creation, without changing the game's checkbox logic.
        InstallHook(harmony, "CreateGlobalDifficultyObject", nameof(CreateDifficultyPostfix), false);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, ModName, StringComparison.Ordinal)) return;
        ReloadSettings();
    }

    private static void ReloadSettings()
    {
        _leaderMaximum = Clamp(ReadInt("InitialLeaderMaximum", MaximumSupportedLeaders),
            VanillaLeaderMaximum, MaximumSupportedLeaders);
        _headstartCount = Clamp(ReadInt("HeadstartSparePartsCount", VanillaHeadstartCount),
            0, MaximumHeadstartCount);
        _debug = ReadBool("DebugLogging", false);
    }

    private static int ReadInt(string key, int fallback)
    {
        try
        {
            object? value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToInt32(value);
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

    private static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(value, maximum));
    }

    private static void InstallHook(HarmonyLib.Harmony harmony, string gameMethod,
        string patchMethod, bool isPrefix)
    {
        try
        {
            MethodInfo? target = AccessTools.Method(typeof(NewGameWindow), gameMethod);
            MethodInfo? patch = AccessTools.Method(typeof(Plugin), patchMethod);
            if (target == null || patch == null)
            {
                _logger?.Warning($"{ModName}: missing {gameMethod} patch target; feature unavailable on this MENACE build.");
                return;
            }

            if (isPrefix)
                harmony.Patch(target, prefix: new HarmonyMethod(patch));
            else
                harmony.Patch(target, postfix: new HarmonyMethod(patch));

            DebugLog($"Hook installed: NewGameWindow.{gameMethod}.");
        }
        catch (Exception ex)
        {
            _logger?.Warning($"{ModName}: {gameMethod} hook could not be installed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Signature of NewGameWindow.AddCustomDifficultySlider:
    ///   (string __0, int __1, int __2, Func&lt;int,string&gt; __3)
    /// Native MENACE uses min 1 / max 6 solely for the Initial Leaders slider.
    /// </summary>
    private static void StartingLeaderSliderPrefix(int __1, ref int __2)
    {
        if (__1 != 1 || __2 != VanillaLeaderMaximum) return;

        int maximum = _leaderMaximum;
        try
        {
            // Prevent an impossible 'pick N of M' dialog on smaller custom
            // leader pools or future game builds with fewer eligible leaders.
            StrategyConfig? config = StrategyConfig.GetDefault();
            int available = config?.InitialPickableUnitLeaders?.Length ?? 0;
            if (available >= VanillaLeaderMaximum)
                maximum = Math.Min(maximum, available);
        }
        catch (Exception ex)
        {
            DebugLog($"Could not inspect initial leader pool: {ex.GetType().Name}.");
        }

        __2 = maximum;
        DebugLog($"Initial Leaders slider max={maximum} (configured={_leaderMaximum}).");
    }

    /// <summary>
    /// After MENACE generates the selected difficulty, normalize ONLY the
    /// extra Spare Parts introduced by its Black Market Headstart checkbox.
    /// If the checkbox was not set, there are no such bonus items, so do nothing.
    /// </summary>
    private static void CreateDifficultyPostfix(NewGameWindow __instance, GlobalDifficulty __result)
    {
        if (__result == null || !__result.IsCustomized) return;

        try
        {
            // Check the actual native toggle instead of inferring it from the
            // contents of InitialAdditionalItems: other mods may add their own
            // Spare Parts to the difficulty even with Headstart disabled.
            object? rawToggle = BlackMarketHeadstartProperty?.GetValue(__instance)
                ?? BlackMarketHeadstartField?.GetValue(__instance);
            if (rawToggle is not LabeledToggle headstartToggle)
            {
                DebugLog("Black Market Headstart toggle unavailable; no reward adjustment.");
                return;
            }
            if (!headstartToggle.GetValue()) return;

            var initialItems = __result.InitialAdditionalItems;
            if (initialItems == null || initialItems.Count == 0) return;

            BaseItemTemplate? sparePartsTemplate = null;
            int currentSpareParts = 0;
            for (int i = 0; i < initialItems.Count; i++)
            {
                BaseItemTemplate? candidate = initialItems[i];
                if (!IsSpareParts(candidate)) continue;
                sparePartsTemplate ??= candidate;
                currentSpareParts++;
            }

            // No vanilla Spare Parts: MENACE may have changed the item reward.
            // Do not synthesize new item types or alter unrelated rewards.
            if (sparePartsTemplate == null) return;

            int desired = _headstartCount;
            if (currentSpareParts == desired) return;

            if (currentSpareParts > desired)
            {
                for (int i = initialItems.Count - 1; i >= 0 && currentSpareParts > desired; i--)
                {
                    if (!IsSpareParts(initialItems[i])) continue;
                    initialItems.RemoveAt(i);
                    currentSpareParts--;
                }
            }
            else
            {
                while (currentSpareParts < desired)
                {
                    initialItems.Add(sparePartsTemplate);
                    currentSpareParts++;
                }
            }

            DebugLog($"Black Market Headstart Spare Parts adjusted to {currentSpareParts}.");
        }
        catch (Exception ex)
        {
            // At most once per new-game creation; no tactical/per-turn logs.
            _logger?.Warning($"{ModName}: unable to adjust starting Spare Parts: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsSpareParts(BaseItemTemplate? item)
    {
        if (item == null) return false;
        try
        {
            return string.Equals(item.GetID(), SparePartsTemplateId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void DebugLog(string message)
    {
        if (_debug) _logger?.Msg($"{ModName}: {message}");
    }
}

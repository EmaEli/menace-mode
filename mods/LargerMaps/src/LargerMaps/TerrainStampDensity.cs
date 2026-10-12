using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LargerMaps;

/// <summary>
/// MEN-003: increase only the fixed terrain-landform stamp attempt budgets.
/// Per-tile environment props, foliage/grass and mission setpieces are untouched.
/// </summary>
public sealed partial class Plugin
{
    private const int StampDensityReferenceSize = 64;
    private const int StampDensityFullSize = 96;
    private const int MaximumStampAttemptsPerGroup = 40;
    private const int MaximumStampDensityDiagnosticLines = 10;

    private static bool _increaseTerrainStamps;
    private static bool _diagnoseTerrainStamps;
    private static float _terrainStampMultiplier = 1f;
    private static int _terrainStampDiagnosticLines;

    private static void PatchTerrainStampDensity(HarmonyLib.Harmony harmony)
    {
        // Read once at startup. Neither generation hooks nor IL2CPP settings are
        // changed in the middle of a mission.
        _increaseTerrainStamps = TargetSize > StampDensityReferenceSize &&
                                 ReadBool("IncreaseTerrainStampDensity", false);
        _diagnoseTerrainStamps = ReadBool("DiagnoseTerrainStampDensity", false);
        float configured = ReadFloat("TerrainStampDensityMultiplier", 1.25f);
        _terrainStampMultiplier = float.IsNaN(configured) || float.IsInfinity(configured)
            ? 1f
            : Math.Clamp(configured, 1f, 1.5f);

        if (!_increaseTerrainStamps && !_diagnoseTerrainStamps) return;

        Type scatterStamps = FindType("Il2CppMenace.Tactical.Mapgen.TerrainGen.ScatterStamps") ??
                             FindType("Menace.Tactical.Mapgen.TerrainGen.ScatterStamps");
        if (scatterStamps == null)
        {
            _increaseTerrainStamps = false;
            _log.Warning($"{ModName}: terrain stamp density unavailable (ScatterStamps not found); vanilla generation retained.");
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (_increaseTerrainStamps)
        {
            // The native 0.7.15 ScatterStamps.ModifyBlueprint method forwards
            // MaxLargeStamps, MaxMediumStamps and MaxSmallStamps as this method's
            // second argument. Patching the integer leaves shared ScriptableObject
            // StampCollection assets and mission-specific overrides unmodified.
            MethodInfo method = scatterStamps.GetMethods(flags).FirstOrDefault(m =>
            {
                if (m.Name != "SpawnStampsFromList" || m.ReturnType != typeof(void)) return false;
                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length == 2 && parameters[1].ParameterType == typeof(int);
            });

            if (method == null)
            {
                _increaseTerrainStamps = false;
                _log.Warning($"{ModName}: terrain stamp density unavailable (SpawnStampsFromList/2 not found); vanilla generation retained.");
            }
            else
            {
                try
                {
                    harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(StampCountPrefix),
                            BindingFlags.Static | BindingFlags.NonPublic)));
                    _log.Msg($"{ModName}: optional terrain stamp density active: max {_terrainStampMultiplier:0.##}x at 96x96; 64x64 unchanged.");
                }
                catch (Exception ex)
                {
                    _increaseTerrainStamps = false;
                    _log.Warning($"{ModName}: terrain stamp density patch skipped safely: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        // Optional, capped map-generation diagnostics (not per-frame / per-unit).
        if (!_diagnoseTerrainStamps) return;
        MethodInfo modify = scatterStamps.GetMethods(flags).FirstOrDefault(m =>
            m.Name == "ModifyBlueprint" && m.ReturnType == typeof(bool) &&
            m.GetParameters().Length == 2);
        if (modify == null)
        {
            _log.Warning($"{ModName}: optional stamp-count diagnostics unavailable (ModifyBlueprint/2 not found).");
            return;
        }

        try
        {
            harmony.Patch(modify,
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(StampCountDiagnosticPostfix),
                    BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: optional stamp-count diagnostics skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void StampCountPrefix(ref int __1)
    {
        if (!_increaseTerrainStamps || __1 <= 0) return;
        __1 = ScaleStampAttempts(__1, TargetSize, _terrainStampMultiplier);
    }

    // Pure, deterministic scaling; keep the 64x64 baseline unmodified.
    // 72/80/88 interpolate smoothly instead of jumping straight to the 96x96 factor.
    private static int ScaleStampAttempts(int original, int mapSize, float configuredMultiplier)
    {
        if (original <= 0 || mapSize <= StampDensityReferenceSize ||
            float.IsNaN(configuredMultiplier) || configuredMultiplier <= 1f)
            return original;

        float ramp = Math.Clamp(
            (mapSize - StampDensityReferenceSize) / (float)(StampDensityFullSize - StampDensityReferenceSize),
            0f, 1f);
        float effectiveMultiplier = 1f + (Math.Clamp(configuredMultiplier, 1f, 1.5f) - 1f) * ramp;
        int scaled = (int)Math.Round(original * effectiveMultiplier, MidpointRounding.AwayFromZero);
        return Math.Max(original, Math.Min(MaximumStampAttemptsPerGroup, scaled));
    }

    private static void StampCountDiagnosticPostfix(object __instance)
    {
        if (!_diagnoseTerrainStamps || __instance == null ||
            _terrainStampDiagnosticLines >= MaximumStampDensityDiagnosticLines)
            return;

        int entry = ++_terrainStampDiagnosticLines;
        try
        {
            object collection = GetMember(__instance, "m_StampCollection");
            if (collection == null) return;

            int large = ToInt(GetMember(collection, "MaxLargeStamps"), 0);
            int medium = ToInt(GetMember(collection, "MaxMediumStamps"), 0);
            int small = ToInt(GetMember(collection, "MaxSmallStamps"), 0);
            long placed = CountMember(__instance, "m_PlacedStamps");
            float multiplier = _increaseTerrainStamps ? _terrainStampMultiplier : 1f;

            _log.Msg($"{ModName}: STAMP DIAGNOSTIC #{entry}: size={TargetSize}, enabled={_increaseTerrainStamps}, " +
                     $"limits L/M/S={large}/{medium}/{small}, adjusted=" +
                     $"{ScaleStampAttempts(large, TargetSize, multiplier)}/" +
                     $"{ScaleStampAttempts(medium, TargetSize, multiplier)}/" +
                     $"{ScaleStampAttempts(small, TargetSize, multiplier)}, placed={placed}.");
        }
        catch (Exception ex)
        {
            if (entry == 1)
                _log.Warning($"{ModName}: optional stamp diagnostic could not read native fields: {ex.GetType().Name}.");
        }
    }
}

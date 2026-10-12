using System;
using System.Reflection;

namespace LargerMaps;

/// <summary>
/// Stage 1 of Local Tactical Routing. Use two EXISTING MENACE AI candidate-
/// selection limits. We deliberately do not patch the pathfinder, mutate
/// actor movement ranges, invent map obstacles, or alter the player UI.
///
/// The game's Move.OnEvaluate reads AIConfig.WEIGHTS.NearTileLimit to bound
/// candidate iteration (verified against GameAssembly v0.7.15+21883 at
/// RVA 0x7BBD62). AIConfig.WEIGHTS.CullTilesDistances already participates
/// in the native AI tile-scoring logic. The scorer still considers attack
/// utility, distance, cover and safety using its own native methods.
/// </summary>
public sealed partial class Plugin
{
    private static object _localTacticalWeights;
    private static int _localTacticalOriginalNear = -1;
    private static int _localTacticalOriginalCull = -1;
    private static bool _localTacticalApplied;
    private static bool _localTacticalWarningLogged;

    private static bool LocalTacticalRoutingEnabled()
        => TargetSize > ZoneReferenceSize && ReadBool("UseLocalTacticalRouting", false);

    private static void ApplyLocalTacticalRouting()
    {
        if (!LocalTacticalRoutingEnabled() || _localTacticalApplied) return;
        try
        {
            // This is a static reference to the actual AIWeightsTemplate loaded
            // by MENACE. Do NOT instantiate our own weights object: the native
            // AI would keep reading the original object and ignore it.
            Type aiConfig = FindType("Il2CppMenace.Tactical.AI.AIConfig") ??
                            FindType("Menace.Tactical.AI.AIConfig");
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            // Il2CppInterop can surface native static fields as CLR properties.
            // Accept either proxy representation; never write the readonly field.
            object weights = null;
            FieldInfo weightsField = aiConfig?.GetField("WEIGHTS", staticFlags);
            try { weights = weightsField?.GetValue(null); } catch { }
            if (weights == null)
            {
                PropertyInfo weightsProperty = aiConfig?.GetProperty("WEIGHTS", staticFlags);
                try { weights = weightsProperty?.GetValue(null); } catch { }
            }
            if (weights == null)
            {
                LocalRoutingWarnOnce("game AI weights unavailable; vanilla AI retained");
                return;
            }

            int oldNear = ToInt(GetMember(weights, "NearTileLimit"), -1);
            int oldCull = ToInt(GetMember(weights, "CullTilesDistances"), -1);
            // Unknown templates or already-mutated values must not be overwritten.
            if (oldNear < 12 || oldNear > 999 || oldCull < 10 || oldCull > 99)
            {
                LocalRoutingWarnOnce("unexpected native AI weight values; vanilla AI retained");
                return;
            }

            float nearSetting = ReadFloat("LocalNearTileLimit", 20f);
            float cullSetting = ReadFloat("LocalCullTileDistance", 12f);
            if (!float.IsFinite(nearSetting) || !float.IsFinite(cullSetting))
            {
                LocalRoutingWarnOnce("invalid AI routing settings; vanilla AI retained");
                return;
            }
            int requestedNear = (int)Math.Round(Math.Clamp(nearSetting, 12f, 64f));
            int requestedCull = (int)Math.Round(Math.Clamp(cullSetting, 10f, 20f));
            // Respect tighter constraints that another mod might have chosen.
            int newNear = Math.Min(oldNear, requestedNear);
            int newCull = Math.Min(oldCull, requestedCull);

            if (!TrySetMember(weights, "NearTileLimit", newNear) ||
                !TrySetMember(weights, "CullTilesDistances", newCull) ||
                ToInt(GetMember(weights, "NearTileLimit"), -1) != newNear ||
                ToInt(GetMember(weights, "CullTilesDistances"), -1) != newCull)
            {
                // Atomic in effect: a partial change to AI parameters must
                // never be accepted. The original paths and native code stay intact.
                TrySetMember(weights, "NearTileLimit", oldNear);
                TrySetMember(weights, "CullTilesDistances", oldCull);
                LocalRoutingWarnOnce("could not apply both AI candidate limits; restored native values");
                return;
            }

            _localTacticalWeights = weights;
            _localTacticalOriginalNear = oldNear;
            _localTacticalOriginalCull = oldCull;
            _localTacticalApplied = true;
            _log.Msg($"{ModName}: local AI candidate limits on {TargetSize}x{TargetSize}: " +
                $"NearTileLimit {oldNear}->{newNear}, CullTilesDistances {oldCull}->{newCull}. " +
                "Native paths and player movement unchanged.");
        }
        catch (Exception ex)
        {
            // Never let this optional change affect tactical startup.
            RestoreLocalTacticalRouting();
            LocalRoutingWarnOnce("AI candidate limit setup failed (" + ex.GetType().Name + ")");
        }
    }

    private static void RestoreLocalTacticalRouting()
    {
        if (!_localTacticalApplied || _localTacticalWeights == null) return;
        try
        {
            bool near = TrySetMember(_localTacticalWeights, "NearTileLimit", _localTacticalOriginalNear);
            bool cull = TrySetMember(_localTacticalWeights, "CullTilesDistances", _localTacticalOriginalCull);
            if (!near || !cull)
                LocalRoutingWarnOnce("could not fully restore original AI weights");
        }
        catch (Exception ex)
        {
            LocalRoutingWarnOnce("original AI weights restore failed (" + ex.GetType().Name + ")");
        }
        finally
        {
            _localTacticalWeights = null;
            _localTacticalOriginalNear = -1;
            _localTacticalOriginalCull = -1;
            _localTacticalApplied = false;
        }
    }

    private static void LocalRoutingWarnOnce(string message)
    {
        if (_localTacticalWarningLogged) return;
        _localTacticalWarningLogged = true;
        _log.Warning($"{ModName}: local routing: {message}.");
    }
}

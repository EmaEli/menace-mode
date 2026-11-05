using System;
using System.Collections.Generic;
using Il2CppMenace.Tactical.TileEffects;
using UnityEngine;

namespace SmokeOverhaul;

public sealed partial class Plugin
{
    // Observe only. Never replace a prefab, extend a live handler, or create/remove effects.
    // Bounded per scene; no worker-thread reads and no retained handlers across scenes.
    private static readonly List<SmokeObservation> SmokeObservations = new();
    private static int _smokeObservationsStarted;
    private const int MaxSmokeObservations = 12;

    internal static void ObserveSmokeAssigned(TileEffectHandler? handler)
    {
        if (!RuntimeSettings.Enabled || !RuntimeSettings.DebugLogging || !AiSmokeOnMainThread ||
            handler == null || _smokeObservationsStarted >= MaxSmokeObservations)
            return;
        try
        {
            TileEffectTemplate? template = handler.GetTemplate();
            if (template == null || !IsSmoke(template)) return;
            foreach (var existing in SmokeObservations)
                if (existing.Handler.Pointer == handler.Pointer) return;
            int id = ++_smokeObservationsStarted;
            var observation = new SmokeObservation(handler, id,
                Time.realtimeSinceStartup + Math.Clamp(handler.GetDelay(), 0f, 30f) + 2f);
            SmokeObservations.Add(observation);
            DebugMsg($"SMOKE LIFECYCLE #{id} assigned: template={GetTemplateId(template)} " +
                $"rounds={template.RemoveAfterRounds} timed={template.HasTimeLimit} " +
                $"prefabPresent={template.SpawnObjectOrEffect != null} prefabs={template.AmountOfPrefabsSpawned} " +
                $"delay={handler.GetDelay():0.##}.");
        }
        catch (Exception ex) { Patches.LogTemplateErrorOnce(ex); }
    }

    internal static void ObserveSmokeRemoved(TileEffectHandler? handler)
    {
        if (!AiSmokeOnMainThread || handler == null) return;
        for (int i = SmokeObservations.Count - 1; i >= 0; i--)
        {
            var observation = SmokeObservations[i];
            if (observation.Handler.Pointer != handler.Pointer) continue;
            if (RuntimeSettings.Enabled && RuntimeSettings.DebugLogging)
                LogSmokeObservation(observation, "native-remove");
            SmokeObservations.RemoveAt(i);
        }
    }

    private static void UpdateSmokeLifecycleDiagnostics(float now)
    {
        if (!AiSmokeOnMainThread) return;
        if (!RuntimeSettings.Enabled || !RuntimeSettings.DebugLogging)
        {
            SmokeObservations.Clear();
            return;
        }
        for (int i = SmokeObservations.Count - 1; i >= 0; i--)
        {
            var observation = SmokeObservations[i];
            if (now < observation.NextCheck) continue;
            LogSmokeObservation(observation, observation.Sample == 0 ? "after-delay+2s" : "after-delay+8s");
            if (++observation.Sample >= 2) SmokeObservations.RemoveAt(i);
            else observation.NextCheck += 6f;
        }
    }

    private static void LogSmokeObservation(SmokeObservation observation, string phase)
    {
        try
        {
            var handler = observation.Handler;
            var tile = handler.GetTile();
            var world = handler.m_WorldObject;
            bool attached = false;
            var effects = tile?.GetEffects();
            if (effects != null)
                for (int i = 0; i < effects.Count; i++)
                    if (effects[i]?.Pointer == handler.Pointer) { attached = true; break; }
            DebugMsg($"SMOKE LIFECYCLE #{observation.Id} {phase}: attached={attached} " +
                $"lifetime={handler.GetLifetime()} worldPresent={world != null} " +
                $"worldActive={(world != null && world.activeInHierarchy)}.");
        }
        catch (Exception ex) { Patches.LogTemplateErrorOnce(ex); }
    }

    private static void ResetSmokeLifecycleDiagnostics()
    {
        SmokeObservations.Clear();
        _smokeObservationsStarted = 0;
    }

    private sealed class SmokeObservation
    {
        internal readonly TileEffectHandler Handler;
        internal readonly int Id;
        internal float NextCheck;
        internal int Sample;
        internal SmokeObservation(TileEffectHandler handler, int id, float nextCheck)
        { Handler = handler; Id = id; NextCheck = nextCheck; }
    }
}

internal static partial class Patches
{
    public static void TileEffectHandler_AssignToTile_Postfix(TileEffectHandler __instance)
        => Plugin.ObserveSmokeAssigned(__instance);

    public static void TileEffectHandler_RemoveFromTile_Prefix(TileEffectHandler __instance)
        => Plugin.ObserveSmokeRemoved(__instance);
}
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly ContactAdvanceState _contactAdvance = new();
    private static volatile bool _contactAdvanceEnabled;
    private static int _contactAdvanceThread;

    private static void PatchContactAdvance(Harmony harmony)
    {
        if (!_offensivePlanningEnabled) return;
        _contactAdvanceThread = Environment.CurrentManagedThreadId;
        foreach (var type in new[] { typeof(ContactAdvanceState), typeof(ContactAdvanceRuntime), typeof(AdvanceContact) })
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var method in nested.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                foreach (var constructor in nested.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
            }
        }
        foreach (string name in new[] { nameof(ContactAdvanceZoneEnter), nameof(ContactAdvanceZoneExit), nameof(ContactAdvanceZoneDone),
            nameof(ContactAdvanceKnownOpponent), nameof(ContactAdvanceZoneReset) })
            RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
        _contactAdvance.Observe(1, 1, 1, new(2, 30, 30, 1, 20));
        _contactAdvance.TryAim(1, 1, 1, 0, 0, out _); _contactAdvance.BeginTurn(1, 1, 1); _contactAdvance.Reset();
        var turn = AccessTools.Method(typeof(AIFaction), "OnTurnStart", Type.EmptyTypes);
        var before = AccessTools.Method(typeof(Agent), "OnBeforeProcessing", Type.EmptyTypes);
        var detection = AccessTools.Method(typeof(Actor), "SetDetectedByFaction", new[] { typeof(int) });
        if (turn == null || before == null || detection == null) throw new MissingMethodException("contact advance lifecycle");
        harmony.Patch(turn, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceTurn)));
        harmony.Patch(before, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceBefore)));
        harmony.Patch(detection, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceDetected)));
        PatchContactAdvanceZones(harmony);
        _contactAdvanceEnabled = true;
    }

    private static void ContactAdvanceTurn(AIFaction __instance)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            ContactAdvanceZoneReset();
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null || !ContactConvergenceRuntime.EligibleFaction(__instance)) return;
            ContactAdvanceRuntime.Refresh(__instance, _contactAdvance, map, manager.m_Round);
            var actors = __instance.GetActors();
            if (actors == null || actors.Count > 256) return;
            foreach (var actor in actors) ContactAdvanceRuntime.Alert(actor?.GetAgent(), _contactAdvance, map, manager.m_Round);
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceBefore(Agent __instance)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            ContactAdvanceZoneReset();
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null || __instance == null) return;
            ContactAdvanceRuntime.Refresh(__instance.GetFaction(), _contactAdvance, map, manager.m_Round);
            ContactAdvanceRuntime.Alert(__instance, _contactAdvance, map, manager.m_Round);
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceDetected(Actor __instance, int __0)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || (__0 != 6 && __0 != 9) ||
            Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map != null) ContactAdvanceRuntime.Observe(manager.GetFaction(__0)?.TryCast<AIFaction>(), __instance,
                _contactAdvance, map, manager.m_Round);
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceDisable(Exception ex)
    {
        _contactAdvanceEnabled = false; _contactAdvance.Reset(); ContactAdvanceZoneReset();
        _log.Warning($"{ModName}: contact advance disabled ({ex.GetType().Name}); native AI retained.");
    }
}
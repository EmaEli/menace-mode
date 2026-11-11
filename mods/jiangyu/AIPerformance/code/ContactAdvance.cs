using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly ContactAdvanceState _contactAdvance = new();
    private static readonly ContactAdvanceDiagnosticBudget _contactAdvanceDiagnosticBudget = new();
    private static volatile bool _contactAdvanceEnabled;
    private static volatile bool _contactAdvanceDiagnostics;
    private static int _contactAdvanceThread;
    private static int _contactAdvanceRound;

    private static void PatchContactAdvance(Harmony harmony)
    {
        if (!_offensivePlanningEnabled) return;
        _contactAdvanceThread = Environment.CurrentManagedThreadId;
        _contactAdvanceDiagnostics = ReadBool("DiagnoseContactAdvance", false);
        _contactAdvanceDiagnosticBudget.Reset();
        _contactAdvanceRound = 0;
        foreach (var type in new[] { typeof(ContactAdvanceState), typeof(ContactAdvanceRuntime), typeof(AdvanceContact),
            typeof(ContactAdvanceDiagnosticBudget) })
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
        foreach (string name in new[] { nameof(ContactAdvanceZoneEnter), nameof(ContactAdvanceZoneEvaluateEnter),
            nameof(ContactAdvanceZoneEnterCore), nameof(ContactAdvanceZonePostEnter), nameof(ContactAdvanceZoneExit),
            nameof(ContactAdvanceZoneExitCore), nameof(ContactAdvanceZoneEvaluateExit), nameof(ContactAdvanceZoneDone),
            nameof(ContactAdvanceZoneDoneCore), nameof(ContactAdvanceZonePostDone), nameof(ContactAdvanceKnownOpponent),
            nameof(ContactAdvanceZoneReset), nameof(ContactAdvanceDiagnostic) })
            RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
        _contactAdvance.Observe(1, 1, 1, new(2, 30, 30, 1, 20));
        _contactAdvance.TryAim(1, 1, 1, 0, 0, out _); _contactAdvance.BeginTurn(1, 1, 1); _contactAdvance.Reset();
        _contactAdvanceDiagnosticBudget.TryAcquire(1, 1, 1, "warmup"); _contactAdvanceDiagnosticBudget.Reset();
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
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            ContactAdvanceRuntime.Refresh(__instance, _contactAdvance, map, manager.m_Round);
            long factionId = __instance.Pointer.ToInt64();
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(manager.m_Round, factionId, 0, "turn",
                    $"result=refreshed contacts={_contactAdvance.Count(map.Pointer.ToInt64(), manager.m_Round, factionId)} thread={Environment.CurrentManagedThreadId}");
            var actors = __instance.GetActors();
            if (actors == null || actors.Count > 256)
            {
                if (_contactAdvanceDiagnostics)
                    ContactAdvanceDiagnostic(manager.m_Round, factionId, 0, "turn-alert", $"result=actor-limit actors={actors?.Count ?? -1} limit=256");
                return;
            }
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
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            ContactAdvanceRuntime.Refresh(__instance.GetFaction(), _contactAdvance, map, manager.m_Round);
            ContactAdvanceRuntime.Alert(__instance, _contactAdvance, map, manager.m_Round);
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceDetected(Actor __instance, int __0)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || (__0 != 6 && __0 != 9)) return;
        if (Environment.CurrentManagedThreadId != _contactAdvanceThread)
        {
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(Volatile.Read(ref _contactAdvanceRound), __0, 0, "detection-thread",
                    $"result=thread-mismatch actor={__instance?.Pointer.ToInt64() ?? 0:X} thread={Environment.CurrentManagedThreadId} expected={_contactAdvanceThread}");
            return;
        }
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null) return;
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            var faction = manager.GetFaction(__0)?.TryCast<AIFaction>();
            var result = ContactAdvanceRuntime.Observe(faction, __instance, _contactAdvance, map, manager.m_Round);
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(manager.m_Round, faction?.Pointer.ToInt64() ?? __0,
                    __instance?.Pointer.ToInt64() ?? 0, "detection", $"result={result} factionType={__0}");
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    internal static void ContactAdvanceDiagnostic(int round, long faction, long actor, string phase, string details)
    {
        try
        {
            if (_contactAdvanceDiagnostics && _contactAdvanceDiagnosticBudget.TryAcquire(round, faction, actor, phase))
                _log.Msg($"{ModName}: CONTACT-ADVANCE round={round} faction={faction:X} actor={actor:X} phase={phase} {details}");
        }
        catch { } // Optional diagnostics must never disable or alter native AI.
    }

    internal static void ContactAdvanceDiagnosticReset()
    {
        _contactAdvanceDiagnosticBudget.Reset();
        Volatile.Write(ref _contactAdvanceRound, 0);
    }

    private static void ContactAdvanceDisable(Exception ex)
    {
        _contactAdvanceEnabled = false; _contactAdvance.Reset(); ContactAdvanceZoneReset();
        _log.Warning($"{ModName}: contact advance disabled ({ex.GetType().Name}); native AI retained.");
    }
}
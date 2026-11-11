using System;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors.Criterions;
using Il2CppMenace.Tactical.AI.Data;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

public sealed partial class Plugin
{
    // Reviewed native ConsiderZones.Evaluate/PostProcess skip AdvanceTowards zones for the whole
    // faction once AIFaction.HasKnownOpponent() is true. The override is scoped to those two
    // calls, including native AI workers, for eligible units without a useful native action.
    [ThreadStatic] private static bool _advanceZoneScope;
    [ThreadStatic] private static bool _advanceZoneKnownObserved;
    [ThreadStatic] private static long _advanceZoneScopeFaction;
    [ThreadStatic] private static long _advanceZoneScopeActor;
    [ThreadStatic] private static int _advanceZoneScopeRound;
    [ThreadStatic] private static string _advanceZoneScopePhase;
    [ThreadStatic] private static long _advanceZoneActor;
    [ThreadStatic] private static bool _advanceZoneEligible;
    [ThreadStatic] private static int _advanceZoneCachedGeneration;
    [ThreadStatic] private static int _advanceZoneScopeGeneration;
    private static int _advanceZoneGeneration;

    private static void PatchContactAdvanceZones(Harmony harmony)
    {
        var evaluate = AccessTools.Method(typeof(ConsiderZones), "Evaluate", new[] { typeof(Actor), typeof(TileScore) });
        var post = AccessTools.Method(typeof(ConsiderZones), "PostProcess", new[] { typeof(Actor), typeof(TileScores) });
        var known = AccessTools.Method(typeof(AIFaction), "HasKnownOpponent", Type.EmptyTypes);
        if (evaluate == null || post == null || known == null) throw new MissingMethodException("contact advance zones");
        // Exit before the advance recovery postfix so its own HasKnownOpponent read stays native.
        var evaluateEnter = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneEvaluateEnter)) { priority = Priority.First };
        var evaluateExit = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneEvaluateExit)) { priority = Priority.First };
        var postEnter = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZonePostEnter)) { priority = Priority.First };
        var postDone = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZonePostDone)) { priority = Priority.First };
        var cleanup = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneFinally));
        harmony.Patch(evaluate, prefix: evaluateEnter, postfix: evaluateExit, finalizer: cleanup);
        harmony.Patch(post, prefix: postEnter, postfix: postDone, finalizer: cleanup);
        harmony.Patch(known, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceKnownOpponent)));
    }

    private static void ContactAdvanceZoneEvaluateEnter(Actor __0, out bool __state)
        => ContactAdvanceZoneEnterCore(__0, "evaluate", out __state);

    private static void ContactAdvanceZonePostEnter(Actor __0, out bool __state)
        => ContactAdvanceZoneEnterCore(__0, "postprocess", out __state);

    private static void ContactAdvanceZoneEnter(Actor __0, out bool __state)
        => ContactAdvanceZoneEnterCore(__0, "fixture", out __state);

    private static void ContactAdvanceZoneEnterCore(Actor __0, string phase, out bool __state)
    {
        __state = _advanceZoneScope;
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || __0 == null) return;
        try
        {
            int generation = Volatile.Read(ref _advanceZoneGeneration);
            long id = __0.Pointer.ToInt64();
            var agent = __0.GetAgent();
            if (_advanceZoneActor != id || _advanceZoneCachedGeneration != generation)
            {
                _advanceZoneEligible = ContactAdvanceRuntime.KeepAdvanceZones(agent, out var gate);
                _advanceZoneActor = id;
                _advanceZoneCachedGeneration = generation;
                if (_contactAdvanceDiagnostics)
                    ContactAdvanceDiagnostic(Volatile.Read(ref _contactAdvanceRound), agent?.GetFaction()?.Pointer.ToInt64() ?? 0, 0,
                        "scope-eligibility", $"result={(_advanceZoneEligible ? "open" : "closed")} gate={gate} actor={id:X} thread={Environment.CurrentManagedThreadId}");
            }
            _advanceZoneScope = _advanceZoneEligible;
            _advanceZoneScopeFaction = agent?.GetFaction()?.Pointer.ToInt64() ?? 0;
            _advanceZoneScopeGeneration = generation;
            if (_contactAdvanceDiagnostics)
            {
                _advanceZoneKnownObserved = false;
                _advanceZoneScopeActor = id;
                _advanceZoneScopeRound = Volatile.Read(ref _contactAdvanceRound);
                _advanceZoneScopePhase = phase;
            }
        }
        catch
        {
            // Native worker failures must not clear main-thread contact memory mid-evaluation.
            _advanceZoneScope = false; _advanceZoneActor = 0;
        }
    }

    private static void ContactAdvanceZoneEvaluateExit(bool __state) => ContactAdvanceZoneExitCore(__state, "evaluate");
    private static void ContactAdvanceZonePostDone(bool __state) => ContactAdvanceZoneDoneCore(__state, "postprocess");
    private static void ContactAdvanceZoneExit(bool __state) => ContactAdvanceZoneExitCore(__state, "fixture");
    private static void ContactAdvanceZoneExitCore(bool __state, string phase)
    {
        if (_contactAdvanceDiagnostics && _advanceZoneScope && !_advanceZoneKnownObserved)
            ContactAdvanceDiagnostic(_advanceZoneScopeRound, _advanceZoneScopeFaction, 0, "known-check-" + phase,
                $"result=not-read actor={_advanceZoneScopeActor:X} thread={Environment.CurrentManagedThreadId}");
        _advanceZoneScope = __state;
    }

    // One eligibility read per Collect/Evaluate/PostProcess pass; later passes re-read.
    private static void ContactAdvanceZoneDone(bool __state) => ContactAdvanceZoneDoneCore(__state, "fixture");
    private static void ContactAdvanceZoneDoneCore(bool __state, string phase)
    {
        ContactAdvanceZoneExitCore(__state, phase);
        _advanceZoneActor = 0;
        _advanceZoneScopeActor = _advanceZoneScopeFaction = 0;
        _advanceZoneScopeRound = 0;
        _advanceZoneKnownObserved = false;
        _advanceZoneScopePhase = null;
    }

    private static void ContactAdvanceZoneFinally(Exception __exception, bool __state)
    {
        if (__exception == null) return;
        _advanceZoneScope = __state; _advanceZoneActor = 0;
    }

    private static bool ContactAdvanceKnownOpponent(AIFaction __instance, ref bool __result)
    {
        if (!_advanceZoneScope || !_contactAdvanceEnabled || !_offensivePlanningEnabled ||
            _advanceZoneScopeGeneration != Volatile.Read(ref _advanceZoneGeneration) ||
            __instance == null || __instance.Pointer.ToInt64() != _advanceZoneScopeFaction) return true;
        if (_contactAdvanceDiagnostics)
        {
            _advanceZoneKnownObserved = true;
            ContactAdvanceDiagnostic(_advanceZoneScopeRound, _advanceZoneScopeFaction, 0,
                "known-check-" + _advanceZoneScopePhase,
                $"result=overridden actor={_advanceZoneScopeActor:X} thread={Environment.CurrentManagedThreadId}");
        }
        __result = false;
        return false;
    }

    private static void ContactAdvanceZoneReset()
    {
        Interlocked.Increment(ref _advanceZoneGeneration);
        _advanceZoneScope = false; _advanceZoneKnownObserved = false; _advanceZoneActor = 0;
        _advanceZoneEligible = false; _advanceZoneScopeFaction = _advanceZoneScopeActor = 0;
        _advanceZoneScopeRound = 0; _advanceZoneScopePhase = null;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace LargerMaps;

/// <summary>
/// Opt-in wall-clock profiling for the existing MENACE AI path. This file NEVER
/// replaces, waits for, or cancels native tasks or changes game decisions.
/// All hooks are absent unless DiagnoseAIPerformance was ON at startup.
/// Pathfinding hooks are a second, separately opted-in diagnostic level.
/// Counters are in memory; output is capped to completed AI actor activations.
/// </summary>
public sealed partial class Plugin
{
    private enum AiPerfKind
    {
        AgentEvaluate, AgentExecute, BehaviorCollect, BehaviorExecute,
        PositionPostProcess, PickBehavior, MovementGetPath, PathProcessRequest,
        NativeFindPath, NativeFindPathForwardBackward, ScheduledTask, Count
    }

    private sealed class AiPerfAtomic
    {
        private long _calls, _sumTicks, _maxTicks;
        internal void Add(long ticks)
        {
            if (ticks < 0) return;
            Interlocked.Increment(ref _calls);
            Interlocked.Add(ref _sumTicks, ticks);
            long old;
            while (ticks > (old = Interlocked.Read(ref _maxTicks)) &&
                   Interlocked.CompareExchange(ref _maxTicks, ticks, old) != old) { }
        }
        internal long Calls => Interlocked.Read(ref _calls);
        internal long Total => Interlocked.Read(ref _sumTicks);
        internal long Max => Interlocked.Read(ref _maxTicks);
    }

    private sealed class AiPerfUnit
    {
        internal readonly long Key;
        internal string Name;
        internal readonly AiPerfAtomic Evaluate = new();
        internal readonly AiPerfAtomic Execute = new();
        internal readonly AiPerfAtomic Collect = new();
        internal readonly AiPerfAtomic Task = new();
        internal readonly AiPerfAtomic BehaviorExecute = new();
        internal readonly AiPerfAtomic MovementPath = new();
        internal readonly AiPerfAtomic NativePath = new();
        internal readonly AiPerfAtomic NativeForwardBack = new();
        // Created only during deep diagnostics. Key: Behavior.GetID(), not the
        // IL2CPP wrapper instance; no gameplay references are retained.
        internal readonly ConcurrentDictionary<string, AiPerfAtomic> BehaviorCollects = new(StringComparer.Ordinal);
        internal AiPerfUnit(long key) { Key = key; }
    }

    // Explicit result type: avoid nested LINQ anonymous-type projections,
    // which require compiler-generated NullableAttribute in the user's
    // older .NET 6 SDK / game interop reference set (CS0656).
    private sealed class AiPerfBehaviorRank
    {
        internal AiPerfUnit Unit;
        internal string Id;
        internal AiPerfAtomic Counter;
    }

    private sealed class AiPerfTurn
    {
        internal int Sequence;
        internal int Round;
        internal long Faction;
        internal long StartTicks;
        internal long LastHeartbeat;
        internal int Heartbeats;
        internal int Pending;
        internal int SlowSamples;
        internal int Sealed;
        internal long ClosedTicks;
        internal string ClosedBy;
        internal long PrimaryActorKey;
        internal long ScheduledActorKey;
        internal readonly AiPerfAtomic[] Metrics = new AiPerfAtomic[(int)AiPerfKind.Count];
        internal readonly ConcurrentDictionary<long, AiPerfUnit> Units = new();
        internal readonly ConcurrentQueue<string> Slow = new();
        internal AiPerfTurn()
        {
            for (int i = 0; i < Metrics.Length; i++) Metrics[i] = new AiPerfAtomic();
        }
    }

    // Store the measured phase at PREFIX time so an actor activation transition
    // does not reassign a slow worker's completed sample to the next actor.
    private struct AiPerfState
    {
        internal long Start;
        internal long ActorKey;
        internal AiPerfTurn Turn;
        internal string BehaviorId;
    }

    private sealed class AiPerfPendingTask
    {
        internal Il2CppSystem.Threading.Tasks.Task Task;
        internal AiPerfTurn Turn;
        internal long Started;
        internal long ActorKey;
    }

    private static volatile bool _aiPerfEnabled;
    private static volatile bool _aiPerfDeep;
    private static AiPerfTurn _aiPerfActive;
    private static int _aiPerfTurnCounter;
    private static int _aiPerfTrackedTaskCount;
    private static int _aiPerfDroppedTasks;
    private static int _aiPerfPollFailures;
    private static long _aiPerfNextPoll;
    private static readonly ConcurrentQueue<AiPerfPendingTask> _aiPerfIncomingTasks = new();
    // Only accessed from MelonLoader's main-thread OnUpdate / hook calls.
    private static readonly List<AiPerfPendingTask> _aiPerfPendingTasks = new();
    // OnTurnStart fires for successive actor activations, NOT just once per faction round.
    // A sealed activation is retained until its observed worker task has completed.
    // The incoming queue can be fed by hooks; the list is main-thread-only.
    private static readonly ConcurrentQueue<AiPerfTurn> _aiPerfSealedTurns = new();
    private static readonly List<AiPerfTurn> _aiPerfPendingReports = new();
    private static int _aiPerfDroppedReports;

    private const int MaxAiPerfTrackedTasks = 2048;
    private const int MaxAiPerfPendingReports = 64;
    private const int MaxAiPerfSlowSamples = 12;
    private const int MaxAiPerfUnitLines = 5;
    private const int MaxAiPerfBehaviorKindsPerUnit = 32;
    private const int MaxAiPerfBehaviorLines = 8;
    private static readonly long AiPerfSlowThreshold = 2 * Stopwatch.Frequency;

    private static void PatchAiPerformance(HarmonyLib.Harmony harmony)
    {
        if (!ReadBool("DiagnoseAIPerformance", false)) return;
        // Separate opt-in ensures no hot native path hook in the standard profile.
        _aiPerfDeep = ReadBool("DiagnoseAIPathfinding", false);
        int ok = 0, missing = 0;
        ok += TryPatchAiPerf(harmony, _aiFactionType, "OnTurnStart", 0,
            null, nameof(AiPerfTurnStart), "faction turn start", ref missing);
        ok += TryPatchAiPerf(harmony, _aiFactionType, "Finish", 0,
            null, nameof(AiPerfTurnFinished), "faction turn completion", ref missing);
        ok += TryPatchAiPerf(harmony, _aiFactionType, "RunWithTimeout", 2,
            null, nameof(AiPerfTaskScheduled), "actual worker task observation", ref missing);
        ok += TryPatchAiPerf(harmony, _agentType, "Evaluate", 0,
            nameof(AiPerfAgentStart), nameof(AiPerfAgentEvaluateEnd), "agent evaluation", ref missing);
        ok += TryPatchAiPerf(harmony, _agentType, "Execute", 0,
            nameof(AiPerfAgentStart), nameof(AiPerfAgentExecuteEnd), "agent execution", ref missing);
        int coreHooks = ok;
        // Stage 1 observes only faction/agent lifetimes. Stage 2 is deliberately
        // opt-in because candidate collection and path search are hot operations.
        if (_aiPerfDeep)
        {
            ok += TryPatchAiPerf(harmony, _agentType, "PostProcessTileScores", 0,
                nameof(AiPerfAgentStart), nameof(AiPerfPositionPostProcessEnd), "candidate postprocess", ref missing);
            ok += TryPatchAiPerf(harmony, _agentType, "PickBehavior", 0,
                nameof(AiPerfAgentStart), nameof(AiPerfPickBehaviorEnd), "behavior choice", ref missing);
            Type behavior = FindType("Il2CppMenace.Tactical.AI.Behavior") ?? FindType("Menace.Tactical.AI.Behavior");
            ok += TryPatchAiPerf(harmony, behavior, "Collect", 1,
                nameof(AiPerfBehaviorCollectStart), nameof(AiPerfBehaviorCollectEnd), "behavior collection with ID", ref missing);
            ok += TryPatchAiPerf(harmony, behavior, "Execute", 1,
                nameof(AiPerfActor0Start), nameof(AiPerfBehaviorExecuteEnd), "behavior execution", ref missing);
    
            Type movement = FindType("Il2CppTactical.MovementSystem") ?? FindType("Tactical.MovementSystem");
            ok += TryPatchAiPerf(harmony, movement, "GetPath", 4,
                nameof(AiPerfActor0Start), nameof(AiPerfMovementGetPathEnd), "movement GetPath", ref missing);
            Type manager = FindType("Il2CppMenace.Tactical.PathfindingManager") ?? FindType("Menace.Tactical.PathfindingManager");
            ok += TryPatchAiPerf(harmony, manager, "RequestProcess", 0,
                nameof(AiPerfGenericStart), nameof(AiPerfProcessRequestEnd), "path process contention", ref missing);
    
            ok += TryPatchAiPerf(harmony, _pathfindingProcessType, "FindPath", 7,
                nameof(AiPerfActor2Start), nameof(AiPerfFindPathEnd), "native path search", ref missing);
            ok += TryPatchAiPerf(harmony, _pathfindingProcessType, "FindPathForwardOrBackward", 7,
                nameof(AiPerfActor2Start), nameof(AiPerfFindPathForwardBackwardEnd), "forward/backward path search", ref missing);
        }
        // With diagnostics OFF, none of these hooks are installed. OnUpdate is a
        // single boolean branch. Stage 1 avoids hooking the hottest paths.
        _aiPerfEnabled = coreHooks == 5;
        if (!_aiPerfEnabled)
            _log.Warning($"{ModName}: AI PERF missing required core hooks ({coreHooks}/5); refusing incomplete profiling.");
        _log.Msg($"{ModName}: AI PERF diagnostic hooks={ok}, unavailable={missing}, deep-operations={_aiPerfDeep}; metrics are optional and not a performance fix.");
    }

    private static int TryPatchAiPerf(HarmonyLib.Harmony harmony, Type type, string name, int parameters,
        string prefixName, string postfixName, string description, ref int missing)
    {
        try
        {
            MethodInfo target = type?.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            if (target == null) { missing++; _log.Warning($"{ModName}: AI PERF missing {description} ({name}/{parameters})."); return 0; }
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo prefix = prefixName == null ? null : typeof(Plugin).GetMethod(prefixName, flags);
            MethodInfo postfix = typeof(Plugin).GetMethod(postfixName, flags);
            if (postfix == null || (prefixName != null && prefix == null)) throw new MissingMethodException("profile callback");
            harmony.Patch(target, prefix: prefix == null ? null : new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            return 1;
        }
        catch (Exception ex)
        {
            missing++;
            _log.Warning($"{ModName}: AI PERF {description} not installed: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    private static AiPerfTurn GetCurrentAiPerfTurn() => Volatile.Read(ref _aiPerfActive);
    private static long AiPerfNow() => Stopwatch.GetTimestamp();
    private static double AiPerfMs(long ticks) => ticks * (1000.0 / Stopwatch.Frequency);
    private static AiPerfUnit AiPerfGetUnit(AiPerfTurn turn, long actor)
        => turn.Units.GetOrAdd(actor, static key => new AiPerfUnit(key));

    private static void AiPerfTurnStart(object __instance)
    {
        if (!_aiPerfEnabled || __instance == null) return;
        try
        {
            int round = -1;
            object manager = InvokeStaticNoArg(_tacticalManagerType, "Get");
            if (manager != null) round = ToInt(GetMember(manager, "m_Round"), -1);
            var turn = new AiPerfTurn
            {
                Sequence = Interlocked.Increment(ref _aiPerfTurnCounter),
                Faction = GetStableObjectKey(__instance), Round = round,
                StartTicks = AiPerfNow()
            };
            // AIFaction.OnTurnStart has no Actor argument. Query the already active
            // actor only as a fallback label; RunWithTimeout's Actor argument is
            // preferred because it directly identifies the evaluated worker.
            try { AiPerfRememberActor(turn, InvokeNoArg(manager, "GetActiveActor") ?? GetMember(manager, "m_ActiveActor")); }
            catch { /* Optional actor labels must never affect AI execution. */ }
            AiPerfTurn previous = Interlocked.Exchange(ref _aiPerfActive, turn);
            // Normal: AIFaction.OnTurnStart happens again for the next actor. Never
            // drop the previous actor's expensive evaluation metrics on rollover.
            AiPerfSealTurn(previous, "next-activation");
        }
        catch (Exception ex) { _log.Warning($"{ModName}: AI PERF start failed: {ex.GetType().Name}"); }
    }

    private static void AiPerfTurnFinished(object __instance)
    {
        if (!_aiPerfEnabled) return;
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        if (turn == null) return;
        try
        {
            if (turn.Faction != GetStableObjectKey(__instance)) return;
            // Finish() is not guaranteed to run before the next OnTurnStart().
            // Both transitions seal an actor activation; neither changes AI execution.
            if (Interlocked.CompareExchange(ref _aiPerfActive, null, turn) == turn)
                AiPerfSealTurn(turn, "finish");
        }
        catch (Exception ex) { _log.Warning($"{ModName}: AI PERF close failed: {ex.GetType().Name}"); }
    }

    private static void AiPerfRememberActor(AiPerfTurn turn, object actor)
    {
        if (turn == null || actor == null) return;
        long key = GetStableObjectKey(actor);
        if (key == 0) return;
        Interlocked.CompareExchange(ref turn.PrimaryActorKey, key, 0);
        AiPerfUnit unit = AiPerfGetUnit(turn, key);
        if (unit.Name == null)
        {
            object template = InvokeNoArg(actor, "GetTemplate") ?? GetMember(actor, "m_Template");
            object id = GetMember(template, "m_ID") ?? GetMember(template, "ID");
            if (id != null) unit.Name = Safe(id);
        }
    }

    private static void AiPerfSealTurn(AiPerfTurn turn, string reason)
    {
        if (turn == null || Interlocked.CompareExchange(ref turn.Sealed, 1, 0) != 0) return;
        turn.ClosedTicks = AiPerfNow();
        turn.ClosedBy = reason;
        _aiPerfSealedTurns.Enqueue(turn);
    }

    // Called by OnUpdate after observing completed Tasks. Never waits, blocks,
    // cancels tasks or alters the game's turn state. Report a phase only when
    // its tracked worker tasks have actually completed, not on a later empty phase.
    private static void PublishAiPerfTurns(bool includeIncomplete = false)
    {
        while (_aiPerfSealedTurns.TryDequeue(out AiPerfTurn turn))
        {
            if (_aiPerfPendingReports.Count >= MaxAiPerfPendingReports)
            {
                if (Interlocked.Increment(ref _aiPerfDroppedReports) == 1)
                    _log.Warning($"{ModName}: AI PERF report buffer full; dropping subsequent diagnostic summaries.");
                continue;
            }
            _aiPerfPendingReports.Add(turn);
        }
        for (int i = 0; i < _aiPerfPendingReports.Count;)
        {
            AiPerfTurn turn = _aiPerfPendingReports[i];
            if (!includeIncomplete && Volatile.Read(ref turn.Pending) != 0)
            {
                i++;
                continue;
            }
            try { ReportAiPerfTurn(turn); }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref _aiPerfPollFailures) <= 3)
                    _log.Warning($"{ModName}: AI PERF report failed: {ex.GetType().Name}.");
            }
            _aiPerfPendingReports.RemoveAt(i);
        }
    }

    private static void ReportAiPerfTurn(AiPerfTurn turn)
    {
        long totalTicks = Math.Max(0, turn.ClosedTicks - turn.StartTicks);
        var a = turn.Metrics;
        long scheduledActor = Interlocked.Read(ref turn.ScheduledActorKey);
        long actor = scheduledActor != 0 ? scheduledActor : Interlocked.Read(ref turn.PrimaryActorKey);
        string template = "<unknown>";
        if (actor != 0 && turn.Units.TryGetValue(actor, out AiPerfUnit primary))
            template = primary.Name ?? "<unknown>";
        _log.Msg($"{ModName}: AI PERF turn=#{turn.Sequence} round={turn.Round} faction=0x{turn.Faction:X} " +
            $"wall={AiPerfMs(totalTicks):F1}ms pending={Volatile.Read(ref turn.Pending)} " +
            $"droppedTasks={Volatile.Read(ref _aiPerfDroppedTasks)} " +
            $"scope=actor-activation actor=0x{actor:X} template={template} closedBy={turn.ClosedBy} " +
            $"evaluate={AiPerfFmt(a[(int)AiPerfKind.AgentEvaluate])} task-observed={AiPerfFmt(a[(int)AiPerfKind.ScheduledTask])} " +
            $"execute={AiPerfFmt(a[(int)AiPerfKind.AgentExecute])}.");
        _log.Msg($"{ModName}: AI PERF ops turn=#{turn.Sequence} " +
            $"collect={AiPerfFmt(a[(int)AiPerfKind.BehaviorCollect])} " +
            $"behavior-exec={AiPerfFmt(a[(int)AiPerfKind.BehaviorExecute])} " +
            $"postprocess={AiPerfFmt(a[(int)AiPerfKind.PositionPostProcess])} " +
            $"pick={AiPerfFmt(a[(int)AiPerfKind.PickBehavior])} " +
            $"movement-path={AiPerfFmt(a[(int)AiPerfKind.MovementGetPath])} " +
            $"path-pool={AiPerfFmt(a[(int)AiPerfKind.PathProcessRequest])} " +
            (_aiPerfDeep
                ? $"findpath={AiPerfFmt(a[(int)AiPerfKind.NativeFindPath])} " +
                  $"forward-back={AiPerfFmt(a[(int)AiPerfKind.NativeFindPathForwardBackward])}."
                : "native-path=not-profiled (enable DiagnoseAIPathfinding for detail)."));
        foreach (AiPerfUnit unit in turn.Units.Values
            .OrderByDescending(x => Math.Max(Math.Max(x.Evaluate.Total, x.Task.Total),
                Math.Max(x.MovementPath.Total, x.NativePath.Total))).Take(MaxAiPerfUnitLines))
        {
            _log.Msg($"{ModName}: AI PERF unit turn=#{turn.Sequence} actor=0x{unit.Key:X} " +
                $"template={unit.Name ?? "<unknown>"} evaluate={AiPerfFmt(unit.Evaluate)} " +
                $"task-observed={AiPerfFmt(unit.Task)} execute={AiPerfFmt(unit.Execute)} " +
                (_aiPerfDeep ? $"collect-slow={AiPerfFmt(unit.Collect)} " +
                    $"behavior-exec={AiPerfFmt(unit.BehaviorExecute)} " +
                    $"movement-path={AiPerfFmt(unit.MovementPath)} " +
                    $"findpath={AiPerfFmt(unit.NativePath)} " +
                    $"forward-back={AiPerfFmt(unit.NativeForwardBack)}." : "."));
        }
        if (_aiPerfDeep)
        {
            // One bounded ranking across the entire activation. These are
            // per-behavior, per-actor totals; their nested / parallel sums
            // must never be added to wall-clock or Agent.Evaluate totals.
            var ranking = new List<AiPerfBehaviorRank>();
            foreach (AiPerfUnit unit in turn.Units.Values)
            {
                foreach (KeyValuePair<string, AiPerfAtomic> pair in unit.BehaviorCollects)
                {
                    if (pair.Value.Calls == 0 || pair.Value.Max < Stopwatch.Frequency / 20)
                        continue;
                    ranking.Add(new AiPerfBehaviorRank
                    {
                        Unit = unit, Id = pair.Key, Counter = pair.Value
                    });
                }
            }
            ranking.Sort((left, right) =>
            {
                int byTotal = right.Counter.Total.CompareTo(left.Counter.Total);
                return byTotal != 0 ? byTotal : right.Counter.Max.CompareTo(left.Counter.Max);
            });
            for (int i = 0; i < ranking.Count && i < MaxAiPerfBehaviorLines; i++)
            {
                AiPerfBehaviorRank hot = ranking[i];
                _log.Msg($"{ModName}: AI PERF behavior turn=#{turn.Sequence} actor=0x{hot.Unit.Key:X} " +
                    $"template={hot.Unit.Name ?? "<unknown>"} id={hot.Id} " +
                    $"collect={AiPerfFmt(hot.Counter)}.");
            }
        }
        int n = 0;
        while (n < MaxAiPerfSlowSamples && turn.Slow.TryDequeue(out string slow))
        {
            _log.Msg($"{ModName}: AI PERF slow turn=#{turn.Sequence} {slow}");
            n++;
        }
    }

    private static string AiPerfFmt(AiPerfAtomic metric)
        => $"n={metric.Calls},sum={AiPerfMs(metric.Total):F1}ms,max={AiPerfMs(metric.Max):F1}ms";

    private static void AiPerfAgentStart(object __instance, out AiPerfState __state)
    {
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        long key = 0;
        try
        {
            if (turn != null && __instance != null)
            {
                object actor = InvokeNoArg(__instance, "GetActor");
                key = GetStableObjectKey(actor);
                if (key != 0) Interlocked.CompareExchange(ref turn.PrimaryActorKey, key, 0);
            }
        }
        catch { /* Never make AI fail because of an actor label. */ }
        __state = new AiPerfState { Start = AiPerfNow(), Turn = turn, ActorKey = key };
    }

    private static void AiPerfGenericStart(out AiPerfState __state)
        => __state = new AiPerfState { Start = AiPerfNow(), Turn = GetCurrentAiPerfTurn() };

    private static void AiPerfActor0Start(object __0, out AiPerfState __state)
    {
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        long actor = 0;
        try { if (turn != null) actor = GetStableObjectKey(__0); }
        catch { /* Optional label only. */ }
        __state = new AiPerfState { Start = AiPerfNow(), Turn = turn, ActorKey = actor };
    }

    private static void AiPerfBehaviorCollectStart(object __instance, object __0, out AiPerfState __state)
    {
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        long actor = 0;
        string id = "<unknown>";
        try
        {
            if (turn != null)
            {
                actor = GetStableObjectKey(__0);
                // MENACE's abstract Behavior.GetID() is implemented by each
                // concrete behavior. Only read its stable label, never mutate
                // the behavior or inspect its tile candidates.
                object value = InvokeNoArg(__instance, "GetID");
                if (value == null) value = InvokeNoArg(__instance, "GetName");
                string label = value == null ? __instance?.GetType().Name : Safe(value);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    // Keep log lines single-token and bounded even with mods.
                    label = label.Replace(' ', '_').Replace('\t', '_')
                        .Replace('\r', '_').Replace('\n', '_');
                    id = label.Length <= 64 ? label : label.Substring(0, 64);
                }
            }
        }
        catch { /* Deep diagnostic labeling must never break native AI. */ }
        // Start after optional label lookup so the measured Collect interval
        // excludes profiler reflection/marshalling overhead.
        __state = new AiPerfState
        {
            Start = AiPerfNow(), Turn = turn, ActorKey = actor, BehaviorId = id
        };
    }

    private static void AiPerfActor2Start(object __2, out AiPerfState __state)
    {
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        long actor = 0;
        try { if (turn != null) actor = GetStableObjectKey(__2); }
        catch { /* Optional label only. */ }
        __state = new AiPerfState { Start = AiPerfNow(), Turn = turn, ActorKey = actor };
    }

    private static long AiPerfRecord(AiPerfState state, AiPerfKind kind)
    {
        long elapsed = AiPerfNow() - state.Start;
        if (state.Turn != null)
        {
            state.Turn.Metrics[(int)kind].Add(elapsed);
            if (state.ActorKey != 0 && kind != AiPerfKind.AgentEvaluate &&
                kind != AiPerfKind.AgentExecute && kind != AiPerfKind.ScheduledTask)
            {
                AiPerfUnit unit = AiPerfGetUnit(state.Turn, state.ActorKey);
                switch (kind)
                {
                    case AiPerfKind.BehaviorExecute: unit.BehaviorExecute.Add(elapsed); break;
                    case AiPerfKind.MovementGetPath: unit.MovementPath.Add(elapsed); break;
                    case AiPerfKind.NativeFindPath: unit.NativePath.Add(elapsed); break;
                    case AiPerfKind.NativeFindPathForwardBackward: unit.NativeForwardBack.Add(elapsed); break;
                    case AiPerfKind.BehaviorCollect:
                        if (elapsed >= Stopwatch.Frequency / 200) unit.Collect.Add(elapsed);
                        // Aggregate by the actual AI Behavior.GetID() for each
                        // actor. No logging or new hooks on the hot path.
                        if (!string.IsNullOrEmpty(state.BehaviorId) &&
                            (unit.BehaviorCollects.Count < MaxAiPerfBehaviorKindsPerUnit ||
                             unit.BehaviorCollects.ContainsKey(state.BehaviorId)))
                            unit.BehaviorCollects.GetOrAdd(state.BehaviorId,
                                static _ => new AiPerfAtomic()).Add(elapsed);
                        break;
                }
            }
        }
        if (state.Turn != null && elapsed >= AiPerfSlowThreshold)
            AiPerfSlow(state.Turn, kind, elapsed, state.ActorKey,
                kind == AiPerfKind.BehaviorCollect ? state.BehaviorId : null);
        return elapsed;
    }

    private static void AiPerfSlow(AiPerfTurn turn, AiPerfKind kind, long elapsed, long key, string behaviorId = null)
    {
        if (Interlocked.Increment(ref turn.SlowSamples) <= MaxAiPerfSlowSamples)
            turn.Slow.Enqueue($"op={kind} actor=0x{key:X} " +
                (behaviorId == null ? "" : $"id={behaviorId} ") +
                $"elapsed={AiPerfMs(elapsed):F1}ms");
    }

    private static void AiPerfAgentEvaluateEnd(AiPerfState __state)
    {
        try
        {
            long ticks = AiPerfRecord(__state, AiPerfKind.AgentEvaluate);
            if (__state.Turn != null) AiPerfGetUnit(__state.Turn, __state.ActorKey).Evaluate.Add(ticks);
        }
        catch { /* Optional profiler must not interrupt real AI tasks. */ }
    }
    private static void AiPerfAgentExecuteEnd(AiPerfState __state)
    {
        try
        {
            long ticks = AiPerfRecord(__state, AiPerfKind.AgentExecute);
            if (__state.Turn != null) AiPerfGetUnit(__state.Turn, __state.ActorKey).Execute.Add(ticks);
        }
        catch { /* Optional profiler must not interrupt real AI tasks. */ }
    }
    private static void AiPerfBehaviorCollectEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.BehaviorCollect); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfBehaviorExecuteEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.BehaviorExecute); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfPositionPostProcessEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.PositionPostProcess); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfPickBehaviorEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.PickBehavior); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfMovementGetPathEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.MovementGetPath); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfProcessRequestEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.PathProcessRequest); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfFindPathEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.NativeFindPath); }
        catch { /* Best-effort diagnostic only. */ }
    }
    private static void AiPerfFindPathForwardBackwardEnd(AiPerfState __state)
    {
        try { AiPerfRecord(__state, AiPerfKind.NativeFindPathForwardBackward); }
        catch { /* Best-effort diagnostic only. */ }
    }

    // The existing safety PREFIX returns the *actual* Task.Run(__0) as __result.
    // Harmony postfixes also execute when an earlier prefix skips the original.
    // Observing IsCompleted never waits for/cancels that worker.
    private static void AiPerfTaskScheduled(object __1, Il2CppSystem.Threading.Tasks.Task __result)
    {
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        if (turn == null || __result == null) return;
        bool counted = false, markedPending = false;
        try
        {
            if (Interlocked.Increment(ref _aiPerfTrackedTaskCount) > MaxAiPerfTrackedTasks)
            {
                Interlocked.Decrement(ref _aiPerfTrackedTaskCount);
                Interlocked.Increment(ref _aiPerfDroppedTasks);
                return;
            }
            counted = true;
            long actorKey = GetStableObjectKey(__1);
            if (actorKey != 0) Interlocked.CompareExchange(ref turn.ScheduledActorKey, actorKey, 0);
            AiPerfRememberActor(turn, __1);
            var unit = AiPerfGetUnit(turn, actorKey);
            if (unit.Name == null)
            {
                object template = InvokeNoArg(__1, "GetTemplate");
                unit.Name = Safe(GetMember(template, "m_ID") ?? GetMember(template, "ID"));
            }
            long scheduledAt = AiPerfNow();
            var tracked = new AiPerfPendingTask
            {
                Task = __result, Turn = turn, ActorKey = actorKey, Started = scheduledAt
            };
            Interlocked.Increment(ref turn.Pending);
            markedPending = true;
            _aiPerfIncomingTasks.Enqueue(tracked);
            // Ownership of this counter is transferred to PollAiPerfTasks.
            counted = false;
            markedPending = false;
        }
        catch (Exception ex)
        {
            if (markedPending) Interlocked.Decrement(ref turn.Pending);
            if (counted) Interlocked.Decrement(ref _aiPerfTrackedTaskCount);
            // No instrumentation fault is allowed to abort the native AI callback.
            if (Interlocked.Increment(ref _aiPerfDroppedTasks) <= 3)
                _log.Warning($"{ModName}: AI PERF dropped task sample: {ex.GetType().Name}.");
        }
    }

    private static void PollAiPerformance()
    {
        long now = AiPerfNow();
        if (now < Interlocked.Read(ref _aiPerfNextPoll)) return;
        Interlocked.Exchange(ref _aiPerfNextPoll, now + Stopwatch.Frequency / 2);
        PollAiPerfTasks();
        PublishAiPerfTurns();
        AiPerfTurn turn = GetCurrentAiPerfTurn();
        if (turn == null) return;
        if (now - turn.StartTicks < 10 * Stopwatch.Frequency ||
            now - Interlocked.Read(ref turn.LastHeartbeat) < 10 * Stopwatch.Frequency ||
            Volatile.Read(ref turn.Heartbeats) >= 6) return;
        Interlocked.Exchange(ref turn.LastHeartbeat, now);
        Interlocked.Increment(ref turn.Heartbeats);
        _log.Msg($"{ModName}: AI PERF heartbeat turn=#{turn.Sequence} round={turn.Round} " +
            $"wall={AiPerfMs(now - turn.StartTicks):F0}ms pendingTasks={Volatile.Read(ref turn.Pending)} " +
            $"agentEvaluationsCompleted={turn.Metrics[(int)AiPerfKind.AgentEvaluate].Calls}.");
    }

    private static void PollAiPerfTasks()
    {
        while (_aiPerfIncomingTasks.TryDequeue(out AiPerfPendingTask queued))
            _aiPerfPendingTasks.Add(queued);
        for (int i = _aiPerfPendingTasks.Count - 1; i >= 0; i--)
        {
            AiPerfPendingTask p = _aiPerfPendingTasks[i];
            bool completed;
            try { completed = p.Task != null && p.Task.IsCompleted; }
            catch
            {
                // A destroyed IL2CPP wrapper is not evidence of completion.
                // Drop this diagnostic sample; never touch the original task.
                Interlocked.Increment(ref _aiPerfDroppedTasks);
                Interlocked.Decrement(ref p.Turn.Pending);
                Interlocked.Decrement(ref _aiPerfTrackedTaskCount);
                _aiPerfPendingTasks.RemoveAt(i);
                continue;
            }
            if (!completed) continue;
            long elapsed = AiPerfNow() - p.Started;
            p.Turn.Metrics[(int)AiPerfKind.ScheduledTask].Add(elapsed);
            AiPerfGetUnit(p.Turn, p.ActorKey).Task.Add(elapsed);
            if (elapsed >= AiPerfSlowThreshold)
                AiPerfSlow(p.Turn, AiPerfKind.ScheduledTask, elapsed, p.ActorKey);
            Interlocked.Decrement(ref p.Turn.Pending);
            Interlocked.Decrement(ref _aiPerfTrackedTaskCount);
            _aiPerfPendingTasks.RemoveAt(i);
        }
    }
}

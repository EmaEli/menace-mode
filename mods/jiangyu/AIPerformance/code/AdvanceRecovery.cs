using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Behaviors.Criterions;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly AdvanceRecoveryState _advanceRecovery = new();
    private static volatile bool _advanceRecoveryEnabled;
    private static bool _advanceRecoveryDiagnostics;
    private static int _advanceRecoveryLogCount;

    private static void PatchAdvanceRecovery(HarmonyLib.Harmony harmony)
    {
        if (!ReadBool("RecoverStalledAdvance", true)) return;
        try
        {
            if (!AdvanceRecoveryBuild.IsCurrentSupported())
            {
                _log.Warning($"{ModName}: advance recovery requires a reviewed 0.7.17/0.7.18 binary; native AI retained.");
                return;
            }
            // Resolve every hook before installing any. A partial install remains inert.
            var scores = AccessTools.Method(typeof(ConsiderZones), "PostProcess", new[] { typeof(Actor), typeof(TileScores) });
            var choice = AccessTools.Method(typeof(Agent), "PickBehavior", Type.EmptyTypes);
            var utility = AccessTools.Method(typeof(Move), "HasUtility", new[] { typeof(TileScores) });
            var reset = typeof(TacticalManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "OnInit" && m.GetParameters().Length == 1);
            if (scores == null || choice == null || utility == null) throw new MissingMethodException("advance recovery hooks");
            // First managed calls can originate on IL2CPP workers (#44).
            foreach (Type type in new[] { typeof(AdvanceRecoveryRuntime), typeof(AdvanceRecoveryState), typeof(AdvanceRecoveryOrder), typeof(Plugin) })
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (type != typeof(Plugin) || method.Name.StartsWith("AdvanceRecovery", StringComparison.Ordinal))
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
            harmony.Patch(scores, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryScores)));
            harmony.Patch(choice, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryChoice)));
            harmony.Patch(utility, prefix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryUtility)));
            harmony.Patch(reset, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryReset)));
            _advanceRecoveryDiagnostics = ReadBool("DiagnoseAdvanceRecovery", false);
            _advanceRecoveryEnabled = true;
            _log.Msg($"{ModName}: conditional advance recovery enabled on maps above 64x64; native Move/pathfinding retained.");
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryReset()
    {
        _advanceRecovery.Reset();
        Interlocked.Exchange(ref _advanceRecoveryLogCount, 0);
    }

    private static void AdvanceRecoveryUtility(Move __instance, TileScores __0)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            if (map == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            var actor = __instance?.GetAgent()?.GetActor();
            if (AdvanceRecoveryRuntime.TrySeedAdvanceUtility(__instance, actor, __0, _advanceRecovery,
                map.Pointer.ToInt64(), manager.m_Round, out int seeded) && _advanceRecoveryDiagnostics)
                AdvanceRecoveryLog($"ADVANCE-UTILITY round={manager.m_Round} actor={actor.Pointer.ToInt64():X} seeded={seeded}");
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryScores(Actor __0, TileScores __1)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            if (map == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            long mapId = map.Pointer.ToInt64();
            bool applied = AdvanceRecoveryRuntime.TryApply(__0, __1, _advanceRecovery, mapId, manager.m_Round, _advanceRecoveryDiagnostics,
                out var result, out bool diagnosticCandidate, out bool stalled);
            bool recoveryApplied = applied || (__0 != null && _advanceRecovery.HasRecoveryApplication(
                mapId, manager.m_Round, __0.Pointer.ToInt64(), __0.GetAgent()?.Pointer.ToInt64() ?? 0));
            if (_advanceRecoveryDiagnostics && __0 != null && diagnosticCandidate &&
                (stalled || result != AdvanceRecoveryResult.History))
            {
                try
                {
                    long actorId = __0.Pointer.ToInt64();
                    long agentId = __0.GetAgent()?.Pointer.ToInt64() ?? 0;
                    _advanceRecovery.RecordRecoveryCheck(mapId, manager.m_Round, actorId, agentId, (int)result, recoveryApplied);
                    if (_advanceRecovery.ShouldLogDiagnostic(mapId, actorId, (int)result))
                        AdvanceRecoveryLog($"ADVANCE-CHECK round={manager.m_Round} actor={actorId:X} result={result} " +
                            $"armed={stalled} applied={recoveryApplied} tiles={__1?.Count ?? 0} " + _advanceRecovery.Describe(actorId));
                }
                catch { }
            }
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryChoice(Agent __instance, ref Behavior __result)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            var actor = __instance?.GetActor();
            if (map == null || actor == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            long mapId = map.Pointer.ToInt64();
            long actorId = actor.Pointer.ToInt64();
            long agentId = __instance.Pointer.ToInt64();
            Move advanceMove = null;
            bool eligible = _advanceRecovery.HasMoveFallbackEligibility(mapId, manager.m_Round, actorId, agentId) &&
                AdvanceRecoveryRuntime.ValidateFallbackContext(actor, _advanceRecovery, mapId, manager.m_Round);
            bool forcedMove = eligible &&
                AdvanceRecoveryRuntime.TryGetAdvanceMove(__instance, actor, __result, out advanceMove);
            if (forcedMove) __result = advanceMove;
            bool provisionalAdvance = eligible &&
                AdvanceRecoveryRuntime.IsProvisionalAdvanceChoice(__instance, actor, __result);
            bool idleOnly = AdvanceRecoveryRuntime.IdleOnly(__instance, __result);
            string choiceDetails = null;
            if (_advanceRecoveryDiagnostics)
            {
                try
                {
                    string chosen = __result == null ? "null" : __result.GetID().ToString();
                    var scores = AdvanceRecoveryChoiceScores(__instance);
                    string signature = $"{chosen}|{scores.Move}|{scores.Idle}|{idleOnly}|{forcedMove}";
                    if (_advanceRecovery.ShouldLogChoice(mapId, manager.m_Round, actorId, agentId, signature,
                        out int recoveryResult, out bool recoveryApplied))
                        choiceDetails = $"recovery={(AdvanceRecoveryResult)recoveryResult} applied={recoveryApplied} " +
                            $"chosen={chosen} moveScore={scores.Move} idleScore={scores.Idle} idleOnly={idleOnly} forcedMove={forcedMove}";
                }
                catch { }
            }
            _advanceRecovery.Choice(mapId, manager.m_Round, actorId, agentId, idleOnly, provisionalAdvance);
            if (choiceDetails != null)
                AdvanceRecoveryLog($"ADVANCE-CHOICE round={manager.m_Round} actor={actorId:X} {choiceDetails} " +
                    _advanceRecovery.Describe(actorId));
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static (string Move, string Idle) AdvanceRecoveryChoiceScores(Agent agent)
    {
        string move = "missing", idle = "missing";
        foreach (Behavior behavior in agent.GetBehaviors())
        {
            if (behavior == null) continue;
            string id = behavior.GetID().ToString();
            string score = behavior.GetScore().ToString(CultureInfo.InvariantCulture);
            if (id == "Move") move = score;
            else if (id == "Idle") idle = score;
        }
        return (move, idle);
    }

    private static void AdvanceRecoveryLog(string message)
    {
        // Diagnostics must never disable gameplay. Bounded to 200 lines per mission.
        try { if (Interlocked.Increment(ref _advanceRecoveryLogCount) <= 200) _log.Msg($"{ModName}: {message}"); }
        catch { }
    }

    private static void AdvanceRecoveryDisable(Exception ex)
    {
        _advanceRecoveryEnabled = false;
        AdvanceRecoveryReset();
        _log.Warning($"{ModName}: advance recovery disabled ({ex.GetType().Name}); subsequent evaluations retain native AI.");
    }
}
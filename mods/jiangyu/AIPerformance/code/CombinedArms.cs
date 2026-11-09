using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly CombinedArmsState _combinedArms = new();
    private static readonly CombinedArmsThreatState _supportThreats = new();
    private static readonly CombinedArmsDiagnosticBudget _combinedArmsDiagnosticBudget = new();
    private static volatile bool _combinedArmsEnabled;
    private static volatile bool _combinedArmsDiagnostics;
    private static int _combinedArmsThread;

    private static void PatchCombinedArms(Harmony harmony)
    {
        if (!ReadBool("CombinedArmsSupport", false)) return;
        try
        {
            if (!ContactConvergenceBuild.IsCurrentSupported()) throw new NotSupportedException("reviewed 0.7.18 required");
            var turn = AccessTools.Method(typeof(AIFaction), "OnTurnStart", Type.EmptyTypes);
            var before = AccessTools.Method(typeof(Agent), "OnBeforeProcessing", Type.EmptyTypes);
            var after = AccessTools.Method(typeof(Agent), "OnAfterProcessing", Type.EmptyTypes);
            var death = AccessTools.Method(typeof(Actor), "OnDeath", Type.EmptyTypes);
            var movement = AccessTools.Method(typeof(Actor), "OnMovementFinished", Type.EmptyTypes);
            var reset = AccessTools.Method(typeof(TacticalManager), "OnInit");
            var detection = AccessTools.Method(typeof(Actor), "SetDetectedByFaction", new[] { typeof(int) });
            if (turn == null || before == null || after == null || death == null || movement == null || reset == null || detection == null)
                throw new MissingMethodException("support lifecycle");
            _combinedArmsThread = Thread.CurrentThread.ManagedThreadId;
            foreach (var type in new[] { typeof(CombinedArmsState), typeof(CombinedArmsRuntime), typeof(SupportProjection),
                typeof(SupportUnit), typeof(CombinedArmsDiagnosticBudget), typeof(CombinedArmsThreatRuntime),
                typeof(CombinedArmsThreatState), typeof(SupportThreat) })
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    foreach (var method in nested.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                        BindingFlags.Instance | BindingFlags.DeclaredOnly)) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    foreach (var constructor in nested.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
                }
            }
            foreach (string name in new[] { nameof(CombinedArmsTurn), nameof(CombinedArmsRefresh), nameof(CombinedArmsReset),
                nameof(CombinedArmsDeath), nameof(CombinedArmsMovement), nameof(CombinedArmsDiagnostic), nameof(CombinedArmsDetected) })
                RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).MethodHandle);
            var warmAsset = new SupportUnit(1, 2, SupportKind.Vehicle, 0, 0, default, false, 0, 0);
            var warmRequest = new SupportUnit(3, 4, SupportKind.Infantry, 6, 0, default, true, 10, 0);
            var warmDiagnostics = new CombinedArmsDiagnosticBudget();
            warmDiagnostics.TryAcquire(1, 1, 1, "warmup"); warmDiagnostics.Reset();
            _combinedArms.Publish(1, 1, 1, new[] { warmAsset, warmRequest });
            _combinedArms.Candidates(1, 1, 1, warmAsset); _combinedArms.Claim(1, 1, 1, warmAsset, warmRequest);
            _combinedArms.Refresh(1, 1, 1, warmRequest.Actor, null); _combinedArms.Release(1, 1, 1, warmAsset.Actor);
            _combinedArms.Reset();
            _supportThreats.Advance(1, 1); _supportThreats.Observe(1, 1, 1, new(1, 2, 2, 20, 1));
            _supportThreats.Get(1, 1, 1); _supportThreats.Reset();
            _combinedArmsDiagnostics = ReadBool("DiagnoseCombinedArmsSupport", false);
            harmony.Patch(turn, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsTurn)));
            harmony.Patch(before, prefix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsRefresh)));
            harmony.Patch(after, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsRefresh)));
            harmony.Patch(death, prefix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsDeath)));
            harmony.Patch(movement, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsMovement)));
            harmony.Patch(reset, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsReset)));
            harmony.Patch(detection, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsDetected)));
            _combinedArmsEnabled = true;
            _log.Msg($"{ModName}: experimental combined-arms support enabled; pirate/Rogue rear support, native setup/fire; no reaction-fire changes.");
            if (_combinedArmsDiagnostics)
                _log.Msg($"{ModName}: combined-arms diagnostics enabled; at most {CombinedArmsDiagnosticBudget.MaxLines} deduplicated lines per mission.");
        }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    private static void CombinedArmsTurn(AIFaction __instance)
    {
        if (!_combinedArmsEnabled || Thread.CurrentThread.ManagedThreadId != _combinedArmsThread) return;
        try
        {
            if (!ContactConvergenceRuntime.EligibleFaction(__instance)) return;
            var manager = TacticalManager.Get(); var map = manager?.GetMap(); var actors = __instance.GetActors();
            if (map == null || actors == null) return;
            if (!_supportThreats.Advance(map.Pointer.ToInt64(), manager.m_Round)) return;
            long factionId = __instance.Pointer.ToInt64();
            if (actors.Count > CombinedArmsState.MaxUnits)
            {
                if (_combinedArmsDiagnostics)
                    CombinedArmsDiagnostic(manager.m_Round, factionId, 0, "publish",
                        $"result=actor-limit actors={actors.Count} limit={CombinedArmsState.MaxUnits}");
                return;
            }
            if (!_combinedArms.NeedsPublish(map.Pointer.ToInt64(), manager.m_Round, __instance.Pointer.ToInt64())) return;
            var units = new List<SupportUnit>();
            int[] rejected = _combinedArmsDiagnostics ? new int[(int)SupportSnapshotReason.Count] : null;
            foreach (var actor in actors)
            {
                var unit = CombinedArmsRuntime.Snapshot(actor?.GetAgent(), true, out var reason);
                if (unit != null) units.Add(unit);
                else if (rejected != null) rejected[(int)reason]++;
            }
            bool published = _combinedArms.Publish(map.Pointer.ToInt64(), manager.m_Round, factionId, units);
            // Establish legitimate observations after loading a mission too; never use TTL coordinates.
            var opponents = __instance.GetOpponents();
            if (published && opponents != null && opponents.Count <= 128)
            {
                int observed = 0;
                foreach (var opponent in opponents)
                {
                    var enemy = opponent?.Actor;
                    if (enemy == null || !enemy.IsDetectedByFaction((int)__instance.GetFactionType())) continue;
                    CombinedArmsThreatRuntime.Observe(__instance, enemy, _supportThreats, map, manager.m_Round);
                    if (++observed == 4) break;
                }
            }
            if (rejected != null)
            {
                int infantry = 0, vehicles = 0, heavy = 0, ordered = 0;
                foreach (var unit in units)
                {
                    if (unit.Kind == SupportKind.Infantry) infantry++;
                    else if (unit.Kind == SupportKind.Vehicle) vehicles++;
                    else heavy++;
                    if (unit.HasOrder) ordered++;
                }
                var reasons = new List<string>();
                for (int i = 1; i < rejected.Length; i++)
                    if (rejected[i] > 0) reasons.Add($"{(SupportSnapshotReason)i}:{rejected[i]}");
                CombinedArmsDiagnostic(manager.m_Round, factionId, 0, "publish",
                    $"result={(published ? "published" : "rejected")} actors={actors.Count} eligible={units.Count} " +
                    $"infantry={infantry} vehicles={vehicles} heavy={heavy} advancing={ordered} " +
                    $"rejected=[{string.Join(",", reasons)}]");
            }
        }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    private static void CombinedArmsRefresh(Agent __instance)
    {
        if (!_combinedArmsEnabled || Thread.CurrentThread.ManagedThreadId != _combinedArmsThread) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap(); var actor = __instance?.GetActor();
            var faction = __instance?.GetFaction();
            if (map != null && actor != null && faction != null)
                _combinedArms.Refresh(map.Pointer.ToInt64(), manager.m_Round, faction.Pointer.ToInt64(), actor.Pointer.ToInt64(),
                    CombinedArmsRuntime.Snapshot(__instance, true));
        }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    private static void CombinedArmsMovement(Actor __instance)
    {
        if (!_combinedArmsEnabled || Thread.CurrentThread.ManagedThreadId != _combinedArmsThread) return;
        try { if (__instance != null) CombinedArmsRefresh(__instance.GetAgent()); }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    private static void CombinedArmsDeath(Actor __instance)
    {
        if (!_combinedArmsEnabled || Thread.CurrentThread.ManagedThreadId != _combinedArmsThread) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap(); var faction = __instance?.GetAgent()?.GetFaction();
            if (map != null && faction != null)
                _combinedArms.Refresh(map.Pointer.ToInt64(), manager.m_Round, faction.Pointer.ToInt64(), __instance.Pointer.ToInt64(), null);
        }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    private static void CombinedArmsReset()
    {
        _combinedArms.Reset();
        _supportThreats.Reset();
        _combinedArmsDiagnosticBudget.Reset();
    }

    private static void CombinedArmsDetected(Actor __instance, int __0)
    {
        if (!_combinedArmsEnabled || (__0 != 6 && __0 != 9) ||
            Thread.CurrentThread.ManagedThreadId != _combinedArmsThread) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null || !_supportThreats.Advance(map.Pointer.ToInt64(), manager.m_Round)) return;
            CombinedArmsThreatRuntime.Observe(manager.GetFaction(__0)?.TryCast<AIFaction>(), __instance,
                _supportThreats, map, manager.m_Round);
        }
        catch (Exception ex) { CombinedArmsDisable(ex); }
    }

    internal static void CombinedArmsDiagnostic(int round, long faction, long actor, string phase, string details)
    {
        try
        {
            if (_combinedArmsDiagnostics && _combinedArmsDiagnosticBudget.TryAcquire(round, faction, actor, phase))
                _log.Msg($"{ModName}: CA-DIAG round={round} faction={faction:X} actor={actor:X} phase={phase} {details}");
        }
        catch { } // Optional diagnostics must never disable gameplay.
    }

    private static void CombinedArmsDisable(Exception ex)
    {
        _combinedArmsEnabled = false; _combinedArms.Reset(); _supportThreats.Reset();
        _log.Warning($"{ModName}: combined-arms support disabled ({ex.GetType().Name}); native AI retained.");
    }
}
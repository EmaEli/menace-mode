using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
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
            var reset = typeof(TacticalManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "OnInit" && m.GetParameters().Length == 1);
            if (scores == null || choice == null) throw new MissingMethodException("advance recovery hooks");
            // First managed calls can originate on IL2CPP workers (#44).
            foreach (Type type in new[] { typeof(AdvanceRecoveryRuntime), typeof(AdvanceRecoveryState), typeof(AdvanceRecoveryOrder), typeof(Plugin) })
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (type != typeof(Plugin) || method.Name.StartsWith("AdvanceRecovery", StringComparison.Ordinal))
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
            harmony.Patch(scores, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryScores)));
            harmony.Patch(choice, postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryChoice)));
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

    private static void AdvanceRecoveryScores(Actor __0, TileScores __1)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            if (map == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            bool applied = AdvanceRecoveryRuntime.TryApply(__0, __1, _advanceRecovery, map.Pointer.ToInt64(), manager.m_Round, out var result);
            if (_advanceRecoveryDiagnostics && __0 != null)
                AdvanceRecoveryLog($"ADVANCE-CHECK round={manager.m_Round} actor={__0.Pointer.ToInt64():X} result={result} applied={applied} " +
                    _advanceRecovery.Describe(__0.Pointer.ToInt64()));
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryChoice(Agent __instance, Behavior __result)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            var actor = __instance?.GetActor();
            if (map == null || actor == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            bool idleOnly = AdvanceRecoveryRuntime.IdleOnly(__instance, __result);
            _advanceRecovery.Choice(map.Pointer.ToInt64(), manager.m_Round, actor.Pointer.ToInt64(), __instance.Pointer.ToInt64(), idleOnly);
            if (_advanceRecoveryDiagnostics)
                AdvanceRecoveryLog($"ADVANCE-CHOICE round={manager.m_Round} actor={actor.Pointer.ToInt64():X} idleOnly={idleOnly} " +
                    _advanceRecovery.Describe(actor.Pointer.ToInt64()));
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
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
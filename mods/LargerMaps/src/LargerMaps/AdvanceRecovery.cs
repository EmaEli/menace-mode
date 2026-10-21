using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors.Criterions;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace LargerMaps;

public sealed partial class Plugin
{
    private static readonly AdvanceRecoveryState _advanceRecovery = new();
    private static volatile bool _advanceRecoveryEnabled;

    private static void PatchAdvanceRecovery(HarmonyLib.Harmony harmony)
    {
        if (TargetSize <= ZoneReferenceSize || !ReadBool("RecoverStalledAdvance", true)) return;
        if (ExtendedPathfinding.MatchedGameVersion != "v0.7.17+22362")
        {
            _log.Warning($"{ModName}: advance recovery requires reviewed MENACE 0.7.17; native AI retained.");
            return;
        }
        try
        {
            // First managed calls can originate on IL2CPP workers (#44).
            foreach (Type type in new[] { typeof(AdvanceRecoveryRuntime), typeof(AdvanceRecoveryState), typeof(AdvanceRecoveryOrder), typeof(Plugin) })
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (type != typeof(Plugin) || method.Name.StartsWith("AdvanceRecovery", StringComparison.Ordinal))
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
            harmony.Patch(AccessTools.Method(typeof(ConsiderZones), "PostProcess", new[] { typeof(Actor), typeof(TileScores) }),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryScores)));
            harmony.Patch(AccessTools.Method(typeof(Agent), "PickBehavior", Type.EmptyTypes),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryChoice)));
            harmony.Patch(typeof(TacticalManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "OnInit" && m.GetParameters().Length == 1),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryReset)));
            _advanceRecoveryEnabled = true;
            _log.Msg($"{ModName}: conditional advance recovery enabled; two idle rounds, one AdvanceTowards order, native Move/pathfinding retained.");
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryReset() => _advanceRecovery.Reset();

    private static void AdvanceRecoveryScores(Actor __0, TileScores __1)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            if (map == null || map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize) return;
            bool applied = AdvanceRecoveryRuntime.TryApply(__0, __1, _advanceRecovery, map.Pointer.ToInt64(), manager.m_Round, out var result);
            if (_aiRouteEnabled)
            {
                try
                {
                    if (applied)
                        AIRouteWrite(__0, "ADVANCE-RECOVERY", "restored missing native single-zone incentive; destination/execution not yet established");
                    else if (__0 != null)
                        AIRouteWrite(__0, "ADVANCE-CHECK", $"result={result} tiles={__1?.Count ?? 0} " +
                            _advanceRecovery.Describe(__0.Pointer.ToInt64()));
                }
                catch (Exception ex) { AIRouteFailure(ex); }
            }
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryChoice(Agent __instance, Behavior __result)
    {
        if (!_advanceRecoveryEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            bool idleOnly = AdvanceRecoveryRuntime.IdleOnly(__instance, __result);
            _advanceRecovery.Choice(manager.GetMap().Pointer.ToInt64(), manager.m_Round, __instance.GetActor().Pointer.ToInt64(),
                __instance.Pointer.ToInt64(), idleOnly);
            if (_aiRouteEnabled)
            {
                try
                {
                    AIRouteWrite(__instance.GetActor(), "ADVANCE-CHOICE", $"idleOnly={idleOnly} " +
                        _advanceRecovery.Describe(__instance.GetActor().Pointer.ToInt64()));
                }
                catch (Exception ex) { AIRouteFailure(ex); }
            }
        }
        catch (Exception ex) { AdvanceRecoveryDisable(ex); }
    }

    private static void AdvanceRecoveryDisable(Exception ex)
    {
        _advanceRecoveryEnabled = false;
        _advanceRecovery.Reset();
        _log.Warning($"{ModName}: advance recovery disabled ({ex.GetType().Name}); subsequent evaluations retain native AI.");
    }
}
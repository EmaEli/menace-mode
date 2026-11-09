using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly OffensivePlanningBudget _offensiveBudget = new();
    private static volatile bool _offensivePlanningEnabled;

    private static void PatchOffensivePlanning(Harmony harmony)
    {
        if (!ReadBool("OffensiveCombatPlanning", true) && !_combinedArmsEnabled) return;
        try
        {
            if (!ContactConvergenceBuild.IsCurrentSupported()) throw new NotSupportedException("reviewed 0.7.18 required");
            var scores = AccessTools.Method(typeof(Agent), "PostProcessTileScores", Type.EmptyTypes);
            var choice = AccessTools.Method(typeof(Agent), "PickBehavior", Type.EmptyTypes);
            var reset = AccessTools.Method(typeof(TacticalManager), "OnInit");
            if (scores == null || choice == null || reset == null) throw new MissingMethodException("offensive planning hooks");
            foreach (var type in new[] { typeof(OffensivePlanningRuntime), typeof(OffensivePlanningBudget), typeof(CombinedArmsResponseRuntime) })
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    foreach (var method in nested.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                        BindingFlags.Instance | BindingFlags.DeclaredOnly)) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    foreach (var constructor in nested.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
                }
            }
            foreach (string name in new[] { nameof(OffensivePlanningScores), nameof(OffensivePlanningChoice), nameof(OffensivePlanningReset) })
                RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
            _offensiveBudget.Spend(1, 1, 1, 1); _offensiveBudget.Reset();
            harmony.Patch(scores, prefix: new HarmonyMethod(typeof(Plugin), nameof(OffensivePlanningScores)));
            harmony.Patch(choice, postfix: new HarmonyMethod(typeof(Plugin), nameof(OffensivePlanningChoice)));
            harmony.Patch(reset, postfix: new HarmonyMethod(typeof(Plugin), nameof(OffensivePlanningReset)));
            var value = AccessTools.Method(typeof(Il2CppMenace.Tactical.AI.Behaviors.InflictDamage), "GetTargetValue",
                new[] { typeof(bool), typeof(Il2CppMenace.Tactical.Skills.Skill), typeof(Tile), typeof(Tile), typeof(Tile) });
            if (value == null) throw new MissingMethodException("native threat valuation");
            RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(nameof(CombinedArmsThreatValue), BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
            harmony.Patch(value, postfix: new HarmonyMethod(typeof(Plugin), nameof(CombinedArmsThreatValue)));
            BindSupportSmoke();
            _offensivePlanningEnabled = ReadBool("OffensiveCombatPlanning", true);
            _log.Msg($"{ModName}: bounded offensive planning enabled; native direct-fire probes, shot-first choices and mission advances (pirate/Rogue).");
        }
        catch (Exception ex) { OffensivePlanningDisable(ex); if (_combinedArmsEnabled) CombinedArmsDisable(ex); }
    }

    private static void OffensivePlanningScores(Agent __instance)
    {
        if ((!_offensivePlanningEnabled && !_combinedArmsEnabled) || !_combatPlanningActive) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            bool unresolvedDanger = false;
            if (map != null && _combinedArmsEnabled)
            {
                try
                {
                    if (CombinedArmsResponseRuntime.PlanVehicle(__instance, __instance.GetTiles(), _supportThreats,
                        _offensiveBudget, map, manager.m_Round, _combinedArmsDiagnostics)) return;
                    int flanks = CombinedArmsThreatRuntime.Flank(__instance, __instance.GetTiles(), _supportThreats,
                        _offensiveBudget, map, manager.m_Round, out unresolvedDanger);
                    if (flanks > 0)
                    {
                        CombinedArmsDiagnostic(manager.m_Round, __instance.GetFaction().Pointer.ToInt64(),
                            __instance.GetActor().Pointer.ToInt64(), "flank", $"uncertain-position seeds={flanks}");
                        return;
                    }
                    if (CombinedArmsRuntime.Plan(__instance, __instance.GetTiles(), _combinedArms, _offensiveBudget,
                        map, manager.m_Round, _combinedArmsDiagnostics)) return;
                    if (unresolvedDanger)
                        CombinedArmsDiagnostic(manager.m_Round, __instance.GetFaction().Pointer.ToInt64(),
                            __instance.GetActor().Pointer.ToInt64(), "flank", "result=unresolved objective-advance=blocked");
                }
                catch (Exception ex) { CombinedArmsDisable(ex); return; }
            }
            if (map != null && _offensivePlanningEnabled) OffensivePlanningRuntime.Plan(__instance, __instance.GetTiles(),
                _missionCivilianPriorityEnabled ? manager.GetMission()?.Objectives : null,
                _offensiveBudget, map.Pointer.ToInt64(), manager.m_Round, id => _simplifiedAttackIds.Contains(id),
                suppressAdvance: unresolvedDanger);
        }
        catch (Exception ex) { OffensivePlanningDisable(ex); }
    }

    private static void OffensivePlanningChoice(Agent __instance, ref Behavior __result)
    {
        if (!_offensivePlanningEnabled && !_combinedArmsEnabled) return;
        try
        {
            var nativeChoice = __result;
            __result = OffensivePlanningRuntime.PreferShot(__instance, __result, _combinedArmsEnabled,
                _offensivePlanningEnabled, out var supportAsset);
            if (_combinedArmsEnabled && supportAsset is { Kind: SupportKind.Vehicle })
            {
                var manager = TacticalManager.Get(); var map = manager?.GetMap();
                if (map != null) __result = CombinedArmsRuntime.PreventSpacingRetreat(__instance, __result,
                    _combinedArms, map, manager.m_Round,
                    CombinedArmsResponseRuntime.Threat(__instance, _supportThreats, map, manager.m_Round));
            }
            if (_combinedArmsEnabled)
            {
                var manager = TacticalManager.Get(); var map = manager?.GetMap();
                if (map != null)
                {
                    var before = __result;
                    __result = CombinedArmsResponseRuntime.GuardAdvance(__instance, __result,
                        _supportThreats, map, manager.m_Round, SupportSmokeCover);
                    if (_combinedArmsDiagnostics && before?.Pointer != __result?.Pointer)
                        CombinedArmsDiagnostic(manager.m_Round, __instance.GetFaction().Pointer.ToInt64(),
                            __instance.GetActor().Pointer.ToInt64(), "response-hold", "reason=exposed-native-route result=wait-for-cover-fire-or-smoke");
                }
            }
            if (_combinedArmsDiagnostics && supportAsset != null)
            {
                try
                {
                    var faction = __instance.GetFaction(); var manager = TacticalManager.Get();
                    string nativeId = nativeChoice == null ? "null" : nativeChoice.GetID().ToString();
                    string selectedId = __result == null ? "null" : __result.GetID().ToString();
                    CombinedArmsDiagnostic(manager?.m_Round ?? 0, faction?.Pointer.ToInt64() ?? 0,
                        supportAsset.Actor, "choice",
                        $"kind={supportAsset.Kind} tile=({supportAsset.X},{supportAsset.Z}) native={nativeId} selected={selectedId}");
                }
                catch { } // Choice diagnostics must not change the native selection.
            }
        }
        catch (Exception ex) { OffensivePlanningDisable(ex); }
    }

    private static void OffensivePlanningReset() => _offensiveBudget.Reset();
    private static void OffensivePlanningDisable(Exception ex)
    {
        _offensivePlanningEnabled = false; _offensiveBudget.Reset();
        _log.Warning($"{ModName}: offensive planning disabled ({ex.GetType().Name}); native/recovery behavior retained.");
    }
}
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.Objectives;

namespace AIPerformance;

// Score only an already-evaluated, visible native damage target. Never discover
// actors, generate attacks, redirect orders or turn incidental AoE into a priority.
internal static class MissionCivilianPriorityRuntime
{
    internal static float Evaluate(InflictDamage behavior, bool immediate, Tile target, Tile aimedAt,
        ObjectiveManager objectives, float nativeValue)
    {
        if (!immediate || !float.IsFinite(nativeValue) || nativeValue <= 0 || target == null ||
            aimedAt == null || target.Pointer != aimedAt.Pointer || objectives == null) return nativeValue;
        var agent = behavior?.GetAgent();
        var faction = agent?.GetFaction();
        if (faction == null || faction.IsAlliedWithPlayer() || (int)faction.GetFactionType() < 5 ||
            (int)faction.GetFactionType() > 9 || agent.IsSleeping() || agent.IsDeploymentPhase()) return nativeValue;
        var civilian = target.GetEntity()?.TryCast<Actor>();
        // Check native detection before faction/death/objective identity reads.
        if (civilian == null || !civilian.IsDetectedByFaction((int)faction.GetFactionType())) return nativeValue;
        int kind = (int)civilian.GetFaction();
        if ((kind != 3 && kind != 4) || faction.IsAlliedWith(kind) || civilian.IsDying() ||
            civilian.IsContainedWithinAnotherEntity()) return nativeValue;
        if (!MatchesObjective(civilian, objectives)) return nativeValue;
        float priority = nativeValue * 2;
        return float.IsFinite(priority) ? priority : nativeValue;
    }

    // Call only after native detection/alliance checks; never discover an unseen actor here.
    internal static bool MatchesObjective(Actor civilian, ObjectiveManager objectives)
    {
        if (civilian == null || objectives == null) return false;
        var list = objectives.m_Objectives;
        if (list == null || list.Length < 1 || list.Length > 32) return false;
        foreach (var objective in list)
        {
            if (objective == null || !objective.IsOngoing()) continue;
            bool matches = false;
            if (objective.TryCast<ProtectUnit>() != null || objective.TryCast<RescueUnit>() != null)
            {
                var victim = objective.GetTarget();
                matches = victim != null && victim.Pointer == civilian.Pointer;
            }
            else
            {
                var collateral = objective.TryCast<Collateral>();
                matches = collateral != null && collateral.FulfillsConditions(civilian);
            }
            if (!matches) continue;
            return true;
        }
        return false;
    }
}

public sealed partial class Plugin
{
    private static volatile bool _missionCivilianPriorityEnabled;

    private static void PatchMissionCivilianPriority(Harmony harmony)
    {
        if (!ReadBool("PrioritizeMissionCivilians", true)) return;
        try
        {
            if (!ContactConvergenceBuild.IsCurrentSupported())
                throw new NotSupportedException("mission civilian priority requires the reviewed 0.7.18 binary");
            var target = AccessTools.Method(typeof(InflictDamage), "GetTargetValue", new[]
                { typeof(bool), typeof(Il2CppMenace.Tactical.Skills.Skill), typeof(Tile), typeof(Tile), typeof(Tile) });
            if (target == null || target.ReturnType != typeof(float)) throw new MissingMethodException("InflictDamage.GetTargetValue");
            foreach (MethodInfo method in typeof(MissionCivilianPriorityRuntime).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            RuntimeHelpers.PrepareMethod(typeof(Plugin).GetMethod(nameof(MissionCivilianPriorityValue),
                BindingFlags.Static | BindingFlags.NonPublic).MethodHandle);
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(Plugin), nameof(MissionCivilianPriorityValue)));
            _missionCivilianPriorityEnabled = true;
        }
        catch (Exception ex) { MissionCivilianPriorityDisable(ex); }
    }

    private static void MissionCivilianPriorityValue(InflictDamage __instance, bool __0, Tile __2, Tile __4, ref float __result)
    {
        if (!_missionCivilianPriorityEnabled || !__0 || !float.IsFinite(__result) || __result <= 0) return;
        try
        {
            __result = MissionCivilianPriorityRuntime.Evaluate(__instance, __0, __2, __4,
                TacticalManager.Get()?.GetMission()?.Objectives, __result);
        }
        catch (Exception ex) { MissionCivilianPriorityDisable(ex); }
    }

    private static void MissionCivilianPriorityDisable(Exception ex)
    {
        _missionCivilianPriorityEnabled = false;
        _log.Warning($"{ModName}: mission civilian priority disabled ({ex.GetType().Name}); native targeting retained.");
    }
}
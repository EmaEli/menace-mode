using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;

namespace AIPerformance;

internal static class SupportProjection
{
    internal readonly record struct Result(float Value, float Attack, int Cost);
    internal static List<Tile> DetectedTargets(AIFaction faction)
    {
        var result = new List<Tile>(); var seen = new HashSet<long>(); var opponents = faction.GetOpponents();
        if (opponents == null || opponents.Count > 128) return result;
        foreach (var opponent in opponents)
        {
            var enemy = opponent?.Actor;
            if (enemy == null || !enemy.IsDetectedByFaction((int)faction.GetFactionType())) continue;
            int kind = (int)enemy.GetFaction();
            if ((kind != 1 && kind != 2) || faction.IsAlliedWith(kind) || enemy.IsDying() ||
                enemy.IsContainedWithinAnotherEntity()) continue;
            var tile = enemy.GetTile();
            if (tile != null && seen.Add(tile.Pointer.ToInt64())) result.Add(tile);
            if (result.Count == 4) break;
        }
        return result;
    }

    internal static Result Evaluate(Agent agent, TileScore origin, SupportUnit request,
        List<InflictDamage> weapons, List<Tile> targets, OffensivePlanningBudget budget, Map map, int round, ref int probes, long started)
    {
        var actor = agent.GetActor(); var faction = agent.GetFaction();
        bool moving = origin.Tile.Pointer != actor.GetTile()?.Pointer;
        float best = 0, attack = 0; int reserve = 1024;
        foreach (var damage in weapons)
        {
            var skill = damage.GetSkill(); var template = skill.GetTemplate();
            int cost = Reserve(damage, actor, moving);
            if (cost <= 0 || cost > actor.GetActionPoints()) continue;
            // Only detected opponents provide coordinates. A mission lane is geometry, not a sighting.
            int considered = 0;
            foreach (var target in targets)
                {
                    if (target == null || CombinedArmsRuntime.Distance2(target, request.X, request.Z) > 400) continue;
                    if (++considered > 2) break;
                    if (!Spend(agent, budget, map, round, 4, ref probes, started)) return new(best, attack, reserve);
                    if (!skill.IsUsableOn(target, origin.Tile, TargetUsageParams.IGNORE_DEPLOYMENT_AND_ANGLE) ||
                        !skill.IsApplicableTo(target, null) || skill.GetFinalUserTile(origin.Tile, target)?.Pointer != origin.Tile.Pointer) continue;
                    float value = damage.GetTargetValue(false, skill, target, origin.Tile, target) *
                        template.AIConfig.ScoreMult * AIConfig.WEIGHTS.InflictDamageFromTile;
                    if (!float.IsFinite(value) || value <= 0) continue;
                    attack = Math.Max(attack, value); best = Math.Max(best, value); reserve = Math.Min(reserve, cost);
                }
            if (attack > 0) continue;
            // Three bounded samples of the next four tiles of intended advance, not the whole objective.
            int dx = request.AimX - request.X, dz = request.AimZ - request.Z;
            double length = Math.Sqrt((double)dx * dx + (double)dz * dz);
            if (length <= 0) continue;
            int ax = request.X + (int)Math.Round(dx / length * Math.Min(4, length));
            int az = request.Z + (int)Math.Round(dz / length * Math.Min(4, length));
            for (int i = 0; i < 3; i++)
            {
                int side = i == 0 ? 0 : i == 1 ? -2 : 2;
                int x = ax - (int)Math.Round(dz / length * side), z = az + (int)Math.Round(dx / length * side);
                if (x < 0 || z < 0 || x >= map.Width || z >= map.Height) continue;
                var lane = map.GetTile(x, z);
                if (lane == null) continue;
                if (!Spend(agent, budget, map, round, 2, ref probes, started)) return new(best, attack, reserve);
                if (!skill.IsInRange(lane, origin.Tile, false) ||
                    !origin.Tile.HasLineOfSightTo(lane, Il2CppTactical.LineOfSightFlags.Default)) continue;
                best = Math.Max(best, 1); reserve = Math.Min(reserve, cost);
                break; // One clear sample is sufficient; it does not manufacture attack value.
            }
        }
        return new(best, attack, reserve);
    }

    internal static int Reserve(InflictDamage damage, Actor actor, bool moving)
    {
        var skill = damage.GetSkill(); var template = skill.GetTemplate();
        long cost = skill.GetActionPointCost();
        // Moving can require re-deployment/setup even if the weapon is ready at this origin.
        if (template.IsDeploymentRequired && (moving || !template.IsDeploymentRequirementSatisfied(actor)))
        {
            var deploy = damage.m_DeployedStanceSkill;
            if (!Available(deploy)) return 0;
            cost += deploy.GetActionPointCost();
        }
        if (template.IsWeaponSetupRequired && (moving || !template.IsSetupRequirementSatisfied(actor)))
        {
            var setup = damage.m_SetupWeaponSkill;
            if (!Available(setup)) return 0;
            cost += setup.GetActionPointCost();
        }
        return cost > 0 && cost <= int.MaxValue ? (int)cost : 0;
    }

    private static bool Available(Skill skill) => skill != null && skill.IsEnabled() &&
        !skill.IsOutOfUses() && skill.HasExecutingElements() && skill.GetActionPointCost() >= 0;
    private static bool Spend(Agent agent, OffensivePlanningBudget budget, Map map, int round, int cost, ref int probes, long started)
    {
        if (probes + cost > OffensivePlanningBudget.PerEvaluation ||
            (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency >= 20 ||
            !budget.Spend(map.Pointer.ToInt64(), round, agent.GetActor().Pointer.ToInt64(), agent.Pointer.ToInt64(),
                agent.GetFaction().Pointer.ToInt64(), cost)) return false;
        probes += cost; return true;
    }
}
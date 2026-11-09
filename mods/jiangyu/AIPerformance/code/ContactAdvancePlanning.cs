using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static partial class ContactAdvanceRuntime
{
    private readonly record struct Candidate(TileScore Score, int X, int Z, int Progress, bool NearRange, bool Current, float Safety);
    private readonly record struct Change(TileScore Score, float Before, int BeforeCost, float Utility, int Cost);

    private static List<Attack> Weapons(Agent agent)
    {
        var result = new List<Attack>(); var behaviors = agent.GetBehaviors();
        if (behaviors == null || behaviors.Count > 128) return result;
        foreach (var behavior in behaviors)
        {
            var attack = behavior?.TryCast<Attack>(); var skill = attack?.GetSkill(); var template = skill?.GetTemplate();
            if (attack == null || (attack.TryCast<InflictDamage>() == null && attack.TryCast<InflictSuppression>() == null) ||
                template == null || !template.IsAttack || !template.IsTargeted || !template.IsLineOfFireNeeded ||
                skill.IsOutOfUses() || !skill.IsEnabled() || !skill.HasExecutingElements() ||
                skill.GetMaxRange() < 1 || skill.GetMaxRange() > 128 || skill.GetActionPointCost() <= 0) continue;
            result.Add(attack);
        }
        result.Sort((a, b) => b.GetSkill().GetMaxRange().CompareTo(a.GetSkill().GetMaxRange()));
        if (result.Count > 2) result.RemoveRange(2, result.Count - 2);
        return result;
    }

    // Cheap lane geometry is deliberately independent of expensive damage-probe tokens.
    // Exhausting a round's damage budget must not erase fresh range-closing decisions.
    private static bool FiringLane(Attack weapon, Tile origin, Tile target, Actor actor, bool moving, out int cost)
    {
        cost = SupportProjection.Reserve(weapon, actor, moving);
        var skill = weapon.GetSkill();
        return cost > 0 && cost <= actor.GetActionPoints() && skill.IsInRange(target, origin, false) &&
            origin.HasLineOfSightTo(target, Il2CppTactical.LineOfSightFlags.Default);
    }

    internal static bool Plan(Agent agent, TileScores tiles, ContactAdvanceState memory,
        OffensivePlanningBudget budget, Map map, int round)
    {
        if (!Ready(agent) || !TryAim(agent, memory, map, round, out var contact) ||
            CombinedArmsResponseRuntime.HasUsefulAction(agent) || tiles == null || tiles.Count < 2 || tiles.Count > 4096) return false;
        var actor = agent.GetActor(); var current = actor.GetTile(); var weapons = Weapons(agent);
        if (current == null || weapons.Count == 0 || !tiles.TryGetValue(current, out var here) || here?.Tile == null ||
            here.Tile.Pointer != current.Pointer || !float.IsFinite(here.UtilityByAttacksScore) ||
            here.UtilityByAttacksScore > 0) return false;
        var target = DetectedTarget(agent, contact);
        // An unseen opponent is an area to search, never a legal attack target or a live-coordinate beacon.
        int tx = target?.GetX() ?? contact.X, tz = target?.GetZ() ?? contact.Z;
        int cx = current.GetX(), cz = current.GetZ();
        int distance = Math.Max(Math.Abs(cx - tx), Math.Abs(cz - tz));
        if (target == null && distance <= 3 + Math.Min(6, round - contact.SeenRound)) return false;
        float threshold = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
        if (!(threshold > 0) || !float.IsFinite(threshold) || !float.IsFinite(threshold * 64)) return false;
        int range = weapons[0].GetSkill().GetMaxRange();
        var candidates = new List<Candidate>();
        foreach (var pair in tiles)
        {
            var score = pair.Value; var tile = score?.Tile;
            if (tile == null || pair.Key == null || tile.Pointer != pair.Key.Pointer || score.APCost < 0 ||
                score.APCost > actor.GetActionPoints() || !float.IsFinite(score.UtilityScore) ||
                !float.IsFinite(score.SafetyScore) || !float.IsFinite(score.UtilityByAttacksScore) ||
                !float.IsFinite(score.UtilityByAttacksScoreCandidate)) continue;
            int x = tile.GetX(), z = tile.GetZ();
            if (x < 0 || z < 0 || x >= map.Width || z >= map.Height ||
                (long)(x - cx) * (x - cx) + (long)(z - cz) * (z - cz) > 100) continue;
            int next = Math.Max(Math.Abs(x - tx), Math.Abs(z - tz));
            candidates.Add(new(score, x, z, distance - next, next <= range, tile.Pointer == current.Pointer, score.SafetyScore));
        }
        // Current origin first, then covered in-range origins, then the strongest forward progress.
        candidates.Sort((a, b) =>
        {
            if (a.Current != b.Current) return a.Current ? -1 : 1;
            if (a.NearRange != b.NearRange) return a.NearRange ? -1 : 1;
            int safety = a.Safety.CompareTo(b.Safety); // Native SafetyScore is a cost: lower is safer.
            return a.NearRange && safety != 0 ? safety : b.Progress.CompareTo(a.Progress);
        });
        var lanes = new Dictionary<long, int>();
        int geometry = 0, probes = 0; long started = Stopwatch.GetTimestamp();
        if (target != null)
            foreach (var candidate in candidates)
            {
                if (geometry >= 16 ||
                    (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency >= 10) break;
                if (!candidate.NearRange) continue;
                bool moving = candidate.Score.Tile.Pointer != current.Pointer;
                foreach (var weapon in weapons)
                {
                    if (++geometry > 16) break;
                    if (!FiringLane(weapon, candidate.Score.Tile, target, actor, moving, out int reserve) ||
                        (long)candidate.Score.APCost + reserve > actor.GetActionPoints()) continue;
                    bool useful = true;
                    var skill = weapon.GetSkill();
                    if (probes + 4 <= OffensivePlanningBudget.PerEvaluation && budget.Spend(map.Pointer.ToInt64(), round,
                        actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), agent.GetFaction().Pointer.ToInt64(), 4))
                    {
                        probes += 4;
                        useful = skill.IsUsableOn(target, candidate.Score.Tile, TargetUsageParams.IGNORE_DEPLOYMENT_AND_ANGLE) &&
                            skill.IsApplicableTo(target, null) && skill.GetFinalUserTile(candidate.Score.Tile, target)?.Pointer == candidate.Score.Tile.Pointer;
                        if (useful)
                        {
                            var damage = weapon.TryCast<InflictDamage>();
                            float value = damage != null ? damage.GetTargetValue(false, skill, target, candidate.Score.Tile, target) :
                                weapon.TryCast<InflictSuppression>().GetTargetValue(false, skill, target, candidate.Score.Tile, target);
                            useful = float.IsFinite(value) && value > 0;
                        }
                    }
                    // A geometric lane is not fabricated damage/attack utility or a forced native action.
                    if (useful) { lanes[candidate.Score.Tile.Pointer.ToInt64()] = reserve; break; }
                }
            }
        bool firingHere = lanes.ContainsKey(current.Pointer.ToInt64());
        var changes = new List<Change>(); bool forward = false;
        int sector = (int)((uint)actor.ID % 5) - 2;
        foreach (var c in candidates)
        {
            float utility; int cost = c.Score.MinimumUtilityAPCost;
            if (lanes.TryGetValue(c.Score.Tile.Pointer.ToInt64(), out int reserve))
            {
                utility = threshold * 24; cost = Math.Min(cost, reserve);
            }
            else if (c.Score.UtilityByAttacksScore > 0 || c.Score.UtilityByAttacksScoreCandidate > 0) continue;
            else if (!firingHere && c.Progress > 0)
            {
                // Five modest lateral approach lanes, no three-responder/three-round stopping rule.
                int lateral = Math.Abs(tx - cx) >= Math.Abs(tz - cz) ? c.Z - tz : c.X - tx;
                utility = threshold * (4 + Math.Min(8, c.Progress) - Math.Min(4, Math.Abs(lateral - sector * 2)) * .1f);
                forward = true;
            }
            else utility = 0; // Cover-only utility must not win while it cannot enable engagement.
            changes.Add(new(c.Score, c.Score.UtilityScore, c.Score.MinimumUtilityAPCost, utility, cost));
        }
        if (!forward && lanes.Count == 0) return false;
        bool committed = false;
        try
        {
            foreach (var change in changes) { change.Score.UtilityScore = change.Utility; change.Score.MinimumUtilityAPCost = change.Cost; }
            committed = true;
        }
        finally
        {
            if (!committed)
                foreach (var change in changes)
                    try { change.Score.UtilityScore = change.Before; change.Score.MinimumUtilityAPCost = change.BeforeCost; }
                    catch { } // Continue restoring the other native entries before the outer fail-closed handler.
        }
        return true;
    }
}
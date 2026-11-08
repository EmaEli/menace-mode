using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Objectives;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tools;
using Math = System.Math;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static class OffensivePlanningRuntime
{
    internal const int MaxTargets = 4, MaxOrigins = 8, MaxSkills = 2;
    internal static bool Eligible(Agent agent)
    {
        var faction = agent?.GetFaction();
        var actor = agent?.GetActor();
        var role = agent?.GetRole();
        return actor != null && role != null && ContactConvergenceRuntime.EligibleFaction(faction) &&
            !role.AvoidOpponents && role.Move > 0 && float.IsFinite(role.Move) &&
            !agent.IsSleeping() && !agent.IsDeploymentPhase() && !actor.IsDying() && !actor.IsMoving() &&
            !actor.IsContainedWithinAnotherEntity() && !actor.IsActionPointsSpent() &&
            float.IsFinite(actor.GetMoralePct()) && actor.GetMoralePct() >= .75f &&
            float.IsFinite(actor.GetSuppressionPct()) && actor.GetSuppressionPct() <= .25f &&
            !actor.HasTag(TagType.MACHINE) && !actor.HasTag(TagType.XENO) && !actor.HasTag(TagType.DRONE);
    }

    internal static Behavior PreferShot(Agent agent, Behavior chosen)
    {
        if (!Eligible(agent) || chosen == null) return chosen;
        string id = chosen.GetID().ToString();
        // Never interrupt execution, flee, smoke, healing or other useful native actions.
        if (id != "Idle" && id != "Move") return chosen;
        var list = agent.GetBehaviors();
        if (list == null || list.Count > 128) return chosen;
        InflictDamage best = null;
        foreach (var behavior in list)
        {
            var damage = behavior?.TryCast<InflictDamage>();
            if (damage != null && damage.GetScore() > 0 && damage.m_Candidates != null &&
                damage.m_Candidates.Count > 0 && damage.m_Candidates.Count <= 256 &&
                (best == null || damage.GetScore() > best.GetScore())) best = damage;
        }
        // Already evaluated by native Attack; native Execute rechecks setup, AP and legality.
        return best ?? chosen;
    }

    internal static void Plan(Agent agent, TileScores tiles, ObjectiveManager objectives,
        OffensivePlanningBudget budget, long map, int round, Func<string, bool> simplified)
    {
        if (!Eligible(agent) || tiles == null || tiles.Count < 2 || tiles.Count > 4096) return;
        var actor = agent.GetActor();
        var current = actor.GetTile();
        var behaviors = agent.GetBehaviors();
        if (current == null || behaviors == null || behaviors.Count > 128) return;
        var targets = Targets(agent, objectives);
        var origins = Origins(agent, tiles, current, targets);
        var weights = AIConfig.WEIGHTS;
        if (weights == null || !Positive(weights.InflictDamageFromTile) ||
            !Positive(weights.IncludeAttacksAgainstAllOpponentsMult)) return;
        float support = Support(agent, current);
        var edits = new Dictionary<long, Edit>();
        Edit GetEdit(TileScore score)
        {
            long key = score.Tile.Pointer.ToInt64();
            if (!edits.TryGetValue(key, out var edit)) edits[key] = edit = new Edit(score);
            return edit;
        }
        var attacks = new List<InflictDamage>();
        int probes = 0;
        bool useful = false;
        long started = Stopwatch.GetTimestamp();
        foreach (var behavior in behaviors)
        {
            var damage = behavior?.TryCast<InflictDamage>();
            var skill = damage?.GetSkill();
            var template = skill?.GetTemplate();
            if (template == null || string.IsNullOrEmpty(template.m_ID) || !simplified(template.m_ID) ||
                !template.IsAttack || !template.IsTargeted ||
                !template.IsLineOfFireNeeded || (int)template.AoEType != 0 || template.IsDeploymentRequired ||
                template.IsWeaponSetupRequired || skill.HasLimitedUses() || skill.IsOutOfUses() ||
                template.AIConfig == null || !Positive(template.AIConfig.ScoreMult)) continue;
            int cost = skill.GetActionPointCost();
            if (cost <= 0 || cost > actor.GetActionPoints()) continue;
            attacks.Add(damage);
            if (attacks.Count >= MaxSkills) break;
        }
        // Round-robin weapons at each origin: the first rifle cannot spend every
        // probe before a second anti-armour/direct-fire option is considered.
        foreach (var origin in origins)
        foreach (var damage in attacks)
        {
            var skill = damage.GetSkill(); var template = skill.GetTemplate();
            int cost = skill.GetActionPointCost();
            foreach (var target in targets)
            {
                if (probes >= OffensivePlanningBudget.PerEvaluation ||
                    (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency >= 20 ||
                    !budget.Spend(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64())) goto Finished;
                probes++;
                // Exact native positioning parameters: ignore only deployment/angle, not
                // range, LoF, uses or target faction. No skill execution or path search.
                if (!skill.IsUsableOn(target, origin.Tile, TargetUsageParams.IGNORE_DEPLOYMENT_AND_ANGLE) ||
                    !skill.IsApplicableTo(target, null) ||
                    skill.GetFinalUserTile(origin.Tile, target)?.Pointer != origin.Tile.Pointer) continue;
                float value = damage.GetTargetValue(false, skill, target, origin.Tile, target);
                if (!Positive(value)) continue;
                value = MissionCivilianPriorityRuntime.Evaluate(damage, true, target, target, objectives, value);
                float attack = value * template.AIConfig.ScoreMult * weights.InflictDamageFromTile * support;
                var edit = GetEdit(origin);
                float delta = Math.Max(0, attack - edit.Attack);
                float utility = edit.Utility + delta * weights.IncludeAttacksAgainstAllOpponentsMult;
                if (!Positive(attack) || !float.IsFinite(utility)) continue;
                // Same native fields, BEFORE normalization. Best rather than summed probes:
                // repeated observations cannot inflate a tile or manufacture extra attacks.
                edit.Utility = utility;
                edit.Attack = Math.Max(edit.Attack, attack);
                edit.Cost = Math.Min(edit.Cost, cost);
                useful = true;
            }
        }
    Finished:
        if (!useful) Advance(agent, tiles, current, support, GetEdit);
        bool committed = false;
        try
        {
            foreach (var edit in edits.Values) edit.Apply();
            committed = true;
        }
        finally
        {
            if (!committed) foreach (var edit in edits.Values) edit.Restore();
        }
    }

    private sealed class Edit
    {
        private readonly TileScore _score;
        private readonly float _utility, _attack;
        private readonly int _cost;
        internal float Utility, Attack;
        internal int Cost;
        internal Edit(TileScore score)
        {
            _score = score; Utility = _utility = score.UtilityScore;
            Attack = _attack = score.UtilityByAttacksScore; Cost = _cost = score.MinimumUtilityAPCost;
        }
        internal void Apply() { _score.UtilityScore = Utility; _score.UtilityByAttacksScore = Attack; _score.MinimumUtilityAPCost = Cost; }
        internal void Restore() { _score.UtilityScore = _utility; _score.UtilityByAttacksScore = _attack; _score.MinimumUtilityAPCost = _cost; }
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0;
    private static int Distance2(Tile a, Tile b)
    {
        int x = a.GetX() - b.GetX(), z = a.GetZ() - b.GetZ();
        return x * x + z * z;
    }

    private static List<Tile> Targets(Agent agent, ObjectiveManager objectives)
    {
        var faction = agent.GetFaction();
        var result = new List<Tile>();
        var seen = new HashSet<long>();
        void Add(Actor opponent)
        {
            // Detection BEFORE faction, tile, death or objective-identity reads.
            if (opponent == null || !opponent.IsDetectedByFaction((int)faction.GetFactionType())) return;
            int kind = (int)opponent.GetFaction();
            if (faction.IsAlliedWith(kind) || opponent.IsDying() || opponent.IsContainedWithinAnotherEntity()) return;
            if (kind != 1 && kind != 2 && kind != 3 && kind != 4) return;
            if ((kind == 3 || kind == 4) && !MissionCivilianPriorityRuntime.MatchesObjective(opponent, objectives)) return;
            var tile = opponent.GetTile();
            if (tile != null && seen.Add(tile.Pointer.ToInt64())) result.Add(tile);
        }
        // Explicit mission handles are not sightings. Add() still requires detection.
        var list = objectives?.m_Objectives;
        if (list != null && list.Length <= 32)
            foreach (var objective in list)
                if (objective != null && objective.IsOngoing() &&
                    (objective.TryCast<ProtectUnit>() != null || objective.TryCast<RescueUnit>() != null))
                    Add(objective.GetTarget()?.TryCast<Actor>());
        var opponents = faction.GetOpponents();
        if (opponents != null && opponents.Count <= 128)
            foreach (var opponent in opponents) Add(opponent?.Actor);
        var current = agent.GetActor().GetTile();
        result.Sort((a, b) => Distance2(a, current).CompareTo(Distance2(b, current)));
        if (result.Count > MaxTargets) result.RemoveRange(MaxTargets, result.Count - MaxTargets);
        return result;
    }

    private static List<TileScore> Origins(Agent agent, TileScores tiles, Tile current, List<Tile> targets)
    {
        var candidates = new List<(TileScore Score, int X, int Z, int Rank)>();
        bool advance = AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, agent.GetActor(), out _, out _);
        var zones = agent.GetZones();
        int cx = current.GetX(), cz = current.GetZ();
        int tx = targets.Count == 0 ? cx : targets[0].GetX(), tz = targets.Count == 0 ? cz : targets[0].GetZ();
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            var tile = score?.Tile;
            if (tile == null || pair.Key == null || tile.Pointer != pair.Key.Pointer) continue;
            int x = tile.GetX(), z = tile.GetZ(), distance = (x - cx) * (x - cx) + (z - cz) * (z - cz);
            if (distance > 36 || !float.IsFinite(score.UtilityScore) ||
                !float.IsFinite(score.UtilityByAttacksScore) || !float.IsFinite(score.SafetyScore)) continue;
            // Do not take a defender outside its ordered area, or turn a scripted order into a charge.
            if (!advance && zones != null && zones.Count > 0 && score.Tile.Pointer != current.Pointer)
            {
                bool inside = false;
                if (zones.Count > 8) continue;
                foreach (var zone in zones)
                    if (zone != null && RectIntExtensions.IsInside(zone.Area, score.Tile)) inside = true;
                if (!inside) continue;
            }
            int rank = tile.Pointer == current.Pointer ? int.MinValue :
                ((x - tx) * (x - tx) + (z - tz) * (z - tz)) * 4 + distance;
            candidates.Add((score, x, z, rank));
        }
        // Comparisons are managed-only: never thousands of interop coordinate reads while sorting.
        candidates.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        var result = new List<TileScore>();
        var selected = new List<(int X, int Z)>();
        foreach (var candidate in candidates)
        {
            if (result.Count >= MaxOrigins) break;
            if (selected.Exists(other => (candidate.X - other.X) * (candidate.X - other.X) +
                (candidate.Z - other.Z) * (candidate.Z - other.Z) < 4)) continue;
            result.Add(candidate.Score);
            // Current tile is always inspected, but must not exclude adjacent firing positions.
            if (candidate.Score.Tile.Pointer != current.Pointer) selected.Add((candidate.X, candidate.Z));
        }
        return result;
    }

    private static float Support(Agent agent, Tile current)
    {
        var friends = agent.GetFaction().GetActors();
        if (friends == null || friends.Count > 128) return 1;
        int nearby = 0;
        foreach (var friend in friends)
            if (friend != null && friend.Pointer != agent.GetActor().Pointer && !friend.IsDying() &&
                friend.GetAgent() != null && !friend.GetAgent().IsSleeping() && !friend.GetAgent().IsDeploymentPhase() &&
                !friend.IsContainedWithinAnotherEntity() && friend.GetTile() is Tile tile && Distance2(tile, current) <= 144)
                nearby++;
        // Modest local support bonus; not a claim to know the number of hidden enemies.
        return 1 + Math.Min(2, nearby) * .25f;
    }

    private static void Advance(Agent agent, TileScores tiles, Tile current, float support, Func<TileScore, Edit> edit)
    {
        if (!Positive(agent.GetRole().ZoneImportanceWeight)) return;
        foreach (var behavior in agent.GetBehaviors())
        {
            if (behavior == null) return;
            string id = behavior.GetID().ToString();
            if (id != "Idle" && id != "Move" && behavior.GetScore() > 0) return;
        }
        if (!AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, agent.GetActor(), out var zone, out _)) return;
        int distance = RectIntExtensions.GetDistanceTo(zone.Area, current);
        float threshold = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
        if (distance <= 0 || !Positive(threshold)) return;
        // Preserve native attack/support opportunities and mission arrival. Do not fake attack utility.
        foreach (var pair in tiles)
            if (pair.Key == null || pair.Value?.Tile == null || pair.Key.Pointer != pair.Value.Tile.Pointer ||
                pair.Value.UtilityScore > 0 || pair.Value.UtilityByAttacksScore > 0 || pair.Value.UtilityByAttacksScoreCandidate > 0 ||
                !float.IsFinite(pair.Value.UtilityScore) || !float.IsFinite(pair.Value.UtilityByAttacksScore) ||
                !float.IsFinite(pair.Value.UtilityByAttacksScoreCandidate) || !float.IsFinite(pair.Value.SafetyScore)) return;
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (score.Tile == null) continue;
            int next = RectIntExtensions.GetDistanceTo(zone.Area, score.Tile);
            if (next >= distance) continue;
            // Safety remains untouched: a useful advance can now compete with cover-only
            // movement, rather than being restricted to equally safe tiles or Idle choices.
            edit(score).Utility = threshold * (1 + Math.Min(1f, (distance - next) / 4f)) * support;
        }
    }
}
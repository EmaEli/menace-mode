using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tools;
using Math = System.Math;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static class CombinedArmsRuntime
{
    // Exact human-crewed combat templates in the supplied catalog. Never a MACHINE-wide exemption.
    internal static bool CrewedVehicle(string id) => id is
        "enemy.vehicle_rogue_army_auto_autocannon_walker" or "enemy.vehicle_rogue_army_auto_laser_walker" or
        "enemy.vehicle_rogue_army_battle_tank" or "enemy.vehicle_rogue_army_heavy_tank" or
        "enemy.vehicle_rogue_army_mercenary_medium_walker" or "enemy.pirate_vehicle.chaingun_guntruck" or
        "enemy.pirate_vehicle.laser_guntruck" or "enemy.pirate_vehicle.heavy_mg_heavy_truck";

    internal static bool Ready(Agent agent, bool requireAP = true)
    {
        var actor = agent?.GetActor(); var role = agent?.GetRole();
        return actor != null && role != null && ContactConvergenceRuntime.EligibleFaction(agent.GetFaction()) &&
            !role.AvoidOpponents && role.Move > 0 && float.IsFinite(role.Move) &&
            !agent.IsSleeping() && !agent.IsDeploymentPhase() && !actor.IsDying() && !actor.IsMoving() &&
            !actor.IsContainedWithinAnotherEntity() && (!requireAP || !actor.IsActionPointsSpent()) && !actor.IsStunned() &&
            float.IsFinite(actor.GetMoralePct()) && actor.GetMoralePct() >= .75f &&
            float.IsFinite(actor.GetSuppressionPct()) && actor.GetSuppressionPct() <= .25f &&
            !actor.HasTag(TagType.XENO) && !actor.HasTag(TagType.DRONE);
    }

    internal static SupportUnit Snapshot(Agent agent, bool primeIds)
    {
        if (!Ready(agent, false)) return null;
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (current == null) return null;
        SupportKind kind;
        if (actor.IsVehicle())
        {
            var template = actor.GetTemplate();
            string id = primeIds ? template?.GetID() : template?.m_ID;
            if (!CrewedVehicle(id)) return null;
            kind = SupportKind.Vehicle;
        }
        else
        {
            if (!actor.IsInfantry() || actor.HasTag(TagType.MACHINE) || actor.HasTag(TagType.INDIRECT_FIRE)) return null;
            kind = actor.HasTag(TagType.WEAPONS_TEAM) ? SupportKind.Heavy : SupportKind.Infantry;
        }
        var zones = agent.GetZones();
        bool ordered = AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, actor, out var zone, out var order);
        if (zones == null || (!ordered && zones.Count != 0) || (ordered && agent.GetRole().ZoneImportanceWeight <= 0)) return null;
        int x = current.GetX(), z = current.GetZ(), aimX = x, aimZ = z;
        if (ordered)
        {
            if (RectIntExtensions.IsInside(zone.Area, current)) return null;
            aimX = Math.Clamp(x, order.X, order.X + order.Width - 1);
            aimZ = Math.Clamp(z, order.Z, order.Z + order.Height - 1);
        }
        if (kind == SupportKind.Infantry && !ordered) return null;
        if (kind != SupportKind.Infantry && Weapons(agent, primeIds).Count == 0) return null;
        return new(actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), kind, x, z, order, ordered, aimX, aimZ);
    }

    internal static List<InflictDamage> Weapons(Agent agent, bool primeIds)
    {
        var result = new List<InflictDamage>();
        var behaviors = agent.GetBehaviors();
        if (behaviors == null || behaviors.Count > 128) return result;
        foreach (var behavior in behaviors)
        {
            var damage = behavior?.TryCast<InflictDamage>(); var skill = damage?.GetSkill(); var t = skill?.GetTemplate();
            if (t == null) continue;
            string id = primeIds ? t.GetID() : t.m_ID;
            if (string.IsNullOrEmpty(id) || !t.IsAttack || !t.IsTargeted || !t.IsLineOfFireNeeded ||
                (int)t.AoEType != 0 || skill.IsOutOfUses() || !skill.IsEnabled() || !skill.HasExecutingElements() ||
                t.AIConfig == null || !Positive(t.AIConfig.ScoreMult) || skill.GetActionPointCost() <= 0) continue;
            result.Add(damage);
            if (result.Count == 2) break;
        }
        return result;
    }

    // true means this asset owns its support policy even when no safe/useful position was found.
    // Prevents the less-conservative generic advance policy undoing AT/defence safeguards.
    internal static bool Plan(Agent agent, TileScores tiles, CombinedArmsState state,
        OffensivePlanningBudget budget, Map map, int round)
    {
        var asset = Snapshot(agent, false);
        if (asset == null || asset.Kind == SupportKind.Infantry || !Ready(agent)) return false;
        if (tiles == null || tiles.Count < 1 || tiles.Count > 4096) return true;
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (!tiles.TryGetValue(current, out var here) || !Finite(here)) return true;
        var requests = state.Candidates(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset);
        if (requests.Length == 0) return false;
        foreach (var behavior in agent.GetBehaviors())
            if (behavior != null && behavior.GetScore() > 0 && behavior.TryCast<Attack>() == null &&
                behavior.GetID().ToString() is not ("Idle" or "Move")) return true;
        var weapons = Weapons(agent, false);
        var targets = SupportProjection.DetectedTargets(agent.GetFaction());
        long start = Stopwatch.GetTimestamp(); int probes = 0;
        foreach (var request in requests)
        {
            var origins = Origins(tiles, here, request);
            var edits = new List<SupportEdit>();
            foreach (var origin in origins)
            {
                if (probes >= OffensivePlanningBudget.PerEvaluation || (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency >= 20) break;
                var projection = SupportProjection.Evaluate(agent, origin, request, weapons, targets, budget, map, round, ref probes, start);
                if (projection.Value <= 0) continue;
                float utility = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
                if (!Positive(utility)) return true;
                float desired = Math.Max(projection.Value, utility * (1 + SupportQuality(origin.Tile, request)));
                edits.Add(new(origin, desired, projection.Attack, projection.Cost));
            }
            if (edits.Count == 0 || !state.Claim(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset, request)) continue;
            bool done = false;
            try { foreach (var edit in edits) edit.Apply(); done = true; }
            finally { if (!done) { foreach (var edit in edits) edit.Restore(); state.Release(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset.Actor); } }
            return true;
        }
        state.Release(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset.Actor);
        return true;
    }

    internal static List<TileScore> Origins(TileScores tiles, TileScore here, SupportUnit request)
    {
        var result = new List<(TileScore Score, float Rank)>();
        int cx = here.Tile.GetX(), cz = here.Tile.GetZ();
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (!Finite(score) || pair.Key == null || pair.Key.Pointer != score.Tile.Pointer || score.SafetyScore > here.SafetyScore) continue;
            int x = score.Tile.GetX(), z = score.Tile.GetZ();
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > 36 ||
                (long)(x - request.X) * (request.AimX - request.X) + (long)(z - request.Z) * (request.AimZ - request.Z) > 0 ||
                (x - request.X) * (x - request.X) + (z - request.Z) * (z - request.Z) < 9) continue;
            result.Add((score, Quality(x, z, request)));
        }
        result.Sort((a, b) => b.Rank.CompareTo(a.Rank));
        var selected = new List<TileScore>();
        // Always compare holding against moving. Holding a useful deployed position avoids setup churn.
        foreach (var item in result) if (item.Score.Tile.Pointer == here.Tile.Pointer) selected.Add(item.Score);
        foreach (var item in result)
            if (selected.Count < 8 && item.Score.Tile.Pointer != here.Tile.Pointer) selected.Add(item.Score);
        return selected;
    }

    internal static bool Behind(Tile tile, SupportUnit request) =>
        (long)(tile.GetX() - request.X) * (request.AimX - request.X) +
        (long)(tile.GetZ() - request.Z) * (request.AimZ - request.Z) <= 0;
    internal static float SupportQuality(Tile tile, SupportUnit request)
        => Quality(tile.GetX(), tile.GetZ(), request);
    private static float Quality(int x, int z, SupportUnit request)
    {
        double distance = Math.Sqrt((x - request.X) * (x - request.X) + (z - request.Z) * (z - request.Z));
        return (float)Math.Max(0, 1 - Math.Abs(distance - 6) / 24);
    }
    internal static int Distance2(Tile a, Tile b) => Distance2(a, b.GetX(), b.GetZ());
    internal static int Distance2(Tile a, int x, int z) => (a.GetX() - x) * (a.GetX() - x) + (a.GetZ() - z) * (a.GetZ() - z);
    private static bool Positive(float v) => float.IsFinite(v) && v > 0;
    private static bool Finite(TileScore score) => score?.Tile != null && float.IsFinite(score.SafetyScore) &&
        float.IsFinite(score.UtilityScore) && float.IsFinite(score.UtilityByAttacksScore) &&
        float.IsFinite(score.UtilityByAttacksScoreCandidate);

    private sealed class SupportEdit
    {
        private readonly TileScore _score;
        private readonly float _utility, _attack, _nextUtility, _nextAttack;
        private readonly int _cost, _nextCost;
        internal SupportEdit(TileScore score, float utility, float attack, int cost)
        {
            _score = score; _utility = score.UtilityScore; _attack = score.UtilityByAttacksScore; _cost = score.MinimumUtilityAPCost;
            _nextAttack = Math.Max(_attack, attack); _nextUtility = Math.Max(_utility, utility);
            _nextCost = Math.Min(_cost, cost);
        }
        internal void Apply() { _score.UtilityScore = _nextUtility; _score.UtilityByAttacksScore = _nextAttack; _score.MinimumUtilityAPCost = _nextCost; }
        internal void Restore() { _score.UtilityScore = _utility; _score.UtilityByAttacksScore = _attack; _score.MinimumUtilityAPCost = _cost; }
    }
}
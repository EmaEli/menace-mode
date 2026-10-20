using System;
using System.Collections.Generic;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tools;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace LargerMaps;

internal enum AdvanceRecoveryResult
{
    Input, Orders, Faction, Zone, Position, History, Contact, ActorState, Resources,
    ActionScore, AttackCandidates, Arrival, Weights, TileData, TileUtility, Geometry,
    Arithmetic, Progress, CurrentTile, Applied
}

internal static class AdvanceRecoveryRuntime
{
    // Equivalent to the missing single-zone contributions reviewed in 0.7.17:
    // ConsiderZones.Evaluate 0x7DBA63 and PostProcess 0x7DC01D. No contact override.
    internal static bool TryApply(Actor actor, TileScores tiles, AdvanceRecoveryState state, long map, int round)
        => TryApply(actor, tiles, state, map, round, out _);

    internal static bool TryApply(Actor actor, TileScores tiles, AdvanceRecoveryState state, long map, int round,
        out AdvanceRecoveryResult result)
    {
        result = AdvanceRecoveryResult.Input;
        if (actor == null || tiles == null || tiles.Count < 2 || tiles.Count > 4096) return false;
        Agent agent = actor.GetAgent();
        AIFaction faction = agent?.GetFaction();
        var role = agent?.GetRole();
        var zones = agent?.GetZones();
        // One unambiguous advance order only. Mixed, defensive and future orders fail closed.
        result = AdvanceRecoveryResult.Orders;
        if (faction == null || role == null || zones == null || zones.Count != 1) return false;
        result = AdvanceRecoveryResult.Faction;
        int kind = (int)faction.GetFactionType();
        if (kind < 5 || kind > 9 || faction.IsAlliedWithPlayer() || role.AvoidOpponents) return false;
        result = AdvanceRecoveryResult.Zone;
        Zone zone = zones[0];
        if (zone == null || (int)zone.Order != 1) return false;
        var area = zone.Area;
        if (area.width <= 0 || area.height <= 0 ||
            !Positive(zone.Priority) || !Positive(role.ZoneImportanceWeight)) return false;
        result = AdvanceRecoveryResult.Position;
        Tile current = actor.GetTile();
        if (current == null) return false;
        var order = new AdvanceRecoveryOrder(area.xMin, area.yMin, area.width, area.height, zone.Priority);
        bool stalled = state.Observe(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), current.GetX(), current.GetZ(), order);
        result = AdvanceRecoveryResult.History;
        if (!stalled) return false;
        result = AdvanceRecoveryResult.Contact;
        if (!faction.HasKnownOpponent()) return false;
        result = AdvanceRecoveryResult.ActorState;
        if (agent.IsDeploymentPhase() || agent.IsSleeping() ||
            actor.IsMoving() || actor.IsDying() || actor.IsActionPointsSpent() || actor.IsContainedWithinAnotherEntity() ||
            actor.TilesMovedThisTurn != 0 || actor.GetSuppressionPct() != 0) return false;
        result = AdvanceRecoveryResult.Resources;
        float morale = actor.GetMoralePct();
        int ap = actor.GetActionPoints();
        if (!float.IsFinite(morale) || morale < 1 || ap <= 0) return false;

        // Attack candidates have already been collected; no skill/LoS/attack evaluation is called here.
        foreach (Behavior behavior in agent.GetBehaviors())
        {
            result = AdvanceRecoveryResult.ActionScore;
            string id = behavior.GetID().ToString();
            if (id != "Idle" && id != "Move" && behavior.GetScore() > 0) return false;
            result = AdvanceRecoveryResult.AttackCandidates;
            Attack attack = behavior.TryCast<Attack>();
            if (attack != null && (attack.m_Candidates == null || attack.m_Candidates.Count != 0)) return false;
        }
        result = AdvanceRecoveryResult.Arrival;
        int currentDistance = RectIntExtensions.GetDistanceTo(area, current);
        if (currentDistance <= 0) return false; // Arrival is not a patrol order.
        result = AdvanceRecoveryResult.Weights;
        var weights = AIConfig.WEIGHTS;
        if (weights == null || !Positive(weights.DistanceToZones) || !Positive(weights.DistanceToAdvanceZones) ||
            !Positive(weights.UtilityThreshold) || !float.IsFinite(role.UtilityScale) || role.UtilityScale < 0 ||
            !Positive(role.UtilityThresholdScale)) return false;
        float zoneWeight = role.ZoneImportanceWeight * zone.Priority;
        float distanceWeight = weights.DistanceToZones * weights.DistanceToAdvanceZones * zoneWeight;
        float insideUtility = System.Math.Max(weights.UtilityThreshold, weights.UtilityThreshold * role.UtilityScale) *
            role.UtilityThresholdScale * role.ZoneImportanceWeight * zone.Priority;
        if (!Positive(distanceWeight) || !Positive(insideUtility)) return false;

        var edits = new List<(TileScore Score, float Distance, float Utility, float NewDistance, float NewUtility)>();
        bool progress = false, foundCurrent = false;
        foreach (var pair in tiles)
        {
            result = AdvanceRecoveryResult.TileData;
            TileScore score = pair.Value;
            if (score == null || score.Tile == null || !float.IsFinite(score.DistanceScore) ||
                !float.IsFinite(score.SafetyScore) || !float.IsFinite(score.UtilityScore) || !float.IsFinite(score.UtilityByAttacksScore) ||
                !float.IsFinite(score.UtilityByAttacksScoreCandidate)) return false;
            result = AdvanceRecoveryResult.TileUtility;
            if (score.UtilityScore > 0 ||
                score.UtilityByAttacksScore > 0 || score.UtilityByAttacksScoreCandidate > 0) return false;
            foundCurrent |= score.Tile.Pointer == current.Pointer;
            result = AdvanceRecoveryResult.Geometry;
            int distance = RectIntExtensions.GetDistanceTo(area, score.Tile);
            if (distance < 0) return false;
            // APCost is still uncomputed here (native TileScore.Reset sets it to 0).
            // Move.OnEvaluate finds/clips paths and assigns costs AFTER this hook.
            // This proves geometric progress only; native Move owns reachability/AP.
            progress |= distance < currentDistance;
            float nextDistance = score.DistanceScore + distance * weights.DistanceToZones * weights.DistanceToAdvanceZones * zoneWeight;
            float nextUtility = score.UtilityScore + (RectIntExtensions.IsInside(area, score.Tile) ? insideUtility : 0);
            result = AdvanceRecoveryResult.Arithmetic;
            if (!float.IsFinite(nextDistance) || !float.IsFinite(nextUtility)) return false;
            edits.Add((score, score.DistanceScore, score.UtilityScore, nextDistance, nextUtility));
        }
        result = AdvanceRecoveryResult.Progress;
        if (!progress) return false;
        result = AdvanceRecoveryResult.CurrentTile;
        if (!foundCurrent) return false;
        bool committed = false;
        try
        {
            foreach (var edit in edits) { edit.Score.DistanceScore = edit.NewDistance; edit.Score.UtilityScore = edit.NewUtility; }
            committed = true;
        }
        finally
        {
            if (!committed)
                foreach (var edit in edits) { edit.Score.DistanceScore = edit.Distance; edit.Score.UtilityScore = edit.Utility; }
        }
        // Safety/AP/path fields, attack utility, Move state and behavior scores are never written.
        result = AdvanceRecoveryResult.Applied;
        return true;
    }

    internal static bool IdleOnly(Agent agent, Behavior chosen)
    {
        if (chosen == null || chosen.GetID().ToString() != "Idle") return false;
        foreach (Behavior behavior in agent.GetBehaviors())
            if (behavior.GetID().ToString() != "Idle" && behavior.GetScore() > 0) return false;
        return true;
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0;
}
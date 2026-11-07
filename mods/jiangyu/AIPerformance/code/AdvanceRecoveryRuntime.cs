using System;
using System.Collections.Generic;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tools;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal enum AdvanceRecoveryResult
{
    Input, Orders, Faction, Zone, Position, History, Contact, ActorState, Resources,
    ActionScore, AttackCandidates, Arrival, Weights, TileData, TileUtility, Geometry,
    Arithmetic, Progress, CurrentTile, Applied, AlreadyApplied
}

internal static class AdvanceRecoveryRuntime
{
    // Missing single-zone contributions reviewed on exact 0.7.17 and 0.7.18 binaries.
    // Explicit AdvanceTowards orders can move unseen units; attack/contact candidates are never fabricated.
    internal static bool TryApply(Actor actor, TileScores tiles, AdvanceRecoveryState state, long map, int round)
        => TryApply(actor, tiles, state, map, round, out _);

    internal static bool TryApply(Actor actor, TileScores tiles, AdvanceRecoveryState state, long map, int round,
        out AdvanceRecoveryResult result)
        => TryApply(actor, tiles, state, map, round, false, out result, out _, out _);

    internal static bool TryApply(Actor actor, TileScores tiles, AdvanceRecoveryState state, long map, int round,
        bool collectDiagnostics, out AdvanceRecoveryResult result, out bool diagnosticCandidate, out bool stalled)
    {
        result = AdvanceRecoveryResult.Input;
        diagnosticCandidate = false;
        stalled = false;
        if (actor == null || tiles == null || tiles.Count < 2 || tiles.Count > 4096) return false;
        Agent agent = actor.GetAgent();
        AIFaction faction = agent?.GetFaction();
        var role = agent?.GetRole();
        var zones = agent?.GetZones();
        int kind = faction == null ? -1 : (int)faction.GetFactionType();
        bool alliedWithPlayer = faction != null && faction.IsAlliedWithPlayer();
        if (collectDiagnostics && kind >= 5 && kind <= 9 && !alliedWithPlayer && zones != null)
            foreach (Zone candidateZone in zones)
                if (candidateZone != null && (int)candidateZone.Order == 1)
                {
                    diagnosticCandidate = true;
                    break;
                }
        // One unambiguous advance order only. Mixed, defensive and future orders fail closed.
        result = AdvanceRecoveryResult.Orders;
        if (faction == null || role == null || zones == null || zones.Count != 1) return false;
        result = AdvanceRecoveryResult.Faction;
        if (kind < 5 || kind > 9 || alliedWithPlayer || role.AvoidOpponents) return false;
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
        stalled = state.Observe(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), current.GetX(), current.GetZ(), order);
        result = AdvanceRecoveryResult.History;
        if (!stalled) return false;
        if (state.HasRecoveryApplication(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64()))
        {
            result = AdvanceRecoveryResult.AlreadyApplied;
            return false;
        }
        result = AdvanceRecoveryResult.Contact;
        if (!faction.HasKnownOpponent())
        {
            state.MarkMoveFallbackEligible(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64());
            return false;
        }
        result = AdvanceRecoveryResult.ActorState;
        if (agent.IsDeploymentPhase() || agent.IsSleeping() ||
            actor.IsMoving() || actor.IsDying() || actor.IsActionPointsSpent() || actor.IsContainedWithinAnotherEntity() ||
            actor.TilesMovedThisTurn != 0 || actor.GetSuppressionPct() != 0) return false;
        result = AdvanceRecoveryResult.Resources;
        float morale = actor.GetMoralePct();
        int ap = actor.GetActionPoints();
        if (!float.IsFinite(morale) || morale < 1 || ap <= 0) return false;

        // Attack candidates already collected; no skill/LoS/attack evaluation here.
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
            // APCost is uncomputed here. Native Move later owns reachability and AP.
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
        state.MarkRecoveryApplied(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64());
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

    internal static bool TryGetAdvanceMove(Agent agent, Actor actor, Behavior chosen, out Move move)
    {
        move = null;
        if (agent == null || actor == null || chosen == null || chosen.GetID().ToString() != "Idle") return false;
        AIFaction faction = agent.GetFaction();
        var role = agent.GetRole();
        var zones = agent.GetZones();
        if (faction == null || role == null || zones == null || zones.Count != 1 ||
            faction.IsAlliedWithPlayer() || (int)faction.GetFactionType() < 5 || (int)faction.GetFactionType() > 9 ||
            role.AvoidOpponents || agent.IsDeploymentPhase() || agent.IsSleeping() || actor.IsMoving() || actor.IsDying() ||
            actor.IsContainedWithinAnotherEntity() || actor.TilesMovedThisTurn != 0 || actor.IsActionPointsSpent() ||
            actor.GetSuppressionPct() != 0 || !float.IsFinite(actor.GetMoralePct()) || actor.GetMoralePct() < 1 ||
            actor.GetActionPoints() <= 0) return false;

        Zone zone = zones[0];
        if (zone == null || (int)zone.Order != 1 || zone.Area.width <= 0 || zone.Area.height <= 0 ||
            !Positive(zone.Priority) || !Positive(role.ZoneImportanceWeight)) return false;
        Tile current = actor.GetTile();
        if (current == null) return false;
        int currentDistance = RectIntExtensions.GetDistanceTo(zone.Area, current);
        if (currentDistance <= 0) return false;

        var behaviors = agent.GetBehaviors();
        if (behaviors == null) return false;
        foreach (Behavior behavior in behaviors)
        {
            if (behavior == null) return false;
            string id = behavior.GetID().ToString();
            if (id == "Idle") continue;
            if (id == "Move")
            {
                if (move != null) return false;
                move = behavior.TryCast<Move>();
                if (move == null) return false;
                continue;
            }
            if (behavior.GetScore() > 0) return false;
            Attack attack = behavior.TryCast<Attack>();
            if (attack != null && (attack.m_Candidates == null || attack.m_Candidates.Count != 0)) return false;
        }

        int ap = actor.GetActionPoints();
        TileScore destination = move?.GetTargetTile();
        if (destination?.Tile == null || destination.APCost <= 0 || destination.APCost > ap) return false;
        int destinationDistance = RectIntExtensions.GetDistanceTo(zone.Area, destination.Tile);
        return destinationDistance >= 0 && destinationDistance < currentDistance;
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0;
}
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
    Arithmetic, Progress, CurrentTile, Applied, AlreadyApplied, MultipleOrders
}

internal static class AdvanceRecoveryRuntime
{
    internal const int MaxAdvanceOrders = 8;
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
        if (collectDiagnostics && kind >= 5 && kind <= 9 && !alliedWithPlayer && zones != null &&
            zones.Count <= MaxAdvanceOrders)
            foreach (Zone candidateZone in zones)
                if (candidateZone != null && (int)candidateZone.Order == 1)
                {
                    diagnosticCandidate = true;
                    break;
                }
        // Only bounded, exclusively AdvanceTowards orders. Never pick an arbitrary first zone.
        result = AdvanceRecoveryResult.Orders;
        if (faction == null || role == null || zones == null || zones.Count < 1 || zones.Count > MaxAdvanceOrders) return false;
        result = AdvanceRecoveryResult.Faction;
        if (kind < 5 || kind > 9 || alliedWithPlayer || role.AvoidOpponents) return false;
        result = zones.Count == 1 ? AdvanceRecoveryResult.Zone : AdvanceRecoveryResult.Orders;
        if (!TryReadAdvanceOrders(agent, actor, out Zone zone, out AdvanceRecoveryOrder order))
        {
            state.Choice(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), false);
            return false;
        }
        result = AdvanceRecoveryResult.Zone;
        var area = zone.Area;
        if (!Positive(role.ZoneImportanceWeight)) return false;
        result = AdvanceRecoveryResult.Position;
        Tile current = actor.GetTile();
        if (current == null) return false;
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
        if (zones.Count > 1)
        {
            // Native multi-zone distance weighting is NOT the single-zone formula.
            // Keep its scores intact; enable only guarded HasUtility progress toward
            // the ID-allocated zone, then let native Move own ranking/path/AP.
            state.MarkMoveFallbackEligible(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64());
            result = AdvanceRecoveryResult.MultipleOrders;
            return false;
        }
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
        return chosen != null && chosen.GetID().ToString() == "Idle" &&
            TryGetAdvanceDestination(agent, actor, out move);
    }

    internal static bool IsProvisionalAdvanceChoice(Agent agent, Actor actor, Behavior chosen)
        => chosen != null && chosen.GetID().ToString() == "Move" &&
            TryGetAdvanceDestination(agent, actor, out Move move) && chosen.Pointer == move.Pointer;

    private static bool TryGetAdvanceDestination(Agent agent, Actor actor, out Move move)
    {
        if (!TryGetAdvanceContext(agent, actor, out move, out Zone zone, out Tile current)) return false;
        if (move.IsMovementDone() || move.HasMovedThisTurn() || move.IsDelayingMovement() ||
            move.IsInsideContainerAndInert()) return false;
        int ap = actor.GetActionPoints();
        TileScore destination = move.GetTargetTile();
        if (destination?.Tile == null || destination.APCost <= 0 || destination.APCost > ap) return false;
        int destinationDistance = RectIntExtensions.GetDistanceTo(zone.Area, destination.Tile);
        return destinationDistance >= 0 && destinationDistance < RectIntExtensions.GetDistanceTo(zone.Area, current);
    }

    // Called before native HasUtility, not after native destination/path selection.
    // Only explicit order progress receives utility; no hypothetical attack search.
    internal static bool TrySeedAdvanceUtility(Move movement, Actor actor, TileScores tiles,
        AdvanceRecoveryState state, long map, int round, out int seeded)
    {
        seeded = 0;
        Agent agent = actor?.GetAgent();
        if (movement == null || agent == null || tiles == null || tiles.Count < 2 || tiles.Count > 4096 ||
            !state.HasMoveFallbackEligibility(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64()) ||
            !ValidateFallbackContext(actor, state, map, round) ||
            !TryGetAdvanceContext(agent, actor, out Move move, out Zone zone, out Tile current) ||
            move.Pointer != movement.Pointer || move.IsMovementDone() || move.HasMovedThisTurn() ||
            move.IsDelayingMovement() || move.IsInsideContainerAndInert()) return false;
        if (!tiles.TryGetValue(current, out TileScore currentScore) || currentScore?.Tile == null ||
            currentScore.Tile.Pointer != current.Pointer || !float.IsFinite(currentScore.SafetyScore)) return false;
        float threshold = move.GetUtilityThreshold();
        if (!Positive(threshold)) return false;
        int currentDistance = RectIntExtensions.GetDistanceTo(zone.Area, current);
        var edits = new List<(TileScore Score, float Utility)>();
        foreach (var pair in tiles)
        {
            TileScore score = pair.Value;
            if (score?.Tile == null || !float.IsFinite(score.SafetyScore) || !float.IsFinite(score.DistanceScore) ||
                !float.IsFinite(score.UtilityScore) || !float.IsFinite(score.UtilityByAttacksScore) ||
                !float.IsFinite(score.UtilityByAttacksScoreCandidate) || score.UtilityScore > 0 ||
                score.UtilityByAttacksScore > 0 || score.UtilityByAttacksScoreCandidate > 0) return false;
            int distance = RectIntExtensions.GetDistanceTo(zone.Area, score.Tile);
            if (distance < 0) return false;
            if (distance < currentDistance && score.SafetyScore >= currentScore.SafetyScore)
                edits.Add((score, score.UtilityScore));
        }
        if (edits.Count == 0) return false;
        bool committed = false;
        try
        {
            foreach (var edit in edits) edit.Score.UtilityScore = threshold;
            committed = true;
        }
        finally
        {
            if (!committed) foreach (var edit in edits) edit.Score.UtilityScore = edit.Utility;
        }
        // APCost may still be zero: native Move owns reachability, clipping and safety ranking.
        seeded = edits.Count;
        return true;
    }

    internal static bool ValidateFallbackContext(Actor actor, AdvanceRecoveryState state, long map, int round)
    {
        Agent agent = actor?.GetAgent();
        if (agent == null) return false;
        Tile tile = actor.GetTile();
        bool valid = tile != null && TryReadAdvanceOrders(agent, actor, out _, out AdvanceRecoveryOrder order) &&
            state.MatchesContext(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), tile.GetX(), tile.GetZ(), order);
        if (!valid) state.Choice(map, round, actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), false);
        return valid;
    }

    private static bool TryReadAdvanceOrders(Agent agent, Actor actor, out Zone selected, out AdvanceRecoveryOrder order)
    {
        selected = null; order = default;
        var zones = agent?.GetZones();
        if (actor == null || zones == null || zones.Count < 1 || zones.Count > MaxAdvanceOrders) return false;
        int count = zones.Count;
        // Reviewed native ConsiderZones uses Entity.ID % advance-zone count.
        int id = count == 1 ? 0 : actor.ID;
        if (id < 0) return false;
        int index = id % count;
        var context = count == 1 ? null : new string[count];
        for (int i = 0; i < count; i++)
        {
            Zone zone = zones[i];
            if (zone == null || (int)zone.Order != 1 || !Positive(zone.Priority)) return false;
            var area = zone.Area;
            if (area.width <= 0 || area.height <= 0) return false;
            if (context != null)
                context[i] = FormattableString.Invariant($"{area.xMin},{area.yMin},{area.width},{area.height},{zone.Priority:R}");
            if (i == index)
            {
                selected = zone;
                order = new(area.xMin, area.yMin, area.width, area.height, zone.Priority);
            }
        }
        if (context != null) order = order with { Context = id + ":" + string.Join(";", context) };
        return zones.Count == count;
    }

    private static bool TryGetAdvanceContext(Agent agent, Actor actor, out Move move, out Zone zone, out Tile current)
    {
        move = null; zone = null; current = null;
        if (agent == null || actor == null) return false;
        AIFaction faction = agent.GetFaction();
        var role = agent.GetRole();
        if (faction == null || role == null ||
            faction.IsAlliedWithPlayer() || (int)faction.GetFactionType() < 5 || (int)faction.GetFactionType() > 9 ||
            role.AvoidOpponents || agent.IsDeploymentPhase() || agent.IsSleeping() || actor.IsMoving() || actor.IsDying() ||
            actor.IsContainedWithinAnotherEntity() || actor.TilesMovedThisTurn != 0 || actor.IsActionPointsSpent() ||
            actor.GetSuppressionPct() != 0 || !float.IsFinite(actor.GetMoralePct()) || actor.GetMoralePct() < 1 ||
            actor.GetActionPoints() <= 0) return false;

        if (!TryReadAdvanceOrders(agent, actor, out zone, out _) || !Positive(role.ZoneImportanceWeight)) return false;
        current = actor.GetTile();
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

        return move != null;
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0;
}
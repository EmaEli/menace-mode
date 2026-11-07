using System;
using System.Collections.Generic;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static class ContactConvergenceRuntime
{
    internal static bool EligibleFaction(AIFaction faction) => faction != null &&
        !faction.IsAlliedWithPlayer() && ((int)faction.GetFactionType() == 6 || (int)faction.GetFactionType() == 9);

    internal static void Observe(AIFaction faction, Actor opponent, ContactMemoryState state, long map, int round)
    {
        if (!EligibleFaction(faction) || opponent == null) return;
        int kind = (int)faction.GetFactionType();
        // Detection is checked BEFORE any position, faction or death read on the opponent.
        if (!opponent.IsDetectedByFaction(kind)) return;
        int other = (int)opponent.GetFaction();
        if ((other != 1 && other != 2 && other != 4) || faction.IsAlliedWith(other)) return;
        if (opponent.IsDying())
        {
            state.Forget(map, round, faction.Pointer.ToInt64(), opponent.Pointer.ToInt64());
            return;
        }
        Tile tile = opponent.GetTile();
        if (tile != null) state.Observe(map, round, faction.Pointer.ToInt64(), opponent.Pointer.ToInt64(), tile.GetX(), tile.GetZ());
    }

    internal static bool TrySeed(Move move, TileScores tiles, ContactMemoryState state, long map, int round, out int seeded)
    {
        seeded = 0;
        Actor actor = move?.GetAgent()?.GetActor();
        Agent agent = actor?.GetAgent();
        var faction = agent?.GetFaction();
        var role = agent?.GetRole();
        var zones = agent?.GetZones();
        if (actor == null || !EligibleFaction(faction) || role == null || role.AvoidOpponents ||
            !float.IsFinite(role.Move) || role.Move <= 0 || zones == null || zones.Count != 0 ||
            agent.IsDeploymentPhase() || agent.IsSleeping() || !actor.IsInfantry() || actor.IsVehicle() ||
            actor.HasTag(TagType.MACHINE) || actor.HasTag(TagType.XENO) || actor.HasTag(TagType.DRONE) ||
            actor.HasTag(TagType.INDIRECT_FIRE) || actor.HasTag(TagType.WEAPONS_TEAM) ||
            actor.IsMoving() || actor.IsDying() || actor.IsContainedWithinAnotherEntity() || actor.IsActionPointsSpent() ||
            actor.TilesMovedThisTurn != 0 || actor.GetSuppressionPct() != 0 ||
            !float.IsFinite(actor.GetMoralePct()) || actor.GetMoralePct() < 1 ||
            move.IsMovementDone() || move.HasMovedThisTurn() || move.IsDelayingMovement() || move.IsInsideContainerAndInert() ||
            tiles == null || tiles.Count < 2 || tiles.Count > 4096) return false;
        var behaviors = agent.GetBehaviors();
        if (behaviors == null || behaviors.Count > 128) return false;
        foreach (Behavior behavior in behaviors)
        {
            if (behavior == null) return false;
            string id = behavior.GetID().ToString();
            if (id != "Idle" && id != "Move" && behavior.GetScore() > 0) return false;
            Attack attack = behavior.TryCast<Attack>();
            if (attack != null && (attack.m_Candidates == null || attack.m_Candidates.Count != 0)) return false;
        }
        Tile current = actor.GetTile();
        if (current == null || !tiles.TryGetValue(current, out var currentScore) || currentScore?.Tile == null ||
            currentScore.Tile.Pointer != current.Pointer || !float.IsFinite(currentScore.SafetyScore)) return false;
        float threshold = move.GetUtilityThreshold();
        if (!float.IsFinite(threshold) || threshold <= 0) return false;
        // Preserve every existing movement/skill utility, even if it is below threshold.
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (score?.Tile == null || !float.IsFinite(score.SafetyScore) || !float.IsFinite(score.DistanceScore) ||
                !float.IsFinite(score.UtilityScore) || !float.IsFinite(score.UtilityByAttacksScore) ||
                !float.IsFinite(score.UtilityByAttacksScoreCandidate) || score.UtilityScore > 0 ||
                score.UtilityByAttacksScore > 0 || score.UtilityByAttacksScoreCandidate > 0) return false;
        }
        if (!state.TryRespond(map, round, faction.Pointer.ToInt64(), actor.Pointer.ToInt64(), agent.Pointer.ToInt64(),
            current.GetX(), current.GetZ(), out var estimate)) return false;
        int distance = ContactMemoryState.Distance(current.GetX(), current.GetZ(), estimate);
        var edits = new List<(Il2CppMenace.Tactical.AI.Data.TileScore Score, float Before)>();
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (ContactMemoryState.Distance(score.Tile.GetX(), score.Tile.GetZ(), estimate) < distance &&
                score.SafetyScore >= currentScore.SafetyScore) edits.Add((score, score.UtilityScore));
        }
        bool committed = false;
        try
        {
            // Native Move still owns destination ranking, clipping, reachability and AP.
            foreach (var edit in edits) edit.Score.UtilityScore = threshold;
            committed = true;
        }
        finally
        {
            if (!committed) foreach (var edit in edits) edit.Score.UtilityScore = edit.Before;
        }
        seeded = edits.Count;
        return seeded > 0;
    }
}
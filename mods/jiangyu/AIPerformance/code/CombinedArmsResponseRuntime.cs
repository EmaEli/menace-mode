using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;
using Il2CppTactical;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static class CombinedArmsResponseRuntime
{
    // Threat response is independent of whether the infantry anchor survived or can still advance.
    internal static SupportThreat? Threat(Agent agent, CombinedArmsThreatState memory, Map map, int round)
    {
        if (map == null || !CombinedArmsRuntime.Ready(agent)) return null;
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (current == null || !AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, actor, out _, out var order)) return null;
        SupportThreat? best = null; int nearest = int.MaxValue;
        foreach (var threat in memory.Get(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64()))
        {
            if (threat.X >= map.Width || threat.Z >= map.Height) continue;
            int distance = CombinedArmsRuntime.Distance2(current, threat.X, threat.Z);
            int ox = Math.Clamp(threat.X, order.X, order.X + order.Width - 1);
            int oz = Math.Clamp(threat.Z, order.Z, order.Z + order.Height - 1);
            // Respect the assigned mission sector, not arbitrary map-wide convergence.
            if (distance > 48 * 48 || (threat.X - ox) * (threat.X - ox) + (threat.Z - oz) * (threat.Z - oz) > 24 * 24) continue;
            if (distance < nearest) { nearest = distance; best = threat; }
        }
        return best;
    }

    internal static bool HasUsefulAction(Agent agent)
    {
        var behaviors = agent.GetBehaviors();
        if (behaviors == null || behaviors.Count > 128) return true;
        foreach (var behavior in behaviors)
            if (behavior != null && behavior.GetScore() > 0 && behavior.GetID().ToString() is not ("Idle" or "Move")) return true;
        return false;
    }

    internal static bool PlanVehicle(Agent agent, TileScores tiles, CombinedArmsThreatState memory,
        OffensivePlanningBudget budget, Map map, int round, bool diagnostics)
    {
        var asset = CombinedArmsRuntime.Snapshot(agent, false);
        if (asset is not { Kind: SupportKind.Vehicle } ||
            Threat(agent, memory, map, round) is not { } threat || tiles == null || tiles.Count > 4096) return false;
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (!tiles.TryGetValue(current, out var here) || !Finite(here)) return false;
        var origins = new List<(TileScore Score, int Rank)>();
        int distance = CombinedArmsRuntime.Distance2(current, threat.X, threat.Z);
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (!Finite(score) || pair.Key == null || pair.Key.Pointer != score.Tile.Pointer || score.APCost < 0 ||
                CombinedArmsRuntime.Distance2(score.Tile, current) > 36) continue;
            int next = CombinedArmsRuntime.Distance2(score.Tile, threat.X, threat.Z);
            if (score.Tile.Pointer != current.Pointer && (next >= distance || next < 9)) continue;
            origins.Add((score, score.Tile.Pointer == current.Pointer ? int.MinValue : next));
        }
        origins.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) :
            a.Score.Tile.Pointer.ToInt64().CompareTo(b.Score.Tile.Pointer.ToInt64()));
        var target = map.GetTile(threat.X, threat.Z);
        bool detected = false;
        foreach (var tile in SupportProjection.DetectedTargets(agent.GetFaction()))
            if (tile.Pointer == target.Pointer) { detected = true; break; }
        var weapons = CombinedArmsRuntime.Weapons(agent, false);
        var edits = new List<(TileScore Score, float Utility, float Attack, int Cost, float Next, float Shot, int Reserve)>();
        int probes = 0; long started = Stopwatch.GetTimestamp();
        float threshold = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
        if (!(threshold > 0 && float.IsFinite(threshold))) return false;
        for (int i = 0; i < Math.Min(4, origins.Count); i++)
        {
            var score = origins[i].Score; float shot = 0; int reserve = 1024;
            foreach (var damage in weapons)
            {
                int cost = SupportProjection.Reserve(damage, actor, score.Tile.Pointer != current.Pointer);
                if (cost <= 0 || (long)score.APCost + cost > actor.GetActionPoints()) continue;
                // Only live-target queries spend the shared probe budget. Native scores can
                // be recreated several times before Execute; budget exhaustion must not erase
                // a still-safe geometric approach that needs no speculative target query.
                if (detected && probes < 8 && (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency <= 10 &&
                    budget.Spend(map.Pointer.ToInt64(), round, asset.Actor, asset.Agent, agent.GetFaction().Pointer.ToInt64(), 4))
                {
                    probes++;
                    var skill = damage.GetSkill();
                    if (skill.IsUsableOn(target, score.Tile, TargetUsageParams.IGNORE_DEPLOYMENT_AND_ANGLE) &&
                        skill.IsApplicableTo(target, null) && skill.GetFinalUserTile(score.Tile, target)?.Pointer == score.Tile.Pointer)
                    {
                        float value = damage.GetTargetValue(false, skill, target, score.Tile, target) *
                            skill.GetTemplate().AIConfig.ScoreMult * AIConfig.WEIGHTS.InflictDamageFromTile;
                        if (float.IsFinite(value) && value > 0) { shot = Math.Max(shot, value); reserve = Math.Min(reserve, cost); }
                    }
                }
                // Staging is allowed toward a last-known threat, but cannot fabricate a shot
                // or increase danger. A real shot competes against UNCHANGED native safety.
                if (shot <= 0 && score.Tile.Pointer != current.Pointer && score.SafetyScore <= here.SafetyScore)
                    reserve = Math.Min(reserve, cost);
            }
            if (reserve == 1024 || (shot <= 0 && score.Tile.Pointer == current.Pointer)) continue;
            float progress = 1f - (float)Math.Sqrt(CombinedArmsRuntime.Distance2(score.Tile, threat.X, threat.Z)) / Math.Max(1, (float)Math.Sqrt(distance));
            float utility = Math.Max(score.UtilityScore, shot > 0 ? Math.Max(shot, threshold * 2) : threshold * (1 + progress));
            edits.Add((score, score.UtilityScore, score.UtilityByAttacksScore, score.MinimumUtilityAPCost,
                utility, Math.Max(score.UtilityByAttacksScore, shot), Math.Min(score.MinimumUtilityAPCost, reserve)));
        }
        bool done = false;
        try
        {
            foreach (var edit in edits) { edit.Score.UtilityScore = edit.Next; edit.Score.UtilityByAttacksScore = edit.Shot; edit.Score.MinimumUtilityAPCost = edit.Reserve; }
            done = true;
        }
        finally
        {
            if (!done) foreach (var edit in edits) { edit.Score.UtilityScore = edit.Utility; edit.Score.UtilityByAttacksScore = edit.Attack; edit.Score.MinimumUtilityAPCost = edit.Cost; }
        }
        if (diagnostics) Plugin.CombinedArmsDiagnostic(round, agent.GetFaction().Pointer.ToInt64(), asset.Actor,
            "response", $"kind=Vehicle threat=({threat.X},{threat.Z}) detected={detected} positions={edits.Count} probes={probes}");
        return edits.Count > 0;
    }

    internal static Behavior GuardAdvance(Agent agent, Behavior chosen, CombinedArmsThreatState memory,
        Map map, int round, Func<Tile, bool> smoke)
    {
        var move = chosen?.TryCast<Move>(); var request = CombinedArmsRuntime.Snapshot(agent, false);
        if (move == null || request is not { Kind: SupportKind.Infantry } ||
            Threat(agent, memory, map, round) == null) return chosen;
        var target = move.GetTargetTile(); var current = agent.GetActor().GetTile(); var tiles = agent.GetTiles();
        if (!Finite(target) || !tiles.TryGetValue(current, out var here) || !Finite(here) ||
            !CombinedArmsRuntime.Forward(current, target.Tile, request) || target.SafetyScore < here.SafetyScore) return chosen;
        var threats = memory.Get(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64());
        int probes = 0; long started = Stopwatch.GetTimestamp();
        bool Exposed(Tile tile)
        {
            foreach (var danger in threats)
            {
                int range = danger.Range + 3 + Math.Max(0, round - danger.SeenRound) * 2;
                if (danger.X >= map.Width || danger.Z >= map.Height ||
                    CombinedArmsRuntime.Distance2(tile, danger.X, danger.Z) > range * range) continue;
                if (smoke?.Invoke(tile) ?? false) continue;
                if (++probes > 32 || (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency > 5) return true;
                if (map.GetTile(danger.X, danger.Z).HasLineOfSightTo(tile, LineOfSightFlags.Default)) return true;
            }
            return false;
        }
        bool blocked = Exposed(target.Tile);
        var path = move.m_Path;
        // Inspect the native clipped route, never run a second pathfinder. Long/missing
        // routes in a threatened sector cannot be certified safe within this small cap.
        if (!blocked && (path == null || path.Count == 0 || path.Count > 16)) blocked = Exposed(current);
        if (!blocked && path != null && path.Count <= 16)
            for (int i = 0; i < path.Count; i++)
            {
                var tile = map.GetTileAtPos(path[i]);
                if (tile == null || (tile.Pointer != current.Pointer && Exposed(tile))) { blocked = true; break; }
            }
        if (!blocked) return chosen;
        foreach (var behavior in agent.GetBehaviors())
            if (behavior != null && behavior.GetID().ToString() == "Idle") return behavior;
        return chosen;
    }

    private static bool Finite(TileScore score) => score?.Tile != null && float.IsFinite(score.SafetyScore) &&
        float.IsFinite(score.UtilityScore) && float.IsFinite(score.UtilityByAttacksScore);
}
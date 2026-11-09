using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppTactical;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static class CombinedArmsThreatRuntime
{
    internal static void Observe(AIFaction faction, Actor enemy, CombinedArmsThreatState memory, Map map, int round)
    {
        if (!ContactConvergenceRuntime.EligibleFaction(faction) || enemy == null ||
            !enemy.IsDetectedByFaction((int)faction.GetFactionType())) return;
        int kind = (int)enemy.GetFaction();
        if ((kind != 1 && kind != 2) || faction.IsAlliedWith(kind)) return;
        long group = faction.Pointer.ToInt64(), target = enemy.Pointer.ToInt64();
        if (enemy.IsDying() || enemy.IsContainedWithinAnotherEntity())
        { memory.Forget(map.Pointer.ToInt64(), round, group, target); return; }
        if (!memory.BeginObservation(map.Pointer.ToInt64(), round, group, target)) return;
        var origin = enemy.GetTile(); var attacks = enemy.GetAllAttacks(); var allies = faction.GetActors();
        if (origin == null || attacks == null || attacks.Count > 128 || allies == null || allies.Count > 128) return;
        int probes = 0; long started = Stopwatch.GetTimestamp();
        foreach (var ally in allies)
        {
            var request = CombinedArmsRuntime.Snapshot(ally?.GetAgent(), true);
            if (request is not { Kind: SupportKind.Infantry }) continue;
            var tile = ally.GetTile();
            foreach (var skill in attacks)
            {
                if (++probes > 32 || (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency > 5) return;
                var template = skill?.GetTemplate();
                if (template == null || !template.IsAttack || !template.IsTargeted || !template.IsLineOfFireNeeded ||
                    !skill.IsEnabled() || skill.IsOutOfUses() || !skill.HasExecutingElements()) continue;
                int range = skill.GetMaxRange();
                if (range < 1 || range > 128 || !skill.IsApplicableTo(tile, null) || !skill.IsInRange(tile, origin, false) ||
                    !origin.HasLineOfSightTo(tile, LineOfSightFlags.Default)) continue;
                memory.Observe(map.Pointer.ToInt64(), round, group,
                    new(target, origin.GetX(), origin.GetZ(), range, round));
                return;
            }
        }
    }

    internal static int Flank(Agent agent, TileScores tiles, CombinedArmsThreatState memory,
        OffensivePlanningBudget budget, Map map, int round)
    {
        var request = CombinedArmsRuntime.Snapshot(agent, false);
        if (request is not { Kind: SupportKind.Infantry } || !CombinedArmsRuntime.Ready(agent) || tiles == null ||
            tiles.Count < 2 || tiles.Count > 4096) return 0;
        var behaviors = agent.GetBehaviors();
        if (behaviors == null || behaviors.Count > 128) return 0;
        foreach (var behavior in behaviors)
            if (behavior != null && behavior.GetScore() > 0 && behavior.GetID().ToString() is not ("Idle" or "Move")) return 0;
        var current = agent.GetActor().GetTile();
        if (current == null || !tiles.TryGetValue(current, out var here) || here?.Tile == null ||
            here.Tile.Pointer != current.Pointer || !float.IsFinite(here.SafetyScore)) return 0;
        var threats = memory.Get(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64());
        if (threats.Length == 0) return 0;
        var candidates = new List<(TileScore Score, int Rank, int X, int Z)>();
        foreach (var pair in tiles)
        {
            var score = pair.Value;
            if (score?.Tile == null || pair.Key == null || pair.Key.Pointer != score.Tile.Pointer ||
                score.Tile.Pointer == current.Pointer || !float.IsFinite(score.UtilityScore) ||
                !float.IsFinite(score.SafetyScore) || score.SafetyScore > here.SafetyScore || score.APCost < 0 ||
                score.APCost >= agent.GetActor().GetActionPoints() || CombinedArmsRuntime.Distance2(score.Tile, current) > 36 ||
                !CombinedArmsRuntime.Forward(current, score.Tile, request)) continue;
            int x = score.Tile.GetX(), z = score.Tile.GetZ();
            candidates.Add((score, (x - request.AimX) * (x - request.AimX) + (z - request.AimZ) * (z - request.AimZ), x, z));
        }
        // Keep sorting managed-only; never interop into the game for thousands of comparisons.
        candidates.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) :
            a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
        float threshold = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
        if (!float.IsFinite(threshold) || threshold <= 0) return 0;
        var edits = new List<(TileScore Score, float Before, float After)>();
        int probes = 0; long started = Stopwatch.GetTimestamp();
        foreach (var threat in threats)
        {
            if (threat.X >= map.Width || threat.Z >= map.Height) continue;
            int uncertainty = 3 + (round - threat.SeenRound) * 2;
            if (CombinedArmsRuntime.Distance2(current, threat.X, threat.Z) > (threat.Range + uncertainty) * (threat.Range + uncertainty)) continue;
            var origin = map.GetTile(threat.X, threat.Z);
            if (!Spend()) break;
            if (!origin.HasLineOfSightTo(current, LineOfSightFlags.Default)) continue;
            foreach (var candidate in candidates)
            {
                if (!Spend()) break;
                var score = candidate.Score;
                if (CombinedArmsRuntime.Distance2(score.Tile, threat.X, threat.Z) <= uncertainty * uncertainty ||
                    origin.HasLineOfSightTo(score.Tile, LineOfSightFlags.Default)) continue;
                edits.Add((score, score.UtilityScore, Math.Max(score.UtilityScore, threshold * 1.5f)));
                if (edits.Count == 4) break;
            }
            if (edits.Count > 0) break; // One local threat response per evaluation.
        }
        bool done = false;
        try { foreach (var edit in edits) edit.Score.UtilityScore = edit.After; done = true; }
        finally { if (!done) foreach (var edit in edits) edit.Score.UtilityScore = edit.Before; }
        return edits.Count;

        bool Spend()
        {
            if (++probes > 16 || (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency > 5) return false;
            return budget.Spend(map.Pointer.ToInt64(), round, request.Actor, request.Agent, agent.GetFaction().Pointer.ToInt64());
        }
    }
}
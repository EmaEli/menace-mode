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
    internal struct OriginDiagnostics
    {
        internal int Examined, Invalid, LessSafe, TooFar, Ahead, TooClose, Ranked, Selected;
    }

    // Exact human-crewed combat templates in the supplied catalog. Never a MACHINE-wide exemption.
    internal static bool CrewedVehicle(string id) => id is
        "enemy.vehicle_rogue_army_auto_autocannon_walker" or "enemy.vehicle_rogue_army_auto_laser_walker" or
        "enemy.vehicle_rogue_army_battle_tank" or "enemy.vehicle_rogue_army_heavy_tank" or
        "enemy.vehicle_rogue_army_mercenary_medium_walker" or "enemy.pirate_vehicle.chaingun_guntruck" or
        "enemy.pirate_vehicle.laser_guntruck" or "enemy.pirate_vehicle.heavy_mg_heavy_truck";

    internal static bool Ready(Agent agent, bool requireAP = true)
    {
        return Readiness(agent, requireAP) == SupportSnapshotReason.Eligible;
    }

    private static SupportSnapshotReason Readiness(Agent agent, bool requireAP)
    {
        if (agent == null) return SupportSnapshotReason.MissingAgent;
        var actor = agent?.GetActor(); var role = agent?.GetRole();
        if (actor == null) return SupportSnapshotReason.MissingActor;
        if (role == null) return SupportSnapshotReason.MissingRole;
        if (!ContactConvergenceRuntime.EligibleFaction(agent.GetFaction())) return SupportSnapshotReason.IneligibleFaction;
        if (role.AvoidOpponents) return SupportSnapshotReason.RoleAvoidsOpponents;
        if (!(role.Move > 0 && float.IsFinite(role.Move))) return SupportSnapshotReason.MovementDisabled;
        if (agent.IsSleeping()) return SupportSnapshotReason.Sleeping;
        if (agent.IsDeploymentPhase()) return SupportSnapshotReason.DeploymentPhase;
        if (actor.IsDying()) return SupportSnapshotReason.Dying;
        if (actor.IsMoving()) return SupportSnapshotReason.Moving;
        if (actor.IsContainedWithinAnotherEntity()) return SupportSnapshotReason.Contained;
        if (requireAP && actor.IsActionPointsSpent()) return SupportSnapshotReason.NoActionPoints;
        if (actor.IsStunned()) return SupportSnapshotReason.Stunned;
        if (!(float.IsFinite(actor.GetMoralePct()) && actor.GetMoralePct() >= .75f)) return SupportSnapshotReason.LowMorale;
        if (!(float.IsFinite(actor.GetSuppressionPct()) && actor.GetSuppressionPct() <= .25f)) return SupportSnapshotReason.Suppressed;
        if (actor.HasTag(TagType.XENO)) return SupportSnapshotReason.Xeno;
        if (actor.HasTag(TagType.DRONE)) return SupportSnapshotReason.Drone;
        return SupportSnapshotReason.Eligible;
    }

    internal static SupportUnit Snapshot(Agent agent, bool primeIds)
        => Snapshot(agent, primeIds, out _);

    internal static SupportUnit Snapshot(Agent agent, bool primeIds, out SupportSnapshotReason reason)
    {
        reason = Readiness(agent, false);
        if (reason != SupportSnapshotReason.Eligible) return null;
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (current == null) { reason = SupportSnapshotReason.MissingTile; return null; }
        SupportKind kind;
        if (actor.IsVehicle())
        {
            var template = actor.GetTemplate();
            string id = primeIds ? template?.GetID() : template?.m_ID;
            if (!CrewedVehicle(id)) { reason = SupportSnapshotReason.UnrecognizedCrewedVehicle; return null; }
            kind = SupportKind.Vehicle;
        }
        else
        {
            if (!actor.IsInfantry()) { reason = SupportSnapshotReason.NotInfantry; return null; }
            if (actor.HasTag(TagType.MACHINE)) { reason = SupportSnapshotReason.MachineInfantry; return null; }
            if (actor.HasTag(TagType.INDIRECT_FIRE)) { reason = SupportSnapshotReason.IndirectFire; return null; }
            kind = actor.HasTag(TagType.WEAPONS_TEAM) ? SupportKind.Heavy : SupportKind.Infantry;
        }
        var zones = agent.GetZones();
        bool ordered = AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, actor, out var zone, out var order);
        if (zones == null) { reason = SupportSnapshotReason.MissingZones; return null; }
        if (!ordered && zones.Count != 0) { reason = SupportSnapshotReason.InvalidOrders; return null; }
        if (ordered && agent.GetRole().ZoneImportanceWeight <= 0) { reason = SupportSnapshotReason.DisabledOrder; return null; }
        int x = current.GetX(), z = current.GetZ(), aimX = x, aimZ = z;
        if (ordered)
        {
            if (RectIntExtensions.IsInside(zone.Area, current)) { reason = SupportSnapshotReason.InsideAdvanceZone; return null; }
            aimX = Math.Clamp(x, order.X, order.X + order.Width - 1);
            aimZ = Math.Clamp(z, order.Z, order.Z + order.Height - 1);
        }
        if (kind == SupportKind.Infantry && !ordered) { reason = SupportSnapshotReason.InfantryWithoutAdvanceOrder; return null; }
        if (kind != SupportKind.Infantry && Weapons(agent, primeIds).Count == 0)
        { reason = SupportSnapshotReason.NoDirectFireWeapon; return null; }
        reason = SupportSnapshotReason.Eligible;
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
        OffensivePlanningBudget budget, Map map, int round, bool diagnostics = false)
    {
        var asset = Snapshot(agent, false, out var snapshotReason);
        long actorId = asset?.Actor ?? 0, factionId = 0;
        if (diagnostics)
        {
            try
            {
                if (actorId == 0) actorId = agent?.GetActor()?.Pointer.ToInt64() ?? 0;
                factionId = agent?.GetFaction()?.Pointer.ToInt64() ?? 0;
            }
            catch { diagnostics = false; }
        }
        if (asset == null)
        {
            if (diagnostics && snapshotReason is not (SupportSnapshotReason.IneligibleFaction or
                SupportSnapshotReason.NotInfantry or SupportSnapshotReason.MachineInfantry or
                SupportSnapshotReason.IndirectFire or SupportSnapshotReason.InfantryWithoutAdvanceOrder))
                Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan", $"result=skipped gate=snapshot:{snapshotReason}");
            return false;
        }
        if (asset.Kind == SupportKind.Infantry) return false;
        var readiness = Readiness(agent, true);
        if (readiness != SupportSnapshotReason.Eligible)
        {
            if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan", $"result=skipped gate=readiness:{readiness} kind={asset.Kind}");
            return false;
        }
        if (tiles == null || tiles.Count < 1 || tiles.Count > 4096)
        {
            if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan",
                $"result=policy-owned gate=tile-scores count={(tiles == null ? -1 : tiles.Count)}");
            return true;
        }
        var actor = agent.GetActor(); var current = actor.GetTile();
        if (!tiles.TryGetValue(current, out var here) || !Finite(here))
        {
            if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan", "result=policy-owned gate=current-tile-score-missing-or-invalid");
            return true;
        }
        var requests = state.Candidates(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset,
            diagnostics, out var candidateDiagnostics);
        string candidateSummary = null;
        if (diagnostics)
        {
            var candidateIds = new List<string>(requests.Length);
            foreach (var request in requests)
                candidateIds.Add($"{request.Actor:X}@{request.X},{request.Z}->({request.AimX},{request.AimZ})");
            candidateSummary = $"candidateGate={candidateDiagnostics.Gate} nearby={candidateDiagnostics.Nearby} " +
                $"compatible={candidateDiagnostics.Compatible} slotBusy={candidateDiagnostics.SlotBusy} " +
                $"returned={candidateDiagnostics.Returned} truncated={candidateDiagnostics.Truncated} " +
                $"assigned={candidateDiagnostics.AssignedRequest:X} requests=[{string.Join(";", candidateIds)}]";
        }
        if (requests.Length == 0)
        {
            if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan", $"result=no-request {candidateSummary}");
            return false;
        }
        foreach (var behavior in agent.GetBehaviors())
        {
            if (behavior == null) continue;
            float score = behavior.GetScore();
            if (!(score > 0) || behavior.TryCast<Attack>() != null) continue;
            string behaviorId = behavior.GetID().ToString();
            if (behaviorId is not ("Idle" or "Move"))
            {
                if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan",
                    $"result=preserved-native-action id={behaviorId} score={score:F2} {candidateSummary}");
                return true;
            }
        }
        var weapons = Weapons(agent, false);
        var targets = SupportProjection.DetectedTargets(agent.GetFaction());
        long start = Stopwatch.GetTimestamp(); int probes = 0, claimFailures = 0;
        var attempts = diagnostics ? new List<string>(requests.Length) : null;
        foreach (var request in requests)
        {
            var origins = Origins(tiles, here, request, diagnostics, out var originDiagnostics);
            var edits = new List<SupportEdit>();
            var seeds = diagnostics ? new List<string>(origins.Count) : null;
            var projectionDiagnostics = default(SupportProjection.DiagnosticCounts);
            string stop = "complete";
            foreach (var origin in origins)
            {
                if (probes >= OffensivePlanningBudget.PerEvaluation) { stop = "probe-cap"; break; }
                if ((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency >= 20) { stop = "soft-time"; break; }
                var projection = SupportProjection.Evaluate(agent, origin, request, weapons, targets, budget, map,
                    round, ref probes, start, diagnostics, out var projectionCount);
                if (diagnostics)
                {
                    projectionDiagnostics.TargetsConsidered += projectionCount.TargetsConsidered;
                    projectionDiagnostics.TargetUsable += projectionCount.TargetUsable;
                    projectionDiagnostics.TargetApplicable += projectionCount.TargetApplicable;
                    projectionDiagnostics.TargetOriginMatches += projectionCount.TargetOriginMatches;
                    projectionDiagnostics.PositiveDamage += projectionCount.PositiveDamage;
                    projectionDiagnostics.LaneSamples += projectionCount.LaneSamples;
                    projectionDiagnostics.LaneInRange += projectionCount.LaneInRange;
                    projectionDiagnostics.LaneClear += projectionCount.LaneClear;
                    projectionDiagnostics.BudgetStopped |= projectionCount.BudgetStopped;
                }
                if (projection.Value <= 0) continue;
                float utility = AIConfig.WEIGHTS.UtilityThreshold * agent.GetRole().UtilityThresholdScale;
                if (!Positive(utility))
                {
                    if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan",
                        $"result=policy-owned gate=invalid-utility request={request.Actor:X} probes={probes}");
                    return true;
                }
                bool moving = origin.Tile.Pointer != current.Pointer;
                // Empty-lane geometry may justify catching up, never repositioning to an ideal rear gap.
                if (moving && projection.Attack <= 0 &&
                    (Distance2(current, request.X, request.Z) <= 36 ||
                     Distance2(origin.Tile, request.X, request.Z) >= Distance2(current, request.X, request.Z))) continue;
                float desired = Math.Max(projection.Value, utility * (1 + SupportQuality(origin.Tile, request)));
                edits.Add(new(origin, desired, projection.Attack, projection.Cost));
                if (diagnostics)
                {
                    try { seeds.Add($"({origin.Tile.GetX()},{origin.Tile.GetZ()}):u={desired:F2},attack={projection.Attack:F2},ap={projection.Cost}"); }
                    catch { }
                }
            }
            bool claimed = edits.Count > 0 && state.Claim(map.Pointer.ToInt64(), round,
                agent.GetFaction().Pointer.ToInt64(), asset, request);
            if (!claimed)
            {
                if (edits.Count > 0) claimFailures++;
                if (diagnostics)
                    attempts.Add($"request={request.Actor:X} origins={origins.Count}/{originDiagnostics.Ranked} " +
                        $"filters=invalid:{originDiagnostics.Invalid},unsafe:{originDiagnostics.LessSafe},far:{originDiagnostics.TooFar}," +
                        $"ahead:{originDiagnostics.Ahead},close:{originDiagnostics.TooClose} positive={edits.Count} " +
                        $"targets={projectionDiagnostics.TargetsConsidered}/{targets.Count} usable={projectionDiagnostics.TargetUsable} " +
                        $"applicable={projectionDiagnostics.TargetApplicable} originMatch={projectionDiagnostics.TargetOriginMatches} " +
                        $"damage={projectionDiagnostics.PositiveDamage} lanes={projectionDiagnostics.LaneSamples}/" +
                        $"{projectionDiagnostics.LaneInRange}/{projectionDiagnostics.LaneClear} " +
                        $"probes={probes} budgetStop={projectionDiagnostics.BudgetStopped} stop={stop} claim=not-needed-or-rejected");
                continue;
            }
            bool done = false;
            try { foreach (var edit in edits) edit.Apply(); done = true; }
            finally { if (!done) { foreach (var edit in edits) edit.Restore(); state.Release(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset.Actor); } }
            if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan",
                $"result=score-seeded assignment=claimed asset={asset.Actor:X} kind={asset.Kind} " +
                $"request={request.Actor:X}@{request.X},{request.Z}->({request.AimX},{request.AimZ}) " +
                $"origins={origins.Count}/{originDiagnostics.Ranked} positive={edits.Count} probes={probes} " +
                $"targets={projectionDiagnostics.TargetsConsidered}/{targets.Count} usable={projectionDiagnostics.TargetUsable} " +
                $"applicable={projectionDiagnostics.TargetApplicable} originMatch={projectionDiagnostics.TargetOriginMatches} " +
                $"damage={projectionDiagnostics.PositiveDamage} lanes={projectionDiagnostics.LaneSamples}/" +
                $"{projectionDiagnostics.LaneInRange}/{projectionDiagnostics.LaneClear} budgetStop={projectionDiagnostics.BudgetStopped} " +
                $"seeds=[{(seeds == null ? "" : string.Join(";", seeds))}] {candidateSummary}");
            return true;
        }
        state.Release(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset.Actor);
        if (diagnostics) Plugin.CombinedArmsDiagnostic(round, factionId, actorId, "plan",
            $"result=no-score-seed assignment=released asset={asset.Actor:X} kind={asset.Kind} " +
            $"weapons={weapons.Count} detectedTargets={targets.Count} " +
            $"claimFailures={claimFailures} probes={probes} candidates={candidateSummary} attempts=[{string.Join(";", attempts)}]");
        return true;
    }

    internal static List<TileScore> Origins(TileScores tiles, TileScore here, SupportUnit request)
        => Origins(tiles, here, request, false, out _);

    internal static List<TileScore> Origins(TileScores tiles, TileScore here, SupportUnit request,
        bool collectDiagnostics, out OriginDiagnostics diagnostics)
    {
        diagnostics = default;
        var result = new List<(TileScore Score, float Rank)>();
        int cx = here.Tile.GetX(), cz = here.Tile.GetZ();
        foreach (var pair in tiles)
        {
            if (collectDiagnostics) diagnostics.Examined++;
            var score = pair.Value;
            if (!Finite(score) || pair.Key == null || pair.Key.Pointer != score.Tile.Pointer)
            {
                if (collectDiagnostics) diagnostics.Invalid++;
                continue;
            }
            if (score.SafetyScore > here.SafetyScore)
            {
                if (collectDiagnostics) diagnostics.LessSafe++;
                continue;
            }
            int x = score.Tile.GetX(), z = score.Tile.GetZ();
            if (score.Tile.Pointer != here.Tile.Pointer && !Forward(here.Tile, score.Tile, request)) continue;
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > 36)
            {
                if (collectDiagnostics) diagnostics.TooFar++;
                continue;
            }
            if (score.Tile.Pointer != here.Tile.Pointer && (long)(x - request.X) * (request.AimX - request.X) +
                (long)(z - request.Z) * (request.AimZ - request.Z) > 0)
            {
                if (collectDiagnostics) diagnostics.Ahead++;
                continue;
            }
            if (score.Tile.Pointer != here.Tile.Pointer &&
                (x - request.X) * (x - request.X) + (z - request.Z) * (z - request.Z) < 9)
            {
                if (collectDiagnostics) diagnostics.TooClose++;
                continue;
            }
            result.Add((score, Quality(x, z, request)));
        }
        if (collectDiagnostics) diagnostics.Ranked = result.Count;
        result.Sort((a, b) => b.Rank.CompareTo(a.Rank));
        var selected = new List<TileScore>();
        // Always compare holding against moving. Holding a useful deployed position avoids setup churn.
        foreach (var item in result) if (item.Score.Tile.Pointer == here.Tile.Pointer) selected.Add(item.Score);
        foreach (var item in result)
            if (selected.Count < 8 && item.Score.Tile.Pointer != here.Tile.Pointer) selected.Add(item.Score);
        if (collectDiagnostics) diagnostics.Selected = selected.Count;
        return selected;
    }

    internal static bool Behind(Tile tile, SupportUnit request) =>
        (long)(tile.GetX() - request.X) * (request.AimX - request.X) +
        (long)(tile.GetZ() - request.Z) * (request.AimZ - request.Z) <= 0;
    internal static bool Forward(Tile current, Tile next, SupportUnit request) =>
        (long)(next.GetX() - current.GetX()) * (request.AimX - request.X) +
        (long)(next.GetZ() - current.GetZ()) * (request.AimZ - request.Z) >= 0;

    internal static Behavior PreventSpacingRetreat(Agent agent, Behavior chosen, CombinedArmsState state, Map map, int round,
        SupportThreat? response = null)
    {
        var asset = Snapshot(agent, false);
        var move = chosen?.TryCast<Move>();
        if (asset is not { Kind: SupportKind.Vehicle } || move == null ||
            Readiness(agent, true) != SupportSnapshotReason.Eligible) return chosen;
        var requests = state.Candidates(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), asset);
        if (requests.Length == 0) return chosen;
        var target = move.GetTargetTile(); var current = agent.GetActor().GetTile(); var tiles = agent.GetTiles();
        if (!Finite(target) || current == null || tiles == null || !tiles.TryGetValue(current, out var here) || !Finite(here)) return chosen;
        // Leave genuine native danger avoidance and a better firing position alone.
        if (target.SafetyScore < here.SafetyScore) return chosen;
        // Preprocessing sees APCost=0 on fresh native scores. Recheck the real clipped path
        // at choice time as well; never promise a firing reserve from the pre-path estimate.
        bool reserveLost = target.APCost > 0 && target.MinimumUtilityAPCost > 0 && target.MinimumUtilityAPCost < 1024 &&
            (long)target.APCost + target.MinimumUtilityAPCost > agent.GetActor().GetActionPoints();
        bool responseApproach = response is { } threat &&
            Distance2(target.Tile, threat.X, threat.Z) < Distance2(current, threat.X, threat.Z);
        if (!reserveLost && (responseApproach || Forward(current, target.Tile, requests[0]) ||
            target.UtilityByAttacksScore > here.UtilityByAttacksScore ||
            target.UtilityByAttacksScoreCandidate > here.UtilityByAttacksScoreCandidate)) return chosen;
        foreach (var behavior in agent.GetBehaviors())
            if (behavior != null && behavior.GetID().ToString() == "Idle") return behavior;
        return chosen;
    }
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
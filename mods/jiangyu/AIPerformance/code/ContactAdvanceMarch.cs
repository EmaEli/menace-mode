using System;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Candidates = Il2CppSystem.Collections.Generic.List<Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

internal static partial class ContactAdvanceRuntime
{
    internal static TileScore MarchChoice(Move move, Candidates candidates, TileScore native,
        ContactAdvanceState memory, Map map, int round)
    {
        var agent = move?.GetAgent();
        if (native?.Tile == null || candidates == null || candidates.Count < 2 || candidates.Count > 4096 ||
            !Ready(agent) || move.IsMovementDone() || move.IsDelayingMovement() || move.IsInsideContainerAndInert() ||
            CombinedArmsResponseRuntime.HasUsefulAction(agent) || !TryAim(agent, memory, map, round, out var contact)) return native;
        var actor = agent.GetActor(); var current = actor.GetTile(); var tiles = agent.GetTiles();
        if (current == null || tiles == null || !tiles.TryGetValue(current, out var here) || here == null ||
            here.UtilityScore != 0 || !float.IsFinite(here.UtilityByAttacksScore) || here.UtilityByAttacksScore > 0) return native;
        int faction = (int)agent.GetFaction().GetFactionType(), ap = actor.GetActionPoints();
        bool bugs = faction == 7;
        // Bugs keep closing on visible troops until a useful native action is available.
        // Other factions still restore ordinary tactical positioning on their own sight.
        if (!bugs)
        {
            var opponents = agent.GetFaction().GetOpponents();
            if (opponents == null || opponents.Count > 128) return native;
            // Detection before position/vision reads, including concealment and LOS.
            foreach (var opponent in opponents)
            {
                var enemy = opponent?.Actor;
                if (enemy != null && enemy.IsDetectedByFaction(faction) &&
                    !enemy.IsDying() && !enemy.IsContainedWithinAnotherEntity() &&
                    actor.HasLineOfSightTo(enemy, current, enemy.GetTile())) return native;
            }
        }
        float scale = AIConfig.WEIGHTS.DistancePickScale, threshold = move.GetUtilityThreshold();
        if (!float.IsFinite(scale) || scale <= 0 || !float.IsFinite(threshold) || threshold <= 0) return native;
        // Exact 0.7.18 GetScaledScore: safety + utility -
        // (DistanceToCurrentTile + DistanceScore) * DistancePickScale.
        // Remove ONLY the cheap-short-step preference during a contact march or bug
        // approach. Native safety, mission distance, real path/AP costs and shot AP remain.
        float Merit(TileScore score) => score.GetScaledScore() + score.DistanceToCurrentTile * scale;
        var best = native; float value = Merit(native);
        if (!float.IsFinite(value)) return native;
        int distance = Math.Max(Math.Abs(current.GetX() - contact.X), Math.Abs(current.GetZ() - contact.Z));
        int bestRemaining = Math.Max(Math.Abs(native.Tile.GetX() - contact.X), Math.Abs(native.Tile.GetZ() - contact.Z));
        foreach (var candidate in candidates)
        {
            var tile = candidate?.Tile;
            if (tile == null || candidate.APCost <= 0 || candidate.APCost > ap ||
                candidate.MinimumUtilityAPCost < 0 || (!bugs && candidate.MinimumUtilityAPCost != 0) ||
                (long)candidate.APCost + candidate.MinimumUtilityAPCost > ap ||
                !float.IsFinite(candidate.UtilityScore) || candidate.UtilityScore < threshold ||
                !float.IsFinite(candidate.DistanceToCurrentTile) || candidate.DistanceToCurrentTile < 0 ||
                tile.GetX() < 0 || tile.GetZ() < 0 || tile.GetX() >= map.Width || tile.GetZ() >= map.Height) continue;
            int remaining = Math.Max(Math.Abs(tile.GetX() - contact.X), Math.Abs(tile.GetZ() - contact.Z));
            if (remaining >= distance) continue;
            float next = Merit(candidate);
            // Equal-quality firing origins must not default to a one-tile bug shuffle.
            if (!float.IsFinite(next) || next < value || (next == value && (!bugs || remaining >= bestRemaining))) continue;
            best = candidate; value = next; bestRemaining = remaining;
        }
        return best;
    }
}

public sealed partial class Plugin
{
    private static void ContactAdvanceMarchChoice(Move __instance, Candidates __0, ref TileScore __result)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null) return;
            var native = __result;
            __result = ContactAdvanceRuntime.MarchChoice(__instance, __0, native, _contactAdvance, map, manager.m_Round);
            if (_contactAdvanceDiagnostics && native?.Pointer != __result?.Pointer)
            {
                try
                {
                    var agent = __instance.GetAgent();
                    ContactAdvanceDiagnostic(manager.m_Round, agent.GetFaction().Pointer.ToInt64(), agent.GetActor().Pointer.ToInt64(),
                        "march-choice", $"native=({native?.Tile?.GetX()},{native?.Tile?.GetZ()}) ap={native?.APCost} " +
                        $"selected=({__result?.Tile?.GetX()},{__result?.Tile?.GetZ()}) ap={__result?.APCost} available={agent.GetActor().GetActionPoints()}");
                }
                catch { } // Optional trace failures must not change the selected movement.
            }
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }
}
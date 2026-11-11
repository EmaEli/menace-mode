using AIPerformance;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;

internal static class FactionConvergenceChecks
{
    private sealed class Battle
    {
        internal AIFaction Faction;
        internal Actor Enemy = new() { Pointer = (IntPtr)90000, Tile = new(70, 40), RejectHiddenReads = true };
        internal Battle(int kind, bool detected = true)
        {
            TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
            Enemy.Detected = detected;
            Faction = new() { Kind = kind }; Faction.Opponents.Add(new() { Actor = Enemy });
            for (int i = 0; i < 45; i++)
            {
                var unit = new Actor { Pointer = (IntPtr)(1000 + i), Tile = new(10 + i % 3, 35 + i % 8), ID = i };
                unit.Agent.Pointer = (IntPtr)(2000 + i); unit.Agent.Faction = Faction;
                unit.Agent.Sleeping = true; unit.Agent.Zones.Clear();
                if (kind == 7) { unit.Tags.Add(TagType.XENO); unit.Infantry = false; unit.Morale = 0; }
                if (kind == 8) { unit.Tags.Add(TagType.MACHINE); unit.Tags.Add(TagType.DRONE); unit.Morale = 0; }
                Faction.Actors.Add(unit); Scores(unit);
            }
            Plugin.BeginAdvanceFixture(Faction, diagnostics: true);
        }
        internal static void Scores(Actor unit)
        {
            unit.Agent.Tiles.Clear();
            unit.Agent.Tiles[unit.Tile] = new() { Tile = unit.Tile, UtilityScore = 500, SafetyScore = 10 };
            var tile = new Tile(unit.Tile.GetX() + 6, unit.Tile.GetZ());
            unit.Agent.Tiles[tile] = new() { Tile = tile, APCost = 30, SafetyScore = 10 };
        }
        internal static TileScore Forward(Actor unit) => unit.Agent.Tiles.Values.Last();
    }

    internal static void Run(Action<bool, string> check)
    {
        // Native Map.UpdateVisibility -> Actor.OnDiscovered sets the bit directly,
        // then AIFaction.OnOpponentSighted. It never calls SetDetectedByFaction.
        var b = new Battle(6, detected: false);
        check(b.Faction.Actors.All(a => a.Agent.Sleeping), "no scouting clairvoyance before a legitimate sighting");
        b.Enemy.Detected = true;
        Plugin.AdvanceNativeSightFixture(b.Faction, b.Enemy);
        foreach (var unit in b.Faction.Actors) Plugin.AdvanceBeforeFixture(unit.Agent);
        check(b.Faction.Actors.All(a => !a.Agent.Sleeping), "native sighting after empty turn refresh mobilizes 45 unordered pirates without the missed setter hook");

        foreach (int kind in new[] { 5, 6, 7, 8, 9 })
        {
            b = new Battle(kind);
            check(b.Faction.Actors.All(a => !a.Agent.Sleeping), $"hostile faction {kind}: every eligible unit shares the alert");
            foreach (var unit in b.Faction.Actors)
            {
                // Movement must work for melee/non-rifle actors too, without invented attacks.
                Plugin.OffensiveScoresFixture(unit.Agent);
                check(Battle.Forward(unit).UtilityScore > 0 && Battle.Forward(unit).MinimumUtilityAPCost == 0,
                    $"faction {kind}: native local movement closes on the known sector");
                check(unit.AP == 100 && Battle.Forward(unit).APCost == 30 && unit.Agent.Zones.Count == 0,
                    "mobilization grants no AP, orders, target, path or attack utility");
            }
            var shooter = b.Faction.Actors[0];
            var shot = new InflictDamage { Owner = shooter.Agent, Score = 2 };
            shot.m_Candidates.Add(new()); shooter.Agent.Behaviors.Add(shot);
            check(Plugin.OffensiveChoiceFixture(shooter.Agent, shooter.Agent.Behaviors[0]) == shot,
                $"faction {kind}: a legal positive native attack still wins over convergence");
        }

        b = new Battle(6); Plugin.CombatSimplificationFixture(false);
        Plugin.OffensiveScoresFixture(b.Faction.Actors[0].Agent);
        check(Battle.Forward(b.Faction.Actors[0]).UtilityScore > 0, "faction gameplay is independent of simplified skill-positioning profiles");
        Plugin.CombatSimplificationFixture(true);

        foreach (int kind in new[] { 6, 9 })
        {
            b = new Battle(kind); Plugin.PublishGroupsFixture(b.Faction);
            var groups = new HashSet<long>();
            foreach (var unit in b.Faction.Actors)
            {
                bool assigned = Plugin.GroupPlanFixture(unit.Agent, out var group);
                check(assigned && group.Infantry is >= 1 and <= 4, "unordered infantry enter real production local advance groups on shared contact objectives");
                groups.Add(group.Group); Plugin.CombinedRefreshFixture(unit.Agent);
                check(Plugin.GroupPlanFixture(unit.Agent, out _), "acting-unit refresh preserves its managed contact order and group");
            }
            check(groups.Count == 12 && b.Faction.Actors.All(a => a.Agent.Zones.Count == 0), "45 unordered infantry form 12 bounded groups without mutating native mission zones");
        }

        foreach (int kind in new[] { 0, 1, 2, 3, 4 })
        {
            b = new Battle(kind);
            check(b.Faction.Actors.All(a => a.Agent.Sleeping), $"non-hostile faction {kind} remains native");
        }
        b = new Battle(8, detected: false); b.Enemy.Tile = new(95, 95);
        Plugin.AdvanceNativeSightFixture(b.Faction, b.Enemy);
        check(b.Enemy.PositionReads == 0 && b.Faction.Actors.All(a => a.Agent.Sleeping), "event without native detection never reads hidden coordinates or mobilizes");
        b.Enemy.Detected = true;
        Task.Run(() => Plugin.AdvanceNativeSightFixture(b.Faction, b.Enemy)).GetAwaiter().GetResult();
        check(b.Enemy.PositionReads == 0, "worker sighting callback does not read native enemy state");

        b = new Battle(7);
        b.Enemy.Detected = false; int reads = b.Enemy.PositionReads;
        b.Enemy.Tile = new(1, 1); TacticalManager.Instance.m_Round = 2;
        foreach (var unit in b.Faction.Actors)
        {
            Battle.Scores(unit); Plugin.AdvanceBeforeFixture(unit.Agent); Plugin.OffensiveScoresFixture(unit.Agent);
            check(Battle.Forward(unit).UtilityScore > 0, "bugs keep pressing the last reported sector, not the hidden live location");
        }
        check(b.Enemy.PositionReads == reads, "whole-faction last-seen pursuit performs zero hidden position reads");
        Plugin.AdvanceResetFixture();
        foreach (var unit in b.Faction.Actors) { unit.Agent.Sleeping = true; Plugin.AdvanceBeforeFixture(unit.Agent); }
        check(b.Faction.Actors.All(a => a.Agent.Sleeping), "mission reset invalidates all faction alerts and assignments");
        FactionAssignmentChecks.Run(check);
        FactionConvergenceGuardChecks.Run(check);
    }
}
using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Scores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

internal static class MultiOrderChecks
{
    internal static void Run(Action<bool, string> check)
    {
        (Actor Actor, Move Move, Scores Tiles) Setup(bool known = true)
        {
            Plugin.BeginFixture(); TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
            var actor = new Actor(); actor.Agent.Faction.Known = known;
            actor.Agent.Zones = [new() { Area = new(10, 0, 5, 5) }, new() { Area = new(-20, 0, 5, 5) }];
            var tiles = new Scores();
            foreach (var tile in new[] { actor.Tile, new Tile(5, 0), new Tile(-5, 0) })
                tiles.Add(tile, new() { Tile = tile, DistanceScore = 7 });
            return (actor, (Move)actor.Agent.Behaviors[1], tiles);
        }
        void Arm(Actor actor, Scores tiles)
        {
            for (int r = 1; r <= 3; r++)
            {
                TacticalManager.Instance.m_Round = r; Plugin.ScoreFixture(actor, tiles);
                if (r < 3) Plugin.ChoiceFixture(actor.Agent, actor.Agent.Behaviors[0]);
            }
        }
        foreach (bool known in new[] { false, true })
        {
            var (actor, move, tiles) = Setup(known); Arm(actor, tiles);
            Plugin.UtilityFixture(move, tiles);
            check(move.HasUtility(tiles), "two advance orders recover with/without contact: " + known);
            check(tiles.Values.All(t => t.DistanceScore == 7 && t.APCost == 0),
                "multi-order recovery leaves native weighted distances/AP untouched");
            check(tiles.Values.Single(t => t.Tile.GetX() == 5).UtilityScore == move.Threshold &&
                tiles.Values.Single(t => t.Tile.GetX() == -5).UtilityScore == 0,
                "native even actor allocation progresses toward first zone, not the other zone");
            move.TargetTile = tiles.Values.Single(t => t.Tile.GetX() == 5); move.TargetTile.APCost = 20;
            check(ReferenceEquals(Plugin.ChoiceFixture(actor.Agent, actor.Agent.Behaviors[0]), move),
                "two-order fallback selects existing affordable native target");
            for (int poll = 0; poll < 3; poll++)
            {
                foreach (var score in tiles.Values) score.UtilityScore = 0;
                move.TargetTile = null; move.Score = 0;
                Plugin.ScoreFixture(actor, tiles); Plugin.UtilityFixture(move, tiles);
                check(move.HasUtility(tiles), "provisional multi-order Move survives native reevaluation");
                move.TargetTile = tiles.Values.Single(t => t.Tile.GetX() == 5); move.TargetTile.APCost = 20;
                Plugin.ChoiceFixture(actor.Agent, move);
            }
        }
        foreach (int id in new[] { 11, 13, int.MaxValue })
        {
            var (actor, move, tiles) = Setup(); actor.ID = id; Arm(actor, tiles);
            Plugin.UtilityFixture(move, tiles);
            check(tiles.Values.Single(t => t.Tile.GetX() == -5).UtilityScore == move.Threshold &&
                tiles.Values.Single(t => t.Tile.GetX() == 5).UtilityScore == 0,
                "odd actor native allocation progresses toward second zone: " + id);
        }
        foreach (string invalid in new[] { "defense", "null zone", "zero priority", "bad geometry", "too many", "negative ID",
            "suppressed", "low morale", "sleep", "attack", "other action", "unsafe", "arrived" })
        {
            var (actor, move, tiles) = Setup();
            if (invalid == "defense") actor.Agent.Zones[1].Order = 2;
            if (invalid == "null zone") actor.Agent.Zones[1] = null;
            if (invalid == "zero priority") actor.Agent.Zones[1].Priority = 0;
            if (invalid == "bad geometry") actor.Agent.Zones[1].Area = new(10, 0, 0, 5);
            if (invalid == "too many") for (int i = 0; i < 8; i++) actor.Agent.Zones.Add(new());
            if (invalid == "negative ID") actor.ID = -1;
            if (invalid == "suppressed") actor.Suppression = .1f;
            if (invalid == "low morale") actor.Morale = .5f;
            if (invalid == "sleep") actor.Agent.Sleeping = true;
            if (invalid == "attack") ((Attack)actor.Agent.Behaviors[2]).m_Candidates.Add(new());
            if (invalid == "other action") actor.Agent.Behaviors.Add(new() { ID = "Reload", Score = 1 });
            if (invalid == "unsafe") tiles.Values.Single(t => t.Tile.GetX() == 5).SafetyScore = -1;
            if (invalid == "arrived") actor.Agent.Zones[0].Area = new(-1, -1, 5, 5);
            Arm(actor, tiles); Plugin.UtilityFixture(move, tiles);
            check(!move.HasUtility(tiles), "multi-order guards: " + invalid);
            check(tiles.Values.All(t => t.UtilityScore == 0 && t.DistanceScore == 7), "no partial writes: " + invalid);
        }
        foreach (bool utilityFirst in new[] { false, true })
        foreach (string change in new[] { "priority", "geometry", "reorder", "add", "defense", "ID" })
        {
            var (actor, move, tiles) = Setup(); Arm(actor, tiles);
            if (change == "priority") actor.Agent.Zones[1].Priority = 2;
            if (change == "geometry") actor.Agent.Zones[1].Area = new(-30, 0, 5, 5);
            if (change == "reorder") actor.Agent.Zones.Reverse();
            if (change == "add") actor.Agent.Zones.Add(new());
            if (change == "defense") actor.Agent.Zones[1].Order = 2;
            if (change == "ID") actor.ID = 11;
            // Mutation between score hook and HasUtility must not reuse prior eligibility.
            if (utilityFirst) Plugin.UtilityFixture(move, tiles);
            check(!move.HasUtility(tiles), "complete order context revalidated before utility: " + change);
            move.TargetTile = tiles.Values.Single(t => t.Tile.GetX() == 5); move.TargetTile.APCost = 20;
            check(!ReferenceEquals(Plugin.ChoiceFixture(actor.Agent, actor.Agent.Behaviors[0]), move),
                "changed orders cannot force a stale target: " + change);
        }
        {
            var (actor, move, tiles) = Setup(); Arm(actor, tiles);
            move.TargetTile = tiles.Values.Single(t => t.Tile.GetX() == -5); move.TargetTile.APCost = 20;
            check(!ReferenceEquals(Plugin.ChoiceFixture(actor.Agent, actor.Agent.Behaviors[0]), move),
                "closer to unassigned zone does not qualify as assigned-order progress");
        }
        {
            var (actor, move, tiles) = Setup(); Arm(actor, tiles);
            actor.Agent.Zones[1].Priority = 2;
            Plugin.ScoreFixture(actor, tiles); Plugin.UtilityFixture(move, tiles);
            check(!move.HasUtility(tiles), "unselected zone changes reset idle history");
        }
        {
            var (actor, move, tiles) = Setup();
            while (actor.Agent.Zones.Count < 8) actor.Agent.Zones.Add(new() { Area = new(10, 0, 5, 5) });
            Arm(actor, tiles); Plugin.UtilityFixture(move, tiles);
            check(move.HasUtility(tiles), "bounded maximum advance-order count is supported");
            check(Plugin.Logs.Count == 0, "multi-order recovery stays silent with diagnostics OFF");
        }
    }
}
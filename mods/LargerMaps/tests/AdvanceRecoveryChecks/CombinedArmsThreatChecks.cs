using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;

internal static class CombinedArmsThreatChecks
{
    internal static void Run(Action<bool, string> check)
    {
        TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
        var map = TacticalManager.Instance.GetMap();
        var memory = new CombinedArmsThreatState(); memory.Advance(1, 1);
        var faction = new AIFaction { Kind = 9 };
        var infantry = new Actor { Pointer = (IntPtr)100, Tile = map.GetTile(10, 10) };
        infantry.Agent.Faction = faction; infantry.Agent.Zones[0].Area = new(40, 10, 3, 3);
        faction.Actors.Add(infantry);
        var enemy = new Actor { Pointer = (IntPtr)500, Detected = false, RejectHiddenReads = true, Tile = map.GetTile(25, 10) };
        enemy.Attacks.Add(new Skill());
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        check(enemy.PositionReads == 0 && memory.Get(1, 1, 30).Length == 0, "never observed enemy supplies no position or weapons");
        enemy.Detected = true;
        enemy.Tile.Sees = t => t.GetZ() == 10;
        infantry.Suppression = .5f;
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        check(memory.Get(1, 1, 30).Single().X == 25,
            "visible ranged threat to advancing infantry is remembered even when its victim is suppressed");
        infantry.Suppression = 0;
        int reads = enemy.PositionReads;
        enemy.Detected = false; enemy.Tile = map.GetTile(60, 60);
        memory.Advance(1, 2);
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 2);
        check(enemy.PositionReads == reads && memory.Get(1, 2, 30).Single().X == 25,
            "lost contact retains last observed position, never hidden movement");
        var here = new TileScore { Tile = infantry.Tile, SafetyScore = 10 };
        var flank = new TileScore { Tile = map.GetTile(12, 13), SafetyScore = 10, APCost = 30 };
        var exposed = new TileScore { Tile = map.GetTile(14, 10), SafetyScore = 10 };
        var retreat = new TileScore { Tile = map.GetTile(8, 13), SafetyScore = 10 };
        var unsafeTile = new TileScore { Tile = map.GetTile(13, 13), SafetyScore = 20 };
        foreach (var score in new[] { here, flank, exposed, retreat, unsafeTile }) infantry.Agent.Tiles[score.Tile] = score;
        int seeded = CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, new(), map, 2);
        check(seeded == 1 && flank.UtilityScore > 0 && flank.UtilityByAttacksScore == 0,
            "uncertain threat rewards a native reachable forward flank out of its possible firing lane, not an attack");
        check(exposed.UtilityScore == 0 && retreat.UtilityScore == 0 && unsafeTile.UtilityScore == 0,
            "flank excludes exposed, retreating and less-safe tiles");
        float before = flank.UtilityScore;
        CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, new(), map, 2);
        check(flank.UtilityScore == before, "repeated flank evaluations do not inflate scores");
        check(memory.Get(1, 2, 99).Length == 0 && memory.Get(2, 2, 30).Length == 0,
            "memory never leaks between factions or maps");
        check(!memory.Advance(1, 1) && !memory.Advance(2, 2) && memory.Get(1, 2, 30).Length == 1,
            "late callbacks cannot rewind or replace the mission");
        memory.Advance(1, 3); check(memory.Get(1, 3, 30).Length == 1, "two-round uncertain contact survives");
        memory.Advance(1, 4); check(memory.Get(1, 4, 30).Length == 0, "unrefreshed contact expires");
        check(memory.BeginObservation(1, 4, 30, 500) && memory.BeginObservation(1, 4, 30, 500) &&
            !memory.BeginObservation(1, 4, 30, 500),
            "one bounded retry is allowed, then repeated detection callbacks stop");
        memory.Reset(); check(memory.Advance(2, 1), "mission reset permits a new map");
        // A visible but unarmed enemy must not be classified as a firing threat.
        memory.Reset(); memory.Advance(1, 1); enemy.Detected = true; enemy.Attacks.Clear();
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        check(memory.Get(1, 1, 30).Length == 0, "visible unarmed actor creates no firing-threat memory");
        memory.Reset(); memory.Advance(1, 1);
        var retrySkill = new Skill { MaxRange = 10 };
        retrySkill.Range = (target, origin) => Math.Abs(target.GetX() - origin.GetX()) <= 10;
        enemy.Attacks.Add(retrySkill); infantry.Tile = map.GetTile(10, 10); enemy.Tile = map.GetTile(25, 10);
        enemy.PositionReads = 0;
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        infantry.Tile = map.GetTile(20, 10);
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        enemy.Tile = map.GetTile(30, 10);
        CombinedArmsThreatRuntime.Observe(faction, enemy, memory, map, 1);
        check(enemy.PositionReads == 2 && memory.Get(1, 1, 30).Single().X == 25,
            "an out-of-range first observation can retry after movement and then stops without refreshing hidden coordinates");
        // Run the linked production faction/detection/scoring hooks, not just the helper.
        infantry.Tile = map.GetTile(10, 10); enemy.Attacks.Add(new Skill()); enemy.Tile = map.GetTile(25, 10);
        faction.Opponents.Add(new() { Actor = enemy }); TacticalManager.Instance.Faction = faction;
        flank.UtilityScore = 0; Plugin.BeginCombinedFixture(faction);
        enemy.Detected = false; reads = enemy.PositionReads;
        Plugin.CombinedDetectedFixture(enemy, 9); Plugin.OffensiveScoresFixture(infantry.Agent);
        check(flank.UtilityScore > 0 && enemy.PositionReads == reads && Plugin.CombinedEnabled,
            "production hooks remember a turn-start sighting and flank without hidden reads after contact loss");
        // An exposed progress tile must not receive generic advance utility when a known
        // threat has line of fire and no safe local candidate breaks that lane.
        infantry.Tile = map.GetTile(10, 10); infantry.Agent.Zones[0].Area = new(40, 10, 3, 3);
        infantry.Agent.Tiles.Clear();
        TileScore AddTile(int x, int z) { var score = new TileScore { Tile = map.GetTile(x, z), SafetyScore = 10 }; infantry.Agent.Tiles[score.Tile] = score; return score; }
        AddTile(10, 10); var exposedProgress = AddTile(14, 10); AddTile(12, 13);
        enemy.Detected = true; enemy.Tile = map.GetTile(20, 10); enemy.Tile.Sees = _ => true;
        foreach (var skill in enemy.Attacks) { skill.MaxRange = 30; skill.Range = (_, _) => true; }
        TacticalManager.Instance.m_Round = 1; Plugin.BeginCombinedFixture(faction); enemy.Detected = false;
        Plugin.OffensiveScoresFixture(infantry.Agent);
        check(exposedProgress.UtilityScore == 0 && exposedProgress.UtilityByAttacksScore == 0,
            "unresolved remembered danger blocks generic objective-advance utility on exposed tiles");
        var nativeShot = new InflictDamage { Owner = infantry.Agent };
        infantry.Agent.Behaviors.Add(nativeShot); enemy.Detected = true;
        Plugin.CombinedDetectedFixture(enemy, 9); Plugin.OffensiveScoresFixture(infantry.Agent);
        check(exposedProgress.UtilityByAttacksScore > 0,
            "unresolved danger does not suppress an otherwise valid native direct-fire opportunity");
        foreach (var score in infantry.Agent.Tiles.Values)
        { score.UtilityScore = 0; score.UtilityByAttacksScore = 0; score.UtilityByAttacksScoreCandidate = 0; }
        enemy.Detected = false; Plugin.BeginCombinedFixture(faction); Plugin.OffensiveScoresFixture(infantry.Agent);
        check(exposedProgress.UtilityScore > AIConfig.WEIGHTS.UtilityThreshold,
            "ordinary objective advance remains available when no remembered threat exists");
        // Exhausted budgets and emergency/defensive contexts cannot get flank utility.
        memory.Observe(1, 1, 30, new(500, 25, 10, 30, 1));
        var emptyBudget = new OffensivePlanningBudget();
        for (int i = 0; i < OffensivePlanningBudget.PerRound; i++)
            emptyBudget.Spend(1, 1, 100, 20, 30);
        flank.UtilityScore = 0;
        check(CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, emptyBudget, map, 1) == 0,
            "flank shares and respects the existing actor-round probe cap");
        infantry.Agent.Zones[0].Order = 0;
        check(CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, new(), map, 1) == 0,
            "defenders cannot be redirected by uncertain threats");
        infantry.Agent.Zones[0].Order = 1; infantry.Suppression = .5f;
        check(CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, new(), map, 1) == 0,
            "suppressed infantry retains native emergency decisions");
        infantry.Suppression = 0;
        var secondFlank = new TileScore { Tile = map.GetTile(13, 12), SafetyScore = 10 };
        infantry.Agent.Tiles[secondFlank.Tile] = secondFlank;
        TileScore.UtilityWritesBeforeThrow = 1; bool threw = false;
        try { CombinedArmsThreatRuntime.Flank(infantry.Agent, infantry.Agent.Tiles, memory, new(), map, 1); }
        catch (InvalidOperationException) { threw = true; }
        check(threw && flank.UtilityScore == 0 && secondFlank.UtilityScore == 0,
            "flank transaction restores all scores after a partial native-write failure");
    }
}
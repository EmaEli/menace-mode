using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;

internal static class CombinedArmsResponseChecks
{
    internal static void Run(Action<bool, string> check)
    {
        TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
        var map = TacticalManager.Instance.GetMap(); var faction = new AIFaction { Kind = 9 };
        TacticalManager.Instance.Faction = faction;
        var infantry = new Actor { Pointer = (IntPtr)100, Tile = map.GetTile(12, 10) };
        var vehicle = new Actor { Pointer = (IntPtr)200, Tile = map.GetTile(10, 10), Vehicle = true, Infantry = false };
        vehicle.Template.m_ID = "enemy.vehicle_rogue_army_auto_laser_walker";
        var enemy = new Actor { Pointer = (IntPtr)500, Tile = map.GetTile(25, 10), Detected = true, RejectHiddenReads = true };
        enemy.Attacks.Add(new()); faction.Opponents.Add(new() { Actor = enemy });
        var gun = new InflictDamage { Owner = vehicle.Agent };
        gun.Skill.Usable = (_, origin) => origin.GetX() >= 14;
        vehicle.Agent.Behaviors.Add(gun);
        foreach (var actor in new[] { infantry, vehicle })
        {
            actor.Agent.Faction = faction; actor.Agent.Pointer = actor.Pointer + 1;
            actor.Agent.Zones[0].Area = new(30, 10, 3, 3); faction.Actors.Add(actor);
            actor.Agent.Tiles[actor.Tile] = new() { Tile = actor.Tile, SafetyScore = 10 };
        }
        var front = new TileScore { Tile = map.GetTile(15, 10), SafetyScore = 20 };
        vehicle.Agent.Tiles[front.Tile] = front;
        Plugin.BeginCombinedFixture(faction);
        infantry.Dying = true; Plugin.CombinedRefreshFixture(infantry.Agent);
        Plugin.OffensiveScoresFixture(vehicle.Agent);
        check(front.UtilityByAttacksScore > 0 && front.UtilityScore > 0,
            "vehicle steps beyond infantry against observed threat even after infantry anchor dies");
        check(front.SafetyScore == 20 && front.MinimumUtilityAPCost == 20,
            "real support shot competes with unchanged native danger and reserves firing AP");
        float before = front.UtilityScore; Plugin.OffensiveScoresFixture(vehicle.Agent);
        check(front.UtilityScore == before, "response utility is idempotent, not an accumulating aggression multiplier");
        var move = (Move)vehicle.Agent.Behaviors[1]; move.TargetTile = front;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(vehicle.Agent, move), move), "threat-directed vehicle movement is not cancelled by infantry spacing");

        var memory = new CombinedArmsThreatState(); memory.Advance(1, 1);
        memory.Observe(1, 1, 30, new(500, 25, 10, 30, 1));
        enemy.Detected = false; int reads = enemy.PositionReads;
        front.UtilityScore = front.UtilityByAttacksScore = 0;
        check(!CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, new(), map, 1, false) &&
            front.UtilityScore == 0, "last-known staging cannot reward a higher-danger tile or invent a shot");
        front.SafetyScore = 10;
        check(CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, new(), map, 1, false) &&
            front.UtilityScore > 0 && front.UtilityByAttacksScore == 0 && enemy.PositionReads == reads,
            "vehicle can approach legitimate last-known position without reading hidden actor or fabricating attack utility");
        front.UtilityScore = 0; front.APCost = 81;
        check(!CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, new(), map, 1, false), "response retains real movement-plus-fire AP reserve");
        front.APCost = 0; vehicle.Agent.Zones[0].Order = 2;
        check(!CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, new(), map, 1, false), "response does not reassign scripted defenders");
        vehicle.Agent.Zones[0].Order = 1;
        var exhausted = new OffensivePlanningBudget();
        for (int i = 0; i < 12; i++) exhausted.Spend(1, 1, 200, 201, faction.Pointer.ToInt64(), 4);
        check(!CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, exhausted, map, 1, false),
            "threat response cannot escape the shared per-actor planning budget");
        front.UtilityScore = 0; TileScore.UtilityWritesBeforeThrow = 0;
        bool rolledBack = false;
        try { CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, new(), map, 1, false); }
        catch (InvalidOperationException) { rolledBack = true; }
        check(rolledBack && front.UtilityScore == 0 && front.UtilityByAttacksScore == 0,
            "failed native utility write rolls back the entire threat-response edit batch");

        infantry.Dying = false; enemy.Detected = true; Plugin.BeginCombinedFixture(faction);
        var exposed = new TileScore { Tile = map.GetTile(14, 10), SafetyScore = 10 };
        infantry.Agent.Tiles[exposed.Tile] = exposed;
        move = (Move)infantry.Agent.Behaviors[1]; move.TargetTile = exposed;
        move.m_Path.Add(new(12, 0, 10)); move.m_Path.Add(new(14, 0, 10));
        check(Plugin.OffensiveChoiceFixture(infantry.Agent, move).GetID() == "Idle", "unsafe native infantry advance is vetoed, not just denied extra utility");
        check(Plugin.GetSupportSmokeThreat(infantry.Agent) is [25, 10], "blocked ordinary rifle infantry requests smoke toward remembered threat");
        var smoke = map.GetTile(14, 10); Plugin.BindSmokeFixture(t => t.Pointer == smoke.Pointer);
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(infantry.Agent, move), move), "native move through smoke-covered route remains available");
        Plugin.OffensiveScoresFixture(infantry.Agent);
        check(exposed.UtilityScore > 0, "existing smoke receives forward movement utility rather than an indefinite hold");
        move.m_Path.Insert(1, new(13, 0, 10));
        check(Plugin.OffensiveChoiceFixture(infantry.Agent, move).GetID() == "Idle", "smoky destination does not excuse an exposed transit waypoint");
        move.m_Path.RemoveAt(1);
        var secondThreat = new Actor { Pointer = (IntPtr)501, Tile = map.GetTile(24, 12), Detected = true };
        secondThreat.Attacks.Add(new()); faction.Opponents.Add(new() { Actor = secondThreat });
        Plugin.CombinedDetectedFixture(secondThreat, 9);
        enemy.Tile.Sees = _ => false; Plugin.BindSmokeFixture(null);
        check(Plugin.OffensiveChoiceFixture(infantry.Agent, move).GetID() == "Idle",
            "avoiding one remembered threat does not authorize crossing a second observed firing lane");
        enemy.Tile.Sees = _ => true;
        Plugin.BindSmokeFixture(null); exposed.SafetyScore = 5;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(infantry.Agent, move), move), "genuine native danger reduction preserved");
        var shot = new InflictDamage { Owner = infantry.Agent, Score = 10 }; shot.m_Candidates.Add(new());
        infantry.Agent.Behaviors.Add(shot);
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(infantry.Agent, move), shot) && Plugin.GetSupportSmokeThreat(infantry.Agent) == null,
            "real native attack wins and does not spend a smoke grenade instead");
        check(Plugin.OffensiveChoiceFixture(infantry.Agent, new Behavior { ID = "Flee" }).GetID() == "Flee", "emergency behavior is not intercepted");

        vehicle.Tags.Add(Il2CppMenace.Tags.TagType.INDIRECT_FIRE);
        check(Plugin.ThreatValueFixture(gun, enemy.Tile, 10) == 20, "positive native artillery damage valuation prioritizes observed firing threat");
        check(Plugin.ThreatValueFixture(gun, enemy.Tile, 0) == 0 && Plugin.ThreatValueFixture(gun, enemy.Tile, -5) == -5,
            "unusable or net-negative friendly-fire valuation is never manufactured into an artillery opportunity");
        check(Plugin.ThreatValueFixture(gun, enemy.Tile, 10, false) == 10, "priority does not recursively inflate positioning probes");
        enemy.Detected = false; reads = enemy.PositionReads;
        check(Plugin.ThreatValueFixture(gun, enemy.Tile, 10) == 10 && enemy.PositionReads == reads, "lost spotting cannot force an artillery attack");
        memory.Advance(1, 4);
        check(CombinedArmsResponseRuntime.Threat(vehicle.Agent, memory, map, 4) == null, "support request expires with legitimate threat memory");
    }
}
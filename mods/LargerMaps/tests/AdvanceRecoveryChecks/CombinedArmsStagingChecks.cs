using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;

internal static class CombinedArmsStagingChecks
{
    internal static void Run(Action<bool, string> check)
    {
        TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
        var map = TacticalManager.Instance.GetMap(); var faction = new AIFaction { Kind = 9 };
        TacticalManager.Instance.Faction = faction;
        var vehicle = new Actor { Pointer = (IntPtr)200, Tile = map.GetTile(10, 10), Vehicle = true, Infantry = false };
        vehicle.Template.m_ID = "enemy.vehicle_rogue_army_auto_laser_walker";
        vehicle.Agent.Pointer = (IntPtr)201; vehicle.Agent.Faction = faction;
        vehicle.Agent.Zones[0].Area = new(30, 10, 3, 3); faction.Actors.Add(vehicle);
        var enemy = new Actor { Pointer = (IntPtr)500, Tile = map.GetTile(25, 10), RejectHiddenReads = true };
        faction.Opponents.Add(new() { Actor = enemy });
        var gun = new InflictDamage { Owner = vehicle.Agent };
        var second = new InflictDamage { Owner = vehicle.Agent };
        vehicle.Agent.Behaviors.Add(gun); vehicle.Agent.Behaviors.Add(second);
        var memory = new CombinedArmsThreatState(); memory.Advance(1, 1);
        memory.Observe(1, 1, faction.Pointer.ToInt64(), new(500, 25, 10, 30, 1));
        var budget = new OffensivePlanningBudget();

        // Agent can recreate all native tile scores repeatedly before its first Execute.
        // Keeping already-edited score objects would hide the regression.
        for (int evaluation = 1; evaluation <= 12; evaluation++)
        {
            vehicle.Agent.Tiles.Clear();
            vehicle.Agent.Tiles[vehicle.Tile] = new() { Tile = vehicle.Tile, SafetyScore = 10 };
            var front = new TileScore { Tile = map.GetTile(15, 10), SafetyScore = 10, APCost = 20 };
            vehicle.Agent.Tiles[front.Tile] = front;
            bool seeded = CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles,
                memory, budget, map, 1, false);
            check(seeded && front.UtilityScore > 0 && front.UtilityByAttacksScore == 0 &&
                front.MinimumUtilityAPCost == 20 && front.SafetyScore == 10,
                $"fresh-score last-known vehicle staging survives evaluation {evaluation} before execution");
        }
        check(gun.Skill.Probes == 0 && second.Skill.Probes == 0 && gun.ValuesRead == 0 && enemy.PositionReads == 0,
            "last-known staging performs no target probes, invented shots or hidden position reads");
        check(budget.Spend(1, 1, 200, 201, faction.Pointer.ToInt64(), 24) &&
            budget.Spend(1, 1, 200, 201, faction.Pointer.ToInt64(), 24),
            "geometric staging does not consume the native target-probe budget");

        TileScore Fresh(float safety = 10, int cost = 20)
        {
            vehicle.Agent.Tiles.Clear();
            vehicle.Agent.Tiles[vehicle.Tile] = new() { Tile = vehicle.Tile, SafetyScore = 10 };
            var score = new TileScore { Tile = map.GetTile(15, 10), SafetyScore = safety, APCost = cost };
            vehicle.Agent.Tiles[score.Tile] = score;
            return score;
        }
        bool Plan() => CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles,
            memory, budget, map, 1, false);
        var next = Fresh(11);
        check(!Plan() && next.UtilityScore == 0, "freshly increased danger cancels staging, not hidden by retained utility");
        next = Fresh(10, 81);
        check(!Plan() && next.UtilityScore == 0, "fresh movement cost still reserves enough AP to fire");
        next = Fresh(); vehicle.AP = 39;
        check(!Plan() && next.UtilityScore == 0, "spent AP invalidates geometric support staging");
        vehicle.AP = 100; gun.Skill.Empty = second.Skill.Empty = true;
        check(!Plan(), "weapons out of uses invalidate staging without cached equipment assumptions");
        gun.Skill.Empty = second.Skill.Empty = false; vehicle.Agent.Zones[0].Order = 2;
        check(!Plan(), "changed defensive orders invalidate staging on the next evaluation");
        vehicle.Agent.Zones[0].Order = 1; vehicle.Morale = .5f;
        check(!Plan(), "low morale still excludes vehicle support staging");
        vehicle.Morale = 1; vehicle.Suppression = .5f;
        check(!Plan(), "suppression still excludes vehicle support staging");
        vehicle.Suppression = 0; vehicle.Moving = true;
        check(!Plan(), "actual movement prevents re-seeding during transit");
        vehicle.Moving = false; enemy.Detected = true;
        next = Fresh();
        check(Plan() && next.UtilityScore > 0 && next.UtilityByAttacksScore == 0 && gun.Skill.Probes == 0,
            "detected target with exhausted budget permits only fresh safe staging, never fabricated attack utility");
        next = Fresh(11);
        check(!Plan() && next.UtilityScore == 0 && gun.Skill.Probes == 0,
            "exhausted budget cannot justify a higher-danger firing origin or run a native target probe");
        budget.Reset();
        check(Plan() && next.UtilityByAttacksScore > 0 && next.SafetyScore == 11 && gun.Skill.Probes > 0,
            "available budget still evaluates real shots against unchanged native danger");

        budget.Reset();
        foreach (int round in new[] { 1, 2, 3 })
        {
            memory.Advance(1, round);
            int before = gun.Skill.Probes + second.Skill.Probes;
            for (int evaluation = 0; evaluation < 8; evaluation++)
            {
                next = Fresh();
                check(CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles,
                    memory, budget, map, round, false), "detected-target staging survives fresh scores after probe exhaustion");
            }
            check(gun.Skill.Probes + second.Skill.Probes - before == OffensivePlanningBudget.PerRound / 4,
                "real vehicle target checks retain the same per-actor probe cap and reset each round");
        }
        memory.Advance(1, 4); next = Fresh();
        check(!CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, budget, map, 4, false) &&
            next.UtilityScore == 0, "expired threat memory cancels staging without retaining a stale destination");

        memory.Observe(1, 4, faction.Pointer.ToInt64(), new(500, 25, 10, 30, 4));
        budget.Reset();
        bool filled = true;
        for (int i = 0; i < OffensivePlanningBudget.PerFactionRound / 4; i++)
            filled &= budget.Spend(1, 4, 700 + i / 12, 900 + i / 12, faction.Pointer.ToInt64(), 4);
        check(filled, "fixture fills the existing shared faction target-probe budget");
        int probesBefore = gun.Skill.Probes + second.Skill.Probes; next = Fresh();
        check(CombinedArmsResponseRuntime.PlanVehicle(vehicle.Agent, vehicle.Agent.Tiles, memory, budget, map, 4, false) &&
            next.UtilityByAttacksScore == 0 && gun.Skill.Probes + second.Skill.Probes == probesBefore,
            "faction probe cap stays enforced while freshly safe geometric staging remains available");
    }
}
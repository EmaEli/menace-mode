using AIPerformance;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

internal static class FactionConvergenceGuardChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int kind in new[] { 5, 6, 7, 8, 9 })
        foreach (string guard in new[] { "defend", "mixed", "deployment", "avoid", "no-move", "stunned", "contained", "allied" })
        {
            TacticalManager.Instance = new();
            var faction = new AIFaction { Kind = kind };
            var enemy = new Actor { Pointer = (IntPtr)999, Tile = new(60, 40), Detected = true, RejectHiddenReads = true };
            faction.Opponents.Add(new() { Actor = enemy });
            var unit = new Actor { Tile = new(10, 40) }; unit.Agent.Faction = faction; unit.Agent.Sleeping = true;
            faction.Actors.Add(unit);
            if (guard == "defend") unit.Agent.Zones = new() { new() { Order = 2 } };
            if (guard == "mixed") unit.Agent.Zones = new() { new() { Order = 1 }, new() { Order = 2 } };
            if (guard == "deployment") unit.Agent.Deployment = true;
            if (guard == "avoid") unit.Agent.Role.AvoidOpponents = true;
            if (guard == "no-move") unit.Agent.Role.Move = 0;
            if (guard == "stunned") unit.Stunned = true;
            if (guard == "contained") unit.Contained = true;
            if (guard == "allied") faction.Allied = true;
            Plugin.BeginAdvanceFixture(faction);
            check(unit.Agent.Sleeping && !ContactAdvanceRuntime.Ready(unit.Agent), $"faction {kind}/{guard}: faction alert preserves explicit native restrictions");
        }

        TacticalManager.Instance = new();
        var vehicle = new Actor { Vehicle = true, Infantry = false, Morale = 0 };
        vehicle.Agent.Faction.Kind = 9; vehicle.Tags.Add(TagType.MACHINE);
        vehicle.Template.m_ID = "enemy.vehicle_rogue_army_auto_laser_walker";
        check(!ContactAdvanceRuntime.Ready(vehicle.Agent), "recognized human-crewed MACHINE vehicle retains its morale guard");
        vehicle.Template.m_ID = "enemy.machine_combat_vehicle";
        check(ContactAdvanceRuntime.Ready(vehicle.Agent), "non-human MACHINE vehicle does not need a human-crewed template or morale");
        vehicle.AP = 0;
        check(!ContactAdvanceRuntime.Ready(vehicle.Agent), "machine convergence cannot ignore spent native AP");
    }
}
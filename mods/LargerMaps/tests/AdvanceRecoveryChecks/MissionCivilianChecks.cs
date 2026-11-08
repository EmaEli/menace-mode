using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.Objectives;

internal static class MissionCivilianChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (string mode in new[] { "protect", "rescue", "collateral", "other collateral", "escort", "duplicate", "unrelated", "generic", "complete", "hidden",
            "allied shooter", "allied civilian", "player", "dying", "contained", "soldier", "sleep", "deployment",
            "hypothetical", "splash", "no objectives", "too many", "zero", "negative", "NaN", "overflow", "exception" })
        {
            Plugin.BeginCivilianFixture();
            TacticalManager.Instance = new();
            var shooter = new Actor();
            var behavior = new InflictDamage { Owner = shooter.Agent };
            var civilian = new Actor { FactionKind = 3, Detected = true, RejectHiddenReads = true };
            var tile = new Tile(10, 10) { Entity = civilian };
            var objective = mode == "rescue" ? (Objective)new RescueUnit { Target = civilian } : new ProtectUnit { Target = civilian };
            var objectives = TacticalManager.Instance.Mission.Objectives.Objectives;
            objectives.Add(objective);
            switch (mode)
            {
                case "duplicate": objectives.Add(new RescueUnit { Target = civilian }); break;
                case "collateral": objectives[0] = new Collateral(); break;
                case "other collateral": objectives[0] = new Collateral { Matches = false }; break;
                case "escort": civilian.FactionKind = 4; break;
                case "unrelated": objective.Target = new Actor { Pointer = (IntPtr)99 }; break;
                case "generic": objectives[0] = new Objective { Target = civilian }; break;
                case "complete": objective.Ongoing = false; break;
                case "hidden": civilian.Detected = false; objective.Throw = true; break;
                case "allied shooter": shooter.Agent.Faction.Allied = true; break;
                case "allied civilian": shooter.Agent.Faction.AllyCivilian = true; break;
                case "player": shooter.Agent.Faction.Kind = 1; break;
                case "dying": civilian.Dying = true; break;
                case "contained": civilian.Contained = true; break;
                case "soldier": civilian.FactionKind = 1; break;
                case "sleep": shooter.Agent.Sleeping = true; break;
                case "deployment": shooter.Agent.Deployment = true; break;
                case "no objectives": objectives.Clear(); break;
                case "too many": for (int i = 0; i < 32; i++) objectives.Add(objective); break;
                case "exception": objective.Throw = true; break;
            }
            float native = mode == "zero" ? 0 : mode == "negative" ? -5 : mode == "NaN" ? float.NaN :
                mode == "overflow" ? float.MaxValue : 10;
            float result = Plugin.CivilianPriorityFixture(behavior, mode != "hypothetical", tile,
                mode == "splash" ? new Tile(12, 10) : tile, native);
            float expected = mode is "protect" or "rescue" or "collateral" or "escort" or "duplicate" ? 20 : native;
            check(result.Equals(expected), "native civilian target priority: " + mode);
            check(civilian.PositionReads == 0, "civilian priority never discovers coordinates: " + mode);
            check(Plugin.CivilianPriorityEnabled == (mode != "exception"), "civilian priority fails closed: " + mode);
        }
    }
}
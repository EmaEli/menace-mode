using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.Skills;
using SmokeOverhaul;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
Agent Fixture()
{
    TacticalManager.Instance = new(); TacticalManager.Busy = false;
    AIPerformance.Plugin.Threat = [20, 10]; Plugin.BeginFixture();
    var agent = new Agent(); agent.Actor.Skills.Skills.Add(new Skill()); return agent;
}
var agent = Fixture(); var skill = (Skill)agent.Actor.Skills.Skills[0];
Check(Plugin.TryAiSmokeAction(agent), "ordinary infantry with an equipped grenade responds from cover without a flamethrower");
Check(skill.Attempts == 1 && skill.UsedTarget.GetX() == 11 && skill.UsedTarget.GetZ() == 10,
    "native smoke use begins on the adjacent threat-facing tile, not behind the unit");
Check(!Plugin.TryAiSmokeAction(agent) && skill.Attempts == 1, "no second request/use in the same actor turn");
Check(Plugin.HasSupportSmokeCover(skill.UsedTarget), "real successful smoke effect is available to movement planning");
RuntimeSettings.MaximumAccuracyPenalty = 0;
Check(!Plugin.HasSupportSmokeCover(skill.UsedTarget), "disabled smoke protection cannot authorize an advance");
RuntimeSettings.MaximumAccuracyPenalty = 60;
foreach (string guard in new[] { "no-grenade", "ammo", "unusable", "ap", "illegal", "sleep", "deployment", "busy", "moving", "dying", "done", "no-request", "already-smoke" })
{
    agent = Fixture(); skill = (Skill)agent.Actor.Skills.Skills[0];
    switch (guard)
    {
        case "no-grenade": agent.Actor.Skills.Skills.Clear(); break;
        case "ammo": skill.Empty = true; break;
        case "unusable": skill.Usable = false; break;
        case "ap": skill.Affordable = false; break;
        case "illegal": skill.Legal = false; break;
        case "sleep": agent.Sleeping = true; break;
        case "deployment": agent.Deployment = true; break;
        case "busy": TacticalManager.Busy = true; break;
        case "moving": agent.Actor.Moving = true; break;
        case "dying": agent.Actor.Dying = true; break;
        case "done": agent.Actor.Done = true; break;
        case "no-request": AIPerformance.Plugin.Threat = null; break;
        case "already-smoke": agent.Actor.Tile.Smoke = true; break;
    }
    Check(!Plugin.TryAiSmokeAction(agent) && skill.Attempts == 0, guard + " preserves native smoke legality and no inventory grants");
}
agent = Fixture(); skill = (Skill)agent.Actor.Skills.Skills[0]; skill.UseResult = false;
Check(!Plugin.TryAiSmokeAction(agent) && skill.Attempts == 1 && !Plugin.HasSupportSmokeCover(skill.UsedTarget),
    "failed native Use does not report successful protection");
Check(!Plugin.TryAiSmokeAction(agent) && skill.Attempts == 1, "failed Use cannot create a retry loop this turn");
agent = Fixture();
Check(!Task.Run(() => Plugin.TryAiSmokeAction(agent)).GetAwaiter().GetResult(), "worker threads cannot execute grenade throws");
foreach (var offset in new[] { (10, 0, true), (11, 0, false), (6, 8, true), (7, 8, false), (0, 0, false) })
{
    agent = Fixture(); skill = (Skill)agent.Actor.Skills.Skills[0]; skill.Consumer = agent.Actor;
    AIPerformance.Plugin.Threat = [10 + offset.Item1, 10 + offset.Item2];
    Check(Plugin.AllowPreventiveHandSmoke(skill) == offset.Item3, "native hand-smoke decision cannot bypass range " + offset);
    Check(Plugin.TryAiSmokeAction(agent) == offset.Item3, "preventive throw uses inclusive circular ten-tile boundary " + offset);
}
foreach (string unavailable in new[] { "ready", "ammo", "ap", "unusable", "illegal", "done", "stunned", "contained", "sleep", "failed-use" })
{
    agent = Fixture(); skill = (Skill)agent.Actor.Skills.Skills[0];
    var mortar = new Agent { Faction = agent.Faction };
    mortar.Actor.Pointer = (IntPtr)456; mortar.Actor.Tile = new(4, 10); agent.Faction.Actors.Add(mortar.Actor);
    var shell = new Skill { ID = "active.launch_mortar_smoke", Consumer = mortar.Actor };
    mortar.Actor.Skills.Skills.Add(shell);
    switch (unavailable)
    {
        case "ammo": shell.Empty = true; break;
        case "ap": shell.Affordable = false; break;
        case "unusable": shell.Usable = false; break;
        case "illegal": shell.Legal = false; break;
        case "done": mortar.Actor.Done = true; break;
        case "stunned": mortar.Actor.Stunned = true; break;
        case "contained": mortar.Actor.Contained = true; break;
        case "sleep": mortar.Sleeping = true; break;
        case "failed-use": shell.UseResult = false; break;
    }
    bool available = unavailable is "ready" or "failed-use";
    Check(Plugin.TryAiSmokeAction(agent) != available && shell.Attempts == 0,
        "infantry defers only to a usable mortar and never executes another actor's skill: " + unavailable);
    if (!available) continue;
    Check(Plugin.TryAiSmokeAction(mortar) == shell.UseResult && shell.Attempts == 1 &&
        shell.UsedTarget.GetX() == 11, "mortar screens the infantry during its own activation: " + unavailable);
    Check(!Plugin.TryAiSmokeAction(mortar) && shell.Attempts == 1, "mortar cannot retry this turn");
    if (shell.UseResult)
        Check(!Plugin.TryAiSmokeAction(agent) && skill.Attempts == 0, "existing mortar screen does not trigger a duplicate hand grenade");
    if (!shell.UseResult)
        Check(Plugin.TryAiSmokeAction(agent) && skill.Attempts == 1, "failed mortar execution releases infantry fallback");
}
agent = Fixture(); skill = (Skill)agent.Actor.Skills.Skills[0]; skill.Consumer = agent.Actor;
AIPerformance.Plugin.Threat = null;
agent.Faction.Opponents.Add(new Opponent { Actor = new Actor { Pointer = (IntPtr)789, Tile = new(20, 10), Detected = true } });
Check(Plugin.AllowPreventiveHandSmoke(skill), "native smoke accepts a detected known threat at ten tiles without a support request");
agent.Faction.Opponents[0].Actor.Detected = false;
Check(!Plugin.AllowPreventiveHandSmoke(skill), "native smoke never reads a hidden actor's current position");
agent.Actor.Pressure = SuppressionState.Suppressed;
Check(Plugin.AllowPreventiveHandSmoke(skill), "actual suppression retains long-range emergency smoke");
agent.Actor.Pressure = SuppressionState.None; agent.Faction.Allied = true;
Check(Plugin.AllowPreventiveHandSmoke(skill), "player-allied smoke remains unaffected");
agent = Fixture(); skill = new Skill { ID = "active.launch_mortar_smoke", Consumer = agent.Actor };
Check(Plugin.AllowPreventiveSmoke(skill, new Tile(11, 10)), "native mortar smoke may screen infantry threatened at ten tiles");
AIPerformance.Plugin.Threat = [21, 10];
Check(!Plugin.AllowPreventiveSmoke(skill, new Tile(11, 10)), "native mortar decision cannot bypass ten-tile threat limit");
AIPerformance.Plugin.Threat = [20, 10];
Check(!Plugin.AllowPreventiveSmoke(skill, new Tile(30, 10)), "native mortar smoke must land near the squad needing protection");
Console.WriteLine($"Smoke support: {checks} linked-runtime checks passed (managed stand-ins, not gameplay).");
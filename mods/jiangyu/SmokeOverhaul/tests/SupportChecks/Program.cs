using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.Skills;
using SmokeOverhaul;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
Agent Fixture()
{
    TacticalManager.Instance = new(); TacticalManager.Busy = false;
    AIPerformance.Plugin.Threat = [25, 10]; Plugin.BeginFixture();
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
Console.WriteLine($"Smoke support: {checks} linked-runtime checks passed (managed stand-ins, not gameplay).");
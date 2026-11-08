using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;

namespace MenaceWeaponMechanicsRebalanced;

public sealed partial class Plugin
{
    private static bool _enabled = true;
    private static readonly Dictionary<string, object> MortarSkillSpecs = new() { ["active.enemy_only_launch_medium_mortar_frag"] = new() };
    private static long NativeKey(Native? value) => value?.Key ?? 0;
    private static bool IsHostileMortarActor(Actor actor) => actor.Hostile;
    private static void Warn(string text) => throw new Exception(text);
    private static int checks;
    private static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new Exception(reason);
    }
    private static (Agent Agent, Attack Attack, Tile Target) Fixture()
    {
        EnemyMortarMissions.Clear();
        TacticalManager.Current = new();
        _enabled = _enemyMortarFirePriority = true;
        _enemyMortarFireRounds = 3;
        InstallEnemyMortarAi(new HarmonyLib.Harmony());
        var map = TacticalManager.Get().Map;
        var agent = new Agent();
        agent.Actor.Tile = map.Add(0, 0);
        var target = map.Add(20, 20);
        var attack = new Attack { Agent = agent, Id = "InflictSuppression", m_TargetTile = target };
        attack.m_Candidates.Add(new() { Target = target, Score = .021f });
        agent.Behaviors.Add(attack);
        return (agent, attack, target);
    }
    private static int Evaluate(Attack attack, int native = 0)
    {
        EnemyMortarEvaluate(attack, attack.Agent.Actor, ref native);
        return attack.Score = native; // The native Behavior.Evaluate wrapper stores the returned score.
    }
    private static void LoseTarget(Attack attack)
    {
        attack.m_Candidates.Clear(); attack.m_TargetTile = null; attack.Score = 0;
    }
    public static void Main()
    {
        for (int duration = 2; duration <= 4; duration++)
        {
            var (agent, attack, target) = Fixture();
            _enemyMortarFireRounds = duration;
            Check(Evaluate(attack) == 250, "Low-scoring native mortar target beats Idle");
            for (int round = 10; round < 10 + duration; round++)
            {
                TacticalManager.Get().m_Round = round;
                LoseTarget(attack);
                Check(Evaluate(attack) >= 250 && attack.m_TargetTile == target, "Blind mission persists at saved tile");
                Check(Evaluate(attack) >= 250, "Repeated evaluations do not consume rounds");
                Check(agent.Actor.AP == 100 && attack.Skill.Uses == 0, "Evaluation does not spend AP/ammo");
            }
            TacticalManager.Get().m_Round = 10 + duration;
            LoseTarget(attack);
            Check(Evaluate(attack) == 0 && attack.m_TargetTile == null, "Blind fire expires, never self-renews");
        }
        {
            var (agent, attack, target) = Fixture();
            Evaluate(attack, 100);
            Behavior choice = new() { Id = "Move", Score = 10000 };
            EnemyMortarChoose(agent, ref choice);
            Check(choice == attack, "Ready mortar wins over Move even with a very high movement score");
            choice = new() { Id = "Confusion", Score = 1000 };
            EnemyMortarChoose(agent, ref choice);
            Check(choice.Id == "Confusion", "Does not override a selected emergency behavior");
            var other = TacticalManager.Get().Map.Add(30, 30);
            attack.m_TargetTile = other;
            attack.m_Candidates = new() { new() { Target = other, Score = 5 } };
            Check(Evaluate(attack, 5) > 0 && attack.m_TargetTile == target, "A new visible candidate cannot drag the mission");
            LoseTarget(attack); attack.Skill.Out = true;
            Check(Evaluate(attack) == 0, "Out of ammunition"); attack.Skill.Out = false;
            attack.Skill.Affordable = false;
            Check(Evaluate(attack) == 0, "Insufficient AP"); attack.Skill.Affordable = true;
            attack.Skill.Usable = false;
            Check(Evaluate(attack) == 0, "Pinned/unusable skill"); attack.Skill.Usable = true;
            attack.Skill.Legal = false;
            Check(Evaluate(attack) == 0, "Illegal/out-of-range remembered tile"); attack.Skill.Legal = true;
            attack.m_RotateBeforeExecuting = true;
            Check(Evaluate(attack) == 0, "Never bypass rotation prerequisites"); attack.m_RotateBeforeExecuting = false;
            var friendly = TacticalManager.Get().Map.Add(21, 20);
            friendly.Occupant = new Actor();
            Check(Evaluate(attack) == 0, "Do not blindly shell friendlies"); friendly.Occupant = null;
            friendly.Structure = true;
            Check(Evaluate(attack) > 0, "Structures are not cast to actors");
            _enemyMortarFirePriority = false;
            LoseTarget(attack);
            Check(Evaluate(attack) == 0, "Optional setting off preserves native evaluation");
        }
        foreach (string guard in new[] { "player", "allied", "smoke", "other", "moving", "dying", "done", "spent", "sleeping", "deployment", "contained", "disabled" })
        {
            var (agent, attack, _) = Fixture();
            switch (guard)
            {
                case "player": agent.Actor.Hostile = false; break;
                case "allied": agent.Faction.Allied = true; break;
                case "smoke": attack.Skill.Id = "active.launch_mortar_smoke"; break;
                case "other": attack.Skill.Id = "active.fire_battle_rifle_tier_1"; break;
                case "moving": agent.Actor.Moving = true; break;
                case "dying": agent.Actor.Dying = true; break;
                case "done": agent.Actor.Done = true; break;
                case "spent": agent.Actor.Spent = true; break;
                case "sleeping": agent.Sleeping = true; break;
                case "deployment": agent.Deployment = true; break;
                case "contained": agent.Actor.Contained = true; break;
                case "disabled": _enabled = false; break;
            }
            Check(Evaluate(attack, 5) == 5 && attack.Skill.Checks == 0, guard + " leaves native AI untouched");
        }
        {
            var (agent, attack, _) = Fixture();
            attack.m_DeployBeforeExecuting = true; attack.Skill.Usable = false;
            Check(Evaluate(attack, 20) == 250, "Native deployment plans retain priority");
            var state = new MortarFireMissionState();
            var key = (1L, 2L, 3L);
            state.Observe(1, 10); state.Acquire(key, 7, 8, 3);
            Check(!state.TryGet((1, 2, 4), out _), "Independent physical skill");
            Check(!state.TryGet((1, 4, 3), out _), "Independent agent lifecycle");
            Check(!state.TryGet((4, 2, 3), out _), "Independent actor");
            state.Observe(1, 11); state.Acquire(key, 99, 99, 4);
            Check(state.TryGet(key, out var tile) && tile == (7, 8), "No renewal or retarget on reevaluation");
            state.SetDuration(2); state.Observe(1, 12);
            Check(!state.TryGet(key, out _), "Live duration change retains original acquisition round");
            state.Acquire(key, 7, 8, 3); state.Observe(2, 12);
            Check(!state.TryGet(key, out _), "New map clears memory");
            state.Acquire(key, 7, 8, 3); state.Observe(2, 1);
            Check(!state.TryGet(key, out _), "Round rollback clears memory");
            state.Acquire(key, 7, 8, 3); state.Clear();
            Check(!state.TryGet(key, out _), "Scene/disable clear");
            Check(MortarFireMissionState.Priority(int.MaxValue) == int.MaxValue, "Score multiplication cannot overflow");
            var score = Task.Run(() => Evaluate(attack, 17)).GetAwaiter().GetResult();
            Check(score == 17, "Worker thread does not touch native state");
        }
        Console.WriteLine($"PASS: {checks} enemy mortar AI managed checks; no native execution.");
    }
}
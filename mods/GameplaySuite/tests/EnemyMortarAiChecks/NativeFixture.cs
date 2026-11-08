using System.Reflection;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.Skills;

// Managed stand-ins only: these tests do not load Harmony or native IL2CPP.
namespace HarmonyLib
{
    public class Harmony
    {
        public readonly List<string> Targets = new();
        public void Patch(MethodInfo method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null) => Targets.Add(method.Name);
    }
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, parameters);
    }
}
namespace Il2CppMenace.Tactical
{
    public class Native
    {
        static long next;
        public long Key = ++next;
        public T? TryCast<T>() where T : class => this as T;
    }
    public class Tile : Native
    {
        public int X, Z;
        public Actor? Occupant;
        public bool Structure;
        public int GetX() => X;
        public int GetZ() => Z;
        public bool HasActor() => Occupant != null;
        public Actor? GetActor() => Structure ? throw new InvalidCastException() : Occupant;
    }
    public class Map : Native
    {
        public readonly Dictionary<(int, int), Tile> Tiles = new();
        public Tile? GetTile(int x, int z) => Tiles.GetValueOrDefault((x, z));
        public Tile Add(int x, int z) => Tiles[(x, z)] = new Tile { X = x, Z = z };
    }
    public class TacticalManager
    {
        public static TacticalManager Current = new();
        public static bool Busy;
        public static bool IsSkillBusy() => Busy;
        public int m_Round = 10;
        public Map Map = new();
        public static TacticalManager Get() => Current;
        public Map GetMap() => Map;
    }
    public class Actor : Native
    {
        public bool Hostile = true, Moving, Dying, Done, Spent, Contained;
        public int Faction = 9, AP = 100;
        public Tile Tile = new();
        public Tile GetTile() => Tile;
        public bool IsDying() => Dying;
        public bool IsMoving() => Moving;
        public bool IsTurnDone() => Done;
        public bool IsActionPointsSpent() => Spent;
        public bool IsContainedWithinAnotherEntity() => Contained;
        public int GetFaction() => Faction;
        public bool IsAlliedWith(int faction) => faction == Faction;
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    public class SkillTemplate { public int Scatter = 4; }
    public class Skill : Native
    {
        public string Id = "active.enemy_only_launch_medium_mortar_frag";
        public bool Out, Usable = true, Affordable = true, Legal = true;
        public int Checks, Uses;
        public Func<Tile, bool>? CanTarget;
        public SkillTemplate Template = new();
        public string GetID() => Id;
        public bool IsOutOfUses() => Out;
        public bool IsUsable() => Usable;
        public bool IsAffordable() => Affordable;
        public bool IsUsableOn(Tile tile, Tile origin) { Checks++; return Legal && (CanTarget?.Invoke(tile) ?? true); }
        public SkillTemplate GetTemplate() => Template;
    }
}
namespace Il2CppMenace.Tactical.AI
{
    public class AIFaction { public bool Allied; public bool IsAlliedWithPlayer() => Allied; }
    public class Agent : Native
    {
        public enum State { None, EvaluatingTiles, EvaluatingBehaviors, ReadyToExecute, Executing }
        public State CurrentState = State.ReadyToExecute;
        public Behavior? m_ActiveBehavior;
        public bool IsTurnDone, FlaggedForDeactivation;
        public Actor Actor = new();
        public AIFaction Faction = new();
        public bool Sleeping, Deployment;
        public readonly List<Behavior> Behaviors = new();
        public Actor GetActor() => Actor;
        public AIFaction GetFaction() => Faction;
        public bool IsSleeping() => Sleeping;
        public bool IsDeploymentPhase() => Deployment;
        public List<Behavior> GetBehaviors() => Behaviors;
        public State GetState() => CurrentState;
        public bool Execute() => true;
        private Behavior? PickBehavior() => null;
    }
    public class Behavior : Native
    {
        public string Id = "Idle";
        public int m_Score;
        public int Score { get => m_Score; set => m_Score = value; }
        public Agent Agent = null!;
        public int GetScore() => Score;
        public string GetID() => Id;
        public Agent GetAgent() => Agent;
    }
}
namespace Il2CppMenace.Tactical.AI.Behaviors
{
    public class Attack : Behavior
    {
        public struct Data { public Tile? Target; public float Score; }
        public Skill Skill = new();
        public Tile? m_TargetTile;
        public List<Data> m_Candidates = new();
        public bool m_DeployBeforeExecuting, m_SetupBeforeExecuting, m_RotateBeforeExecuting,
            m_DontActuallyExecute, m_IsExecuted;
        public int m_AdditionalRadius = 1;
        public Skill GetSkill() => Skill;
        private int OnEvaluate(Actor actor) => 0;
    }
}
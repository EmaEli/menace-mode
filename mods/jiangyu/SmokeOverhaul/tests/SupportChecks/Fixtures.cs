namespace Il2CppInterop.Runtime.InteropTypes { }
namespace Il2CppMenace.Tools
{
    public static class DataTemplateLoader { public static T Get<T>(string id, bool unused) where T : new() => new(); }
}
namespace Il2CppMenace.Tactical
{
    public enum SuppressionState { None, Suppressed, PinnedDown }
    public enum UsageParameter { AutoAudioVolume }
    public class Tile(int x, int z)
    {
        public int GetX() => x; public int GetZ() => z;
        public bool Smoke, Empty = true, Walkable = true;
        public int GetCoverMask() => 1; // The request must work even from cover; no flamethrower.
        public bool IsEmpty() => Empty;
        public bool IsValidMovementDestination() => Walkable;
    }
    public class Actor
    {
        public IntPtr Pointer = (IntPtr)123;
        public Tile Tile = new(10, 10);
        public bool Dying, Moving, Done, Contained, Stunned, Detected;
        public AI.Agent Agent;
        public AI.Agent GetAgent() => Agent;
        public bool IsContainedWithinAnotherEntity() => Contained;
        public bool IsStunned() => Stunned;
        public SuppressionState Pressure;
        public Skills.SkillContainer Skills = new();
        public bool IsInfantry() => true; public bool IsDying() => Dying; public bool IsMoving() => Moving;
        public bool IsTurnDone() => Done; public bool IsAlive() => !Dying;
        public bool IsDetectedByFaction(int index) => Detected;
        public Tile GetTile() => Tile; public Skills.SkillContainer GetSkills() => Skills;
        public SuppressionState GetSuppressionState() => Pressure;
        public Actor GetLastAttacker() => null; public Actor ToActor() => this;
    }
    public class Map
    {
        public Dictionary<(int, int), Tile> Tiles = new();
        public bool IsInBounds(int x, int z) => x >= 0 && z >= 0 && x < 96 && z < 96;
        public Tile GetTile(int x, int z)
        { if (!Tiles.TryGetValue((x, z), out var tile)) Tiles[(x, z)] = tile = new(x, z); return tile; }
    }
    public class TacticalManager
    {
        public static TacticalManager Instance = new(); public Map Map = new();
        public static TacticalManager Get() => Instance; public Map GetMap() => Map;
        public static bool Busy; public static bool IsSkillBusy() => Busy;
    }
}
namespace Il2CppMenace.Tactical.AI
{
    public class Opponent { public Actor Actor; public bool Known = true; public bool IsKnown() => Known; }
    public class AIFaction
    {
        public List<Actor> Actors = new(); public List<Opponent> Opponents = new(); public bool Allied;
        public List<Actor> GetActors() => Actors;
        public bool IsAlliedWithPlayer() => Allied;
        public bool HasKnownOpponent() => false; public int GetIndex() => 9;
        public List<Opponent> GetOpponents() => Opponents;
    }
    public class Agent
    {
        public Actor Actor = new(); public bool Sleeping, Deployment;
        public AIFaction Faction = new();
        public Agent() { Actor.Agent = this; Faction.Actors.Add(Actor); }
        public bool IsSleeping() => Sleeping; public bool IsDeploymentPhase() => Deployment;
        public Actor GetActor() => Actor; public int GetNumThreatsFaced() => 0;
        public AIFaction GetFaction() => Faction;
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    public class SkillTemplate { public IntPtr Pointer = (IntPtr)1; public bool IsUsableWhilePinnedDown; }
    public class BaseSkill
    {
        public string ID = "active.throw_smoke_grenade_limited";
        public string GetID() => ID; public T TryCast<T>() where T : class => this as T;
    }
    public class Skill : BaseSkill
    {
        public bool Empty, Usable = true, Affordable = true, Legal = true, UseResult = true;
        public int Attempts; public Tile UsedTarget;
        public Actor Consumer;
        public Actor GetAPConsumer() => Consumer; public bool IsOutOfUses() => Empty;
        public bool IsUsable() => Usable; public bool IsAffordable() => Affordable;
        public bool IsUsableOn(Tile target, Tile origin) => Legal;
        public bool Use(Tile target, UsageParameter usage)
        { Attempts++; UsedTarget = target; if (UseResult) { Empty = true; target.Smoke = true; } return UseResult; }
    }
    public class SkillContainer { public List<BaseSkill> Skills = new(); public List<BaseSkill> GetAllSkills() => Skills; }
}
namespace AIPerformance
{
    public static class Plugin
    {
        public static int[] Threat = [20, 10];
        public static int[] GetSupportSmokeThreat(Il2CppMenace.Tactical.AI.Agent agent) => Threat;
        public static int[] GetSupportMortarSmokeThreat(Il2CppMenace.Tactical.AI.Agent agent) => Threat;
    }
}
namespace SmokeOverhaul
{
    public sealed partial class Plugin
    {
        public static void BeginFixture()
        { _aiSmokeMainThreadId = Environment.CurrentManagedThreadId; AiSmokeSceneChanged("Tactical"); }
        private static void DebugMsg(string text) { }
        private static void Error(string text) => throw new Exception(text);
        private static bool TileHasSmoke(Il2CppMenace.Tactical.Tile tile) => tile.Smoke;
    }
    public static class RuntimeSettings
    {
        public static bool Enabled = true, EnableAISmokeUsage = true, SimpleAISmokeRules = true, AllowSmokeWhilePinnedDown = true, DebugLogging;
        public static int AccuracyPenaltyPerTile = 20, MaximumAccuracyPenalty = 60;
    }
}
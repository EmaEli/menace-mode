// In-memory stand-ins only. Production recovery sources are linked unchanged.
namespace Il2CppSystem.Collections.Generic
{
    public class Dictionary<K, V> : System.Collections.Generic.Dictionary<K, V> { }
}
namespace UnityEngine
{
    public record struct RectInt(int xMin, int yMin, int width, int height);
}
namespace Il2CppMenace.Tactical
{
    public class Tile(int x, int z)
    {
        private static int _next;
        public IntPtr Pointer = (IntPtr)Interlocked.Increment(ref _next);
        public int GetX() => x;
        public int GetZ() => z;
    }
    public class Actor
    {
        public Actor() { Agent.Actor = this; foreach (var behavior in Agent.Behaviors) behavior.Owner = Agent; }
        public IntPtr Pointer = (IntPtr)10;
        public AI.Agent Agent = new();
        public Tile Tile = new(0, 0);
        public int AP = 100, TilesMovedThisTurn;
        public bool Moving, Dying, Contained;
        public float Suppression, Morale = 1;
        public AI.Agent GetAgent() => Agent;
        public Tile GetTile() => Tile;
        public bool IsMoving() => Moving;
        public bool IsDying() => Dying;
        public bool IsContainedWithinAnotherEntity() => Contained;
        public bool IsActionPointsSpent() => AP <= 0;
        public int GetActionPoints() => AP;
        public float GetSuppressionPct() => Suppression;
        public float GetMoralePct() => Morale;
    }
    public sealed class Map
    {
        public IntPtr Pointer = (IntPtr)1;
        public int Width = 96, Height = 96;
    }
    public sealed class TacticalManager
    {
        public static TacticalManager Instance = new();
        public static TacticalManager Get() => Instance;
        public Map Map = new();
        public int m_Round = 1;
        public Map GetMap() => Map;
        public void OnInit(object mission) { }
    }
}
namespace Il2CppMenace.Tactical.AI
{
    public class Agent
    {
        public Tactical.Actor Actor;
        public Tactical.Actor GetActor() => Actor;
        private Behavior PickBehavior() => Behaviors[0];
        public IntPtr Pointer = (IntPtr)20;
        public AIFaction Faction = new();
        public Data.RoleData Role = new();
        public List<Zone> Zones = [new()];
        public List<Behavior> Behaviors = [new(), new Behaviors.Move(), new Behaviors.Attack()];
        public bool Deployment, Sleeping;
        public AIFaction GetFaction() => Faction;
        public Data.RoleData GetRole() => Role;
        public List<Zone> GetZones() => Zones;
        public List<Behavior> GetBehaviors() => Behaviors;
        public bool IsDeploymentPhase() => Deployment;
        public bool IsSleeping() => Sleeping;
    }
    public class Behavior
    {
        private static int _next;
        public IntPtr Pointer = (IntPtr)Interlocked.Increment(ref _next);
        public Agent Owner;
        public Agent GetAgent() => Owner;
        public float Threshold = 20;
        public float GetUtilityThreshold() => Threshold;
        public string ID = "Idle";
        public int Score;
        public string GetID() => ID;
        public int GetScore() => Score;
        public T TryCast<T>() where T : class => this as T;
    }
    public class AIFaction
    {
        public int Kind = 7;
        public bool Allied, Known = true;
        public int GetFactionType() => Kind;
        public bool IsAlliedWithPlayer() => Allied;
        public bool HasKnownOpponent() => Known;
    }
    public class Zone
    {
        public UnityEngine.RectInt Area = new(10, 0, 5, 5);
        public int Order = 1;
        public float Priority = 1;
    }
    public static class AIConfig { public static Weights WEIGHTS = new(); }
    public class Weights
    {
        public float DistanceToZones = 2, DistanceToAdvanceZones = 3, UtilityThreshold = 10;
    }
}
namespace Il2CppMenace.Tactical.AI.Behaviors
{
    public class Move : Behavior
    {
        public Data.TileScore TargetTile;
        public bool Done, Moved, Delaying, Inert;
        public bool IsMovementDone() => Done;
        public bool HasMovedThisTurn() => Moved;
        public bool IsDelayingMovement() => Delaying;
        public bool IsInsideContainerAndInert() => Inert;
        public Move() { ID = "Move"; }
        public Data.TileScore GetTargetTile() => TargetTile;
        public bool HasUtility(Il2CppSystem.Collections.Generic.Dictionary<Tactical.Tile, Data.TileScore> tiles)
            => tiles.Values.Any(t => t.UtilityScore >= GetUtilityThreshold());
    }
    public class Attack : Behavior
    {
        public List<object> m_Candidates = new();
        public Attack() { ID = "InflictDamage"; }
    }
}
namespace Il2CppMenace.Tactical.AI.Behaviors.Criterions
{
    public sealed class ConsiderZones
    {
        public void PostProcess(Tactical.Actor actor, Il2CppSystem.Collections.Generic.Dictionary<Tactical.Tile, Data.TileScore> tiles) { }
    }
}
namespace Il2CppMenace.Tactical.AI.Data
{
    public class RoleData
    {
        public bool AvoidOpponents;
        public float ZoneImportanceWeight = 1, UtilityScale = 2, UtilityThresholdScale = 1;
    }
    public class TileScore
    {
        public Tactical.Tile Tile;
        public static int WritesBeforeThrow = -1;
        private float _distanceScore;
        public float DistanceScore
        {
            get => _distanceScore;
            set
            {
                if (WritesBeforeThrow == 0) { WritesBeforeThrow = -1; throw new InvalidOperationException("native write failed"); }
                if (WritesBeforeThrow > 0) WritesBeforeThrow--;
                _distanceScore = value;
            }
        }
        private float _utilityScore;
        public static int UtilityWritesBeforeThrow = -1;
        public float UtilityScore
        {
            get => _utilityScore;
            set
            {
                if (UtilityWritesBeforeThrow == 0) { UtilityWritesBeforeThrow = -1; throw new InvalidOperationException("utility write failed"); }
                if (UtilityWritesBeforeThrow > 0) UtilityWritesBeforeThrow--;
                _utilityScore = value;
            }
        }
        public float UtilityByAttacksScore, UtilityByAttacksScoreCandidate;
        public float SafetyScore = 123, UtilityScoreScaled = 456;
        public object Path = new();
        public int APCost;
    }
}
namespace Il2CppMenace.Tools
{
    public static class RectIntExtensions
    {
        // Deliberately injected native geometry result/failure for transaction tests.
        public static bool ThrowOnInside;
        public static int GetDistanceTo(UnityEngine.RectInt r, Tactical.Tile t)
            => (int)Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(r.xMin - t.GetX(), t.GetX() - (r.xMin + r.width))), 2) +
                Math.Pow(Math.Max(0, Math.Max(r.yMin - t.GetZ(), t.GetZ() - (r.yMin + r.height))), 2));
        public static bool IsInside(UnityEngine.RectInt r, Tactical.Tile t)
            => ThrowOnInside ? throw new InvalidOperationException("native geometry unavailable") :
                t.GetX() >= r.xMin && t.GetX() < r.xMin + r.width && t.GetZ() >= r.yMin && t.GetZ() < r.yMin + r.height;
    }
}
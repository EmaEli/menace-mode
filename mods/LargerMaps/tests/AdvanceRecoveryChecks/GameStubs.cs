// In-memory stand-ins only. Production recovery sources are linked unchanged.
namespace Il2CppSystem.Collections.Generic
{
    public class Dictionary<K, V> : System.Collections.Generic.Dictionary<K, V> { }
}
namespace UnityEngine
{
    public record struct RectInt(int xMin, int yMin, int width, int height);
    public record struct Vector3(float x, float y, float z);
}
namespace Il2CppMenace.Tactical
{
    public class Tile(int x, int z)
    {
        private static int _next;
        public IntPtr Pointer = (IntPtr)Interlocked.Increment(ref _next);
        public int GetX() => x;
        public int GetZ() => z;
        public Entity Entity;
        public Entity GetEntity() => Entity;
        public Func<Tile, bool> Sees = _ => true;
        public int LaneProbes;
        public bool HasLineOfSightTo(Tile other, Il2CppTactical.LineOfSightFlags flags) { LaneProbes++; return Sees(other); }
    }
    public class Entity
    {
        public IntPtr Pointer = (IntPtr)10;
        public T TryCast<T>() where T : class => this as T;
    }
    public class Actor : Entity
    {
        public Actor() { Agent.Actor = this; foreach (var behavior in Agent.Behaviors) behavior.Owner = Agent; }
        public AI.Agent Agent = new();
        public Tile Tile = new(0, 0);
        public int AP = 100, TilesMovedThisTurn, ID = 10;
        public bool Moving, Dying, Contained, Stunned, Deployed, WeaponSetup;
        public EntityTemplate Template = new();
        public EntityTemplate GetTemplate() => Template;
        public bool IsStunned() => Stunned;
        public float Suppression, Morale = 1;
        public bool Detected, RejectHiddenReads, Infantry = true, Vehicle;
        public List<Skills.Skill> Attacks = new();
        public List<Skills.Skill> GetAllAttacks()
        {
            if (RejectHiddenReads && !Detected) throw new Exception("hidden weapon read");
            return Attacks;
        }
        public int FactionKind = 1, PositionReads;
        public HashSet<Il2CppMenace.Tags.TagType> Tags = new();
        public AI.Agent GetAgent() => Agent;
        public Tile GetTile()
        {
            if (RejectHiddenReads && !Detected) throw new Exception("hidden position read");
            PositionReads++; return Tile;
        }
        public bool IsDetectedByFaction(int faction) => Detected;
        public void SetDetectedByFaction(int faction) { Detected = true; }
        public int GetFaction() => FactionKind;
        public bool IsInfantry() => Infantry;
        public bool IsVehicle() => Vehicle;
        public bool HasTag(Il2CppMenace.Tags.TagType tag) => Tags.Contains(tag);
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
        public Tile GetTileAtPos(UnityEngine.Vector3 point) => GetTile((int)point.x, (int)point.z);
        private Dictionary<(int, int), Tile> _tiles = new();
        public Tile GetTile(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Height) throw new Exception("out-of-bounds map read");
            if (!_tiles.TryGetValue((x,z), out var tile)) _tiles[(x,z)] = tile = new(x,z);
            return tile;
        }
    }
    public class EntityTemplate
    {
        public string m_ID = "enemy.rogue_army_infantry";
        public string GetID() => m_ID;
    }
    public sealed class TacticalManager
    {
        public static TacticalManager Instance = new();
        public static TacticalManager Get() => Instance;
        public Map Map = new();
        public int m_Round = 1;
        public Map GetMap() => Map;
        public AI.AIFaction Faction;
        public AI.AIFaction GetFaction(int kind) => Faction;
        public Il2CppMenace.Strategy.Mission Mission = new();
        public Il2CppMenace.Strategy.Mission GetMission() => Mission;
        public void OnInit(object mission) { }
    }
}
namespace Il2CppMenace.Tactical.AI
{
    public class Agent
    {
        public Tactical.Actor Actor;
        public Tactical.Actor GetActor() => Actor;
        public Il2CppSystem.Collections.Generic.Dictionary<Tactical.Tile, Data.TileScore> Tiles = new();
        public Il2CppSystem.Collections.Generic.Dictionary<Tactical.Tile, Data.TileScore> GetTiles() => Tiles;
        private void PostProcessTileScores() { }
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
        public void SetSleeping(bool sleeping) => Sleeping = sleeping;
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
        public IntPtr Pointer = (IntPtr)30;
        public int Kind = 7;
        public bool Allied, AllyCivilian, Known = true;
        public int GetFactionType() => Kind;
        public bool IsAlliedWithPlayer() => Allied;
        public bool HasKnownOpponent() => Known;
        public bool IsAlliedWith(int faction) => Allied || ((faction == 3 || faction == 4) && AllyCivilian);
        public T TryCast<T>() where T : class => this as T;
        public List<Opponent> Opponents = new();
        public List<Opponent> GetOpponents() => Opponents;
        public List<Tactical.Actor> Actors = new();
        public List<Tactical.Actor> GetActors() => Actors;
        public void OnOpponentSighted(Tactical.Actor actor, int ttl) { }
        public void OnTurnStart() { }
    }
    public class SkillBehavior : Behavior
    {
        public Il2CppMenace.Tactical.Skills.Skill Skill;
        public Il2CppMenace.Tactical.Skills.Skill GetSkill() => Skill;
        public Il2CppMenace.Tactical.Skills.Skill m_DeployedStanceSkill, m_SetupWeaponSkill;
    }
    public sealed class Opponent { public Tactical.Actor Actor; }
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
        public float InflictDamageFromTile = 5, IncludeAttacksAgainstAllOpponentsMult = 1;
    }
}
namespace Il2CppMenace.Tactical.AI.Behaviors
{
    public class Move : Behavior
    {
        public Data.TileScore TargetTile;
        public List<UnityEngine.Vector3> m_Path = new();
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
    public class Attack : SkillBehavior
    {
        public List<object> m_Candidates = new();
        public Attack() { ID = "InflictDamage"; }
    }
    public class InflictDamage : Attack
    {
        public InflictDamage() { Skill = new(); }
        public Func<Tactical.Tile, Tactical.Tile, float> Value = (_, _) => 10;
        public int ValuesRead;
        public float GetTargetValue(bool immediate, Il2CppMenace.Tactical.Skills.Skill skill,
            Tactical.Tile target, Tactical.Tile from, Tactical.Tile aimedAt)
        {
            if (immediate) throw new Exception("positioning must use non-immediate native damage value");
            ValuesRead++; return Value(target, from);
        }
    }
    public class InflictSuppression : Attack
    {
        public InflictSuppression() { ID = "InflictSuppression"; Skill = new(); }
        public float GetTargetValue(bool immediate, Il2CppMenace.Tactical.Skills.Skill skill,
            Tactical.Tile target, Tactical.Tile from, Tactical.Tile aimedAt) => 10;
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    public struct TargetUsageParams
    {
        public bool IncludeDeploymentAndAngle, IncludeUses, IncludeTargetFaction;
        public static TargetUsageParams IGNORE_DEPLOYMENT_AND_ANGLE => new() { IncludeUses = true, IncludeTargetFaction = true };
    }
    public class SkillTemplate
    {
        public string m_ID = "active.fixture";
        public string GetID() => m_ID;
        public string name => throw new InvalidOperationException("Unity name access is forbidden on worker probes");
        public bool IsAttack = true, IsTargeted = true, IsLineOfFireNeeded = true;
        public bool IsDeploymentRequired, IsWeaponSetupRequired;
        public int AoEType;
        public Profile AIConfig = new();
        public bool IsDeploymentRequirementSatisfied(Tactical.Actor actor) => !IsDeploymentRequired || actor.Deployed;
        public bool IsSetupRequirementSatisfied(Tactical.Actor actor) => !IsWeaponSetupRequired || actor.WeaponSetup;
    }
    public class Profile { public float ScoreMult = 1; }
    public class Skill
    {
        public SkillTemplate Template = new();
        public bool Limited, Empty, Applicable = true, Teleport, Enabled = true, ExecutingElements = true;
        public bool IsEnabled() => Enabled;
        public bool HasExecutingElements() => ExecutingElements;
        public Func<Tactical.Tile, Tactical.Tile, bool> Range = (_, _) => true;
        public bool IsInRange(Tactical.Tile target, Tactical.Tile origin, bool angle) => Range(target, origin);
        public int Cost = 20, Probes;
        public int MaxRange = 30;
        public int GetMaxRange() => MaxRange;
        public Func<Tactical.Tile, Tactical.Tile, bool> Usable = (_, _) => true;
        public SkillTemplate GetTemplate() => Template;
        public bool HasLimitedUses() => Limited;
        public bool IsOutOfUses() => Empty;
        public int GetActionPointCost() => Cost;
        public bool IsUsableOn(Tactical.Tile target, Tactical.Tile origin, TargetUsageParams parameters)
        {
            if (!parameters.IncludeUses || !parameters.IncludeTargetFaction || parameters.IncludeDeploymentAndAngle)
                throw new Exception("changed native positioning legality parameters");
            Probes++; return Usable(target, origin);
        }
        public bool IsApplicableTo(Tactical.Tile target, Tactical.Entity entity) => Applicable;
        public Tactical.Tile GetFinalUserTile(Tactical.Tile origin, Tactical.Tile target) => Teleport ? target : origin;
    }
}
namespace Il2CppMenace.Strategy
{
    public class Mission { public Tactical.Objectives.ObjectiveManager Objectives = new(); }
}
namespace Il2CppMenace.Tactical.Objectives
{
    public class Objective
    {
        public Entity Target;
        public bool Ongoing = true, Throw;
        public bool IsOngoing() => Ongoing;
        public Entity GetTarget() => Throw ? throw new Exception("objective read failed") : Target;
        public T TryCast<T>() where T : class => this as T;
    }
    public class ProtectUnit : Objective { }
    public class RescueUnit : Objective { }
    public class Collateral : Objective
    {
        public bool Matches = true;
        public bool FulfillsConditions(Entity target) => Matches;
    }
    public class ObjectiveManager
    {
        public List<Objective> Objectives = new();
        public Objective[] m_Objectives => Objectives.ToArray();
        public List<Objective> GetObjectives() => Objectives;
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
        public float Move = 1;
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
        public int MinimumUtilityAPCost = 1024; // Exact native TileScore.Reset sentinel.
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
namespace Il2CppMenace.Tags
{
    public enum TagType { MACHINE, XENO, DRONE, INDIRECT_FIRE, WEAPONS_TEAM }
}
namespace Il2CppTactical { public enum LineOfSightFlags { Default } }
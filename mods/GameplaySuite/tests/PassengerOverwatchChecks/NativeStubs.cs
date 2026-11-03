using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppTactical;

namespace Il2CppTactical { internal enum LineOfSightFlags { Default } }

internal class Obj
{
    private static long _next;
    public long Key = ++_next;
    public T? TryCast<T>() where T : class => this as T;
}

namespace Il2CppMenace.Items
{
    internal enum ItemSlot { InfantrySpecial }
    internal class Items : Obj { public Obj? Special; public Obj? GetItemAtSlot(ItemSlot slot) => Special; }
}

namespace Il2CppMenace.Tactical
{
    internal enum EntityContainerType { None, Building, Vehicle, Forest, Tower }
    internal enum SuppressionState { None, Suppressed, PinnedDown }
    internal enum ExecutingElementType { All, AllButSpecial, OneAtRandom, OneAtRandomExceptSpecial, OneAtRandomButSpecialLast, SpecialOnly }
    internal enum Direction { North }
    internal enum CoverType { None, Low, High }
    internal static class Config { public static int[] COVER_CONCEALMENT = { 0, 2, 4 }; }
    internal class EntityProperties : Obj
    {
        public int Vision = 20, Detection, Concealment;
        public int GetVision() => Vision;
        public int GetDetection() => Detection;
        public int GetConcealment() => Concealment;
    }
    internal class Tile : Obj
    {
        public int Distance = 5, Concealment;
        public bool Los = true;
        public CoverType Cover;
        public Entity? Occupant;
        public bool HasLineOfSightTo(Tile target, LineOfSightFlags flags) => Los;
        public int GetDistanceTo(Tile target) => Distance;
        public Direction GetDirectionTo(Tile target) => Direction.North;
        public int GetConcealment() => Concealment;
        public Entity? GetEntity() => Occupant;
        public CoverType GetCover(Direction direction, Entity target, EntityProperties? properties, bool realCover) => Cover;
    }
    internal class Entity : Obj
    {
        public Entity? Container, Contained;
        public Tile Tile = new();
        public bool Throw;
        public Entity? GetContainerEntity() => Throw ? throw new Exception("native lookup") : Container;
        public Entity? GetContainedEntity() => Contained;
        public Tile GetTile() => Tile;
        public virtual EntityTemplate? GetTemplate() => new();
        public virtual bool IsActor() => false;
    }
    internal class EntityTemplate : Obj
    {
        public EntityContainerType ContainerType = EntityContainerType.Vehicle;
        public Cover? ProvidesCoverInside = new();
    }
    internal class Cover : Obj { public string name = "cover_inside_vehicle_open"; public int Concealment; }
    internal class Actor : Entity
    {
        public bool Infantry = true, Vehicle, Ready = true, Player = true, Moving;
        public int AP = 80, Updates, Faction = 1;
        public SuppressionState Suppression;
        public EntityTemplate? Template = new();
        public EntityProperties? Properties = new();
        public bool Revealed, NativeLos = true;
        public int NativeLosCalls;
        public List<Obj> Elements = new() { new(), new() };
        public Il2CppMenace.Items.Items Items = new();
        public SkillContainer Skills = new();
        public override bool IsActor() => true;
        public bool IsAlliedWith(Actor other) => Faction == other.Faction;
        public override EntityTemplate? GetTemplate() => Template;
        public int GetDistanceTo(Tile target) => Tile.GetDistanceTo(target);
        public bool HasLineOfSightTo(Entity target, Tile source, Tile destination)
        { NativeLosCalls++; return Container?.IsActor() != true && NativeLos; }
        public SuppressionState GetSuppressionState() => Suppression;
        public List<Obj> GetElements() => Elements;
        public Il2CppMenace.Items.Items GetItems() => Items;
        public int GetActionPoints() => AP;
        public void SetActionPoints(int ap, bool update) => AP = ap;
        public void UpdateActorState() => Updates++;
        public SkillContainer GetSkills() => Skills;
        public bool IsMoving() => Moving;
    }
    internal class TacticalManager : Obj
    {
        public static TacticalManager Current = new();
        public Actor? Active;
        public bool PlayerTurn = true, Finished;
        public static bool Busy;
        public static TacticalManager Get() => Current;
        public Actor? GetActiveActor() => Active;
        public bool IsPlayerTurn() => PlayerTurn;
        public bool IsFinished() => Finished;
        public static bool IsSkillBusy() => Busy;
    }
}

namespace Il2CppMenace.Tactical.Skills
{
    internal enum UsabilityWhileContained { AllowInsideStructures, Always, Never }
    internal class SkillTemplate : Obj
    {
        public UsabilityWhileContained UsabilityWhileContained;
        public bool IsUsableWhilePinnedDown;
        public ExecutingElementType ExecutingElement;
    }
    internal class SkillEventHandler : Obj
    {
        public bool Usable = true, Throw;
        public int Calls;
        public bool IsUsable() { Calls++; return Throw ? throw new Exception("handler") : Usable; }
    }
    internal class SkillContainer { public bool UiNeedsUpdate; public int Updates; public void ScheduleUpdate() => Updates++; }
    internal class TargetUsageParams { public static TargetUsageParams Player = new(); }
    internal class Skill : Obj
    {
        public Actor? Owner, Payer;
        public SkillTemplate? Template = new();
        public SkillEventHandler[]? Handlers = Array.Empty<SkillEventHandler>();
        public bool Enabled = true, Executing = true, Hidden, Limited = true, Indirect, Supported = true, Requirements = true;
        public int Uses = 4, Cost = 40, NativeQueries;
        public Actor? GetActor() => Owner;
        public Actor? GetAPConsumer() => Payer ?? Owner;
        public SkillTemplate? GetTemplate() => Template;
        public SkillEventHandler[]? GetSkillEventHandlers() => Handlers;
        public bool IsEnabled() => Enabled;
        public bool IsHidden() => Hidden;
        public bool HasExecutingElements() => Executing;
        public bool HasLimitedUses() => Limited;
        public bool IsOutOfUses() => Uses <= 0;
        public int GetUses() => Uses;
        public int GetActionPointCost() => Cost;
        public int GetMinRange() => 1;
        public int GetMaxRange() => 10;
        public bool IsInRange(Tile target, Tile source, bool angle) => true;
        public bool IsApplicableTo(Tile tile, Actor target) => true;
        public bool IsUsable(TargetUsageParams p) { NativeQueries++; return Owner?.Container?.IsActor() != true; }
        public string GetID() => "test.attack";
    }
}

namespace Il2CppMenace.States
{
    internal class TacticalState
    {
        public static TacticalState Current = new(); public int Ends;
        public static TacticalState Get() => Current;
        public void EndTurn() => Ends++;
    }
}

namespace MenaceOverwatch
{
    internal static partial class Native
    {
        internal static long Key(Obj? value) => value?.Key ?? 0;
        internal static bool Valid(Obj? value) => Key(value) != 0;
        internal static bool Same(Obj? a, Obj? b) => Key(a) != 0 && Key(a) == Key(b);
        internal static bool IsInfantryActor(Actor? a) => Valid(a) && a!.Infantry;
        internal static bool IsVehicleActor(Actor? a) => Valid(a) && a!.Vehicle;
        internal static bool IsCombatReady(Actor? a) => Valid(a) && a!.Ready;
        internal static bool IsPlayerControlled(Actor a) => a.Player;
        internal static bool IsIndirectFire(Skill s) => s.Indirect;
        internal static bool SupportedForActor(Actor a, Skill s) => s.Supported;
        internal static bool RequirementsSatisfied(Skill s, Actor a) => s.Requirements;
        internal static bool IsAlphaStrikeSkill(Skill s) => false;
        internal static EntityProperties? CurrentProperties(Actor actor, out string type)
        { type = "test actor"; return actor.Properties; }
        internal const string TargetNotVisibleReason = "target is not visible/detectable to the shooter";
    }
}
using System.Reflection;

namespace HarmonyLib
{
    public sealed class HarmonyMethod(Type type, string name)
    {
        internal readonly MethodInfo Method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public)!;
    }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] args) => type.GetMethod(name, args);
    }
    public sealed class Harmony
    {
        internal static readonly Dictionary<MethodBase, (HarmonyMethod? Prefix, HarmonyMethod? Postfix, HarmonyMethod? Finalizer)> Hooks = new();
        public void Patch(MethodBase method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? finalizer = null)
            => Hooks.Add(method, (prefix, postfix, finalizer));
        internal static void Call(MethodBase method, bool after, object?[] args)
        {
            if (Hooks.TryGetValue(method, out var hooks))
                (after ? hooks.Postfix : hooks.Prefix)?.Method.Invoke(null, args);
        }
        internal static T Movement<T>(MethodBase method, object actor, Func<T> native)
        {
            Hooks.TryGetValue(method, out var hooks);
            object?[] before = { actor, null };
            hooks.Prefix?.Method.Invoke(null, before);
            Exception? failure = null;
            T result = default!;
            try { result = native(); }
            catch (Exception e) { failure = e; }
            if (hooks.Finalizer != null)
                failure = (Exception?)hooks.Finalizer.Method.Invoke(null, new object?[] { failure, before[1] });
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }
    }
}
namespace Il2CppInterop.Runtime.InteropTypes
{
    public class Il2CppObjectBase
    {
        private static long next;
        public IntPtr Pointer = new(++next);
        public T? TryCast<T>() where T : class => this as T;
    }
}
namespace Il2CppMenace.Tactical
{
    using Il2CppInterop.Runtime.InteropTypes;
    using Skills;
    public enum ActorStance { Default, Deployed, PinnedDown }
    public enum SuppressionState { None, Suppressed, PinnedDown }
    [Flags] public enum EntityFlags { None = 0, Stunned = 1, Rooted = 4, HeavyWeaponsAllowed = 8, RootedByStance = 16384 }
    [Flags] public enum MovementAction { None, Enter, Leave }
    public enum MovementFlags { None }
    public enum MoveBackwardsMode { None }
    public enum MovementFailedReason { None, Deployed, Rooted }
    public enum EntityPropertyType { Accuracy = 3, AccuracyMult = 9, APLeaveCost = 51 }
    public sealed class EntityProperties : Il2CppObjectBase
    {
        public EntityFlags Flags;
        public float Accuracy = 40, AccuracyMult = 1;
        public int APLeaveCost = 40;
        public void UpdateProperty(EntityPropertyType type, int amount)
        {
            if (type == EntityPropertyType.Accuracy) Accuracy += amount;
            else throw new Exception("Unexpected non-accuracy property");
        }
        public void UpdateMultProperty(EntityPropertyType type, float amount)
        {
            if (type == EntityPropertyType.AccuracyMult) AccuracyMult *= amount;
            else throw new Exception("Unexpected non-accuracy multiplier");
        }
        public bool IsRooted() => (Flags & (EntityFlags.Rooted | EntityFlags.RootedByStance)) != 0;
    }
    public sealed class Tile { public bool Walkable = true; }
    public sealed class EntityTemplate(string id)
    {
        public string GetID() => id;
        public int ContainerType = 1;
    }
    public class Entity(string id = "other") : Il2CppObjectBase
    {
        public Entity? Container, Contained;
        public readonly EntityTemplate Template = new(id);
        public bool FailEject;
        public EntityTemplate GetTemplate() => Template;
        public Entity? GetContainerEntity() => Container;
        public Entity? GetContainedEntity() => Contained;
        public void ContainEntity(Entity other)
        {
            if (Contained == null && other.Container == null) { Contained = other; other.Container = this; }
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, true, new object?[] { this, other });
        }
        public bool EjectEntity(Tile tile)
        {
            var method = MethodBase.GetCurrentMethod()!;
            object?[] before = { this, null };
            HarmonyLib.Harmony.Call(method, false, before);
            bool result = !FailEject && Contained != null;
            if (result)
            {
                // Model native callbacks seeing old containment until the transaction completes.
                (Contained as Actor)?.Skills.Update();
                Contained!.Container = null; Contained = null;
            }
            HarmonyLib.Harmony.Call(method, true, new object?[] { result, before[1] });
            return result;
        }
    }
    public class Actor : Entity
    {
        public readonly SkillContainer Skills;
        public readonly EntityProperties Properties = new();
        public Actor() => Skills = new(this);
        public ActorStance Stance;
        public ActorStance m_Stance { get => Stance; set => Stance = value; }
        public SuppressionState Suppression;
        public bool Infantry = true, Moving, Dying, ThrowStance;
        public int StanceChanges, AP = 100;
        public bool IsInfantry() => Infantry;
        public bool IsMoving() => Moving;
        public bool IsDying() => Dying;
        public ActorStance GetStance() => Stance;
        public SuppressionState GetSuppressionState() => Suppression;
        public SkillContainer GetSkills() => Skills;
        public virtual EntityProperties GetCurrentProperties() => Properties;
        public void SetStance(ActorStance stance, bool force = false)
        {
            if (ThrowStance) throw new Exception("invalid native actor");
            Stance = stance; StanceChanges++;
        }
        public bool FailMove, ThrowMove;
        public bool MoveTo(Tile tile, ref MovementAction action, MovementFlags flags = MovementFlags.None)
        {
            return HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, this, () =>
            {
                if (ThrowMove) throw new InvalidOperationException("native movement failure");
                var path = Il2CppTactical.MovementSystem.GetPath(this, tile, MoveBackwardsMode.None, flags);
                if (FailMove || !path.Success || AP < path.Cost) return false;
                Skills.Update(); // Synchronous property rebuild while the old container pointer is still set.
                if (Stance == ActorStance.Deployed) return false;
                AP -= path.Cost;
                Moving = true;
                return true;
            });
        }
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    using Il2CppInterop.Runtime.InteropTypes;
    public sealed class Skill(Actor? actor) : Il2CppObjectBase
    {
        public Actor? GetActor() => actor;
        public void OnUpdate(EntityProperties properties)
            => HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, true, new object?[] { this, properties });
    }
    public class SkillEventHandlerTemplate : Il2CppObjectBase { }
    public sealed class SkillTemplate : Il2CppObjectBase
    {
        public readonly List<SkillEventHandlerTemplate> EventHandlers = new();
    }
    public sealed class SkillContainer(Il2CppObjectBase owner) : Il2CppObjectBase
    {
        public bool UiNeedsUpdate;
        public int Scheduled;
        public float DisplayedAim;
        public Action? DuringRebuild;
        public bool ThrowUpdate;
        public Il2CppObjectBase GetOwner() => owner;
        public void ScheduleUpdate() => Scheduled++;
        public void Update()
        {
            HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, this, () =>
            {
                if (ThrowUpdate) throw new InvalidOperationException("native property failure");
                if (owner is not Actor actor) return true;
                actor.Properties.Accuracy = 40; actor.Properties.AccuracyMult = 1;
                actor.Properties.Flags &= ~EntityFlags.RootedByStance;
                if (actor.Stance == ActorStance.Deployed)
                {
                    actor.Properties.Flags |= EntityFlags.RootedByStance;
                    actor.Properties.AccuracyMult *= 1.15f; // Stand-in native stance effect only.
                }
                var skill = new Skill(actor);
                skill.OnUpdate(actor.Properties);
                DuringRebuild?.Invoke();
                skill.OnUpdate(actor.Properties); // Multiple skills must not stack the garrison effect.
                DisplayedAim = actor.Properties.Accuracy * actor.Properties.AccuracyMult;
                return true;
            });
        }
        public void OnMovementFinished(Tile tile)
            => HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, true, new object?[] { this });
    }
}
namespace Il2CppMenace.Tactical.Skills.Effects
{
    public struct PropertyChange
    {
        public EntityPropertyType PropertyType;
        public int Amount;
        public float AmountMult;
    }
    public sealed class StanceDeployed : SkillEventHandlerTemplate
    {
        public PropertyChange[] Properties = Array.Empty<PropertyChange>();
    }
}
namespace Il2CppMenace.Tools
{
    public static class DataTemplateLoader
    {
        public static Il2CppMenace.Tactical.Skills.SkillTemplate? Template;
        public static T? Get<T>(string id, bool log = true) where T : class
        {
            if (id != "special.stance.deployed" || log) throw new Exception("Unexpected template lookup");
            return Template as T;
        }
    }
}
namespace GarrisonableBuildings
{
    using Il2CppInterop.Runtime.InteropTypes;
    using Il2CppMenace.Tactical;
    public sealed partial class Plugin
    {
        private const string ModName = "test";
        private static readonly HashSet<string> _enabledGarrisonIds = new(StringComparer.OrdinalIgnoreCase) { "shelter" };
        internal static readonly List<string> Warnings = new();
        private static bool Valid(Il2CppObjectBase? value) => value?.Pointer != null && value.Pointer != IntPtr.Zero;
        private static string Short(Exception e) => e.Message;
        private static string EntityId(Entity e) => e.GetTemplate().GetID();
        private static void DebugMsg(string message) { }
        private static void DebugWarning(string message) => Warnings.Add(message);
        private static void DebugError(string message) => throw new Exception(message);
        internal static void Install() => InstallGarrisonDeploymentHooks(new HarmonyLib.Harmony());
        internal static void Clear() => ClearGarrisonDeployment();
        internal static void Resolve()
        {
            _garrisonAccuracyChanges = null;
            TryResolveGarrisonAccuracy();
        }
        internal static bool Resolved => _garrisonAccuracyChanges != null;
        internal static bool InRebuild => _garrisonPropertyFrame != null;
    }
}
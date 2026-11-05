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
        internal static readonly Dictionary<MethodBase, (HarmonyMethod? Prefix, HarmonyMethod? Postfix)> Hooks = new();
        public void Patch(MethodBase method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
            => Hooks.Add(method, (prefix, postfix));
        internal static void Call(MethodBase method, bool after, object?[] args)
        {
            if (Hooks.TryGetValue(method, out var hooks))
                (after ? hooks.Postfix : hooks.Prefix)?.Method.Invoke(null, args);
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
    public sealed class Tile { }
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
    public sealed class Actor : Entity
    {
        public readonly SkillContainer Skills;
        public Actor() => Skills = new(this);
        public ActorStance Stance;
        public SuppressionState Suppression;
        public bool Infantry = true, Moving, Dying, ThrowStance;
        public int StanceChanges, AP = 100;
        public bool IsInfantry() => Infantry;
        public bool IsMoving() => Moving;
        public bool IsDying() => Dying;
        public ActorStance GetStance() => Stance;
        public SuppressionState GetSuppressionState() => Suppression;
        public SkillContainer GetSkills() => Skills;
        public void SetStance(ActorStance stance, bool force = false)
        {
            if (ThrowStance) throw new Exception("invalid native actor");
            Stance = stance; StanceChanges++;
        }
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    using Il2CppInterop.Runtime.InteropTypes;
    public sealed class SkillContainer(Il2CppObjectBase owner) : Il2CppObjectBase
    {
        public bool UiNeedsUpdate;
        public int Scheduled;
        public float DisplayedAim;
        public Il2CppObjectBase GetOwner() => owner;
        public void ScheduleUpdate() => Scheduled++;
        public void Update()
        {
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, false, new object?[] { this });
            // Stand-in for MENACE's property rebuild, NOT production bonus logic.
            DisplayedAim = owner is Actor { Stance: ActorStance.Deployed } ? 46 : 40;
        }
        public void OnMovementFinished(Tile tile)
            => HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, true, new object?[] { this });
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
    }
}
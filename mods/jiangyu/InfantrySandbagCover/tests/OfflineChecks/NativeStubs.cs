using System.Collections;
using System.Reflection;
using InfantrySandbagCover;

namespace HarmonyLib
{
    public sealed class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodBase method, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod finalizer = null) { }
        public void UnpatchSelf() { }
    }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string method) { } }
    public static class AccessTools { public static MethodInfo Method(Type type, string name, Type[] args) => type.GetMethod(name, args); }
}
namespace Jiangyu.Sdk
{
    public abstract class JiangyuSystem
    {
        public FakeContext Context = new();
        public virtual void OnInit() { }
        public virtual void OnSceneLoaded(int i, string scene) { }
        public virtual void OnUnload() { }
    }
    public sealed class FakeContext { public FakeCoroutines Coroutines = new(); public FakeLog Log = new(); }
    public sealed class FakeCoroutines { public object Start(IEnumerator e) => e; public void Stop(object e) { } }
    public sealed class FakeLog { public void Warn(string s) => Log.Warn(s); }
    public static class Log { public static List<string> Warnings = new(); public static void Warn(string s) => Warnings.Add(s); }
}
namespace MenaceModSettings
{
    public sealed class SettingDefinition(int min, int max, int value)
    {
        public object Default = value;
        public object Coerce(int value) => Math.Clamp(value, min, max);
    }
    public sealed class SettingsBuilder
    {
        public List<SettingDefinition> Items = new();
        public SettingsBuilder AddNumber(string key, string label, int min, int max, int value)
        { Items.Add(new(min, max, value)); return this; }
    }
    public static class ModSettings
    {
        public static event Action<string, string, object> Changed;
        public static int Turns = 2;
        public static List<SettingDefinition> Definitions;
        public static void Register(string group, Action<SettingsBuilder> build)
        {
            var builder = new SettingsBuilder(); build(builder); Definitions = builder.Items;
            Turns = (int)Definitions.Single().Default;
        }
        public static T Get<T>(string group, string key) => (T)(object)Turns;
        public static void SetTurns(int turns) { Turns = turns; Changed?.Invoke("Infantry Sandbag Cover", "ConstructionTurns", turns); }
    }
    public static class ModGui
    {
        public static Action Draw;
        public static void Add(Action draw) => Draw += draw;
        public static void Remove(Action draw) => Draw -= draw;
    }
}
namespace Il2CppInterop.Runtime { public static class Il2CppType { public static Type Of<T>() => typeof(T); } }
namespace Il2CppMenace.Tactical
{
    public enum Direction { North = 0, East = 2, South = 4, West = 6 }
    public enum MovementAction { Walk }
    public class Entity { }
    public class Element { }
    public sealed class Tile(int x, int z)
    {
        private static long _next;
        public IntPtr Pointer = new(++_next);
        public int Mask;
        public int GetX() => x;
        public int GetZ() => z;
        public bool HasHalfCoverInDir(Direction d) => (Mask & (1 << ((int)d / 2))) != 0;
        public UnityEngine.Vector3 GetPos() => new(x * 8, 0, z * 8);
    }
    public sealed class Actor : Entity
    {
        private static long _next;
        public IntPtr Pointer = new(++_next);
        public Tile Tile = new(4, 5);
        public int AP = 100, HP = 100, TilesMovedThisTurn;
        public bool Moving, Contained, Busy, Player = true, Infantry = true;
        public int GetActionPoints() => AP;
        public void SetActionPoints(int ap, bool force = false) => AP = ap;
        public Tile GetTile() => Tile;
        public int GetHitpoints() => HP;
        public bool IsInfantry() => Infantry;
        public bool IsPlayerOrPlayerAI() => Player;
        public bool IsMoving() => Moving;
        public bool IsContainedWithinAnotherEntity() => Contained;
    }
    public sealed class TacticalManager
    {
        public static TacticalManager Instance = new();
        public HashSet<Skills.Skill> BusySkills = new();
        public int Round = 1;
        public static TacticalManager Get() => Instance;
        public int GetRound() => Round;
        public void InvokeOnMovement(Actor actor, Tile from, Tile to, MovementAction action, Entity container)
            => ConstructionSystem.Moved(actor, from, to);
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    using Il2CppMenace.Tactical;
    using Il2CppMenace.Tactical.Skills.Effects;
    [Flags] public enum UsageParameter { Default = 0, Fake = 2, NoApConsumed = 64 }
    public struct TargetUsageParams { }
    public class SkillEventHandler { public Skill ParentSkill { get; set; } }
    public sealed class Skill
    {
        private static long _next;
        public IntPtr Pointer = new(++_next);
        public string ID = ConstructionSystem.SkillId;
        public Actor Actor = new();
        public bool NativeUsable = true, Delay, DeferCompletion, PlacementFails, ThrowPlacement, ThrowEnvironment, RejectAfterCharge;
        public int Applies, EnvironmentChanges, Cost = 80, NativeApplications, ClearBusyCalls, AfterUseCalls, ContainerUpdates;
        private Tile _scheduledTile;
        private UsageParameter _scheduledUsage;
        public string GetID() => ID;
        public Actor GetActor() => Actor;
        public int GetActionPointCost() => Cost;
        public bool IsUsable() { bool value = NativeUsable && !Actor.Busy && Actor.AP >= Cost; ConstructionSystem.Usable(this, ref value); return value; }
        public bool IsUsable(TargetUsageParams p) => IsUsable();
        public bool Use(Tile tile, UsageParameter usage = UsageParameter.Default)
        {
            bool result = false;
            var original = ConstructionSystem.BeforeUse(this, ref tile, usage, ref result, out var state);
            try
            {
                if (original && IsUsable(new TargetUsageParams()))
                {
                    Actor.AP -= Cost;
                    if (!RejectAfterCharge)
                    {
                        Actor.Busy = true;
                        TacticalManager.Instance.BusySkills.Add(this);
                        if (!Delay) ApplyToTile(tile, usage);
                    }
                    result = !RejectAfterCharge;
                }
                ConstructionSystem.AfterUse(this, result, state);
                return result;
            }
            catch (Exception e) { ConstructionSystem.UseFailed(e, this, state); throw; }
        }
        public void ApplyToTile(Tile tile, UsageParameter usage)
        {
            bool original = ConstructionSystem.BeforeApply(this, tile, usage, out var state);
            try
            {
                if (original)
                {
                    // Native ApplyToTile schedules BOTH effects and completion. Skipping
                    // this method leaves the accepted skill/actor/tactical manager busy.
                    NativeApplications++;
                    _scheduledTile = tile;
                    _scheduledUsage = usage;
                }
                if (!DeferCompletion) FinishNative();
            }
            catch (Exception e) { ConstructionSystem.ApplyFailed(e, this, state); throw; }
        }
        public void FinishNative()
        {
            if (_scheduledTile == null) return;
            var tile = _scheduledTile;
            _scheduledTile = null;
            new SpawnHalfCoverHandler { ParentSkill = this }.OnApply(Actor, Actor.Tile, tile, tile, null, true);
            new DestroyEnvironmentFeaturesHandler { ParentSkill = this }.OnTileHit(this, tile, tile, tile.GetPos());
            ClearBusy(_scheduledUsage);
            AfterUseCalls++;
            ContainerUpdates++;
        }
        public void ClearBusy(UsageParameter usage)
        {
            ClearBusyCalls++;
            Actor.Busy = false;
            TacticalManager.Instance.BusySkills.Remove(this);
            ConstructionSystem.Cleared(this);
        }
    }
}
namespace Il2CppMenace.Tactical.Skills.Effects
{
    using Il2CppMenace.Tactical;
    public sealed class SpawnHalfCoverHandler : SkillEventHandler
    {
        public void OnApply(Actor user, Tile from, Tile target, Tile center, Element element, bool hit)
        {
            try
            {
                if (!ConstructionSystem.BeforeCover(this, target)) return;
                ParentSkill.Applies++;
                if (ParentSkill.ThrowPlacement) throw new InvalidOperationException("fixture placement exception");
                if (!ParentSkill.PlacementFails) target.Mask = 15;
            }
            catch (Exception e) { ConstructionSystem.CoverFailed(e, this); throw; }
        }
    }
    public sealed class DestroyEnvironmentFeaturesHandler : SkillEventHandler
    {
        public void OnTileHit(Skill skill, Tile target, Tile center, UnityEngine.Vector3 impact)
        {
            try
            {
                if (!ConstructionSystem.BeforeEnvironment(skill, target)) return;
                if (skill.ThrowEnvironment) throw new InvalidOperationException("fixture environment exception");
                skill.EnvironmentChanges++;
            }
            catch (Exception e) { ConstructionSystem.EnvironmentFailed(e, skill); throw; }
        }
    }
}
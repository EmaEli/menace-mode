using System.Reflection;

// Managed stand-ins model native field reads, not IL2CPP detours. Production hooks
// are registered and dispatched by exact method signature, including overloads.
namespace HarmonyLib
{
    public static class Priority { public const int Last = 0; }
    public sealed class HarmonyMethod(Type type, string name)
    {
        public int priority;
        internal MethodInfo Method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
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
        internal static void Call(MethodBase method, object instance, bool after = false)
        {
            if (Hooks.TryGetValue(method, out var hooks))
                (after ? hooks.Postfix : hooks.Prefix)?.Method.Invoke(null, new[] { instance });
        }
    }
}

namespace Il2CppMenace.Tactical.Skills
{
    public class BaseSkill
    {
        public T? TryCast<T>() where T : class => this as T;
    }
    public sealed class Skill(string id, int range, long key) : BaseSkill
    {
        public IntPtr Pointer = new(key);
        public int m_MaxRange = range;
        public int TemplateMaxRange = range;
        public string GetID() => id;
        public int GetMaxRange()
        {
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, this);
            return m_MaxRange;
        }
        public bool IsInRange(int distance, int additional)
        {
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, this);
            return distance >= 1 && distance <= m_MaxRange + additional;
        }
        public bool IsInRange(Tile target, Tile user, bool angle = true)
        {
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, this);
            return Math.Abs(target.X - user.X) <= m_MaxRange;
        }
        public void SetRanges(int min, int max, int ideal)
        {
            m_MaxRange = Math.Max(max, Math.Max(min, ideal));
            HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, this, after: true);
        }
    }
    public sealed class SkillContainer(params BaseSkill[] skills)
    {
        public IntPtr Pointer = new(1);
        public BaseSkill[] GetAllSkills() => skills;
        public void Update() => HarmonyLib.Harmony.Call(MethodBase.GetCurrentMethod()!, this, after: true);
    }
}

namespace Il2CppMenace.Tactical
{
    using Skills;
    public sealed class Tile(int x) { public int X = x; }
    public sealed class Actor(SkillContainer container) { public SkillContainer GetSkills() => container; }
    public sealed class BaseFaction(params Actor[] actors) { public Actor[] GetActors() => actors; }
    public sealed class TacticalManager(params BaseFaction[] factions)
    {
        public static TacticalManager? Current;
        public IntPtr Pointer = new(1);
        public static TacticalManager? Get() => Current;
        public BaseFaction[] GetFactions() => factions;
    }
}

namespace MenaceWeaponRangesRebalanced
{
    public sealed partial class Plugin
    {
        private static bool _enabled = true, _limitThrownGrenadeRange = true;
        private static int _thrownGrenadeMaxRange = 2;
        internal static readonly List<string> Logs = new();
        private static void DebugLog(string message) => Logs.Add(message);
        private static void Warn(string message) => throw new Exception(message);
        internal static void Install() => InstallThrownGrenadeRuntimePatches(new HarmonyLib.Harmony());
        internal static void Configure(bool enabled, bool limit, int max = 2)
        {
            _enabled = enabled;
            _limitThrownGrenadeRange = limit;
            _thrownGrenadeMaxRange = max;
            RefreshRuntimeThrownGrenadeRanges();
        }
        internal static void SceneChanged()
        {
            RefreshRuntimeThrownGrenadeRanges(restoreOnly: true);
            lock (RuntimeGrenadeRanges) RuntimeGrenadeRanges.Clear();
        }
    }
}
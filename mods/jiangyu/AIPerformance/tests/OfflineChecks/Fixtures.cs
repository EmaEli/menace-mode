using System.Reflection;

// Recovery policy is exercised separately by AdvanceRecoveryChecks with native stand-ins.
namespace AIPerformance
{
    public sealed partial class Plugin
    {
        private static void PatchAdvanceRecovery(HarmonyLib.Harmony harmony) { }
        private static void AdvanceRecoveryReset() { }
        private static bool _contactConvergenceEnabled { get; set; }
        private static void PatchContactConvergence(HarmonyLib.Harmony harmony) { }
        private static void ContactConvergenceReset() { }
        private static bool _missionCivilianPriorityEnabled { get; set; }
        private static void PatchMissionCivilianPriority(HarmonyLib.Harmony harmony) { }
        private static bool _offensivePlanningEnabled { get; set; }
        private static void PatchOffensivePlanning(HarmonyLib.Harmony harmony) { }
        private static void OffensivePlanningReset() { }
    }
}

namespace MenaceModSettings
{
    public static class ModSettings
    {
        public static readonly Dictionary<(string, string), object> Values = new();
        public static object Get(string mod, string key) => Values.GetValueOrDefault((mod, key));
        public static void Register(string mod, Action<Builder> configure) => configure(new Builder(mod));
        public sealed class Builder(string mod)
        {
            public void AddHeader(string title) { }
            public void AddToggle(string key, string label, bool value) => Values.TryAdd((mod, key), value);
            public void AddSlider(string key, string label, float min, float max, float value) => Values.TryAdd((mod, key), value);
        }
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyMethod(MethodInfo method) { public MethodInfo Method = method; }
    public sealed class Harmony
    {
        public MethodInfo Target, Prefix;
        public void Patch(MethodInfo target, HarmonyMethod prefix)
            => (Target, Prefix) = (target, prefix.Method);
    }
}
namespace Il2CppSystem
{
    public sealed class Action { }
}
namespace Il2CppSystem.Threading.Tasks
{
    public sealed class Task
    {
        public static readonly Task Worker = new();
        public static Il2CppSystem.Action Scheduled;
        public static Task Run(Il2CppSystem.Action action) { Scheduled = action; return Worker; }
    }
}
namespace Il2CppMenace.Tactical
{
    public sealed class Actor { }
}
namespace Il2CppMenace.Tactical.AI
{
    public static class AIFaction
    {
        private static Il2CppSystem.Threading.Tasks.Task RunWithTimeout(Il2CppSystem.Action action,
            Il2CppMenace.Tactical.Actor actor) => throw new Exception("Original must not run");
    }
    public static class AIConfig { public static Weights WEIGHTS { get; } = new(); }
    public sealed class Weights
    {
        public int NearTileLimit { get; set; } = 64;
        private int _cull = 20;
        public bool RejectCull;
        public int CullTilesDistances { get => _cull; set { if (RejectCull) throw new Exception(); _cull = value; } }
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    public sealed class SkillTemplate
    {
        public string name;
        public string m_ID;
        public string GetID() => m_ID ??= name;
        public bool IsAttack;
        public bool IsLineOfFireNeeded = true;
        public Profile AIConfig;
    }
    public class Profile
    {
        public IntPtr Pointer { get; set; }
        private bool _positioning = true;
        public bool IgnoreWrites;
        public bool InfluencesPositioning { get => _positioning; set { if (!IgnoreWrites) _positioning = value; } }
    }
}
namespace Il2CppMenace.Tools
{
    public static class DataTemplateLoader
    {
        public static readonly Dictionary<string, Il2CppMenace.Tactical.Skills.SkillTemplate> Templates = new();
        public static bool EmptyCollection;
        public static IEnumerable<T> GetAll<T>() => EmptyCollection ? Array.Empty<T>() : Templates.Values.Cast<T>();
        public static T Get<T>(string id, bool required) where T : class => Templates.GetValueOrDefault(id) as T;
    }
}
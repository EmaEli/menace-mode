// Test doubles for the host/interop helpers. The diagnostic callbacks themselves
// are linked unchanged from production; this is not an IL2CPP runtime test.
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace LargerMaps;

public sealed partial class Plugin
{
    private const string ModName = "Test";
    private static readonly TestLog _log = new();
    private static readonly Type _tacticalManagerType = null, _aiFactionType = null,
        _agentType = null, _moveBehaviorType = typeof(FakeMove), _skillType = null;
    public static readonly List<string> Lines = new();
    public static int ProfileCalls;
    private struct ProfileState { internal int Marker; }

    public static string MoveState(object behavior) => AIRouteMoveState(AIRouteAsMove(behavior));

    // Same declaring type and different __state type as the real deep profiler.
    private static void ProfilePrefix(out ProfileState __state)
        => __state = new ProfileState { Marker = 123 };
    private static void ProfilePostfix(ProfileState __state)
    {
        if (__state.Marker != 123) throw new Exception("Profiler state corrupted");
        ProfileCalls++;
    }

    private static void LegacyPrefix(out string __state) => __state = "legacy";
    private static void LegacyPostfix(string __state) { }

    public static bool LegacyCollisionIsRejected(Harmony harmony, Type target)
    {
        InstallProfiler(harmony, target.GetMethod("Execute"));
        return TryInstallRouteProbe(harmony, target, "Execute", 1,
            nameof(LegacyPrefix), nameof(LegacyPostfix)) == 0;
    }

    public static void Install(Harmony harmony, Type target, bool enabled)
    {
        _aiRouteBudget.Reset();
        _aiRouteEnabled = enabled;
        int ok = TryInstallRouteProbe(harmony, target, "Execute", 1,
            nameof(AIRouteBehaviorProbe.AIRouteBehaviorPrefix),
            nameof(AIRouteBehaviorProbe.AIRouteBehaviorPostfix), typeof(AIRouteBehaviorProbe));
        if (ok != 1) throw new Exception("Route installation failed: " + string.Join(";", Lines));
    }

    public static void InstallProfiler(Harmony harmony, MethodInfo target)
        => harmony.Patch(target, new HarmonyMethod(typeof(Plugin), nameof(ProfilePrefix)),
            new HarmonyMethod(typeof(Plugin), nameof(ProfilePostfix)));

    private static Type FindType(string _) => null;
    private static bool ReadBool(string _, bool fallback) => fallback;
    private static object GetMember(object value, string name)
        => value?.GetType().GetField(name)?.GetValue(value) ?? value?.GetType().GetProperty(name)?.GetValue(value);
    private static object InvokeNoArg(object value, string name)
        => value?.GetType().GetMethod(name, Type.EmptyTypes)?.Invoke(value, null);
    private static object InvokeStaticNoArg(Type _, string name) => null;
    private static long GetStableObjectKey(object value) => value == null ? 0 : 1;
    private static string Safe(object value) => value?.ToString() ?? "<null>";
    private static string DescribeTile(object value) => Safe(value);
    private static string DescribeActor(object value) => Safe(value);
    private static int ToInt(object value, int fallback)
        => value == null ? fallback : Convert.ToInt32(value);
    private static int GetCollectionCount(object value) => (value as ICollection)?.Count ?? 0;
    private static List<object> SnapshotCollection(object value, int limit)
        => (value as IEnumerable)?.Cast<object>().Take(limit).ToList() ?? new();
    private sealed class TestLog
    {
        public void Msg(string line) => Lines.Add(line);
        public void Warning(string line) => Lines.Add(line);
    }
}
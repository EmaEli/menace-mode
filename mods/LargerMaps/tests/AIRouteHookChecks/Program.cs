using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LargerMaps;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

var legacy = new Harmony("men002.hook-check.legacy");
Check(Plugin.LegacyCollisionIsRejected(legacy, typeof(LegacyBehavior)),
    "Original shared-declaring-type state collision reproduced");
// Remove the intentionally invalid pair atomically; removing just its prefix
// would ask Harmony to rebuild an invalid intermediate wrapper again.
legacy.Unpatch(typeof(LegacyBehavior).GetMethod("Execute"), HarmonyPatchType.All, legacy.Id);

Check(Plugin.MoveState(new FakeMove()).Contains("belowUtility=3"), "Concrete Move state read");
Check(Plugin.MoveState(new FakeBaseWrapper()).Contains("belowUtility=3"), "Base proxy safely rewrapped as Move");
Check(Plugin.MoveState(new FakeBehavior()) == "<unavailable>", "Unrelated behavior not cast");

// Actual Harmony patch generation and calls, with production route callbacks.
// Both installation orders must keep the profiler's struct and route's string
// state independent. Diagnostic OFF must not change the native return either.
foreach (bool routeFirst in new[] { false, true })
foreach (bool enabled in new[] { false, true })
{
    var harmony = new Harmony($"men002.hook-check.{routeFirst}.{enabled}");
    MethodInfo target = typeof(FakeBehavior).GetMethod(nameof(FakeBehavior.Execute));
    Plugin.Lines.Clear();
    Plugin.ProfileCalls = 0;
    if (routeFirst) Plugin.Install(harmony, typeof(FakeBehavior), enabled);
    Plugin.InstallProfiler(harmony, target);
    if (!routeFirst) Plugin.Install(harmony, typeof(FakeBehavior), enabled);
    var behavior = new FakeBehavior();
    Check(behavior.Execute(new object()), "Native true result preserved");
    Check(!behavior.Execute(null), "Native false result preserved");
    Check(behavior.Calls == 2, "Original executed exactly twice");
    Check(Plugin.ProfileCalls == 2, "Profiler state and calls preserved");
    Check(Plugin.Lines.Count(s => s.Contains("AI ROUTE BEHAVIOR")) == (enabled ? 1 : 0),
        "Opt-in route callback ran; null actor is safely ignored");
    Check(!Plugin.Lines.Any(s => s.Contains("missing hook") || s.Contains("observation failed")),
        "No hook/observer failure");
    harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
}
Console.WriteLine($"AI route managed Harmony checks passed: {checks} (not IL2CPP)");

public class FakeBehavior
{
    public int Calls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Execute(object actor) { Calls++; return actor != null; }
    public string GetName() => "Idle";
    public string GetID() => "Idle";
    public int GetScore() => 1;
}

public class LegacyBehavior
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Execute(object actor) => actor != null;
}

public class FakeMove
{
    public int m_TurnsBelowUtilityThreshold = 3;
}

public class FakeBaseWrapper
{
    public string GetID() => "Move";
    public T TryCast<T>() where T : class => new FakeMove() as T;
}
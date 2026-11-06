#nullable disable
namespace AIPerformance;

// Runs the linked production setup/restore code with host/reflection/native-object
// doubles. These are not IL2CPP templates and cannot prove game behavior or CPU cost.
public sealed partial class Plugin
{
    private const string ModName = "Planning test";
    private const int ZoneReferenceSize = 64;
    private static int TargetSize = 80;
    private static readonly PlanningTestLog _log = new();
    private static readonly Dictionary<string, bool> TestSettings = new();
    private static string[] KnownCombatSkillIds => Enumerable.Range(0, 6).Select(i => "active.test_attack_" + i).ToArray();
    private static readonly string[] KnownSupportSkillIds = { "active.rogue_officer_whistle" };
    private static bool ReadBool(string key, bool fallback) => TestSettings.TryGetValue(key, out bool value) ? value : fallback;
    private static Type FindType(string name) => name.EndsWith("DataTemplateLoader") ? typeof(PlanningTestLoader) : typeof(PlanningTestTemplate);
    private static object GetMember(object obj, string name) => obj?.GetType().GetProperty(name)?.GetValue(obj);
    private static bool TrySetMember(object obj, string name, object value)
    {
        try { obj.GetType().GetProperty(name).SetValue(obj, value); return true; }
        catch { return false; }
    }
    private static int ToInt(object value, int fallback) => value == null ? fallback : Convert.ToInt32(value);

    private static PlanningTestTemplate[] ResetPlanningTest(bool? simplify = true, int size = 80)
    {
        RestoreSimplifiedCombatPlanning();
        _combatPlanningWarned = false;
        TargetSize = size;
        TestSettings.Clear();
        if (simplify.HasValue) TestSettings["SimplifyCombatAIPlanning"] = simplify.Value;
        var ids = KnownCombatSkillIds.Concat(KnownSupportSkillIds);
        var templates = ids.Select((id, i) => new PlanningTestTemplate {
            name = id, IsAttack = i < KnownCombatSkillIds.Length, AIConfig = new PlanningTestProfile(new PlanningTestState())
        }).ToArray();
        PlanningTestLoader.Templates = templates;
        PlanningTestLoader.ThrowEnumeration = false;
        PlanningTestLoader.ThrowAfterFirst = false;
        PlanningTestLoader.MissingId = null;
        PlanningTestLoader.Lookups = 0;
        return templates;
    }

    public static void RunPlanningChecks(Action<bool, string> check)
    {
        foreach (int size in new[] { 64, 80, 96 })
        foreach (bool simplify in new[] { false, true })
        {
            var templates = ResetPlanningTest(simplify, size);
            ApplySimplifiedCombatPlanning();
            bool active = size >= 64 && simplify;
            for (int i = 0; i < templates.Length; i++)
                check(templates[i].AIConfig.InfluencesPositioning == !active,
                    "all attack and buff templates follow the same global setting");
            int writes = templates.Sum(t => t.AIConfig.State.Writes), lookups = PlanningTestLoader.Lookups;
            ApplySimplifiedCombatPlanning();
            check(templates.Sum(t => t.AIConfig.State.Writes) == writes && PlanningTestLoader.Lookups == lookups,
                "active scene setup is not repeated per callback");
            RestoreSimplifiedCombatPlanning();
            check(templates.All(t => t.AIConfig.InfluencesPositioning), "scene exit restores all owned true-to-false mutations");
            check(_combatPlanningRestores.Count == 0 && !_combatPlanningActive, "scene exit clears restores and active guard");
        }
        var defaults = ResetPlanningTest(null);
        ApplySimplifiedCombatPlanning();
        check(defaults.All(t => !t.AIConfig.InfluencesPositioning), "missing simplify key defaults ON");

        var disabled = ResetPlanningTest(true);
        disabled[0].AIConfig.State.Enabled = false;
        ApplySimplifiedCombatPlanning();
        check(!disabled[0].AIConfig.InfluencesPositioning && disabled[0].AIConfig.State.Writes == 0,
            "an originally false profile is not rewritten or enabled");
        RestoreSimplifiedCombatPlanning();
        check(!disabled[0].AIConfig.InfluencesPositioning && disabled.Skip(1).All(t => t.AIConfig.InfluencesPositioning), "restore preserves original false state");

        var shared = ResetPlanningTest(true);
        var alias = new PlanningTestTemplate { name = "active.alias_profile", AIConfig = new PlanningTestProfile(shared[0].AIConfig.State) };
        PlanningTestLoader.Templates = new[] { alias }.Concat(shared).ToArray();
        ApplySimplifiedCombatPlanning();
        check(!ReferenceEquals(alias.AIConfig, shared[0].AIConfig) && !alias.AIConfig.InfluencesPositioning &&
              !shared[0].AIConfig.InfluencesPositioning,
            "every alias of a shared native profile receives the global simplification");
        check(shared[0].AIConfig.State.Writes == 1, "a shared native profile is only mutated once");

        var fallback = ResetPlanningTest(true);
        PlanningTestLoader.ThrowEnumeration = true;
        ApplySimplifiedCombatPlanning();
        check(fallback.All(t => !t.AIConfig.InfluencesPositioning),
            "failed GetAll enumeration applies the known-ID fallback to all attacks and buffs");

        var partial = ResetPlanningTest(true);
        PlanningTestLoader.ThrowAfterFirst = true;
        ApplySimplifiedCombatPlanning();
        check(partial.All(t => !t.AIConfig.InfluencesPositioning),
            "interrupted GetAll enumeration supplements already-modified skills by known ID");

        var verifyFailure = ResetPlanningTest(true);
        verifyFailure[5].AIConfig.State.ThrowOnDisabledRead = true;
        ApplySimplifiedCombatPlanning();
        check(verifyFailure[5].AIConfig.InfluencesPositioning && verifyFailure[5].AIConfig.State.Writes == 2,
            "failed post-write readback rolls the flag back, not leaves an unowned mutation");

        RestoreSimplifiedCombatPlanning();
    }
}

internal sealed class PlanningTestState
{
    public bool Enabled = true, ThrowOnDisabledRead;
    public int Writes;
}
internal sealed class PlanningTestProfile
{
    public PlanningTestState State;
    public PlanningTestProfile(PlanningTestState state) => State = state;
    public bool InfluencesPositioning
    {
        get => State.ThrowOnDisabledRead && !State.Enabled ? throw new InvalidOperationException("simulated readback failure") : State.Enabled;
        set { State.Enabled = value; State.Writes++; }
    }
}
internal sealed class PlanningTestTemplate
{
    public string name { get; set; }
    public bool IsAttack { get; set; } = true;
    public PlanningTestProfile AIConfig { get; set; }
}
internal static class PlanningTestLoader
{
    public static PlanningTestTemplate[] Templates;
    public static bool ThrowEnumeration;
    public static bool ThrowAfterFirst;
    public static string MissingId;
    public static int Lookups;
    public static IEnumerable<PlanningTestTemplate> GetAll<T>()
    {
        if (ThrowEnumeration) throw new InvalidOperationException("simulated unsupported enumeration");
        for (int i = 0; i < Templates.Length; i++)
        {
            if (ThrowAfterFirst && i == 1) throw new InvalidOperationException("simulated partial enumeration");
            yield return Templates[i];
        }
    }
    public static T Get<T>(string id, bool _)
    {
        Lookups++;
        return (T)(object)(id == MissingId ? null : Templates.FirstOrDefault(t => t.name == id));
    }
}
internal sealed class PlanningTestLog
{
    public void Msg(string _) { }
    public void Warning(string _) { }
}
#nullable disable
using System.Reflection;

namespace LargerMaps;

// Runs the linked production setup/restore code with host/reflection/native-object
// doubles. These are not IL2CPP templates and cannot prove game behavior or CPU cost.
public sealed partial class Plugin
{
    private const string ModName = "Planning test";
    private const int ZoneReferenceSize = 64;
    private static int TargetSize = 80;
    private static readonly PlanningTestLog _log = new();
    private static readonly Dictionary<string, bool> TestSettings = new();
    private static string[] KnownCombatSkillIds => CandidateInfantryFireSkills.Concat(new[] { "active.test_other_attack" }).ToArray();
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

    private static PlanningTestTemplate[] ResetPlanningTest(bool? preserve, bool simplify = true, int size = 80)
    {
        RestoreSimplifiedCombatPlanning();
        _combatPlanningWarned = false;
        TargetSize = size;
        TestSettings.Clear();
        if (preserve.HasValue) TestSettings["PreserveBasicInfantryFirePositioning"] = preserve.Value;
        TestSettings["SimplifyCombatAIPlanning"] = simplify;
        var ids = KnownCombatSkillIds.Concat(KnownSupportSkillIds);
        var templates = ids.Select((id, i) => new PlanningTestTemplate {
            name = id, IsAttack = i < 6, AIConfig = new PlanningTestProfile(new PlanningTestState { Pointer = new IntPtr(i + 1) })
        }).ToArray();
        PlanningTestLoader.Templates = templates;
        PlanningTestLoader.ThrowEnumeration = false;
        PlanningTestLoader.MissingId = null;
        PlanningTestLoader.Lookups = 0;
        return templates;
    }

    public static void RunPlanningChecks(Action<bool, string> check)
    {
        foreach (int size in new[] { 64, 80, 96 })
        foreach (bool simplify in new[] { false, true })
        foreach (bool preserve in new[] { false, true })
        {
            var templates = ResetPlanningTest(preserve, simplify, size);
            ApplySimplifiedCombatPlanning();
            bool active = size > 64 && simplify;
            for (int i = 0; i < templates.Length; i++)
                check(templates[i].AIConfig.InfluencesPositioning == (!active || (preserve && i < 5)),
                    "actual setup: 64-map / simplify / selective toggle matrix");
            check(_preservedInfantryFireProfiles.Count == (active && preserve ? 5 : 0), "five-profile selection is opt-in and large-map-only");
            check(PlanningTestLoader.Lookups == (active && preserve ? 5 : 0), "selection has a five-ID lookup budget");
            int writes = templates.Sum(t => t.AIConfig.State.Writes), lookups = PlanningTestLoader.Lookups;
            ApplySimplifiedCombatPlanning();
            check(templates.Sum(t => t.AIConfig.State.Writes) == writes && PlanningTestLoader.Lookups == lookups,
                "active scene setup is not repeated per callback");
            RestoreSimplifiedCombatPlanning();
            check(templates.All(t => t.AIConfig.InfluencesPositioning), "scene exit restores all owned true-to-false mutations");
            check(_preservedInfantryFireProfiles.Count == 0 && _combatPlanningRestores.Count == 0 && !_combatPlanningActive,
                "scene exit clears identities, restores and active guard");
        }
        var defaults = ResetPlanningTest(null);
        ApplySimplifiedCombatPlanning();
        check(defaults.All(t => !t.AIConfig.InfluencesPositioning), "missing selective key really defaults OFF");

        var disabled = ResetPlanningTest(true);
        disabled[0].AIConfig.State.Enabled = false;
        ApplySimplifiedCombatPlanning();
        check(_preservedInfantryFireProfiles.Count == 4 && disabled[0].AIConfig.State.Writes == 0, "originally disabled candidate is never enabled or owned");
        RestoreSimplifiedCombatPlanning();
        check(!disabled[0].AIConfig.InfluencesPositioning && disabled.Skip(1).All(t => t.AIConfig.InfluencesPositioning), "restore preserves original false state");

        var shared = ResetPlanningTest(true);
        var alias = new PlanningTestTemplate { name = "active.alias_not_whitelisted", AIConfig = new PlanningTestProfile(shared[0].AIConfig.State) };
        PlanningTestLoader.Templates = new[] { alias }.Concat(shared).ToArray();
        ApplySimplifiedCombatPlanning();
        check(!ReferenceEquals(alias.AIConfig, shared[0].AIConfig) && alias.AIConfig.InfluencesPositioning,
            "a shared native pointer in a different wrapper is preserved even if enumerated first");
        check(shared[0].AIConfig.State.Writes == 0 && _preservedInfantryFireProfiles.Count == 5, "preservation never writes a selected/shared profile");

        foreach (string id in CandidateInfantryFireSkills)
        {
            var missing = ResetPlanningTest(true);
            PlanningTestLoader.MissingId = id;
            ApplySimplifiedCombatPlanning();
            check(_preservedInfantryFireProfiles.Count == 4, "missing ID cannot prevent later candidates");
            check(!missing.Single(t => t.name == id).AIConfig.InfluencesPositioning, "unresolved candidate retains baseline simplification");
        }
        for (int failure = 0; failure < 4; failure++)
        {
            var invalid = ResetPlanningTest(true);
            if (failure == 0) invalid[0].IsAttack = false;
            if (failure == 1) invalid[0].IsLineOfFireNeeded = false;
            if (failure == 2) invalid[0].AIConfig.State.Pointer = IntPtr.Zero;
            if (failure == 3) invalid[0].AIConfig = null;
            ApplySimplifiedCombatPlanning();
            check(_preservedInfantryFireProfiles.Count == 4, "nonattack/no-LOF/zero-pointer/null profiles are not preserved");
            check(invalid.Skip(1).Take(4).All(t => t.AIConfig.InfluencesPositioning), "one incompatible candidate does not affect the other four");
        }
        var fallback = ResetPlanningTest(true);
        PlanningTestLoader.ThrowEnumeration = true;
        ApplySimplifiedCombatPlanning();
        check(fallback.Take(5).All(t => t.AIConfig.InfluencesPositioning) && fallback.Skip(5).All(t => !t.AIConfig.InfluencesPositioning),
            "failed GetAll enumeration retains selection through supported ID fallback");

        var verifyFailure = ResetPlanningTest(true);
        verifyFailure[5].AIConfig.State.ThrowOnDisabledRead = true;
        ApplySimplifiedCombatPlanning();
        check(verifyFailure[5].AIConfig.InfluencesPositioning && verifyFailure[5].AIConfig.State.Writes == 2,
            "failed post-write readback rolls the flag back, not leaves an unowned mutation");

        var transition = ResetPlanningTest(true);
        ApplySimplifiedCombatPlanning();
        TestSettings["PreserveBasicInfantryFirePositioning"] = false;
        ApplySimplifiedCombatPlanning();
        check(transition.Take(5).All(t => t.AIConfig.InfluencesPositioning), "restart-required toggle does not churn active scene flags");
        RestoreSimplifiedCombatPlanning();
        ApplySimplifiedCombatPlanning();
        check(transition.All(t => !t.AIConfig.InfluencesPositioning), "next scene with option OFF cannot retain stale selected pointers");
        RestoreSimplifiedCombatPlanning();
    }
}

internal sealed class PlanningTestState
{
    public IntPtr Pointer;
    public bool Enabled = true, ThrowOnDisabledRead;
    public int Writes;
}
internal sealed class PlanningTestProfile
{
    public PlanningTestState State;
    public PlanningTestProfile(PlanningTestState state) => State = state;
    public IntPtr Pointer => State.Pointer;
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
    public bool IsLineOfFireNeeded { get; set; } = true;
    public PlanningTestProfile AIConfig { get; set; }
}
internal static class PlanningTestLoader
{
    public static PlanningTestTemplate[] Templates;
    public static bool ThrowEnumeration;
    public static string MissingId;
    public static int Lookups;
    public static IEnumerable<PlanningTestTemplate> GetAll<T>()
        => ThrowEnumeration ? throw new InvalidOperationException("simulated unsupported enumeration") : Templates;
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
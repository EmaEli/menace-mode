using System.Reflection;
using AIPerformance;
using MenaceModSettings;
using Il2CppMenace.Tools;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.AI;

int checks = 0;
void Check(bool value, string message)
{
    checks++;
    if (!value) throw new Exception(message);
}
void Setting(string key, object value) => ModSettings.Values[("AI Performance", key)] = value;
var logs = new List<string>();
var plugin = new Plugin();
var harmony = new HarmonyLib.Harmony();
ModSettings.Values[("Larger Maps", "TacticalMapSize")] = 64f;
ModSettings.Values[("Larger Maps", "SimplifyCombatAIPlanning")] = false;
plugin.Initialize(logs.Add, logs.Add, logs.Add, harmony);
Check(harmony.Target.Name == "RunWithTimeout", "Correct task hook");
Check(harmony.Target.GetParameters()[1].ParameterType == typeof(Il2CppMenace.Tactical.Actor), "Actor contract");
Check(ModSettings.Values[("AI Performance", "RecoverStalledAdvance")] is true, "Recovery defaults ON independently");
Check(ModSettings.Values[("AI Performance", "DiagnoseAdvanceRecovery")] is false, "Recovery diagnostics default OFF");
Check(ModSettings.Values[("AI Performance", "ConvergeOnLastKnownContacts")] is false, "Experimental convergence defaults OFF");
Check(ModSettings.Values[("AI Performance", "DiagnoseContactConvergence")] is false, "Contact diagnostics default OFF");
Check(ModSettings.Values[("AI Performance", "DiagnoseContactAdvance")] is false, "Contact/advance diagnostics default OFF");
Check(ModSettings.Values[("AI Performance", "OffensiveCombatPlanning")] is true, "Bounded offensive planning defaults ON");
Check(ModSettings.Values[("AI Performance", "CombinedArmsSupport")] is false, "Combined-arms support defaults OFF pending gameplay trial");
Check(ModSettings.Values[("AI Performance", "DiagnoseCombinedArmsSupport")] is false, "Combined-arms diagnostics default OFF");
var diagnosticBudget = new CombinedArmsDiagnosticBudget();
Check(!diagnosticBudget.TryAcquire(0, 1, 1, "plan"), "Combined-arms diagnostics reject invalid rounds");
Check(diagnosticBudget.TryAcquire(1, 1, 10, "plan"), "First diagnostic record accepted");
Check(!diagnosticBudget.TryAcquire(1, 1, 10, "plan"), "Repeated actor/round/phase diagnostic is deduplicated");
Check(diagnosticBudget.TryAcquire(1, 1, 10, "choice"), "Distinct diagnostic phases are retained");
var boundedDiagnostics = new CombinedArmsDiagnosticBudget();
for (int i = 0; i < CombinedArmsDiagnosticBudget.MaxLines; i++)
    Check(boundedDiagnostics.TryAcquire(i + 1, 1, i + 1, "plan"), "Diagnostic budget accepts records up to its cap");
Check(!boundedDiagnostics.TryAcquire(CombinedArmsDiagnosticBudget.MaxLines + 1, 1, 999, "plan"), "Diagnostic budget stops at its mission cap");
boundedDiagnostics.Reset();
Check(boundedDiagnostics.TryAcquire(1, 1, 999, "plan"), "Mission reset reopens the diagnostic budget");
var supportState = new CombinedArmsState();
var supportOrder = new AdvanceRecoveryOrder(30, 30, 4, 4, 1f);
var supportUnits = new List<SupportUnit>
{
    new(100, 1000, SupportKind.Vehicle, 20, 20, supportOrder, true, 30, 30),
    new(101, 1001, SupportKind.Vehicle, 21, 20, supportOrder, true, 30, 30)
};
for (int i = 0; i < 6; i++)
    supportUnits.Add(new SupportUnit(200 + i, 2000 + i, SupportKind.Infantry, 22 + i, 20,
        supportOrder, true, 30, 30));
supportUnits.Add(new SupportUnit(300, 3000, SupportKind.Infantry, 28, 20,
    new AdvanceRecoveryOrder(40, 40, 4, 4, 1f), true, 40, 40));
supportUnits.Add(new SupportUnit(301, 3001, SupportKind.Infantry, 29, 20, default, false, 29, 20));
Check(supportState.Publish(1, 1, 5, supportUnits), "Combined-arms diagnostics fixture published");
var firstAsset = supportUnits[0]; var secondAsset = supportUnits[1];
var candidateSet = supportState.Candidates(1, 1, 5, firstAsset, true, out var candidateCounts);
Check(candidateCounts.Gate == SupportCandidateGate.Ready && candidateCounts.Nearby == 8,
    "Candidate diagnostics count nearby support requests");
Check(candidateCounts.Compatible == 7 && candidateCounts.Returned == 4 && candidateCounts.Truncated == 3,
    $"Candidate diagnostics separate compatibility from the four-request cap ({candidateCounts.Compatible}/{candidateCounts.Returned}/{candidateCounts.Truncated})");
Check(supportState.Claim(1, 1, 5, firstAsset, candidateSet[0]), "Combined-arms fixture claims a vehicle slot");
var secondCandidates = supportState.Candidates(1, 1, 5, secondAsset, true, out var secondCounts);
Check(secondCounts.SlotBusy == 1 && secondCounts.Returned == 4 && secondCounts.Truncated == 2,
    "Candidate diagnostics report occupied kind-specific slots");
supportState.Candidates(2, 1, 5, secondAsset, true, out var mapMismatch);
Check(mapMismatch.Gate == SupportCandidateGate.MapMismatch, "Candidate diagnostics identify a stale map");
supportState.Candidates(1, 2, 5, secondAsset, true, out var roundMismatch);
Check(roundMismatch.Gate == SupportCandidateGate.RoundMismatch, "Candidate diagnostics identify a stale round");
var action = new Il2CppSystem.Action();
object[] taskArgs = { action, null };
Check(!(bool)harmony.Prefix.Invoke(null, taskArgs), "Skip detached wrapper");
Check(ReferenceEquals(taskArgs[1], Il2CppSystem.Threading.Tasks.Task.Worker), "Return actual worker");
Check(ReferenceEquals(action, Il2CppSystem.Threading.Tasks.Task.Scheduled), "Schedule original action");
Check((bool)harmony.Prefix.Invoke(null, new object[] { null, null }), "Null action retains native behavior");

SkillTemplate Add(string id, bool attack = true, long pointer = 1)
{
    var skill = new SkillTemplate { name = id, IsAttack = attack, AIConfig = new Profile { Pointer = new IntPtr(pointer) } };
    DataTemplateLoader.Templates[id] = skill;
    return skill;
}
var attack = Add("active.aimed_shot");
var buff = Add("active.rogue_officer_whistle", false, 2);
var utility = Add("active.unrelated_noncombat", false, 3);
plugin.OnSceneLoaded(0, "Tactical");
Check(!attack.AIConfig.InfluencesPositioning, "64x64 optimization applies despite old Larger Maps OFF");
Check(attack.m_ID == attack.name, "Scene setup primes native-style cached skill ID for worker reads");
Check(!buff.AIConfig.InfluencesPositioning, "Known buff optimized");
Check(utility.AIConfig.InfluencesPositioning, "Unrelated utility preserved");
Check(logs.Any(l => l.Contains("2 attack") || l.Contains("1 attack / 1 buff")), "Successful count");
int setupLogs = logs.Count;
plugin.OnSceneLoaded(0, "Tactical");
Check(logs.Count == setupLogs, "Repeated callback is idempotent");
plugin.OnSceneLoaded(0, "MissionPreparation");
Check(attack.AIConfig.InfluencesPositioning && buff.AIConfig.InfluencesPositioning, "Preparation restores flags");

Setting("SimplifyCombatAIPlanning", false);
plugin.OnSceneLoaded(0, "Tactical");
Check(attack.AIConfig.InfluencesPositioning, "Explicit companion OFF respected");
plugin.OnUnload();
Setting("SimplifyCombatAIPlanning", true);
ModSettings.Values[("Larger Maps", "TacticalMapSize")] = 42;
plugin.OnSceneLoaded(0, "Tactical");
Check(!attack.AIConfig.InfluencesPositioning, "Stale Larger Maps size cannot disable independent AI planning");
plugin.OnUnload();
ModSettings.Values.Remove(("Larger Maps", "TacticalMapSize"));
ModSettings.Values[("Larger Maps", "TacticalMapSize")] = 96;
DataTemplateLoader.EmptyCollection = true;
plugin.OnSceneLoaded(0, "Tactical");
Check(!attack.AIConfig.InfluencesPositioning, "96 and broken collection ID fallback");
plugin.OnSceneLoaded(0, "Strategy");
Check(attack.AIConfig.InfluencesPositioning, "Strategy restores flags");
ModSettings.Values.Remove(("Larger Maps", "TacticalMapSize"));
ModSettings.Values.Remove(("Larger Maps", "SimplifyCombatAIPlanning"));

DataTemplateLoader.EmptyCollection = false;
var infantry = Add("active.fire_pirate_assault_rifle", true, 20);
var alien = Add("active.alien_stab_attack", true, 21);
plugin.OnSceneLoaded(0, "Tactical");
Check(!infantry.AIConfig.InfluencesPositioning && !alien.AIConfig.InfluencesPositioning,
    "attack skills used by different factions receive the same global simplification");
Check(!attack.AIConfig.InfluencesPositioning, "AI works with no Larger Maps settings registered");
plugin.OnUnload();
Check(infantry.AIConfig.InfluencesPositioning && alien.AIConfig.InfluencesPositioning,
    "scene exit restores every faction's original skill flags");
attack.AIConfig.IgnoreWrites = true;
plugin.OnSceneLoaded(0, "Tactical");
Check(attack.AIConfig.InfluencesPositioning, "Ignored setter does not count as changed");
plugin.OnUnload();
attack.AIConfig.IgnoreWrites = false;

Setting("UseLocalTacticalRouting", true);
ModSettings.Values[("Larger Maps", "TacticalMapSize")] = 64;
plugin.OnSceneLoaded(0, "Tactical");
Check(AIConfig.WEIGHTS.NearTileLimit == 20 && AIConfig.WEIGHTS.CullTilesDistances == 12, "64x64 candidate limits");
plugin.OnUnload();
Check(AIConfig.WEIGHTS.NearTileLimit == 64 && AIConfig.WEIGHTS.CullTilesDistances == 20, "Routing restored exactly");
AIConfig.WEIGHTS.NearTileLimit = 12;
AIConfig.WEIGHTS.CullTilesDistances = 10;
plugin.OnSceneLoaded(0, "Tactical");
Check(AIConfig.WEIGHTS.NearTileLimit == 12 && AIConfig.WEIGHTS.CullTilesDistances == 10, "Respect tighter limits");
plugin.OnUnload();
AIConfig.WEIGHTS.NearTileLimit = 64;
AIConfig.WEIGHTS.CullTilesDistances = 20;
AIConfig.WEIGHTS.RejectCull = true;
plugin.OnSceneLoaded(0, "Tactical");
Check(AIConfig.WEIGHTS.NearTileLimit == 64 && AIConfig.WEIGHTS.CullTilesDistances == 20, "Partial routing failure rolls back");
plugin.OnUnload();
AIConfig.WEIGHTS.RejectCull = false;
Setting("LocalNearTileLimit", float.NaN);
plugin.OnSceneLoaded(0, "Tactical");
Check(AIConfig.WEIGHTS.NearTileLimit == 64 && AIConfig.WEIGHTS.CullTilesDistances == 20, "Invalid routing settings rejected");
plugin.OnUnload();
Console.WriteLine($"AI performance: {checks} production behavior checks passed (managed fixtures, not IL2CPP).");
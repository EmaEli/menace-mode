using System.Reflection;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace HarmonyLib
{
    public sealed class HarmonyMethod
    {
        public HarmonyMethod(Type type, string method) { }
    }
    public sealed class Harmony
    {
        public void Patch(MethodInfo target, HarmonyMethod postfix = null, HarmonyMethod prefix = null) { }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] parameters = null)
            => parameters == null ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) :
                type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, parameters);
    }
}
namespace AIPerformance
{
    public sealed partial class Plugin
    {
        private const string ModName = "AI Performance";
        private const int ZoneReferenceSize = 64;
        private static readonly FixtureLog _log = new();
        private static bool ReadBool(string key, bool fallback) => fallback;
        private static bool _combatPlanningActive = true;
        private static readonly HashSet<string> _simplifiedAttackIds = new() { "active.fixture" };
        internal static void BeginCombinedFixture(AIFaction faction)
        {
            BeginFixture(); BeginOffensiveFixture(); _combinedArms.Reset(); _supportThreats.Reset(); _combinedArmsEnabled = true;
            _combinedArmsThread = Thread.CurrentThread.ManagedThreadId;
            CombinedArmsTurn(faction);
        }
        internal static void CombinedRefreshFixture(Agent agent) => CombinedArmsRefresh(agent);
        internal static void CombinedDetectedFixture(Actor actor, int faction) => CombinedArmsDetected(actor, faction);
        internal static bool CombinedEnabled => _combinedArmsEnabled;
        internal static void BindSmokeFixture(Func<Tile, bool> cover) => _supportSmokeCover = cover;
        internal static float ThreatValueFixture(Il2CppMenace.Tactical.AI.Behaviors.InflictDamage behavior, Tile target, float value, bool immediate = true)
        { CombinedArmsThreatValue(behavior, immediate, target, ref value); return value; }
        private static Type FindType(string name) => null;
        internal static void BeginOffensiveFixture()
        {
            _offensiveBudget.Reset(); _offensivePlanningEnabled = true; _missionCivilianPriorityEnabled = true;
            _combinedArmsEnabled = false; _combinedArms.Reset();
            _contactAdvance.Reset(); _contactAdvanceEnabled = false;
        }
        internal static void BeginAdvanceFixture(AIFaction faction)
        {
            BeginOffensiveFixture(); _contactAdvanceEnabled = true; _contactAdvanceThread = Environment.CurrentManagedThreadId;
            TacticalManager.Instance.Faction = faction; ContactAdvanceTurn(faction);
        }
        internal static void AdvanceSightFixture(Actor actor, int faction) => ContactAdvanceDetected(actor, faction);
        internal static void AdvanceBeforeFixture(Agent agent) => ContactAdvanceBefore(agent);
        internal static void AdvanceResetFixture() => OffensivePlanningReset();
        internal static bool ContactAdvanceOn => _contactAdvanceEnabled;
        internal static void OffensiveScoresFixture(Agent agent) => OffensivePlanningScores(agent);
        internal static Behavior OffensiveChoiceFixture(Agent agent, Behavior choice)
        { OffensivePlanningChoice(agent, ref choice); return choice; }
        internal static bool OffensiveEnabled => _offensivePlanningEnabled;
        private sealed class FixtureLog
        {
            public void Msg(string message)
            {
                if (RejectLog) throw new InvalidOperationException("diagnostic sink failure");
                Logs.Add(message);
            }
            public void Warning(string message) => Logs.Add(message);
        }
        internal static bool RejectLog;
        internal static readonly List<string> Logs = new();
        internal static bool RecoveryEnabled => _advanceRecoveryEnabled;
        internal static void BeginFixture(bool diagnostics = false)
        {
            AdvanceRecoveryReset();
            _advanceRecoveryEnabled = true;
            _advanceRecoveryDiagnostics = diagnostics;
            RejectLog = false;
            Logs.Clear();
        }
        internal static void ResetFixtureMission() => AdvanceRecoveryReset();
        internal static void ScoreFixture(Actor actor, TileScores tiles) => AdvanceRecoveryScores(actor, tiles);
        internal static void UtilityFixture(Il2CppMenace.Tactical.AI.Behaviors.Move move, TileScores tiles)
            => AdvanceRecoveryUtility(move, tiles);
        internal static Behavior ChoiceFixture(Agent agent, Behavior choice)
        {
            AdvanceRecoveryChoice(agent, ref choice);
            return choice;
        }
        internal static void BeginContactFixture(bool diagnostics = false)
        {
            ContactConvergenceReset();
            _contactConvergenceEnabled = true;
            _contactConvergenceDiagnostics = diagnostics;
            RejectLog = false;
            Logs.Clear();
        }
        internal static void ContactSightFixture(AIFaction faction, Actor actor)
        {
            TacticalManager.Instance.Faction = faction;
            ContactConvergenceDetected(actor, (int)faction.GetFactionType());
        }
        internal static void ContactUtilityFixture(Il2CppMenace.Tactical.AI.Behaviors.Move move, TileScores tiles)
            => ContactConvergenceUtility(move, tiles);
        internal static void ResetContactFixture() => ContactConvergenceReset();
        internal static bool ContactEnabled => _contactConvergenceEnabled;
        internal static void BeginCivilianFixture() => _missionCivilianPriorityEnabled = true;
        internal static bool CivilianPriorityEnabled => _missionCivilianPriorityEnabled;
        internal static float CivilianPriorityFixture(Il2CppMenace.Tactical.AI.Behaviors.InflictDamage behavior,
            bool immediate, Tile tile, Tile aimedAt, float score)
        {
            MissionCivilianPriorityValue(behavior, immediate, tile, aimedAt, ref score);
            return score;
        }
    }
}
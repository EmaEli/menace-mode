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
        public void Patch(MethodInfo target, HarmonyMethod postfix) { }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] parameters)
            => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, parameters);
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
        internal static Behavior ChoiceFixture(Agent agent, Behavior choice)
        {
            AdvanceRecoveryChoice(agent, ref choice);
            return choice;
        }
    }
}
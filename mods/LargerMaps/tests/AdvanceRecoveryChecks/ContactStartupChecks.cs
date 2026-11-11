using AIPerformance;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors.Criterions;

internal static class ContactStartupChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var previous = TacticalManager.Instance;
        int reads = TacticalManager.GetCalls;
        TacticalManager.Instance = null; // Startup is before any tactical scene exists.
        try
        {
            var disabled = new Harmony();
            Plugin.ContactSetupFixture(disabled, false);
            check(!Plugin.ContactAdvanceOn && disabled.Calls.Count == 0, "disabled contact startup installs nothing");
            var enabled = new Harmony();
            Plugin.ContactSetupFixture(enabled, true);
            check(Plugin.ContactAdvanceOn, "production contact setup completes before tactical scene");
            check(TacticalManager.GetCalls == reads, "contact startup never reads tactical/native state");
            check(Plugin.ContactCountFixture() == 0, "managed warmup leaves no fictional contacts");
            check(enabled.Calls.Count == 7, "all seven real contact/zone targets registered");
            foreach (var (owner, target, prefix, postfix) in new[] {
                (typeof(AIFaction), "OnTurnStart", (string)null, "ContactAdvanceTurn"),
                (typeof(Agent), "OnBeforeProcessing", "ContactAdvanceBefore", (string)null),
                (typeof(Actor), "SetDetectedByFaction", (string)null, "ContactAdvanceDetected"),
                (typeof(AIFaction), "OnOpponentSighted", (string)null, "ContactAdvanceSighted"),
                (typeof(ConsiderZones), "Evaluate", "ContactAdvanceZoneEvaluateEnter", "ContactAdvanceZoneEvaluateExit"),
                (typeof(ConsiderZones), "PostProcess", "ContactAdvanceZonePostEnter", "ContactAdvanceZonePostDone"),
                (typeof(AIFaction), "HasKnownOpponent", "ContactAdvanceKnownOpponent", (string)null) })
            {
                var call = enabled.Calls.Single(c => c.Target.DeclaringType == owner && c.Target.Name == target);
                check(call.Prefix?.Name == prefix && call.Postfix?.Name == postfix, "startup keeps intended hook phase: " + target);
                foreach (var callback in new[] { call.Prefix, call.Postfix, call.Finalizer }.Where(c => c != null))
                    check(callback.Owner == typeof(Plugin) && callback.Owner.GetMethod(callback.Name, System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static) != null, "registered callback exists: " + callback.Name);
                if (owner == typeof(ConsiderZones))
                    check(call.Finalizer?.Name == "ContactAdvanceZoneFinally" && call.Prefix.priority == Priority.First &&
                        call.Postfix.priority == Priority.First, "zone cleanup/priority retained at startup");
            }
        }
        finally { TacticalManager.Instance = previous; }
    }
}
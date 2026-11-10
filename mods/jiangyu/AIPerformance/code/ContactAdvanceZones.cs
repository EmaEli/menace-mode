using System;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors.Criterions;
using Il2CppMenace.Tactical.AI.Data;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

public sealed partial class Plugin
{
    // Reviewed native ConsiderZones.Evaluate/PostProcess skip AdvanceTowards zones for the whole
    // faction once AIFaction.HasKnownOpponent() is true. The override is scoped to those two
    // calls, on the AI thread, for eligible units without a useful native action.
    [ThreadStatic] private static bool _advanceZoneScope;
    private static long _advanceZoneActor;
    private static bool _advanceZoneEligible;

    private static void PatchContactAdvanceZones(Harmony harmony)
    {
        var evaluate = AccessTools.Method(typeof(ConsiderZones), "Evaluate", new[] { typeof(Actor), typeof(TileScore) });
        var post = AccessTools.Method(typeof(ConsiderZones), "PostProcess", new[] { typeof(Actor), typeof(TileScores) });
        var known = AccessTools.Method(typeof(AIFaction), "HasKnownOpponent", Type.EmptyTypes);
        if (evaluate == null || post == null || known == null) throw new MissingMethodException("contact advance zones");
        // Exit before the advance recovery postfix so its own HasKnownOpponent read stays native.
        var enter = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneEnter)) { priority = Priority.First };
        var exit = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneExit)) { priority = Priority.First };
        var done = new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceZoneDone)) { priority = Priority.First };
        harmony.Patch(evaluate, prefix: enter, postfix: exit);
        harmony.Patch(post, prefix: enter, postfix: done);
        harmony.Patch(known, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceKnownOpponent)));
    }

    private static void ContactAdvanceZoneEnter(Actor __0, out bool __state)
    {
        __state = _advanceZoneScope;
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || __0 == null ||
            Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            long id = __0.Pointer.ToInt64();
            if (_advanceZoneActor != id)
            {
                _advanceZoneEligible = ContactAdvanceRuntime.KeepAdvanceZones(__0.GetAgent());
                _advanceZoneActor = id;
            }
            _advanceZoneScope = _advanceZoneEligible;
        }
        catch (Exception ex) { _advanceZoneScope = false; ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceZoneExit(bool __state) => _advanceZoneScope = __state;

    // One eligibility read per Collect/Evaluate/PostProcess pass; later passes re-read.
    private static void ContactAdvanceZoneDone(bool __state) { _advanceZoneScope = __state; _advanceZoneActor = 0; }

    private static bool ContactAdvanceKnownOpponent(ref bool __result)
    {
        if (!_advanceZoneScope || !_contactAdvanceEnabled) return true;
        __result = false;
        return false;
    }

    private static void ContactAdvanceZoneReset() { _advanceZoneScope = false; _advanceZoneActor = 0; _advanceZoneEligible = false; }
}

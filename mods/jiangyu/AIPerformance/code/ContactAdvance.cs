using System;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly ContactAdvanceState _contactAdvance = new();
    private static readonly ContactAdvanceDiagnosticBudget _contactAdvanceDiagnosticBudget = new();
    private static volatile bool _contactAdvanceEnabled;
    private static volatile bool _contactAdvanceDiagnostics;
    private static int _contactAdvanceThread;
    private static int _contactAdvanceRound;

    private static void PatchContactAdvance(Harmony harmony)
    {
        if (!_offensivePlanningEnabled) return;
        _contactAdvanceThread = Environment.CurrentManagedThreadId;
        // The existing explicit combined-arms trace opt-in includes its contact/crossing path.
        _contactAdvanceDiagnostics = ReadBool("DiagnoseContactAdvance", false) || ReadBool("DiagnoseCombinedArmsSupport", false);
        _contactAdvanceDiagnosticBudget.Reset();
        _contactAdvanceRound = 0;
        // #100: PrepareMethod's speculative sweep crashed inside the loader's native
        // JIT detour before the menu. Keep only bounded, native-free state warmup;
        // let the runtime compile gameplay helpers normally when they are needed.
        _contactAdvance.Observe(1, 1, 1, new(2, 30, 30, 1, 20));
        _contactAdvance.TryAim(1, 1, 1, 0, 0, out _); _contactAdvance.TryAssign(1, 1, 1, 10, 0, 0, out _);
        _contactAdvance.TryRead(1, 1, 1, 2, out _); _contactAdvance.BeginTurn(1, 1, 1); _contactAdvance.Reset();
        _contactAdvanceDiagnosticBudget.TryAcquire(1, 1, 1, "warmup"); _contactAdvanceDiagnosticBudget.Reset();
        var turn = AccessTools.Method(typeof(AIFaction), "OnTurnStart", Type.EmptyTypes);
        var before = AccessTools.Method(typeof(Agent), "OnBeforeProcessing", Type.EmptyTypes);
        var detection = AccessTools.Method(typeof(Actor), "SetDetectedByFaction", new[] { typeof(int) });
        var sighting = AccessTools.Method(typeof(AIFaction), "OnOpponentSighted", new[] { typeof(Actor), typeof(int) });
        if (turn == null || before == null || detection == null || sighting == null) throw new MissingMethodException("contact advance lifecycle");
        // Native OnTurnStart rebuilds visibility; refresh only after that work, not before it.
        harmony.Patch(turn, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceTurn)));
        harmony.Patch(before, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceBefore)));
        harmony.Patch(detection, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceDetected)));
        // Map.UpdateVisibility -> Actor.OnDiscovered inlines the detection bit write.
        // Hooking only SetDetectedByFaction misses that normal native sighting path.
        harmony.Patch(sighting, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactAdvanceSighted)));
        PatchContactAdvanceZones(harmony);
        _contactAdvanceEnabled = true;
        if (_contactAdvanceDiagnostics)
            _log.Msg($"{ModName}: bounded contact/group diagnostics enabled (contact or combined-arms opt-in).");
    }

    private static void ContactAdvanceTurn(AIFaction __instance)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            ContactAdvanceZoneReset();
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null || !ContactAdvanceRuntime.HostileFaction(__instance)) return;
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            if (!ContactAdvanceRuntime.Refresh(__instance, _contactAdvance, map, manager.m_Round)) return;
            long factionId = __instance.Pointer.ToInt64();
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(manager.m_Round, factionId, 0, "turn",
                    $"result=refreshed contacts={_contactAdvance.Count(map.Pointer.ToInt64(), manager.m_Round, factionId)} thread={Environment.CurrentManagedThreadId}");
            var actors = __instance.GetActors();
            if (actors == null || actors.Count > 256)
            {
                if (_contactAdvanceDiagnostics)
                    ContactAdvanceDiagnostic(manager.m_Round, factionId, 0, "turn-alert", $"result=actor-limit actors={actors?.Count ?? -1} limit=256");
                return;
            }
            int assigned = 0, awakened = 0;
            foreach (var actor in actors)
            {
                var agent = actor?.GetAgent(); bool sleeping = agent?.IsSleeping() == true;
                if (!ContactAdvanceRuntime.Alert(agent, _contactAdvance, map, manager.m_Round)) continue;
                assigned++; if (sleeping) awakened++;
            }
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(manager.m_Round, factionId, 0, "turn-alert",
                    $"result=assigned factionType={(int)__instance.GetFactionType()} actors={actors.Count} assigned={assigned} awakened={awakened}");
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceBefore(Agent __instance)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            ContactAdvanceZoneReset();
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null || __instance == null) return;
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            ContactAdvanceRuntime.Refresh(__instance.GetFaction(), _contactAdvance, map, manager.m_Round);
            ContactAdvanceRuntime.Alert(__instance, _contactAdvance, map, manager.m_Round);
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceDetected(Actor __instance, int __0)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || __0 < 5 || __0 > 9) return;
        if (Environment.CurrentManagedThreadId != _contactAdvanceThread)
        {
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(Volatile.Read(ref _contactAdvanceRound), __0, 0, "detection-thread",
                    $"result=thread-mismatch actor={__instance?.Pointer.ToInt64() ?? 0:X} thread={Environment.CurrentManagedThreadId} expected={_contactAdvanceThread}");
            return;
        }
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null) return;
            if (_contactAdvanceDiagnostics) Volatile.Write(ref _contactAdvanceRound, manager.m_Round);
            var faction = manager.GetFaction(__0)?.TryCast<AIFaction>();
            var result = ContactAdvanceRuntime.Observe(faction, __instance, _contactAdvance, map, manager.m_Round);
            if (_contactAdvanceDiagnostics)
                ContactAdvanceDiagnostic(manager.m_Round, faction?.Pointer.ToInt64() ?? __0,
                    __instance?.Pointer.ToInt64() ?? 0, "detection", $"result={result} factionType={__0}");
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    private static void ContactAdvanceSighted(AIFaction __instance, Actor __0)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled || Environment.CurrentManagedThreadId != _contactAdvanceThread) return;
        try
        {
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null) return;
            var result = ContactAdvanceRuntime.Observe(__instance, __0, _contactAdvance, map, manager.m_Round);
            // Sample once per faction/round; repeated visibility updates must not
            // allocate strings by default or spend the entire mission's hook log budget.
            if (_contactAdvanceDiagnostics && result != ContactAdvanceObservation.IneligibleFaction && result != ContactAdvanceObservation.Unchanged)
                ContactAdvanceDiagnostic(manager.m_Round, __instance?.Pointer.ToInt64() ?? 0, 0,
                    "native-sighting", $"result={result} target={__0?.Pointer.ToInt64() ?? 0:X}");
        }
        catch (Exception ex) { ContactAdvanceDisable(ex); }
    }

    internal static void ContactAdvanceDiagnostic(int round, long faction, long actor, string phase, string details)
    {
        try
        {
            if (_contactAdvanceDiagnostics && _contactAdvanceDiagnosticBudget.TryAcquire(round, faction, actor, phase))
                _log.Msg($"{ModName}: CONTACT-ADVANCE round={round} faction={faction:X} actor={actor:X} phase={phase} {details}");
        }
        catch { } // Optional diagnostics must never disable or alter native AI.
    }

    internal static void ContactAdvanceDiagnosticReset()
    {
        _contactAdvanceDiagnosticBudget.Reset();
        Volatile.Write(ref _contactAdvanceRound, 0);
    }

    private static void ContactAdvanceDisable(Exception ex)
    {
        _contactAdvanceEnabled = false; _contactAdvance.Reset(); ContactAdvanceZoneReset();
        _log.Warning($"{ModName}: contact advance disabled ({ex.GetType().Name}); native AI retained.");
    }
}
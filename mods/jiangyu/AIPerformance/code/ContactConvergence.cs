using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using TileScores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly ContactMemoryState _contactMemory = new();
    private static volatile bool _contactConvergenceEnabled;
    private static bool _contactConvergenceDiagnostics;
    private static int _contactConvergenceLogCount;

    private static void PatchContactConvergence(Harmony harmony)
    {
        if (!ReadBool("ConvergeOnLastKnownContacts", false)) return;
        try
        {
            // New integration reviewed against 0.7.18 only; do not infer 0.7.17 compatibility.
            if (!ContactConvergenceBuild.IsCurrentSupported())
                throw new NotSupportedException("contact convergence requires the reviewed 0.7.18 binary");
            var sight = AccessTools.Method(typeof(Actor), "SetDetectedByFaction", new[] { typeof(int) });
            var utility = AccessTools.Method(typeof(Move), "HasUtility", new[] { typeof(TileScores) });
            var reset = AccessTools.Method(typeof(TacticalManager), "OnInit");
            if (sight == null || utility == null || reset == null) throw new MissingMethodException("contact convergence hooks");
            foreach (Type type in new[] { typeof(ContactConvergenceRuntime), typeof(ContactMemoryState), typeof(ContactEstimate), typeof(Plugin) })
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (type != typeof(Plugin) || method.Name.StartsWith("ContactConvergence", StringComparison.Ordinal))
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
            // Exercise bounded managed dictionaries before any IL2CPP worker can enter them (#44).
            _contactMemory.Observe(1, 1, 1, 1, 20, 20);
            _contactMemory.TryRespond(1, 1, 1, 2, 3, 0, 0, out _);
            ContactConvergenceReset();
            harmony.Patch(sight, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactConvergenceDetected)));
            harmony.Patch(utility, prefix: new HarmonyMethod(typeof(Plugin), nameof(ContactConvergenceUtility)));
            harmony.Patch(reset, postfix: new HarmonyMethod(typeof(Plugin), nameof(ContactConvergenceReset)));
            _contactConvergenceDiagnostics = ReadBool("DiagnoseContactConvergence", false);
            _contactConvergenceEnabled = true;
            _log.Msg($"{ModName}: experimental contact convergence enabled; unassigned pirate/Rogue infantry only, at most three responders per faction.");
        }
        catch (Exception ex) { ContactConvergenceDisable(ex); }
    }

    private static void ContactConvergenceReset()
    {
        _contactMemory.Reset();
        Interlocked.Exchange(ref _contactConvergenceLogCount, 0);
    }

    private static void ContactConvergenceDetected(Actor __instance, int __0)
    {
        if (!_contactConvergenceEnabled || (__0 != 6 && __0 != 9)) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            // Capture only a new native detection event. Never poll an Actor/TTL list for coordinates.
            var faction = manager?.GetFaction(__0)?.TryCast<AIFaction>();
            if (map != null) ContactConvergenceRuntime.Observe(faction, __instance, _contactMemory, map.Pointer.ToInt64(), manager.m_Round);
        }
        catch (Exception ex) { ContactConvergenceDisable(ex); }
    }

    private static void ContactConvergenceUtility(Move __instance, TileScores __0)
    {
        if (!_contactConvergenceEnabled) return;
        try
        {
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            if (map != null && ContactConvergenceRuntime.TrySeed(__instance, __0, _contactMemory, map.Pointer.ToInt64(), manager.m_Round, out int seeded) &&
                _contactConvergenceDiagnostics)
                ContactConvergenceLog($"CONTACT-APPROACH round={manager.m_Round} seeded={seeded}");
        }
        catch (Exception ex) { ContactConvergenceDisable(ex); }
    }

    private static void ContactConvergenceLog(string message)
    {
        try { if (Interlocked.Increment(ref _contactConvergenceLogCount) <= 50) _log.Msg($"{ModName}: {message}"); }
        catch { } // Optional diagnostics must not disable behavior.
    }

    private static void ContactConvergenceDisable(Exception ex)
    {
        _contactConvergenceEnabled = false;
        ContactConvergenceReset();
        _log.Warning($"{ModName}: contact convergence disabled ({ex.GetType().Name}); native AI retained.");
    }
}
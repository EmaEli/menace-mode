using System;
using System.Reflection;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static Func<Tile, bool> _supportSmokeCover;

    internal static bool SupportSmokeCover(Tile tile)
    {
        try { return _supportSmokeCover?.Invoke(tile) ?? false; }
        catch { return false; }
    }

    private static void BindSupportSmoke()
    {
        _supportSmokeCover = null;
        try
        {
            var method = FindType("SmokeOverhaul.Plugin")?.GetMethod("HasSupportSmokeCover", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                _supportSmokeCover = (Func<Tile, bool>)Delegate.CreateDelegate(typeof(Func<Tile, bool>), method);
            }
        }
        catch { } // Optional companion; no smoke means no smoke-protected advance.
    }

    // Queried only by Smoke Overhaul's existing main-thread Agent.Execute hook.
    // This is a request, not a grenade grant or a second skill-execution owner.
    public static int[] GetSupportSmokeThreat(Agent agent)
    {
        if (!_combinedArmsEnabled || Environment.CurrentManagedThreadId != _combinedArmsThread) return null;
        try
        {
            if (CombinedArmsRuntime.Snapshot(agent, false) is not { Kind: SupportKind.Infantry } ||
                CombinedArmsResponseRuntime.HasUsefulAction(agent)) return null;
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (CombinedArmsResponseRuntime.Threat(agent, _supportThreats, map, manager?.m_Round ?? 0) is not { } threat) return null;
            var current = agent.GetActor().GetTile(); var origin = map.GetTile(threat.X, threat.Z);
            if (CombinedArmsRuntime.Distance2(current, threat.X, threat.Z) > threat.Range * threat.Range ||
                !origin.HasLineOfSightTo(current, Il2CppTactical.LineOfSightFlags.Default)) return null;
            return new[] { threat.X, threat.Z };
        }
        catch { return null; }
    }

    private static void CombinedArmsThreatValue(Il2CppMenace.Tactical.AI.Behaviors.InflictDamage __instance,
        bool __0, Tile __2, ref float __result)
    {
        // Boost only a positive native immediate-use valuation. Native collection,
        // spotting, setup, blast/friendly-fire scoring and execution remain the owners.
        if (!_combinedArmsEnabled || !__0 || __2 == null || !float.IsFinite(__result) || __result <= 0) return;
        try
        {
            var agent = __instance.GetAgent(); var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (!CombinedArmsRuntime.Ready(agent) || map == null) return;
            var actor = agent.GetActor();
            if (actor.IsVehicle() ? !CombinedArmsRuntime.CrewedVehicle(actor.GetTemplate()?.m_ID) :
                !actor.IsInfantry() || actor.HasTag(Il2CppMenace.Tags.TagType.MACHINE)) return;
            int x = __2.GetX(), z = __2.GetZ();
            foreach (var threat in _supportThreats.Get(map.Pointer.ToInt64(), manager.m_Round, agent.GetFaction().Pointer.ToInt64()))
                if (threat.X == x && threat.Z == z)
                {
                    foreach (var target in SupportProjection.DetectedTargets(agent.GetFaction()))
                        if (target.Pointer == __2.Pointer) { float value = __result * 2; if (float.IsFinite(value)) __result = value; return; }
                }
        }
        catch { } // Never invalidate a native attack because of an optional priority bonus.
    }
}
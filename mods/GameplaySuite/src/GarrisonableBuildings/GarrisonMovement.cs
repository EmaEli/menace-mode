#nullable enable
using System;
using System.Collections.Concurrent;
using HarmonyLib;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppTactical;

namespace GarrisonableBuildings;

public sealed partial class Plugin
{
    private static readonly ConcurrentDictionary<IntPtr, byte> GarrisonMovementActors = new();

    private sealed class GarrisonMovementState
    {
        public Actor Actor = null!;
        public EntityProperties? Properties;
        public bool Preview, HadStanceRoot;
    }

    private static void InstallGarrisonMovementHooks(HarmonyLib.Harmony harmony)
    {
        var query = new HarmonyMethod(typeof(GarrisonMovementHooks), nameof(GarrisonMovementHooks.Preview));
        var finish = new HarmonyMethod(typeof(GarrisonMovementHooks), nameof(GarrisonMovementHooks.Finished));
        var possible = AccessTools.Method(typeof(MovementSystem), nameof(MovementSystem.IsMovementPossible),
            new[] { typeof(Actor), typeof(Tile), typeof(bool), typeof(MovementFailedReason).MakeByRefType() });
        var path = AccessTools.Method(typeof(MovementSystem), nameof(MovementSystem.GetPath),
            new[] { typeof(Actor), typeof(Tile), typeof(MoveBackwardsMode), typeof(MovementFlags) });
        var preview = AccessTools.Method(typeof(TacticalState), nameof(TacticalState.ComputeActorPath), new[] { typeof(Actor) });
        var travel = AccessTools.Method(typeof(TacticalState), nameof(TacticalState.ExecuteActorTravel), new[] { typeof(Actor) });
        var move = AccessTools.Method(typeof(Actor), nameof(Actor.MoveTo),
            new[] { typeof(Tile), typeof(MovementAction).MakeByRefType(), typeof(MovementFlags) });
        if (possible == null || path == null || preview == null || travel == null || move == null)
            throw new MissingMethodException("Native garrison exits require movement eligibility, preview and execution hooks.");
        harmony.Patch(possible, prefix: query, finalizer: finish);
        harmony.Patch(path, prefix: query, finalizer: finish);
        harmony.Patch(preview, prefix: query, finalizer: finish);
        harmony.Patch(travel,
            prefix: new HarmonyMethod(typeof(GarrisonMovementHooks), nameof(GarrisonMovementHooks.Travel)), finalizer: finish);
        harmony.Patch(move,
            prefix: new HarmonyMethod(typeof(GarrisonMovementHooks), nameof(GarrisonMovementHooks.Move)), finalizer: finish);
    }

    private static GarrisonMovementState? BeginGarrisonMovement(Actor? actor, bool preview)
    {
        GarrisonMovementState? state = null;
        try
        {
            if (!Valid(actor) || actor!.IsDying() || !actor.IsInfantry() || actor.IsMoving() ||
                actor.GetStance() != ActorStance.Deployed || actor.GetSuppressionState() == SuppressionState.PinnedDown ||
                !IsDeploymentGarrison(actor.GetContainerEntity())) return null;
            var properties = actor.GetCurrentProperties();
            if (!Valid(properties) || (properties.Flags & (EntityFlags.Rooted | EntityFlags.Stunned)) != 0) return null;
            if (!GarrisonMovementActors.TryAdd(actor.Pointer, 0)) return null;
            state = new GarrisonMovementState { Actor = actor, Preview = preview, Properties = properties,
                HadStanceRoot = (properties.Flags & EntityFlags.RootedByStance) != 0 };
            if (preview)
            {
                // Query-only view: no native animation, skill use, AP charge or stat rebuild.
                // GetPath/ComputeActorPath also inspect the stance directly and add undeploy AP.
                actor.m_Stance = ActorStance.Default;
                properties.Flags &= ~EntityFlags.RootedByStance;
            }
            else
            {
                // Release before the UI tries an unusable in-container undeploy skill.
                // The scope prevents the Update prefix from immediately deploying again.
                actor.SetStance(ActorStance.Default, false);
                var skills = actor.GetSkills();
                if (Valid(skills)) { skills!.Update(); skills.UiNeedsUpdate = true; }
            }
            return state;
        }
        catch (Exception e)
        {
            FinishGarrisonMovement(state);
            DebugWarning($"{ModName}: garrison movement preparation failed: {Short(e)}");
            return null;
        }
    }

    private static void FinishGarrisonMovement(GarrisonMovementState? state)
    {
        if (state == null) return;
        try
        {
            var actor = state.Actor;
            if (state.Preview)
            {
                // Restore only our stance-root bit; preserve other native flag changes.
                if (state.HadStanceRoot && Valid(state.Properties)) state.Properties!.Flags |= EntityFlags.RootedByStance;
                if (Valid(actor) && actor.GetStance() == ActorStance.Default && !actor.IsMoving() &&
                    actor.GetSuppressionState() != SuppressionState.PinnedDown && IsDeploymentGarrison(actor.GetContainerEntity()))
                    actor.m_Stance = ActorStance.Deployed;
            }
        }
        catch (Exception e) { DebugWarning($"{ModName}: garrison preview restoration failed: {Short(e)}"); }
        finally { GarrisonMovementActors.TryRemove(state.Actor.Pointer, out _); }
        // Failed/cancelled movement stays deployed; successful movement stays released.
        if (!state.Preview) SyncGarrisonDeployment(state.Actor, scheduleRefresh: true);
    }

    private static class GarrisonMovementHooks
    {
        public static void Preview(Actor __0, out GarrisonMovementState? __state)
            => __state = BeginGarrisonMovement(__0, preview: true);
        public static void Travel(Actor __0, out GarrisonMovementState? __state)
            => __state = BeginGarrisonMovement(__0, preview: false);
        public static void Move(Actor __instance, out GarrisonMovementState? __state)
            => __state = BeginGarrisonMovement(__instance, preview: false);
        public static Exception? Finished(Exception? __exception, GarrisonMovementState? __state)
        {
            FinishGarrisonMovement(__state);
            return __exception;
        }
    }
}
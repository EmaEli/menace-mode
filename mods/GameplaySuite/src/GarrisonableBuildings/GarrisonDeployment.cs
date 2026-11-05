#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace GarrisonableBuildings;

public sealed partial class Plugin
{
    private static readonly ConcurrentDictionary<IntPtr, byte> GarrisonDeploymentActors = new();
    [ThreadStatic] private static bool _syncingGarrisonDeployment;
    [ThreadStatic] private static GarrisonPropertyFrame? _garrisonPropertyFrame;

    private sealed class GarrisonPropertyFrame
    {
        public Actor? Actor;
        public readonly HashSet<IntPtr> Properties = new();
    }

    private static void ClearGarrisonDeployment()
    {
        GarrisonDeploymentActors.Clear();
        _garrisonPropertyFrame = null;
    }

    private static void InstallGarrisonDeploymentHooks(HarmonyLib.Harmony harmony)
    {
        try
        {
            // Use the completed containment transaction, not OnLeftContainer: native
            // callbacks run before EjectEntity clears the occupant's container pointer.
            var enter = AccessTools.Method(typeof(Entity), nameof(Entity.ContainEntity), new[] { typeof(Entity) });
            var leave = AccessTools.Method(typeof(Entity), nameof(Entity.EjectEntity), new[] { typeof(Tile) });
            var update = AccessTools.Method(typeof(SkillContainer), nameof(SkillContainer.Update), Type.EmptyTypes);
            var moved = AccessTools.Method(typeof(SkillContainer), nameof(SkillContainer.OnMovementFinished), new[] { typeof(Tile) });
            var properties = AccessTools.Method(typeof(Skill), nameof(Skill.OnUpdate), new[] { typeof(EntityProperties) });
            if (enter == null || leave == null || update == null || moved == null || properties == null)
                throw new MissingMethodException("Garrison accuracy requires containment, ejection and native property rebuild hooks.");

            harmony.Patch(enter, postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.Entered)));
            harmony.Patch(leave,
                prefix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.BeforeEjection)),
                postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.AfterEjection)));
            harmony.Patch(update,
                prefix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.BeforeUpdate)),
                finalizer: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.AfterUpdate)));
            harmony.Patch(moved, postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.MovementFinished)));
            harmony.Patch(properties, postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.PropertiesUpdated)));
            DebugMsg($"{ModName}: native-template garrison accuracy hooks installed; no automatic deployed stance or movement overrides.");
        }
        catch (Exception e) { DebugError($"{ModName}: native garrison deployment hooks failed: {Short(e)}"); }
    }

    private static bool IsDeploymentGarrison(Entity? container)
    {
        // Do not depend on ContainerType/cover here: safe collapse temporarily
        // normalizes those template fields while the same occupant is ejected.
        return Valid(container) && _enabledGarrisonIds.Contains(container!.GetTemplate()?.GetID() ?? string.Empty);
    }

    private static Actor? DeploymentActor(SkillContainer skills)
    {
        return skills.GetOwner() is Il2CppObjectBase owner ? owner.TryCast<Actor>() : null;
    }

    private static void RefreshGarrisonProperties(Actor? actor)
    {
        if (!Valid(actor)) return;
        var skills = actor!.GetSkills();
        if (!Valid(skills)) return;
        skills!.UiNeedsUpdate = true;
        skills.ScheduleUpdate();
    }

    // One-time upgrade recovery for occupants saved in the forced 1.3.0-1.3.2
    // stance. New entries are marked known, and later deliberate deployment wins.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RecoverLegacyGarrisonStance(Actor? actor)
    {
        if (_syncingGarrisonDeployment || !Valid(actor) || actor!.IsDying() || !actor.IsInfantry() ||
            !IsDeploymentGarrison(actor.GetContainerEntity()) || GarrisonDeploymentActors.ContainsKey(actor.Pointer)) return;
        _syncingGarrisonDeployment = true;
        try
        {
            if (actor.GetStance() == ActorStance.Deployed)
            {
                if (actor.IsMoving() || actor.GetSuppressionState() == SuppressionState.PinnedDown) return;
                actor.SetStance(ActorStance.Default, false);
                var skills = actor.GetSkills();
                if (Valid(skills)) skills!.UiNeedsUpdate = true;
            }
            GarrisonDeploymentActors.TryAdd(actor.Pointer, 0);
        }
        catch (Exception e) { DebugWarning($"{ModName}: legacy garrison stance recovery failed: {Short(e)}"); }
        finally { _syncingGarrisonDeployment = false; }
    }

    private static class DeploymentHooks
    {
        public static void Entered(Entity __instance, Entity __0)
        {
            try
            {
                if (!Valid(__0) || !IsDeploymentGarrison(__instance)) return;
                var container = __0.GetContainerEntity();
                if (Valid(container) && container!.Pointer == __instance.Pointer)
                {
                    var actor = __0.TryCast<Actor>();
                    if (!Valid(actor) || !actor!.IsInfantry()) return;
                    GarrisonDeploymentActors.TryAdd(actor.Pointer, 0);
                    RefreshGarrisonProperties(actor);
                }
            }
            catch (Exception e) { DebugWarning($"{ModName}: garrison entry sync failed: {Short(e)}"); }
        }

        public static void BeforeEjection(Entity __instance, out Actor? __state)
        {
            __state = null;
            try
            {
                if (!IsDeploymentGarrison(__instance)) return;
                __state = __instance.GetContainedEntity()?.TryCast<Actor>();
            }
            catch (Exception e) { DebugWarning($"{ModName}: garrison ejection capture failed: {Short(e)}"); }
        }

        public static void AfterEjection(bool __result, Actor? __state)
        {
            try { if (__result) RefreshGarrisonProperties(__state); }
            catch (Exception e) { DebugWarning($"{ModName}: garrison exit refresh failed: {Short(e)}"); }
        }

        public static void BeforeUpdate(SkillContainer __instance, out GarrisonPropertyFrame? __state)
        {
            __state = _garrisonPropertyFrame;
            _garrisonPropertyFrame = new GarrisonPropertyFrame();
            try
            {
                _garrisonPropertyFrame.Actor = DeploymentActor(__instance);
                RecoverLegacyGarrisonStance(_garrisonPropertyFrame.Actor);
            }
            catch (Exception e) { DebugWarning($"{ModName}: garrison property sync failed: {Short(e)}"); }
        }

        public static Exception? AfterUpdate(Exception? __exception, GarrisonPropertyFrame? __state)
        {
            _garrisonPropertyFrame = __state;
            return __exception;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void PropertiesUpdated(Skill __instance, EntityProperties __0)
        {
            try
            {
                var frame = _garrisonPropertyFrame;
                var actor = frame?.Actor;
                if (_garrisonAccuracyChanges == null || !Valid(__0) || !Valid(actor) || actor!.IsDying() ||
                    !actor.IsInfantry() || actor.IsMoving() || actor.GetStance() != ActorStance.Default ||
                    actor.GetSuppressionState() == SuppressionState.PinnedDown ||
                    !IsDeploymentGarrison(actor.GetContainerEntity()) || __instance.GetActor()?.Pointer != actor.Pointer ||
                    !frame!.Properties.Add(__0.Pointer)) return;
                ApplyGarrisonAccuracy(__0);
            }
            catch (Exception e) { DebugWarning($"{ModName}: garrison accuracy update failed: {Short(e)}"); }
        }

        public static void MovementFinished(SkillContainer __instance)
        {
            try { RefreshGarrisonProperties(DeploymentActor(__instance)); }
            catch (Exception e) { DebugWarning($"{ModName}: garrison movement sync failed: {Short(e)}"); }
        }
    }
}
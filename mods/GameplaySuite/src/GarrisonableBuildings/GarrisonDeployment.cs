#nullable enable
using System;
using System.Collections.Concurrent;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace GarrisonableBuildings;

public sealed partial class Plugin
{
    private static readonly ConcurrentDictionary<IntPtr, byte> GarrisonDeploymentActors = new();
    [ThreadStatic] private static bool _syncingGarrisonDeployment;

    private static void ClearGarrisonDeployment() => GarrisonDeploymentActors.Clear();

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
            if (enter == null || leave == null || update == null || moved == null)
                throw new MissingMethodException("Native garrison deployment requires containment, ejection and skill refresh/movement hooks.");

            harmony.Patch(enter, postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.Entered)));
            harmony.Patch(leave,
                prefix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.BeforeEjection)),
                postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.AfterEjection)));
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.BeforeUpdate)));
            harmony.Patch(moved, postfix: new HarmonyMethod(typeof(DeploymentHooks), nameof(DeploymentHooks.MovementFinished)));
            DebugMsg($"{ModName}: native garrison deployment hooks installed; no separate accuracy modifier.");
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

    private static void SyncGarrisonDeployment(Actor? actor, bool scheduleRefresh)
    {
        if (_syncingGarrisonDeployment || !Valid(actor)) return;
        _syncingGarrisonDeployment = true;
        try
        {
            IntPtr key = actor!.Pointer;
            if (actor.IsDying() || !actor.IsInfantry())
            {
                GarrisonDeploymentActors.TryRemove(key, out _);
                return;
            }

            bool inside = IsDeploymentGarrison(actor.GetContainerEntity());
            ActorStance stance = actor.GetStance();
            ActorStance? desired = null;
            if (inside)
            {
                // Track existing occupants too (e.g. loading a mission). Native
                // suppression and unknown stances always win over auto-deployment.
                GarrisonDeploymentActors.TryAdd(key, 0);
                if (!actor.IsMoving() && stance == ActorStance.Default &&
                    actor.GetSuppressionState() != SuppressionState.PinnedDown)
                    desired = ActorStance.Deployed;
            }
            else if (GarrisonDeploymentActors.ContainsKey(key))
            {
                if (stance == ActorStance.Deployed) desired = ActorStance.Default;
                else GarrisonDeploymentActors.TryRemove(key, out _);
            }

            if (!desired.HasValue) return;
            actor.SetStance(desired.Value, false);
            // Keep ownership on failure so a later property refresh can retry exit cleanup.
            if (!inside) GarrisonDeploymentActors.TryRemove(key, out _);
            // Let MENACE rebuild native status effects and the stats/skill UI. Do
            // not build properties recursively within containment/death callbacks.
            var skills = actor.GetSkills();
            if (Valid(skills))
            {
                skills!.UiNeedsUpdate = true;
                if (scheduleRefresh) skills.ScheduleUpdate();
            }
            DebugMsg($"{ModName}: native garrison stance {stance}->{desired.Value}: actor={EntityId(actor)}.");
        }
        catch (Exception e) { DebugWarning($"{ModName}: native garrison deployment sync failed: {Short(e)}"); }
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
                    SyncGarrisonDeployment(__0.TryCast<Actor>(), scheduleRefresh: true);
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
                if (Valid(__state) && __state!.IsInfantry()) GarrisonDeploymentActors.TryAdd(__state.Pointer, 0);
            }
            catch (Exception e) { DebugWarning($"{ModName}: garrison ejection capture failed: {Short(e)}"); }
        }

        public static void AfterEjection(bool __result, Actor? __state)
        {
            if (__result) SyncGarrisonDeployment(__state, scheduleRefresh: true);
        }

        public static void BeforeUpdate(SkillContainer __instance)
        {
            try { SyncGarrisonDeployment(DeploymentActor(__instance), scheduleRefresh: false); }
            catch (Exception e) { DebugWarning($"{ModName}: garrison property sync failed: {Short(e)}"); }
        }

        public static void MovementFinished(SkillContainer __instance)
        {
            try { SyncGarrisonDeployment(DeploymentActor(__instance), scheduleRefresh: true); }
            catch (Exception e) { DebugWarning($"{ModName}: garrison movement sync failed: {Short(e)}"); }
        }
    }
}
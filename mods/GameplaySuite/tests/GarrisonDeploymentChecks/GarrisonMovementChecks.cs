using System.Reflection;
using GarrisonableBuildings;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppTactical;

internal static class GarrisonMovementChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const MethodImplAttributes safeguard = MethodImplAttributes.NoInlining | MethodImplAttributes.NoOptimization;
        check(typeof(Plugin).GetMethod("BeginGarrisonMovement", BindingFlags.Static | BindingFlags.NonPublic) == null,
            "the crash-attributed movement helper is removed, not worked around");
        foreach (string name in new[] { "RecoverLegacyGarrisonStance", "TryResolveGarrisonAccuracy", "ApplyGarrisonAccuracy" })
        {
            var method = typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            check((method.GetMethodImplementationFlags() & safeguard) == safeguard, "native interop helper retains #44 safeguard: " + name);
        }
        (Entity shelter, Actor unit) Occupant()
        {
            Plugin.Clear();
            var shelter = new Entity("shelter");
            var unit = new Actor();
            shelter.ContainEntity(unit);
            unit.Skills.Update();
            return (shelter, unit);
        }
        foreach (bool blockDeployed in new[] { true, false })
        {
            var (shelter, unit) = Occupant();
            MovementSystem.BlockDeployed = blockDeployed;
            int changes = unit.StanceChanges;
            check(MovementSystem.IsMovementPossible(unit, new(), false, out _), "native movement eligibility can preview exit");
            var path = MovementSystem.GetPath(unit, new(), MoveBackwardsMode.None);
            check(path.Success && path.Cost == 40, "native path keeps existing leave AP without undeploy surcharge");
            var ui = new TacticalState();
            ui.ComputeActorPath(unit);
            check(ui.PreviewCost == 40, "UI preview also excludes the native undeploy-skill AP fee");
            check(unit.Stance == ActorStance.Default && unit.StanceChanges == changes && unit.AP == 100,
                "hover previews require no stance spoofing, animation or AP");
            check(unit.Properties.Flags == EntityFlags.None && unit.Skills.DisplayedAim == 46,
                "native preview keeps bonus-aware stats without any stance root");
            ui.ExecuteActorTravel(unit);
            check(ui.Started && unit.Moving && unit.Stance == ActorStance.Default, "click releases deployment before unusable undeploy-skill gate");
            check(unit.AP == 60 && unit.StanceChanges == 0, "execution charges only native leave AP without deploying/undeploying");
            unit.Skills.Update();
            check(unit.Stance == ActorStance.Default, "old container pointer does not redeploy an actor already moving out");
            shelter.EjectEntity(new());
            unit.Moving = false;
            unit.Skills.OnMovementFinished(new());
            unit.Skills.Update();
            check(unit.Container == null && unit.Stance == ActorStance.Default && unit.Skills.DisplayedAim == 40,
                "completed exit remains released without leaking garrison effects");
        }
        MovementSystem.BlockDeployed = true;
        foreach (var flags in new[] { EntityFlags.Rooted, EntityFlags.Stunned })
        {
            var (_, unit) = Occupant();
            unit.Properties.Flags |= flags;
            check(!MovementSystem.IsMovementPossible(unit, new(), false, out _), "independent root/stun still blocks preview");
            var ui = new TacticalState(); ui.ExecuteActorTravel(unit);
            check(!ui.Started && unit.Stance == ActorStance.Default && unit.AP == 100, "independent root/stun is not bypassed on execution");
        }
        foreach (string reason in new[] { "AP", "blocked tile", "native rejection", "native exception" })
        {
            var (_, unit) = Occupant();
            var ui = new TacticalState();
            if (reason == "AP") unit.AP = 39;
            if (reason == "blocked tile") ui.Target.Walkable = false;
            if (reason == "native rejection") unit.FailMove = true;
            if (reason == "native exception") unit.ThrowMove = true;
            int ap = unit.AP;
            bool threw = false;
            try { ui.ExecuteActorTravel(unit); }
            catch (InvalidOperationException e) { threw = e.Message == "native movement failure"; }
            unit.Skills.Update();
            check(threw == (reason == "native exception"), "native exceptions are not swallowed");
            check(!unit.Moving && unit.Stance == ActorStance.Default && unit.AP == ap && unit.Skills.DisplayedAim == 46,
                "failed movement retains garrison accuracy without deployment: " + reason);
        }
        {
            var (_, unit) = Occupant();
            MovementSystem.DuringQuery = () => throw new InvalidOperationException("native query failure");
            try { new TacticalState().ComputeActorPath(unit); throw new Exception("query should throw"); }
            catch (InvalidOperationException e) { check(e.Message == "native query failure", "query exception survives nested finalizers"); }
            finally { MovementSystem.DuringQuery = null; }
            check(unit.Stance == ActorStance.Default && unit.Properties.Flags == EntityFlags.None,
                "exceptional query leaves native stance and properties untouched");
            MovementSystem.DuringQuery = () => unit.Properties.Flags |= EntityFlags.HeavyWeaponsAllowed;
            MovementSystem.IsMovementPossible(unit, new(), false, out _);
            MovementSystem.DuringQuery = null;
            check(unit.Properties.Flags == EntityFlags.HeavyWeaponsAllowed, "preview preserves unrelated flag changes");
        }
        foreach (string id in new[] { "vanilla_bunker", "vanilla_tower", "vehicle", "disabled_3x3" })
        {
            var building = new Entity(id);
            var unit = new Actor { Stance = ActorStance.Deployed };
            building.ContainEntity(unit); unit.Skills.Update();
            check(!MovementSystem.GetPath(unit, new(), MoveBackwardsMode.None).Success && unit.StanceChanges == 0,
                "unsupported building retains native deployed movement restrictions: " + id);
        }
        {
            var (_, unit) = Occupant();
            unit.Suppression = SuppressionState.PinnedDown; unit.Stance = ActorStance.PinnedDown;
            var stance = unit.Stance;
            var action = MovementAction.Leave;
            check(!unit.MoveTo(new(), ref action) && unit.Stance == stance && unit.AP == 100, "native pinning still blocks movement");
        }
        {
            var (_, unit) = Occupant();
            var action = MovementAction.Leave;
            check(unit.MoveTo(new(), ref action) && unit.Stance == ActorStance.Default && unit.AP == 60,
                "direct native movement also releases without the player UI entry point");
        }
        {
            Plugin.Clear();
            var shelter = new Entity("shelter");
            var unit = new DispatchActor();
            shelter.ContainEntity(unit); unit.Skills.Update();
            check(MovementSystem.GetPath(unit, new(), MoveBackwardsMode.None).Success && unit.PropertyCalls > 0,
                "guarded preview helper preserves virtual current-property dispatch");
            int before = unit.PropertyCalls;
            var action = MovementAction.Leave;
            check(unit.MoveTo(new(), ref action) && unit.PropertyCalls > before && unit.AP == 60,
                "guarded execution preserves virtual property dispatch and native leave AP");
        }
    }

    private sealed class DispatchActor : Actor
    {
        public int PropertyCalls;
        public override EntityProperties GetCurrentProperties()
        {
            PropertyCalls++;
            return base.GetCurrentProperties();
        }
    }
}
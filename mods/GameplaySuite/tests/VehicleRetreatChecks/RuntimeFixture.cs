using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace SquaddieProbe;

partial class ProbeRuntime
{
    class VehiclePressureState
    {
        public long OwnerPtr = 1, ActivationSerial, RetreatStartedAtMs;
        public Actor? Actor;
        public bool BreakContactPending = true, RetreatInProgress, BreakHandledThisActivation;
        public bool PostBreakPressureResetPending, ApPenaltyAppliedThisActivation, FireHandledThisActivation;
        public bool HasThreat = true;
        public float Pressure = 100;
    }
    bool _vehicleCrewPressure = true;
    long _vehiclePressureActiveActorPtr, _vehiclePressureActivationSerial;
    int _vehicleBreakContactThreshold = 100;
    Dictionary<long, VehiclePressureState> _vehiclePressureStates = new();
    Action<string> _log = _ => { };
    int Searches, Ends;
    bool KillDuringSearch, HasPath = true;
    static bool Valid(Native? value) => value != null && value.Pointer != IntPtr.Zero;
    static long Key(Native? value) => value?.Pointer.ToInt64() ?? 0;
    static string SafeMoraleLabel(Actor actor) => "Neutral";
    void BeginVehiclePressureActivation(TacticalManager manager, Actor actor, VehiclePressureState state) =>
        TryStartBreakContact(manager, actor, state);
    void EndVehiclePressureActivation(VehiclePressureState state) { Ends++; }
    bool TryFindBreakContactRetreat(TacticalManager manager, Actor actor, VehiclePressureState state,
        out Tile destination, out MovementAction action, out int cost, out float gain, out string locomotion)
    {
        Searches++;
        if (KillDuringSearch) actor.Alive = false;
        destination = new(); action = MovementAction.Forward; cost = 20; gain = 3; locomotion = "reverse";
        return HasPath && actor.AP >= cost;
    }
    static int checks;
    static void Check(bool ok, string name)
    { checks++; if (!ok) throw new Exception("Vehicle retreat check failed: " + name); }
    static (Actor, Skill) Burning(string effect)
    {
        var actor = new Actor();
        var skill = new Skill { Owner = actor };
        actor.Skills.All[effect] = new BaseSkill();
        actor.Skills.All["special.vehicle_extinguish_fire"] = skill;
        return (actor, skill);
    }
    static void Main()
    {
        var mover = new PathMover();
        Check(VehicleRetreatSafety.CanStartMovement(mover), "living native move unchanged");
        mover.Destroyed = true;
        Check(!VehicleRetreatSafety.CanStartMovement(mover), "destroyed Unity mover with wrapper rejected");
        mover.Destroyed = false; mover.m_Element!.Destroyed = true;
        Check(!VehicleRetreatSafety.CanStartMovement(mover), "destroyed element rejected");
        mover.m_Element.Destroyed = false; mover.m_Element.Alive = false;
        Check(!VehicleRetreatSafety.CanStartMovement(mover), "dead element rejected");
        mover.m_Element.Alive = true; mover.m_Actor!.Dying = true;
        Check(!VehicleRetreatSafety.CanStartMovement(mover), "dying actor rejected");
        mover.m_Actor.Dying = false; mover.m_Actor.Alive = false;
        Check(!VehicleRetreatSafety.CanStartMovement(mover), "dead actor rejected");
        Check(!VehicleRetreatSafety.CanStartMovement(null), "null mover rejected");
        foreach (string effect in new[] { "damage_effect.small_fire", "damage_effect.medium_fire", "damage_effect.big_fire" })
        {
            var (actor, skill) = Burning(effect);
            var runtime = new ProbeRuntime();
            var state = new VehiclePressureState();
            runtime._vehiclePressureStates[1] = state;
            var manager = new TacticalManager { Active = actor };
            actor.Skills.Busy = true;
            runtime.UpdateVehiclePressureSystem(manager);
            Check(skill.Uses == 0 && actor.Moves == 0 && !state.FireHandledThisActivation, "busy turn start deferred");
            actor.Skills.Busy = false;
            runtime.UpdateVehiclePressureSystem(manager);
            Check(skill.Uses == 1 && actor.AP == 60 && actor.Moves == 0, "firefighting before retreat, native AP cost");
            runtime.UpdateVehiclePressureSystem(manager);
            Check(skill.Uses == 1 && actor.Moves == 0, "wait for native firefighting to finish");
            actor.Skills.Busy = false;
            runtime.UpdateVehiclePressureSystem(manager);
            Check(skill.Uses == 1 && actor.Moves == 1 && state.RetreatInProgress, "retreat after firefighting");
            runtime.UpdateVehiclePressureSystem(manager);
            Check(actor.Moves == 1 && skill.Uses == 1, "no repeat skill or move");
            actor.Alive = false;
            runtime.UpdateVehiclePressureSystem(manager);
            Check(!state.RetreatInProgress && !state.BreakContactPending && actor.ApWrites == 0, "death cancels pending state without native AP writes");
        }
        foreach (string failure in new[] { "absent", "no-fire", "unaffordable", "unusable", "out-of-uses", "target", "rejected", "wrong-native-type", "dead", "dying", "global-busy", "moving", "stunned", "turn-done", "leaving", "tile" })
        {
            var (actor, skill) = Burning("damage_effect.small_fire");
            switch (failure)
            {
                case "absent": actor.Skills.All.Remove("special.vehicle_extinguish_fire"); break;
                case "no-fire": actor.Skills.All.Remove("damage_effect.small_fire"); break;
                case "unaffordable": actor.AP = 30; break;
                case "unusable": skill.Usable = false; break;
                case "out-of-uses": skill.OutOfUses = true; break;
                case "target": skill.TargetLegal = false; break;
                case "rejected": skill.Accepted = false; break;
                case "wrong-native-type": actor.Skills.All["special.vehicle_extinguish_fire"] = new BaseSkill(); break;
                case "dead": actor.Alive = false; break;
                case "dying": actor.Dying = true; break;
                case "global-busy": TacticalManager.Busy = true; break;
                case "moving": actor.Moving = true; break;
                case "stunned": actor.Stunned = true; break;
                case "turn-done": actor.TurnDone = true; break;
                case "leaving": actor.Leaving = true; break;
                case "tile": actor.Tile = null; break;
            }
            int ap = actor.AP;
            Check(!VehicleRetreatSafety.TryExtinguishFire(actor) && actor.AP == ap, failure + " does not force firefighting");
            TacticalManager.Busy = false;
        }
        var (survivor, _) = Burning("damage_effect.big_fire");
        var lethalSearch = new ProbeRuntime { KillDuringSearch = true };
        var deadState = new VehiclePressureState { FireHandledThisActivation = true };
        lethalSearch.TryStartBreakContact(new(), survivor, deadState);
        Check(survivor.Moves == 0 && survivor.ApWrites == 0 && !deadState.BreakContactPending, "death after planning does not issue move");
        deadState.RetreatInProgress = true;
        lethalSearch.FinalizeBreakContact(survivor, deadState, "test");
        Check(survivor.ApWrites == 0 && !deadState.RetreatInProgress, "dead movement finalization has no native writes");
        var noPath = new ProbeRuntime { HasPath = false };
        var healthy = new Actor();
        var noPathState = new VehiclePressureState();
        noPath.TryStartBreakContact(new(), healthy, noPathState);
        Check(healthy.AP == 0 && healthy.Moves == 0 && noPathState.PostBreakPressureResetPending, "native no-path fallback retained");
        Console.WriteLine($"PASS: {checks} production vehicle retreat checks");
    }
}
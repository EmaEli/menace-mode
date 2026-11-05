using GarrisonableBuildings;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
Plugin.Install();
Check(HarmonyLib.Harmony.Hooks.Count == 4, "exact native containment, ejection, refresh and movement hooks registered");
var shelter = new Entity("SHELTER");
var actor = new Actor();
shelter.ContainEntity(actor);
Check(actor.Stance == ActorStance.Deployed && actor.StanceChanges == 1, "successful entry uses native deployed stance");
Check(actor.Skills.UiNeedsUpdate && actor.Skills.Scheduled == 1, "entry schedules native UI/property refresh");
Check(actor.AP == 100, "no separate deploy AP charge");
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 46, "stance reaches property/stat rebuild before native calculation");
shelter.ContainEntity(actor);
actor.Skills.Update();
Check(actor.StanceChanges == 1, "re-entry and property refresh do not stack or repeat deployment");
var rejected = new Actor();
shelter.ContainEntity(rejected);
Check(rejected.StanceChanges == 0, "failed entry is not deployed");
shelter.FailEject = true;
Check(!shelter.EjectEntity(new()) && actor.Stance == ActorStance.Deployed, "failed exit preserves deployed occupant");
shelter.FailEject = false;
Check(shelter.EjectEntity(new()) && actor.Stance == ActorStance.Default, "successful exit clears native deployment after pointers clear");
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 40 && actor.Skills.Scheduled == 2, "exit restores native stats and schedules refresh");
Check(actor.AP == 100, "entry/exit deployment logic does not spend AP");

actor.Moving = true;
shelter.ContainEntity(actor);
Check(actor.Stance == ActorStance.Default, "moving entrants are not forcibly deployed");
actor.Moving = false;
actor.Skills.OnMovementFinished(new());
Check(actor.Stance == ActorStance.Deployed, "settled arrival deploys through movement-finished hook");
actor.Suppression = SuppressionState.PinnedDown;
actor.Stance = ActorStance.PinnedDown;
actor.Skills.Update();
Check(actor.Stance == ActorStance.PinnedDown, "native pinned-down stance is preserved");
actor.Stance = ActorStance.Default;
actor.Skills.Update();
Check(actor.Stance == ActorStance.Default, "pinned suppression blocks deployment even with default stance");
actor.Suppression = SuppressionState.None;
actor.Skills.Update();
Check(actor.Stance == ActorStance.Deployed, "recovery inside re-enables native deployment");
actor.Stance = ActorStance.PinnedDown;
shelter.EjectEntity(new());
Check(actor.Stance == ActorStance.PinnedDown, "exit does not overwrite native pinned-down stance");

var loaded = new Actor { Container = shelter };
shelter.Contained = loaded;
Plugin.Clear();
loaded.Skills.Update();
Check(loaded.Stance == ActorStance.Deployed, "existing loaded occupants reconcile on native property rebuild");
shelter.Template.ContainerType = 0;
shelter.EjectEntity(new());
Check(loaded.Stance == ActorStance.Default, "collapse ejection clears deployment despite temporary template normalization");

var deployedLoad = new Actor { Stance = ActorStance.Deployed, Container = shelter };
shelter.Contained = deployedLoad;
Plugin.Clear();
shelter.EjectEntity(new());
Check(deployedLoad.Stance == ActorStance.Default, "loaded already-deployed occupants clean up even before first refresh");

foreach (string id in new[] { "vanilla_bunker", "vanilla_tower", "vehicle", "disabled_3x3" })
{
    var other = new Entity(id);
    var unit = new Actor { Stance = ActorStance.Deployed };
    other.ContainEntity(unit);
    unit.Skills.Update();
    other.EjectEntity(new());
    Check(unit.StanceChanges == 0 && unit.Stance == ActorStance.Deployed, "unsupported container never takes ownership: " + id);
}
var vehicle = new Actor { Infantry = false };
shelter.ContainEntity(vehicle);
vehicle.Skills.Update();
Check(vehicle.StanceChanges == 0, "vehicle occupants are excluded");
shelter.EjectEntity(new());
var dying = new Actor { Dying = true };
shelter.ContainEntity(dying);
dying.Skills.Update();
Check(dying.StanceChanges == 0, "dying occupants are excluded");
shelter.EjectEntity(new());
var unknown = new Actor { Stance = (ActorStance)99 };
shelter.ContainEntity(unknown);
unknown.Skills.Update();
Check(unknown.StanceChanges == 0, "unknown stances fail closed");
shelter.EjectEntity(new());
var invalid = new Actor { Pointer = IntPtr.Zero };
shelter.ContainEntity(invalid);
invalid.Skills.Update();
Check(invalid.StanceChanges == 0, "invalid native pointers are ignored");
shelter.EjectEntity(new());
Check(Plugin.Warnings.Count == 0, "normal transition tests produce no warnings");
var throws = new Actor { ThrowStance = true };
shelter.ContainEntity(throws);
Check(throws.StanceChanges == 0 && Plugin.Warnings.Count == 1, "native stance exceptions are contained");
throws.ThrowStance = false;
throws.Skills.Update();
Check(throws.Stance == ActorStance.Deployed, "recursion guard resets after native failure");
new SkillContainer(new Entity()).Update();
Check(Plugin.Warnings.Count == 1, "non-actor skill owners are ignored without warnings");
throws.ThrowStance = true;
shelter.EjectEntity(new());
Check(throws.Stance == ActorStance.Deployed && Plugin.Warnings.Count == 2, "failed native exit cleanup remains retryable");
throws.ThrowStance = false;
throws.Skills.Update();
Check(throws.Stance == ActorStance.Default, "next property refresh retries failed exit cleanup");
Console.WriteLine($"Garrison deployment checks passed: {checks}");
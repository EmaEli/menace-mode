using GarrisonableBuildings;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.Tools;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
Plugin.Install();
Check(HarmonyLib.Harmony.Hooks.Count == 5, "containment and scoped native stat hooks only; no movement overrides");
DataTemplateLoader.Template = null;
Plugin.Resolve();
Check(!Plugin.Resolved, "missing native template remains retryable");
var effect = new StanceDeployed { Properties = new[]
{
    new PropertyChange { PropertyType = EntityPropertyType.AccuracyMult, AmountMult = 1.15f },
    new PropertyChange { PropertyType = EntityPropertyType.APLeaveCost, Amount = 100 }
} };
DataTemplateLoader.Template = new SkillTemplate();
DataTemplateLoader.Template.EventHandlers.Add(new SkillEventHandlerTemplate());
DataTemplateLoader.Template.EventHandlers.Add(effect);
Plugin.Resolve();
Check(Plugin.Resolved, "typed native StanceDeployed template resolves after delayed load");
var shelter = new Entity("SHELTER");
var actor = new Actor();
shelter.ContainEntity(actor);
Check(actor.Stance == ActorStance.Default && actor.StanceChanges == 0, "entry never deploys the actor");
Check(actor.Skills.UiNeedsUpdate && actor.Skills.Scheduled == 1, "entry schedules native UI/property refresh");
Check(actor.AP == 100, "no separate deploy AP charge");
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 46 && actor.Properties.Flags == 0, "native accuracy reaches unit stats without stance root");
shelter.ContainEntity(actor);
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 46 && actor.StanceChanges == 0, "multiple skills, entry and rebuilds never stack or deploy");
var rejected = new Actor();
shelter.ContainEntity(rejected);
rejected.Skills.Update();
Check(rejected.StanceChanges == 0, "failed entry is not deployed");
Check(rejected.Skills.DisplayedAim == 40, "failed entry has no bonus");
shelter.FailEject = true;
Check(!shelter.EjectEntity(new()) && actor.Skills.DisplayedAim == 46, "failed exit preserves garrison stats");
shelter.FailEject = false;
Check(shelter.EjectEntity(new()) && actor.Stance == ActorStance.Default, "successful exit clears native deployment after pointers clear");
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 40 && actor.Skills.UiNeedsUpdate, "exit restores native stats and schedules refresh");
Check(actor.AP == 100, "entry/exit deployment logic does not spend AP");

actor.Moving = true;
shelter.ContainEntity(actor);
Check(actor.Stance == ActorStance.Default, "moving entrants are not forcibly deployed");
actor.Moving = false;
actor.Skills.OnMovementFinished(new());
actor.Skills.Update();
Check(actor.Stance == ActorStance.Default && actor.Skills.DisplayedAim == 46, "settled arrival gains accuracy only");
actor.Suppression = SuppressionState.PinnedDown;
actor.Stance = ActorStance.PinnedDown;
actor.Skills.Update();
Check(actor.Stance == ActorStance.PinnedDown, "native pinned-down stance is preserved");
Check(actor.Skills.DisplayedAim == 40, "pinned occupants do not receive deployed benefit");
actor.Stance = ActorStance.Default;
actor.Skills.Update();
Check(actor.Stance == ActorStance.Default, "pinned suppression blocks deployment even with default stance");
actor.Suppression = SuppressionState.None;
actor.Skills.Update();
Check(actor.Stance == ActorStance.Default && actor.Skills.DisplayedAim == 46, "recovery restores accuracy without deploying");
actor.Stance = ActorStance.PinnedDown;
shelter.EjectEntity(new());
Check(actor.Stance == ActorStance.PinnedDown, "exit does not overwrite native pinned-down stance");

var loaded = new Actor { Container = shelter };
shelter.Contained = loaded;
Plugin.Clear();
loaded.Skills.Update();
Check(loaded.Stance == ActorStance.Default && loaded.Skills.DisplayedAim == 46, "loaded occupants gain property-only benefit");
shelter.Template.ContainerType = 0;
shelter.EjectEntity(new());
Check(loaded.Stance == ActorStance.Default, "collapse ejection clears deployment despite temporary template normalization");

var deployedLoad = new Actor { Stance = ActorStance.Deployed, Container = shelter };
shelter.Contained = deployedLoad;
Plugin.Clear();
deployedLoad.Skills.Update();
Check(deployedLoad.Stance == ActorStance.Default && deployedLoad.StanceChanges == 1 && deployedLoad.Skills.DisplayedAim == 46,
    "one-time upgrade recovery releases old forced deployment before native rebuild");
deployedLoad.Stance = ActorStance.Deployed;
deployedLoad.Skills.Update();
Check(deployedLoad.Stance == ActorStance.Deployed && deployedLoad.StanceChanges == 1 && deployedLoad.Skills.DisplayedAim == 46,
    "later manual deployment is preserved and never double-buffed");
shelter.EjectEntity(new());

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
Check(unknown.Skills.DisplayedAim == 40, "unknown stance gains no bonus");
shelter.EjectEntity(new());
var invalid = new Actor { Pointer = IntPtr.Zero };
shelter.ContainEntity(invalid);
invalid.Skills.Update();
Check(invalid.StanceChanges == 0, "invalid native pointers are ignored");
shelter.EjectEntity(new());
Check(Plugin.Warnings.Count == 0, "normal transition tests produce no warnings");
var throws = new Actor { ThrowStance = true, Stance = ActorStance.Deployed, Container = shelter };
shelter.Contained = throws;
throws.Skills.Update();
Check(throws.StanceChanges == 0 && Plugin.Warnings.Count == 1, "native stance exceptions are contained");
throws.ThrowStance = false;
throws.Skills.Update();
Check(throws.Stance == ActorStance.Default && throws.Skills.DisplayedAim == 46, "failed legacy recovery is retryable");
new SkillContainer(new Entity()).Update();
Check(Plugin.Warnings.Count == 1, "non-actor skill owners are ignored without warnings");
shelter.EjectEntity(new());
throws.Skills.Update();
Check(throws.Skills.DisplayedAim == 40, "ejection removes accuracy without touching stance");

var nested = new Actor { Container = new Entity("shelter") };
actor.Container = shelter;
actor.Stance = ActorStance.Default;
actor.Skills.DuringRebuild = () => nested.Skills.Update();
actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 46 && nested.Skills.DisplayedAim == 46 && !Plugin.InRebuild,
    "nested property rebuild restores outer scope without multiplying twice");
actor.Skills.DuringRebuild = () => { nested.Skills.ThrowUpdate = true; nested.Skills.Update(); };
try { actor.Skills.Update(); throw new Exception("Expected native update failure"); }
catch (InvalidOperationException) { Check(!Plugin.InRebuild, "exception finalizers clear all nested scopes"); }
actor.Skills.DuringRebuild = null;
new Skill(actor).OnUpdate(actor.Properties);
Check(actor.Properties.AccuracyMult == 1.15f, "out-of-rebuild skill calls cannot stack bonus");

effect.Properties = new[] { new PropertyChange { PropertyType = EntityPropertyType.Accuracy, Amount = 8 } };
Plugin.Resolve(); actor.Skills.Update();
Check(actor.Skills.DisplayedAim == 48, "additive native accuracy uses loaded amount, not hard-coded 15 percent");
effect.Properties = new[] { new PropertyChange { PropertyType = EntityPropertyType.AccuracyMult, AmountMult = float.NaN } };
Plugin.Resolve(); actor.Skills.Update();
Check(!Plugin.Resolved && actor.Skills.DisplayedAim == 40, "invalid native data fails closed without poisoned stats");
effect.Properties = new[] { new PropertyChange { PropertyType = EntityPropertyType.AccuracyMult, AmountMult = 1.15f } };
Plugin.Resolve();
GarrisonMovementChecks.Run(Check);
Console.WriteLine($"Garrison deployment checks passed: {checks}");
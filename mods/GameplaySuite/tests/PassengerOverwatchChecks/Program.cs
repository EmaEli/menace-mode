using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using MenaceOverwatch;

int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
(Actor p, Actor v, Skill s) Setup()
{
    var p = new Actor(); var v = new Actor { Infantry = false, Vehicle = true };
    p.Container = v; v.Contained = p;
    return (p, v, new Skill { Owner = p });
}
void Deny(Action<Actor, Actor, Skill> change, string label)
{
    var (p, v, s) = Setup(); change(p, v, s);
    Check(!Native.CanArm(p, s, out _), label + " blocks arming");
}

var (p, v, s) = Setup();
Check(PassengerOverwatch.TryGetTransport(p, out var found) && ReferenceEquals(found, v), "native open-top membership");
Check(ReferenceEquals(PassengerOverwatch.GetPassenger(v), p), "vehicle resolves its passenger");
Check(Native.CanArm(p, s, out _), "AllowInsideStructures extended only for open-top Overwatch");
Check(s.NativeQueries == 0 && s.Template!.UsabilityWhileContained == UsabilityWhileContained.AllowInsideStructures,
    "no shared template mutation or native ordinary-attack override");
foreach (string profile in new[] { "cover_inside_vehicle_closed", "custom_open", "cover_inside_building", "" })
    Deny((p, v, s) => v.Template!.ProvidesCoverInside!.name = profile, profile);
Deny((p, v, s) => v.Template = null, "missing vehicle metadata");
Deny((p, v, s) => v.Template!.ProvidesCoverInside = null, "missing cover");
Deny((p, v, s) => v.Template!.ContainerType = EntityContainerType.Building, "not a vehicle container");
Deny((p, v, s) => v.Ready = false, "destroyed/stunned/routing transport");
Deny((p, v, s) => v.Vehicle = false, "non-vehicle actor container");
Deny((p, v, s) => v.Contained = new Actor(), "stale passenger link");
Deny((p, v, s) => v.Faction = 9, "hostile container");
Deny((p, v, s) => p.Infantry = false, "non-infantry occupant");
Deny((p, v, s) => p.Throw = true, "throwing native lookup");
Deny((p, v, s) => v.Moving = true, "moving vehicle");
Deny((p, v, s) => p.Moving = true, "moving passenger");
Deny((p, v, s) => s.Owner = v, "wrong skill owner");
Deny((p, v, s) => s.Payer = v, "vehicle AP payer");
Deny((p, v, s) => s.Template = null, "missing skill metadata");
Deny((p, v, s) => s.Template!.UsabilityWhileContained = UsabilityWhileContained.Never, "Never contained");
Deny((p, v, s) => s.Template!.UsabilityWhileContained = (UsabilityWhileContained)999, "unknown contained policy");
Deny((p, v, s) => p.Suppression = SuppressionState.PinnedDown, "pinned infantry");
Deny((p, v, s) => s.Handlers = null, "missing native handlers");
Deny((p, v, s) => s.Handlers = new[] { new SkillEventHandler { Usable = false } }, "handler veto");
Deny((p, v, s) => s.Handlers = new[] { new SkillEventHandler { Throw = true } }, "throwing handler");
Deny((p, v, s) => s.Handlers = new SkillEventHandler[] { null! }, "missing handler");
foreach (var mode in new[] { ExecutingElementType.AllButSpecial, ExecutingElementType.OneAtRandomExceptSpecial })
    Deny((p, v, s) => { s.Template!.ExecutingElement = mode; p.Elements.RemoveAt(1); p.Items.Special = new(); }, "only special element remains");
foreach (var mutate in new Action<Actor, Actor, Skill>[] {
    (p,v,s) => p.Ready = false, (p,v,s) => s.Indirect = true, (p,v,s) => s.Supported = false,
    (p,v,s) => s.Requirements = false, (p,v,s) => s.Enabled = false, (p,v,s) => s.Hidden = true,
    (p,v,s) => s.Executing = false, (p,v,s) => s.Uses = 0, (p,v,s) => s.Cost = 0,
    (p,v,s) => p.AP = 39 }) Deny(mutate, "existing Overwatch eligibility");

(p, v, s) = Setup(); p.Suppression = SuppressionState.Suppressed;
Check(Native.CanArm(p, s, out _), "suppressed is not pinned");
s.Template!.UsabilityWhileContained = UsabilityWhileContained.Always;
Check(Native.CanArm(p, s, out _), "native Always remains supported");
p.Suppression = SuppressionState.PinnedDown; s.Template.IsUsableWhilePinnedDown = true;
Check(Native.CanArm(p, s, out _), "explicit native pinned permission retained");
var handler = new SkillEventHandler(); s.Handlers = new[] { handler, new SkillEventHandler() };
Check(Native.CanArm(p, s, out _) && handler.Calls == 1, "native handlers queried");

(p, v, s) = Setup();
long position = PassengerOverwatch.ContainerKey(p);
Check(PassengerOverwatch.SamePositionContext(p, position), "same transport retains context");
v.Ready = false;
Check(!PassengerOverwatch.SamePositionContext(p, position), "destroyed transport invalidates context");
v.Ready = true;
p.Container = null;
Check(!PassengerOverwatch.SamePositionContext(p, position), "ejection invalidates context");
Check(Native.CanArm(p, s, out _) && s.NativeQueries == 1, "uncontained native usability unchanged");
p.Container = new Entity();
Check(Native.CanArm(p, s, out _) && s.NativeQueries == 2, "structure native usability unchanged");
p.Container = new Actor { Vehicle = true, Infantry = false };
Check(!PassengerOverwatch.SamePositionContext(p, position), "transport swap invalidates context");

(p, v, s) = Setup(); TacticalManager.Current = new() { Active = v }; TacticalState.Current = new();
var runtime = new Runtime(); runtime.TogglePassengerSelection();
Check(ReferenceEquals(runtime.Selection, p) && runtime.SelectingPassenger, "panel selects passenger without native activation change");
Check(ReferenceEquals(TacticalManager.Current.Active, v), "native vehicle activation unchanged");
Check(runtime.Reserve(p, s) && runtime.Reactions(p) == 2, "passenger has its own watch");
Check(p.AP == 0 && v.AP == 80 && TacticalState.Current.Ends == 0, "arming passenger neither spends nor ends vehicle AP");
Check(p.Updates == 1 && p.Skills.UiNeedsUpdate, "passenger spent state updated");
runtime.TogglePassengerSelection();
Check(ReferenceEquals(runtime.Selection, v), "vehicle plan independently selectable");
var vehicleSkill = new Skill { Owner = v };
Check(runtime.Reserve(v, vehicleSkill) && runtime.Count == 2 && TacticalState.Current.Ends == 1,
    "separate vehicle watch still ends active vehicle normally");
TacticalManager.Current.Active = new Actor();
Check(ReferenceEquals(runtime.Selection, TacticalManager.Current.Active) && !runtime.SelectingPassenger, "changing unit clears passenger choice");

(p, v, s) = Setup(); runtime = new(); TacticalManager.Current.Active = v;
runtime.TogglePassengerSelection(); v.Contained = null;
Check(ReferenceEquals(runtime.Selection, v) && !runtime.SelectingPassenger, "stale passenger selection falls back safely");
Check(!runtime.Reserve(p, s) && runtime.Count == 0 && p.AP == 80 && v.AP == 80, "rejected arm spends nothing");
(p, v, s) = Setup(); runtime = new(); s.Uses = 0;
Check(!runtime.Reserve(p, new Skill { Owner = p }, s) && runtime.Count == 0 && p.AP == 80,
    "invalid second weapon rejects both without spending AP");
(p, v, s) = Setup(); runtime = new(); p.AP = 40;
Check(runtime.Reserve(p, s, ai: true) && p.AP == 40 && v.AP == 80, "AI reservations do not alter native AP");
foreach (var change in new Action<Actor, Actor>[] {
    (p,v) => p.Container = null, (p,v) => p.Container = new Actor(), (p,v) => v.Contained = null,
    (p,v) => v.Ready = false, (p,v) => v.Template!.ProvidesCoverInside!.name = "cover_inside_vehicle_closed" })
{
    (p, v, s) = Setup(); runtime = new(); Check(runtime.Reserve(p, s), "lifecycle fixture armed");
    change(p, v); runtime.Maintain();
    Check(runtime.Count == 0 && runtime.RemovedMarkers == 1, "invalid container expires watch and removes marker");
}
(p, v, s) = Setup(); runtime = new(); runtime.Reserve(p, s);
runtime.OnMovementCommitted(v);
Check(runtime.Count == 0, "committed vehicle movement expires its passenger watch");
(p, v, s) = Setup(); runtime = new(); runtime.Reserve(p, s); runtime.MarkPending(p);
runtime.OnMovementCommitted(v);
Check(runtime.PendingExpired(p), "committed movement invalidates already pending passenger reaction");

// Exercise the actual Native.CanReact and passenger detection, including the native
// actor-container veto that caused armed 0.35 passengers never to queue a shot.
(p, v, s) = Setup();
var enemy = new Actor { Faction = 9 };
var destination = enemy.Tile;
Check(!p.HasLineOfSightTo(enemy, p.Tile, destination), "native actor-container veto reproduced");
Check(Native.CanReact(p, s, enemy, destination, out _), "open-top passenger passes full reaction eligibility");
int calls = p.NativeLosCalls;
Check(Native.HasActorLineOfSight(p, enemy, p.Tile, destination, out var detail)
    && detail.Contains("open-top passenger") && p.NativeLosCalls == calls, "passenger does not enter vetoed native observer path");
Check(p.AP == 80 && v.AP == 80 && s.Uses == 4 && p.Container == v, "detection changes no AP, ammo or containment");
v.Properties!.Vision = 99; p.Properties!.Vision = 4;
Check(!Native.CanReact(p, s, enemy, destination, out _), "passenger cannot borrow vehicle vision");
p.Properties.Vision = 10; p.Tile.Distance = 10;
Check(Native.CanReact(p, s, enemy, destination, out _), "vision and weapon range inclusive boundary");
p.Tile.Distance = 11;
Check(!Native.CanReact(p, s, enemy, destination, out var reject) && reject.Contains("out of range"), "weapon range still enforced");
p.Tile.Distance = 5; p.Tile.Los = false; enemy.Revealed = true;
Check(!Native.CanReact(p, s, enemy, destination, out _), "revealed targets cannot bypass blocked tile LOS");
p.Tile.Los = true; enemy.Revealed = false;
enemy.Properties!.Concealment = 6;
Check(!Native.CanReact(p, s, enemy, destination, out _), "target concealment blocks detection");
p.Properties.Detection = 1;
Check(Native.CanReact(p, s, enemy, destination, out _), "passenger detection offsets concealment at boundary");
destination.Concealment = 1;
Check(!Native.CanReact(p, s, enemy, destination, out _), "smoke/tile concealment retained");
destination.Concealment = 0; destination.Cover = CoverType.Low;
Check(!Native.CanReact(p, s, enemy, destination, out _), "directional cover concealment retained");
enemy.Revealed = true;
Check(Native.CanReact(p, s, enemy, destination, out _), "native revealed target ignores concealment, not vision/range");
p.Properties.Vision = 4;
Check(!Native.CanReact(p, s, enemy, destination, out _), "revealed still respects own vision");
enemy.Revealed = false; p.Properties.Vision = 1; p.Tile.Distance = 1;
Check(Native.CanReact(p, s, enemy, destination, out _), "native minimum adjacent detection range retained");
p.Properties.Vision = 10; p.Tile.Distance = 5; enemy.Properties.Concealment = 0; p.Properties.Detection = 0;
enemy.Container = new Entity(); enemy.Tile.Occupant = enemy.Container;
destination.Concealment = 100;
Check(Native.CanReact(p, s, enemy, destination, out _), "native contained-target cover replaces tile concealment");
var enemyTransport = new Actor { Infantry = false, Vehicle = true };
enemy.Container = enemyTransport; destination.Occupant = enemyTransport;
enemyTransport.Template!.ProvidesCoverInside!.Concealment = 6;
Check(!Native.CanReact(p, s, enemy, destination, out _), "native inside-cover concealment retained");
enemyTransport.Template.ProvidesCoverInside.Concealment = 5;
Check(Native.CanReact(p, s, enemy, destination, out _), "inside-cover inclusive detection boundary");
v.Template!.ProvidesCoverInside!.name = "cover_inside_vehicle_closed";
Check(!Native.HasActorLineOfSight(p, enemy, p.Tile, destination, out _), "closed passenger cannot use open-top detection");
v.Template.ProvidesCoverInside.name = "cover_inside_vehicle_open";
p.Properties = null;
Check(!Native.HasActorLineOfSight(p, enemy, p.Tile, destination, out _), "missing native properties fail closed");
p.Properties = new(); p.Container = null; p.NativeLos = false;
Check(!Native.HasActorLineOfSight(p, enemy, p.Tile, destination, out _) && p.NativeLosCalls > calls,
    "ordinary infantry still use native visibility vetoes");
p.NativeLos = true; p.Container = new Entity();
Check(Native.HasActorLineOfSight(p, enemy, p.Tile, destination, out _), "structure observers retain native path");
Console.WriteLine($"Passenger Overwatch checks passed: {checks}");
using System.Reflection;
using InfantrySandbagCover;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using MenaceModSettings;
using UnityEngine;

int checks = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
ConstructionSystem Host()
{
    TacticalManager.Instance.Round = 1;
    var host = new ConstructionSystem(); host.OnInit(); host.OnSceneLoaded(1, "Tactical"); return host;
}
ConstructionState Work(ConstructionSystem host) => (ConstructionState)typeof(ConstructionSystem)
    .GetField("_work", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
void Next(Skill skill) { TacticalManager.Instance.Round++; skill.Actor.AP = 100; skill.Actor.TilesMovedThisTurn = 0; }

// Execute the production hooks around a native-equivalent accept/charge/apply pipeline.
var host = Host();
Check((int)ModSettings.Definitions.Single().Default == 2, "default two turns");
Check((int)ModSettings.Definitions.Single().Coerce(-10) == 1 && (int)ModSettings.Definitions.Single().Coerce(100) == 10, "option clamps");
var s = new Skill();
Check(s.Use(s.Actor.Tile), "first work accepted after native internal IsUsable");
Check(s.Actor.AP == 20 && s.Applies == 0 && s.EnvironmentChanges == 0 && s.Actor.Tile.Mask == 0, "paid first work, no early effects");
Check(Work(host).Get(s.Actor.Pointer.ToInt64()).Completed == 1, "production progress recorded");
s.Actor.AP = 100;
Check(!s.Use(s.Actor.Tile) && s.Actor.AP == 100, "repeated action same round cannot double charge/progress");
host.Refresh(); ModGui.Draw();
Check(GUI.Labels.Contains("Sandbags 1/2"), "progress label");
var root = GameObject.All.Single(g => !g.Destroyed && g.Name == "Infantry Sandbag Construction");
Check(root.Children.Count == 2 && root.Children.All(c => c.Renderer != null), "independent renderer children");
Check(root.Children[0].Renderer.Positions[0] == new Vector3(28.14f, .55f, 36.14f), "mortar tile footprint");
int objects = GameObject.All.Count; host.Refresh();
Check(GameObject.All.Count == objects, "no per-frame marker recreation");
GUI.color = new(.2f, .3f, .4f, 1); GUI.matrix = new(77); GUI.depth = 123; ModGui.Draw();
Check(GUI.color == new Color(.2f, .3f, .4f, 1) && GUI.matrix.Value == 77 && GUI.depth == 123, "GUI state restored");
Next(s); Check(s.Use(s.Actor.Tile), "second-round work accepted");
Check(s.Applies == 1 && s.EnvironmentChanges == 1 && s.Actor.Tile.Mask == 15 && s.Actor.AP == 20, "native effects only on completion");
host.Refresh(); Check(root.Destroyed, "finished marker disposed");
s.Actor.Tile.Mask = 0; s.Actor.AP = 100;
Check(!s.Use(s.Actor.Tile) && s.Actor.AP == 100, "successful completion also locks this round");
host.OnUnload();

// Settings snapshot, optional pauses, per-actor identity and rejected native actions.
host = Host(); ModSettings.SetTurns(3); s = new Skill(); var other = new Skill();
Check(s.Use(s.Actor.Tile) && other.Use(other.Actor.Tile), "builders isolated even at equal coordinates");
ModSettings.SetTurns(1); Next(s);
Check(s.Use(s.Actor.Tile) && s.Applies == 0, "existing site keeps three-turn requirement");
Next(s); Next(s); Check(s.Use(s.Actor.Tile) && s.Applies == 1, "stationary skipped round pauses without cancelling");
var one = new Skill(); Check(one.Use(one.Actor.Tile) && one.Applies == 1, "new site uses changed one-turn setting");
var rejected = new Skill { NativeUsable = false };
Check(!rejected.Use(rejected.Actor.Tile) && rejected.Actor.AP == 100, "native rejection no debit");
rejected.NativeUsable = true; Check(rejected.Use(rejected.Actor.Tile), "native rejection leaves no stale pending use");
var chargedReject = new Skill { RejectAfterCharge = true };
Check(!chargedReject.Use(chargedReject.Actor.Tile) && chargedReject.Actor.AP == 100, "native rejection after charging refunds without progress");
host.OnUnload();

// Movement commits, walking away and back, forced relocation, transports and death.
host = Host(); s = new Skill(); Check(s.Use(s.Actor.Tile), "start movement fixture");
var start = s.Actor.Tile; var away = new Tile(6, 5);
TacticalManager.Instance.InvokeOnMovement(s.Actor, start, away, MovementAction.Walk, null);
TacticalManager.Instance.InvokeOnMovement(s.Actor, away, start, MovementAction.Walk, null);
Check(Work(host).Get(s.Actor.Pointer.ToInt64()).Completed == 0, "out-and-back cancels without relying on final coordinate");
s.Actor.AP = 100; Check(!s.Use(start) && s.Actor.AP == 100, "cannot restart during moved round");
Next(s); Check(s.Use(start) && s.Applies == 0, "next round restarts at first work");
s.Actor.Tile = away; host.Refresh(); Check(Work(host).Get(s.Actor.Pointer.ToInt64()).Completed == 0, "teleport/forced tile change cancels");
Next(s); Check(s.Use(away), "restart after relocation"); s.Actor.Contained = true; host.Refresh();
Check(Work(host).Get(s.Actor.Pointer.ToInt64()) == null, "embarking removes work");
s.Actor.Contained = false; Next(s); s.Use(away); s.Actor.HP = 0; host.Refresh();
Check(Work(host).Get(s.Actor.Pointer.ToInt64()) == null, "dead builder removed"); host.OnUnload();

// Failed final placement keeps paid earlier work, refunds only actual debit and permits retry.
host = Host(); s = new Skill(); s.Use(s.Actor.Tile); Next(s); s.PlacementFails = true;
Check(s.Use(s.Actor.Tile) && s.Actor.AP == 100, "failed final placement refunded");
Check(Work(host).Get(s.Actor.Pointer.ToInt64()).Completed == 1, "earlier work preserved on failed final placement");
s.PlacementFails = false; Check(s.Use(s.Actor.Tile) && s.Actor.AP == 20 && s.Actor.Tile.Mask == 15, "retry same round succeeds once");
host.OnUnload();

host = Host(); s = new Skill { Cost = 35 }; s.Actor.Tile.Mask = 1;
s.Use(s.Actor.Tile); Next(s); s.PlacementFails = true;
Check(s.Use(s.Actor.Tile) && s.Actor.AP == 100, "failed final work refunds actual modified cost, not fixed 80");
s.PlacementFails = false;
Check(s.Use(s.Actor.Tile) && s.Actor.AP == 65 && s.Actor.Tile.Mask == 15, "partial existing cover does not prevent native completion");
host.OnUnload();

host = Host(); s = new Skill { Delay = true };
Check(s.Use(s.Actor.Tile) && s.Actor.AP == 20, "pending native accepted action");
TacticalManager.Instance.InvokeOnMovement(s.Actor, s.Actor.Tile, new Tile(9, 9), MovementAction.Walk, null);
s.ApplyToTile(s.Actor.Tile, UsageParameter.Default);
Check(s.Actor.AP == 100 && s.Applies == 0, "movement before scheduled application refunds and blocks effects");
s.ApplyToTile(s.Actor.Tile, UsageParameter.Default); Check(s.Applies == 0 && s.Actor.AP == 100, "duplicate apply cannot refund twice");
host.OnUnload();

host = Host(); ModSettings.SetTurns(1); s = new Skill { ThrowPlacement = true };
try { s.Use(s.Actor.Tile); throw new Exception("expected fixture exception"); } catch (InvalidOperationException) { }
Check(s.Actor.AP == 100 && !s.IsUsable(), "exception refunds once and fails closed"); host.OnUnload();

host = Host();
var ordinary = new Skill { ID = "active.unrelated", Actor = new Actor { Player = false } };
Check(ordinary.Use(ordinary.Actor.Tile) && ordinary.Applies == 1 && ordinary.Actor.AP == 20, "other native skills untouched");
foreach (var invalid in new[] { new Skill { Actor = new Actor { Player = false } }, new Skill { Actor = new Actor { Infantry = false } },
    new Skill { Actor = new Actor { TilesMovedThisTurn = 1 } }, new Skill { Actor = new Actor { Contained = true } } })
    Check(!invalid.Use(invalid.Actor.Tile) && invalid.Actor.AP == 100, "ineligible actor untouched");
s = new Skill(); s.Actor.Tile.Mask = 15; Check(!s.Use(s.Actor.Tile), "fully covered tile rejected before debit");
s.Actor.Tile.Mask = 0;
Check(!s.Use(s.Actor.Tile, UsageParameter.Fake) && !s.Use(s.Actor.Tile, UsageParameter.NoApConsumed) && s.Actor.AP == 100, "simulation/free-use cannot advance work");
Check(!Task.Run(() => s.Use(s.Actor.Tile)).GetAwaiter().GetResult(), "worker-thread construction blocked");
Check(s.Use(s.Actor.Tile), "main thread still usable"); host.OnSceneLoaded(2, "Tactical");
Check(Work(host).Get(s.Actor.Pointer.ToInt64()) == null, "new scene clears native-pointer identities");
host.OnSceneLoaded(3, "Strategy"); s.Actor.AP = 100; Check(!s.Use(s.Actor.Tile), "outside tactical rejected");
host.OnUnload(); Check(ModGui.Draw == null, "GUI callback released");
Console.WriteLine($"PASS: {checks} sandbag production runtime/marker checks (managed stand-ins; no native dispatch).");
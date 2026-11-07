using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tools;
using Scores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
Check(AdvanceRecoveryBuild.IsSupported("871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111"), "reviewed 0.7.17");
Check(AdvanceRecoveryBuild.IsSupported("494C9E212D411D898B3ABAFB4AD233F7A39A22648D713A86B9641D698B2D12E9"), "reviewed 0.7.18, case insensitive");
foreach (string hash in new[] { null, "", "0.7.18+22828", new string('0',64), "877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db" })
    Check(!AdvanceRecoveryBuild.IsSupported(hash), "unknown/unreviewed builds fail closed");
var order = new AdvanceRecoveryOrder(10, 0, 5, 5, 1);
var tracker = new AdvanceRecoveryState();
bool Observe(int r, int x = 0, long map = 1, long agent = 20, AdvanceRecoveryOrder? o = null)
    => tracker.Observe(map, r, 10, agent, x, 0, o ?? order);
void Idle(int r) => tracker.Choice(1, r, 10, 20, true);
Check(!Observe(1), "first observation cannot recover");
Idle(1);
for (int i = 0; i < 10000; i++) Check(!Observe(1), "polls never become rounds");
Check(!Observe(2), "one idle round insufficient"); Idle(2);
Check(Observe(3), "two consecutive completed idle rounds qualify");
Check(!Observe(3, x: 1), "same-round movement cancels recovery");
Idle(3); Check(!Observe(4, x: 1), "later Idle cannot erase activity");
tracker.Reset(); Observe(1); Idle(1); Observe(2); Idle(2);
tracker.Choice(1, 2, 10, 20, false); Idle(2);
Check(!Observe(3), "useful choice invalidates idle history");
foreach (string change in new[] { "map", "agent", "order", "gap", "rewind", "reset" })
{
    tracker.Reset(); Observe(1); Idle(1); Observe(2); Idle(2);
    if (change == "reset") tracker.Reset();
    Check(!Observe(change == "gap" ? 4 : change == "rewind" ? 1 : 3,
        map: change == "map" ? 2 : 1, agent: change == "agent" ? 21 : 20,
        o: change == "order" ? order with { Priority = 2 } : order), change + " resets history");
}
tracker.Reset();
Check(tracker.Describe(10) == "history=absent", "diagnostics do not create history");
Observe(1); Idle(1);
Check(tracker.Describe(10).Contains("idle=True busy=False"), "diagnostics expose idle evidence");
Check(tracker.Describe(10).Contains("idleRounds=0"), "diagnostics never complete a round");
Check(tracker.ShouldLogDiagnostic(1, 20, (int)AdvanceRecoveryResult.History), "first recovery stage is logged");
Check(!tracker.ShouldLogDiagnostic(1, 20, (int)AdvanceRecoveryResult.History), "unchanged stage is deduplicated");
Check(tracker.ShouldLogDiagnostic(1, 20, (int)AdvanceRecoveryResult.Applied), "changed recovery stage is logged");
Check(!Observe(2), "diagnostic reads do not advance history"); Idle(2);
Check(Observe(3), "diagnostic reads preserve recovery eligibility");
tracker.Reset();
for (int i = 1; i <= AdvanceRecoveryState.MaxActors; i++)
    Check(!tracker.Observe(1, 1, i, i, 0, 0, order), "bounded initial entries");
Check(!tracker.Observe(1, 1, 9999, 9999, 0, 0, order), "overflow fails closed");
tracker.Reset();
for (int round = 1; round <= 3; round++)
{
    int r = round;
    var results = new bool[32];
    Parallel.For(0, 32, i =>
    {
        results[i] = tracker.Observe(1, r, i + 1, i + 1, 0, 0, order);
        tracker.Choice(1, r, i + 1, i + 1, true);
    });
    Check(results.All(v => v == (r == 3)), "parallel actors retain independent history");
}

(Actor Actor, Scores Tiles, AdvanceRecoveryState State) Fixture()
{
    AIConfig.WEIGHTS = new();
    var actor = new Actor();
    var tiles = new Scores();
    tiles.Add(actor.Tile, new TileScore { Tile = actor.Tile, DistanceScore = 4 });
    // At ConsiderZones.PostProcess, Move has not found/clipped paths yet.
    var next = new Tile(5, 0); tiles.Add(next, new TileScore { Tile = next, DistanceScore = 2 });
    var state = new AdvanceRecoveryState();
    for (int r = 1; r <= 2; r++)
    {
        if (AdvanceRecoveryRuntime.TryApply(actor, tiles, state, 1, r)) throw new Exception("premature recovery");
        state.Choice(1, r, 10, 20, true);
    }
    return (actor, tiles, state);
}
foreach (int faction in Enumerable.Range(0, 11))
{
    var (a, t, s) = Fixture(); a.Agent.Faction.Kind = faction;
    Check(AdvanceRecoveryRuntime.TryApply(a, t, s, 1, 3, out var result) == (faction >= 5 && faction <= 9), "faction " + faction);
    Check(result == (faction >= 5 && faction <= 9 ? AdvanceRecoveryResult.Applied : AdvanceRecoveryResult.Faction), "faction result " + faction);
    if (faction >= 5 && faction <= 9)
    {
        Check(t[a.Tile].DistanceScore == 64, "native distance contribution, not arbitrary score boost");
        Check(t.Values.Last().DistanceScore == 32, "closer candidate retains smaller penalty");
        Check(t.Values.All(v => v.SafetyScore == 123 && v.UtilityScoreScaled == 456 && v.Path != null), "safety/scaled/path untouched");
        Check(a.Agent.Faction.Known, "contact knowledge untouched");
    }
}
var cases = new Dictionary<string, Action<Actor, Scores>>
{
    ["allied"] = (a,t) => a.Agent.Faction.Allied = true,
    ["no known contact"] = (a,t) => a.Agent.Faction.Known = false,
    ["civilian role"] = (a,t) => a.Agent.Role.AvoidOpponents = true,
    ["defend"] = (a,t) => a.Agent.Zones[0].Order = 2,
    ["mixed orders"] = (a,t) => a.Agent.Zones.Add(new() { Order = 2 }),
    ["multiple advance"] = (a,t) => a.Agent.Zones.Add(new()),
    ["no order"] = (a,t) => a.Agent.Zones.Clear(),
    ["sleep"] = (a,t) => a.Agent.Sleeping = true,
    ["deployment"] = (a,t) => a.Agent.Deployment = true,
    ["dying"] = (a,t) => a.Dying = true,
    ["moving"] = (a,t) => a.Moving = true,
    ["garrison"] = (a,t) => a.Contained = true,
    ["already moved"] = (a,t) => a.TilesMovedThisTurn = 1,
    ["suppression"] = (a,t) => a.Suppression = 0.1f,
    ["morale"] = (a,t) => a.Morale = 0.5f,
    ["unknown morale"] = (a,t) => a.Morale = float.NaN,
    ["unknown suppression"] = (a,t) => a.Suppression = float.NaN,
    ["no AP"] = (a,t) => a.AP = 0,
    ["utility"] = (a,t) => t.Values.Last().UtilityScore = 1,
    ["attack utility"] = (a,t) => t.Values.Last().UtilityByAttacksScore = 1,
    ["pending attack utility"] = (a,t) => t.Values.Last().UtilityByAttacksScoreCandidate = 1,
    ["attack target"] = (a,t) => ((Attack)a.Agent.Behaviors.Last()).m_Candidates.Add(new()),
    ["other action"] = (a,t) => a.Agent.Behaviors.Last().Score = 1,
    ["unknown utility"] = (a,t) => t.Values.Last().UtilityScore = float.NaN,
    ["unknown distance"] = (a,t) => t.Values.Last().DistanceScore = float.NaN,
    ["unknown safety"] = (a,t) => t.Values.Last().SafetyScore = float.NaN,
    ["invalid weights"] = (a,t) => AIConfig.WEIGHTS.DistanceToZones = -1,
    ["absent weights"] = (a,t) => AIConfig.WEIGHTS = null,
    ["zero zone weight"] = (a,t) => a.Agent.Role.ZoneImportanceWeight = 0,
    ["absent current tile"] = (a,t) => t.Remove(a.Tile),
    ["no closer tile"] = (a,t) => t.Values.Last().Tile = new(-5,0),
    ["already arrived"] = (a,t) => a.Agent.Zones[0].Area = new(-1,-1,20,20)
};
foreach (var (label, mutate) in cases)
{
    var (a, t, s) = Fixture(); mutate(a,t);
    var before = t.Values.Select(v => (v.DistanceScore, v.UtilityScore)).ToArray();
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3, out var result), label + " preserves native behavior");
    Check(result != AdvanceRecoveryResult.Applied, label + " reports rejection, never success");
    Check(before.SequenceEqual(t.Values.Select(v => (v.DistanceScore,v.UtilityScore))), label + " writes no scores");
}
foreach (var (label, expected) in new Dictionary<string, AdvanceRecoveryResult>
{
    ["defend"] = AdvanceRecoveryResult.Zone, ["allied"] = AdvanceRecoveryResult.Faction,
    ["no known contact"] = AdvanceRecoveryResult.Contact, ["sleep"] = AdvanceRecoveryResult.ActorState,
    ["morale"] = AdvanceRecoveryResult.Resources, ["attack target"] = AdvanceRecoveryResult.AttackCandidates,
    ["other action"] = AdvanceRecoveryResult.ActionScore, ["invalid weights"] = AdvanceRecoveryResult.Weights,
    ["unknown utility"] = AdvanceRecoveryResult.TileData, ["utility"] = AdvanceRecoveryResult.TileUtility,
    ["already arrived"] = AdvanceRecoveryResult.History, ["no closer tile"] = AdvanceRecoveryResult.Progress
})
{
    var (a,t,s) = Fixture(); cases[label](a,t);
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,out var result) && result == expected, label + " exact rejection stage");
}
{
    var (a,t,s) = Fixture(); a.Agent.Faction.Known = false;
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,out var contactResult) && contactResult == AdvanceRecoveryResult.Contact,
        "no-contact recovery preserves native zone scoring");
    Check(s.HasMoveFallbackEligibility(1,3,10,20), "stalled explicit order remains eligible for Move fallback without contact");
    Check(t[a.Tile].DistanceScore == 4, "no-contact recovery does not double-apply native zone scores");
}
{
    var (a,t,s) = Fixture();
    Check(AdvanceRecoveryRuntime.TryApply(a,t,s,1,3), "known-contact stalled order receives missing native score contributions");
    float appliedDistance = t[a.Tile].DistanceScore;
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,out var result) && result == AdvanceRecoveryResult.AlreadyApplied,
        "recovery is applied only once per actor per round");
    Check(t[a.Tile].DistanceScore == appliedDistance, "repeat evaluation does not stack recovery scores");
}
{
    var (a,t,s) = Fixture();
    Check(AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,true,out var result,out bool candidate,out bool armed) &&
        candidate && armed && result == AdvanceRecoveryResult.Applied, "hostile advance recovery reports target and armed state");
}
{
    var (a,t,s) = Fixture(); a.Agent.Zones.Add(new() { Order = 2 });
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,true,out var result,out bool candidate,out bool armed) &&
        candidate && !armed && result == AdvanceRecoveryResult.Orders, "mixed hostile orders remain rejected but diagnosable");
}
{
    var (a,t,s) = Fixture(); a.Agent.Zones[0].Order = 2;
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,true,out var result,out bool candidate,out _) &&
        !candidate && result == AdvanceRecoveryResult.Zone, "defensive orders are not diagnostic targets");
}
foreach (int cost in new[] { 0, 20, 101, int.MaxValue })
{
    var (a,t,s) = Fixture(); t.Values.Last().APCost = cost;
    Check(AdvanceRecoveryRuntime.TryApply(a,t,s,1,3), "pre-path cost does not reject geometric incentive: " + cost);
    Check(t.Values.Last().APCost == cost && a.AP == 100, "native Move retains cost/AP ownership");
}
{
    var (a,t,s) = Fixture();
    var remaining = new Tile(2,0); t.Add(remaining, new() { Tile=remaining, APCost=10 }); t.Remove(a.Tile);
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,out var result) && result == AdvanceRecoveryResult.CurrentTile, "missing current tile distinguished from input size");
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,new(),1,3,out result) && result == AdvanceRecoveryResult.History, "missing idle history distinguished");
    Check(!AdvanceRecoveryRuntime.TryApply(null,t,s,1,3,out result) && result == AdvanceRecoveryResult.Input, "invalid input distinguished");
}
{
    var (a,t,s) = Fixture(); cases["already arrived"](a,t); s.Reset();
    for (int r = 1; r <= 2; r++)
    {
        AdvanceRecoveryRuntime.TryApply(a,t,s,1,r);
        s.Choice(1,r,10,20,true);
    }
    Check(!AdvanceRecoveryRuntime.TryApply(a,t,s,1,3,out var result) && result == AdvanceRecoveryResult.Arrival, "arrival with unchanged order distinguished from order change");
}
{
    var (a,t,s) = Fixture(); var inside = new Tile(10,0); t.Add(inside,new() { Tile=inside, APCost=50 });
    Check(AdvanceRecoveryRuntime.TryApply(a,t,s,1,3), "inside-zone candidate accepted");
    Check(t[inside].UtilityScore == 20 && t[a.Tile].UtilityScore == 0, "native inside-only utility threshold");
}
{
    var (a,t,s) = Fixture(); RectIntExtensions.ThrowOnInside = true;
    try { AdvanceRecoveryRuntime.TryApply(a,t,s,1,3); Check(false,"native failure should throw to fail-closed wrapper"); }
    catch (InvalidOperationException) { Check(t[a.Tile].DistanceScore == 4 && t.Values.Last().DistanceScore == 2, "all geometry checked before writes"); }
    finally { RectIntExtensions.ThrowOnInside = false; }
    Check(AdvanceRecoveryRuntime.IdleOnly(a.Agent,a.Agent.Behaviors[0]), "idle-only observation");
    a.Agent.Behaviors[1].Score = 1;
    Check(!AdvanceRecoveryRuntime.IdleOnly(a.Agent,a.Agent.Behaviors[0]), "positive native Move is not stalled Idle");
}
{
    var (a,t,s) = Fixture();
    var move = (Move)a.Agent.Behaviors[1];
    var destination = new Tile(4, 0);
    var target = new TileScore { Tile = destination, APCost = 2 };
    t.Add(destination, target);
    move.TargetTile = target;
    Check(AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out Move forced) &&
        ReferenceEquals(forced, move), "stalled unseen advance selects existing legal native Move target");
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, move, out _), "non-Idle native choice is preserved");
    move.TargetTile = new TileScore { Tile = new Tile(-1, 0), APCost = 2 };
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out _), "Move target must advance toward order");
    move.TargetTile = new TileScore { Tile = destination, APCost = 0 };
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out _), "unprocessed Move target is not forced");
    move.TargetTile = new TileScore { Tile = destination, APCost = 101 };
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out _), "Move target beyond AP is not forced");
    move.TargetTile = target;
    ((Attack)a.Agent.Behaviors.Last()).m_Candidates.Add(new());
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out _), "valid attack candidate prevents forced Move");
    ((Attack)a.Agent.Behaviors.Last()).m_Candidates.Clear();
    a.Agent.Behaviors.Last().Score = 1;
    Check(!AdvanceRecoveryRuntime.TryGetAdvanceMove(a.Agent, a, a.Agent.Behaviors[0], out _), "positive competing action prevents forced Move");
}
{
    var (a,t,s) = Fixture(); TileScore.WritesBeforeThrow = 1;
    try { AdvanceRecoveryRuntime.TryApply(a,t,s,1,3); Check(false,"partial-write fault should propagate"); }
    catch (InvalidOperationException) { Check(t[a.Tile].DistanceScore == 4 && t.Values.Last().DistanceScore == 2, "partial writes rolled back"); }
    finally { TileScore.WritesBeforeThrow = -1; }
}
// Null-target regression: no path/AP data exists yet, native HasUtility must see order utility.
foreach (bool known in new[] { false, true })
{
    var (a,t,s) = Fixture(); a.Agent.Faction.Known = known;
    var move = (Move)a.Agent.Behaviors[1];
    AdvanceRecoveryRuntime.TryApply(a,t,s,1,3);
    Check(move.GetTargetTile() == null && !move.HasUtility(t), "reproduce null-target utility rejection");
    var unsafeTile = new Tile(6,0);
    t.Add(unsafeTile, new() { Tile = unsafeTile, SafetyScore = 122 });
    Check(AdvanceRecoveryRuntime.TrySeedAdvanceUtility(move,a,t,s,1,3,out int seeded) && seeded == 1,
        "known/unseen safe order progress is eligible before paths exist");
    Check(move.HasUtility(t) && t.Values.ElementAt(1).UtilityScore == move.GetUtilityThreshold(), "native threshold, not arbitrary boost");
    Check(t[a.Tile].UtilityScore == 0 && t[unsafeTile].UtilityScore == 0, "current/less-safe tiles get no incentive");
    Check(t.Values.All(v => v.APCost == 0 && v.UtilityScoreScaled == 456 && v.Path != null), "native AP/path/scaled data untouched");
    Check(!AdvanceRecoveryRuntime.TrySeedAdvanceUtility(move,a,t,s,1,3,out _), "existing utility is preserved on repeated polls");
}
foreach (string rejection in new[] { "history", "map", "round", "sleep", "deployment", "dying", "moving", "garrison",
    "already moved", "suppression", "morale", "no AP", "allied", "defend", "mixed orders", "attack target", "other action",
    "utility", "attack utility", "pending attack utility", "unknown safety", "no closer tile", "no safe progress" })
{
    var (a,t,s) = Fixture(); var move = (Move)a.Agent.Behaviors[1];
    AdvanceRecoveryRuntime.TryApply(a,t,s,1,3);
    if (rejection == "no safe progress") t.Values.Last().SafetyScore = 122;
    else if (cases.TryGetValue(rejection, out var mutate)) mutate(a,t);
    var before = t.Values.Select(v => v.UtilityScore).ToArray();
    Check(!AdvanceRecoveryRuntime.TrySeedAdvanceUtility(move,a,t,rejection == "history" ? new() : s,
        rejection == "map" ? 2 : 1,rejection == "round" ? 4 : 3,out _), "utility guard: " + rejection);
    Check(before.SequenceEqual(t.Values.Select(v => v.UtilityScore)), "utility guard writes nothing: " + rejection);
}
foreach (float threshold in new[] { 0, -1, float.NaN, float.PositiveInfinity })
{
    var (a,t,s) = Fixture(); AdvanceRecoveryRuntime.TryApply(a,t,s,1,3);
    var move = (Move)a.Agent.Behaviors[1]; move.Threshold = threshold;
    Check(!AdvanceRecoveryRuntime.TrySeedAdvanceUtility(move,a,t,s,1,3,out _), "invalid native threshold rejected");
}
foreach (string flag in new[] { "done", "moved", "delaying", "inert", "wrong move" })
{
    var (a,t,s) = Fixture(); AdvanceRecoveryRuntime.TryApply(a,t,s,1,3);
    var move = (Move)a.Agent.Behaviors[1];
    move.Done = flag == "done"; move.Moved = flag == "moved";
    move.Delaying = flag == "delaying"; move.Inert = flag == "inert";
    Check(!AdvanceRecoveryRuntime.TrySeedAdvanceUtility(flag == "wrong move" ? new Move() : move,a,t,s,1,3,out _),
        "native movement guard: " + flag);
}
{
    var (a,t,s) = Fixture(); AdvanceRecoveryRuntime.TryApply(a,t,s,1,3);
    var move = (Move)a.Agent.Behaviors[1];
    var next = new Tile(6,0); t.Add(next,new() { Tile = next });
    TileScore.UtilityWritesBeforeThrow = 1;
    try { AdvanceRecoveryRuntime.TrySeedAdvanceUtility(move,a,t,s,1,3,out _); Check(false,"utility fault must propagate"); }
    catch (InvalidOperationException) { Check(t.Values.All(v => v.UtilityScore == 0), "partial utility writes rolled back"); }
    finally { TileScore.UtilityWritesBeforeThrow = -1; }
}
// Exercise the actual hooks, not just their scoring policy, across native/enlarged maps.
foreach (var dimensions in new[] { (64,64), (96,64), (64,96), (96,96) })
{
    Plugin.BeginFixture();
    var (a,t,_) = Fixture();
    TacticalManager.Instance = new() { Map = new() { Width = dimensions.Item1, Height = dimensions.Item2 } };
    for (int r = 1; r <= 3; r++)
    {
        TacticalManager.Instance.m_Round = r;
        Plugin.ScoreFixture(a,t);
        Plugin.UtilityFixture((Move)a.Agent.Behaviors[1],t);
        Plugin.ChoiceFixture(a.Agent,a.Agent.Behaviors[0]);
    }
    Check((t[a.Tile].DistanceScore > 4) == (dimensions == (96,96)), "hooks use actual map dimensions " + dimensions);
    Check((t.Values.Last().UtilityScore > 0) == (dimensions == (96,96)), "utility prefix respects actual map dimensions " + dimensions);
    Check(Plugin.Logs.Count == 0, "default hooks emit no per-actor logs");
}
{
    Plugin.BeginFixture();
    var (a,t,_) = Fixture();
    a.Agent.Faction.Known = false;
    var move = (Move)a.Agent.Behaviors[1];
    var destination = new Tile(4, 0);
    var target = new TileScore { Tile = destination, APCost = 2 };
    t.Add(destination, target);
    move.TargetTile = target;
    TacticalManager.Instance = new() { Map = new() { Width = 96, Height = 96 } };
    Behavior choice = a.Agent.Behaviors[0];
    for (int r = 1; r <= 3; r++)
    {
        TacticalManager.Instance.m_Round = r;
        Plugin.ScoreFixture(a,t);
        choice = Plugin.ChoiceFixture(a.Agent, choice);
    }
    Check(ReferenceEquals(choice, move), "actual hook forces existing legal Move target for an unseen stalled advance");
    Check(t[a.Tile].DistanceScore == 4, "unseen forced Move does not add duplicate native zone scores");
}
{
    Plugin.BeginFixture(diagnostics: true);
    var (a,t,_) = Fixture();
    TacticalManager.Instance = new();
    for (int r = 1; r <= 2; r++)
    {
        TacticalManager.Instance.m_Round = r;
        Plugin.ScoreFixture(a,t);
        Plugin.ChoiceFixture(a.Agent,a.Agent.Behaviors[0]);
    }
    Check(Plugin.Logs.Count == 0, "unarmed history does not emit recovery traces");
    TacticalManager.Instance.m_Round = 3;
    Plugin.ScoreFixture(a,t);
    Check(Plugin.Logs.Count == 1 && Plugin.Logs[0].Contains("ADVANCE-CHECK round=3") &&
        Plugin.Logs[0].Contains("result=Applied armed=True applied=True"), "armed candidate logs its applied stage");
    Plugin.ChoiceFixture(a.Agent,a.Agent.Behaviors[0]);
    Check(Plugin.Logs.Count == 2 && Plugin.Logs[1].Contains("ADVANCE-CHOICE round=3") &&
        Plugin.Logs[1].Contains("recovery=Applied") && Plugin.Logs[1].Contains("chosen=Idle") &&
        Plugin.Logs[1].Contains("moveScore=0") && Plugin.Logs[1].Contains("idleScore=0"), "recovery choice reports Move and Idle scores");
    Plugin.ChoiceFixture(a.Agent,a.Agent.Behaviors[0]);
    Check(Plugin.Logs.Count == 2, "unchanged recovery choice is deduplicated");
}
{
    Plugin.BeginFixture(diagnostics: true);
    var (a,t,_) = Fixture();
    TacticalManager.Instance = new();
    Plugin.ResetFixtureMission();
    TacticalManager.Instance.m_Round = 3;
    Plugin.ScoreFixture(a,t);
    Check(t[a.Tile].DistanceScore == 4, "new mission cannot inherit idle history");
    Check(Plugin.Logs.Count == 0, "reset mission does not emit an unarmed recovery trace");
}
{
    Plugin.BeginFixture(diagnostics: true);
    TacticalManager.Instance = new();
    for (int i = 0; i < 500; i++)
    {
        var (a,t,_) = Fixture();
        a.Pointer = (IntPtr)(100 + i);
        a.Agent.Pointer = (IntPtr)(1000 + i);
        a.Agent.Zones.Add(new() { Order = 2 });
        Plugin.ScoreFixture(a,t);
    }
    Check(Plugin.Logs.Count == 200, "targeted diagnostics capped at 200 lines per mission");
}
{
    Plugin.BeginFixture(diagnostics: true);
    var (a,t,_) = Fixture();
    a.Agent.Zones.Add(new() { Order = 2 });
    Plugin.RejectLog = true;
    Plugin.ScoreFixture(a,t);
    Check(Plugin.RecoveryEnabled, "diagnostic failures do not disable recovery");
    Plugin.RejectLog = false;
    Plugin.ScoreFixture(null,t);
    Check(Plugin.RecoveryEnabled, "null actor safely rejected by actual hook");
    TacticalManager.Instance.Map = null;
    Plugin.ScoreFixture(a,t);
    Plugin.ChoiceFixture(a.Agent,a.Agent.Behaviors[0]);
    Check(Plugin.RecoveryEnabled, "scene teardown safely ignored by both hooks");
}
Console.WriteLine($"Advance recovery checks passed: {checks} (managed stand-ins, not IL2CPP/gameplay)");
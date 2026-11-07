using AIPerformance;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Scores = Il2CppSystem.Collections.Generic.Dictionary<Il2CppMenace.Tactical.Tile, Il2CppMenace.Tactical.AI.Data.TileScore>;

internal static class ContactConvergenceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var state = new ContactMemoryState();
        state.Observe(1, 1, 30, 100, 30, 20);
        check(state.TryEstimate(1, 2, 30, 100, out var estimate) && estimate.X == 30 && estimate.Radius == 5,
            "contact coordinates stay fixed as uncertainty grows");
        check(!state.TryEstimate(1, 2, 31, 100, out _), "no cross-faction information sharing");
        check(!state.TryEstimate(1, 4, 30, 100, out _), "lost contact expires after two rounds");
        state.Observe(1, 4, 30, 100, 30, 20);
        check(!state.TryEstimate(2, 4, 30, 100, out _), "map transition clears memory");
        state.Observe(2, 4, 30, 100, 30, 20);
        check(!state.TryEstimate(2, 1, 30, 100, out _), "round rollback clears memory");
        state.Observe(2, 1, 30, 100, 30, 20);
        state.Reset();
        check(!state.TryEstimate(2, 1, 30, 100, out _), "explicit reset clears memory");
        state.Observe(1, 1, 30, 100, 30, 20);
        var sectors = new HashSet<int>();
        for (int i = 0; i < 3; i++)
        {
            check(state.TryRespond(1, 1, 30, 10 + i, 20 + i, 0, 0, out estimate), "bounded responder accepted");
            sectors.Add(estimate.X);
        }
        check(sectors.Count == 3, "responders receive separate approach sectors");
        check(!state.TryRespond(1, 1, 30, 13, 23, 0, 0, out _), "fourth responder rejected");
        check(!state.TryRespond(1, 1, 30, 10, 999, 0, 0, out _), "recycled actor identity fails closed");
        for (int i = 0; i < 100; i++)
            check(state.TryRespond(1, 1, 30, 10, 20, 0, 0, out _), "choice polls are not idle rounds");
        check(state.TryRespond(1, 2, 30, 10, 20, 0, 0, out _), "first stationary round retained");
        check(!state.TryRespond(1, 3, 30, 10, 20, 0, 0, out _), "two stationary rounds stop despite growing uncertainty");
        state.Reset();
        state.Observe(1, 1, 30, 100, 30, 20);
        check(!state.TryRespond(1, 1, 30, 10, 20, 26, 20, out _), "arrival does not create patrol/pursuit");
        state.Reset();
        for (int i = 1; i <= ContactMemoryState.MaxContacts + 1; i++) state.Observe(1, 1, 30, i, i, 20);
        check(!state.TryEstimate(1, 1, 30, ContactMemoryState.MaxContacts + 1, out _), "contact memory is bounded");

        (Actor Actor, Move Move, Scores Tiles, ContactMemoryState State) Setup()
        {
            var actor = new Actor();
            actor.Agent.Faction.Kind = 6;
            actor.Agent.Zones.Clear();
            var move = actor.Agent.Behaviors.OfType<Move>().Single();
            var near = new Tile(10, 0);
            var unsafeTile = new Tile(12, 0);
            var farther = new Tile(-1, 0);
            var tiles = new Scores
            {
                [actor.Tile] = new() { Tile = actor.Tile },
                [near] = new() { Tile = near },
                [unsafeTile] = new() { Tile = unsafeTile, SafetyScore = 122 },
                [farther] = new() { Tile = farther }
            };
            var memory = new ContactMemoryState();
            memory.Observe(1, 1, 30, 100, 30, 0);
            return (actor, move, tiles, memory);
        }
        bool Apply((Actor Actor, Move Move, Scores Tiles, ContactMemoryState State) f) =>
            ContactConvergenceRuntime.TrySeed(f.Move, f.Tiles, f.State, 1, 1, out _);
        var f = Setup();
        check(Apply(f), "production runtime seeds approach utility");
        check(f.Tiles.Values.Single(s => s.Tile.GetX() == 10).UtilityScore == f.Move.Threshold,
            "safe progress receives native threshold");
        check(f.Tiles.Values.Where(s => s.Tile.GetX() != 10).All(s => s.UtilityScore == 0),
            "current, farther and less-safe tiles unchanged");
        check(f.Tiles.Values.All(s => s.DistanceScore == 0 && s.APCost == 0 && s.UtilityScoreScaled == 456 && s.Path != null),
            "native distance, path, AP and scaled values untouched");
        check(!Apply(f), "existing utility prevents duplicate injection");
        foreach (Action<Actor> deny in new Action<Actor>[]
        {
            a => a.Agent.Zones.Add(new()), a => a.Agent.Zones = null,
            a => a.Agent.Sleeping = true, a => a.Agent.Deployment = true,
            a => a.Agent.Role.AvoidOpponents = true, a => a.Agent.Role.Move = 0,
            a => a.Agent.Role.Move = float.NaN, a => a.Moving = true, a => a.Dying = true,
            a => a.AP = 0, a => a.Morale = .5f, a => a.Morale = float.NaN,
            a => a.Suppression = 1, a => a.Contained = true, a => a.TilesMovedThisTurn = 1,
            a => a.Infantry = false, a => a.Vehicle = true, a => a.Agent.Faction.Allied = true,
            a => a.Agent.Behaviors.OfType<Attack>().Single().m_Candidates.Add(new()),
            a => a.Agent.Behaviors.OfType<Attack>().Single().Score = 1
        })
        {
            f = Setup(); deny(f.Actor);
            check(!Apply(f) && f.Tiles.Values.All(s => s.UtilityScore == 0), "mission/combat/actor guards fail without writes");
        }
        foreach (int kind in new[] { 0, 1, 2, 3, 4, 5, 7, 8 })
        { f = Setup(); f.Actor.Agent.Faction.Kind = kind; check(!Apply(f), "only pirate/Rogue factions eligible"); }
        f = Setup(); f.Actor.Agent.Faction.Kind = 9; check(Apply(f), "Rogue eligible");
        foreach (TagType tag in Enum.GetValues<TagType>())
        { f = Setup(); f.Actor.Tags.Add(tag); check(!Apply(f), "support/nonorganic tags excluded"); }
        foreach (Action<TileScore> deny in new Action<TileScore>[]
        {
            s => s.UtilityScore = 1, s => s.UtilityByAttacksScore = 1, s => s.UtilityByAttacksScoreCandidate = 1,
            s => s.DistanceScore = float.NaN, s => s.SafetyScore = float.NaN, s => s.UtilityScore = float.PositiveInfinity
        })
        { f = Setup(); deny(f.Tiles.Values.Last()); check(!Apply(f), "preserve existing utility and reject invalid scores"); }
        f = Setup(); f.Tiles.Remove(f.Actor.Tile); check(!Apply(f), "missing current tile rejected");
        f = Setup(); f.Move.Done = true; check(!Apply(f), "completed move preserved");
        f = Setup(); f.Move.Delaying = true; check(!Apply(f), "delayed move preserved");
        f = Setup(); f.Move.Threshold = float.NaN; check(!Apply(f), "invalid threshold rejected");
        f = Setup(); f.State.Reset(); check(!Apply(f), "no contact means no fabricated destination");
        f = Setup();
        TileScore.UtilityWritesBeforeThrow = 0;
        bool failed = false;
        try { Apply(f); } catch (InvalidOperationException) { failed = true; }
        check(failed && f.Tiles.Values.All(s => s.UtilityScore == 0), "failed native write rolls back");
        f = Setup(); var extra = new Tile(11, 0); f.Tiles.Add(extra, new() { Tile = extra });
        TileScore.UtilityWritesBeforeThrow = 1; failed = false;
        try { Apply(f); } catch (InvalidOperationException) { failed = true; }
        check(failed && f.Tiles.Values.All(s => s.UtilityScore == 0), "partial multi-tile write rolls back earlier edits");

        f = Setup(); state = new ContactMemoryState();
        var target = new Actor { Pointer = (IntPtr)100, Detected = true, Tile = new(30, 0), RejectHiddenReads = true };
        ContactConvergenceRuntime.Observe(f.Actor.Agent.Faction, target, state, 1, 1);
        target.Detected = false; target.Tile = new(80, 80); target.Dying = true;
        ContactConvergenceRuntime.Observe(f.Actor.Agent.Faction, target, state, 1, 2);
        check(target.PositionReads == 1 && state.TryEstimate(1, 2, 30, 100, out estimate) && estimate.X == 30,
            "lost contact never reads hidden live position or death");
        target.Detected = true; target.Dying = false;
        ContactConvergenceRuntime.Observe(f.Actor.Agent.Faction, target, state, 1, 2);
        check(state.TryEstimate(1, 2, 30, 100, out estimate) && estimate.X == 80, "legitimate reacquisition updates snapshot");
        target.Dying = true;
        ContactConvergenceRuntime.Observe(f.Actor.Agent.Faction, target, state, 1, 2);
        check(!state.TryEstimate(1, 2, 30, 100, out _), "observed death disproves contact without reading hidden deaths");
        target.Dying = false;
        state.Reset(); target.FactionKind = 3;
        ContactConvergenceRuntime.Observe(f.Actor.Agent.Faction, target, state, 1, 1);
        check(!state.TryEstimate(1, 1, 30, 100, out _), "civilian detections cannot create pursuit contacts");
        target.FactionKind = 1;

        Plugin.BeginContactFixture();
        TacticalManager.Instance = new();
        f = Setup(); target.Tile = new(30, 0);
        Plugin.ContactSightFixture(f.Actor.Agent.Faction, target);
        Plugin.ContactUtilityFixture(f.Move, f.Tiles);
        check(f.Tiles.Values.Any(s => s.UtilityScore > 0) && Plugin.Logs.Count == 0, "production detection/utility hooks work silently");
        Plugin.ResetContactFixture();
        f = Setup(); Plugin.ContactUtilityFixture(f.Move, f.Tiles);
        check(f.Tiles.Values.All(s => s.UtilityScore == 0), "mission reset removes hook memory");
        Plugin.BeginContactFixture(true); f = Setup();
        Plugin.ContactSightFixture(f.Actor.Agent.Faction, target);
        Plugin.RejectLog = true;
        Plugin.ContactUtilityFixture(f.Move, f.Tiles);
        check(Plugin.ContactEnabled && f.Tiles.Values.Any(s => s.UtilityScore > 0), "diagnostic failure does not disable behavior");
        Plugin.RejectLog = false; Plugin.ResetContactFixture();
        state = new ContactMemoryState(); state.Observe(1, 1, 30, 100, 30, 20);
        int responders = 0;
        Parallel.For(0, 100, i =>
        {
            if (state.TryRespond(1, 1, 30, 1000 + i, 2000 + i, 0, 0, out _)) Interlocked.Increment(ref responders);
        });
        check(responders == ContactMemoryState.MaxResponders, "concurrent workers cannot exceed faction response cap");
    }
}
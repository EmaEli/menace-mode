using AIPerformance;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;

internal static class CombinedArmsChecks
{
    private sealed class Fixture
    {
        internal readonly Actor Infantry = new() { Pointer = (IntPtr)100, Tile = new(12, 10), ID = 1 };
        internal readonly Actor Vehicle = new() { Pointer = (IntPtr)200, Tile = new(2, 10), Vehicle = true, Infantry = false };
        internal readonly InflictDamage Gun = new();
        internal readonly AIFaction Faction = new() { Kind = 9 };
        internal readonly TileScore Rear, Front, Unsafe;
        internal Fixture(bool heavy = false)
        {
            TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
            Infantry.Agent.Pointer = (IntPtr)101; Vehicle.Agent.Pointer = (IntPtr)201;
            Infantry.Agent.Faction = Vehicle.Agent.Faction = Faction;
            foreach (var actor in new[] { Infantry, Vehicle }) actor.Agent.Zones[0].Area = new(40, 10, 3, 3);
            Vehicle.Template.m_ID = "enemy.vehicle_rogue_army_auto_autocannon_walker";
            Vehicle.Tags.Add(TagType.MACHINE);
            if (heavy)
            {
                Vehicle.Vehicle = false; Vehicle.Infantry = true; Vehicle.Tags.Clear(); Vehicle.Tags.Add(TagType.WEAPONS_TEAM);
                Gun.Skill.Template.IsDeploymentRequired = true; Gun.Skill.Template.IsWeaponSetupRequired = true;
                Gun.m_DeployedStanceSkill = new() { Cost = 10 }; Gun.m_SetupWeaponSkill = new() { Cost = 15 };
            }
            Gun.Owner = Vehicle.Agent; Vehicle.Agent.Behaviors.Add(Gun);
            Faction.Actors.Add(Infantry); Faction.Actors.Add(Vehicle);
            TileScore Add(int x, int z, float danger = 10)
            {
                var tile = new Tile(x, z); var score = new TileScore { Tile = tile, SafetyScore = danger };
                Vehicle.Agent.Tiles[tile] = score; return score;
            }
            Vehicle.Agent.Tiles[Vehicle.Tile] = new() { Tile = Vehicle.Tile, SafetyScore = 10 };
            Rear = Add(6, 10); Front = Add(15, 10); Unsafe = Add(7, 12, 50);
            Plugin.BeginCombinedFixture(Faction);
        }
        internal void Plan() => Plugin.OffensiveScoresFixture(Vehicle.Agent);
    }

    internal static void Run(Action<bool, string> check)
    {
        var f = new Fixture(); f.Plan();
        check(f.Rear.UtilityScore > 0 && f.Rear.UtilityByAttacksScore == 0, "rear lane receives support utility, not a fabricated shot");
        check(f.Front.UtilityScore == 0 && f.Unsafe.UtilityScore == 0, "support stays behind infantry and never rewards worse native threat cost");
        check(f.Rear.SafetyScore == 10 && f.Unsafe.SafetyScore == 50 && f.Rear.APCost == 0, "AT danger, paths and movement AP stay native");
        check(f.Rear.MinimumUtilityAPCost == 20, "vehicle reserves a real shot");
        float first = f.Rear.UtilityScore; f.Plan(); check(f.Rear.UtilityScore == first, "repeated passes never inflate utility");
        f.Gun.Score = 5; f.Gun.m_Candidates.Add(new());
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, f.Vehicle.Agent.Behaviors[1]), f.Gun), "recognized crewed MACHINE vehicle prefers real native shot");
        check(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, new Behavior { ID = "Flee" }).ID == "Flee", "native emergency choice preserved");

        f = new(true); f.Plan();
        check(f.Rear.MinimumUtilityAPCost == 45 && f.Rear.UtilityScore > 0, "heavy weapon reserves deployment plus setup plus firing cost");
        check(!f.Vehicle.Deployed && !f.Vehicle.WeaponSetup, "position scoring never forces deployment/setup flags");
        check(SupportProjection.Reserve(f.Gun, f.Vehicle, false) == 45, "current unready weapon includes full setup cost");
        f.Vehicle.Deployed = f.Vehicle.WeaponSetup = true;
        check(SupportProjection.Reserve(f.Gun, f.Vehicle, false) == 20 && SupportProjection.Reserve(f.Gun, f.Vehicle, true) == 45,
            "moving re-reserves setup while holding a ready position does not");
        f = new(true); f.Gun.m_SetupWeaponSkill = null; f.Plan();
        check(f.Rear.UtilityScore == 0, "missing native setup skill fails closed");
        f = new(true); f.Vehicle.AP = 40; f.Plan();
        check(f.Rear.UtilityScore == 0, "insufficient setup-plus-shot AP cannot promise ready support");

        foreach (string guard in new[] { "robot", "defend", "no-order", "dead-request", "request-defense", "indirect", "ammo", "disabled", "stunned", "hidden", "blocked", "range" })
        {
            f = new();
            switch (guard)
            {
                case "robot": f.Vehicle.Template.m_ID = "enemy.construct_strider"; break;
                case "defend": f.Vehicle.Agent.Zones[0].Order = 2; break;
                case "no-order": f.Infantry.Agent.Zones.Clear(); break;
                case "dead-request": f.Infantry.Dying = true; break;
                case "request-defense": f.Infantry.Agent.Zones[0].Order = 2; break;
                case "indirect": f.Gun.Skill.Template.IsLineOfFireNeeded = false; break;
                case "ammo": f.Gun.Skill.Empty = true; break;
                case "disabled": f.Gun.Skill.Enabled = false; break;
                case "stunned": f.Vehicle.Stunned = true; break;
                case "hidden":
                    var enemy = new Actor { Pointer = (IntPtr)400, RejectHiddenReads = true };
                    f.Faction.Opponents.Add(new() { Actor = enemy }); break;
                case "blocked": foreach (var score in f.Vehicle.Agent.Tiles.Values) score.Tile.Sees = _ => false; break;
                case "range": f.Gun.Skill.Range = (_, _) => false; break;
            }
            Plugin.BeginCombinedFixture(f.Faction); f.Plan();
            check(guard == "hidden" ? f.Rear.UtilityScore > 0 : f.Rear.UtilityScore == 0, guard + " respects support eligibility/visibility/geometry");
        }
        f = new(); f.Infantry.AP = 0; Plugin.CombinedRefreshFixture(f.Infantry.Agent); f.Plan();
        check(f.Rear.UtilityScore > 0, "infantry that spent its AP still receives covering support");
        f = new(); f.Infantry.Dying = true; Plugin.CombinedRefreshFixture(f.Infantry.Agent); f.Plan();
        check(f.Rear.UtilityScore == 0, "main-thread death refresh releases the request before planning");
        f = new();
        var target = new Actor { Pointer = (IntPtr)401, Detected = true, Tile = new(17, 10) };
        f.Faction.Opponents.Add(new() { Actor = target }); f.Plan();
        check(f.Rear.UtilityByAttacksScore > 0, "detected threat creates genuine native damage positioning value");
        f = new(); f.Rear.Tile.Sees = _ => throw new Exception("native LOS failure"); f.Plan();
        check(!Plugin.CombinedEnabled && f.Rear.UtilityScore == 0, "native failure disables support without partial writes");
        f = new(); TileScore.UtilityWritesBeforeThrow = 1; f.Plan();
        check(!Plugin.CombinedEnabled && f.Rear.UtilityScore == 0 && f.Rear.MinimumUtilityAPCost == 1024,
            "partial scoring failure restores all utility and reserve edits");
        f = new(); f.Vehicle.Agent.Behaviors.Add(new Behavior { ID = "Buff", Score = 100 }); f.Plan();
        check(f.Rear.UtilityScore == 0, "useful native support action is not displaced by a covering move");
        f = new(); f.Gun.Score = 10; f.Gun.m_Candidates.Add(new());
        check(ReferenceEquals(OffensivePlanningRuntime.PreferShot(f.Vehicle.Agent, f.Vehicle.Agent.Behaviors[1], false), f.Vehicle.Agent.Behaviors[1]),
            "combined-arms OFF leaves MACHINE-vehicle shot selection native");
        // Regressions for the observed 1.6.1 spacing-only withdrawals.
        f = new(); f.Vehicle.Tile = new(10, 10); f.Vehicle.Agent.Tiles.Clear();
        var hold = new TileScore { Tile = f.Vehicle.Tile, SafetyScore = 10 };
        f.Vehicle.Agent.Tiles[hold.Tile] = hold; f.Vehicle.Agent.Tiles[f.Rear.Tile] = f.Rear;
        Plugin.BeginCombinedFixture(f.Faction); f.Plan();
        check(hold.UtilityScore > 0 && f.Rear.UtilityScore == 0, "near infantry: hold instead of backing up to the six-tile gap");
        var move = (Move)f.Vehicle.Agent.Behaviors[1]; move.TargetTile = f.Rear;
        check(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move).GetID() == "Idle", "native spacing-only withdrawal is held");
        f.Rear.SafetyScore = 5;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move), move), "genuine native danger reduction retained");
        f.Rear.SafetyScore = 10; f.Rear.UtilityByAttacksScore = 20;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move), move), "better real firing position retained");
        f.Rear.UtilityByAttacksScore = 0; f.Gun.Score = 10; f.Gun.m_Candidates.Add(new());
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move), f.Gun), "visible legal shot wins over withdrawal");
        f = new(); f.Rear.APCost = 81; f.Plan();
        check(f.Rear.UtilityScore == 0, "known movement cost cannot consume the twenty-AP firing reserve");
        f = new(); f.Rear.APCost = 80; f.Plan();
        check(f.Rear.UtilityScore > 0, "exact movement-plus-shot budget is accepted");
        move = (Move)f.Vehicle.Agent.Behaviors[1]; move.TargetTile = f.Rear;
        f.Rear.APCost = 81;
        check(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move).GetID() == "Idle",
            "native path computed after scoring cannot silently spend the firing reserve");
        f.Rear.APCost = 80;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move), move),
            "native clipped path retaining exactly the reserve may execute");
        f = new(); f.Vehicle.Agent.Zones[0].Area = new(41, 11, 3, 3); Plugin.BeginCombinedFixture(f.Faction); f.Plan();
        check(f.Rear.UtilityScore > 0, "neighboring native advance allocations share a support corridor");
        f = new(); f.Vehicle.Agent.Zones[0].Area = new(0, 10, 1, 1); Plugin.BeginCombinedFixture(f.Faction); f.Plan();
        check(f.Rear.UtilityScore == 0, "opposite explicit advance orders cannot be reassigned");
        foreach (var sample in new[] { (83, 70, 81, 72, 41, 54, 87, 71), (19, 51, 18, 37, 60, 50, 14, 49) })
        {
            f = new(); var sampleMap = TacticalManager.Instance.GetMap();
            f.Vehicle.Tile = sampleMap.GetTile(sample.Item1, sample.Item2);
            f.Infantry.Tile = sampleMap.GetTile(sample.Item3, sample.Item4);
            f.Vehicle.Agent.Zones[0].Area = f.Infantry.Agent.Zones[0].Area = new(sample.Item5, sample.Item6, 3, 3);
            f.Vehicle.Agent.Tiles.Clear();
            hold = new() { Tile = f.Vehicle.Tile, SafetyScore = 10 };
            var loggedRetreat = new TileScore { Tile = sampleMap.GetTile(sample.Item7, sample.Item8), SafetyScore = 10 };
            f.Vehicle.Agent.Tiles[hold.Tile] = hold; f.Vehicle.Agent.Tiles[loggedRetreat.Tile] = loggedRetreat;
            Plugin.BeginCombinedFixture(f.Faction); f.Plan();
            check(loggedRetreat.UtilityScore == 0, "captured 1.6.1 East/West withdrawal receives no new support seed");
            move = (Move)f.Vehicle.Agent.Behaviors[1]; move.TargetTile = loggedRetreat;
            check(Plugin.OffensiveChoiceFixture(f.Vehicle.Agent, move).GetID() == "Idle",
                "captured zero-attack-utility withdrawal is held unless native safety improves");
        }
        f = new(); int otherRefreshes = 0;
        Task.Run(() => { f.Infantry.Dying = true; Plugin.CombinedRefreshFixture(f.Infantry.Agent); Interlocked.Increment(ref otherRefreshes); }).GetAwaiter().GetResult();
        f.Plan(); check(otherRefreshes == 1 && f.Rear.UtilityScore > 0, "worker callbacks cannot mutate the main-thread ally snapshot");

        var state = new CombinedArmsState();
        var order = new AdvanceRecoveryOrder(40, 10, 3, 3, 1);
        SupportUnit infantry(long id, int x) => new(id, id + 100, SupportKind.Infantry, x, 10, order, true, 40, 10);
        var a = new SupportUnit(1, 11, SupportKind.Vehicle, 2, 10, order, true, 40, 10);
        var b = new SupportUnit(2, 12, SupportKind.Vehicle, 2, 10, order, true, 40, 10);
        var near = infantry(10, 12); var far = infantry(20, 18);
        state.Publish(1, 1, 9, new[] { a, b, near, far });
        check(!state.NeedsPublish(1, 1, 9) && !state.Publish(1, 1, 9, new[] { a, near }), "repeated lifecycle event cannot rebuild/renew snapshot");
        check(state.Candidates(1, 1, 9, a)[0] == near, "nearest compatible advancing infantry preferred");
        check(state.Claim(1, 1, 9, a, near) && state.Candidates(1, 1, 9, b)[0] == far, "one vehicle per request prevents dogpile");
        var heavy = new SupportUnit(2, 12, SupportKind.Heavy, 2, 10, order, true, 40, 10); state.Refresh(1, 1, 9, b.Actor, heavy);
        check(state.Claim(1, 1, 9, heavy, near), "heavy team and vehicle may jointly support a squad");
        near = infantry(10, 13); far = infantry(20, 4);
        state.Publish(1, 2, 9, new[] { a, heavy, near, far });
        check(state.Candidates(1, 2, 9, a)[0] == near, "short lease preserves advancing partner despite a newly closer squad");
        state.Publish(1, 3, 9, new[] { a, heavy, infantry(10, 14), far });
        check(state.Candidates(1, 3, 9, a)[0] == far, "lease expiry allows nearby reassignment");
        check(!state.Publish(2, 3, 9, new[] { a, near }) && state.Candidates(2, 3, 9, a).Length == 0,
            "late other-map callbacks cannot replace mission state");
        check(!state.Publish(1, 2, 9, new[] { a, near }), "round rewind cannot renew requests");
        state.Reset(); state.Publish(2, 1, 9, new[] { a, near });
        check(state.Candidates(2, 1, 9, a).Length == 1, "mission reset releases all previous ownership");
        state.Refresh(2, 1, 9, near.Actor, null);
        check(state.Candidates(2, 1, 9, a).Length == 0, "missing/dead anchor releases its assignment");
        state.Reset(); state.Publish(1, 1, 9, new[] { a, b, near });
        int owners = 0;
        Parallel.ForEach(new[] { a, b }, asset => { if (state.Claim(1, 1, 9, asset, near)) Interlocked.Increment(ref owners); });
        check(owners == 1, "concurrent supporters cannot claim the same type slot");
        state.Reset(); near = infantry(10, 12); state.Publish(1, 1, 9, new[] { a, near, far }); state.Claim(1, 1, 9, a, near);
        near = infantry(10, 13); state.Refresh(1, 1, 9, near.Actor, near);
        state.Publish(1, 2, 9, new[] { a, near, far });
        check(state.Candidates(1, 2, 9, a)[0] == near, "actual same-round movement refresh preserves lease evidence for next round");
        var changedOrder = new SupportUnit(near.Actor, near.Agent, near.Kind, near.X, near.Z, order with { X = 70 }, true, 70, 10);
        state.Refresh(1, 2, 9, near.Actor, changedOrder);
        check(!state.Claim(1, 2, 9, a, near), "changed complete orders invalidate stale worker request");
        var budget = new OffensivePlanningBudget(); int accepted = 0;
        Parallel.For(1, 100, actor =>
        {
            for (int i = 0; i < 48; i++)
                if (budget.Spend(1, 1, actor, actor + 100, 9)) Interlocked.Increment(ref accepted);
        });
        check(accepted == OffensivePlanningBudget.PerFactionRound, "concurrent units share a hard faction-round budget");
        check(budget.Spend(1, 2, 1, 101, 9, 4) && !budget.Spend(1, 1, 500, 600, 9), "new-round budget does not permit old-round replenishment");
        check(!budget.Spend(1, 2, 1, 999, 9), "reused actor address cannot renew an actor-round allowance");
    }
}
using AIPerformance;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;

internal static class ContactAdvanceChecks
{
    private sealed class Fixture
    {
        internal Actor Unit = new() { Pointer = (IntPtr)800, Tile = new(10, 20), ID = 2 };
        internal Actor Enemy = new() { Pointer = (IntPtr)900, Tile = new(70, 20), Detected = true, RejectHiddenReads = true };
        internal AIFaction Faction = new() { Kind = 9 };
        internal InflictDamage Gun = new();
        internal TileScore Here, Forward, Back;
        internal Fixture(bool vehicle = false, bool sleeping = false, bool diagnostics = false)
        {
            TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
            Unit.Agent.Faction = Faction; Unit.Agent.Zones.Clear(); Unit.Agent.Sleeping = sleeping;
            Unit.Agent.Pointer = (IntPtr)801;
            if (vehicle)
            {
                Unit.Vehicle = true; Unit.Infantry = false; Unit.Tags.Add(TagType.MACHINE);
                Unit.Template.m_ID = "enemy.vehicle_rogue_army_auto_autocannon_walker";
            }
            Gun.Owner = Unit.Agent; Gun.Skill.MaxRange = 25;
            Gun.Skill.Range = (target, origin) => Math.Abs(target.GetX() - origin.GetX()) <= Gun.Skill.MaxRange;
            Gun.Skill.Usable = Gun.Skill.Range;
            Unit.Agent.Behaviors.Add(Gun); Enemy.Attacks.Add(new() { MaxRange = 40 });
            Faction.Actors.Add(Unit); Faction.Opponents.Add(new() { Actor = Enemy });
            Scores(); Plugin.BeginAdvanceFixture(Faction, diagnostics);
        }
        internal void Scores()
        {
            Unit.Agent.Tiles.Clear();
            Here = Add(Unit.Tile, 50, 500); Forward = Add(new(Unit.Tile.GetX() + 6, 20), 30, 0, 30);
            Back = Add(new(Unit.Tile.GetX() - 1, 20), 500, 10000, 10);
        }
        internal TileScore Add(Tile tile, float safety, float utility, int cost = 0)
        {
            var score = new TileScore { Tile = tile, SafetyScore = safety, UtilityScore = utility, APCost = cost };
            Unit.Agent.Tiles[tile] = score; return score;
        }
        internal void Plan() => Plugin.OffensiveScoresFixture(Unit.Agent);
    }

    private static void BugsApproach(Action<bool, string> check)
    {
        var f = new Fixture(); f.Faction.Kind = 7;
        f.Unit.Infantry = false; f.Unit.Tags.Add(TagType.XENO);
        f.Unit.Template.m_ID = "enemy.alien_01_small_spiderling";
        f.Unit.Sight = (_, _, _) => true;
        Plugin.BeginAdvanceFixture(f.Faction);
        var step = f.Add(new(11, 20), 30, 0, 18);
        f.Forward.APCost = 90; f.Plan();
        var move = f.Unit.Agent.Behaviors.OfType<Move>().Single();
        var candidates = new Il2CppSystem.Collections.Generic.List<TileScore> { step, f.Forward };
        void Scale()
        {
            foreach (var score in candidates)
            {
                score.UtilityScoreScaled = score.UtilityScore; score.SafetyScoreScaled = 0;
                score.DistanceToCurrentTile = score.APCost / 10f * 1.5f;
            }
        }
        Scale();
        check(move.GetHighestTileScoreScaled(candidates) == step, "bugs reproduce native one-tile travel preference with visible troops");
        check(Plugin.MarchChoiceFixture(move, candidates) == f.Forward, "visible troops do not stop a bug's full affordable approach");

        // In range after moving: equal lane utility used to choose the cheapest step.
        f.Enemy.Tile = new(30, 20); Plugin.AdvanceSightFixture(f.Enemy, 7);
        foreach (var score in f.Unit.Agent.Tiles.Values) score.MinimumUtilityAPCost = 1024;
        f.Forward.APCost = 80; f.Plan(); Scale();
        check(step.MinimumUtilityAPCost == 20 && f.Forward.MinimumUtilityAPCost == 20 &&
            step.UtilityScore == f.Forward.UtilityScore, "bug attack lanes reserve actual attack AP with equal utility");
        check(Plugin.MarchChoiceFixture(move, candidates) == f.Forward, "bugs break equal-merit attack-lane ties by forward progress while reserving attack AP");
        object path = f.Forward.Path; float utility = f.Forward.UtilityScore;
        check(f.Unit.AP == 100 && f.Forward.APCost == 80 && f.Forward.MinimumUtilityAPCost == 20 &&
            f.Forward.Path == path && f.Forward.UtilityScore == utility, "bug approach leaves native path, scores and resources unchanged");
        f.Forward.APCost = 81; Scale();
        check(Plugin.MarchChoiceFixture(move, candidates) == step, "bug movement cannot spend reserved attack AP");
        f.Forward.APCost = 80; Scale();
        foreach (int invalid in new[] { -1, 1024, int.MaxValue })
        {
            f.Forward.MinimumUtilityAPCost = invalid;
            check(Plugin.MarchChoiceFixture(move, candidates) == step, "invalid or unaffordable bug attack reserve fails closed");
        }
        f.Forward.MinimumUtilityAPCost = 20; f.Forward.SafetyScoreScaled = -1000;
        check(Plugin.MarchChoiceFixture(move, candidates) == step, "bug charge retains native danger penalties");
        f.Forward.SafetyScoreScaled = 0; f.Forward.DistanceScore = 100;
        check(Plugin.MarchChoiceFixture(move, candidates) == step, "bug charge retains mission distance penalties");
        f.Forward.DistanceScore = 0;
        foreach (string id in new[] { "InflictDamage", "Confusion", "Flee" })
        {
            var action = new Behavior { ID = id, Score = 1, Owner = f.Unit.Agent };
            f.Unit.Agent.Behaviors.Add(action);
            check(Plugin.MarchChoiceFixture(move, candidates) == step, "bug charge preserves useful native " + id);
            f.Unit.Agent.Behaviors.Remove(action);
        }
        f.Gun.Score = 10; f.Gun.m_Candidates.Add(new());
        f.Gun.Skill.Template.m_ID = "active.alien_jump_attack";
        check(Plugin.OffensiveChoiceFixture(f.Unit.Agent, move) == f.Gun, "bugs prefer a legal native jump assault over more movement");
        f.Gun.Skill.Template.m_ID = "active.fire_alien_needle_spitter_stinger";
        check(Plugin.OffensiveChoiceFixture(f.Unit.Agent, move) == f.Gun, "bugs prefer a legal ordinary attack over more movement");
        f.Gun.Score = 0; f.Enemy.Detected = false; int reads = f.Enemy.PositionReads;
        check(Plugin.MarchChoiceFixture(move, candidates) == f.Forward && f.Enemy.PositionReads == reads,
            "bug approach uses last-known contact without hidden live-coordinate reads");
        f.Faction.Kind = 9;
        check(Plugin.MarchChoiceFixture(move, candidates) == step, "ranged humans do not inherit the bug-only attack-lane rush");
    }

    internal static void Run(Action<bool, string> check)
    {
        var f = new Fixture(sleeping: true);
        check(!f.Unit.Agent.Sleeping && f.Unit.AP == 100, "legitimate faction contact alerts unassigned sleeper without AP grants");
        f.Plan();
        check(f.Forward.UtilityScore > 0 && f.Here.UtilityScore == 0 && f.Back.UtilityScore == 0,
            "far contact: advance instead of stopping in current/backward high-utility cover");
        check(f.Forward.UtilityByAttacksScore == 0 && f.Forward.SafetyScore == 30, "range closing does not fabricate attack value or erase safety");
        check(f.Forward.MinimumUtilityAPCost == 0 && f.Forward.APCost == 30,
            "pure closing clears the 1024 post-utility sentinel but retains the real native movement AP cost");
        // Native GetScaledScore subtracts AP-derived travel distance * DistancePickScale.
        // Before this correction the modest progress gradient loses to a single cheap step.
        var step = f.Add(new(11, 20), 30, 0, 18);
        f.Forward.APCost = 90; f.Plan();
        foreach (var score in new[] { step, f.Forward })
        {
            score.UtilityScoreScaled = score.UtilityScore; score.SafetyScoreScaled = 0;
            score.DistanceToCurrentTile = score.APCost / 10f * 1.5f;
        }
        var march = new Il2CppSystem.Collections.Generic.List<TileScore> { step, f.Forward };
        var move = f.Unit.Agent.Behaviors.OfType<Move>().Single();
        check(move.GetHighestTileScoreScaled(march) == step, "native travel preference reproduces the one-square choice");
        check(Plugin.MarchChoiceFixture(move, march) == f.Forward, "unseen-contact approach chooses full affordable progress, not a cheap one-square step");
        object path = f.Forward.Path; float utility = f.Forward.UtilityScore;
        check(f.Unit.AP == 100 && f.Forward.APCost == 90 && f.Forward.Path == path && f.Forward.UtilityScore == utility &&
            f.Forward.DistanceToCurrentTile == 13.5f && f.Forward.SafetyScore == 30 && f.Forward.MinimumUtilityAPCost == 0,
            "march re-ranks existing native paths without changing safety, scores, resources or path costs");
        f.Unit.Sight = (_, _, _) => true;
        check(Plugin.MarchChoiceFixture(move, march) == step, "own native vision/LOS restores ordinary tactical movement");
        f.Unit.Sight = (_, _, _) => false;
        var visibleOther = new Actor { Pointer = (IntPtr)901, Detected = true, Tile = new(12, 21) };
        f.Faction.Opponents.Add(new() { Actor = visibleOther });
        f.Unit.Sight = (enemy, _, _) => enemy == visibleOther;
        check(Plugin.MarchChoiceFixture(move, march) == step, "seeing any opponent stops marching, not just the assigned contact");
        f.Faction.Opponents.RemoveAt(f.Faction.Opponents.Count - 1); f.Unit.Sight = (_, _, _) => false;
        f.Enemy.Detected = false; int hiddenReads = f.Enemy.PositionReads;
        check(Plugin.MarchChoiceFixture(move, march) == f.Forward && f.Enemy.PositionReads == hiddenReads,
            "last-known march never reads hidden live coordinates, vision or concealment");
        f.Enemy.Detected = true;
        f.Forward.SafetyScoreScaled = -1000;
        check(Plugin.MarchChoiceFixture(move, march) == step, "native danger can still defeat a longer march");
        f.Forward.SafetyScoreScaled = 0; f.Forward.DistanceScore = 100;
        check(Plugin.MarchChoiceFixture(move, march) == step, "mission-distance penalties remain native");
        f.Forward.DistanceScore = 0;
        foreach (string guard in new[] { "ap", "reserve", "nan", "done", "delaying", "inert", "shot", "defend", "allied", "deployment", "suppressed", "off" })
        {
            if (guard == "ap") f.Forward.APCost = 101;
            if (guard == "reserve") f.Forward.MinimumUtilityAPCost = 20;
            if (guard == "nan") f.Forward.DistanceToCurrentTile = float.NaN;
            if (guard == "done") move.Done = true;
            if (guard == "delaying") move.Delaying = true;
            if (guard == "inert") move.Inert = true;
            if (guard == "shot") f.Gun.Score = 1;
            if (guard == "defend") f.Unit.Agent.Zones.Add(new() { Order = 0 });
            if (guard == "allied") f.Faction.Allied = true;
            if (guard == "deployment") f.Unit.Agent.Deployment = true;
            if (guard == "suppressed") f.Unit.Suppression = .5f;
            if (guard == "off") Plugin.AdvanceResetFixture();
            check(Plugin.MarchChoiceFixture(move, march) == step, guard + " prevents march replacement");
            f.Forward.APCost = 90; f.Forward.MinimumUtilityAPCost = 0; f.Forward.DistanceToCurrentTile = 13.5f;
            move.Done = move.Delaying = move.Inert = false; f.Gun.Score = 0; f.Unit.Agent.Zones.Clear();
            f.Faction.Allied = false; f.Unit.Agent.Deployment = false; f.Unit.Suppression = 0;
        }
        Plugin.BeginAdvanceFixture(f.Faction, diagnostics: true);
        var workerMove = move; var workerMarch = march; var far = f.Forward;
        check(Task.Run(() => Plugin.MarchChoiceFixture(workerMove, workerMarch) == far).GetAwaiter().GetResult(),
            "native AI worker uses full affordable contact progress");
        check(Plugin.Logs.Any(line => line.Contains("phase=march-choice") && line.Contains("selected=(16,20) ap=90 available=100")),
            "bounded opt-in trace records actual native versus march goal and path AP");
        Plugin.RejectLog = true;
        check(Plugin.MarchChoiceFixture(move, march) == f.Forward && Plugin.ContactAdvanceOn,
            "optional trace failure neither disables contact advance nor changes the selected route");
        Plugin.RejectLog = false;
        foreach (int kind in Enumerable.Range(5, 5))
        {
            f.Faction.Kind = kind; Plugin.BeginAdvanceFixture(f.Faction);
            for (int i = 0; i < 10; i++)
                check(Plugin.MarchChoiceFixture(move, march) == f.Forward, kind + " repeated evaluations retain full-progress destination");
        }
        BugsApproach(check);
        f = new Fixture(); // Keep subsequent fresh-score/lane checks independent of this path-ranking fixture.
        f.Plan();
        for (int i = 0; i < 31; i++)
        {
            var squad = new Actor { Pointer = (IntPtr)(1000 + i), Tile = new(5 + i, 15) };
            squad.Agent.Faction = f.Faction; squad.Agent.Zones.Clear(); squad.Agent.Sleeping = true;
            f.Faction.Actors.Add(squad);
        }
        Plugin.BeginAdvanceFixture(f.Faction);
        check(f.Faction.Actors.Count(a => !a.Agent.Sleeping) == 32, "faction alert mobilizes all 32 eligible squads, not three leased responders");
        float first = f.Forward.UtilityScore;
        for (int i = 0; i < 50; i++) { f.Scores(); f.Plan(); check(f.Forward.UtilityScore == first, "fresh repeated scoring retains progress without inflation"); }
        var left = f.Add(new(16, 16), 30, 0, 30); var right = f.Add(new(16, 24), 30, 0, 30);
        f.Unit.ID = 0; f.Plan(); check(left.UtilityScore > right.UtilityScore, "left-sector squad favors stable left approach lane");
        f.Unit.ID = 4; f.Plan(); check(right.UtilityScore > left.UtilityScore, "right-sector squad favors distinct right approach lane");

        f = new Fixture(vehicle: true, sleeping: true); f.Plan();
        check(!f.Unit.Agent.Sleeping && f.Forward.UtilityScore > 0, "crewed MACHINE vehicle closes independently of infantry requests");
        f.Gun.Score = 1; f.Gun.m_Candidates.Add(new());
        check(Plugin.OffensiveChoiceFixture(f.Unit.Agent, f.Unit.Agent.Behaviors[0]) == f.Gun, "vehicle fires its positive legal native attack instead of moving");

        f = new Fixture(); f.Enemy.Detected = false; int reads = f.Enemy.PositionReads;
        for (int round = 2; round <= 9; round++)
        {
            TacticalManager.Instance.m_Round = round; f.Unit.Tile = new(10 + (round - 2) * 4, 20);
            f.Scores(); Plugin.AdvanceBeforeFixture(f.Unit.Agent); f.Plan();
            check(f.Forward.UtilityScore > 0, "last-known approach persists beyond old three-turn lease");
        }
        check(f.Enemy.PositionReads == reads, "lost contact never refreshes hidden coordinates or weapons");
        f.Unit.Tile = new(68, 20); f.Scores(); f.Plan();
        check(f.Forward.UtilityScore == 0, "arrival searches last-known area instead of following the hidden actor");
        TacticalManager.Instance.m_Round = 14; f.Scores(); f.Plan();
        check(f.Forward.UtilityScore == 0, "old area expires rather than becoming permanent hidden pursuit");

        f = new Fixture(); f.Enemy.Tile = new(40, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        var outside = f.Add(new(14, 20), 0, 2000, 20); f.Plan();
        check(f.Forward.UtilityScore > outside.UtilityScore && f.Forward.MinimumUtilityAPCost == 20,
            "cover in own firing range outranks safer cover outside range and reserves shot AP");
        check(f.Forward.UtilityByAttacksScore == 0, "projected lane never masquerades as an executed shot");
        var smoke = Plugin.GetSupportSmokeThreat(f.Unit.Agent);
        check(smoke == null, "enemy weapon range alone does not justify preventive smoke thirty tiles away");
        f.Enemy.Tile = new(20, 20); f.Gun.Skill.MaxRange = 5; Plugin.AdvanceSightFixture(f.Enemy, 9);
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) is [20, 20], "ordinary infantry requests smoke for a known threat exactly ten tiles away");
        f.Enemy.Detected = false; int smokeReads = f.Enemy.PositionReads;
        check(Plugin.GetSupportMortarSmokeThreat(f.Unit.Agent) is [20, 20] && f.Enemy.PositionReads == smokeReads,
            "recent hidden contact uses only its observed tile and does not require an equipped grenade");
        TacticalManager.Instance.m_Round += 3;
        check(Plugin.GetSupportMortarSmokeThreat(f.Unit.Agent) == null, "stale contact cannot request preventive mortar smoke");
        TacticalManager.Instance.m_Round -= 3; f.Enemy.Detected = true;
        f.Enemy.Tile = new(21, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "eleven-tile contact cannot request preventive hand smoke");
        f.Enemy.Tile = new(20, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        f.Gun.Skill.MaxRange = 40;
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "do not smoke a usable own firing lane");
        f.Gun.Skill.MaxRange = 10; f.Enemy.Tile = new(65, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "do not waste smoke outside observed enemy weapon range");
        f.Enemy.Tile = new(20, 20); f.Gun.Skill.MaxRange = 5; Plugin.AdvanceSightFixture(f.Enemy, 9);
        TacticalManager.Instance.Map.GetTile(20, 20).Sees = _ => false;
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "covered approach does not request smoke merely because it is in range");

        f = new Fixture(); f.Enemy.Tile = new(40, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        var safestLane = f.Add(new(15, 20), 0, 0, 20);
        for (int z = 12; z <= 28; z++) if (z != 20) f.Add(new(16, z), 100 + z, 0, 20);
        f.Plan();
        check(safestLane.MinimumUtilityAPCost == 20, "bounded lane probes consider lowest native danger first, not the highest SafetyScore");

        f = new Fixture(); f.Enemy.Tile = new(40, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        f.Gun.Skill.Template.IsDeploymentRequired = f.Gun.Skill.Template.IsWeaponSetupRequired = true;
        f.Gun.m_DeployedStanceSkill = new() { Cost = 10 }; f.Gun.m_SetupWeaponSkill = new() { Cost = 15 };
        f.Unit.Tags.Add(TagType.WEAPONS_TEAM); f.Plan();
        check(f.Forward.MinimumUtilityAPCost == 45 && f.Unit.AP == 100 && !f.Unit.WeaponSetup,
            "weapon team reserves real deployment/setup/shot cost without free setup");
        for (int i = 0; i < 20; i++)
        {
            f.Scores(); f.Plan();
            check(f.Forward.MinimumUtilityAPCost == 45 && f.Forward.UtilityScore > 0, "lane staging survives exhausted expensive-probe budget");
        }
        var mg = new InflictSuppression { Owner = f.Unit.Agent, Score = 1 }; mg.m_Candidates.Add(new());
        f.Unit.Agent.Behaviors.Add(mg);
        check(Plugin.OffensiveChoiceFixture(f.Unit.Agent, f.Unit.Agent.Behaviors[0]) == mg, "positive native sustained/suppression fire takes precedence over Idle");

        f = new Fixture(); f.Gun.Skill.MaxRange = 100; f.Plan();
        check(f.Here.UtilityScore == 0 && f.Forward.UtilityScore > 0,
            "a projected current-origin lane with no native shot cannot park the unit or cause cover shuffling");

        foreach (string guard in new[] { "no-contact", "defend", "deployment", "allied", "broken", "emergency" })
        {
            f = new Fixture();
            if (guard == "no-contact") Plugin.AdvanceResetFixture();
            if (guard == "defend") f.Unit.Agent.Zones.Add(new() { Order = 0 });
            if (guard == "deployment") f.Unit.Agent.Deployment = true;
            if (guard == "allied") f.Faction.Allied = true;
            if (guard == "broken") f.Unit.Morale = .1f;
            if (guard == "emergency") f.Unit.Agent.Behaviors.Add(new() { ID = "Flee", Score = 10 });
            f.Plan(); check(f.Forward.UtilityScore == 0, guard + " retains native decisions");
        }

        f = new Fixture(); f.Enemy.Detected = false; Plugin.AdvanceResetFixture(); f.Unit.Agent.Sleeping = true;
        Task.Run(() => Plugin.AdvanceSightFixture(f.Enemy, 9)).GetAwaiter().GetResult();
        Plugin.AdvanceBeforeFixture(f.Unit.Agent);
        check(f.Unit.Agent.Sleeping, "hidden/worker event cannot alert faction");
        f.Enemy.Detected = true; Plugin.AdvanceSightFixture(f.Enemy, 9); Plugin.AdvanceBeforeFixture(f.Unit.Agent);
        check(!f.Unit.Agent.Sleeping, "fresh main-thread contact can alert after mission reset");
        TileScore.UtilityWritesBeforeThrow = 1; f.Plan();
        check(!Plugin.ContactAdvanceOn && f.Here.UtilityScore == 500 && f.Back.UtilityScore == 10000,
            "partial setter failure restores native scores and disables only contact advance");
        var memory = new ContactAdvanceState(); memory.Observe(1, 1, 6, new(10, 30, 30, 1, 20));
        check(!memory.TryAim(1, 1, 9, 0, 0, out _), "contact memory is faction-isolated");
        check(!memory.TryAim(2, 1, 6, 0, 0, out _), "different map cannot inherit contact memory without lifecycle reset");
        memory.TryAim(1, 3, 6, 0, 0, out _);
        check(!memory.TryAim(1, 2, 6, 0, 0, out _), "old worker cannot rewind contact memory");
        memory.Reset(); check(!memory.TryAim(2, 1, 6, 0, 0, out _), "mission reset clears all known areas");

        // Scoped advance-zone override: native ConsiderZones skips AdvanceTowards zones on faction contact.
        f = new Fixture(); f.Unit.Agent.Zones.Add(new());
        check(Plugin.KnownOpponentFixture(f.Faction), "outside ConsiderZones the faction contact flag stays native");
        bool state = Plugin.ZoneEnterFixture(f.Unit);
        check(!state && !Plugin.KnownOpponentFixture(f.Faction), "ready advance-ordered unit without a useful shot keeps native advance zones after contact");
        Plugin.ZoneExitFixture(state);
        check(Plugin.KnownOpponentFixture(f.Faction), "override ends with the ConsiderZones call");
        f.Gun.Score = 5; Plugin.ZoneEnterFixture(f.Unit);
        check(!Plugin.KnownOpponentFixture(f.Faction), "eligibility is read once per scoring pass");
        Plugin.ZoneDoneFixture(false); Plugin.ZoneEnterFixture(f.Unit);
        check(Plugin.KnownOpponentFixture(f.Faction), "unit with a positive native attack holds and fires; no override");
        Plugin.ZoneDoneFixture(false);
        var tank = new Fixture(vehicle: true); tank.Unit.Agent.Zones.Add(new()); Plugin.ZoneEnterFixture(tank.Unit);
        check(!Plugin.KnownOpponentFixture(tank.Faction), "crewed vehicles with advance orders push too");
        Plugin.ZoneDoneFixture(false);
        foreach (string guard in new[] { "no-zones", "defend", "suppressed", "broken", "spent", "allied", "deployment" })
        {
            f = new Fixture(); if (guard != "no-zones") f.Unit.Agent.Zones.Add(new());
            if (guard == "defend") f.Unit.Agent.Zones[0].Order = 2;
            if (guard == "suppressed") f.Unit.Suppression = .5f;
            if (guard == "broken") f.Unit.Morale = .5f;
            if (guard == "spent") f.Unit.AP = 0;
            if (guard == "allied") f.Faction.Allied = true;
            if (guard == "deployment") f.Unit.Agent.Deployment = true;
            Plugin.ZoneEnterFixture(f.Unit);
            check(Plugin.KnownOpponentFixture(f.Faction), guard + " keeps the native contact flag");
            Plugin.ZoneDoneFixture(false);
        }
        f = new Fixture(); f.Unit.Agent.Zones.Add(new());
        var workerUnit = f;
        bool workerScoped = Task.Run(() =>
        {
            bool previous = Plugin.ZoneEnterFixture(workerUnit.Unit);
            bool active = !Plugin.KnownOpponentFixture(workerUnit.Faction);
            Plugin.ZoneDoneFixture(previous);
            return active && Plugin.KnownOpponentFixture(workerUnit.Faction);
        }).GetAwaiter().GetResult();
        check(workerScoped, "AI worker opens eligible native-zone scope and restores its native contact flag");
        check(Plugin.KnownOpponentFixture(f.Faction), "worker override never leaks onto the main thread");
        state = Plugin.ZoneEnterFixture(f.Unit);
        check(Plugin.KnownOpponentFixture(new AIFaction { Pointer = (IntPtr)31 }), "zone scope cannot override another faction");
        Plugin.ZoneExceptionFixture(state);
        check(Plugin.KnownOpponentFixture(f.Faction), "native criterion exception restores contact scope");
        using (var entered = new ManualResetEventSlim())
        using (var reset = new ManualResetEventSlim())
        {
            var staleWorker = Task.Run(() =>
            {
                bool previous = Plugin.ZoneEnterFixture(workerUnit.Unit);
                entered.Set(); reset.Wait();
                bool invalidated = Plugin.KnownOpponentFixture(workerUnit.Faction);
                Plugin.ZoneDoneFixture(previous);
                return invalidated;
            });
            entered.Wait(); Plugin.AdvanceBeforeFixture(f.Unit.Agent); reset.Set();
            check(staleWorker.GetAwaiter().GetResult(), "new processing generation invalidates a stale worker scope");
        }
        Plugin.ZoneEnterFixture(f.Unit); Plugin.AdvanceBeforeFixture(f.Unit.Agent);
        check(Plugin.KnownOpponentFixture(f.Faction), "agent processing start clears a stale scope");
        Plugin.ZoneEnterFixture(f.Unit); Plugin.AdvanceResetFixture(); Plugin.BeginOffensiveFixture();
        check(Plugin.KnownOpponentFixture(f.Faction), "disabled contact advance never overrides the native flag");

        f = new Fixture(diagnostics: true);
        check(Plugin.ContactAdvanceDiagnosticsOn && Plugin.Logs.Any(line => line.Contains("CONTACT-ADVANCE round=1") &&
            line.Contains("phase=turn") && line.Contains("contacts=1")),
            "opt-in turn trace reports the refreshed contact count");
        f.Unit.Agent.Zones.Add(new());
        state = Plugin.ZoneEnterFixture(f.Unit);
        check(!Plugin.KnownOpponentFixture(f.Faction), "diagnostic scope still performs the scoped native override");
        Plugin.ZoneExitFixture(state); Plugin.ZoneDoneFixture(false);
        check(Plugin.Logs.Any(line => line.Contains("phase=scope-eligibility") && line.Contains("result=open gate=Eligible")) &&
            Plugin.Logs.Any(line => line.Contains("phase=known-check-fixture") && line.Contains("result=overridden")),
            "opt-in trace proves scope eligibility and native check interception");
        f.Enemy.Detected = false; int readsBeforeUnseen = f.Enemy.PositionReads;
        Plugin.AdvanceSightFixture(f.Enemy, 9);
        check(f.Enemy.PositionReads == readsBeforeUnseen && Plugin.Logs.Any(line =>
            line.Contains("phase=detection") && line.Contains("result=NotDetected")),
            "detection trace records rejection without reading hidden position or weapons");
        TacticalManager.Instance.m_Round = 2; Plugin.AdvanceBeforeFixture(f.Unit.Agent);
        state = Plugin.ZoneEnterFixture(f.Unit); Plugin.ZoneExitFixture(state); Plugin.ZoneDoneFixture(false);
        check(Plugin.Logs.Any(line => line.Contains("round=2") && line.Contains("phase=known-check-fixture") &&
            line.Contains("result=not-read")), "opt-in trace distinguishes a missing native known-opponent read");

        f = new Fixture(diagnostics: true);
        workerUnit = f;
        Task.Run(() =>
        {
            bool previous = Plugin.ZoneEnterFixture(workerUnit.Unit);
            Plugin.KnownOpponentFixture(workerUnit.Faction);
            Plugin.ZoneDoneFixture(previous);
        }).GetAwaiter().GetResult();
        check(Plugin.Logs.Any(line => line.Contains("phase=scope-eligibility") && line.Contains("result=open")),
            "opt-in trace records eligible worker scoring instead of rejecting the AI thread");
        Plugin.RejectLog = true;
        state = Plugin.ZoneEnterFixture(f.Unit); Plugin.KnownOpponentFixture(f.Faction); Plugin.ZoneExitFixture(state);
        Plugin.RejectLog = false;
        check(Plugin.ContactAdvanceOn, "diagnostic sink failures do not disable contact advance");

        var diagnosticBudget = new ContactAdvanceDiagnosticBudget(); int accepted = 0;
        for (int i = 0; i < ContactAdvanceDiagnosticBudget.MaxLines + 20; i++)
            if (diagnosticBudget.TryAcquire(1, 1, i + 1, "bounded")) accepted++;
        check(accepted == ContactAdvanceDiagnosticBudget.HookLines && !diagnosticBudget.TryAcquire(1, 1, 1, "bounded"),
            "early worker/zone traces cannot consume the 128 reserved group-action records");
        int groupLines = 0;
        for (int i = 0; i < ContactAdvanceDiagnosticBudget.MaxLines + 20; i++)
            if (diagnosticBudget.TryAcquire(i + 1, 1, i + 1, "group-signal")) groupLines++;
        check(accepted + groupLines == ContactAdvanceDiagnosticBudget.MaxLines, "hook plus group diagnostics still cap at 256 per mission");
        diagnosticBudget.Reset();
        check(diagnosticBudget.TryAcquire(1, 1, 1, "bounded"), "contact diagnostic budget resets cleanly");
        for (int i = 1; i <= 3; i++) check(diagnosticBudget.TryAcquire(20, 1, i, "group-choice"), "late group choices have their reserved budget");
        check(!diagnosticBudget.TryAcquire(20, 1, 4, "group-choice") && diagnosticBudget.TryAcquire(20, 1, 4, "group-signal"),
            "sample repeated group choices while retaining actual late fire/smoke receipts");
    }
}
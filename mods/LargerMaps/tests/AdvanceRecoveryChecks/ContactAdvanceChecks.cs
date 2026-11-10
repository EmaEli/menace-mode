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
        internal Fixture(bool vehicle = false, bool sleeping = false)
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
            Scores(); Plugin.BeginAdvanceFixture(Faction);
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

    internal static void Run(Action<bool, string> check)
    {
        var f = new Fixture(sleeping: true);
        check(!f.Unit.Agent.Sleeping && f.Unit.AP == 100, "legitimate faction contact alerts unassigned sleeper without AP grants");
        f.Plan();
        check(f.Forward.UtilityScore > 0 && f.Here.UtilityScore == 0 && f.Back.UtilityScore == 0,
            "far contact: advance instead of stopping in current/backward high-utility cover");
        check(f.Forward.UtilityByAttacksScore == 0 && f.Forward.SafetyScore == 30, "range closing does not fabricate attack value or erase safety");
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
        check(smoke is { Length: 2 } && smoke[0] == 40, "ordinary unassigned infantry requests smoke inside enemy range but outside own");
        f.Gun.Skill.MaxRange = 40;
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "do not smoke a usable own firing lane");
        f.Gun.Skill.MaxRange = 10; f.Enemy.Tile = new(65, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        check(Plugin.GetSupportSmokeThreat(f.Unit.Agent) == null, "do not waste smoke outside observed enemy weapon range");
        f.Enemy.Tile = new(40, 20); Plugin.AdvanceSightFixture(f.Enemy, 9);
        TacticalManager.Instance.Map.GetTile(40, 20).Sees = _ => false;
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

        foreach (string guard in new[] { "no-contact", "defend", "deployment", "allied", "drone", "broken", "emergency" })
        {
            f = new Fixture();
            if (guard == "no-contact") Plugin.AdvanceResetFixture();
            if (guard == "defend") f.Unit.Agent.Zones.Add(new() { Order = 0 });
            if (guard == "deployment") f.Unit.Agent.Deployment = true;
            if (guard == "allied") f.Faction.Allied = true;
            if (guard == "drone") f.Unit.Tags.Add(TagType.DRONE);
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
        foreach (string guard in new[] { "no-zones", "defend", "suppressed", "broken", "spent", "drone", "allied", "deployment", "indirect" })
        {
            f = new Fixture(); if (guard != "no-zones") f.Unit.Agent.Zones.Add(new());
            if (guard == "defend") f.Unit.Agent.Zones[0].Order = 2;
            if (guard == "suppressed") f.Unit.Suppression = .5f;
            if (guard == "broken") f.Unit.Morale = .5f;
            if (guard == "spent") f.Unit.AP = 0;
            if (guard == "drone") f.Unit.Tags.Add(TagType.DRONE);
            if (guard == "allied") f.Faction.Allied = true;
            if (guard == "deployment") f.Unit.Agent.Deployment = true;
            if (guard == "indirect") f.Unit.Tags.Add(TagType.INDIRECT_FIRE);
            Plugin.ZoneEnterFixture(f.Unit);
            check(Plugin.KnownOpponentFixture(f.Faction), guard + " keeps the native contact flag");
            Plugin.ZoneDoneFixture(false);
        }
        f = new Fixture(); f.Unit.Agent.Zones.Add(new());
        Task.Run(() => Plugin.ZoneEnterFixture(f.Unit)).GetAwaiter().GetResult();
        check(Plugin.KnownOpponentFixture(f.Faction), "worker-thread scoring cannot arm the override");
        Plugin.ZoneEnterFixture(f.Unit); Plugin.AdvanceBeforeFixture(f.Unit.Agent);
        check(Plugin.KnownOpponentFixture(f.Faction), "agent processing start clears a stale scope");
        Plugin.ZoneEnterFixture(f.Unit); Plugin.AdvanceResetFixture(); Plugin.BeginOffensiveFixture();
        check(Plugin.KnownOpponentFixture(f.Faction), "disabled contact advance never overrides the native flag");
    }
}
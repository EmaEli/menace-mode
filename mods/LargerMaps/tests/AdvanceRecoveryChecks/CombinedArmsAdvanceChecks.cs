using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Skills;

internal static class CombinedArmsAdvanceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var contact = new AdvanceContact(90, 40, 20, 1, 40);
        SupportUnit Unit(long id, int x, SupportKind kind = SupportKind.Infantry) => new(id, id + 100, kind, x, 20, default, true, 40, 20);
        var a = Unit(1, 10); var b = Unit(2, 14); var v = Unit(3, 12, SupportKind.Vehicle);
        var state = new CombinedArmsAdvanceState();
        AdvanceGroupMember[] Members(params SupportUnit[] units) => units.Select(u => new AdvanceGroupMember(u, contact)).ToArray();
        check(state.Publish(1, 1, 9, Members(a, b, v)), "publish one local infantry/support group");
        state.TryGet(1, 1, 9, a, true, out var first); state.TryGet(1, 1, 9, v, true, out var vehicle);
        check(first.Group == vehicle.Group && first.Infantry == 2 && first.Support == 1 && first.Phase == AdvanceGroupPhase.Rally,
            "infantry and vehicle share the same roster, deadline and phase, not independent leases");
        check(!state.TryGet(1, 1, 6, a, true, out _) && !state.TryGet(2, 1, 9, a, true, out _), "groups isolate faction and map");
        check(!state.Signal(1, 1, 9, a.Actor, 9, 20, true), "backward smoke cannot release a crossing");
        check(state.Signal(1, 1, 9, a.Actor, 11, 20, true), "accepted forward smoke opens one shared crossing window");
        state.TryGet(1, 1, 9, b, true, out var crossing);
        check(crossing.Phase == AdvanceGroupPhase.Cross && !crossing.CanSmoke && !crossing.FireRecent,
            "peer sees smoke receipt; a different activation cannot repeatedly request smoke without progress");
        for (int round = 2; round <= 12; round++)
        {
            contact = contact with { SeenRound = round };
            state.Publish(1, round, 9, Members(b, v)); // Original leader dies; shared deadline must survive.
            state.TryGet(1, round, 9, b, true, out var next);
            check(next.Group == first.Group && next.Phase == (round == 2 ? AdvanceGroupPhase.Cross : AdvanceGroupPhase.Fallback),
                "leader loss/republication does not renew the shared two-round wait or stale smoke");
        }
        check(!state.Publish(1, 11, 9, Members(a)) && !state.TryGet(1, 11, 9, b, true, out _), "old worker cannot rewind the group frame");
        check(state.Signal(1, 12, 9, 999, 40, 20, false), "real friendly artillery fire at the contact sector can support a mobile group");
        state.TryGet(1, 12, 9, v, true, out crossing);
        check(crossing.FireRecent && crossing.Phase == AdvanceGroupPhase.Cross, "actual fire opens a common infantry/vehicle advance window");
        contact = contact with { Target = 91, SeenRound = 13 };
        state.Publish(1, 13, 9, Members(b, v)); state.TryGet(1, 13, 9, b, true, out var changed);
        check(changed.Phase == AdvanceGroupPhase.Fallback, "switching to another legitimate contact does not restart spent waiting time");
        state.Refresh(1, 13, 9, v.Actor, null);
        check(!state.TryGet(1, 13, 9, v, true, out _), "death invalidates the acting member immediately");
        state.Reset(); check(!state.TryGet(1, 12, 9, b, true, out _), "scene reset clears groups and receipts");
        contact = contact with { SeenRound = 1 };
        var crowd = Enumerable.Range(1, CombinedArmsState.MaxUnits).Select(i => Unit(i, 10 + i % 3)).ToArray();
        state.Publish(1, 1, 9, Members(crowd));
        foreach (var member in crowd)
            check(state.TryGet(1, 1, 9, member, false, out var limited) && limited.Infantry <= CombinedArmsAdvanceState.MaxInfantry,
                "crowded faction retains bounded small groups rather than one global all-pairs formation");
        check(!state.Signal(1, 1, 9, crowd[0].Actor, 70, 0, false), "fire at a different sector cannot certify this crossing");
        state.Signal(1, 1, 9, crowd[0].Actor, 40, 20, false);
        state.TryGet(1, 1, 9, crowd[^1], true, out var sharedFire);
        check(sharedFire.FireRecent && sharedFire.Phase == AdvanceGroupPhase.Cross,
            "one group's real fire opens the same contact-sector window for neighboring groups, not just its own members");
        state.Reset();

        // Execute the production publication, smoke request, successful-action hook, scoring and choice.
        TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
        var faction = new AIFaction { Kind = 9 };
        Actor Friendly(int id, int x, bool tank = false)
        {
            var unit = new Actor { Pointer = (IntPtr)id, Tile = new(x, 20), ID = id };
            unit.Agent.Pointer = (IntPtr)(id + 100); unit.Agent.Faction = faction;
            unit.Agent.Zones[0].Area = new(70, 18, 4, 4);
            if (tank)
            { unit.Vehicle = true; unit.Infantry = false; unit.Tags.Add(Il2CppMenace.Tags.TagType.MACHINE); unit.Template.m_ID = "enemy.vehicle_rogue_army_auto_laser_walker"; }
            var gun = new InflictDamage { Owner = unit.Agent }; gun.Skill.MaxRange = 5;
            gun.Skill.Range = (target, origin) => Math.Abs(target.GetX() - origin.GetX()) <= 5;
            gun.Skill.Usable = gun.Skill.Range; unit.Agent.Behaviors.Add(gun); faction.Actors.Add(unit);
            return unit;
        }
        var infantry = Friendly(201, 10); var peer = Friendly(202, 14); var walker = Friendly(203, 12, true);
        var enemy = new Actor { Pointer = (IntPtr)290, Tile = new(20, 20), Detected = true, RejectHiddenReads = true };
        enemy.Attacks.Add(new() { MaxRange = 40 }); faction.Opponents.Add(new() { Actor = enemy });
        Plugin.BeginAdvanceFixture(faction); Plugin.PublishGroupsFixture(faction);
        var move = (Move)infantry.Agent.Behaviors[1]; var tankMove = (Move)walker.Agent.Behaviors[1];
        var step = new Tile(11, 20); var onward = new Tile(16, 20);
        var stepScore = new TileScore { Tile = step, APCost = 10, SafetyScore = 0 };
        infantry.Agent.Tiles[infantry.Tile] = new() { Tile = infantry.Tile };
        infantry.Agent.Tiles[step] = stepScore;
        infantry.Agent.Tiles[onward] = new() { Tile = onward, APCost = 20, SafetyScore = 0 };
        move.TargetTile = stepScore;
        tankMove.TargetTile = new() { Tile = new(15, 20), APCost = 20 };
        check(Plugin.GroupChoiceFixture(infantry.Agent, move).GetID() == "Idle", "exposed lead infantry stages for its real nearby support");
        check(Plugin.GroupChoiceFixture(walker.Agent, tankMove).GetID() == "Idle", "vehicle does not independently rush ahead of its infantry screen");
        var smoke = new Skill { Entity = infantry, Template = new() { m_ID = "active.throw_smoke_grenade", IsAttack = false } };
        infantry.Skills.Skills.Add(smoke);
        check(Plugin.GetSupportSmokeThreat(infantry.Agent) is { Length: 2 }, "equipped affordable smoke is requested only with a feasible forward move");
        smoke.Cost = 100;
        check(Plugin.GetSupportSmokeThreat(infantry.Agent) == null, "no grenade request when its real cost leaves no movement AP");
        check(Plugin.GetSupportMortarSmokeThreat(infantry.Agent) is [20, 20], "mortar support does not spend requesting infantry AP");
        smoke.Cost = 20;
        Plugin.GroupUsedFixture(smoke, step, false);
        check(Plugin.GroupChoiceFixture(infantry.Agent, move).GetID() == "Idle", "failed native Use does not certify fire/smoke support");
        Plugin.GroupUsedFixture(smoke, step, true);
        check(Plugin.GetSupportSmokeThreat(infantry.Agent) == null, "accepted smoke prevents repeated same-place grenade requests");
        check(Plugin.GroupChoiceFixture(infantry.Agent, move).GetID() == "Idle", "accepted smoke must actually materialize before an exposed crossing");
        Plugin.BindSmokeFixture(tile => tile.GetX() == 11 && tile.GetZ() == 20);
        move.m_Path.Add(new(11, 0, 20));
        check(Plugin.GroupChoiceFixture(infantry.Agent, move) == move, "real smoke releases the protected native crossing");
        Plugin.OffensiveScoresFixture(infantry.Agent);
        check(stepScore.UtilityScore > infantry.Agent.Tiles[onward].UtilityScore && stepScore.UtilityByAttacksScore == 0,
            "smoke creates a preferred forward step, not fabricated attack utility or an idle cosmetic cloud");
        check(stepScore.MinimumUtilityAPCost == 0 && stepScore.APCost == 10,
            "smoke crossing is affordable movement rather than a 1024-AP phantom follow-up attack");
        check(infantry.AP == 100 && walker.AP == 100, "planning never grants or spends resources outside native execution");
        Plugin.BindSmokeFixture(_ => false);
        var gunSkill = new Skill { Entity = walker, Template = new() { IsAttack = true } };
        Plugin.GroupUsedFixture(gunSkill, enemy.Tile, true);
        check(Plugin.GroupChoiceFixture(infantry.Agent, move) == move, "real vehicle fire releases infantry even without smoke");
        check(Plugin.GroupChoiceFixture(walker.Agent, tankMove).GetID() == "Idle", "a fire window does not waive the vehicle's infantry screen");
        var gunBehavior = infantry.Agent.Behaviors.OfType<InflictDamage>().Single();
        gunBehavior.Score = 1; gunBehavior.m_Candidates.Add(new());
        check(Plugin.OffensiveChoiceFixture(infantry.Agent, move) == gunBehavior, "cohesion never replaces a useful native attack with a hold");
        enemy.Detected = false; int hiddenReads = enemy.PositionReads;
        Plugin.GroupChoiceFixture(walker.Agent, tankMove);
        check(enemy.PositionReads == hiddenReads, "group crossing does not poll hidden target positions");
        Plugin.AdvanceResetFixture(); Plugin.BindSmokeFixture(null);
    }
}
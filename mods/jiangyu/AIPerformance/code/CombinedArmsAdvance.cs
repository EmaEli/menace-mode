using System;
using System.Collections.Generic;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.Skills;

namespace AIPerformance;

public sealed partial class Plugin
{
    private static readonly CombinedArmsAdvanceState _advanceGroups = new();
    private static readonly HashSet<long> _mortarCapabilityTraced = new();

    // Contact objectives are managed snapshots, never new native mission orders.
    // Keep the reviewed human combined-arms guards; only its missing-order rejection changes.
    private static SupportUnit ContactSupportSnapshot(Agent agent, Map map, int round, bool primeIds, out SupportSnapshotReason reason)
    {
        var unit = CombinedArmsRuntime.Snapshot(agent, primeIds, out reason);
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled ||
            (unit == null && reason != SupportSnapshotReason.InfantryWithoutAdvanceOrder) || unit?.HasOrder == true ||
            !ContactAdvanceRuntime.TryAim(agent, _contactAdvance, map, round, out var contact)) return unit;
        var actor = agent.GetActor(); var current = actor.GetTile();
        int x = contact.X / 8 * 8, z = contact.Z / 8 * 8;
        var order = new AdvanceRecoveryOrder(x, z, Math.Min(8, map.Width - x), Math.Min(8, map.Height - z), 1);
        reason = SupportSnapshotReason.Eligible;
        return new(actor.Pointer.ToInt64(), agent.Pointer.ToInt64(), unit?.Kind ?? SupportKind.Infantry,
            current.GetX(), current.GetZ(), order, true, contact.X, contact.Z);
    }

    private static void TraceMortarCapability(Actor actor, int round, long faction)
    {
        if (!_contactAdvanceDiagnostics || actor == null || _mortarCapabilityTraced.Count >= 128) return;
        try
        {
            if (actor.GetTemplate()?.GetID() != "enemy.rogue_army_mortar_weapon_team" ||
                !_mortarCapabilityTraced.Add(actor.Pointer.ToInt64())) return;
            var skills = actor.GetSkills()?.GetAllSkills();
            if (skills == null || skills.Count > 128) return;
            bool frag = false, smoke = false, smokeUsable = false;
            foreach (var raw in skills)
            {
                var skill = raw?.TryCast<Skill>(); string id = skill?.GetTemplate()?.GetID();
                if (id == "active.launch_medium_mortar_frag") frag = true;
                if (id is not ("active.launch_mortar_smoke" or "active.mod.launch_mortar_smoke_autoloader" or
                    "active.mod.twinfire.launch_mortar_smoke_autoloader")) continue;
                smoke = true; smokeUsable |= skill.IsEnabled() && !skill.IsOutOfUses();
            }
            ContactAdvanceDiagnostic(round, faction, actor.Pointer.ToInt64(), "group-capability",
                $"result=mortar-loadout fragEquipped={frag} recognizedSmokeEquipped={smoke} smokeEnabledWithUses={smokeUsable}");
        }
        catch { } // Observe only; never add a missing skill, ammunition or equipment.
    }

    private static void PublishAdvanceGroups(long map, int round, long faction, List<SupportUnit> units)
    {
        if (!_contactAdvanceEnabled || !_offensivePlanningEnabled) return;
        var members = new List<AdvanceGroupMember>();
        foreach (var unit in units)
            if (_contactAdvance.TryAssign(map, round, faction, unit.Actor, unit.X, unit.Z, out var contact)) members.Add(new(unit, contact));
        _advanceGroups.Publish(map, round, faction, members);
    }

    private static bool TryAdvanceGroup(Agent agent, Map map, int round, bool exposed, out AdvanceGroupPlan plan)
    {
        plan = default;
        return _combinedArmsEnabled && _contactAdvanceEnabled && _offensivePlanningEnabled && map != null &&
            _advanceGroups.TryGet(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(),
                ContactSupportSnapshot(agent, map, round, false, out _), exposed, out plan);
    }

    // Observes successful native actions; it never executes a skill or grants equipment/AP/ammo.
    private static void AdvanceGroupSkillUsed(Skill __instance, Tile __0, bool __result)
    {
        if (!__result || !_combinedArmsEnabled || !_contactAdvanceEnabled || !_offensivePlanningEnabled ||
            Environment.CurrentManagedThreadId != _combinedArmsThread || __instance == null || __0 == null) return;
        try
        {
            var actor = __instance.GetEntity()?.TryCast<Actor>(); var agent = actor?.GetAgent();
            if (agent == null || !ContactConvergenceRuntime.EligibleFaction(agent.GetFaction())) return;
            var template = __instance.GetTemplate(); string id = template?.GetID();
            bool smoke = id is "active.throw_smoke_grenade" or "active.throw_smoke_grenade_limited" or
                "active.launch_mortar_smoke" or "active.mod.launch_mortar_smoke_autoloader" or
                "active.mod.twinfire.launch_mortar_smoke_autoloader";
            if (!smoke && template?.IsAttack != true) return;
            var manager = TacticalManager.Get(); var map = manager?.GetMap();
            if (map == null) return;
            long faction = agent.GetFaction().Pointer.ToInt64();
            bool recorded = _advanceGroups.Signal(map.Pointer.ToInt64(), manager.m_Round, faction,
                actor.Pointer.ToInt64(), __0.GetX(), __0.GetZ(), smoke);
            if (recorded)
                ContactAdvanceDiagnostic(manager.m_Round, faction, actor.Pointer.ToInt64(), "group-signal",
                    $"result={(smoke ? "smoke-used" : "fire-used")} target=({__0.GetX()},{__0.GetZ()})");
        }
        catch { } // Optional observation cannot veto or repeat the successful native action.
    }

    private static Behavior AdvanceGroupChoice(Agent agent, Behavior chosen, Map map, int round)
    {
        var move = chosen?.TryCast<Move>(); var actor = agent?.GetActor();
        if (move == null || actor == null || !ContactAdvanceRuntime.Ready(agent) ||
            CombinedArmsResponseRuntime.HasUsefulAction(agent) ||
            !TryAdvanceGroup(agent, map, round, false, out var group)) return chosen;
        var destination = move.GetTargetTile()?.Tile; var current = actor.GetTile();
        if (destination == null || current == null || group.Contact.Range <= 0 || round - group.Contact.SeenRound > 2) return chosen;
        int remaining = CombinedArmsAdvanceState.Distance(destination.GetX(), destination.GetZ(), group.Contact.X, group.Contact.Z);
        int distance = CombinedArmsAdvanceState.Distance(current.GetX(), current.GetZ(), group.Contact.X, group.Contact.Z);
        if (remaining >= distance || remaining > group.Contact.Range + 3) return chosen;
        bool smoke = SupportSmokeCover(destination);
        bool exposed = !smoke && map.GetTile(group.Contact.X, group.Contact.Z).HasLineOfSightTo(destination, Il2CppTactical.LineOfSightFlags.Default);
        // A covered destination alone is insufficient: certify the native clipped crossing too.
        var path = move.m_Path;
        if (!exposed && (path == null || path.Count == 0 || path.Count > 16)) exposed = true;
        if (!exposed && path != null && path.Count <= 16)
            foreach (var point in path)
            {
                var tile = map.GetTileAtPos(point);
                if (tile == null || (!SupportSmokeCover(tile) &&
                    map.GetTile(group.Contact.X, group.Contact.Z).HasLineOfSightTo(tile, Il2CppTactical.LineOfSightFlags.Default)))
                { exposed = true; break; }
            }
        if (!exposed) return chosen;
        if (!TryAdvanceGroup(agent, map, round, true, out group)) return chosen;
        bool unsupported = actor.IsVehicle() ? group.Infantry == 0 || remaining < group.FrontDistance + 2 :
            group.Infantry > 1 && (group.RearDistance - remaining > 6 || group.Support > 0);
        bool smokePending = group.Phase == AdvanceGroupPhase.Cross && !group.FireRecent &&
            !SupportSmokeCover(map.GetTile(group.SmokeX, group.SmokeZ));
        bool screen = actor.IsVehicle() && group.Infantry > 0 && remaining < group.FrontDistance + 2;
        bool hold = group.Phase != AdvanceGroupPhase.Fallback &&
            (screen || ((group.Phase == AdvanceGroupPhase.Rally || smokePending) && unsupported));
        ContactAdvanceDiagnostic(round, agent.GetFaction().Pointer.ToInt64(), actor.Pointer.ToInt64(), "group-choice",
            $"group={group.Group:X} phase={group.Phase} infantry={group.Infantry} support={group.Support} result={(hold ? "stage" : "advance")}");
        if (hold)
            foreach (var behavior in agent.GetBehaviors())
                if (behavior?.GetID().ToString() == "Idle") return behavior;
        return chosen;
    }

    private static bool HasFeasibleSmokeAdvance(Agent agent, AdvanceGroupPlan plan)
    {
        if (!plan.CanSmoke || plan.Phase == AdvanceGroupPhase.Cross) return false;
        var actor = agent.GetActor(); var skills = actor.GetSkills()?.GetAllSkills();
        var tiles = agent.GetTiles(); var current = actor.GetTile();
        if (skills == null || skills.Count > 128 || tiles == null || tiles.Count > 4096 || current == null) return false;
        int smokeCost = int.MaxValue;
        foreach (var raw in skills)
        {
            var skill = raw?.TryCast<Skill>();
            if (skill?.GetTemplate()?.GetID() is not ("active.throw_smoke_grenade" or "active.throw_smoke_grenade_limited") ||
                skill.IsOutOfUses() || !skill.IsEnabled()) continue;
            int cost = skill.GetActionPointCost();
            if (cost >= 0) smokeCost = Math.Min(smokeCost, cost);
        }
        int distance = CombinedArmsAdvanceState.Distance(current.GetX(), current.GetZ(), plan.Contact.X, plan.Contact.Z);
        int examined = 0;
        foreach (var pair in tiles)
        {
            if (++examined > 256) break;
            var score = pair.Value; var tile = score?.Tile;
            if (tile == null || pair.Key?.Pointer != tile.Pointer || !float.IsFinite(score.SafetyScore) ||
                score.APCost < 0 || (long)score.APCost + smokeCost > actor.GetActionPoints()) continue;
            if (CombinedArmsAdvanceState.Distance(tile.GetX(), tile.GetZ(), plan.Contact.X, plan.Contact.Z) < distance &&
                tile.IsValidMovementDestination()) return true;
        }
        return false;
    }
}
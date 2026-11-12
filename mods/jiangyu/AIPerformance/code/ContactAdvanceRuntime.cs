using System;
using Il2CppMenace.Tags;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;

namespace AIPerformance;

internal enum ContactAdvanceObservation { IneligibleFaction, MissingTarget, NotDetected, NonHostile, Contained, MissingTile, Recorded, Capacity, Unchanged }
internal enum AdvanceZoneGate { NotReady, NoAdvanceOrder, UsefulAction, Eligible }

internal static partial class ContactAdvanceRuntime
{
    internal static bool HostileFaction(AIFaction faction)
    {
        if (faction == null || faction.IsAlliedWithPlayer()) return false;
        int kind = (int)faction.GetFactionType();
        return kind >= 5 && kind <= 9;
    }

    internal static bool Mobilizable(Agent agent)
    {
        var actor = agent?.GetActor(); var role = agent?.GetRole();
        if (actor == null || role == null || !HostileFaction(agent.GetFaction()) ||
            role.AvoidOpponents || !float.IsFinite(role.Move) || role.Move <= 0 || agent.IsDeploymentPhase() ||
            actor.IsDying() || actor.IsContainedWithinAnotherEntity() || actor.IsStunned()) return false;
        var zones = agent.GetZones();
        return zones != null && (zones.Count == 0 ||
            AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, actor, out _, out _));
    }

    internal static bool Ready(Agent agent)
    {
        if (!Mobilizable(agent) || agent.IsSleeping()) return false;
        var actor = agent.GetActor();
        if (actor.IsMoving() || actor.IsActionPointsSpent()) return false;
        int kind = (int)agent.GetFaction().GetFactionType();
        // Creatures/constructs need not have a human morale percentage. Their useful
        // native emergency actions still win. Human-crewed MACHINE vehicles keep morale.
        bool nonHuman = kind == 7 || kind == 8 || actor.HasTag(TagType.XENO) || actor.HasTag(TagType.DRONE) ||
            (actor.HasTag(TagType.MACHINE) && !CombinedArmsRuntime.CrewedVehicle(actor.GetTemplate()?.m_ID));
        return nonHuman || (float.IsFinite(actor.GetMoralePct()) && actor.GetMoralePct() >= .75f &&
            float.IsFinite(actor.GetSuppressionPct()) && actor.GetSuppressionPct() <= .25f);
    }

    // Native advance zones stay active after contact only for ready units without a useful shot.
    internal static bool KeepAdvanceZones(Agent agent) => KeepAdvanceZones(agent, out _);

    internal static bool KeepAdvanceZones(Agent agent, out AdvanceZoneGate gate)
    {
        if (agent == null || !Ready(agent)) { gate = AdvanceZoneGate.NotReady; return false; }
        if (!AdvanceRecoveryRuntime.TryReadAdvanceOrders(agent, agent.GetActor(), out _, out _))
        { gate = AdvanceZoneGate.NoAdvanceOrder; return false; }
        if (CombinedArmsResponseRuntime.HasUsefulAction(agent))
        { gate = AdvanceZoneGate.UsefulAction; return false; }
        gate = AdvanceZoneGate.Eligible;
        return true;
    }

    internal static ContactAdvanceObservation Observe(AIFaction faction, Actor enemy, ContactAdvanceState memory, Map map, int round)
    {
        // Detection before any enemy position, weapon, death or faction reads.
        if (!HostileFaction(faction)) return ContactAdvanceObservation.IneligibleFaction;
        if (enemy == null || memory == null || map == null) return ContactAdvanceObservation.MissingTarget;
        if (!enemy.IsDetectedByFaction((int)faction.GetFactionType())) return ContactAdvanceObservation.NotDetected;
        int kind = (int)enemy.GetFaction();
        if ((kind != 1 && kind != 2 && kind != 4) || faction.IsAlliedWith(kind)) return ContactAdvanceObservation.NonHostile;
        if (enemy.IsDying() || enemy.IsContainedWithinAnotherEntity())
        { memory.Forget(map.Pointer.ToInt64(), round, faction.Pointer.ToInt64(), enemy.Pointer.ToInt64()); return ContactAdvanceObservation.Contained; }
        var tile = enemy.GetTile();
        if (tile == null) return ContactAdvanceObservation.MissingTile;
        int x = tile.GetX(), z = tile.GetZ();
        if (x < 0 || z < 0 || x >= map.Width || z >= map.Height) return ContactAdvanceObservation.MissingTile;
        if (memory.TryRead(map.Pointer.ToInt64(), round, faction.Pointer.ToInt64(), enemy.Pointer.ToInt64(), out var previous) &&
            previous.SeenRound == round && previous.X == x && previous.Z == z) return ContactAdvanceObservation.Unchanged;
        var attacks = enemy.GetAllAttacks();
        int range = 0;
        if (attacks != null && attacks.Count <= 128)
            foreach (var skill in attacks)
            {
                var template = skill?.GetTemplate();
                if (template == null || !template.IsAttack || !template.IsLineOfFireNeeded ||
                    !skill.IsEnabled() || skill.IsOutOfUses() || !skill.HasExecutingElements()) continue;
                int r = skill.GetMaxRange();
                if (r > range && r <= 128) range = r;
            }
        return memory.Observe(map.Pointer.ToInt64(), round, faction.Pointer.ToInt64(),
            new(enemy.Pointer.ToInt64(), x, z, round, range)) ?
            ContactAdvanceObservation.Recorded : ContactAdvanceObservation.Capacity;
    }

    internal static bool Refresh(AIFaction faction, ContactAdvanceState memory, Map map, int round)
    {
        if (!HostileFaction(faction) || map == null ||
            !memory.BeginTurn(map.Pointer.ToInt64(), round, faction.Pointer.ToInt64())) return false;
        var opponents = faction.GetOpponents();
        if (opponents == null || opponents.Count > 128) return true;
        int count = 0;
        foreach (var opponent in opponents)
        {
            var enemy = opponent?.Actor;
            if (enemy == null || !enemy.IsDetectedByFaction((int)faction.GetFactionType())) continue;
            Observe(faction, enemy, memory, map, round);
            if (++count == ContactAdvanceState.MaxContacts) break;
        }
        return true;
    }

    internal static bool TryAim(Agent agent, ContactAdvanceState memory, Map map, int round, out AdvanceContact contact)
    {
        contact = default;
        if (!Mobilizable(agent) || map == null || memory == null) return false;
        var tile = agent.GetActor().GetTile();
        return tile != null && memory.TryAssign(map.Pointer.ToInt64(), round, agent.GetFaction().Pointer.ToInt64(), agent.GetActor().Pointer.ToInt64(),
            tile.GetX(), tile.GetZ(), out contact) && contact.X < map.Width && contact.Z < map.Height;
    }

    internal static bool Alert(Agent agent, ContactAdvanceState memory, Map map, int round)
    {
        if (!TryAim(agent, memory, map, round, out _)) return false;
        if (agent.IsSleeping()) agent.SetSleeping(false);
        return true;
    }

    private static Tile DetectedTarget(Agent agent, AdvanceContact contact)
    {
        var faction = agent.GetFaction(); var opponents = faction.GetOpponents();
        if (opponents == null || opponents.Count > 128) return null;
        foreach (var opponent in opponents)
        {
            var enemy = opponent?.Actor;
            if (enemy == null || enemy.Pointer.ToInt64() != contact.Target ||
                !enemy.IsDetectedByFaction((int)faction.GetFactionType())) continue;
            if (enemy.IsDying() || enemy.IsContainedWithinAnotherEntity()) return null;
            return enemy.GetTile();
        }
        return null;
    }

    internal static int[] SmokeThreat(Agent agent, ContactAdvanceState memory, Map map, int round)
    {
        if (!Ready(agent) || !agent.GetActor().IsInfantry() || CombinedArmsResponseRuntime.HasUsefulAction(agent) ||
            !TryAim(agent, memory, map, round, out var contact) || round - contact.SeenRound > 2 || contact.Range <= 0) return null;
        var current = agent.GetActor().GetTile(); var origin = map.GetTile(contact.X, contact.Z);
        if (current == null || origin == null || CombinedArmsRuntime.Distance2(current, contact.X, contact.Z) > 10 * 10 ||
            CombinedArmsRuntime.Distance2(current, contact.X, contact.Z) > contact.Range * contact.Range ||
            !origin.HasLineOfSightTo(current, Il2CppTactical.LineOfSightFlags.Default)) return null;
        // Already in a usable firing lane: do not smoke our own shot. No requests solely for being far away.
        var target = DetectedTarget(agent, contact);
        if (target != null)
            foreach (var weapon in Weapons(agent))
                if (FiringLane(weapon, current, target, agent.GetActor(), false, out _)) return null;
        return new[] { contact.X, contact.Z };
    }
}
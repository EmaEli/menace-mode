using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct AdvanceGroupMember(SupportUnit Unit, AdvanceContact Contact);
internal enum AdvanceGroupPhase { Rally, Cross, Fallback }
internal readonly record struct AdvanceGroupPlan(long Group, AdvanceContact Contact, int Infantry, int Support,
    int FrontDistance, int RearDistance, AdvanceGroupPhase Phase, int SmokeRound, int SmokeX, int SmokeZ,
    bool CanSmoke, bool FireRecent);

// Small, shared local groups. Only scalar friendly snapshots and legitimate contact memory;
// no native objects, faction-wide hot scans, scheduler, pathfinder or hidden target reads.
internal sealed class CombinedArmsAdvanceState
{
    internal const int MaxInfantry = 4, MaxSupport = 2, Radius = 24, WaitRounds = 2;
    private readonly object _gate = new();
    private readonly Dictionary<long, Frame> _factions = new();
    private long _map;
    private sealed class Frame
    {
        internal int Round;
        internal Dictionary<long, Group> Actors = new();
        internal Dictionary<(int, int), List<Group>> Targets = new();
    }
    private sealed class Group
    {
        internal long Id;
        internal AdvanceContact Contact;
        internal List<SupportUnit> Units = new();
        internal int WaitSince, FireRound, SmokeRound, SmokeX, SmokeZ, SmokeDistance;
    }

    internal void Reset() { lock (_gate) { _map = 0; _factions.Clear(); } }

    internal bool Publish(long map, int round, long faction, IReadOnlyList<AdvanceGroupMember> members)
    {
        lock (_gate)
        {
            if (map == 0 || faction == 0 || round < 1 || members == null || members.Count > CombinedArmsState.MaxUnits ||
                (_map != 0 && _map != map)) return false;
            if (!_factions.TryGetValue(faction, out var frame))
            {
                if (_factions.Count >= CombinedArmsState.MaxFactions) return false;
                _factions[faction] = frame = new();
            }
            if (round <= frame.Round) return false;
            _map = map;
            var next = new Dictionary<long, Group>();
            var groups = new HashSet<Group>();
            var ordered = new List<AdvanceGroupMember>();
            foreach (var member in members) if (Valid(member)) ordered.Add(member);
            ordered.Sort((a, b) => a.Unit.Kind != b.Unit.Kind ? a.Unit.Kind.CompareTo(b.Unit.Kind) : a.Unit.Actor.CompareTo(b.Unit.Actor));
            // Preserve group identity/deadline even as units move, a leader dies or roles activate in a new order.
            var retained = new HashSet<long>();
            foreach (var member in ordered)
                if (Valid(member) && frame.Actors.TryGetValue(member.Unit.Actor, out var old) &&
                    old.Contact.Target == member.Contact.Target && old.Units.Exists(u => u.Actor == member.Unit.Actor &&
                        u.Agent == member.Unit.Agent && u.Order == member.Unit.Order))
                { groups.Add(old); retained.Add(member.Unit.Actor); }
            foreach (var group in groups) group.Units = new();
            var cells = new Dictionary<(long, int, int), List<Group>>();
            foreach (var member in ordered)
            {
                if (!Valid(member) || next.ContainsKey(member.Unit.Actor)) continue;
                var unit = member.Unit; Group selected = null;
                frame.Actors.TryGetValue(unit.Actor, out var previous);
                if (retained.Contains(unit.Actor) && (unit.Kind == SupportKind.Infantry || previous.Units.Count > 0) &&
                    Room(previous, unit)) selected = previous;
                if (selected == null)
                {
                    int probes = 0, best = int.MaxValue;
                    for (int x = unit.X / 16 - 2; x <= unit.X / 16 + 2; x++)
                    for (int z = unit.Z / 16 - 2; z <= unit.Z / 16 + 2; z++)
                        if (cells.TryGetValue((member.Contact.Target, x, z), out var entries))
                            foreach (var candidate in entries)
                            {
                                if (++probes > 32) break;
                                if (!Room(candidate, unit)) continue;
                                var anchor = candidate.Units[0];
                                int distance = Distance(unit.X, unit.Z, anchor.X, anchor.Z);
                                if (distance < best) { selected = candidate; best = distance; }
                            }
                }
                if (selected == null)
                {
                    if (unit.Kind == SupportKind.Heavy) continue;
                    selected = retained.Contains(unit.Actor) && Room(previous, unit) ? previous :
                        new() { Id = unit.Actor, Contact = member.Contact, WaitSince = previous?.WaitSince ?? 0 };
                }
                if (previous?.WaitSince > 0 && (selected.WaitSince == 0 || previous.WaitSince < selected.WaitSince))
                    selected.WaitSince = previous.WaitSince; // Regrouping/target changes cannot renew spent waiting time.
                bool first = selected.Units.Count == 0;
                selected.Units.Add(unit); selected.Contact = member.Contact; next[unit.Actor] = selected;
                if (first)
                {
                    var cell = (member.Contact.Target, unit.X / 16, unit.Z / 16);
                    if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new();
                    list.Add(selected);
                }
            }
            var targets = new Dictionary<(int, int), List<Group>>();
            foreach (var group in new HashSet<Group>(next.Values))
            {
                var key = (group.Contact.X / 8, group.Contact.Z / 8);
                if (!targets.TryGetValue(key, out var list)) targets[key] = list = new();
                list.Add(group);
            }
            frame.Round = round; frame.Actors = next; frame.Targets = targets;
            return true;
        }
    }

    internal void Refresh(long map, int round, long faction, long actor, SupportUnit unit)
    {
        lock (_gate)
        {
            if (!Find(map, round, faction, actor, out var frame, out var group)) return;
            int index = group.Units.FindIndex(u => u.Actor == actor);
            if (index < 0) return;
            var old = group.Units[index];
            if (unit == null || unit.Agent != old.Agent || unit.Order != old.Order || unit.Kind != old.Kind)
            { group.Units.RemoveAt(index); frame.Actors.Remove(actor); }
            else group.Units[index] = unit;
        }
    }

    internal bool TryGet(long map, int round, long faction, SupportUnit unit, bool exposed, out AdvanceGroupPlan plan)
    {
        lock (_gate)
        {
            plan = default;
            if (unit == null || !Find(map, round, faction, unit.Actor, out _, out var group) ||
                !group.Units.Exists(u => u.Actor == unit.Actor && u.Agent == unit.Agent && u.Order == unit.Order)) return false;
            int infantry = 0, support = 0, front = int.MaxValue, rear = 0;
            foreach (var member in group.Units)
                if (member.Kind == SupportKind.Infantry)
                {
                    infantry++;
                    int distance = Distance(member.X, member.Z, group.Contact.X, group.Contact.Z);
                    front = Math.Min(front, distance); rear = Math.Max(rear, distance);
                }
                else support++;
            if (exposed && group.WaitSince == 0) group.WaitSince = round;
            bool covered = (group.FireRound > 0 && round - group.FireRound <= 1) ||
                (group.SmokeRound > 0 && round - group.SmokeRound <= 1);
            var phase = covered ? AdvanceGroupPhase.Cross : group.WaitSince > 0 && round - group.WaitSince >= WaitRounds ?
                AdvanceGroupPhase.Fallback : AdvanceGroupPhase.Rally;
            plan = new(group.Id, group.Contact, infantry, support, front, rear, phase,
                group.SmokeRound, group.SmokeX, group.SmokeZ,
                group.SmokeRound == 0 || front <= group.SmokeDistance - 3,
                group.FireRound > 0 && round - group.FireRound <= 1);
            return true;
        }
    }

    internal bool Signal(long map, int round, long faction, long actor, int x, int z, bool smoke)
    {
        lock (_gate)
        {
            if (!smoke)
            {
                // Share actual friendly fire across nearby groups facing this sector;
                // artillery need not be a mobile group member. No all-pairs search.
                if (_map != map || !_factions.TryGetValue(faction, out var frame) || frame.Round != round) return false;
                bool signaled = false; int probes = 0;
                for (int cx = x / 8 - 1; cx <= x / 8 + 1; cx++)
                for (int cz = z / 8 - 1; cz <= z / 8 + 1; cz++)
                    if (frame.Targets.TryGetValue((cx, cz), out var candidates))
                        foreach (var candidate in candidates)
                        {
                            if (++probes > 32) return signaled;
                            if (Distance(x, z, candidate.Contact.X, candidate.Contact.Z) > 4) continue;
                            candidate.FireRound = round; signaled = true;
                        }
                return signaled;
            }
            if (!Find(map, round, faction, actor, out _, out var group)) return false;
            var unit = group.Units.Find(u => u.Actor == actor);
            if (unit == null || Distance(unit.X, unit.Z, x, z) > 3 ||
                Distance(x, z, group.Contact.X, group.Contact.Z) >= Distance(unit.X, unit.Z, group.Contact.X, group.Contact.Z)) return false;
            group.SmokeRound = round; group.SmokeX = x; group.SmokeZ = z;
            group.SmokeDistance = int.MaxValue;
            foreach (var member in group.Units)
                if (member.Kind == SupportKind.Infantry)
                    group.SmokeDistance = Math.Min(group.SmokeDistance, Distance(member.X, member.Z, group.Contact.X, group.Contact.Z));
            return true;
        }
    }

    private bool Find(long map, int round, long faction, long actor, out Frame frame, out Group group)
    {
        frame = null; group = null;
        return _map == map && _factions.TryGetValue(faction, out frame) && frame.Round == round && frame.Actors.TryGetValue(actor, out group);
    }
    private static bool Valid(AdvanceGroupMember m) => m.Unit != null && m.Unit.Actor != 0 && m.Unit.Agent != 0 &&
        m.Unit.X >= 0 && m.Unit.Z >= 0 && m.Contact.Target != 0 && m.Contact.X >= 0 && m.Contact.Z >= 0;
    private static bool Room(Group group, SupportUnit unit)
    {
        int count = 0;
        foreach (var member in group.Units) if ((member.Kind == SupportKind.Infantry) == (unit.Kind == SupportKind.Infantry)) count++;
        if (count >= (unit.Kind == SupportKind.Infantry ? MaxInfantry : MaxSupport)) return false;
        if (group.Units.Count == 0) return true;
        var anchor = group.Units[0];
        return Distance(unit.X, unit.Z, anchor.X, anchor.Z) <= Radius && (!unit.HasOrder || !anchor.HasOrder ||
            (Distance(unit.AimX, unit.AimZ, anchor.AimX, anchor.AimZ) <= Radius &&
            (long)(unit.AimX - unit.X) * (anchor.AimX - anchor.X) + (long)(unit.AimZ - unit.Z) * (anchor.AimZ - anchor.Z) > 0));
    }
    internal static int Distance(int x, int z, int tx, int tz) => Math.Max(Math.Abs(x - tx), Math.Abs(z - tz));
}
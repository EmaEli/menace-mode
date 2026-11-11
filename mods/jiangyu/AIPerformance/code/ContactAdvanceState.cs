using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct AdvanceContact(long Target, int X, int Z, int SeenRound, int Range);

// Faction knowledge, not three short-lived scouting leases. Never retains native objects.
internal sealed class ContactAdvanceState
{
    internal const int MaxContacts = 32, MaxFactions = 16, MaxAssignments = 4096, LifetimeRounds = 12, AssignmentRounds = 2;
    private readonly object _gate = new();
    private readonly Dictionary<(long Faction, long Target), AdvanceContact> _contacts = new();
    private readonly Dictionary<long, int> _turns = new();
    private readonly Dictionary<(long Faction, long Actor), (long Target, int Until)> _assignments = new();
    private long _map;
    private int _round;

    internal void Reset()
    {
        lock (_gate) { _map = 0; _round = 0; _contacts.Clear(); _turns.Clear(); _assignments.Clear(); }
    }

    internal bool BeginTurn(long map, int round, long faction)
    {
        lock (_gate)
        {
            if (!Context(map, round) || faction == 0 || (_turns.TryGetValue(faction, out int r) && r == round)) return false;
            if (!_turns.ContainsKey(faction) && _turns.Count >= MaxFactions) return false;
            _turns[faction] = round;
            return true;
        }
    }

    internal bool Observe(long map, int round, long faction, AdvanceContact contact)
    {
        lock (_gate)
        {
            if (!Context(map, round) || faction == 0 || contact.Target == 0 || contact.X < 0 || contact.Z < 0 ||
                contact.SeenRound != round || contact.Range < 0 || contact.Range > 128) return false;
            var key = (faction, contact.Target);
            if (!_contacts.ContainsKey(key))
            {
                int count = 0;
                foreach (var existing in _contacts.Keys) if (existing.Faction == faction) count++;
                if (count >= MaxContacts || _contacts.Count >= MaxContacts * MaxFactions) return false;
            }
            _contacts[key] = contact;
            return true;
        }
    }

    internal bool TryRead(long map, int round, long faction, long target, out AdvanceContact contact)
    {
        lock (_gate)
        {
            contact = default;
            return Context(map, round) && _contacts.TryGetValue((faction, target), out contact);
        }
    }

    // Nearby units share an objective sector; each unit keeps its assignment for two
    // rounds. Fresh scoring never rescans/reranks contacts or chases moving hidden actors.
    internal bool TryAssign(long map, int round, long faction, long actor, int x, int z, out AdvanceContact contact)
    {
        lock (_gate)
        {
            contact = default;
            if (!Context(map, round) || faction == 0 || actor == 0 || x < 0 || z < 0) return false;
            var key = (faction, actor);
            if (_assignments.TryGetValue(key, out var assignment) && assignment.Until >= round &&
                _contacts.TryGetValue((faction, assignment.Target), out contact)) return true;
            if (!_assignments.ContainsKey(key) && _assignments.Count >= MaxAssignments) return false;
            if (!TryAim(map, round, faction, x / 16 * 16 + 8, z / 16 * 16 + 8, out contact)) return false;
            _assignments[key] = (contact.Target, round + AssignmentRounds - 1);
            return true;
        }
    }

    internal int Count(long map, int round, long faction)
    {
        lock (_gate)
        {
            if (!Context(map, round) || faction == 0) return 0;
            int count = 0;
            foreach (var key in _contacts.Keys) if (key.Faction == faction) count++;
            return count;
        }
    }

    internal void Forget(long map, int round, long faction, long target)
    {
        lock (_gate) { if (Context(map, round)) _contacts.Remove((faction, target)); }
    }

    internal bool TryAim(long map, int round, long faction, int x, int z, out AdvanceContact contact)
    {
        lock (_gate)
        {
            contact = default;
            if (!Context(map, round) || faction == 0 || x < 0 || z < 0) return false;
            long best = long.MaxValue;
            foreach (var pair in _contacts)
            {
                if (pair.Key.Faction != faction) continue;
                var candidate = pair.Value;
                long cost = Math.Max(Math.Abs((long)x - candidate.X), Math.Abs((long)z - candidate.Z)) +
                    (round - candidate.SeenRound) * 4L;
                if (cost < best || (cost == best && candidate.Target < contact.Target))
                { contact = candidate; best = cost; }
            }
            return contact.Target != 0;
        }
    }

    private bool Context(long map, int round)
    {
        if (map == 0 || round < 1) return false;
        // Mission reset owns generation changes. An old worker cannot restore an old map.
        if (_map == 0) _map = map;
        if (_map != map || round < _round) return false;
        if (_round != round)
        {
            var expired = new List<(long Faction, long Target)>();
            foreach (var pair in _contacts)
                if (round - pair.Value.SeenRound > LifetimeRounds) expired.Add(pair.Key);
            foreach (var key in expired) _contacts.Remove(key);
            var assignments = new List<(long Faction, long Actor)>();
            foreach (var pair in _assignments)
                if (pair.Value.Until < round || !_contacts.ContainsKey((pair.Key.Faction, pair.Value.Target))) assignments.Add(pair.Key);
            foreach (var key in assignments) _assignments.Remove(key);
            _round = round;
        }
        return true;
    }
}
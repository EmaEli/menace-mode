using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct AdvanceContact(long Target, int X, int Z, int SeenRound, int Range);

// Faction knowledge, not three short-lived scouting leases. Never retains native objects.
internal sealed class ContactAdvanceState
{
    internal const int MaxContacts = 32, LifetimeRounds = 12;
    private readonly object _gate = new();
    private readonly Dictionary<(long Faction, long Target), AdvanceContact> _contacts = new();
    private readonly Dictionary<long, int> _turns = new();
    private long _map;
    private int _round;

    internal void Reset()
    {
        lock (_gate) { _map = 0; _round = 0; _contacts.Clear(); _turns.Clear(); }
    }

    internal bool BeginTurn(long map, int round, long faction)
    {
        lock (_gate)
        {
            if (!Context(map, round) || faction == 0 || (_turns.TryGetValue(faction, out int r) && r == round)) return false;
            if (!_turns.ContainsKey(faction) && _turns.Count >= 16) return false;
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
            if (!_contacts.ContainsKey(key) && _contacts.Count >= MaxContacts) return false;
            _contacts[key] = contact;
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
            _round = round;
        }
        return true;
    }
}
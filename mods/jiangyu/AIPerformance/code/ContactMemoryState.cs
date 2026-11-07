using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct ContactEstimate(long Target, int X, int Z, int SeenRound, int Radius);

// Managed coordinates only: no retained Actor reference and no hidden-position refresh.
internal sealed class ContactMemoryState
{
    internal const int MaxContacts = 32, MaxResponders = 3, LifetimeRounds = 2;
    private readonly object _sync = new();
    private readonly Dictionary<(long Faction, long Target), ContactEstimate> _contacts = new();
    private readonly Dictionary<(long Faction, long Actor), Response> _responses = new();
    private long _map;
    private int _round;
    private sealed class Response
    {
        internal long Agent, Target;
        internal int Started, Round, X, Z, Stalled, Slot;
        internal bool Stopped;
    }

    internal void Reset()
    {
        lock (_sync) { _map = 0; _round = 0; _contacts.Clear(); _responses.Clear(); }
    }

    internal void Observe(long map, int round, long faction, long target, int x, int z)
    {
        lock (_sync)
        {
            if (!Context(map, round) || faction == 0 || target == 0 || x < 0 || z < 0) return;
            var key = (faction, target);
            if (_contacts.ContainsKey(key) || _contacts.Count < MaxContacts)
                _contacts[key] = new ContactEstimate(target, x, z, round, 3);
        }
    }

    internal bool TryEstimate(long map, int round, long faction, long target, out ContactEstimate estimate)
    {
        lock (_sync)
        {
            estimate = default;
            if (!Context(map, round) || !_contacts.TryGetValue((faction, target), out var contact)) return false;
            estimate = contact with { Radius = 3 + (round - contact.SeenRound) * 2 };
            return true;
        }
    }

    internal void Forget(long map, int round, long faction, long target)
    {
        lock (_sync)
        {
            if (!Context(map, round)) return;
            _contacts.Remove((faction, target));
            var released = new List<(long Faction, long Actor)>();
            foreach (var pair in _responses)
                if (pair.Key.Faction == faction && pair.Value.Target == target) released.Add(pair.Key);
            foreach (var key in released) _responses.Remove(key);
        }
    }

    internal bool TryRespond(long map, int round, long faction, long actor, long agent, int x, int z,
        out ContactEstimate estimate)
    {
        lock (_sync)
        {
            estimate = default;
            if (!Context(map, round) || faction == 0 || actor == 0 || agent == 0 || x < 0 || z < 0) return false;
            var key = (faction, actor);
            if (!_responses.TryGetValue(key, out var response))
            {
                int count = 0, usedSlots = 0;
                foreach (var pair in _responses)
                    if (pair.Key.Faction == faction) { count++; usedSlots |= 1 << pair.Value.Slot; }
                if (count >= MaxResponders) return false;
                int slot = 0;
                while ((usedSlots & (1 << slot)) != 0) slot++;
                ContactEstimate best = default;
                long bestCost = long.MaxValue;
                foreach (var pair in _contacts)
                {
                    if (pair.Key.Faction != faction) continue;
                    var contact = pair.Value;
                    long cost = Math.Abs((long)x - contact.X) + Math.Abs((long)z - contact.Z) +
                        (round - contact.SeenRound) * 8L;
                    if (cost < bestCost || (cost == bestCost && contact.Target < best.Target))
                    { best = contact; bestCost = cost; }
                }
                if (best.Target == 0) return false;
                response = new Response { Agent = agent, Target = best.Target, Started = round, Round = round, Slot = slot };
                _responses.Add(key, response);
            }
            if (response.Agent != agent || !TryEstimate(map, round, faction, response.Target, out estimate)) return false;
            // A fresh sighting can authorize another attempt, never a repeated choice poll.
            if (response.Stopped && estimate.SeenRound > response.Round)
            { response.Started = round; response.Stalled = 0; response.Stopped = false; }
            if (response.Stopped) return false;
            // Small separate approach sectors; no exact-tile pursuit or global grid search.
            int offset = (response.Slot - 1) * 4;
            estimate = estimate with { X = (int)Math.Clamp((long)estimate.X + offset, 0, int.MaxValue) };
            int distance = Distance(x, z, estimate);
            if (response.Round != round)
            {
                // Growing uncertainty must not look like physical progress by a stationary unit.
                response.Stalled = x == response.X && z == response.Z ? response.Stalled + 1 : 0;
                response.Round = round;
            }
            response.X = x; response.Z = z;
            if (distance == 0 || response.Stalled >= 2 || round - response.Started >= 3)
            { response.Stopped = true; return false; }
            return true;
        }
    }

    internal static int Distance(int x, int z, ContactEstimate estimate) =>
        (int)Math.Min(int.MaxValue, Math.Max(0L, Math.Max(Math.Abs((long)x - estimate.X),
            Math.Abs((long)z - estimate.Z)) - estimate.Radius));

    private bool Context(long map, int round)
    {
        if (map == 0 || round < 1) return false;
        if (_map != map || round < _round) { _contacts.Clear(); _responses.Clear(); _map = map; }
        if (_round != round || _map != map)
        {
            var expired = new List<(long Faction, long Target)>();
            foreach (var pair in _contacts)
                if (round - pair.Value.SeenRound > LifetimeRounds) expired.Add(pair.Key);
            foreach (var key in expired) _contacts.Remove(key);
            var released = new List<(long Faction, long Actor)>();
            foreach (var pair in _responses)
                if (!_contacts.ContainsKey((pair.Key.Faction, pair.Value.Target)) || round - pair.Value.Round > 1)
                    released.Add(pair.Key);
            foreach (var key in released) _responses.Remove(key);
        }
        _round = round;
        return true;
    }
}
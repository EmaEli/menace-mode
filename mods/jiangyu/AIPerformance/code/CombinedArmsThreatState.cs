using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct SupportThreat(long Target, int X, int Z, int Range, int SeenRound);

// Possible firing positions, not live opponent handles. Only main-thread observations can refresh them.
internal sealed class CombinedArmsThreatState
{
    internal const int Lifetime = 2, Limit = 32;
    private readonly object _gate = new();
    private readonly Dictionary<(long Faction, long Target), SupportThreat> _threats = new();
    private readonly HashSet<(long Faction, long Target)> _observed = new();
    private long _map;
    private int _round;
    internal void Reset() { lock (_gate) { _map = 0; _round = 0; _threats.Clear(); _observed.Clear(); } }
    internal bool Advance(long map, int round)
    {
        lock (_gate)
        {
            if (map == 0 || round < 1 || (_map != 0 && _map != map) || round < _round) return false;
            if (round > _round) _observed.Clear();
            _map = map; _round = round;
            var expired = new List<(long, long)>();
            foreach (var pair in _threats) if (round - pair.Value.SeenRound > Lifetime) expired.Add(pair.Key);
            foreach (var key in expired) _threats.Remove(key);
            return true;
        }
    }
    internal bool BeginObservation(long map, int round, long faction, long target)
    {
        lock (_gate)
            return map == _map && round == _round && faction != 0 && target != 0 &&
                _observed.Count < Limit && _observed.Add((faction, target));
    }
    internal void Observe(long map, int round, long faction, SupportThreat threat)
    {
        lock (_gate)
        {
            if (map != _map || round != _round || faction == 0 || threat.Target == 0 || threat.SeenRound != round ||
                threat.X < 0 || threat.Z < 0 || threat.Range <= 0 || threat.Range > 128) return;
            var key = (faction, threat.Target);
            if (_threats.ContainsKey(key) || _threats.Count < Limit) _threats[key] = threat;
        }
    }
    internal void Forget(long map, int round, long faction, long target)
    {
        lock (_gate) if (map == _map && round == _round) _threats.Remove((faction, target));
    }
    internal SupportThreat[] Get(long map, int round, long faction)
    {
        lock (_gate)
        {
            if (map != _map || round != _round) return Array.Empty<SupportThreat>();
            var list = new List<SupportThreat>();
            foreach (var pair in _threats) if (pair.Key.Faction == faction) list.Add(pair.Value);
            list.Sort((a, b) => a.Target.CompareTo(b.Target));
            return list.ToArray();
        }
    }
}
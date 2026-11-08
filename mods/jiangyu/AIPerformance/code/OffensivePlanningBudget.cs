using System.Collections.Generic;

namespace AIPerformance;

// Counts all native usability/value probes, including rejected ones. No native objects retained.
internal sealed class OffensivePlanningBudget
{
    internal const int MaxActors = 256, PerRound = 48, PerEvaluation = 24;
    private readonly object _gate = new();
    private readonly Dictionary<long, (long Agent, int Round, int Used)> _actors = new();
    private long _map;

    internal bool Spend(long map, int round, long actor, long agent)
    {
        if (map == 0 || actor == 0 || agent == 0 || round < 1) return false;
        lock (_gate)
        {
            if (_map == 0) _map = map;
            if (_map != map) return false; // Only the mission lifecycle may clear an old map.
            if (!_actors.TryGetValue(actor, out var entry) || entry.Agent != agent || entry.Round < round)
            {
                if (!_actors.ContainsKey(actor) && _actors.Count >= MaxActors) return false;
                entry = (agent, round, 0);
            }
            if (entry.Round != round || entry.Used >= PerRound) return false;
            _actors[actor] = (agent, round, entry.Used + 1);
            return true;
        }
    }

    internal void Reset() { lock (_gate) { _actors.Clear(); _map = 0; } }
}
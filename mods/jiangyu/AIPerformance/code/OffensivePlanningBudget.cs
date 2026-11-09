using System.Collections.Generic;

namespace AIPerformance;

// Counts all native usability/value probes, including rejected ones. No native objects retained.
internal sealed class OffensivePlanningBudget
{
    internal const int MaxActors = 256, PerRound = 48, PerEvaluation = 24, PerFactionRound = 512;
    private readonly object _gate = new();
    private readonly Dictionary<long, (long Agent, int Round, int Used)> _actors = new();
    private readonly Dictionary<long, (int Round, int Used)> _factions = new();
    private long _map;

    internal bool Spend(long map, int round, long actor, long agent, long faction = 0, int cost = 1)
    {
        if (map == 0 || actor == 0 || agent == 0 || round < 1 || cost < 1 || cost > PerEvaluation) return false;
        lock (_gate)
        {
            if (_map == 0) _map = map;
            if (_map != map) return false; // Only the mission lifecycle may clear an old map.
            bool found = _actors.TryGetValue(actor, out var entry);
            if (found && entry.Agent != agent && entry.Round >= round) return false;
            if (!found || entry.Round < round)
            {
                if (!_actors.ContainsKey(actor) && _actors.Count >= MaxActors) return false;
                entry = (agent, round, 0);
            }
            if (entry.Round != round || entry.Used + cost > PerRound) return false;
            if (faction != 0)
            {
                if (!_factions.TryGetValue(faction, out var total))
                {
                    if (_factions.Count >= 16) return false;
                    total = (round, 0);
                }
                if (total.Round < round) total = (round, 0);
                if (total.Round != round || total.Used + cost > PerFactionRound) return false;
                _factions[faction] = (round, total.Used + cost);
            }
            _actors[actor] = (agent, round, entry.Used + cost);
            return true;
        }
    }

    internal void Reset() { lock (_gate) { _actors.Clear(); _factions.Clear(); _map = 0; } }
}
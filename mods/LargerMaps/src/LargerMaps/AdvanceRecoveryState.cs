using System;
using System.Collections.Generic;

namespace LargerMaps;

internal readonly record struct AdvanceRecoveryOrder(int X, int Z, int Width, int Height, float Priority);

// Managed evidence only: repeated choice polls are not completed idle rounds.
internal sealed class AdvanceRecoveryState
{
    internal const int MaxActors = 2048;
    private readonly object _sync = new();
    private readonly Dictionary<long, Entry> _actors = new();
    private long _map;
    private int _round = -1;
    private sealed class Entry
    {
        internal long Agent;
        internal int Round, X, Z, IdleRounds;
        internal AdvanceRecoveryOrder Order;
        internal bool Idle, Busy;
    }

    internal void Reset()
    {
        lock (_sync) { _actors.Clear(); _map = 0; _round = -1; }
    }

    // Opt-in diagnostics only; expose no native references and do not mutate evidence.
    internal string Describe(long actor)
    {
        lock (_sync)
            return _actors.TryGetValue(actor, out Entry e)
                ? $"historyRound={e.Round} idleRounds={e.IdleRounds} idle={e.Idle} busy={e.Busy}"
                : "history=absent";
    }

    internal bool Observe(long map, int round, long actor, long agent, int x, int z, AdvanceRecoveryOrder order)
    {
        lock (_sync)
        {
            if (map == 0 || actor == 0 || agent == 0 || round < 1 || x < 0 || z < 0) return false;
            if (map != _map || round < _round) { _actors.Clear(); _map = map; }
            _round = round;
            if (!_actors.TryGetValue(actor, out Entry e))
            {
                if (_actors.Count >= MaxActors) return false;
                _actors[actor] = new Entry { Agent = agent, Round = round, X = x, Z = z, Order = order };
                return false;
            }
            bool same = e.Agent == agent && e.X == x && e.Z == z && e.Order == order;
            if (e.Round != round)
            {
                e.IdleRounds = same && e.Round + 1 == round && e.Idle && !e.Busy ? Math.Min(2, e.IdleRounds + 1) : 0;
                e.Round = round;
                e.Idle = e.Busy = false;
            }
            else if (!same) { e.IdleRounds = 0; e.Idle = false; e.Busy = true; }
            e.Agent = agent; e.X = x; e.Z = z; e.Order = order;
            return e.IdleRounds >= 2 && !e.Busy;
        }
    }

    internal void Choice(long map, int round, long actor, long agent, bool idleOnly)
    {
        lock (_sync)
        {
            if (_map != map || !_actors.TryGetValue(actor, out Entry e) || e.Round != round || e.Agent != agent) return;
            // A later Idle poll must never erase an earlier useful action/choice.
            if (idleOnly) e.Idle = true;
            else { e.Busy = true; e.IdleRounds = 0; }
        }
    }
}
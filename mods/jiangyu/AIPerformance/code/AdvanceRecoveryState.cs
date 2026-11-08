using System;
using System.Collections.Generic;

namespace AIPerformance;

internal readonly record struct AdvanceRecoveryOrder(int X, int Z, int Width, int Height, float Priority,
    string Context = null);

// Managed evidence only: repeated choice polls are not completed idle rounds.
internal sealed class AdvanceRecoveryState
{
    internal const int MaxActors = 2048;
    private readonly object _sync = new();
    private readonly Dictionary<long, Entry> _actors = new();
    private readonly Dictionary<long, int> _diagnosticStages = new();
    private long _map;
    private long _diagnosticMap;
    private int _round = -1;
    private sealed class Entry
    {
        internal long Agent;
        internal int Round, X, Z, IdleRounds;
        internal int RecoveryRound, RecoveryResult, AppliedRecoveryRound, MoveFallbackRound;
        internal int Distance = -1, AdvanceDistance = -1, AdvanceRound = -1;
        internal AdvanceRecoveryOrder Order;
        internal bool Idle, Busy, RecoveryApplied, AdvanceSelected, Continuing;
        internal string LastChoiceSignature;
    }

    internal void Reset()
    {
        lock (_sync)
        {
            _actors.Clear();
            _diagnosticStages.Clear();
            _map = _diagnosticMap = 0;
            _round = -1;
        }
    }

    internal string Describe(long actor)
    {
        lock (_sync)
            return _actors.TryGetValue(actor, out Entry e)
                ? $"historyRound={e.Round} idleRounds={e.IdleRounds} idle={e.Idle} busy={e.Busy}"
                : "history=absent";
    }

    internal bool Observe(long map, int round, long actor, long agent, int x, int z, AdvanceRecoveryOrder order,
        int distance = -1)
    {
        lock (_sync)
        {
            if (map == 0 || actor == 0 || agent == 0 || round < 1 || x < 0 || z < 0) return false;
            if (map != _map || round < _round)
            {
                _actors.Clear();
                _diagnosticStages.Clear();
                _map = _diagnosticMap = map;
            }
            _round = round;
            if (!_actors.TryGetValue(actor, out Entry e))
            {
                if (_actors.Count >= MaxActors) return false;
                _actors[actor] = new Entry { Agent = agent, Round = round, X = x, Z = z, Order = order, Distance = distance };
                return false;
            }
            bool sameOrder = e.Agent == agent && e.Order == order;
            bool same = sameOrder && e.X == x && e.Z == z;
            if (e.Round != round)
            {
                // A provisional selection is never progress. Continue only after an
                // observed distance reduction, in the very next round, on the same order.
                e.Continuing = sameOrder && e.Round + 1 == round && e.AdvanceSelected &&
                    e.AdvanceRound + 1 == round && distance > 0 && distance < e.AdvanceDistance;
                e.IdleRounds = same && e.Round + 1 == round && e.Idle && !e.Busy ? Math.Min(2, e.IdleRounds + 1) : 0;
                e.AdvanceSelected = false;
                if (e.IdleRounds == 0) e.LastChoiceSignature = null;
                e.Round = round;
                e.Idle = e.Busy = false;
                e.AppliedRecoveryRound = -1;
                e.MoveFallbackRound = -1;
            }
            else if (!same)
            {
                if (!sameOrder) e.AdvanceSelected = false;
                e.Continuing = false;
                e.IdleRounds = 0; e.Idle = false; e.Busy = true; e.AppliedRecoveryRound = -1; e.MoveFallbackRound = -1;
            }
            e.Agent = agent; e.X = x; e.Z = z; e.Order = order; e.Distance = distance;
            return (e.IdleRounds >= 2 || e.Continuing) && !e.Busy;
        }
    }

    internal void Choice(long map, int round, long actor, long agent, bool idleOnly, bool provisionalAdvance = false,
        bool advanceActivity = false)
    {
        lock (_sync)
        {
            if (_map != map || !_actors.TryGetValue(actor, out Entry e) || e.Round != round || e.Agent != agent) return;
            // A later Idle poll must never erase an earlier useful action/choice.
            if (idleOnly) e.Idle = true;
            // PickBehavior also runs provisionally before native re-evaluation. A legal
            // recovered Move is not movement evidence: retain only this round's eligibility.
            else if (provisionalAdvance && e.MoveFallbackRound == round && !e.Busy)
            {
                e.Idle = false;
                e.AdvanceSelected = true; e.AdvanceDistance = e.Distance; e.AdvanceRound = round;
            }
            else
            {
                // Movement completion polls may follow the provisional Move. Preserve
                // its origin evidence, but never grant another move in the same round.
                if (!advanceActivity) e.AdvanceSelected = false;
                e.Continuing = false;
                e.Busy = true; e.IdleRounds = 0; e.LastChoiceSignature = null; e.AppliedRecoveryRound = -1; e.MoveFallbackRound = -1;
            }
        }
    }

    internal bool ShouldLogDiagnostic(long map, long actor, int result)
    {
        lock (_sync)
        {
            if (map == 0 || actor == 0) return false;
            if (_diagnosticMap != map)
            {
                _diagnosticStages.Clear();
                _diagnosticMap = map;
            }
            if (_diagnosticStages.TryGetValue(actor, out int previous))
            {
                if (previous == result) return false;
                _diagnosticStages[actor] = result;
                return true;
            }
            if (_diagnosticStages.Count >= MaxActors) return false;
            _diagnosticStages.Add(actor, result);
            return true;
        }
    }

    internal void RecordRecoveryCheck(long map, int round, long actor, long agent, int result, bool applied)
    {
        lock (_sync)
        {
            if (_map != map || !_actors.TryGetValue(actor, out Entry e) || e.Round != round || e.Agent != agent) return;
            e.RecoveryRound = round;
            e.RecoveryResult = result;
            e.RecoveryApplied = applied;
        }
    }

    internal void MarkRecoveryApplied(long map, int round, long actor, long agent)
    {
        lock (_sync)
            if (_map == map && _actors.TryGetValue(actor, out Entry e) && e.Round == round && e.Agent == agent)
                e.AppliedRecoveryRound = e.MoveFallbackRound = round;
    }

    internal void MarkMoveFallbackEligible(long map, int round, long actor, long agent)
    {
        lock (_sync)
            if (_map == map && _actors.TryGetValue(actor, out Entry e) && e.Round == round && e.Agent == agent && !e.Busy)
                e.MoveFallbackRound = round;
    }

    internal bool HasRecoveryApplication(long map, int round, long actor, long agent)
    {
        lock (_sync)
            return _map == map && _actors.TryGetValue(actor, out Entry e) && e.Round == round &&
                e.Agent == agent && e.AppliedRecoveryRound == round && !e.Busy;
    }

    internal bool HasMoveFallbackEligibility(long map, int round, long actor, long agent)
    {
        lock (_sync)
            return _map == map && _actors.TryGetValue(actor, out Entry e) && e.Round == round &&
                e.Agent == agent && e.MoveFallbackRound == round && !e.Busy;
    }

    internal bool MatchesContext(long map, int round, long actor, long agent, int x, int z, AdvanceRecoveryOrder order)
    {
        lock (_sync)
            return _map == map && _actors.TryGetValue(actor, out Entry e) && e.Round == round &&
                e.Agent == agent && e.X == x && e.Z == z && e.Order == order && !e.Busy;
    }

    internal bool ShouldLogChoice(long map, int round, long actor, long agent, string signature,
        out int result, out bool applied)
    {
        lock (_sync)
        {
            result = 0;
            applied = false;
            if (_map != map || !_actors.TryGetValue(actor, out Entry e) || e.Round != round || e.Agent != agent ||
                (e.IdleRounds < 2 && !e.Continuing) || e.RecoveryRound != round) return false;
            string completeSignature = $"{e.RecoveryResult}|{e.RecoveryApplied}|{signature}";
            if (e.LastChoiceSignature == completeSignature) return false;
            e.LastChoiceSignature = completeSignature;
            result = e.RecoveryResult;
            applied = e.RecoveryApplied;
            return true;
        }
    }
}
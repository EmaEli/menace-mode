using System.Collections.Generic;

namespace AIPerformance;

internal enum CombinedArmsGuardDecision { Invalid, Hold, Expired, Capacity }

// Permit a short exposure veto, but never let it strand a unit for an entire mission.
internal sealed class CombinedArmsGuardState
{
    internal const int HoldRounds = 2, MaxActors = 256;
    private readonly object _gate = new();
    private readonly Dictionary<(long Faction, long Actor), int> _firstThreatRound = new();
    private long _map;
    private int _round;

    internal CombinedArmsGuardDecision Decide(long map, int round, long faction, long actor, out int firstRound)
    {
        lock (_gate)
        {
            firstRound = 0;
            if (map == 0 || round < 1 || faction == 0 || actor == 0) return CombinedArmsGuardDecision.Invalid;
            if (_map != map) { _map = map; _round = 0; _firstThreatRound.Clear(); }
            if (round < _round) return CombinedArmsGuardDecision.Invalid;
            _round = round;
            var key = (faction, actor);
            if (_firstThreatRound.TryGetValue(key, out firstRound))
                return round - firstRound < HoldRounds ? CombinedArmsGuardDecision.Hold : CombinedArmsGuardDecision.Expired;
            if (_firstThreatRound.Count >= MaxActors) return CombinedArmsGuardDecision.Capacity;
            firstRound = round;
            _firstThreatRound.Add(key, round);
            return CombinedArmsGuardDecision.Hold;
        }
    }

    internal void Reset()
    {
        lock (_gate) { _map = 0; _round = 0; _firstThreatRound.Clear(); }
    }
}
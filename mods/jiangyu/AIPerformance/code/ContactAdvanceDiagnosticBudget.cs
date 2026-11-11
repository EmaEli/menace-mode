using System.Collections.Generic;

namespace AIPerformance;

internal sealed class ContactAdvanceDiagnosticBudget
{
    internal const int MaxLines = 256;
    private readonly object _gate = new();
    private readonly HashSet<(int Round, long Faction, long Actor, string Phase)> _seen = new();
    private int _count;

    internal bool TryAcquire(int round, long faction, long actor, string phase)
    {
        if (round < 1 || phase == null) return false;
        lock (_gate)
        {
            var key = (round, faction, actor, phase);
            if (_seen.Contains(key) || _count >= MaxLines) return false;
            _seen.Add(key);
            _count++;
            return true;
        }
    }

    internal void Reset()
    {
        lock (_gate) { _seen.Clear(); _count = 0; }
    }
}
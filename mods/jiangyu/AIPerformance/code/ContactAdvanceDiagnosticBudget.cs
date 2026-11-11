using System.Collections.Generic;

namespace AIPerformance;

internal sealed class ContactAdvanceDiagnosticBudget
{
    internal const int MaxLines = 256, HookLines = 128, GroupLines = 128;
    private readonly object _gate = new();
    private readonly HashSet<(int Round, long Faction, long Actor, string Phase)> _seen = new();
    private readonly Dictionary<(int Round, long Faction), int> _groupChoices = new();
    private int _hooks, _groups;

    internal bool TryAcquire(int round, long faction, long actor, string phase)
    {
        if (round < 1 || phase == null) return false;
        lock (_gate)
        {
            var key = (round, faction, actor, phase);
            bool group = phase.StartsWith("group-", System.StringComparison.Ordinal);
            if (_seen.Contains(key) || (group ? _groups >= GroupLines : _hooks >= HookLines)) return false;
            var turn = (round, faction);
            if (phase == "group-choice")
            {
                _groupChoices.TryGetValue(turn, out int choices);
                if (choices >= 3) return false;
                _groupChoices[turn] = choices + 1;
            }
            _seen.Add(key);
            if (group) _groups++; else _hooks++;
            return true;
        }
    }

    internal void Reset()
    {
        lock (_gate) { _seen.Clear(); _groupChoices.Clear(); _hooks = _groups = 0; }
    }
}
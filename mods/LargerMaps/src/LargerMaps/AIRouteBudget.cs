using System;
using System.Collections.Generic;

namespace LargerMaps;

// Pure managed diagnostic accounting. No native/game references are retained.
internal sealed class AIRouteBudget
{
    private sealed class Entry
    {
        internal int Seen, Written, Repeated, Limited;
        internal string Last;
    }

    private readonly object _gate = new();
    private readonly Dictionary<(long, string), Entry> _entries = new();
    private long _map;
    private int _round = int.MinValue;
    private bool _overflow;
    internal const int MaxEntries = 16384;

    internal void Reset()
    {
        lock (_gate)
        {
            _entries.Clear(); _map = 0; _round = int.MinValue; _overflow = false;
        }
    }

    // 1 = write, 2 = first suppressed distinct event, 3 = buffer overflow warning.
    // Consecutive polls are deduplicated; real repeated skill uses are NOT.
    internal int Observe(long map, int round, long actor, string kind, string fingerprint,
        int limit, bool deduplicate)
    {
        lock (_gate)
        {
            if (_map != map || _round != round)
            {
                _entries.Clear(); _map = map; _round = round; _overflow = false;
            }
            var key = (actor, kind);
            if (!_entries.TryGetValue(key, out Entry entry))
            {
                if (_entries.Count >= MaxEntries)
                {
                    if (_overflow) return 0;
                    _overflow = true; return 3;
                }
                _entries[key] = entry = new Entry();
            }
            entry.Seen++;
            if (deduplicate && entry.Last == fingerprint) { entry.Repeated++; return 0; }
            entry.Last = fingerprint;
            if (entry.Written >= limit) return ++entry.Limited == 1 ? 2 : 0;
            entry.Written++;
            return 1;
        }
    }

    internal string Summary(long actor)
    {
        lock (_gate)
        {
            var parts = new List<string>();
            foreach (var pair in _entries)
                if (pair.Key.Item1 == actor)
                {
                    Entry e = pair.Value;
                    parts.Add($"{pair.Key.Item2}:seen={e.Seen},written={e.Written},repeat={e.Repeated},limited={e.Limited}");
                }
            return string.Join(";", parts);
        }
    }

    internal static double Distance(int x, int z, int xmin, int zmin, int width, int height)
    {
        if (x < 0 || z < 0 || width <= 0 || height <= 0) return double.NaN;
        double dx = Math.Max(xmin - x, Math.Max(0, x - (xmin + width - 1)));
        double dz = Math.Max(zmin - z, Math.Max(0, z - (zmin + height - 1)));
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
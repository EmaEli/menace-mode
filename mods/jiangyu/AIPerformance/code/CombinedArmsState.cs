using System;
using System.Collections.Generic;

namespace AIPerformance;

internal enum SupportKind { Infantry, Vehicle, Heavy }
internal sealed class SupportUnit
{
    internal long Actor { get; }
    internal long Agent { get; }
    internal SupportKind Kind { get; }
    internal int X { get; }
    internal int Z { get; }
    internal AdvanceRecoveryOrder Order { get; }
    internal bool HasOrder { get; }
    internal int AimX { get; }
    internal int AimZ { get; }
    internal SupportUnit(long actor, long agent, SupportKind kind, int x, int z,
        AdvanceRecoveryOrder order, bool hasOrder, int aimX, int aimZ)
        => (Actor, Agent, Kind, X, Z, Order, HasOrder, AimX, AimZ) = (actor, agent, kind, x, z, order, hasOrder, aimX, aimZ);
}

// Managed snapshots only. Native objects and hidden-opponent information never enter this owner.
internal sealed class CombinedArmsState
{
    internal const int MaxUnits = 128, MaxFactions = 4, CellSize = 8, Radius = 24;
    private readonly object _gate = new();
    private readonly Dictionary<long, Group> _groups = new();
    private long _map;
    private sealed class Group
    {
        internal int Round;
        internal Dictionary<long, SupportUnit> Units = new();
        internal Dictionary<(int, int), List<SupportUnit>> Cells = new();
        internal Dictionary<long, (long Request, int Since)> Assignments = new();
        internal Dictionary<(long Request, SupportKind Kind), long> Slots = new();
        internal HashSet<long> Progress = new();
    }

    internal void Reset() { lock (_gate) { _map = 0; _groups.Clear(); } }

    internal bool NeedsPublish(long map, int round, long faction)
    {
        lock (_gate)
            return map != 0 && round > 0 && (_map == 0 || _map == map) &&
                (!_groups.TryGetValue(faction, out var group) || round > group.Round);
    }

    internal bool Publish(long map, int round, long faction, IReadOnlyList<SupportUnit> units)
    {
        lock (_gate)
        {
            if (map == 0 || faction == 0 || round < 1 || units == null || units.Count > MaxUnits) return false;
            if (_map == 0) _map = map; // Only the main-thread publisher can establish a mission.
            if (_map != map) return false;
            if (!_groups.TryGetValue(faction, out var group))
            {
                if (_groups.Count >= MaxFactions) return false;
                _groups[faction] = group = new Group();
            }
            if (round <= group.Round) return false; // Repeated turn events cannot renew assignments.
            var next = new Dictionary<long, SupportUnit>();
            foreach (var unit in units)
                if (unit != null && unit.Actor != 0 && unit.Agent != 0 && !next.ContainsKey(unit.Actor)) next.Add(unit.Actor, unit);
            var keep = new Dictionary<long, (long Request, int Since)>();
            foreach (var pair in group.Assignments)
                if (round == group.Round + 1 && round - pair.Value.Since < 2 &&
                    next.TryGetValue(pair.Key, out var asset) && group.Units.TryGetValue(pair.Key, out var oldAsset) &&
                    asset.Agent == oldAsset.Agent && asset.Order == oldAsset.Order &&
                    next.TryGetValue(pair.Value.Request, out var request) &&
                    group.Units.TryGetValue(pair.Value.Request, out var oldRequest) &&
                    request.Agent == oldRequest.Agent && request.Order == oldRequest.Order && Compatible(asset, request) &&
                    (group.Progress.Contains(request.Actor) || request.X != oldRequest.X || request.Z != oldRequest.Z)) keep[pair.Key] = pair.Value;
            group.Round = round; group.Units = next; group.Assignments = keep;
            group.Progress.Clear(); RebuildSlots(group);
            Reindex(group);
            return true;
        }
    }

    private static void Reindex(Group group)
    {
        group.Cells = new();
        foreach (var unit in group.Units.Values)
        {
            if (unit.Kind != SupportKind.Infantry) continue;
            var cell = (unit.X / CellSize, unit.Z / CellSize);
            if (!group.Cells.TryGetValue(cell, out var list)) group.Cells[cell] = list = new();
            list.Add(unit);
        }
    }

    // Update only the acting unit on main-thread lifecycle events, never a faction-wide hot scan.
    internal void Refresh(long map, int round, long faction, long actor, SupportUnit unit)
    {
        lock (_gate)
        {
            if (_map != map || !_groups.TryGetValue(faction, out var group) || group.Round != round ||
                !group.Units.TryGetValue(actor, out var previous)) return;
            bool changed = unit == null || unit.Agent != previous.Agent || unit.Order != previous.Order ||
                unit.Kind != previous.Kind || unit.HasOrder != previous.HasOrder || unit.X != previous.X || unit.Z != previous.Z;
            if (!changed) return;
            if (unit != null && unit.Kind == SupportKind.Infantry && unit.Order == previous.Order &&
                Math.Abs(unit.AimX - unit.X) + Math.Abs(unit.AimZ - unit.Z) <
                Math.Abs(previous.AimX - previous.X) + Math.Abs(previous.AimZ - previous.Z)) group.Progress.Add(actor);
            if (unit == null) group.Units.Remove(actor); else group.Units[actor] = unit;
            var remove = new List<long>();
            bool contextChanged = unit == null || unit.Agent != previous.Agent || unit.Order != previous.Order ||
                unit.Kind != previous.Kind || unit.HasOrder != previous.HasOrder;
            foreach (var pair in group.Assignments)
                if (contextChanged && (pair.Key == actor || pair.Value.Request == actor)) remove.Add(pair.Key);
            foreach (long key in remove) group.Assignments.Remove(key);
            RebuildSlots(group);
            Reindex(group);
        }
    }

    internal SupportUnit[] Candidates(long map, int round, long faction, SupportUnit asset)
        => Candidates(map, round, faction, asset, false, out _);

    internal SupportUnit[] Candidates(long map, int round, long faction, SupportUnit asset,
        bool collectDiagnostics, out SupportCandidateDiagnostics diagnostics)
    {
        diagnostics = default;
        lock (_gate)
        {
            Group group = null;
            SupportCandidateGate gate = SupportCandidateGate.Ready;
            if (_map != map) gate = SupportCandidateGate.MapMismatch;
            else if (!_groups.TryGetValue(faction, out group)) gate = SupportCandidateGate.FactionNotPublished;
            else if (group.Round != round) gate = SupportCandidateGate.RoundMismatch;
            else if (!group.Units.TryGetValue(asset.Actor, out var known)) gate = SupportCandidateGate.AssetNotPublished;
            else if (known.Agent != asset.Agent) gate = SupportCandidateGate.AgentMismatch;
            else if (known.Order != asset.Order) gate = SupportCandidateGate.OrderMismatch;
            else if (asset.Kind == SupportKind.Infantry) gate = SupportCandidateGate.AssetIsInfantry;
            if (gate != SupportCandidateGate.Ready)
            {
                if (collectDiagnostics) diagnostics = new(gate, 0, 0, 0, 0, 0, 0);
                return Array.Empty<SupportUnit>();
            }
            var list = new List<SupportUnit>();
            int nearby = 0, compatible = 0, slotBusy = 0;
            int cx = asset.X / CellSize, cz = asset.Z / CellSize;
            for (int x = cx - 3; x <= cx + 3; x++)
            for (int z = cz - 3; z <= cz + 3; z++)
                if (group.Cells.TryGetValue((x, z), out var entries))
                    foreach (var request in entries)
                    {
                        if (collectDiagnostics) nearby++;
                        if (!Compatible(asset, request)) continue;
                        if (collectDiagnostics) compatible++;
                        if (!Available(group, asset, request.Actor))
                        {
                            if (collectDiagnostics) slotBusy++;
                            continue;
                        }
                        list.Add(request);
                    }
            group.Assignments.TryGetValue(asset.Actor, out var assigned);
            list.Sort((a, b) =>
            {
                bool aa = a.Actor == assigned.Request, bb = b.Actor == assigned.Request;
                if (aa != bb) return aa ? -1 : 1;
                int distance = Distance2(asset, a).CompareTo(Distance2(asset, b));
                return distance != 0 ? distance : a.Actor.CompareTo(b.Actor);
            });
            int truncated = Math.Max(0, list.Count - 4);
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            if (collectDiagnostics)
                diagnostics = new(SupportCandidateGate.Ready, nearby, compatible, slotBusy,
                    list.Count, truncated, assigned.Request);
            return list.ToArray();
        }
    }

    private static bool Available(Group group, SupportUnit asset, long request)
        => !group.Slots.TryGetValue((request, asset.Kind), out long owner) || owner == asset.Actor;

    private static void RebuildSlots(Group group)
    {
        group.Slots.Clear();
        foreach (var pair in group.Assignments)
            if (group.Units.TryGetValue(pair.Key, out var asset)) group.Slots[(pair.Value.Request, asset.Kind)] = pair.Key;
    }

    internal bool Claim(long map, int round, long faction, SupportUnit asset, SupportUnit request)
    {
        lock (_gate)
        {
            if (_map != map || !_groups.TryGetValue(faction, out var group) || group.Round != round ||
                !group.Units.TryGetValue(asset.Actor, out var a) || a.Agent != asset.Agent || a.Order != asset.Order ||
                !group.Units.TryGetValue(request.Actor, out var r) || r != request || !Compatible(asset, r) ||
                !Available(group, asset, r.Actor)) return false;
            group.Assignments[asset.Actor] = (r.Actor, group.Assignments.TryGetValue(asset.Actor, out var old) &&
                old.Request == r.Actor ? old.Since : round);
            if (old.Request != r.Actor) group.Slots.Remove((old.Request, asset.Kind));
            group.Slots[(r.Actor, asset.Kind)] = asset.Actor;
            return true;
        }
    }

    internal void Release(long map, int round, long faction, long asset)
    {
        lock (_gate)
            if (_map == map && _groups.TryGetValue(faction, out var group) && group.Round == round)
            {
                if (group.Assignments.TryGetValue(asset, out var old) && group.Units.TryGetValue(asset, out var unit))
                    group.Slots.Remove((old.Request, unit.Kind));
                group.Assignments.Remove(asset);
            }
    }

    private static int Distance2(SupportUnit a, SupportUnit b) => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
    private static bool Compatible(SupportUnit asset, SupportUnit request) => asset.Actor != request.Actor &&
        request.Kind == SupportKind.Infantry && request.HasOrder && asset.Kind != SupportKind.Infantry &&
        Distance2(asset, request) <= Radius * Radius && (!asset.HasOrder ||
        (Math.Abs((long)asset.AimX - request.AimX) + Math.Abs((long)asset.AimZ - request.AimZ) <= Radius &&
         (long)(asset.AimX - asset.X) * (request.AimX - request.X) +
         (long)(asset.AimZ - asset.Z) * (request.AimZ - request.Z) > 0));
}
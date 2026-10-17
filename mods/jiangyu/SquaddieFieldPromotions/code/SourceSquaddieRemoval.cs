using System;
using System.Collections.Generic;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;

namespace SquaddieFieldPromotions;

/// <summary>
/// Targeted equivalent of the native pool removal, never a death or a random removal.
/// Snapshots preserve ordering and the exact native objects for synchronous rollback.
/// No notifications are sent until the promotion and ledger have both committed.
/// </summary>
internal sealed class SourceSquaddieRemoval
{
    private readonly Squaddies _pool;
    private readonly int _id;
    private readonly Il2CppSystem.Collections.Generic.List<Squaddie> _list;
    private readonly List<Squaddie> _before = new();
    private readonly BaseUnitLeader _owner;
    private readonly List<int> _assignment = new();
    private bool _started;

    internal SourceSquaddieRemoval(StrategyState state, PromotionRecord record)
    {
        _pool = state.Squaddies ?? throw new InvalidOperationException("Squaddie pool unavailable.");
        _id = record.SourceSquaddieId;
        if (_id <= 0) throw new InvalidOperationException("Invalid source squaddie ID.");
        if (_pool.m_AliveSquaddies == null || _pool.m_WoundedSquaddies == null || _pool.m_KilledSquaddies == null)
            throw new NotSupportedException("Native squaddie pool layout unavailable.");
        Squaddie source = null;
        foreach (var list in new[] { _pool.m_AliveSquaddies, _pool.m_WoundedSquaddies })
        {
            for (int i = 0; i < list.Count; i++)
            {
                var soldier = list[i] ?? throw new InvalidOperationException("Null source pool entry.");
                if (soldier.GetId() != _id) continue;
                if (source != null) throw new InvalidOperationException("Duplicate source squaddie ID.");
                source = soldier;
                _list = list;
            }
        }
        // Already absent (e.g. an old preview soldier subsequently killed): nothing to remove.
        if (source == null) return;
        if (source.Name != record.Name || source.Nickname != record.Nickname ||
            (int)source.GetGender() != record.Gender || (int)source.GetSkinColor() != record.SkinColor)
            throw new InvalidOperationException("Source identity no longer matches the promotion record.");
        for (int i = 0; i < _pool.m_KilledSquaddies.Count; i++)
            if (_pool.m_KilledSquaddies[i]?.GetId() == _id)
                throw new InvalidOperationException("Source appears in both living and killed pools.");
        for (int i = 0; i < _list.Count; i++) _before.Add(_list[i]);
        // GetCurrentLeader and TryRemoveSquaddie are the same native path used by pool removal.
        _owner = source.GetCurrentLeader();
        if (_owner != null)
        {
            var ids = _owner.m_SquaddieIds ?? throw new NotSupportedException("Squad assignment list unavailable.");
            int matches = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                _assignment.Add(ids[i]);
                if (ids[i] == _id) matches++;
            }
            if (matches != 1) throw new InvalidOperationException("Ambiguous source squad assignment.");
        }
    }

    internal bool Present => _list != null;

    internal void Remove()
    {
        if (!Present) return;
        _started = true;
        if (_owner != null && (!_owner.TryRemoveSquaddie(_id) || _owner.HasSquaddie(_id)))
            throw new InvalidOperationException("Cannot detach the promoted soldier from its squad.");
        int index = -1;
        for (int i = 0; i < _list.Count; i++)
            if (_list[i]?.GetId() == _id) index = i;
        if (index < 0 || _list.Count != _before.Count)
            throw new InvalidOperationException("Source pool changed during promotion.");
        _list.RemoveAt(index);
        if (_list.Count != _before.Count - 1 || _pool.IsAlive(_id))
            throw new InvalidOperationException("Source removal readback failed.");
        for (int i = 0; i < _list.Count; i++)
            if (_list[i]?.GetId() == _id) throw new InvalidOperationException("Source still in the pool.");
    }

    internal void Restore()
    {
        if (!_started) return;
        _list.Clear();
        foreach (var soldier in _before) _list.Add(soldier);
        if (_owner != null)
        {
            _owner.m_SquaddieIds.Clear();
            foreach (int id in _assignment) _owner.m_SquaddieIds.Add(id);
        }
        _started = false;
    }

    internal void NotifyChanged()
    {
        if (Present) _pool.OnSquaddiesChanged?.Invoke();
    }
}
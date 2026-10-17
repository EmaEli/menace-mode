using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using SquaddieFieldPromotions;

internal static class SourceRemovalChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var candidate = new Candidate(42, "Source", "Nickname", 0, 1, 5, 100, true);
        var record = LeaderForge.CreateRecord(null, new Selection(candidate, 3, true, false), 0);
        var state = new StrategyState();
        var owner = new BaseUnitLeader();
        owner.m_SquaddieIds.AddRange(new[] { 11, 42, 12 });
        var source = new Squaddie { Id = 42, Name = "Source", Nickname = "Nickname", Gender = 0, SkinColor = 1, Owner = owner };
        var other = new Squaddie { Id = 12 };
        state.Squaddies.m_AliveSquaddies.AddRange(new[] { other, source });
        var removal = new SourceSquaddieRemoval(state, record);
        removal.Remove();
        check(!owner.HasSquaddie(42) && !state.Squaddies.IsAlive(42), "assigned source detached by exact ID");
        removal.Restore();
        check(owner.m_SquaddieIds.SequenceEqual(new[] { 11, 42, 12 }) &&
            state.Squaddies.m_AliveSquaddies.SequenceEqual(new[] { other, source }), "rollback restores original objects and ordering");
        owner.FailDetach = true;
        try { new SourceSquaddieRemoval(state, record).Remove(); throw new Exception("detach unexpectedly succeeded"); }
        catch (InvalidOperationException) { }
        check(state.Squaddies.IsAlive(42) && owner.HasSquaddie(42), "failed detachment preserves assigned soldier");
        owner.FailDetach = false;
        source.Name = "Different person";
        try { new SourceSquaddieRemoval(state, record); throw new Exception("identity mismatch accepted"); }
        catch (InvalidOperationException) { }
        check(state.Squaddies.IsAlive(42), "mismatched identity never removed");
        source.Name = "Source";
        state.Squaddies.m_AliveSquaddies.Add(source);
        try { new SourceSquaddieRemoval(state, record); throw new Exception("duplicate source accepted"); }
        catch (InvalidOperationException) { }
        state.Squaddies.m_AliveSquaddies.RemoveAt(2);
        state.Squaddies.m_AliveSquaddies.Remove(source);
        state.Squaddies.m_WoundedSquaddies.Add(source);
        removal = new SourceSquaddieRemoval(state, record);
        removal.Remove();
        check(state.Squaddies.m_WoundedSquaddies.Count == 0 && state.Squaddies.m_KilledSquaddies.Count == 0,
            "old preview source can be removed from wounded pool without a fake death");
        removal.Restore();
        check(state.Squaddies.m_WoundedSquaddies.Single() == source && owner.HasSquaddie(42), "wounded-source rollback restores assignment");
        state.Squaddies.m_WoundedSquaddies.Clear();
        owner.m_SquaddieIds.Remove(42);
        removal = new SourceSquaddieRemoval(state, record);
        check(!removal.Present, "already absent preview source is a no-op");
        removal.Remove(); removal.Restore();
        check(state.Squaddies.m_AliveSquaddies.Single() == other, "absent source never removes a different soldier");
    }
}
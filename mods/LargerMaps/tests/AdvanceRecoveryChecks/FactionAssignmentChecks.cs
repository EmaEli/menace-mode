using AIPerformance;

internal static class FactionAssignmentChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var state = new ContactAdvanceState();
        state.Observe(1, 1, 6, new(10, 70, 20, 1, 25));
        state.Observe(1, 1, 6, new(20, 20, 70, 1, 25));
        check(state.TryAssign(1, 1, 6, 100, 0, 15, out var first) && first.Target == 10,
            "sector assignment picks a deterministic legitimate contact");
        check(state.TryAssign(1, 1, 6, 101, 15, 0, out var neighbor) && neighbor.Target == first.Target,
            "neighbors converge on one shared objective instead of independently picking opposite targets");
        state.Observe(1, 2, 6, new(30, 65, 65, 2, 25));
        for (int i = 0; i < 1000; i++)
            check(state.TryAssign(1, 2, 6, 100, 65, 65, out var stable) && stable.Target == first.Target,
                "fresh evaluations and a new nearer contact cannot churn a two-round objective");
        check(state.TryAssign(1, 3, 6, 100, 65, 65, out var next) && next.Target == 30,
            "expired assignment can choose a newly relevant known sector");
        state.Forget(1, 3, 6, 30);
        check(state.TryAssign(1, 3, 6, 100, 65, 65, out next) && next.Target != 30,
            "a legitimately observed death invalidates its current assignment immediately");
        check(!state.TryAssign(1, 3, 7, 100, 65, 65, out _), "faction knowledge is not shared with other hostile factions");
        check(!state.TryAssign(2, 3, 6, 100, 65, 65, out _), "a different mission map cannot inherit objectives");
        check(!state.TryAssign(1, 2, 6, 100, 65, 65, out _), "worker cannot rewind assignment generation");
        check(!state.TryAssign(1, 14, 6, 100, 65, 65, out _), "lost contact expiry releases the objective rather than chasing forever");

        state.Reset();
        foreach (long faction in new long[] { 5, 6, 7, 8, 9 })
        {
            for (int i = 0; i < ContactAdvanceState.MaxContacts; i++)
                check(state.Observe(1, 1, faction, new(i + 1, 70, 20, 1, 25)), "contact allowance is per faction, not consumed by the first faction's sightings");
            check(!state.Observe(1, 1, faction, new(1000, 70, 20, 1, 25)), "per-faction contact count remains bounded");
            for (int i = 0; i < 45; i++)
                check(state.TryAssign(1, 1, faction, i + 1, 10, 20, out _), "all five factions can simultaneously assign 45 units");
        }
        check(state.Count(1, 1, 5) == 32 && state.Count(1, 1, 9) == 32, "earlier factions cannot starve later faction memory");
        state.Reset(); state.Observe(1, 1, 6, new(1, 70, 20, 1, 25));
        for (int actor = 1; actor <= ContactAdvanceState.MaxAssignments; actor++)
            check(state.TryAssign(1, 1, 6, actor, 10, 20, out _), "managed assignment storage accepts only its fixed capacity");
        check(!state.TryAssign(1, 1, 6, ContactAdvanceState.MaxAssignments + 1, 10, 20, out _), "assignment capacity fails closed");
        check(state.TryAssign(1, 1, 6, 1, 10, 20, out _), "capacity cannot evict an existing unit's assignment");
        check(state.TryAssign(1, 3, 6, ContactAdvanceState.MaxAssignments + 1, 10, 20, out _), "round expiry reclaims stale unit slots");
        state.Reset();
        check(!state.TryAssign(2, 1, 6, 1, 10, 20, out _), "mission reset clears contact objectives and assignment leases together");
    }
}
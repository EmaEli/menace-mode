using CoordinatedFire.Core;

internal static class CoordinatedFireChecks
{
    public static void Run(Action<bool, string> check)
    {
        int scenarios = 0;
        int[][] sequences = { Array.Empty<int>(), new[] { 0 }, new[] { 5 }, new[] { 30 },
            new[] { 30, 30 }, new[] { 12, 30, 20 }, new[] { 60 }, new[] { 0, 50 } };
        foreach (int maximum in new[] { 40, 60, 80, 100, 120 })
        foreach (int cost in new[] { 0, 5, 15, 20, 30, 40, 50 })
        foreach (int spent in new[] { 0, 1, 5, 10, 15, 29, 30, 40, 70 })
        foreach (int[] grants in sequences)
        {
            int start = maximum - spent;
            if (start < cost || start < 0) continue;
            int actual = start, carried = 0, expected = start - cost;
            foreach (int grant in grants)
            {
                carried = Fee.AccumulateClippedGain(carried,
                    Fee.ClippedGain(actual, actual + grant, maximum), cost);
                actual = Math.Min(maximum, actual + grant);
                expected = Math.Min(maximum, expected + grant);
            }
            check(Fee.AfterPreservingClippedGains(actual, cost, carried) == expected,
                $"actual C# Berserk arithmetic: maximum={maximum}, cost={cost}, spent={spent}");
            check(carried >= 0 && carried <= cost, "multiple kills cannot carry more than fee");
            scenarios++;
        }
        check(scenarios == 2072, "all original positive-grant/cap scenarios executed in actual C#");
        check(Fee.ClippedGain(95, 125, 100) == 25 && Fee.AfterPreservingClippedGains(100, 30, 25) == 95,
            "reported five-AP example");
        check(Fee.ClippedGain(110, 140, 100) == 0, "already over-cap AP not credited");
        check(Fee.ClippedGain(95, 90, 100) == 0, "AP penalty not credited");
        check(Fee.SuppressNativeDebit(95, 65, 30) == 95, "exact native child debit suppressed");
        check(Fee.SuppressNativeDebit(95, 70, 30) == 70, "unrelated debit untouched");
        check(Fee.SuppressNativeDebit(95, 125, 30) == 125, "positive grant untouched");
        check(Fee.After(0, 30) == 0, "fee debit never negative");

        var actor = new ActorSnapshot(1, true, true, false, true, false, false, false, false, 95);
        var squad = new AttackSnapshot(1, 1, 1, "squad", WeaponRole.Squad,
            30, 1, 12, true, true, true, 16, true, true, false, true, false, true);
        var leader = squad with { SkillKey = 2, WeaponKey = 2, Id = "leader", Role = WeaponRole.Leader, ActionPointCost = 20 };
        Decision ready = Rules.Evaluate(actor, squad, leader);
        check(ready.Allowed && ready.Plan?.Cost == 30, "one maximum-child fee");
        check(ready.Plan?.First.SkillKey == squad.SkillKey, "higher-cost first attack");
        foreach (AttackSnapshot invalid in new[]
        {
            leader with { IsAffordable = false }, leader with { IsUsable = false },
            leader with { AmmoRemaining = 0 }, leader with { IsDirectFire = false },
            leader with { RequiresDeployment = true, DeploymentSatisfied = false },
            leader with { RequiresSetup = true, SetupSatisfied = false },
            leader with { ActorKey = 2 }, leader with { WeaponKey = squad.WeaponKey },
            leader with { MinRange = 13, MaxRange = 15 },
        })
            check(!Rules.Evaluate(actor, squad, invalid).Allowed, "second weapon rules not bypassed");
        check(!Rules.Evaluate(actor with { ActionPoints = 29 }, squad, leader).Allowed, "combined fee affordability");
        check(!AIPolicy.CanReplaceWithoutOwningTurnEnd(30, 30), "AI native final-AP handoff retained");
        check(AIPolicy.CanReplaceWithoutOwningTurnEnd(31, 30), "AI replacement requires spare AP");
    }
}
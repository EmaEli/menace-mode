using MenaceOverwatch;

internal static class OverwatchAIWeaponPlannerChecks
{
    private sealed record Candidate(string Name, bool Squad, bool Leader, int Cost);

    internal static void Run(Action<bool, string> check)
    {
        var leader = new Candidate("leader-longest", false, true, 30);
        var squad = new Candidate("squad-partner", true, false, 20);
        var otherLeader = new Candidate("leader-shorter", false, true, 15);
        Candidate[] ordered = { leader, squad, otherLeader };

        var paired = AIOverwatchWeaponPlanner.Select(ordered, x => x.Squad, x => x.Leader,
            x => x.Cost, allowPair: true);
        check(paired.HasValue && ReferenceEquals(paired.Value.Primary, leader)
            && ReferenceEquals(paired.Value.Secondary, squad) && paired.Value.Cost == 30,
            "AI Overwatch pairs the ordered attack with the other infantry weapon at the maximum AP fee");
        check(paired.HasValue && AIReactionBudget.Calculate(29, paired.Value.Cost, 2, 0) == 1,
            "paired Overwatch uses the combined fee for its fractional AP reaction chance");

        var single = AIOverwatchWeaponPlanner.Select(ordered, x => x.Squad, x => x.Leader,
            x => x.Cost, allowPair: false);
        check(single.HasValue && ReferenceEquals(single.Value.Primary, leader)
            && single.Value.Secondary == null && single.Value.Cost == 30,
            "AI Overwatch retains single-weapon behavior when pairing is unavailable");

        Candidate[] oneRole = { squad };
        var fallback = AIOverwatchWeaponPlanner.Select(oneRole, x => x.Squad, x => x.Leader,
            x => x.Cost, allowPair: true);
        check(fallback.HasValue && ReferenceEquals(fallback.Value.Primary, squad)
            && fallback.Value.Secondary == null,
            "AI Overwatch falls back safely when the opposite infantry weapon is absent");

        check(AIOverwatchWeaponPlanner.Select(Array.Empty<Candidate>(), x => x.Squad,
            x => x.Leader, x => x.Cost, allowPair: true) == null,
            "AI Overwatch does not create a reservation without eligible weapons");
    }
}
using MenaceInfantryAssault;

internal static class AssaultDefenseChecks
{
    internal static void Run(Action<bool, string> check)
    {
        for (int suppression = 0; suppression <= 2; suppression++)
        for (int stance = 0; stance <= 3; stance++)
        foreach (bool alive in new[] { false, true })
        foreach (bool dying in new[] { false, true })
        foreach (bool stunned in new[] { false, true })
        {
            bool expected = alive && !dying && !stunned && suppression < 2 && stance != 2;
            check(AssaultDefenseRules.CanReact(alive, dying, suppression, stance, stunned) == expected,
                $"defensive fire: suppression={suppression}, stance={stance}, alive={alive}, dying={dying}, stunned={stunned}");
        }
        check(AssaultDefenseRules.CqbBonusScale(0) == 1f, "ready defender: full CQB bonus");
        check(AssaultDefenseRules.CqbBonusScale(1) == 0.5f, "suppressed defender: half CQB bonus");
        check(!AssaultDefenseRules.CanReact(true, false, 2, 0, false), "pinned suppression alone blocks fire");
        check(!AssaultDefenseRules.CanReact(true, false, 0, 2, false), "pinned stance alone blocks fire");
    }
}
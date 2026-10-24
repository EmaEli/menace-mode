using System;

namespace MenaceOverwatch;

internal static class AIReactionBudget
{
    // Whole AP costs guarantee reactions. A single discrete draw allocates the fractional
    // remainder with exact probability remainder/cost; callers cap the result by available ammo.
    internal static int Calculate(int availableAp, int cost, int ammoBound, int randomDraw)
    {
        if (availableAp <= 0 || cost <= 0 || ammoBound <= 0) return 0;

        int reactions = availableAp / cost;
        int remainder = availableAp % cost;
        if (remainder > 0 && randomDraw >= 0 && randomDraw < remainder) reactions++;
        return Math.Min(reactions, ammoBound);
    }
}

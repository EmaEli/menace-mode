using System;

namespace MenaceOverwatch;

internal static class AIReactionBudget
{
    // Whole AP costs guarantee reactions. The multiplier scales only the fractional remainder
    // chance, capped at 100%; callers cap the result by available ammo.
    internal static int FractionalDrawThreshold(int remainder, int cost, int multiplier)
    {
        if (remainder <= 0 || cost <= 0) return 0;
        return (int)Math.Min(cost, (long)remainder * Math.Clamp(multiplier, 1, 4));
    }

    internal static int Calculate(int availableAp, int cost, int ammoBound, int randomDraw, int fractionalChanceMultiplier = 1)
    {
        if (availableAp <= 0 || cost <= 0 || ammoBound <= 0) return 0;

        int reactions = availableAp / cost;
        int remainder = availableAp % cost;
        int threshold = FractionalDrawThreshold(remainder, cost, fractionalChanceMultiplier);
        if (threshold >= cost || (threshold > 0 && randomDraw >= 0 && randomDraw < threshold)) reactions++;
        return Math.Min(reactions, ammoBound);
    }
}

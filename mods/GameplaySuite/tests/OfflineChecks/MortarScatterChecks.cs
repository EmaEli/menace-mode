using MenaceWeaponMechanicsRebalanced;

internal static class MortarScatterChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int baseScatter in new[] { 3, 4, 5 })
        {
            for (int shots = 0; shots <= 12; shots++)
            {
                check(MortarScatterRules.EffectiveScatter(baseScatter, shots, false)
                    == Math.Max(2, baseScatter - shots), "unsolved mortar progression");
                check(MortarScatterRules.EffectiveScatter(baseScatter, shots, true) == 1,
                    "exact hit unlocks minimum scatter at every shot count");
            }
            check(MortarScatterRules.EffectiveScatter(baseScatter, -5, false) == baseScatter,
                "negative history cannot widen scatter");
            check(MortarScatterRules.EffectiveScatter(baseScatter, int.MaxValue, false) == 2,
                "large history cannot overflow scatter");
        }
        foreach (int baseScatter in new[] { -1, 0, 1 })
        foreach (bool solved in new[] { false, true })
            check(MortarScatterRules.EffectiveScatter(baseScatter, 20, solved) == Math.Max(0, baseScatter),
                "ranging never widens naturally small scatter (e.g. smoke)");

        for (int radius = 0; radius <= 5; radius++)
        {
            int segments = 0;
            for (int dz = -radius - 1; dz <= radius + 1; dz++)
            for (int dx = -radius - 1; dx <= radius + 1; dx++)
            {
                int edges = MortarScatterRules.BorderEdges(dx, dz, radius);
                if (Math.Abs(dx) > radius || Math.Abs(dz) > radius)
                    check(edges == 0, "no preview edges outside the scatter envelope");
                else if (Math.Abs(dx) < radius && Math.Abs(dz) < radius)
                    check(edges == 0, "no internal edges in the envelope");
                check(((edges & 1) != 0) == (Math.Abs(dx) <= radius && dz == -radius), "south border");
                check(((edges & 2) != 0) == (dx == radius && Math.Abs(dz) <= radius), "east border");
                check(((edges & 4) != 0) == (Math.Abs(dx) <= radius && dz == radius), "north border");
                check(((edges & 8) != 0) == (dx == -radius && Math.Abs(dz) <= radius), "west border");
                for (int bit = 1; bit <= 8; bit <<= 1)
                    if ((edges & bit) != 0) segments++;
            }
            check(segments == 4 * (2 * radius + 1), "closed square uses 2*Scatter+1 tiles per side");
        }
    }
}
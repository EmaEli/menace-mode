using SmokeOverhaul;

internal static class SmokeAiChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (bool pinned in new[] { false, true })
        foreach (bool suppressed in new[] { false, true })
        {
            int expected = pinned ? 60 : suppressed ? 45 : 30;
            check(SmokeAiRules.Chance(pinned, suppressed) == expected, "original proactive smoke odds / pinned precedence");
            check(Enumerable.Range(0, 100).Count(roll => roll < SmokeAiRules.Chance(pinned, suppressed)) == expected,
                "strict 0..99 threshold is exactly 60/45/30, not >= off by one");
        }
        int scenarios = 0;
        foreach (bool advance in new[] { false, true })
        for (int dx = -8; dx <= 8; dx++)
        for (int dz = -8; dz <= 8; dz++)
        {
            int valid = 0;
            for (int reach = 0; reach < 2; reach++)
            for (int candidate = 0; candidate < 3; candidate++)
            {
                if (!SmokeAiRules.TryGetLocalOffset(dx, dz, advance, reach, candidate, out int x, out int z)) continue;
                valid++;
                check(x * dx + z * dz > 0, "smoke never goes behind the detected threat bearing");
                check((dx - x) * (dx - x) + (dz - z) * (dz - z) < dx * dx + dz * dz,
                    "every accepted smoke candidate is strictly closer to the known threat");
                check(Math.Max(Math.Abs(x), Math.Abs(z)) <= 2, "local smoke candidates never exceed two tiles");
            }
            check(valid <= 6, "at most six candidates regardless of map size");
            check(dx == 0 && dz == 0 ? valid == 0 : valid > 0, "all noncoincident bearings have candidates");
            scenarios++;
        }
        check(scenarios == 578, "all 17x17 bearings including zero / both candidate orders tested");
        check(SmokeAiRules.TryGetLocalOffset(7, 0, false, 0, 0, out int nearX, out int nearZ) && nearX == 1 && nearZ == 0,
            "defense prefers the adjacent forward tile");
        check(SmokeAiRules.TryGetLocalOffset(7, 0, true, 0, 0, out int farX, out int farZ) && farX == 2 && farZ == 0,
            "exposed flamethrowers prefer the further forward tile");
        foreach (int bad in new[] { -1, 2, 3 })
            check(!SmokeAiRules.TryGetLocalOffset(7, 0, false, bad, 0, out _, out _), "invalid reach index fails closed");
        foreach (int bad in new[] { -1, 3, 4 })
            check(!SmokeAiRules.TryGetLocalOffset(7, 0, false, 0, bad, out _, out _), "invalid candidate index fails closed");
    }
}
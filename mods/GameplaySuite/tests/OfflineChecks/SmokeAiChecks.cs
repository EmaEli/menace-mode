using SmokeOverhaul;

internal static class SmokeAiChecks
{
    public static void Run(Action<bool, string> check)
    {
        check(SmokeAiRules.Chance(false, false, true) == 100, "equipped infantry answers a coordinated smoke request without repeated random waiting");
        check(SmokeAiRules.Chance(true, false, true) == 60 && SmokeAiRules.Chance(false, true, true) == 45,
            "support request does not change native emergency smoke probabilities");
        foreach (bool pinned in new[] { false, true })
        foreach (bool suppressed in new[] { false, true })
        {
            int expected = pinned ? 60 : suppressed ? 45 : 30;
            check(SmokeAiRules.Chance(pinned, suppressed) == expected, "original proactive smoke odds / pinned precedence");
            check(Enumerable.Range(0, 100).Count(roll => roll < SmokeAiRules.Chance(pinned, suppressed)) == expected,
                "strict 0..99 threshold is exactly 60/45/30, not >= off by one");
        }
        check(SmokeAiRules.TrySelectThreatBearing(true, true, -4, 3, 1, -1, out int attackerX, out int attackerZ) &&
            attackerX == -4 && attackerZ == 3, "defensive smoke follows the confirmed attacker even when a nearer enemy is elsewhere");
        check(!SmokeAiRules.TrySelectThreatBearing(true, false, 0, 0, 1, -1, out _, out _),
            "defensive smoke is skipped when no current attacker bearing is confirmed");
        check(!SmokeAiRules.TrySelectThreatBearing(true, true, 0, 0, 1, -1, out _, out _),
            "defensive smoke rejects a zero-length attacker bearing");
        check(SmokeAiRules.TrySelectThreatBearing(false, false, 0, 0, 4, -2, out int nearestX, out int nearestZ) &&
            nearestX == 4 && nearestZ == -2, "exposed-unit smoke follows a known nearest threat without requiring an attacker");
        check(!SmokeAiRules.TrySelectThreatBearing(false, false, 0, 0, 0, 0, out _, out _),
            "smoke is skipped when no nonzero threat bearing is known");

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

        check(SmokeCoverRules.ExposureTiles(0, false, false) == 0f, "clear endpoints add no smoke exposure");
        check(SmokeCoverRules.ExposureTiles(0, true, false) == 0.5f, "one smoke endpoint counts as half a tile");
        check(SmokeCoverRules.ExposureTiles(0, true, true) == 1f, "two smoke endpoints count as one tile total");
        check(SmokeCoverRules.ExposureTiles(0, true, true, sameEndpointTile: true) == 0.5f,
            "one shared endpoint tile is not counted twice");
        check(SmokeCoverRules.ExposureTiles(1, false, true) == 1.5f,
            "one intervening tile plus one smoke endpoint counts as one and a half tiles");
        check(SmokeCoverRules.ExposureTiles(-1, false, false) == 0f, "negative intervening count is clamped to zero");
        check(SmokeCoverRules.AccuracyPenalty(0.5f, 20, 60) == 10f, "half endpoint exposure applies half the configured penalty");
        check(SmokeCoverRules.AccuracyPenalty(2.5f, 20, 60) == 50f, "fractional exposure preserves fractional tile weight");
        check(SmokeCoverRules.AccuracyPenalty(4f, 20, 60) == 60f, "smoke accuracy penalty still respects its cap");
        check(SmokeCoverRules.AccuracyPenalty(1f, 0, 60) == 0f && SmokeCoverRules.AccuracyPenalty(1f, 20, 0) == 0f,
            "zero per-tile or maximum setting disables the smoke penalty");
    }
}
namespace MenaceWeaponRangesRebalanced;

internal static class ThrownGrenadeRangeRules
{
    internal readonly record struct Decision(int DesiredRange, bool ShouldOwn);

    internal static bool IsThrownGrenadeSkill(string skillName)
    {
        return !string.IsNullOrWhiteSpace(skillName)
            && skillName.StartsWith("active.throw_", StringComparison.OrdinalIgnoreCase)
            // These native hand-thrown grenades do not contain "grenade" in their skill IDs.
            && (skillName.IndexOf("grenade", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(skillName, "active.throw_scrap_bomb_frag_tier1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(skillName, "active.throw_flashbang", StringComparison.OrdinalIgnoreCase));
    }

    internal static Decision Evaluate(
        int originalRange,
        int currentRange,
        bool enabled,
        bool wasTouched,
        int maximumRange)
    {
        int sourceRange = wasTouched ? originalRange : currentRange;
        bool shouldOwn = enabled && sourceRange > maximumRange;
        int desiredRange = shouldOwn
            ? maximumRange
            : (wasTouched ? originalRange : currentRange);
        return new Decision(desiredRange, shouldOwn);
    }
}
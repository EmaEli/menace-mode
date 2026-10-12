namespace SquaddieProbe;

/// <summary>
/// Stable, read-only compatibility surface for optional integrations with other mods.
/// A negative XP value or an empty rank means the Squaddie Veterancy runtime is not available.
/// </summary>
public static class CompatibilityApi
{
    public const int ApiVersion = 1;

    public static int GetSquaddieXp(int squaddieId)
        => ProbeRuntime.GetSquaddieXpForCompatibility(squaddieId);

    public static string GetSquaddieRank(int squaddieId)
    {
        int xp = GetSquaddieXp(squaddieId);
        return xp < 0
            ? string.Empty
            : VeterancyRules.DisplayName(VeterancyRules.RankFromXp(xp));
    }
}

namespace MenaceWeaponMechanicsRebalanced;

// Pure rules shared by firing and the UI; no Unity, templates or random rolls.
internal static class MortarScatterRules
{
    internal const int ReductionPerShot = 1;
    internal const int UnsolvedFloor = 2;
    internal const int SolvedScatter = 1;

    internal static int EffectiveScatter(int baseScatter, int shots, bool solutionFound)
    {
        baseScatter = Math.Max(0, baseScatter);
        if (baseScatter <= 1) return baseScatter;
        if (solutionFound) return Math.Min(baseScatter, SolvedScatter);
        long tightened = baseScatter - (long)Math.Max(0, shots) * ReductionPerShot;
        return (int)Math.Max(Math.Min(baseScatter, UnsolvedFloor), tightened);
    }

    // Skill.GetAvailableScatterTiles uses an inclusive square, not a circle.
    // Bits follow the projected tile corners: south, east, north, west.
    internal static int BorderEdges(int dx, int dz, int scatter)
    {
        int radius = Math.Max(0, scatter);
        if (Math.Abs((long)dx) > radius || Math.Abs((long)dz) > radius) return 0;
        return (dz == -radius ? 1 : 0) | (dx == radius ? 2 : 0)
            | (dz == radius ? 4 : 0) | (dx == -radius ? 8 : 0);
    }
}
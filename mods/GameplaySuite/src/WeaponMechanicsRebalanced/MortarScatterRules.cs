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

    internal static float ColorProgress(int baseScatter, int effectiveScatter, bool solutionFound)
    {
        if (solutionFound) return 1f;
        baseScatter = Math.Max(0, baseScatter);
        if (baseScatter <= SolvedScatter) return 0f;
        float progress = (baseScatter - (float)effectiveScatter) / (baseScatter - SolvedScatter);
        return Math.Clamp(progress, 0f, 1f);
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

internal sealed class MortarTurnScatterState
{
    private int _shots;
    private bool _solutionFound;

    internal int Shots => _shots;
    internal bool SolutionFound => _solutionFound;

    internal int EffectiveScatter(int baseScatter)
        => MortarScatterRules.EffectiveScatter(baseScatter, _shots, _solutionFound);

    internal void RecordShot(bool directHit)
    {
        if (_shots < 1000000) _shots++;
        if (directHit) _solutionFound = true;
    }

    internal void Reset()
    {
        _shots = 0;
        _solutionFound = false;
    }
}
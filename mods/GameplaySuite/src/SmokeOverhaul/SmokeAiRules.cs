namespace SmokeOverhaul;

// Pure geometry/probability from PR #12 (0426b448): no game calls or state.
internal static class SmokeAiRules
{
    internal const int PinnedChance = 60;
    internal const int SuppressedChance = 45;
    internal const int FlamethrowerChance = 30;

    internal static int Chance(bool pinned, bool suppressed)
        => pinned ? PinnedChance : suppressed ? SuppressedChance : FlamethrowerChance;

    internal static bool TryGetLocalOffset(int dx, int dz, bool advance, int reachIndex,
        int candidate, out int x, out int z)
    {
        x = z = 0;
        if (reachIndex < 0 || reachIndex >= 2 || candidate < 0 || candidate >= 3 || (dx == 0 && dz == 0))
            return false;
        int sx = System.Math.Sign(dx), sz = System.Math.Sign(dz);
        int reach = advance ? 2 - reachIndex : 1 + reachIndex;
        int stepX, stepZ;
        if (candidate == 0) { stepX = sx; stepZ = sz; }
        else if (sx != 0 && sz != 0)
        {
            stepX = candidate == 1 ? sx : 0;
            stepZ = candidate == 1 ? 0 : sz;
        }
        else if (sx != 0) { stepX = sx; stepZ = candidate == 1 ? 1 : -1; }
        else { stepX = candidate == 1 ? 1 : -1; stepZ = sz; }
        x = stepX * reach;
        z = stepZ * reach;
        int remainingX = dx - x, remainingZ = dz - z;
        return remainingX * remainingX + remainingZ * remainingZ < dx * dx + dz * dz;
    }
}
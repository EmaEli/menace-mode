namespace MenaceInfantryAssault;

// Pure policy shared with the offline regression runner. MENACE suppression states:
// None=0, Suppressed=1, PinnedDown=2; pinned stance=2 also vetoes reaction fire.
internal static class AssaultDefenseRules
{
    internal static bool CanReact(bool alive, bool dying, int suppression, int stance, bool stunned) =>
        alive && !dying && suppression != 2 && stance != 2 && !stunned;

    internal static float CqbBonusScale(int suppression) => suppression == 1 ? 0.5f : 1f;
}
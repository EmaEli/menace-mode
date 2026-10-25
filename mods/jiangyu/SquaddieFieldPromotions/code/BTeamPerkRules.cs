namespace SquaddieFieldPromotions;

internal static class BTeamPerkRules
{
    internal const int AllyRoutingEvent = 2;
    internal const int AllyDestroyedEvent = 4;
    internal const int ElementLostEvent = 8;

    internal static bool AppliesTo(string initialPerkId) =>
        initialPerkId == PromotionProgression.InitialPerkId ||
        initialPerkId == PromotionProgression.VehicleInitialPerkId;

    internal static float ScaleMoraleLoss(int moraleEvent, float moraleChange)
    {
        const int casualtyEvents = AllyDestroyedEvent | ElementLostEvent;
        if (!float.IsFinite(moraleChange) || moraleChange >= 0f || (moraleEvent & casualtyEvents) == 0)
            return moraleChange;
        return moraleChange * 2f;
    }
}
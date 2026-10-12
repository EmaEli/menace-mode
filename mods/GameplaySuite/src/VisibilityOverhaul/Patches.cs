using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace VisibilityOverhaul;

internal static class Patches
{
    // Skill.OnUpdate executes while MENACE rebuilds an entity's current properties.
    // Apply our modifiers once per EntityProperties instance so Vision becomes part of
    // the native property pipeline instead of being injected later from GetVision().
    private static readonly HashSet<IntPtr> ProcessedProperties = new();

    public static long VisionAdjustments { get; private set; }
    public static long ConcealmentAdjustments { get; private set; }
    public static int LastConcealmentModifier { get; private set; }

    public static void ResetRuntimeState()
    {
        ProcessedProperties.Clear();
        VisionAdjustments = 0;
        ConcealmentAdjustments = 0;
        LastConcealmentModifier = 0;
    }

    public static void SkillContainer_Update_Prefix()
    {
        ProcessedProperties.Clear();
    }

    public static void Skill_OnUpdate_Postfix(Skill __instance, EntityProperties _properties)
    {
        if (!RuntimeSettings.Enabled || _properties == null) return;

        int visionBonus = RuntimeSettings.VisionBonus;
        bool adjustVision = visionBonus != 0;
        bool adjustConcealment = RuntimeSettings.MovementConcealmentEnabled;
        if (!adjustVision && !adjustConcealment) return;

        IntPtr propertiesKey = _properties.Pointer;
        if (propertiesKey == IntPtr.Zero || !ProcessedProperties.Add(propertiesKey)) return;

        try
        {
            // Native-style vision handling: alter the raw property during MENACE's normal
            // property rebuild. All later GetVision()/map visibility reads therefore see
            // the same bonus-aware value without patching GetVision or Map visibility.
            if (adjustVision)
            {
                long adjusted = (long)_properties.Vision + visionBonus;
                _properties.Vision = adjusted > int.MaxValue ? int.MaxValue : (int)adjusted;
                VisionAdjustments++;
            }

            if (!adjustConcealment) return;

            var entity = __instance.GetEntity();
            if (entity == null || !entity.IsInfantry()) return;

            var actor = entity.TryCast<Actor>();
            if (actor == null) return;

            int moved = Math.Max(0, actor.TilesMovedThisTurn);
            int modifier = RuntimeSettings.StationaryConcealmentBonus
                           - moved * RuntimeSettings.ConcealmentLossPerTile;

            int floor = -RuntimeSettings.MaxBelowVanillaPenalty;
            if (modifier < floor) modifier = floor;

            if (modifier != 0)
                _properties.Concealment += modifier;

            LastConcealmentModifier = modifier;
            ConcealmentAdjustments++;
        }
        catch
        {
            // Property recalculation is a hot path. Fail soft for a single entity rather
            // than destabilizing tactical resolution. Patch binding failures are logged at init.
        }
    }
}

using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace VisibilityOverhaul;

internal static class Patches
{
    // Skill.OnUpdate executes while MENACE rebuilds an entity's current properties.
    // Apply our modifiers once per EntityProperties instance so Vision becomes part of
    // the native property pipeline instead of being injected later from GetVision().
    private static readonly HashSet<IntPtr> ProcessedProperties = new();
    private static long _skillUpdateCall;

    public static long VisionAdjustments { get; private set; }
    public static long ConcealmentAdjustments { get; private set; }
    public static int LastConcealmentModifier { get; private set; }

    public static void ResetRuntimeState()
    {
        ProcessedProperties.Clear();
        _skillUpdateCall = 0;
        VisionAdjustments = 0;
        ConcealmentAdjustments = 0;
        LastConcealmentModifier = 0;
        PirateVisibilityDiagnostics.Reset();
    }

    public static void StartDiagnostics(string version, string gameVersion, string settings) =>
        PirateVisibilityDiagnostics.Start(version, gameVersion, settings);

    public static void LogDiagnosticSummary() =>
        PirateVisibilityDiagnostics.LogSummary(VisionAdjustments, ConcealmentAdjustments);

    public static void ResetDiagnostics() => PirateVisibilityDiagnostics.Reset();

    public static void SkillContainer_Update_Prefix()
    {
        ProcessedProperties.Clear();
        if (PirateVisibilityDiagnostics.Enabled)
            _skillUpdateCall++;
    }

    public static void Skill_OnUpdate_Postfix(Skill __instance, EntityProperties _properties)
    {
        bool diagnostics = PirateVisibilityDiagnostics.Enabled;
        if ((!RuntimeSettings.Enabled && !diagnostics) || _properties == null) return;

        int visionBonus = RuntimeSettings.Enabled ? RuntimeSettings.VisionBonus : 0;
        bool adjustVision = visionBonus != 0;
        bool adjustConcealment = RuntimeSettings.Enabled && RuntimeSettings.MovementConcealmentEnabled;
        if (!adjustVision && !adjustConcealment && !diagnostics) return;

        IntPtr propertiesKey = _properties.Pointer;
        if (propertiesKey == IntPtr.Zero || !ProcessedProperties.Add(propertiesKey)) return;

        Entity? diagnosticEntity = diagnostics ? PirateVisibilityDiagnostics.ResolveEntity(__instance) : null;
        Actor? diagnosticActor = diagnostics ? PirateVisibilityDiagnostics.ResolveActor(diagnosticEntity) : null;
        var before = diagnostics && diagnosticActor != null
            ? PirateVisibilityDiagnostics.CaptureBefore(_properties, diagnosticActor)
            : default;
        int concealmentModifier = 0;
        bool concealmentEvaluated = false;
        bool visionMutationApplied = false;
        bool concealmentMutationApplied = false;

        try
        {
            // Native-style vision handling: alter the raw property during MENACE's normal
            // property rebuild. All later GetVision()/map visibility reads therefore see
            // the same bonus-aware value without patching GetVision or Map visibility.
            if (adjustVision)
            {
                long adjusted = (long)_properties.Vision + visionBonus;
                _properties.Vision = adjusted > int.MaxValue ? int.MaxValue : (int)adjusted;
                visionMutationApplied = true;
                VisionAdjustments++;
            }

            if (!adjustConcealment) return;

            var entity = diagnosticEntity ?? __instance.GetEntity();
            if (entity == null || !entity.IsInfantry()) return;

            var actor = entity.TryCast<Actor>();
            if (actor == null) return;

            int moved = Math.Max(0, actor.TilesMovedThisTurn);
            int modifier = RuntimeSettings.StationaryConcealmentBonus
                           - moved * RuntimeSettings.ConcealmentLossPerTile;

            int floor = -RuntimeSettings.MaxBelowVanillaPenalty;
            if (modifier < floor) modifier = floor;

            if (modifier != 0)
            {
                _properties.Concealment += modifier;
                concealmentMutationApplied = true;
            }

            concealmentModifier = modifier;
            concealmentEvaluated = true;
            LastConcealmentModifier = modifier;
            ConcealmentAdjustments++;
        }
        catch
        {
            // Property recalculation is a hot path. Fail soft for a single entity rather
            // than destabilizing tactical resolution. Patch binding failures are logged at init.
        }
        finally
        {
            if (diagnostics)
                PirateVisibilityDiagnostics.CaptureAfter(
                    diagnosticActor,
                    _properties,
                    before,
                    concealmentModifier,
                    _properties.Pointer,
                    _skillUpdateCall,
                    visionMutationApplied,
                    concealmentEvaluated,
                    concealmentMutationApplied);
        }
    }

    public static void Actor_HasLineOfSightTo_Postfix(
        Actor __instance,
        Entity __0,
        Tile __1,
        Tile __2,
        bool __result) =>
        PirateVisibilityDiagnostics.ObserveVisibilityCheck(__instance, __0, __1, __2, __result);

    public static void Actor_IsHiddenToPlayerAtTile_Postfix(Actor __instance, Tile __0, bool __result) =>
        PirateVisibilityDiagnostics.ObserveFinalVisibility(__instance, __0, __result);
}

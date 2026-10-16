#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace GarrisonableBuildings;

// Safe collapse of occupied multi-tile garrisons.
//
// This mod changes the whitelisted multi-tile structures from ContainerType.None to
// ContainerType.Building and assigns cover_inside_building. MENACE's native collapse path
// was reproduced hard-crashing when one of these structures dies, including a 1x2 Civilian
// Shelter with no live occupant. During Structure.Die only, the two template fields changed
// by this mod are restored to their vanilla values. If the structure is occupied, native
// death is deferred until the occupant has fully finished its ejection/leave movement;
// destroying the structure in the same call stack as EjectEntity was reproduced as a native
// hard crash. The shared template is restored in the postfix, so other live structures
// remain garrisonable. (Moved here from Building Combat Rules 0.1.2-0.2.0, GitHub #50.)
public sealed partial class Plugin
{
    private const int CollapseResumeGraceFrames = 6;
    private const int CollapseEjectRetryFrames = 15;
    private const int CollapseStallReportFrames = 600;

    private static bool _safeCollapse = true;
    private static MethodInfo? _getEjectionTile;
    private static readonly Dictionary<IntPtr, PendingCollapse> PendingCollapses = new();
    private static readonly HashSet<IntPtr> ResumingCollapses = new();

    private sealed class PendingCollapse
    {
        public Structure Structure { get; init; } = null!;
        public Entity Occupant { get; init; } = null!;
        public bool Quiet { get; init; }
        public int Frames { get; set; }
        public int SettledFrames { get; set; }
        public int RetryCountdown { get; set; }
        public bool EjectionRequested { get; set; }
        public bool StallReported { get; set; }
    }

    private readonly struct CollapseTemplateState
    {
        public readonly EntityTemplate? Template;
        public readonly EntityContainerType OriginalContainerType;
        public readonly InsideCoverTemplate? OriginalInsideCover;
        public readonly bool Changed;

        public CollapseTemplateState(EntityTemplate template)
        {
            Template = template;
            OriginalContainerType = template.ContainerType;
            OriginalInsideCover = template.ProvidesCoverInside;
            Changed = true;
        }
    }

    private static void InstallSafeCollapseHooks(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo? die = typeof(Structure).GetMethod(
                nameof(Structure.Die),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);

            if (die == null)
            {
                DebugError($"{ModName}: Structure.Die(bool) not found; safe occupied collapse unavailable.");
                return;
            }

            _getEjectionTile = typeof(Entity).GetMethod(
                "GetEjectionTile",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            MethodInfo? transientGetCurrentProperties = typeof(TransientActor).GetMethod(
                nameof(TransientActor.GetCurrentProperties),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            MethodInfo? unitGetCurrentProperties = typeof(UnitActor).GetMethod(
                nameof(UnitActor.GetCurrentProperties),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            harmony.Patch(die,
                prefix: new HarmonyMethod(typeof(CollapseHooks), nameof(CollapseHooks.StructureDiePrefix)),
                postfix: new HarmonyMethod(typeof(CollapseHooks), nameof(CollapseHooks.StructureDiePostfix)));

            var actorPropertiesFinalizer = new HarmonyMethod(typeof(CollapseHooks), nameof(CollapseHooks.ActorGetCurrentPropertiesFinalizer));
            if (transientGetCurrentProperties != null)
                harmony.Patch(transientGetCurrentProperties, finalizer: actorPropertiesFinalizer);
            if (unitGetCurrentProperties != null)
                harmony.Patch(unitGetCurrentProperties, finalizer: actorPropertiesFinalizer);

            DebugMsg($"{ModName}: hooked Structure.Die for safe occupied collapse. Invalid-actor guard=" +
                     $"{transientGetCurrentProperties != null && unitGetCurrentProperties != null}, native ejection helper={_getEjectionTile != null}.");
        }
        catch (Exception e)
        {
            DebugError($"{ModName}: failed to install safe collapse hooks: {Short(e)}");
        }
    }

    private static void ClearPendingCollapses()
    {
        PendingCollapses.Clear();
        ResumingCollapses.Clear();
    }

    private static void ReloadSafeCollapseSetting()
    {
        _safeCollapse = ReadBool("SafeOccupiedCollapse", true);
    }

    private static bool IsManagedMultiTileGarrison(Structure structure)
    {
        try
        {
            EntityTemplate? template = structure.GetTemplate();
            if (template == null) return false;
            string id = template.GetID() ?? string.Empty;
            return IsMultiTileCandidate(id) && template.ContainerType == EntityContainerType.Building;
        }
        catch { return false; }
    }

    private static bool IsMultiTileCandidate(string id)
    {
        return CandidateById.TryGetValue(id, out CandidateSpec? spec) && spec.Width * spec.Height > 1;
    }

    private static CollapseTemplateState BeforeStructureDie(Structure structure, bool forceSafeCollapse = false)
    {
        if ((!_safeCollapse && !forceSafeCollapse) || structure == null || !SafeIsBuilding(structure)) return default;

        // Multi-tile civilian structures are vanilla non-containers. Only ContainerType and
        // ProvidesCoverInside are changed to make them enterable. Restore those fields for the
        // synchronous native death path, then put them back in the postfix.
        try
        {
            EntityTemplate? template = structure.GetTemplate();
            if (template == null) return default;

            string id = template.GetID() ?? string.Empty;
            if (!IsMultiTileCandidate(id)) return default;
            if (template.ContainerType != EntityContainerType.Building) return default;

            var state = new CollapseTemplateState(template);
            template.ContainerType = EntityContainerType.None;
            template.ProvidesCoverInside = null;
            DebugMsg($"{ModName}: normalized multi-tile garrison template for native collapse: building={id}, ContainerType=Building->None, insideCover=cleared.");
            return state;
        }
        catch (Exception e)
        {
            DebugError($"{ModName}: multi-tile collapse template normalization failed: {Short(e)}");
            return default;
        }
    }

    private static bool TryConsumeResumingCollapse(Structure structure)
    {
        if (structure == null) return false;
        try
        {
            IntPtr key = structure.Pointer;
            return key != IntPtr.Zero && ResumingCollapses.Remove(key);
        }
        catch { return false; }
    }

    private static bool ShouldDeferStructureDie(Structure structure, bool quiet)
    {
        if (!_safeCollapse || structure == null || !SafeIsBuilding(structure)) return false;

        IntPtr key;
        try { key = structure.Pointer; }
        catch { return false; }
        if (key == IntPtr.Zero) return false;

        // Repeated lethal hits while an occupant is already leaving must not restart the native
        // death path or request another ejection.
        if (PendingCollapses.ContainsKey(key)) return true;

        if (!IsManagedMultiTileGarrison(structure)) return false;

        Entity? occupant;
        try { occupant = structure.GetContainedEntity(); }
        catch { return false; }
        if (!Valid(occupant)) return false;

        var pending = new PendingCollapse
        {
            Structure = structure,
            Occupant = occupant!,
            Quiet = quiet,
            Frames = 0,
            SettledFrames = 0,
            RetryCountdown = 0,
            EjectionRequested = false,
            StallReported = false
        };
        PendingCollapses[key] = pending;

        // If MENACE has already started an exit (for example a morale/fleeing reaction), do not
        // issue a second EjectEntity call. Just let that movement finish before resuming collapse.
        if (!IsActorMoving(occupant!))
            pending.EjectionRequested = TryRequestCollapseEjection(structure, occupant!);

        DebugMsg($"{ModName}: deferred occupied multi-tile collapse until ejection movement finishes: building={EntityId(structure)}, occupant={EntityId(occupant!)}.");
        return true;
    }

    private static void ProcessPendingCollapses()
    {
        if (PendingCollapses.Count == 0) return;

        foreach (IntPtr key in PendingCollapses.Keys.ToArray())
        {
            if (!PendingCollapses.TryGetValue(key, out PendingCollapse? pending)) continue;
            pending.Frames++;

            if (!Valid(pending.Structure))
            {
                PendingCollapses.Remove(key);
                continue;
            }

            bool occupantValid = Valid(pending.Occupant);
            bool occupantMoving = occupantValid && IsActorMoving(pending.Occupant);
            bool stillContained = occupantValid && IsStillContainedBy(pending.Structure, pending.Occupant);

            if (stillContained)
            {
                pending.SettledFrames = 0;
                if (occupantMoving) continue;

                if (pending.RetryCountdown > 0)
                {
                    pending.RetryCountdown--;
                    continue;
                }

                pending.EjectionRequested = TryRequestCollapseEjection(pending.Structure, pending.Occupant);
                pending.RetryCountdown = CollapseEjectRetryFrames;

                if (!pending.EjectionRequested && pending.Frames >= CollapseStallReportFrames && !pending.StallReported)
                {
                    pending.StallReported = true;
                    DebugError($"{ModName}: occupied multi-tile collapse remains deferred because MENACE could not eject the occupant: building={EntityId(pending.Structure)}, occupant={EntityId(pending.Occupant)}.");
                }
                continue;
            }

            // EjectEntity clears containment before its visual/path movement is necessarily done.
            // Wait for Actor.IsMoving() to become false, then count a fresh grace window after
            // movement has actually settled so native movement-finished callbacks can unwind.
            if (occupantMoving)
            {
                pending.SettledFrames = 0;
                continue;
            }

            pending.SettledFrames++;
            if (pending.SettledFrames < CollapseResumeGraceFrames) continue;

            PendingCollapses.Remove(key);
            ResumingCollapses.Add(key);

            try
            {
                DebugMsg($"{ModName}: resuming deferred multi-tile collapse after occupant exit completed: building={EntityId(pending.Structure)}, occupant={EntityId(pending.Occupant)}.");
                pending.Structure.Die(pending.Quiet);
            }
            catch (Exception e)
            {
                ResumingCollapses.Remove(key);
                DebugError($"{ModName}: deferred occupied-building collapse failed: {Short(e)}");
            }
        }
    }

    private static bool IsStillContainedBy(Structure structure, Entity occupant)
    {
        try
        {
            Entity? current = occupant.GetContainerEntity();
            if (Valid(current) && current!.Pointer == structure.Pointer) return true;
        }
        catch { }

        try
        {
            Entity? current = structure.GetContainedEntity();
            return Valid(current) && current!.Pointer == occupant.Pointer;
        }
        catch { return false; }
    }

    private static bool IsActorMoving(Entity entity)
    {
        try
        {
            Actor? actor = entity.ToActor();
            return Valid(actor) && actor!.IsMoving();
        }
        catch { return false; }
    }

    private static void AfterStructureDie(CollapseTemplateState state)
    {
        if (!state.Changed || state.Template == null) return;

        try
        {
            state.Template.ContainerType = state.OriginalContainerType;
            state.Template.ProvidesCoverInside = state.OriginalInsideCover;
            DebugMsg($"{ModName}: restored garrison template after native collapse: building={state.Template.GetID() ?? "-"}.");
        }
        catch (Exception e)
        {
            DebugError($"{ModName}: failed to restore multi-tile garrison template after collapse: {Short(e)}");
        }
    }

    private static bool TryRequestCollapseEjection(Structure structure, Entity occupant)
    {
        if (!Valid(structure) || !Valid(occupant)) return false;

        try
        {
            Tile? ejectTile = null;
            bool nativeHelperAvailable = _getEjectionTile != null;
            if (nativeHelperAvailable)
            {
                try { ejectTile = _getEjectionTile!.Invoke(structure, null) as Tile; }
                catch (Exception e) { DebugMsg($"{ModName}: native ejection-tile lookup failed: {Short(e)}"); }
            }

            // Fall back to the structure anchor only when the private native helper itself is
            // unavailable on this game build. If the helper exists but returns no tile, respect
            // that result and retry on a later frame rather than forcing an unsafe ejection.
            if (!nativeHelperAvailable && !Valid(ejectTile))
            {
                try { ejectTile = structure.GetTile(); }
                catch { ejectTile = null; }
            }

            bool ejected = Valid(ejectTile) && structure.EjectEntity(ejectTile!);
            if (ejected)
            {
                DebugMsg($"{ModName}: requested pre-collapse occupant ejection: building={EntityId(structure)}, occupant={EntityId(occupant)}.");
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            DebugMsg($"{ModName}: occupied-building ejection request failed: {Short(e)}");
            return false;
        }
    }

    private static Exception? HandleActorPropertiesException(Exception? exception, ref EntityProperties? result)
    {
        if (exception == null) return null;

        // This mod probes actor properties through reflection while evaluating movement/container
        // state. MENACE can invalidate a transient/unit actor between the probe and
        // MethodInfo.Invoke, which turns the native failure into a noisy TargetInvocationException.
        // Only suppress failures whose live managed call stack clearly originates from this mod;
        // every other caller keeps vanilla exception behavior. Returning null is the same safe
        // "actor unavailable" result that the discovery paths already handle.
        string stack;
        try { stack = Environment.StackTrace ?? string.Empty; }
        catch { return exception; }

        if (stack.IndexOf("GarrisonableBuildings", StringComparison.OrdinalIgnoreCase) < 0) return exception;

        result = null;
        DebugMsg($"{ModName}: suppressed invalid Actor.GetCurrentProperties failure: {Short(exception)}");
        return null;
    }

    private static string EntityId(Entity entity)
    {
        try { return entity.GetTemplate()?.GetID() ?? entity.ToString() ?? "-"; }
        catch
        {
            try { return entity.ToString() ?? "-"; }
            catch { return "-"; }
        }
    }

    private static bool SafeIsBuilding(Structure structure)
    {
        try { return structure.IsBuilding(); }
        catch { return false; }
    }

    private static bool Valid(Il2CppObjectBase? obj)
    {
        if (obj == null) return false;
        try { return obj.Pointer != IntPtr.Zero; }
        catch { return false; }
    }

    private static string Short(Exception e)
    {
        Exception x = e;
        while (x is TargetInvocationException tie && tie.InnerException != null) x = tie.InnerException;
        return $"{x.GetType().Name}: {x.Message}";
    }

    private static class CollapseHooks
    {
        public static bool StructureDiePrefix(Structure __instance, bool __0, out CollapseTemplateState __state)
        {
            __state = default;
            try
            {
                // A deferred collapse is allowed through exactly once. Force the existing
                // multi-tile template normalization even if the user changed the setting while
                // the occupant was leaving; otherwise the resumed native death could regress.
                bool resuming = TryConsumeResumingCollapse(__instance);
                if (!resuming && ShouldDeferStructureDie(__instance, __0)) return false;
                __state = BeforeStructureDie(__instance, forceSafeCollapse: resuming);
            }
            catch (Exception e)
            {
                DebugError($"{ModName}: structure death hook failed: {Short(e)}");
            }
            return true;
        }

        public static void StructureDiePostfix(CollapseTemplateState __state)
        {
            try { AfterStructureDie(__state); }
            catch (Exception e) { DebugError($"{ModName}: structure death restore hook failed: {Short(e)}"); }
        }

        public static Exception? ActorGetCurrentPropertiesFinalizer(Exception? __exception, ref EntityProperties? __result)
        {
            try { return HandleActorPropertiesException(__exception, ref __result); }
            catch { return __exception; }
        }
    }
}

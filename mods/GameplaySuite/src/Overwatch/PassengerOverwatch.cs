using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppTactical;

namespace MenaceOverwatch;

// A deliberately narrow extension of MENACE's AllowInsideStructures rule. Never mutate
// shared skill templates or borrow vehicle AP to make an embarked attack usable.
internal static class PassengerOverwatch
{
    internal static bool TryGetTransport(Actor passenger, out Actor? transport)
    {
        transport = null;
        try
        {
            if (!Native.IsInfantryActor(passenger)) return false;
            var container = passenger.GetContainerEntity();
            var vehicle = container?.TryCast<Actor>();
            if (!Native.IsVehicleActor(vehicle!) || !Native.IsCombatReady(vehicle!)) return false;
            if (!Native.Same(vehicle!.GetContainedEntity(), passenger)
                || !passenger.IsAlliedWith(vehicle)) return false;
            var template = vehicle.GetTemplate();
            if (!Native.Valid(template) || template!.ContainerType != EntityContainerType.Vehicle) return false;
            var cover = template.ProvidesCoverInside;
            if (!Native.Valid(cover) || !string.Equals(cover!.name, "cover_inside_vehicle_open",
                    StringComparison.OrdinalIgnoreCase)) return false;
            transport = vehicle;
            return true;
        }
        catch { return false; }
    }

    internal static Actor? GetPassenger(Actor vehicle)
    {
        try
        {
            var passenger = vehicle.GetContainedEntity()?.TryCast<Actor>();
            return Native.Valid(passenger) && TryGetTransport(passenger!, out var container)
                && Native.Same(container, vehicle) ? passenger : null;
        }
        catch { return null; }
    }

    internal static long ContainerKey(Actor actor) => Native.Key(actor.GetContainerEntity());

    internal static bool SamePositionContext(Actor actor, long containerKey)
    {
        try
        {
            return Native.Valid(actor) && ContainerKey(actor) == containerKey
                && (!IsEmbarked(actor) || TryGetTransport(actor, out _));
        }
        catch { return false; }
    }

    internal static bool CanUseAttack(Actor actor, Skill skill, out string reason)
    {
        reason = "Ready.";
        try
        {
            var container = actor.GetContainerEntity();
            // Buildings/forests and uncontained actors retain their existing rules.
            if (!Native.Valid(container) || !container!.IsActor()) return true;
            if (!TryGetTransport(actor, out var transport))
            { reason = "Passenger Overwatch requires a live allied open-top transport."; return false; }
            if (actor.IsMoving() || transport!.IsMoving())
            { reason = "Passenger Overwatch is unavailable during movement."; return false; }
            if (!Native.Same(skill.GetActor(), actor) || !Native.Same(skill.GetAPConsumer(), actor))
            { reason = "Passenger attacks must belong to and use AP from the passenger."; return false; }
            var template = skill.GetTemplate();
            if (!Native.Valid(template) || (template!.UsabilityWhileContained != UsabilityWhileContained.AllowInsideStructures
                    && template.UsabilityWhileContained != UsabilityWhileContained.Always))
            { reason = "This attack cannot be used while embarked."; return false; }
            if (actor.GetSuppressionState() == SuppressionState.PinnedDown
                && !template.IsUsableWhilePinnedDown)
            { reason = "Passenger is pinned down."; return false; }
            if ((template.ExecutingElement == ExecutingElementType.AllButSpecial
                    || template.ExecutingElement == ExecutingElementType.OneAtRandomExceptSpecial)
                && actor.GetElements().Count <= 1
                && Native.Valid(actor.GetItems().GetItemAtSlot(ItemSlot.InfantrySpecial)))
            { reason = "No non-special passenger elements can fire this attack."; return false; }

            // IsUsable always rejects AllowInsideStructures inside an Actor, even with
            // IGNORE_DEPLOYMENT_AND_ANGLE. Keep its other gates explicitly: existing
            // Native callers check enabled/executing elements, setup and ammo; execute
            // every native handler's usability veto here rather than overriding it.
            var handlers = skill.GetSkillEventHandlers();
            if (handlers == null)
            { reason = "Passenger attack usability handlers are unavailable."; return false; }
            for (int i = 0; i < handlers.Length; i++)
            {
                if (!Native.Valid(handlers[i]) || !handlers[i].IsUsable())
                { reason = "Passenger attack is not currently usable."; return false; }
            }
            return true;
        }
        catch
        {
            reason = "Passenger attack eligibility could not be verified.";
            return false;
        }
    }

    internal static bool IsEmbarked(Actor actor)
    {
        try
        {
            var container = actor.GetContainerEntity();
            return Native.Valid(container) && container!.IsActor();
        }
        catch { return true; }
    }

    internal static bool HasLineOfSight(Actor passenger, Entity target, Tile source, Tile destination, out string detail)
    {
        detail = "passenger detection could not be verified";
        try
        {
            if (!TryGetTransport(passenger, out _) || !Native.Valid(target)
                || !Native.Valid(source) || !Native.Valid(destination)) return false;
            if (!source.HasLineOfSightTo(destination, LineOfSightFlags.Default))
            { detail = "open-top passenger: no tile line of sight"; return false; }
            var defender = target.TryCast<Actor>();
            var observerProperties = Native.CurrentProperties(passenger, out _);
            var targetProperties = Native.Valid(defender) ? Native.CurrentProperties(defender!, out _) : null;
            if (!Native.Valid(observerProperties) || !Native.Valid(targetProperties)) return false;

            // MENACE 0.7.17 Actor.HasLineOfSightTo (RVA 0x637400), excluding only
            // its blanket actor-container veto. Retain the passenger's own senses,
            // target cover/concealment, smoke, revealed state and adjacent-tile rules.
            int distance = source.GetDistanceTo(destination);
            int vision = observerProperties!.GetVision();
            if (defender!.Revealed)
            {
                detail = $"open-top passenger: revealed target distance={distance} vision={vision}";
                return distance <= vision;
            }

            int coverConcealment = 0;
            int tileConcealment;
            if (Native.Valid(target.GetContainerEntity()) && Native.Same(target.GetTile(), destination))
            {
                var cover = destination.GetEntity()?.GetTemplate()?.ProvidesCoverInside;
                if (distance > 1 && Native.Valid(cover)) coverConcealment = cover!.Concealment;
                tileConcealment = 0;
            }
            else
            {
                if (distance > 1)
                {
                    int coverIndex = (int)destination.GetCover(destination.GetDirectionTo(source), target, null, true);
                    var values = Config.COVER_CONCEALMENT;
                    if (values == null || coverIndex < 0 || coverIndex >= values.Length) return false;
                    coverConcealment = values[coverIndex];
                }
                tileConcealment = destination.GetConcealment();
            }

            int concealment = Math.Max(0, coverConcealment + targetProperties!.GetConcealment()
                + tileConcealment - observerProperties.GetDetection());
            int detectionRange = Math.Max(1, vision - concealment);
            detail = $"open-top passenger: distance={distance} vision={vision} concealment={concealment} detectionRange={detectionRange}";
            return distance <= detectionRange;
        }
        catch (Exception e)
        {
            detail = "open-top passenger detection failed: " + e.GetType().Name;
            return false;
        }
    }
}
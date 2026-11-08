using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace SquaddieProbe;

internal static class VehicleRetreatSafety
{
    internal static bool IsLivingActor(Actor? actor)
    {
        try { return actor != null && actor.Pointer != IntPtr.Zero && actor.IsAlive() && !actor.IsDying(); }
        catch { return false; }
    }

    // StartPathInternal schedules this call through Element.Delay. A nonzero IL2CPP
    // wrapper pointer does NOT mean the Unity component survived the delay (#97).
    internal static bool CanStartMovement(PathMover? mover)
    {
        try
        {
            if (!mover) return false;
            var element = mover!.m_Element;
            return element && element.IsAlive() && IsLivingActor(mover.m_Actor);
        }
        catch { return false; }
    }

    internal static bool IsReadyToRetreat(Actor actor)
    {
        if (!IsLivingActor(actor)) return false;
        try
        {
            var skills = actor.GetSkills();
            return !actor.IsTurnDone() && !actor.IsLeavingMap() && !actor.IsMoving()
                && !actor.IsStunned() && skills != null && skills.Pointer != IntPtr.Zero
                && !skills.IsBusy() && !TacticalManager.IsSkillBusy();
        }
        catch { return false; }
    }

    internal static bool TryExtinguishFire(Actor actor)
    {
        if (!IsReadyToRetreat(actor)) return false;
        try
        {
            var skills = actor.GetSkills();
            bool burning = false;
            foreach (string id in new[] { "damage_effect.small_fire", "damage_effect.medium_fire", "damage_effect.big_fire" })
            {
                var effect = skills.GetSkillByID(id, null);
                if (effect != null && effect.Pointer != IntPtr.Zero) { burning = true; break; }
            }
            if (!burning) return false;
            var skill = skills.GetSkillByID("special.vehicle_extinguish_fire", null)?.TryCast<Skill>();
            if (skill == null || skill.Pointer == IntPtr.Zero || skill.IsOutOfUses()
                || !skill.IsUsable() || !skill.IsAffordable()) return false;
            var tile = actor.GetTile();
            return tile != null && tile.Pointer != IntPtr.Zero && skill.IsUsableOn(tile, tile)
                && skill.Use(tile, UsageParameter.AutoAudioVolume);
        }
        catch { return false; }
    }
}
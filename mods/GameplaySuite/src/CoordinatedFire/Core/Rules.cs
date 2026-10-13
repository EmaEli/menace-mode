using System;

namespace CoordinatedFire.Core;

public enum WeaponRole { Unknown, Squad, Leader }

public sealed record AttackSnapshot(
    long SkillKey, long WeaponKey, long ActorKey, string Id, WeaponRole Role,
    int ActionPointCost, int MinRange, int MaxRange,
    bool IsUsable, bool IsAffordable, bool LimitedAmmo, int AmmoRemaining,
    bool IsDirectFire, bool IsWeaponAttack,
    bool RequiresDeployment, bool DeploymentSatisfied,
    bool RequiresSetup, bool SetupSatisfied);

public sealed record ActorSnapshot(long Key, bool IsPlayerControlled,
    bool IsInfantry, bool IsVehicle, bool IsAlive, bool IsStunned, bool IsRouting,
    bool IsMoving, bool IsBusy, int ActionPoints);

public sealed record VolleyPlan(AttackSnapshot Squad, AttackSnapshot Leader,
    AttackSnapshot First, AttackSnapshot Second, int Cost, int MinRange, int MaxRange);

public sealed record Decision(bool Allowed, string Reason, VolleyPlan? Plan)
{
    public static Decision Deny(string reason) => new(false, reason, null);
}

public static class Rules
{
    public static Decision Evaluate(ActorSnapshot actor, AttackSnapshot squad, AttackSnapshot leader,
        bool allowAI = false, bool allowVehicles = false)
    {
        if (actor.Key == 0 || !actor.IsAlive) return Decision.Deny("Unit unavailable.");
        bool vehicleVolley = actor.IsVehicle && allowVehicles;
        if (!actor.IsInfantry && !vehicleVolley) return Decision.Deny("Infantry only (vehicle support is optional).");
        if (!actor.IsPlayerControlled && !allowAI) return Decision.Deny(actor.IsVehicle ? "Player vehicles only." : "Player infantry only.");
        if (actor.IsStunned || actor.IsRouting) return Decision.Deny("Unit cannot fire.");
        if (actor.IsBusy || actor.IsMoving) return Decision.Deny("Wait for the current action.");
        if (squad.ActorKey != actor.Key || leader.ActorKey != actor.Key)
            return Decision.Deny("Both attacks must belong to this unit.");
        if (!vehicleVolley && (squad.Role != WeaponRole.Squad || leader.Role != WeaponRole.Leader))
            return Decision.Deny("A squad weapon and a leader weapon are required.");
        if (squad.SkillKey == 0 || leader.SkillKey == 0 || squad.SkillKey == leader.SkillKey)
            return Decision.Deny("Two distinct attacks are required.");
        if (squad.WeaponKey == 0 || leader.WeaponKey == 0 || squad.WeaponKey == leader.WeaponKey)
            return Decision.Deny("Two distinct equipped weapons are required.");
        if (!squad.IsWeaponAttack || !leader.IsWeaponAttack)
            return Decision.Deny("Only weapon attacks are supported.");
        if (!squad.IsDirectFire || !leader.IsDirectFire)
            return Decision.Deny("Unsupported targeting mode.");

        // These explicit template checks are deliberately performed before generic
        // IsUsable. Live testing showed that a fire mode could look usable in the
        // overlay while the native weapon still required a deployed stance/setup.
        string firstLabel = vehicleVolley ? "First vehicle weapon" : "Squad weapon";
        string secondLabel = vehicleVolley ? "Second vehicle weapon" : "Leader weapon";
        if (squad.RequiresDeployment && !squad.DeploymentSatisfied)
            return Decision.Deny(firstLabel + " requires DEPLOY stance first.");
        if (squad.RequiresSetup && !squad.SetupSatisfied)
            return Decision.Deny(firstLabel + " must be SET UP first.");
        if (leader.RequiresDeployment && !leader.DeploymentSatisfied)
            return Decision.Deny(secondLabel + " requires DEPLOY stance first.");
        if (leader.RequiresSetup && !leader.SetupSatisfied)
            return Decision.Deny(secondLabel + " must be SET UP first.");

        if (!squad.IsUsable || !leader.IsUsable)
            return Decision.Deny("Both original attacks must be usable.");
        if (!squad.IsAffordable || !leader.IsAffordable)
            return Decision.Deny("An original attack cannot pay its native costs.");
        if ((squad.LimitedAmmo && squad.AmmoRemaining <= 0) ||
            (leader.LimitedAmmo && leader.AmmoRemaining <= 0))
            return Decision.Deny("Both weapons need ammunition.");
        if (squad.ActionPointCost < 0 || leader.ActionPointCost < 0)
            return Decision.Deny("Unsupported AP cost.");
        if (squad.MinRange < 0 || leader.MinRange < 0 ||
            squad.MaxRange < squad.MinRange || leader.MaxRange < leader.MinRange)
            return Decision.Deny("Invalid weapon range.");
        int cost = Math.Max(squad.ActionPointCost, leader.ActionPointCost);
        if (actor.ActionPoints < cost) return Decision.Deny("Not enough AP.");
        int min = Math.Max(squad.MinRange, leader.MinRange);
        int max = Math.Min(squad.MaxRange, leader.MaxRange);
        if (min > max) return Decision.Deny("The weapon ranges do not overlap.");
        // Expensive-first remains useful if the follow-up becomes invalid after the
        // first shot: the committed fee is at least the cost of the attack that fired.
        var first = squad.ActionPointCost >= leader.ActionPointCost ? squad : leader;
        var second = first.SkillKey == squad.SkillKey ? leader : squad;
        return new(true, "Ready", new(squad, leader, first, second, cost, min, max));
    }
}

public static class Fee
{
    // Final debit is applied against CURRENT AP so unrelated AP gains/losses survive.
    public static int After(int current, int cost)
    {
        if (current < 0 || cost < 0) throw new ArgumentOutOfRangeException();
        return Math.Max(0, current - cost);
    }

    // MENACE's clamping SetActionPoints loses the part of an AP grant above max.
    // The deferred salvo fee would otherwise cause that lost amount to disappear.
    public static int ClippedGain(int current, int requested, int maxActionPoints)
    {
        if (current < 0 || maxActionPoints < 0) throw new ArgumentOutOfRangeException();
        if (requested <= current || current > maxActionPoints) return 0;
        return Math.Max(0, requested - maxActionPoints);
    }

    // A prepaid fee could have absorbed at most 'cost' AP from clamped grants.
    // Bound accumulation to that fee so multiple kills cannot mint extra AP.
    public static int AccumulateClippedGain(int carried, int clipped, int cost)
    {
        if (carried < 0 || clipped < 0 || cost < 0) throw new ArgumentOutOfRangeException();
        int remaining = Math.Max(0, cost - carried);
        return carried + Math.Min(clipped, remaining);
    }

    public static int AfterPreservingClippedGains(int current, int cost, int carried)
    {
        if (carried < 0) throw new ArgumentOutOfRangeException();
        return After(current, cost) + Math.Min(cost, carried);
    }

    // During a coordinated child attack, suppress only the exact vanilla AP debit.
    // Other AP writes (bonuses, penalties, scripts) are left untouched.
    public static int SuppressNativeDebit(int current, int requested, int expectedCost)
    {
        if (current < 0 || expectedCost < 0) throw new ArgumentOutOfRangeException();
        return expectedCost > 0 && requested == current - expectedCost ? current : requested;
    }
}

public static class AIPolicy
{
    // Replacing an AI attack with an asynchronous two-shot transaction is safe only
    // while MENACE still has AP left for the acting AI unit afterwards. Live logs
    // showed that consuming the AI actor's final AP inside the replacement bypasses
    // the native Agent hand-off and can leave the tactical state with no selectable UI.
    // Let the original AI attack execute unchanged when the combined fee would exhaust AP.
    public static bool CanReplaceWithoutOwningTurnEnd(int currentActionPoints, int combinedCost)
    {
        if (currentActionPoints < 0 || combinedCost < 0) return false;
        return currentActionPoints > combinedCost;
    }
}

public static class TurnFlow
{
    // A combined attack must not end the actor's turn merely because the two child
    // attacks completed. Let MENACE end the actor only when its own AP-spent rule
    // says the actor is finished (or another native condition already did so).
    public static bool ShouldRestoreTurn(bool actionPointsSpent, bool turnDone)
        => turnDone && !actionPointsSpent;

    public static bool ShouldEndActorTurn(bool actionPointsSpent, bool turnDone)
        => actionPointsSpent || turnDone;
}

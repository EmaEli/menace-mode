using System;
using System.Collections.Generic;

namespace MenaceOverwatch;

internal static class AIOverwatchWeaponPlanner
{
    internal static (T Primary, T? Secondary, int Cost)? Select<T>(
        IReadOnlyList<T> orderedAttacks,
        Func<T, bool> isSquadWeapon,
        Func<T, bool> isLeaderWeapon,
        Func<T, int> actionPointCost,
        bool allowPair) where T : class
    {
        if (orderedAttacks.Count == 0) return null;

        T? squad = null;
        T? leader = null;
        foreach (T attack in orderedAttacks)
        {
            if (squad == null && isSquadWeapon(attack)) squad = attack;
            if (leader == null && isLeaderWeapon(attack)) leader = attack;
        }

        if (allowPair && squad != null && leader != null)
        {
            T primary = orderedAttacks[0];
            T secondary;
            if (isSquadWeapon(primary)) secondary = leader;
            else if (isLeaderWeapon(primary)) secondary = squad;
            else { primary = squad; secondary = leader; }

            int cost = Math.Max(actionPointCost(squad), actionPointCost(leader));
            if (cost > 0) return (primary, secondary, cost);
        }

        T fallback = orderedAttacks[0];
        int fallbackCost = actionPointCost(fallback);
        return fallbackCost > 0 ? (fallback, null, fallbackCost) : null;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;

namespace SquaddieFieldPromotions;

internal static class MentorPerks
{
    internal static MentorSnapshot Read(StrategyState state, int soldierId, SquaddieServiceRecord service)
    {
        var soldier = state.Squaddies.GetById(soldierId);
        if (soldier == null) return null;
        var leader = soldier.GetCurrentLeader();
        if (leader == null && service?.Units != null)
        {
            // Prefer verified service history over an unreliable display name.
            foreach (var unit in service.Units.OrderByDescending(u => u.MissionsServed)
                         .ThenBy(u => u.LeaderTemplateId, StringComparer.Ordinal))
            {
                leader = FindHired(state, unit.LeaderTemplateId);
                if (leader != null) break;
            }
        }
        if (leader == null)
        {
            var last = soldier.GetLastLeaderTemplate();
            if (last != null) leader = FindHired(state, last.GetID());
        }
        if (leader?.LeaderTemplate == null) return null;
        var skills = new List<string>();
        foreach (object value in PrivateMember.Enumerate(leader.GetPerks()))
        {
            if (value is PerkTemplate perk && !string.IsNullOrWhiteSpace(perk.GetID()))
                skills.Add(perk.GetID());
        }
        // The person's real learned perks, never the entire mentor's tree.
        return new MentorSnapshot(leader.LeaderTemplate.GetID(), skills);
    }

    private static BaseUnitLeader FindHired(StrategyState state, string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId)) return null;
        foreach (object candidate in PrivateMember.Enumerate(state.Roster.GetHiredLeaders()))
            if (candidate is BaseUnitLeader leader && leader.LeaderTemplate?.GetID() == templateId)
                return leader;
        return null;
    }
}

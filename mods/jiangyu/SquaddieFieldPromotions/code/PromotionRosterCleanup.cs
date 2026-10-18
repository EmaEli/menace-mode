using System;
using System.Collections.Generic;

namespace SquaddieFieldPromotions;

internal enum PromotionRosterLocation
{
    Hirable,
    Hired,
    Dismissed,
    Unburied,
    Buried
}

internal sealed class PromotionRosterEntry
{
    internal string LeaderId { get; }
    internal PromotionRosterLocation Location { get; }

    internal PromotionRosterEntry(string leaderId, PromotionRosterLocation location)
    {
        LeaderId = leaderId;
        Location = location;
    }
}

internal sealed class PromotionRosterCleanupPlan
{
    internal bool CanApply { get; }
    internal string BlockReason { get; }
    internal IReadOnlyList<string> HirableIds { get; }
    internal IReadOnlyList<string> HiredIds { get; }
    internal IReadOnlyList<string> DismissedIds { get; }
    internal IReadOnlyList<string> UnburiedIds { get; }
    internal IReadOnlyList<string> BuriedIds { get; }
    internal int AlreadyAbsentCount { get; }

    internal PromotionRosterCleanupPlan(bool canApply, string blockReason,
        IReadOnlyList<string> hirableIds, IReadOnlyList<string> hiredIds,
        IReadOnlyList<string> dismissedIds, IReadOnlyList<string> unburiedIds,
        IReadOnlyList<string> buriedIds, int alreadyAbsentCount)
    {
        CanApply = canApply;
        BlockReason = blockReason;
        HirableIds = hirableIds;
        HiredIds = hiredIds;
        DismissedIds = dismissedIds;
        UnburiedIds = unburiedIds;
        BuriedIds = buriedIds;
        AlreadyAbsentCount = alreadyAbsentCount;
    }
}

/// <summary>Builds an all-or-nothing plan for removing promoted leaders from every native roster list.</summary>
internal static class PromotionRosterCleanup
{
    internal static PromotionRosterCleanupPlan BuildPlan(IEnumerable<string> leaderIds,
        IEnumerable<PromotionRosterEntry> rosterEntries)
    {
        if (leaderIds == null) throw new ArgumentNullException(nameof(leaderIds));
        if (rosterEntries == null) throw new ArgumentNullException(nameof(rosterEntries));

        var targets = new HashSet<string>(StringComparer.Ordinal);
        var targetOrder = new List<string>();
        foreach (string id in leaderIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !targets.Add(id))
                return Block("The promotion ledger contains a blank or duplicate leader ID.");
            targetOrder.Add(id);
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        var hirable = new HashSet<string>(StringComparer.Ordinal);
        var hired = new HashSet<string>(StringComparer.Ordinal);
        var dismissed = new HashSet<string>(StringComparer.Ordinal);
        var unburied = new HashSet<string>(StringComparer.Ordinal);
        var buried = new HashSet<string>(StringComparer.Ordinal);
        foreach (PromotionRosterEntry entry in rosterEntries)
        {
            if (entry == null || !targets.Contains(entry.LeaderId)) continue;
            found.Add(entry.LeaderId);
            switch (entry.Location)
            {
                case PromotionRosterLocation.Hirable: hirable.Add(entry.LeaderId); break;
                case PromotionRosterLocation.Hired: hired.Add(entry.LeaderId); break;
                case PromotionRosterLocation.Dismissed: dismissed.Add(entry.LeaderId); break;
                case PromotionRosterLocation.Unburied: unburied.Add(entry.LeaderId); break;
                case PromotionRosterLocation.Buried: buried.Add(entry.LeaderId); break;
                default: return Block("A promoted leader has an unknown native roster state.");
            }
        }

        var hirableIds = InTargetOrder(targetOrder, hirable);
        var hiredIds = InTargetOrder(targetOrder, hired);
        var dismissedIds = InTargetOrder(targetOrder, dismissed);
        var unburiedIds = InTargetOrder(targetOrder, unburied);
        var buriedIds = InTargetOrder(targetOrder, buried);
        int absent = 0;
        foreach (string id in targetOrder)
            if (!found.Contains(id)) absent++;

        return new PromotionRosterCleanupPlan(true, null, hirableIds, hiredIds, dismissedIds,
            unburiedIds, buriedIds, absent);
    }

    private static List<string> InTargetOrder(List<string> targetOrder, HashSet<string> matches)
    {
        var result = new List<string>();
        foreach (string id in targetOrder)
            if (matches.Contains(id)) result.Add(id);
        return result;
    }

    private static PromotionRosterCleanupPlan Block(string reason) =>
        new(false, reason, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), 0);
}
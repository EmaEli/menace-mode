using System;
using System.Collections.Generic;
using System.Linq;

namespace SquaddieFieldPromotions;

internal sealed class VehicleCrewSelection
{
    internal VehicleCrewCandidate Candidate;
    internal int EligibleCount;
}

/// <summary>Eligibility and stable ranking for anonymous Veterancy crew-person snapshots.</summary>
internal static class VehiclePromotionRules
{
    internal static VehicleCrewSelection Choose(IEnumerable<VehicleCrewCandidate> candidates,
        ISet<string> alreadyPromoted)
    {
        if (candidates == null) return null;
        alreadyPromoted ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var eligible = candidates
            .Where(IsEligible)
            .Where(candidate => !alreadyPromoted.Contains(SourceKey(candidate.PilotTemplateId, candidate.PersonId)))
            .GroupBy(candidate => SourceKey(candidate.PilotTemplateId, candidate.PersonId),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.Xp)
            .ThenByDescending(candidate => candidate.IsActive)
            .ThenBy(candidate => candidate.PilotTemplateId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.PersonId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return eligible.Count == 0 ? null : new VehicleCrewSelection
        {
            Candidate = eligible[0],
            EligibleCount = eligible.Count
        };
    }

    internal static string SourceKey(string pilotTemplateId, string personId) =>
        (pilotTemplateId ?? "") + "|" + (personId ?? "");

    internal static bool IsEligible(VehicleCrewCandidate candidate) =>
        candidate != null && !string.IsNullOrWhiteSpace(candidate.PilotTemplateId) &&
        PromotionProgression.IsSafeTemplateId(candidate.PilotTemplateId) &&
        Guid.TryParseExact(candidate.PersonId, "N", out Guid person) && person != Guid.Empty &&
        candidate.Xp >= 0 && !candidate.IsPilotDeployed &&
        string.Equals(candidate.Status, "Healthy", StringComparison.OrdinalIgnoreCase);
}
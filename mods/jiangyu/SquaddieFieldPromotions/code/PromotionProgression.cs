using System;
using System.Collections.Generic;
using System.Linq;

namespace SquaddieFieldPromotions;

internal static class PromotionProgression
{
    internal const string TreeId = "mod.squaddiefieldpromotions.generic_infantry_tree";
    internal const string TemplateId = "mod.squaddiefieldpromotions.generic_perk_source";
    internal const string InitialPerkId = "perk.unique_sy_economics";

    internal static readonly (string SkillId, int Tier)[] GenericInfantryPerks =
    {
        ("perk.scout", 1), ("perk.athletic", 1), ("perk.new_tricks", 1),
        ("perk.hit_the_deck", 1), ("perk.buff", 1),
        ("perk.solid_grouping", 2), ("perk.bags_belts", 2),
        ("perk.mobile_infantry", 2), ("perk.share_the_load", 2), ("perk.firing_line", 2),
        ("perk.steady_gun", 3), ("perk.tankbuster", 3),
        ("perk.fortify", 3), ("perk.zig_zag", 3),
        ("perk.full_send", 4), ("perk.command_rally", 4),
    };

    internal static string RootSourceTemplateId(string templateId, PromotionLedger ledger)
    {
        if (!IsSafeTemplateId(templateId)) return "";
        if (!templateId.StartsWith(PromotionRecord.LeaderPrefix, StringComparison.Ordinal))
            return templateId;
        if (ledger == null) return "";

        var records = ledger.Validated();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string current = templateId;
        while (current.StartsWith(PromotionRecord.LeaderPrefix, StringComparison.Ordinal))
        {
            if (!visited.Add(current)) return "";
            var record = records.FirstOrDefault(item => item.LeaderId == current);
            if (record == null || !IsSafeTemplateId(record.SourceLeaderTemplateId)) return "";
            current = record.SourceLeaderTemplateId;
        }
        return IsSafeTemplateId(current) ? current : "";
    }

    internal static bool IsSafeTemplateId(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Any(char.IsControl);
}
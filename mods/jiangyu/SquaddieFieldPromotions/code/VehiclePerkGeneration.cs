using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SquaddieFieldPromotions;

/// <summary>Saved, XP-banded generic vehicle-leader tree; named-pilot signature perks are excluded.</summary>
internal static class VehiclePerkGeneration
{
    // Schema 2 vehicle promotions retain their original native starting perk on old saves.
    internal const int LegacySchema = 2;
    internal const int Schema = 3;
    internal const string LegacyInitialPerkId = "perk.expert_pilot_vehicle";
    internal const string InitialPerkId = PromotionProgression.VehicleInitialPerkId;
    private static readonly int[] Slots = { 4, 5, 4, 2 };

    private static readonly string[][] Base =
    {
        new[] { "solid_grouping", "new_tricks", "tankbuster", "minimize_silhouette" },
        new[] { "bags_belts", "zig_zag", "dash_walker", "fragmentation", "fervor", "scout", "grind_down" },
        new[] { "critical_hits", "minimize_silhouette", "fortify", "vanguard", "call_out_target", "drive_by" },
        new[] { "steady_gun", "lucky_shot", "sharpshooter" },
    };

    private static readonly string[][] Advanced =
    {
        new[] { "lancer", "recycled_parts", "shepherd" },
        new[] { "hotwire_vehicle", "take_aim_new", "unbreakable_vehicle", "quick_hands", "to_shreds_vehicle" },
        new[] { "overload_vehicle", "fury", "finisher" },
        new[] { "berserk", "shooting_gallery", "divine_intervention" },
    };

    private static readonly IReadOnlyDictionary<string, int> MentorTiers =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["perk.solid_grouping"] = 1, ["perk.new_tricks"] = 1, ["perk.tankbuster"] = 1,
            ["perk.lancer"] = 1, ["perk.recycled_parts"] = 1, ["perk.shepherd"] = 1,
            ["perk.bags_belts"] = 2, ["perk.zig_zag"] = 2, ["perk.dash_walker"] = 2,
            ["perk.fragmentation"] = 2, ["perk.fervor"] = 2, ["perk.hotwire_vehicle"] = 2,
            ["perk.take_aim_new"] = 2, ["perk.unbreakable_vehicle"] = 2, ["perk.quick_hands"] = 2,
            ["perk.overload_vehicle"] = 3, ["perk.to_shreds_vehicle"] = 2, ["perk.fury"] = 3,
            ["perk.finisher"] = 3, ["perk.vanguard"] = 3, ["perk.call_out_target"] = 3,
            ["perk.grind_down"] = 2, ["perk.steady_gun"] = 4, ["perk.lucky_shot"] = 4,
            ["perk.sharpshooter"] = 4, ["perk.divine_intervention"] = 4, ["perk.berserk"] = 4,
            ["perk.shooting_gallery"] = 4,
        };

    internal static void Generate(PromotionRecord record, MentorSnapshot mentor, Func<string, bool> isAvailable = null)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        // Only filter new snapshots. Never rewrite an existing vehicle leader on load.
        isAvailable ??= _ => true;
        int xp = record.SourceCrewXp;
        var random = new PerkGeneration.StableRandom(record.LeaderId + ":sfp-vehicle-perks-v1");
        int band = PerkGeneration.Band(xp);
        var used = new HashSet<string>(StringComparer.Ordinal) { InitialPerkId };
        var choices = new List<GeneratedPerk>();

        for (int tier = 1; tier <= 4; tier++)
        {
            int advancedCount = tier switch
            {
                1 => band >= 4 ? 2 : band >= 1 ? 1 : 0,
                2 => band >= 4 ? 2 : band >= 2 ? 1 : 0,
                3 => band >= 5 ? 2 : band >= 3 ? 1 : 0,
                _ => band >= 6 ? 2 : band >= 4 ? 1 : 0,
            };
            var premium = Advanced[tier - 1].Select(Id).Where(id => !used.Contains(id) && isAvailable(id)).ToList();
            var ordinary = Base[tier - 1].Select(Id).Where(id => !used.Contains(id) && isAvailable(id)).ToList();
            random.Shuffle(premium);
            random.Shuffle(ordinary);
            foreach (string id in premium.Take(advancedCount))
            {
                used.Add(id);
                choices.Add(new GeneratedPerk { Id = id, Tier = tier, Origin = PerkGeneration.OriginAdvanced });
            }
            foreach (string id in ordinary)
            {
                if (choices.Count(perk => perk.Tier == tier) >= Slots[tier - 1]) break;
                if (used.Add(id))
                    choices.Add(new GeneratedPerk { Id = id, Tier = tier, Origin = PerkGeneration.OriginBase });
            }
            if (choices.Count(perk => perk.Tier == tier) != Slots[tier - 1])
                throw new InvalidOperationException("Insufficient curated vehicle perk options for tier " + tier);
        }

        int maxMentor = band >= 4 ? 2 : band >= 1 ? 1 : 0;
        int maxMentorTier = band >= 6 ? 4 : band >= 4 ? 3 : 2;
        if (maxMentor > 0 && mentor != null)
        {
            var learned = mentor.LearnedPerkIds
                .Where(id => MentorTiers.TryGetValue(id, out int tier) && tier <= maxMentorTier &&
                    id != InitialPerkId && isAvailable(id))
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            random.Shuffle(learned);
            int inherited = 0;
            foreach (string id in learned)
            {
                if (inherited >= maxMentor) break;
                int tier = MentorTiers[id];
                var existing = choices.FirstOrDefault(perk => perk.Id == id);
                if (existing != null)
                {
                    if (existing.Tier != tier) continue;
                    existing.Origin = PerkGeneration.OriginMentor;
                    inherited++;
                    continue;
                }
                var replace = choices.LastOrDefault(perk => perk.Tier == tier && perk.Origin == PerkGeneration.OriginBase)
                    ?? choices.LastOrDefault(perk => perk.Tier == tier && perk.Origin == PerkGeneration.OriginAdvanced);
                if (replace == null) continue;
                used.Remove(replace.Id);
                replace.Id = id;
                replace.Origin = PerkGeneration.OriginMentor;
                used.Add(id);
                inherited++;
            }
        }

        var ordered = new List<GeneratedPerk>();
        for (int tier = 1; tier <= 4; tier++)
        {
            var group = choices.Where(perk => perk.Tier == tier).ToList();
            random.Shuffle(group);
            ordered.AddRange(group);
        }
        record.PerkSchemaVersion = Schema;
        record.XpAtPromotion = xp;
        record.MentorLeaderTemplateId = PromotionProgression.IsSafeTemplateId(mentor?.LeaderTemplateId)
            ? mentor.LeaderTemplateId : "";
        record.InitialPerkId = InitialPerkId;
        record.GeneratedPerks = ordered;
        Validate(record);
    }

    internal static void Validate(PromotionRecord record)
    {
        record.InitialPerkId ??= "";
        record.MentorLeaderTemplateId ??= "";
        string expectedInitialPerkId = record.PerkSchemaVersion == LegacySchema
            ? LegacyInitialPerkId
            : record.PerkSchemaVersion == Schema ? InitialPerkId : null;
        if (!record.IsVehicleCrewPromotion || expectedInitialPerkId == null ||
            record.XpAtPromotion != record.SourceCrewXp || record.MentorLeaderTemplateId.Length > 256 ||
            record.MentorLeaderTemplateId.Any(char.IsControl) || record.InitialPerkId != expectedInitialPerkId ||
            record.GeneratedPerks == null || record.GeneratedPerks.Count != Slots.Sum())
            throw new InvalidDataException("Invalid saved vehicle perk snapshot.");

        var seen = new HashSet<string>(StringComparer.Ordinal) { expectedInitialPerkId };
        int previousTier = 1;
        foreach (var perk in record.GeneratedPerks)
        {
            if (perk == null || string.IsNullOrWhiteSpace(perk.Id) ||
                !perk.Id.StartsWith("perk.", StringComparison.Ordinal) || perk.Id.Length > 128 ||
                perk.Id.Any(char.IsControl) || !seen.Add(perk.Id) || perk.Tier < previousTier || perk.Tier > 4 ||
                (perk.Origin != PerkGeneration.OriginBase && perk.Origin != PerkGeneration.OriginAdvanced &&
                 perk.Origin != PerkGeneration.OriginMentor))
                throw new InvalidDataException("Saved vehicle perk entry is duplicate or malformed.");
            previousTier = perk.Tier;
        }
        for (int tier = 1; tier <= 4; tier++)
            if (record.GeneratedPerks.Count(perk => perk.Tier == tier) != Slots[tier - 1])
                throw new InvalidDataException("Invalid saved vehicle perk tier distribution.");
    }

    private static string Id(string shortName) => "perk." + shortName;
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SquaddieFieldPromotions;

/// <summary>A saved perk choice, not an instruction to reroll at save/load.</summary>
public sealed class GeneratedPerk
{
    public string Id { get; set; } = "";
    public int Tier { get; set; }
    public string Origin { get; set; } = "";
}

internal sealed class MentorSnapshot
{
    internal string LeaderTemplateId { get; }
    internal string[] LearnedPerkIds { get; }

    internal MentorSnapshot(string id, IEnumerable<string> learned)
    {
        LeaderTemplateId = id ?? "";
        LearnedPerkIds = (learned ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal).ToArray();
    }
}

/// <summary>
/// Stable, curated infantry perk tree generated once per new promotion. MENACE still controls
/// when a leader can buy an option; Veterancy XP only decides which options are offered.
/// </summary>
internal static class PerkGeneration
{
    internal const int Schema = 1;
    internal const string OriginBase = "Base", OriginAdvanced = "Advanced", OriginMentor = "Mentor";
    internal static readonly int[] Slots = { 5, 5, 4, 2 };

    // Native perks observed at these tiers in extracted MENACE perk trees.
    private static readonly string[][] Base =
    {
        new[] { "athletic", "scout", "solid_grouping", "new_tricks", "hit_the_deck", "buff" },
        new[] { "bags_belts", "mobile_infantry", "share_the_load", "stubborn", "determined", "quick_hands", "zig_zag" },
        new[] { "steady_gun", "fortify", "tankbuster", "call_out_target", "pointfire", "grenadier" },
        new[] { "full_send", "team_spirit", "blaze_of_glory" },
    };

    private static readonly string[][] Advanced =
    {
        new[] { "run_and_gun", "payback", "suppressive_fire", "trinketing" },
        new[] { "resourceful", "return_of_serve", "scavenger", "standby" },
        new[] { "counter_strike", "finisher", "disruptive_fire", "fragmentation", "jump_the_grenade" },
        new[] { "critical_hits", "sharpshooter", "berserk", "firing_line" },
    };

    // Learned skills a mentor may pass on; unique, active command and scripted perks are excluded.
    internal static readonly IReadOnlyDictionary<string, int> MentorTiers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["perk.athletic"] = 1, ["perk.scout"] = 1, ["perk.solid_grouping"] = 1, ["perk.hit_the_deck"] = 1,
        ["perk.buff"] = 2, ["perk.bags_belts"] = 2, ["perk.mobile_infantry"] = 2, ["perk.stubborn"] = 2,
        ["perk.determined"] = 2, ["perk.quick_hands"] = 2, ["perk.fortify"] = 3, ["perk.firing_line"] = 4,
    };

    // Same bands as PromotionRules.Attributes and the Squaddie Veterancy rank ladder.
    internal static int Band(int? xp) => xp >= 8000 ? 6 : xp >= 4000 ? 5 : xp >= 2000 ? 4 :
        xp >= 1000 ? 3 : xp >= 500 ? 2 : xp >= 250 ? 1 : 0;

    internal static void Generate(PromotionRecord record, int? xp, MentorSnapshot mentor)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
        var random = new StableRandom(record.LeaderId + ":sfp-perks-v1");
        int band = Band(xp);
        string initial = PromotionProgression.InitialPerkId;
        var used = new HashSet<string>(StringComparer.Ordinal) { initial };
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
            var premium = Advanced[tier - 1].Select(Id).Where(id => !used.Contains(id)).ToList();
            var ordinary = Base[tier - 1].Select(Id).Where(id => !used.Contains(id)).ToList();
            random.Shuffle(premium);
            random.Shuffle(ordinary);
            foreach (string id in premium.Take(advancedCount))
            {
                used.Add(id);
                choices.Add(new GeneratedPerk { Id = id, Tier = tier, Origin = OriginAdvanced });
            }
            foreach (string id in ordinary)
            {
                if (choices.Count(p => p.Tier == tier) >= Slots[tier - 1]) break;
                if (used.Add(id)) choices.Add(new GeneratedPerk { Id = id, Tier = tier, Origin = OriginBase });
            }
            if (choices.Count(p => p.Tier == tier) != Slots[tier - 1])
                throw new InvalidOperationException("Insufficient curated perk options for tier " + tier);
        }

        // Inherited skills are offered as options, never granted automatically.
        int maxMentor = band >= 4 ? 2 : band >= 1 ? 1 : 0;
        int maxMentorTier = band >= 6 ? 4 : band >= 4 ? 3 : 2;
        if (maxMentor > 0 && mentor != null)
        {
            var learned = mentor.LearnedPerkIds
                .Where(id => MentorTiers.TryGetValue(id, out int t) && t <= maxMentorTier && id != initial)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            random.Shuffle(learned);
            int inherited = 0;
            foreach (string id in learned)
            {
                if (inherited >= maxMentor) break;
                int tier = MentorTiers[id];
                var already = choices.FirstOrDefault(p => p.Id == id);
                if (already != null)
                {
                    if (already.Tier != tier) continue; // Never move a skill to another tier.
                    already.Origin = OriginMentor;
                    inherited++;
                    continue;
                }
                var replace = choices.LastOrDefault(p => p.Tier == tier && p.Origin == OriginBase)
                    ?? choices.LastOrDefault(p => p.Tier == tier && p.Origin == OriginAdvanced);
                if (replace == null) continue;
                used.Remove(replace.Id);
                replace.Id = id;
                replace.Origin = OriginMentor;
                used.Add(id);
                inherited++;
            }
        }

        var ordered = new List<GeneratedPerk>();
        for (int tier = 1; tier <= 4; tier++)
        {
            var group = choices.Where(p => p.Tier == tier).ToList();
            random.Shuffle(group);
            ordered.AddRange(group);
        }
        record.PerkSchemaVersion = Schema;
        record.XpAtPromotion = xp;
        record.MentorLeaderTemplateId = PromotionProgression.IsSafeTemplateId(mentor?.LeaderTemplateId)
            ? mentor.LeaderTemplateId : "";
        record.InitialPerkId = initial;
        record.GeneratedPerks = ordered;
        Validate(record);
    }

    private static string Id(string shortName) => "perk." + shortName;

    internal static void Validate(PromotionRecord record)
    {
        record.InitialPerkId ??= "";
        record.MentorLeaderTemplateId ??= "";
        if (record.PerkSchemaVersion == 0) return; // Pre-0.15 record: keeps the shared generic tree.
        if (record.PerkSchemaVersion != Schema)
            throw new InvalidDataException("Unsupported generated perk schema.");
        if (record.XpAtPromotion < 0 || record.MentorLeaderTemplateId.Length > 256 ||
            record.MentorLeaderTemplateId.Any(char.IsControl) ||
            !ValidPerkId(record.InitialPerkId) ||
            record.GeneratedPerks == null || record.GeneratedPerks.Count != Slots.Sum())
            throw new InvalidDataException("Invalid saved dynamic perk snapshot.");
        var seen = new HashSet<string>(StringComparer.Ordinal) { record.InitialPerkId };
        int previousTier = 1;
        foreach (var perk in record.GeneratedPerks)
        {
            if (perk == null || !ValidPerkId(perk.Id) || !seen.Add(perk.Id) ||
                perk.Tier < previousTier || perk.Tier > 4 ||
                (perk.Origin != OriginBase && perk.Origin != OriginAdvanced && perk.Origin != OriginMentor))
                throw new InvalidDataException("Saved perk entry is duplicate or malformed.");
            previousTier = perk.Tier;
        }
        for (int tier = 1; tier <= 4; tier++)
            if (record.GeneratedPerks.Count(p => p.Tier == tier) != Slots[tier - 1])
                throw new InvalidDataException("Invalid saved perk tier distribution.");
    }

    private static bool ValidPerkId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.StartsWith("perk.", StringComparison.Ordinal) &&
        id.Length <= 128 && !id.Any(char.IsControl);

    // FNV-1a + xorshift32; stable between CLR versions, OSes and process restarts.
    private sealed class StableRandom
    {
        private uint _state;
        internal StableRandom(string key)
        {
            _state = 2166136261u;
            foreach (char c in key)
                _state = unchecked((_state ^ c) * 16777619u);
            if (_state == 0) _state = 0x9E3779B9u;
        }

        internal int Next(int exclusive)
        {
            if (exclusive <= 0) throw new ArgumentOutOfRangeException(nameof(exclusive));
            uint x = _state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _state = x;
            return (int)(x % (uint)exclusive);
        }

        internal void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}


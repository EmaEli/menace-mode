using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SquaddieFieldPromotions;

// Managed values only: never retain native soldier wrappers across scene/load boundaries.
internal sealed class Candidate
{
    internal int Id { get; }
    internal string Name { get; }
    internal string Nickname { get; }
    internal int Gender { get; }
    internal int SkinColor { get; }
    internal int Missions { get; }
    internal int? Xp { get; }
    internal bool Assigned { get; }

    internal Candidate(int id, string name, string nickname, int gender, int skinColor,
        int missions, int? xp, bool assigned)
    {
        Id = id; Name = name; Nickname = nickname; Gender = gender;
        SkinColor = skinColor; Missions = missions; Xp = xp; Assigned = assigned;
    }
}

internal sealed class Selection
{
    internal Candidate Soldier { get; }
    internal int LivingCount { get; }
    internal bool UsesXp { get; }
    internal bool PartialXp { get; }
    internal string Criterion => UsesXp ? "highest Veterancy XP" : "most native missions completed";

    internal Selection(Candidate soldier, int livingCount, bool usesXp, bool partialXp)
    {
        Soldier = soldier; LivingCount = livingCount; UsesXp = usesXp; PartialXp = partialXp;
    }
}

internal static class PromotionRules
{
    internal static Selection Choose(IEnumerable<Candidate> candidates)
    {
        var soldiers = candidates.ToArray();
        var ids = new HashSet<int>();
        foreach (var soldier in soldiers)
        {
            if (soldier.Id < 0 || !ids.Add(soldier.Id) || soldier.Missions < 0 || soldier.Xp < 0 ||
                soldier.Gender < 0 || soldier.Gender > 1 || soldier.SkinColor < 0 || soldier.SkinColor > 2)
                throw new InvalidDataException("Invalid or duplicate living squaddie data; review cancelled.");
        }
        if (soldiers.Length == 0) return null;
        // Never rank unknown XP as zero or compare partial XP against a different metric.
        bool allXp = soldiers.All(s => s.Xp.HasValue);
        bool partialXp = !allXp && soldiers.Any(s => s.Xp.HasValue);
        var chosen = soldiers.OrderByDescending(s => allXp ? s.Xp!.Value : s.Missions)
            .ThenBy(s => s.Id).First();
        return new Selection(chosen, soldiers.Length, allXp, partialXp);
    }

    internal static byte[] Attributes(int? xp)
    {
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
        byte[] values = { 65, 65, 62, 50, 50, 5, 45 };
        int bonus = xp >= 500 ? 12 : xp >= 250 ? 10 : xp >= 150 ? 8 :
            xp >= 100 ? 6 : xp >= 50 ? 4 : xp >= 25 ? 2 : 0;
        for (int i = 0; i < values.Length; i++)
            values[i] = (byte)Math.Min(85, values[i] + (i == 5 ? bonus / 2 : bonus));
        return values;
    }

    internal static string Describe(Selection selection)
    {
        const string locked = "PREVIEW ONLY - no soldier is removed and no leader is created.\n" +
            "Permanent promotion is locked until save/load reconstruction and exact source removal are verified.";
        if (selection == null) return "No living squaddie is available.\n\n" + locked;
        var soldier = selection.Soldier;
        var attributes = Attributes(selection.UsesXp ? soldier.Xp : null);
        string xp = soldier.Xp?.ToString() ?? "unavailable (Veterancy is optional)";
        return $"{Clean(soldier.Name, "Squaddie")} '{Clean(soldier.Nickname, "Field")}' (ID {soldier.Id})\n" +
            $"Selection: {selection.Criterion}; ties use lowest native ID.\n" +
            $"Living soldiers reviewed: {selection.LivingCount} (assigned and unassigned).\n" +
            $"Missions: {soldier.Missions}; XP: {xp}; assigned: {(soldier.Assigned ? "yes" : "no")}.\n" +
            $"Gender: {(soldier.Gender == 0 ? "male" : "female")}; native skin: {soldier.SkinColor}.\n" +
            (selection.PartialXp ? "Incomplete XP coverage: native missions used for EVERY candidate.\n" : "") +
            $"Provisional AGI/WS/VAL/TOU/VIT/PRE/POS: {string.Join(" / ", attributes)}.\n\n" + locked;
    }

    private static string Clean(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return new string(value.Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
    }
}
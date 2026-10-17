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
    internal int ExcludedCount { get; }
    internal bool UsesXp { get; }
    internal bool PartialXp { get; }
    internal string Criterion => UsesXp ? "highest Veterancy XP" : "most native missions completed";

    internal Selection(Candidate soldier, int livingCount, bool usesXp, bool partialXp, int excludedCount = 0)
    {
        Soldier = soldier; LivingCount = livingCount; UsesXp = usesXp; PartialXp = partialXp;
        ExcludedCount = excludedCount;
    }
}

internal static class PromotionRules
{
    /// <summary>Best eligible living squaddie; <paramref name="excluded"/> holds already promoted IDs.</summary>
    internal static Selection Choose(IEnumerable<Candidate> candidates, ISet<int> excluded = null)
    {
        var all = candidates.ToArray();
        var ids = new HashSet<int>();
        foreach (var soldier in all)
        {
            if (soldier.Id < 0 || !ids.Add(soldier.Id) || soldier.Missions < 0 || soldier.Xp < 0 ||
                soldier.Gender < 0 || soldier.Gender > 1 || soldier.SkinColor < 0 || soldier.SkinColor > 2)
                throw new InvalidDataException("Invalid or duplicate living squaddie data; promotion cancelled.");
        }
        var soldiers = excluded == null ? all : all.Where(s => !excluded.Contains(s.Id)).ToArray();
        if (soldiers.Length == 0) return null;
        // Never rank unknown XP as zero or compare partial XP against a different metric.
        bool allXp = soldiers.All(s => s.Xp.HasValue);
        bool partialXp = !allXp && soldiers.Any(s => s.Xp.HasValue);
        var chosen = soldiers.OrderByDescending(s => allXp ? s.Xp!.Value : s.Missions)
            .ThenBy(s => s.Id).First();
        return new Selection(chosen, soldiers.Length, allXp, partialXp, all.Length - soldiers.Length);
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

    internal static string Describe(Selection selection, PromotionRecord record)
    {
        const string footer = "The source soldier has been removed from their squad and the reserve. " +
            "Promoted leaders are restored when the save is loaded.";
        if (selection == null)
            return "No eligible living squaddie is available. Soldiers already promoted are skipped.\n" +
                "No leader or soldier was changed.";
        if (record == null) throw new ArgumentNullException(nameof(record));
        var soldier = selection.Soldier;
        string xp = soldier.Xp?.ToString() ?? "unavailable (Veterancy is optional)";
        return $"{Clean(soldier.Name, "Squaddie")} '{Clean(soldier.Nickname, "Field")}' (ID {soldier.Id}) " +
            "is now a squad leader. Recruit the new leader in Hire (0 credits).\n" +
            $"Selection: {selection.Criterion}; ties use lowest native ID.\n" +
            $"Eligible living soldiers: {selection.LivingCount}" +
            (selection.ExcludedCount > 0 ? $" ({selection.ExcludedCount} already promoted skipped).\n" : ".\n") +
            $"Missions: {soldier.Missions}; XP: {xp}; assigned: {(soldier.Assigned ? "yes" : "no")}.\n" +
            (selection.PartialXp ? "Incomplete XP coverage: native missions used for EVERY candidate.\n" : "") +
            $"Leader AGI/WS/VAL/TOU/VIT/PRE/POS: {string.Join(" / ", record.InitialAttributes)}.\n\n" + footer;
    }

    private static string Clean(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return new string(value.Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
    }
}
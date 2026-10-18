using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2CppMenace.Conversations;
using Il2CppMenace.Strategy;

namespace SquaddieFieldPromotions;

internal static class PromotionIdentity
{
    private static readonly string[] ShipUnitTitles =
    {
        "TCRN Imperius Ship's Company",
        "TCRN Imperius Marine Detachment",
        "TCRN Imperius Security Platoon",
        "TCRN Imperius Boarding Section",
        "TCRN Imperius Expeditionary Company",
        "TCRN Imperius Rapid Response Unit",
    };

    internal static void Populate(PromotionRecord record, Candidate candidate, SquaddieServiceRecord service)
    {
        record.MissionsParticipated = candidate.Missions;
        record.SourceUnitTitle = Clean(candidate.LastUnitTitle, "", 120);
        record.SourceCommander = Clean(candidate.LastCommander, "", 100);
        record.VeterancyRank = Clean(candidate.VeterancyRank, "", 64);
        record.UnitTitle = TitleFor(record.BadgeIndex, record.SourceUnitTitle);
        record.UnitDescription = DescriptionFor(record, candidate, service);
    }

    internal static string TitleFor(int badgeIndex, string sourceUnitTitle)
    {
        if (badgeIndex < 0) throw new ArgumentOutOfRangeException(nameof(badgeIndex));
        string source = Clean(sourceUnitTitle, "", 120);
        if (source.Length > 0 && !IsGenericSourceTitle(source))
            return source + " - Imperius Section " + DynamicBadgeFactory.LabelForIndex(badgeIndex);

        int titleIndex = badgeIndex % ShipUnitTitles.Length;
        int cycle = badgeIndex / ShipUnitTitles.Length;
        return ShipUnitTitles[titleIndex] + (cycle == 0 ? "" : " " + (cycle + 1).ToString(CultureInfo.InvariantCulture));
    }

    internal static string LegacyTitle(int badgeIndex) => TitleFor(badgeIndex, "");

    internal static string LegacyDescription(PromotionRecord record)
    {
        string name = Clean(record.Name, "Squaddie", 160);
        return name + " was promoted to squad leader of " + LegacyTitle(record.BadgeIndex) +
            ". Earlier unit-by-unit service history was not recorded by this version of SFP.";
    }

    internal static string DescriptionFor(PromotionRecord record, Candidate candidate, SquaddieServiceRecord service)
    {
        var text = new StringBuilder();
        text.Append("Promoted to squad leader of ").Append(record.UnitTitle).AppendLine(".");
        int trackedMissions = service?.Units?.Sum(unit => unit.MissionsServed) ?? 0;
        if (trackedMissions > 0)
        {
            text.Append("MENACE lifetime mission count: ").Append(candidate.Missions).AppendLine(".");
            text.AppendLine("Recorded unit service since SFP tracking began:");
            foreach (var unit in service.Units.OrderByDescending(item => item.MissionsServed)
                         .ThenBy(item => item.UnitTitle, StringComparer.Ordinal))
            {
                text.Append("- ").Append(unit.MissionsServed).Append(" mission(s) in ")
                    .Append(Clean(unit.UnitTitle, unit.LeaderTemplateId, 120));
                if (!string.IsNullOrWhiteSpace(unit.CommanderName))
                    text.Append(" under ").Append(Clean(unit.CommanderName, "", 100));
                text.Append(" (wounded in ").Append(unit.WoundedMissions).Append(" mission(s))");
                text.AppendLine(".");
                if (text.Length > 7000)
                {
                    text.AppendLine("Additional unit assignments are stored in the service record.");
                    break;
                }
            }
            int wounded = service.Units.Sum(unit => unit.WoundedMissions);
            text.Append("Wounded in ").Append(wounded).AppendLine(" recorded mission(s).");
        }
        else
        {
            text.Append("MENACE records ").Append(candidate.Missions).AppendLine(" prior mission(s).");
            text.AppendLine("Earlier unit-by-unit service history was not recorded by SFP.");
        }
        if (!string.IsNullOrWhiteSpace(record.SourceUnitTitle))
        {
            text.Append("Last known assignment: ").Append(Clean(record.SourceUnitTitle, "", 120));
            if (!string.IsNullOrWhiteSpace(record.SourceCommander))
                text.Append(" under ").Append(Clean(record.SourceCommander, "", 100));
            text.AppendLine(".");
        }
        if (!string.IsNullOrWhiteSpace(record.VeterancyRank))
            text.Append("Veterancy status: ").Append(Clean(record.VeterancyRank, "", 64)).AppendLine(".");
        else text.AppendLine("Veterancy status was not available at promotion time.");
        string description = new string(text.ToString()
            .Where(c => c == '\n' || c == '\t' || (!char.IsControl(c) && c != '<' && c != '>'))
            .Take(8000).ToArray()).Trim();
        return description.Length == 0 ? "Service history unavailable." : description;
    }

    internal static string CommanderName(SpeakerTemplate speaker)
    {
        if (speaker == null) return "";
        string nickname = speaker.Nickname?.GetRawDefaultTranslation();
        return Clean(string.IsNullOrWhiteSpace(nickname) ? speaker.FullName : nickname, "", 100);
    }

    internal static string Clean(string value, string fallback, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var clean = new string(value.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(maximum).ToArray()).Trim();
        return clean.Length == 0 ? fallback : clean;
    }

    private static bool IsGenericSourceTitle(string value) =>
        value.Equals("15th Vanguard Division", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Field Promotion Squad", StringComparison.OrdinalIgnoreCase);
}
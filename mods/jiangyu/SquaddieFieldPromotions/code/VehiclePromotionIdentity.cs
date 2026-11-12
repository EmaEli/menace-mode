using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppMenace.States;
using Il2CppMenace.Tools;
using Il2CppMenace.Strategy;

namespace SquaddieFieldPromotions;

internal static class VehiclePromotionIdentity
{
    private static readonly string[] Forenames =
    {
        "Ari", "Bastian", "Cass", "Dara", "Elias", "Farah", "Galen", "Hana",
        "Ilan", "Juno", "Kellan", "Lina", "Marek", "Nadia", "Oren", "Petra",
        "Quinn", "Rafi", "Sera", "Tomas", "Uma", "Vera", "Wes", "Yara", "Zane"
    };

    private static readonly string[] Surnames =
    {
        "Arden", "Brek", "Cato", "Dorne", "Esker", "Fen", "Gant", "Havel",
        "Ibarra", "Jarek", "Kade", "Lorne", "Marek", "Neri", "Orlov", "Pell",
        "Quade", "Rook", "Sato", "Tallis", "Ulan", "Varga", "Weller", "Yoon", "Zoric"
    };

    internal static PromotionRecord CreateRecord(StrategyState state, VehicleCrewSelection selection,
        int badgeIndex, PromotionLedger ledger)
    {
        if (selection?.Candidate == null) throw new ArgumentNullException(nameof(selection));
        VehicleCrewCandidate candidate = selection.Candidate;
        string token = Guid.NewGuid().ToString("N");
        string[] name = UniqueName(candidate, ledger);
        UnitLeaderTemplate pilot = DataTemplateLoader.Get<UnitLeaderTemplate>(candidate.PilotTemplateId, false)
            ?? throw new InvalidOperationException("The source vehicle pilot template is unavailable.");
        if (!PrivateMember.TryGet(pilot, "UnitActorType", out object actorType) || Convert.ToInt32(actorType) != 1 ||
            !PrivateMember.TryGet(pilot, "InitialVehicleItem", out object vehicleItem) || vehicleItem != null)
            throw new InvalidOperationException("The source vehicle pilot template is not a generic vehicle leader.");
        int gender = Convert.ToInt32(pilot.Gender);
        int skin = Convert.ToInt32(pilot.SkinColor);
        string rank = PromotionIdentity.Clean(candidate.VeterancyRank, RankFromXp(candidate.Xp), 64);
        string role = PromotionIdentity.Clean(candidate.Role, "Crew", 80);
        string sourceCommander = PromotionIdentity.Clean(candidate.PilotName, "Unknown pilot", 100);
        string vehicleName = PromotionIdentity.Clean(candidate.VehicleName, "", 120);

        var record = new PromotionRecord
        {
            LeaderId = PromotionRecord.LeaderPrefix + token,
            SpeakerId = PromotionRecord.SpeakerPrefix + token,
            NumericGuid = LeaderForge.FindUnusedLeaderGuid(state),
            SourceSquaddieId = 0,
            Forename = name[0],
            Surname = name[1],
            IsVehicleCrewPromotion = true,
            SourceCrewPilotTemplateId = candidate.PilotTemplateId,
            SourceCrewPersonId = candidate.PersonId,
            ReplacementCrewPersonId = Guid.NewGuid().ToString("N"),
            SourceCrewPilotName = sourceCommander,
            SourceCrewRole = role,
            SourceCrewVehicleGuid = PromotionIdentity.Clean(candidate.VehicleGuid, "", 128),
            SourceVehicleName = vehicleName,
            SourceCrewXp = candidate.Xp,
            SourceCrewWasActive = candidate.IsActive,
            CrewTransactionState = "Pending",
            Name = name[0] + " " + name[1],
            Nickname = role,
            Gender = gender,
            SkinColor = skin,
            BadgeIndex = badgeIndex,
            VoicePitchPreset = VoicePitchProfiles.ForBadge(badgeIndex),
            InitialAttributes = PromotionRules.Attributes(candidate.Xp),
            SourceCommander = sourceCommander,
            SourceLeaderTemplateId = PromotionProgression.RootSourceTemplateId(candidate.PilotTemplateId, ledger),
            VeterancyRank = rank,
            UnitTitle = rank + " Vehicle Commander",
        };
        record.UnitDescription = Description(record, candidate, selection.EligibleCount);
        MentorSnapshot mentor = null;
        try { mentor = MentorPerks.ReadHiredLeader(state, candidate.PilotTemplateId); }
        catch { /* Mentor perks are optional; retain the curated vehicle pool. */ }
        VehiclePerkGeneration.Generate(record, mentor, DynamicPerkTrees.IsAvailable);
        record.Validate();
        return record;
    }

    private static string Description(PromotionRecord record, VehicleCrewCandidate candidate, int eligibleCount)
    {
        var text = new StringBuilder();
        text.Append(record.Name).Append(" was promoted from the ").Append(record.SourceCrewRole)
            .Append(" position in ").Append(record.SourceCommander);
        if (record.SourceVehicleName.Length > 0)
            text.Append("'s ").Append(record.SourceVehicleName);
        if (record.SourceCrewVehicleGuid.Length > 0)
            text.Append(" [vehicle ").Append(record.SourceCrewVehicleGuid).Append("]");
        text.AppendLine(".");
        text.Append("Veterancy: ").Append(record.VeterancyRank).Append(" (")
            .Append(record.SourceCrewXp).AppendLine(" XP).");
        text.Append("The crew member was replaced with a fresh recruit. Only the new commander is added to Hire; ")
            .AppendLine("no vehicle is included.");
        text.Append("Promotion source: ").Append(record.SourceCrewWasActive ? "active crew" : "reserve crew")
            .Append("; ").Append(eligibleCount).AppendLine(" eligible crew member(s) were available.");
        return PromotionIdentity.Clean(text.ToString(), "Vehicle crew promotion.", 8000);
    }

    private static string[] UniqueName(VehicleCrewCandidate candidate, PromotionLedger ledger)
    {
        var existing = new HashSet<string>((ledger?.Records ?? new List<PromotionRecord>())
            .Where(record => record != null).Select(record => record.Name), StringComparer.OrdinalIgnoreCase);
        var random = new PerkGeneration.StableRandom(candidate.PersonId + ":sfp-vehicle-name-v1");
        int firstStart = random.Next(Forenames.Length);
        int lastStart = random.Next(Surnames.Length);
        for (int attempt = 0; attempt < Forenames.Length * Surnames.Length; attempt++)
        {
            string first = Forenames[(firstStart + attempt) % Forenames.Length];
            string last = Surnames[(lastStart + attempt / Forenames.Length) % Surnames.Length];
            if (!existing.Contains(first + " " + last)) return new[] { first, last };
        }
        throw new InvalidOperationException("No unused vehicle-commander identity remains in this save.");
    }

    private static string RankFromXp(int xp) => xp >= 8000 ? "Elite" : xp >= 4000 ? "Crack" :
        xp >= 2000 ? "Veteran" : xp >= 1000 ? "Regular" : xp >= 500 ? "Hardened" :
        xp >= 250 ? "Combat Tested" : "Green";

}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SquaddieFieldPromotions;

/// <summary>
/// Everything needed to rebuild one promoted leader's native templates on load.
/// Managed values only; serialized by Jiangyu into the save's mod-state sidecar.
/// </summary>
public sealed class PromotionRecord
{
    internal const string IdPrefix = "mod.squaddiefieldpromotions.";
    internal const string LeaderPrefix = IdPrefix + "leader.";
    internal const string SpeakerPrefix = IdPrefix + "speaker.";
    public string LeaderId { get; set; } = "";
    public string SpeakerId { get; set; } = "";
    public int NumericGuid { get; set; }
    public int SourceSquaddieId { get; set; }
    // Absent/false in 0.9.x ledgers. Migrated only after a matching leader exists in this save.
    public bool SourceRemoved { get; set; }
    public string Name { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Gender { get; set; }
    public int SkinColor { get; set; }
    // Empty on old sidecars and promotions without a matching first-per-save portrait profile.
    public string PortraitProfile { get; set; } = "";
    public int BadgeIndex { get; set; }
    // Neutral when omitted from an older sidecar: no save migration required.
    public int VoicePitchPreset { get; set; }
    // Blank on older sidecars; rehydration assigns a stable profile in memory, without rewriting the save.
    // New promotions persist their profile and never reroll it.
    public string VoiceSetId { get; set; } = "";
    public int MissionsParticipated { get; set; }
    public string SourceUnitTitle { get; set; } = "";
    public string SourceCommander { get; set; } = "";
    // Optional in older save sidecars; blank provenance deliberately falls back to generic art.
    public string SourceLeaderTemplateId { get; set; } = "";
    public string VeterancyRank { get; set; } = "";
    public string UnitTitle { get; set; } = "";
    public string UnitDescription { get; set; } = "";
    public byte[] InitialAttributes { get; set; } = Array.Empty<byte>();
    // 0 on pre-0.15 sidecars: the leader keeps the shared generic tree and purchased perks.
    public int PerkSchemaVersion { get; set; }
    public int? XpAtPromotion { get; set; }
    public string InitialPerkId { get; set; } = "";
    public string MentorLeaderTemplateId { get; set; } = "";
    public List<GeneratedPerk> GeneratedPerks { get; set; } = new();

    public void Validate()
    {
        SourceLeaderTemplateId ??= "";
        PortraitProfile ??= "";
        VoiceSetId ??= "";
        if (!ValidId(LeaderId, LeaderPrefix) || !ValidId(SpeakerId, SpeakerPrefix) ||
            !string.Equals(LeaderId.Substring(LeaderPrefix.Length),
                           SpeakerId.Substring(SpeakerPrefix.Length), StringComparison.Ordinal))
            throw new InvalidDataException("Promotion record leader/speaker ID pair is invalid.");
        if (NumericGuid <= 0 || SourceSquaddieId < 0 || BadgeIndex < 0 || MissionsParticipated < 0)
            throw new InvalidDataException("Promotion record contains invalid numeric ID/index.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 160 ||
            string.IsNullOrWhiteSpace(Nickname) || Nickname.Length > 160)
            throw new InvalidDataException("Promotion record name/nickname invalid.");
        if (Gender != 0 && Gender != 1)
            throw new InvalidDataException("Promotion record gender must match the native MENACE genders.");
        if (SkinColor < 0 || SkinColor > 2)
            throw new InvalidDataException("Promotion record skin color must match the native MENACE palette.");
        if (PortraitProfile.Length > 64 ||
            (PortraitProfile.Length > 0 && !PromotionPortraitAssignment.IsKnownProfile(PortraitProfile)))
            throw new InvalidDataException("Promotion record portrait profile is invalid.");
        if (PortraitProfile.Length > 0 &&
            !PromotionPortraitAssignment.IsCompatible(PortraitProfile, Gender, SkinColor))
            throw new InvalidDataException("The portrait profile does not match the recorded native gender/skin.");
        if (!VoicePitchProfiles.IsValid(VoicePitchPreset))
            throw new InvalidDataException("Promotion record voice pitch preset is invalid.");
        if (VoiceSetId.Length > 64 || (VoiceSetId.Length > 0 && !PromotionVoiceCatalog.SafeId(VoiceSetId)))
            throw new InvalidDataException("Promotion record voice set ID is invalid.");
        if (string.IsNullOrWhiteSpace(UnitTitle) || UnitTitle.Length > 180 ||
            string.IsNullOrWhiteSpace(UnitDescription) || UnitDescription.Length > 8000 ||
            (SourceUnitTitle?.Length ?? 0) > 120 || (SourceCommander?.Length ?? 0) > 100 ||
            SourceLeaderTemplateId.Length > 256 || SourceLeaderTemplateId.Any(char.IsControl) ||
            (VeterancyRank?.Length ?? 0) > 64)
            throw new InvalidDataException("Promotion record title/description/service identity invalid.");
        if (InitialAttributes == null || InitialAttributes.Length != 7)
            throw new InvalidDataException("Promotion record leader attributes invalid.");
        foreach (byte attribute in InitialAttributes)
            if (attribute > 100) throw new InvalidDataException("Promotion record attribute outside 0..100.");
        PerkGeneration.Validate(this);
    }

    private static bool ValidId(string value, string prefix)
    {
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string token = value.Substring(prefix.Length);
        if (token.Length != 32) return false;
        foreach (char c in token)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }
}

/// <summary>
/// Per-save ledger of promotions, persisted by Jiangyu (Context.State) beside the save file.
/// The sidecar key is this type's full name; the early load path reads the same blob.
/// </summary>
public sealed class PromotionLedger
{
    public const int CurrentVersion = 2;
    public int Version { get; set; } = CurrentVersion;
    public List<PromotionRecord> Records { get; set; } = new();
    public List<SquaddieServiceRecord> ServiceRecords { get; set; } = new();
    public List<string> ProcessedMissionKeys { get; set; } = new();

    /// <summary>Validated records in ledger order; throws on any malformed or duplicate entry.</summary>
    public IReadOnlyList<PromotionRecord> Validated()
    {
        bool legacy = Version == 1;
        if (Version != CurrentVersion && !legacy)
            throw new InvalidDataException("Unsupported promotion ledger version " + Version + ".");
        if (legacy)
        {
            foreach (var record in Records ?? new List<PromotionRecord>())
            {
                if (record == null) throw new InvalidDataException("Promotion ledger contains a null record.");
                record.UnitTitle = PromotionIdentity.LegacyTitle(record.BadgeIndex);
                record.UnitDescription = PromotionIdentity.LegacyDescription(record);
            }
            Version = CurrentVersion;
        }
        Records ??= new List<PromotionRecord>();
        var records = Records;
        var leaderIds = new HashSet<string>(StringComparer.Ordinal);
        var guids = new HashSet<int>();
        var squaddies = new HashSet<int>();
        var badges = new HashSet<int>();
        foreach (var record in records)
        {
            if (record == null) throw new InvalidDataException("Promotion ledger contains a null record.");
            record.Validate();
            if (!leaderIds.Add(record.LeaderId) || !guids.Add(record.NumericGuid) || !badges.Add(record.BadgeIndex) ||
                (record.SourceSquaddieId > 0 && !squaddies.Add(record.SourceSquaddieId)))
                throw new InvalidDataException("Promotion ledger contains duplicate identities.");
        }
        ServiceRecords ??= new List<SquaddieServiceRecord>();
        ProcessedMissionKeys ??= new List<string>();
        if (ProcessedMissionKeys.Count > 10000 || ProcessedMissionKeys.Any(key =>
                string.IsNullOrWhiteSpace(key) || key.Length > 256) ||
            ProcessedMissionKeys.Distinct(StringComparer.Ordinal).Count() != ProcessedMissionKeys.Count)
            throw new InvalidDataException("Promotion ledger contains invalid or duplicate mission keys.");
        var serviceIds = new HashSet<int>();
        foreach (var service in ServiceRecords)
        {
            if (service == null || service.SquaddieId <= 0 || !serviceIds.Add(service.SquaddieId) || service.Units == null)
                throw new InvalidDataException("Promotion ledger contains an invalid squaddie service record.");
            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in service.Units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.LeaderTemplateId) || unit.LeaderTemplateId.Length > 256 ||
                    string.IsNullOrWhiteSpace(unit.UnitTitle) || unit.UnitTitle.Length > 120 ||
                    (unit.CommanderName?.Length ?? 0) > 100 || unit.MissionsServed <= 0 || unit.WoundedMissions < 0 ||
                    unit.WoundedMissions > unit.MissionsServed || !unitIds.Add(unit.LeaderTemplateId))
                    throw new InvalidDataException("Promotion ledger contains invalid unit service data.");
            }
        }
        return records;
    }

    public HashSet<int> PromotedSquaddieIds()
    {
        var ids = new HashSet<int>();
        foreach (var record in Validated())
            if (record.SourceSquaddieId > 0) ids.Add(record.SourceSquaddieId);
        return ids;
    }

    public int NextBadgeIndex()
    {
        int next = 0;
        foreach (var record in Validated()) next = Math.Max(next, record.BadgeIndex + 1);
        return next;
    }

    public SquaddieServiceRecord FindServiceRecord(int squaddieId)
    {
        Validated();
        return ServiceRecords.FirstOrDefault(record => record.SquaddieId == squaddieId);
    }
}

/// <summary>Assigns every new promotion a portrait set matching its native gender/skin, rotating within that pool.</summary>
internal static class PromotionPortraitAssignment
{
    internal const string MaleBrown01 = "promoted_male_01";
    internal const string Female01 = "promoted_female_01";
    internal const string MaleBlack02 = "promoted_male_02";
    internal const string FemaleBlack02 = "promoted_female_02";
    internal const string MaleWhite03 = "promoted_male_03";
    internal const string FemaleWhite03 = "promoted_female_03";
    internal const string MaleBrown04 = "promoted_male_04";
    internal const string FemaleBrown04 = "promoted_female_04";
    internal const string MaleBlack05 = "promoted_male_05";
    internal const string FemaleBlack05 = "promoted_female_05";
    internal const string MaleBrown06 = "promoted_male_06";
    internal const string FemaleBrown06 = "promoted_female_06";
    internal const string MaleWhite07 = "promoted_male_07";
    internal const string FemaleWhite07 = "promoted_female_07";
    internal const string MaleBlack08 = "promoted_male_08";
    internal const string FemaleBlack08 = "promoted_female_08";
    internal const string MaleWhite09 = "promoted_male_09";
    internal const string FemaleWhite09 = "promoted_female_09";
    internal const string MaleBrown10 = "promoted_male_10";
    internal const string FemaleBrown10 = "promoted_female_10";
    internal const string MaleWhite11 = "promoted_male_11";
    internal const string FemaleWhite11 = "promoted_female_11";
    internal const string MaleBlack12 = "promoted_male_12";
    internal const string FemaleBlack12 = "promoted_female_12";
    // MENACE 0.7.17 values are verified in its native proxy dump.
    internal const int MaleGender = 0;
    internal const int FemaleGender = 1;
    internal const int WhiteSkinColor = 0;
    internal const int BrownSkinColor = 1;
    internal const int BlackSkinColor = 2;
    // 0.14.0 persisted this profile on male White records; keep those sidecars loadable.
    internal const int LegacyWhiteSkinColor = WhiteSkinColor;

    // Rotation pools in assignment order. Add further sets here; rotation picks them up automatically.
    private static readonly (string Profile, int Gender, int SkinColor)[] Profiles =
    {
        (MaleBrown01, MaleGender, BrownSkinColor), (MaleBrown04, MaleGender, BrownSkinColor),
        (MaleBrown06, MaleGender, BrownSkinColor), (MaleBrown10, MaleGender, BrownSkinColor),
        (MaleBlack02, MaleGender, BlackSkinColor), (MaleBlack05, MaleGender, BlackSkinColor),
        (MaleBlack08, MaleGender, BlackSkinColor), (MaleBlack12, MaleGender, BlackSkinColor),
        (MaleWhite03, MaleGender, WhiteSkinColor),
        (MaleWhite07, MaleGender, WhiteSkinColor), (MaleWhite09, MaleGender, WhiteSkinColor),
        (MaleWhite11, MaleGender, WhiteSkinColor),
        (Female01, FemaleGender, BrownSkinColor), (FemaleBrown04, FemaleGender, BrownSkinColor),
        (FemaleBrown06, FemaleGender, BrownSkinColor), (FemaleBrown10, FemaleGender, BrownSkinColor),
        (FemaleBlack02, FemaleGender, BlackSkinColor), (FemaleBlack05, FemaleGender, BlackSkinColor),
        (FemaleBlack08, FemaleGender, BlackSkinColor), (FemaleBlack12, FemaleGender, BlackSkinColor),
        (FemaleWhite03, FemaleGender, WhiteSkinColor),
        (FemaleWhite07, FemaleGender, WhiteSkinColor), (FemaleWhite09, FemaleGender, WhiteSkinColor),
        (FemaleWhite11, FemaleGender, WhiteSkinColor),
    };

    internal static IEnumerable<string> AllProfiles => Profiles.Select(item => item.Profile);

    internal static bool IsKnownProfile(string profile) => Profiles.Any(item => item.Profile == profile);

    internal static bool IsCompatible(string profile, int gender, int skinColor)
    {
        // 0.14.0 persisted male_01 on male White records; keep those sidecars loadable.
        if (profile == MaleBrown01 && gender == MaleGender && skinColor == LegacyWhiteSkinColor) return true;
        // 0.15.3/0.15.4 assigned portrait set 06 to White; preserve those saved appearances.
        if ((profile == MaleBrown06 && gender == MaleGender || profile == FemaleBrown06 && gender == FemaleGender) &&
            skinColor == WhiteSkinColor) return true;
        // 0.14.3 applied female_01 to the first female of any native skin color.
        if (profile == Female01) return gender == FemaleGender;
        return Profiles.Any(item => item.Profile == profile && item.Gender == gender && item.SkinColor == skinColor);
    }

    internal static string[] ProfilesFor(int gender, int skinColor) => Profiles
        .Where(item => item.Gender == gender && item.SkinColor == skinColor)
        .Select(item => item.Profile).ToArray();

    /// <summary>Picks the least-used compatible set in this save; ties keep pool order.</summary>
    internal static bool TryAssignRotatingProfile(PromotionLedger ledger, PromotionRecord record)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        if (record == null) throw new ArgumentNullException(nameof(record));
        IReadOnlyList<PromotionRecord> existing = ledger.Validated();
        record.Validate();
        if (record.PortraitProfile.Length != 0) return false;
        string profile = LeastUsed(ProfilesFor(record.Gender, record.SkinColor), existing);
        if (profile == null) return false;
        record.PortraitProfile = profile;
        record.Validate();
        return true;
    }

    internal static string LeastUsed(IReadOnlyList<string> pool, IReadOnlyList<PromotionRecord> existing)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool
            .Select((candidate, order) => (candidate, order,
                uses: existing.Count(item => item.PortraitProfile == candidate)))
            .OrderBy(item => item.uses).ThenBy(item => item.order).First().candidate;
    }
}

/// <summary>
/// Bounded deterministic voice profiles. Index zero preserves the unchanged
/// voice on pre-feature JSON save sidecars.
/// </summary>
internal static class VoicePitchProfiles
{
    private static readonly float[] Multipliers = { 1.00f, 0.90f, 0.95f, 1.05f, 1.10f };
    internal static int Count => Multipliers.Length;
    internal static bool IsValid(int preset) => preset >= 0 && preset < Count;
    internal static int ForBadge(int badgeIndex)
    {
        if (badgeIndex < 0) throw new ArgumentOutOfRangeException(nameof(badgeIndex));
        return badgeIndex % Count;
    }
    internal static float Pitch(int preset)
    {
        if (!IsValid(preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        return Multipliers[preset];
    }
}

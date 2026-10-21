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
    // Empty on old sidecars and all leaders except the one assigned bespoke artwork.
    public string PortraitProfile { get; set; } = "";
    public int BadgeIndex { get; set; }
    // Neutral when omitted from an older sidecar: no save migration required.
    public int VoicePitchPreset { get; set; }
    public int MissionsParticipated { get; set; }
    public string SourceUnitTitle { get; set; } = "";
    public string SourceCommander { get; set; } = "";
    // Optional in older save sidecars; blank provenance deliberately falls back to generic art.
    public string SourceLeaderTemplateId { get; set; } = "";
    public string VeterancyRank { get; set; } = "";
    public string UnitTitle { get; set; } = "";
    public string UnitDescription { get; set; } = "";
    public byte[] InitialAttributes { get; set; } = Array.Empty<byte>();

    public void Validate()
    {
        SourceLeaderTemplateId ??= "";
        PortraitProfile ??= "";
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
        var portraitProfiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record == null) throw new InvalidDataException("Promotion ledger contains a null record.");
            record.Validate();
            if (!leaderIds.Add(record.LeaderId) || !guids.Add(record.NumericGuid) || !badges.Add(record.BadgeIndex) ||
                (record.SourceSquaddieId > 0 && !squaddies.Add(record.SourceSquaddieId)))
                throw new InvalidDataException("Promotion ledger contains duplicate identities.");
            if (record.PortraitProfile.Length > 0 && !portraitProfiles.Add(record.PortraitProfile))
                throw new InvalidDataException("Promotion ledger contains a duplicate portrait profile.");
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

/// <summary>Assigns each bespoke portrait once per save to the first future matching promotion.</summary>
internal static class PromotionPortraitAssignment
{
    internal const string MaleBrown01 = "promoted_male_01";
    internal const string Female01 = "promoted_female_01";
    internal const string MaleBlack02 = "promoted_male_02";
    // MENACE 0.7.17 values are verified in its native proxy dump.
    internal const int MaleGender = 0;
    internal const int FemaleGender = 1;
    internal const int BrownSkinColor = 1;
    internal const int BlackSkinColor = 2;
    // 0.14.0 persisted this profile on male White records; keep those sidecars loadable.
    internal const int LegacyWhiteSkinColor = 0;

    internal static bool IsKnownProfile(string profile) =>
        profile == MaleBrown01 || profile == Female01 || profile == MaleBlack02;

    internal static bool IsCompatible(string profile, int gender, int skinColor)
    {
        if (profile == MaleBrown01)
            return gender == MaleGender &&
                (skinColor == BrownSkinColor || skinColor == LegacyWhiteSkinColor);
        if (profile == Female01) return gender == FemaleGender;
        if (profile == MaleBlack02) return gender == MaleGender && skinColor == BlackSkinColor;
        return false;
    }

    private static string ProfileFor(int gender, int skinColor)
    {
        if (gender == FemaleGender) return Female01;
        if (gender == MaleGender && skinColor == BrownSkinColor) return MaleBrown01;
        if (gender == MaleGender && skinColor == BlackSkinColor) return MaleBlack02;
        return null;
    }

    internal static bool TryAssignFirstMatchingProfile(PromotionLedger ledger, PromotionRecord record)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        if (record == null) throw new ArgumentNullException(nameof(record));
        IReadOnlyList<PromotionRecord> existing = ledger.Validated();
        record.Validate();
        if (record.PortraitProfile.Length != 0) return false;
        string profile = ProfileFor(record.Gender, record.SkinColor);
        if (profile == null || existing.Any(candidate => candidate.PortraitProfile == profile)) return false;
        record.PortraitProfile = profile;
        record.Validate();
        return true;
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

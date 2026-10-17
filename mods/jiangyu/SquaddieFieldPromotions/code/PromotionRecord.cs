using System;
using System.Collections.Generic;
using System.IO;

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
    public int BadgeIndex { get; set; }
    public byte[] InitialAttributes { get; set; } = Array.Empty<byte>();

    public void Validate()
    {
        if (!ValidId(LeaderId, LeaderPrefix) || !ValidId(SpeakerId, SpeakerPrefix) ||
            !string.Equals(LeaderId.Substring(LeaderPrefix.Length),
                           SpeakerId.Substring(SpeakerPrefix.Length), StringComparison.Ordinal))
            throw new InvalidDataException("Promotion record leader/speaker ID pair is invalid.");
        if (NumericGuid <= 0 || SourceSquaddieId < 0 || BadgeIndex < 0)
            throw new InvalidDataException("Promotion record contains invalid numeric ID/index.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 160 ||
            string.IsNullOrWhiteSpace(Nickname) || Nickname.Length > 160)
            throw new InvalidDataException("Promotion record name/nickname invalid.");
        if (Gender != 0 && Gender != 1)
            throw new InvalidDataException("Promotion record gender must match the native MENACE genders.");
        if (SkinColor < 0 || SkinColor > 2)
            throw new InvalidDataException("Promotion record skin color must match the native MENACE palette.");
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
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public List<PromotionRecord> Records { get; set; } = new();

    /// <summary>Validated records in ledger order; throws on any malformed or duplicate entry.</summary>
    public IReadOnlyList<PromotionRecord> Validated()
    {
        if (Version != CurrentVersion)
            throw new InvalidDataException("Unsupported promotion ledger version " + Version + ".");
        var records = Records ?? new List<PromotionRecord>();
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SquaddieFieldPromotions;

internal sealed class PromotionVoiceCatalog
{
    internal const string FileName = "xcom_voice_profiles.json";
    internal const string BundleFile = "sfp_xcom_voicepacks.sfpbundle";
    internal const string ReceiptFile = "sfp_xcom_voicepacks.build.json";
    private readonly Dictionary<string, PromotionVoiceSet> _byId;

    private PromotionVoiceCatalog(PromotionVoiceCatalogFile file)
    {
        BundleFileName = file.BundleFile;
        ReceiptFileName = file.ReceiptFile;
        Profiles = file.Profiles;
        _byId = new Dictionary<string, PromotionVoiceSet>(StringComparer.Ordinal);
        foreach (var profile in Profiles)
            _byId.Add(profile.Id, profile);
    }

    internal string BundleFileName { get; }
    internal string ReceiptFileName { get; }
    internal IReadOnlyList<PromotionVoiceSet> Profiles { get; }
    internal PromotionVoiceSet Find(string id) => id != null && _byId.TryGetValue(id, out var profile) ? profile : null;

    internal static PromotionVoiceCatalog Load(string modFolder)
    {
        var path = Path.Combine(modFolder, "runtime_assets", "voice", FileName);
        if (!File.Exists(path)) return null;
        return Parse(File.ReadAllText(path));
    }

    internal static PromotionVoiceCatalog Parse(string json)
    {
        var file = JsonSerializer.Deserialize<PromotionVoiceCatalogFile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Validate(file);
        return new PromotionVoiceCatalog(file);
    }

    private static void Validate(PromotionVoiceCatalogFile file)
    {
        if (file == null || file.SchemaVersion != 1 ||
            file.BundleFile != BundleFile || file.ReceiptFile != ReceiptFile ||
            file.Profiles == null || file.Profiles.Count == 0)
            throw new InvalidDataException("Invalid XCOM voice profile catalog header.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in file.Profiles)
        {
            if (profile == null || !SafeId(profile.Id) || !ids.Add(profile.Id) ||
                string.IsNullOrWhiteSpace(profile.SourcePack) || profile.SourcePack.Length > 100 ||
                (profile.Gender != "male" && profile.Gender != "female") || profile.Kind != "human" ||
                profile.RoleEvidence == null || profile.Routes == null || profile.Routes.Count == 0)
                throw new InvalidDataException("Invalid or non-human XCOM voice profile.");

            foreach (var route in profile.Routes)
            {
                if (!SafeRouteKey(route.Key) || route.Value?.Assets == null || route.Value.Assets.Length == 0 ||
                    string.IsNullOrWhiteSpace(route.Value.Subtitle) || route.Value.Subtitle.Length > 400 ||
                    route.Value.Subtitle.Any(char.IsControl) ||
                    route.Value.Assets.Any(asset => !SafeAssetName(asset) ||
                        !asset.StartsWith("xcom_" + profile.Id + "_", StringComparison.Ordinal)))
                    throw new InvalidDataException("Invalid XCOM voice route for " + profile.Id + ".");
            }
        }
    }

    internal static bool SafeId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 64 &&
        id.All(ch => ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '_');

    private static bool SafeAssetName(string name) => !string.IsNullOrEmpty(name) && name.Length <= 128 &&
        name.All(ch => ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '_');

    private static bool SafeRouteKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var parts = key.Split('|');
        return parts.Length == 2 && SafeId(parts[0]) && parts[1].Length > 0 &&
            parts[1].All(ch => ch >= '0' && ch <= '9');
    }
}

internal sealed class PromotionVoiceCatalogFile
{
    public int SchemaVersion { get; set; }
    public string BundleFile { get; set; } = "";
    public string ReceiptFile { get; set; } = "";
    public List<PromotionVoiceSet> Profiles { get; set; } = new();
}

internal sealed class PromotionVoiceSet
{
    public string Id { get; set; } = "";
    public string SourcePack { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool FuturePilotCandidate { get; set; }
    public bool AviationRadioCandidate { get; set; }
    public string[] RoleEvidence { get; set; } = Array.Empty<string>();
    public Dictionary<string, PromotionVoiceRoute> Routes { get; set; } = new(StringComparer.Ordinal);

    internal PromotionVoiceRoute ResolveRoute(string key)
    {
        if (Routes.TryGetValue(key, out var route)) return route;
        // Stem needs a valid clip even for a muted Sound. Use only this speaker's
        // audio as an inaudible carrier, never a generic or another profile's voice.
        return new PromotionVoiceRoute
        {
            Assets = Routes.Values.First().Assets,
            Subtitle = "",
            IsSilent = true
        };
    }
}

internal sealed class PromotionVoiceRoute
{
    public string[] Assets { get; set; } = Array.Empty<string>();
    public string Subtitle { get; set; } = "";
    internal bool IsSilent { get; set; }
}

internal static class PromotionVoiceAssignment
{
    internal static bool TryAssignRotatingProfile(PromotionLedger ledger, PromotionRecord record,
        PromotionVoiceCatalog catalog)
    {
        if (record == null || ledger == null || catalog == null || !string.IsNullOrEmpty(record.VoiceSetId))
            return false;
        string gender = record.Gender == 0 ? "male" : record.Gender == 1 ? "female" : "";
        if (gender.Length == 0) return false;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var existing in ledger.Records)
        {
            if (string.IsNullOrEmpty(existing.VoiceSetId)) continue;
            counts.TryGetValue(existing.VoiceSetId, out int count);
            counts[existing.VoiceSetId] = count + 1;
        }
        var selected = catalog.Profiles.Where(profile => profile.Gender == gender)
            .OrderBy(profile => counts.TryGetValue(profile.Id, out var count) ? count : 0)
            .ThenBy(profile => profile.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null) return false;
        record.VoiceSetId = selected.Id;
        return true;
    }

    internal static int AssignMissingProfiles(PromotionLedger ledger, PromotionVoiceCatalog catalog)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        int assigned = 0;
        foreach (var record in ledger.Validated())
            if (TryAssignRotatingProfile(ledger, record, catalog)) assigned++;
        return assigned;
    }

    internal static void ValidateAssignment(PromotionRecord record, PromotionVoiceCatalog catalog)
    {
        if (string.IsNullOrEmpty(record.VoiceSetId)) return;
        var profile = catalog?.Find(record.VoiceSetId);
        string gender = record.Gender == 0 ? "male" : record.Gender == 1 ? "female" : "";
        if (profile == null || profile.Gender != gender)
            throw new InvalidDataException("Promotion voice profile is missing or has the wrong gender.");
    }
}
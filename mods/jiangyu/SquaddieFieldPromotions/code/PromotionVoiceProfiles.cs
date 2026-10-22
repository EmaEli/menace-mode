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

            var captionsByAsset = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var route in profile.Routes)
            {
                if (!SafeRouteKey(route.Key) || route.Value?.Assets == null || route.Value.Assets.Length == 0 ||
                    string.IsNullOrWhiteSpace(route.Value.Subtitle) || route.Value.Subtitle.Length > 400 ||
                    route.Value.Subtitle.Any(char.IsControl) ||
                    route.Value.Assets.Any(asset => !SafeAssetName(asset) ||
                        !asset.StartsWith("xcom_" + profile.Id + "_", StringComparison.Ordinal)))
                    throw new InvalidDataException("Invalid XCOM voice route for " + profile.Id + ".");
                var captions = route.Value.AssetSubtitles;
                if (captions == null) continue; // Existing same-wording variation catalogs remain valid.
                if (captions.Length != route.Value.Assets.Length || captions.Length == 0 ||
                    captions.Any(text => string.IsNullOrWhiteSpace(text) || text.Length > 400 || text.Any(char.IsControl)) ||
                    captions[0] != route.Value.Subtitle ||
                    route.Value.Assets.Distinct(StringComparer.Ordinal).Count() != route.Value.Assets.Length)
                    throw new InvalidDataException("Invalid per-recording voice captions for " + profile.Id + ".");
                for (int i = 0; i < captions.Length; i++)
                {
                    var asset = route.Value.Assets[i];
                    if (captionsByAsset.TryGetValue(asset, out var previous) && previous != captions[i])
                        throw new InvalidDataException("Conflicting captions for a voice recording in " + profile.Id + ".");
                    captionsByAsset[asset] = captions[i];
                }
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

    // Keep sparse profiles loadable for saved assignments, but never assign them anew.
    internal bool EligibleForAssignment => PromotionVoiceQuality.MeetsMinimum(this);

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
    // Optional schema-1 extension. Missing means every legacy clip shares Subtitle.
    // The first caption must equal Subtitle so direct/non-conversation barks and
    // the safe fallback always play the first recording with its correct wording.
    public string[] AssetSubtitles { get; set; }
    internal bool IsSilent { get; set; }
    internal bool HasCaptionedVariants => !IsSilent && AssetSubtitles != null && Assets.Length > 1;
}

internal sealed class PromotionVoiceVariant
{
    internal PromotionVoiceVariant(string asset, string subtitle, int itemId)
    { Asset = asset; Subtitle = subtitle; ItemId = itemId; }
    internal string Asset { get; }
    internal string Subtitle { get; }
    internal int ItemId { get; }
}

// An immutable audio/text/ID choice, not independent random choices in Stem/UI.
// Snapshot the catalog and keep repeat avoidance local to this native route.
internal sealed class PromotionVoiceVariants
{
    private readonly PromotionVoiceVariant[] _choices;
    private int _previous = -1;

    internal PromotionVoiceVariants(PromotionVoiceRoute route, Func<string, int> itemId)
    {
        if (!route.HasCaptionedVariants || route.AssetSubtitles.Length != route.Assets.Length)
            throw new ArgumentException("A captioned variant pool needs paired recordings and captions.");
        _choices = route.Assets.Select((asset, i) =>
            new PromotionVoiceVariant(asset, route.AssetSubtitles[i], itemId(asset))).ToArray();
        if (_choices.Select(choice => choice.ItemId).Distinct().Count() != _choices.Length)
            throw new InvalidDataException("Captioned voice variant itemId collision.");
    }

    internal IReadOnlyList<PromotionVoiceVariant> Choices => _choices;

    internal PromotionVoiceVariant Next(Random random)
    {
        int next = random.Next(_previous < 0 ? _choices.Length : _choices.Length - 1);
        if (_previous >= 0 && next >= _previous) next++;
        _previous = next;
        return _choices[next];
    }
}

internal static class PromotionVoiceQuality
{
    private static readonly string[] RequiredRoutes =
    {
        "ready|0", "moving|0", "spotted|0", "kill|0", "taking_fire|0", "miss|0",
        "grenade_frag|0", "success|0", "emp_success|0", "emp_failed|0",
        "morale_recovery|0", "ready_quiet|0"
    };

    // First curation pass: retain the strongest imported profiles, not a claim of
    // complete coverage or listening verification. Reused route slots are not variety.
    internal static bool MeetsMinimum(PromotionVoiceSet profile)
    {
        if (profile.Routes.Count < 42 || !RequiredRoutes.All(profile.Routes.ContainsKey))
            return false;
        var assets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in profile.Routes.Values)
            foreach (var asset in route.Assets)
                assets.Add(asset);
        return assets.Count >= 12;
    }
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
        var selected = catalog.Profiles.Where(profile => profile.Gender == gender && profile.EligibleForAssignment)
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
using System.Text.Json;
using System.Text.Json.Nodes;
using SquaddieFieldPromotions;

internal static class VoiceVariantChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string Catalog(object route) => JsonSerializer.Serialize(new
        {
            schemaVersion = 1, bundleFile = PromotionVoiceCatalog.BundleFile, receiptFile = PromotionVoiceCatalog.ReceiptFile,
            profiles = new[] { new { id = "fixture", sourcePack = "Fixture", gender = "male", kind = "human",
                roleEvidence = Array.Empty<string>(), routes = new Dictionary<string, object> { ["ready|0"] = route } } }
        });
        var legacy = PromotionVoiceCatalog.Parse(Catalog(new { assets = new[] { "xcom_fixture_a", "xcom_fixture_b" }, subtitle = "Ready." }));
        check(legacy.Find("fixture").Routes["ready|0"].AssetSubtitles == null &&
            !legacy.Find("fixture").Routes["ready|0"].HasCaptionedVariants,
            "legacy same-wording voice variations do not opt into the new playback hook");
        var json = Catalog(new { assets = new[] { "xcom_fixture_a", "xcom_fixture_b", "xcom_fixture_c" },
            subtitle = "Ready.", assetSubtitles = new[] { "Ready.", "Orders?", "Standing by." } });
        var route = PromotionVoiceCatalog.Parse(json).Find("fixture").Routes["ready|0"];
        check(route.HasCaptionedVariants && route.AssetSubtitles[1] == "Orders?", "per-recording captions parse without a schema migration");
        var ids = new Dictionary<string, int> { ["xcom_fixture_a"] = 101, ["xcom_fixture_b"] = 102, ["xcom_fixture_c"] = 103 };
        var variants = new PromotionVoiceVariants(route, asset => ids[asset]);
        var seen = new HashSet<int>();
        int previous = -1;
        var random = new Random(73);
        for (int i = 0; i < 1000; i++)
        {
            var selected = variants.Next(random);
            check(selected.ItemId != previous && ids[selected.Asset] == selected.ItemId &&
                selected.Subtitle == route.AssetSubtitles[Array.IndexOf(route.Assets, selected.Asset)],
                "audio, caption and native Sound ID are selected atomically without an immediate repeat");
            seen.Add(selected.ItemId);
            previous = selected.ItemId;
        }
        check(seen.SetEquals(ids.Values), "every authored recording can be chosen, not just the donor variation count");
        route.Assets[0] = "xcom_fixture_mutated"; route.AssetSubtitles[0] = "Mutated.";
        check(variants.Choices[0].Asset == "xcom_fixture_a" && variants.Choices[0].Subtitle == "Ready.",
            "an in-flight variant choice does not share mutable catalog storage");
        void Reject(Action<JsonNode> mutate, string message)
        {
            var node = JsonNode.Parse(json); mutate(node["profiles"][0]["routes"]["ready|0"]);
            bool rejected = false;
            try { PromotionVoiceCatalog.Parse(node.ToJsonString()); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, message);
        }
        Reject(node => node["assetSubtitles"] = new JsonArray("Ready."), "reject fewer captions than recordings");
        Reject(node => node["assetSubtitles"] = new JsonArray(), "reject empty per-recording captions");
        Reject(node => node["assetSubtitles"][0] = "Wrong.", "fallback caption must match the first recording");
        Reject(node => node["assetSubtitles"][1] = "", "reject blank variant captions");
        Reject(node => node["assetSubtitles"][1] = "Bad\ncaption", "reject control characters in variant captions");
        Reject(node => node["assets"][1] = "xcom_fixture_a", "reject duplicate recording padding within a captioned route");
        var conflict = JsonNode.Parse(json);
        conflict["profiles"][0]["routes"]["moving|0"] = JsonSerializer.SerializeToNode(new
            { assets = new[] { "xcom_fixture_a" }, subtitle = "Wrong.", assetSubtitles = new[] { "Wrong." } });
        bool conflictRejected = false;
        try { PromotionVoiceCatalog.Parse(conflict.ToJsonString()); }
        catch (InvalidDataException) { conflictRejected = true; }
        check(conflictRejected, "the same recording cannot acquire different captions through different native routes");
        bool collisionRejected = false;
        try { new PromotionVoiceVariants(PromotionVoiceCatalog.Parse(json).Find("fixture").Routes["ready|0"], _ => 101); }
        catch (InvalidDataException) { collisionRejected = true; }
        check(collisionRejected, "native variant Sound ID collisions fail before playback");
        var silent = PromotionVoiceCatalog.Parse(json).Find("fixture").ResolveRoute("arrival|0");
        check(silent.IsSilent && silent.Subtitle == "" && !silent.HasCaptionedVariants,
            "missing events stay silent and never inherit the carrier recording's caption pool");
    }
}
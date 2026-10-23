using System.Text.Json;
using SquaddieFieldPromotions;

internal static class VoiceProposalChecks
{
    internal static void Run(Action<bool, string> check, string proposalPath, string modRoot)
    {
        var baseline = PromotionVoiceCatalog.Load(modRoot);
        var proposal = PromotionVoiceCatalog.Parse(File.ReadAllText(proposalPath));
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(modRoot,
            "runtime_assets/voice/voice_routes.json")));
        var keys = native.RootElement.GetProperty("routes").EnumerateObject()
            .Where(item => item.Name.StartsWith("john|", StringComparison.Ordinal))
            .Select(item => item.Name[5..]).ToHashSet(StringComparer.Ordinal);
        check(keys.Count == 180, "proposal validation uses the real 180 native slots");
        check(baseline.Profiles.Select(p => p.Id).ToHashSet().SetEquals(proposal.Profiles.Select(p => p.Id)),
            "proposal preserves every saved voice ID and adds no untracked profiles");
        var random = new Random(23);
        foreach (var before in baseline.Profiles)
        {
            var after = proposal.Find(before.Id);
            check(after.Gender == before.Gender && after.Kind == before.Kind && after.SourcePack == before.SourcePack &&
                after.FuturePilotCandidate == before.FuturePilotCandidate && after.AviationRadioCandidate == before.AviationRadioCandidate &&
                after.RoleEvidence.SequenceEqual(before.RoleEvidence), "speaker metadata is unchanged: " + before.Id);
            check(after.Routes.Keys.All(keys.Contains), "only real native slots are proposed: " + before.Id);
            check(!before.EligibleForAssignment || after.EligibleForAssignment,
                "no previously eligible speaker is retired by an additive proposal: " + before.Id);
            foreach (var pair in before.Routes)
            {
                check(after.Routes.TryGetValue(pair.Key, out var route), "baseline mapping remains: " + before.Id + "/" + pair.Key);
                check(route.Assets.Take(pair.Value.Assets.Length).SequenceEqual(pair.Value.Assets),
                    "baseline recording/fallback order remains: " + before.Id + "/" + pair.Key);
                var oldCaptions = pair.Value.AssetSubtitles ?? Enumerable.Repeat(pair.Value.Subtitle, pair.Value.Assets.Length).ToArray();
                var newCaptions = route.AssetSubtitles ?? Enumerable.Repeat(route.Subtitle, route.Assets.Length).ToArray();
                check(newCaptions.Take(oldCaptions.Length).SequenceEqual(oldCaptions),
                    "baseline exact captions remain: " + before.Id + "/" + pair.Key);
            }
            foreach (var key in keys)
            {
                var route = after.ResolveRoute(key);
                check(route.IsSilent == !after.Routes.ContainsKey(key) &&
                    route.Assets.All(asset => asset.StartsWith("xcom_" + before.Id + "_", StringComparison.Ordinal)) &&
                    (!route.IsSilent || route.Subtitle == ""), "own speaker or captionless silence: " + before.Id + "/" + key);
                if (!route.HasCaptionedVariants) continue;
                var ids = route.Assets.Select((asset, index) => (asset, index)).ToDictionary(p => p.asset, p => p.index + 1);
                var pool = new PromotionVoiceVariants(route, asset => ids[asset]);
                string previous = null;
                for (var i = 0; i < route.Assets.Length * 2; i++)
                {
                    var choice = pool.Next(random);
                    int index = Array.IndexOf(route.Assets, choice.Asset);
                    check(index >= 0 && choice.Subtitle == route.AssetSubtitles[index] && choice.Asset != previous,
                        "actual candidate variants keep audio/caption paired and avoid consecutive repeats");
                    previous = choice.Asset;
                }
            }
        }
        var unchanged = baseline.Find("trepang2blackops");
        if (unchanged != null)
            check(JsonSerializer.Serialize(unchanged) == JsonSerializer.Serialize(proposal.Find(unchanged.Id)),
                "the existing BlackOps profile is completely unchanged");
        Console.WriteLine("VOICE_PROPOSAL_CONTRACT|profiles=" + proposal.Profiles.Count +
            "|eligible_before=" + baseline.Profiles.Count(p => p.EligibleForAssignment) +
            "|eligible_proposed=" + proposal.Profiles.Count(p => p.EligibleForAssignment));
    }
}
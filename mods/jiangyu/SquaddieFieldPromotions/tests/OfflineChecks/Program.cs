using SquaddieFieldPromotions;
using Jiangyu.Sdk;
using MenaceModSettings;
using Il2CppMenace.Conversations;
using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Strategy;
using System.Text.Json;

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks++;
}
Candidate C(int id, int missions = 0, int? xp = null, bool assigned = false,
    string lastUnitTitle = "", string lastCommander = "", string rank = "", string sourceLeaderTemplateId = "",
    int gender = 0, int skinColor = 1) =>
    new(id, "Soldier", "Nickname", gender, skinColor, missions, xp, assigned, lastUnitTitle, lastCommander, rank,
        sourceLeaderTemplateId);
void Reject(Action action, string name)
{
    try { action(); } catch (InvalidDataException) { Check(true, name); return; }
    throw new Exception("FAILED (accepted invalid data): " + name);
}

var cleanupPlan = PromotionRosterCleanup.BuildPlan(
    new[] { "leader.hirable", "leader.dismissed", "leader.absent", "leader.hired", "leader.unburied", "leader.buried" },
    new[]
    {
        new PromotionRosterEntry("leader.hirable", PromotionRosterLocation.Hirable),
        new PromotionRosterEntry("leader.dismissed", PromotionRosterLocation.Dismissed),
        new PromotionRosterEntry("vanilla.leader", PromotionRosterLocation.Hired),
        new PromotionRosterEntry("leader.hired", PromotionRosterLocation.Hired),
        new PromotionRosterEntry("leader.unburied", PromotionRosterLocation.Unburied),
        new PromotionRosterEntry("leader.buried", PromotionRosterLocation.Buried),
    });
Check(cleanupPlan.CanApply && cleanupPlan.HirableIds.SequenceEqual(new[] { "leader.hirable" }) &&
    cleanupPlan.HiredIds.SequenceEqual(new[] { "leader.hired" }) &&
    cleanupPlan.DismissedIds.SequenceEqual(new[] { "leader.dismissed" }) &&
    cleanupPlan.UnburiedIds.SequenceEqual(new[] { "leader.unburied" }) &&
    cleanupPlan.BuriedIds.SequenceEqual(new[] { "leader.buried" }) && cleanupPlan.AlreadyAbsentCount == 1,
    "cleanup targets SFP IDs in all five native roster states and preserves unrelated leaders");
cleanupPlan = PromotionRosterCleanup.BuildPlan(
    new[] { "leader.duplicate" },
    new[]
    {
        new PromotionRosterEntry("leader.duplicate", PromotionRosterLocation.Hirable),
        new PromotionRosterEntry("leader.duplicate", PromotionRosterLocation.Dismissed),
    });
Check(cleanupPlan.CanApply && cleanupPlan.HirableIds.SequenceEqual(new[] { "leader.duplicate" }) &&
    cleanupPlan.DismissedIds.SequenceEqual(new[] { "leader.duplicate" }),
    "duplicate native placements are planned for removal from every matching list");
cleanupPlan = PromotionRosterCleanup.BuildPlan(
    new[] { "leader.duplicate", "leader.duplicate" },
    Array.Empty<PromotionRosterEntry>());
Check(!cleanupPlan.CanApply, "duplicate IDs in the promotion ledger still block cleanup");

Check(PromotionRules.Choose(Array.Empty<Candidate>()) == null, "empty roster");
var selection = PromotionRules.Choose(new[] { C(2, 20, 5), C(3, 0, 100, true), C(1, 0, 100) });
Check(selection.UsesXp && selection.Soldier.Id == 1, "highest XP and lowest-ID tie");
Check(selection.LivingCount == 3, "assigned and unassigned included");
Check(PromotionRules.Choose(new[] { C(4, xp: 200, assigned: true), C(1, xp: 2) }).Soldier.Id == 4, "assigned top XP eligible");
selection = PromotionRules.Choose(new[] { C(1, 2), C(3, 9), C(2, 9) });
Check(!selection.UsesXp && !selection.PartialXp && selection.Soldier.Id == 2, "no Veterancy: native missions with ID tie");
selection = PromotionRules.Choose(new[] { C(1, 2, 999), C(2, 3, skinColor: 1) });
Check(!selection.UsesXp && selection.PartialXp && selection.Soldier.Id == 2, "partial XP does not mix metrics");
PromotionRecord R(Selection s, int badge = 0, SquaddieServiceRecord service = null) =>
    LeaderForge.CreateRecord(null, s, badge, service);
Check(VoicePitchProfiles.Count == 5 && VoicePitchProfiles.Pitch(0) == 1.00f &&
    VoicePitchProfiles.Pitch(1) == 0.90f && VoicePitchProfiles.Pitch(4) == 1.10f,
    "five bounded pitch profiles with unchanged original voice");
for (int badge = 0; badge < 12; badge++)
    Check(R(selection, badge).VoicePitchPreset == badge % VoicePitchProfiles.Count,
        "promotion badge maps deterministically to pitch profile " + badge);
var oldVoiceJson = System.Text.Json.JsonSerializer.Serialize(R(selection))
    .Replace(",\"VoicePitchPreset\":0", "");
Check(System.Text.Json.JsonSerializer.Deserialize<PromotionRecord>(oldVoiceJson).VoicePitchPreset == 0,
    "pre-feature save records without voice pitch stay neutral");
Check(JsonSerializer.Deserialize<PromotionRecord>("{}").VoiceSetId == "",
    "pre-feature save records have no assigned voice pack");
string voiceCatalogJson = JsonSerializer.Serialize(new
{
    schemaVersion = 1,
    bundleFile = PromotionVoiceCatalog.BundleFile,
    receiptFile = PromotionVoiceCatalog.ReceiptFile,
    profiles = new[]
    {
        new { id = "aaa_sparse", sourcePack = "Sparse", gender = "male", kind = "human", roleEvidence = Array.Empty<string>(),
            routes = new Dictionary<string, object> { ["ready|0"] = new { assets = new[] { "xcom_aaa_sparse_001" }, subtitle = "Ready." } } },
        new { id = "aaron", sourcePack = "Aaron", gender = "male", kind = "human", roleEvidence = Array.Empty<string>(),
            routes = new Dictionary<string, object> { ["ready|0"] = new { assets = new[] { "xcom_aaron_001" }, subtitle = "Ready." } } },
        new { id = "beau", sourcePack = "Beau", gender = "male", kind = "human", roleEvidence = Array.Empty<string>(),
            routes = new Dictionary<string, object> { ["ready|0"] = new { assets = new[] { "xcom_beau_001" }, subtitle = "Go." } } },
        new { id = "bella", sourcePack = "Bella", gender = "female", kind = "human", roleEvidence = Array.Empty<string>(),
            routes = new Dictionary<string, object> { ["ready|0"] = new { assets = new[] { "xcom_bella_001" }, subtitle = "Ready." } } }
    }
});
var voiceCatalog = PromotionVoiceCatalog.Parse(voiceCatalogJson);
VoiceVariantChecks.Run(Check);
var aaronVoice = voiceCatalog.Find("aaron");
Check(ReferenceEquals(aaronVoice.ResolveRoute("ready|0"), aaronVoice.Routes["ready|0"]) &&
    !aaronVoice.ResolveRoute("ready|0").IsSilent,
    "mapped route keeps the assigned speaker's original clips and caption");
foreach (var profile in voiceCatalog.Profiles)
foreach (var missingKey in new[] { "arrival|0", "ally_dead|0", "success|0", "unknown_future_event|99" })
{
    var silentRoute = profile.ResolveRoute(missingKey);
    Check(silentRoute.IsSilent && silentRoute.Subtitle == "" && silentRoute.Assets.Length > 0 &&
        silentRoute.Assets.All(asset => asset.StartsWith("xcom_" + profile.Id + "_")) &&
        !profile.Routes.ContainsKey(missingKey),
        "unmapped route is silent, captionless and never borrows another speaker: " + profile.Id + "/" + missingKey);
}
var voiceModRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var fullVoiceCatalog = PromotionVoiceCatalog.Load(voiceModRoot);
var genericVoiceRoutes = JsonDocument.Parse(File.ReadAllText(Path.Combine(voiceModRoot,
    "runtime_assets/voice/voice_routes.json"))).RootElement.GetProperty("routes");
var allVoiceRouteKeys = genericVoiceRoutes.EnumerateObject().Select(route => route.Name)
    .Where(key => key.StartsWith("john|")).Select(key => key.Substring(5)).ToArray();
Check(fullVoiceCatalog.Profiles.Count == 56 && allVoiceRouteKeys.Length == 180,
    "full private voice catalog and all native SFP routes are included in coverage checks");
Check(fullVoiceCatalog.Profiles.All(profile => profile.Routes.Keys.All(allVoiceRouteKeys.Contains)),
    "every imported route corresponds to a native SFP event slot");
foreach (var profile in fullVoiceCatalog.Profiles)
foreach (var key in allVoiceRouteKeys)
{
    var route = profile.ResolveRoute(key);
    var mapped = profile.Routes.ContainsKey(key);
    Check(route.IsSilent == !mapped && (mapped || route.Subtitle == "") &&
        route.Assets.All(asset => asset.StartsWith("xcom_" + profile.Id + "_")),
        "full catalog route uses its own speaker or silence: " + profile.Id + "/" + key);
}
var eligibleVoices = fullVoiceCatalog.Profiles.Where(profile => profile.EligibleForAssignment).ToArray();
Check(eligibleVoices.Length == 31 && eligibleVoices.Count(profile => profile.Gender == "male") == 22 &&
    eligibleVoices.Count(profile => profile.Gender == "female") == 9,
    "curated pool includes BlackOps: 31 eligible profiles (22 male, 9 female), not all 56");
Check(fullVoiceCatalog.Profiles.Where(profile => !profile.EligibleForAssignment).Count() == 25 &&
    !fullVoiceCatalog.Find("bishopvp").EligibleForAssignment &&
    !fullVoiceCatalog.Find("fem_bishop_vp").EligibleForAssignment,
    "sparse profiles including missing selection audio stay compatibility-only");
var blackOpsVoice = fullVoiceCatalog.Find("trepang2blackops");
Check(blackOpsVoice != null && blackOpsVoice.EligibleForAssignment && blackOpsVoice.Routes.Count == 42 &&
    blackOpsVoice.Routes.Values.SelectMany(route => route.Assets).Distinct().Count() == 42,
    "BlackOps has 42 independently named recordings and remains eligible without padding");
Check(new[] { "ally_dead|0", "danger_close|0", "enemy_blocked|0", "enemy_fleeing|0",
    "enemy_hidden|0", "enemy_hidden|1", "suppress_fire|0", "suppressed|0" }
    .All(blackOpsVoice.Routes.ContainsKey), "BlackOps includes the eight curated event additions");
Check(new[] { "ready_ack_got_this|0", "taking_fire|2", "taking_fire|3", "success|1",
    "success|2", "success|3", "success|4", "ready_quiet|2" }
    .All(key => blackOpsVoice.ResolveRoute(key).IsSilent && blackOpsVoice.ResolveRoute(key).Subtitle == ""),
    "removed BlackOps duplicates and mismatched actions resolve to captionless silence");
var sparseCatalog = PromotionVoiceCatalog.Parse(voiceCatalogJson);
Check(!PromotionVoiceAssignment.TryAssignRotatingProfile(new PromotionLedger(),
    new PromotionRecord { Gender = 0 }, sparseCatalog),
    "no eligible voice returns false rather than selecting a sparse fallback");
// Give the rotation fixtures real eligible route shapes; the alphabetically first
// sparse profile deliberately remains in the catalog to catch unfiltered selection.
foreach (var profile in voiceCatalog.Profiles.Where(profile => profile.Id != "aaa_sparse"))
    profile.Routes = fullVoiceCatalog.Find("marcusvp").Routes.ToDictionary(pair => pair.Key,
        pair => new PromotionVoiceRoute { Subtitle = pair.Value.Subtitle,
            Assets = pair.Value.Assets.Select(asset => asset.Replace("xcom_marcusvp_", "xcom_" + profile.Id + "_")).ToArray() });
Check(aaronVoice.EligibleForAssignment, "complete route and distinct-clip fixture is eligible");
var originalReady = aaronVoice.Routes["ready|0"];
aaronVoice.Routes.Remove("ready|0");
aaronVoice.Routes["unknown_future_event|99"] = originalReady;
Check(!aaronVoice.EligibleForAssignment, "route count alone cannot hide a missing selection response");
aaronVoice.Routes.Remove("unknown_future_event|99");
aaronVoice.Routes["ready|0"] = originalReady;
var originalRoutes = aaronVoice.Routes;
aaronVoice.Routes = originalRoutes.ToDictionary(pair => pair.Key,
    pair => new PromotionVoiceRoute { Subtitle = pair.Value.Subtitle, Assets = originalReady.Assets });
Check(!aaronVoice.EligibleForAssignment, "42 routes reusing one clip do not count as recording variety");
aaronVoice.Routes = originalRoutes;
var savedSparseVoice = new PromotionRecord { Gender = 0, VoiceSetId = "aaa_sparse" };
PromotionVoiceAssignment.ValidateAssignment(savedSparseVoice, voiceCatalog);
Check(!PromotionVoiceAssignment.TryAssignRotatingProfile(new PromotionLedger(), savedSparseVoice, voiceCatalog) &&
    savedSparseVoice.VoiceSetId == "aaa_sparse" && voiceCatalog.Find(savedSparseVoice.VoiceSetId) != null,
    "retired profiles still validate and play for existing characters without changing saved identity");
foreach (int gender in new[] { 0, 1 })
{
    var curatedVoiceLedger = new PromotionLedger();
    int poolSize = eligibleVoices.Count(profile => profile.Gender == (gender == 0 ? "male" : "female"));
    for (int i = 0; i < poolSize * 2; i++)
    {
        var record = new PromotionRecord { Gender = gender };
        Check(PromotionVoiceAssignment.TryAssignRotatingProfile(curatedVoiceLedger, record, fullVoiceCatalog) &&
            fullVoiceCatalog.Find(record.VoiceSetId).EligibleForAssignment,
            "real catalog rotation never selects a retired profile");
        curatedVoiceLedger.Records.Add(record);
    }
    Check(curatedVoiceLedger.Records.GroupBy(record => record.VoiceSetId).Count() == poolSize &&
        curatedVoiceLedger.Records.GroupBy(record => record.VoiceSetId).All(group => group.Count() == 2),
        "curated rotation still uses every compatible profile before reuse");
}
PromotionRecord LegacyVoiceRecord(int index, int gender)
{
    var record = R(selection, index);
    var token = index.ToString("x32");
    record.LeaderId = PromotionRecord.LeaderPrefix + token;
    record.SpeakerId = PromotionRecord.SpeakerPrefix + token;
    record.NumericGuid = 20000 + index;
    record.SourceSquaddieId = 300 + index;
    record.Gender = gender;
    record.VoiceSetId = "";
    return record;
}
var voiceLedger = new PromotionLedger();
var voiceRecord = new PromotionRecord { Gender = 0 };
Check(PromotionVoiceAssignment.TryAssignRotatingProfile(voiceLedger, voiceRecord, voiceCatalog) &&
    voiceRecord.VoiceSetId == "aaron", "first promotion receives first compatible unused voice profile");
voiceLedger.Records.Add(voiceRecord);
var nextVoiceRecord = new PromotionRecord { Gender = 0 };
Check(PromotionVoiceAssignment.TryAssignRotatingProfile(voiceLedger, nextVoiceRecord, voiceCatalog) &&
    nextVoiceRecord.VoiceSetId == "beau", "voice rotation uses another unused profile before reuse");
voiceLedger.Records.Add(nextVoiceRecord);
var femaleVoiceRecord = new PromotionRecord { Gender = 1 };
Check(PromotionVoiceAssignment.TryAssignRotatingProfile(voiceLedger, femaleVoiceRecord, voiceCatalog) &&
    femaleVoiceRecord.VoiceSetId == "bella", "voice selection is constrained to the leader's gender");
PromotionVoiceAssignment.ValidateAssignment(femaleVoiceRecord, voiceCatalog);
try
{
    PromotionVoiceAssignment.ValidateAssignment(new PromotionRecord { Gender = 1, VoiceSetId = "aaron" }, voiceCatalog);
    throw new Exception("FAILED: mismatched voice profile gender was accepted");
}
catch (InvalidDataException) { Check(true, "saved voice assignments reject gender mismatches"); }
var legacyVoiceLedger = new PromotionLedger();
var previouslyAssignedVoice = LegacyVoiceRecord(0, 0);
previouslyAssignedVoice.VoiceSetId = "aaron";
var legacyMaleVoice1 = LegacyVoiceRecord(1, 0);
var legacyMaleVoice2 = LegacyVoiceRecord(2, 0);
var legacyFemaleVoice = LegacyVoiceRecord(3, 1);
legacyVoiceLedger.Records.AddRange(new[]
    { previouslyAssignedVoice, legacyMaleVoice1, legacyMaleVoice2, legacyFemaleVoice });
Check(PromotionVoiceAssignment.AssignMissingProfiles(legacyVoiceLedger, voiceCatalog) == 3 &&
    legacyMaleVoice1.VoiceSetId == "beau" && legacyMaleVoice2.VoiceSetId == "aaron" &&
    legacyFemaleVoice.VoiceSetId == "bella" && previouslyAssignedVoice.VoiceSetId == "aaron",
    "legacy voice fallbacks receive stable gender-matched profiles without changing existing assignments");
Check(PromotionVoiceAssignment.AssignMissingProfiles(legacyVoiceLedger, voiceCatalog) == 0 &&
    legacyMaleVoice1.VoiceSetId == "beau" && legacyMaleVoice2.VoiceSetId == "aaron",
    "legacy runtime voice assignment does not reroll on repeated rehydration");
Reject(() => PromotionVoiceCatalog.Parse(voiceCatalogJson.Replace("\"kind\":\"human\"", "\"kind\":\"robot\"")),
    "catalog rejects non-human voice profiles");
Check(PromotionRules.Describe(selection, R(selection)).Contains("Incomplete XP coverage"), "partial-coverage reason visible");
var excluded = PromotionRules.Choose(new[] { C(1, 2, 999), C(2, 3), C(3, xp: 5) }, new HashSet<int> { 2 });
Check(excluded.Soldier.Id == 1 && excluded.UsesXp && excluded.LivingCount == 2 && excluded.ExcludedCount == 1,
    "already promoted soldiers are skipped before ranking and XP coverage");
Check(PromotionRules.Choose(new[] { C(1), C(2) }, new HashSet<int> { 1, 2 }) == null, "all promoted means no candidate");
Reject(() => PromotionRules.Choose(new[] { C(1), C(1) }, new HashSet<int> { 1 }), "excluded entries are still validated");
Check(PromotionRules.Choose(new[] { C(5, xp: 0), C(1, xp: 0) }).UsesXp, "zero XP is valid");
Reject(() => PromotionRules.Choose(new[] { C(1), C(1) }), "duplicate native ID");
Reject(() => PromotionRules.Choose(new[] { C(-1) }), "negative native ID");
Reject(() => PromotionRules.Choose(new[] { C(1, -1) }), "invalid mission count");
Reject(() => PromotionRules.Choose(new[] { C(1, xp: -1) }), "invalid XP snapshot");
Check(PromotionIdentity.TitleFor(0, "81st Astral Assault").StartsWith("81st Astral Assault - Imperius Section "),
    "promotion title inherits the squaddie's unit identity");
Check(!PromotionIdentity.TitleFor(0, "15th Vanguard Division").Contains("15th Vanguard Division") &&
    PromotionIdentity.TitleFor(0, "") != PromotionIdentity.TitleFor(1, ""), "generic title fallback is varied and not the vanilla placeholder");
Check(PromotionProgression.GenericInfantryPerks.Length == 16 &&
    PromotionProgression.GenericInfantryPerks.Count(perk => perk.Tier == 1) == 5 &&
    PromotionProgression.GenericInfantryPerks.Count(perk => perk.Tier == 2) == 5 &&
    PromotionProgression.GenericInfantryPerks.Count(perk => perk.Tier == 3) == 4 &&
    PromotionProgression.GenericInfantryPerks.Count(perk => perk.Tier == 4) == 2,
    "generic promotion tree has all 16 perks in the chosen tier distribution");
Check(JsonSerializer.Deserialize<PromotionRecord>("{}").SourceLeaderTemplateId == "",
    "older save sidecars without provenance default to unavailable");
Check(JsonSerializer.Deserialize<PromotionRecord>("{}").PortraitProfile == "",
    "older save sidecars without portrait assignments keep default artwork");
var legacyPerkJson = JsonSerializer.Deserialize<PromotionRecord>(
    JsonSerializer.Serialize(R(selection)).Replace("\"PerkSchemaVersion\":1", "\"PerkSchemaVersion\":0"));
Check(JsonSerializer.Deserialize<PromotionRecord>("{}").PerkSchemaVersion == 0, "pre-0.15 sidecars default to the shared generic tree");
legacyPerkJson.GeneratedPerks = null; legacyPerkJson.InitialPerkId = null;
legacyPerkJson.Validate();
Check(true, "legacy records without a perk snapshot remain loadable");
PromotionRecord PerkRecord(int? xp, MentorSnapshot mentor = null, string leaderId = null)
{
    var record = R(new Selection(C(950, xp: xp), 1, xp.HasValue, false));
    if (leaderId != null) record.LeaderId = leaderId;
    PerkGeneration.Generate(record, xp, mentor);
    return record;
}
foreach (int? xp in new int?[] { null, 0, 249, 250, 499, 500, 999, 1000, 1999, 2000, 3999, 4000, 7999, 8000, int.MaxValue })
{
    var perks = PerkRecord(xp);
    var tiers = perks.GeneratedPerks.Select(p => p.Tier).ToArray();
    Check(perks.PerkSchemaVersion == PerkGeneration.Schema && perks.GeneratedPerks.Count == 16 &&
        tiers.Count(t => t == 1) == 5 && tiers.Count(t => t == 2) == 5 && tiers.Count(t => t == 3) == 4 &&
        tiers.Count(t => t == 4) == 2 && tiers.SequenceEqual(tiers.OrderBy(t => t)) &&
        perks.GeneratedPerks.Select(p => p.Id).Distinct().Count() == 16 &&
        perks.InitialPerkId == PromotionProgression.InitialPerkId &&
        perks.GeneratedPerks.All(p => p.Id != perks.InitialPerkId) && perks.XpAtPromotion == xp,
        "generated tree 5/5/4/2 without duplicates, accepted generic initial perk, XP " + (xp?.ToString() ?? "unknown"));
}
int AdvancedCount(int? xp) => PerkRecord(xp).GeneratedPerks.Count(p => p.Origin == PerkGeneration.OriginAdvanced);
Check(AdvancedCount(null) == 0 && AdvancedCount(249) == 0, "unknown XP and Green use only the base pool");
Check(AdvancedCount(250) == 1 && AdvancedCount(500) == 2 && AdvancedCount(1000) == 3 &&
    AdvancedCount(2000) == 6 && AdvancedCount(4000) == 7 && AdvancedCount(8000) == 8,
    "advanced options grow with each Veterancy XP band");
var stableId = PromotionRecord.LeaderPrefix + new string('a', 32);
var firstTree = PerkRecord(2000, null, stableId).GeneratedPerks.Select(p => p.Id + ":" + p.Tier);
var secondTree = PerkRecord(2000, null, stableId).GeneratedPerks.Select(p => p.Id + ":" + p.Tier);
Check(firstTree.SequenceEqual(secondTree), "same leader identity always produces the same tree");
Check(!firstTree.SequenceEqual(PerkRecord(2000, null, PromotionRecord.LeaderPrefix + new string('b', 32))
    .GeneratedPerks.Select(p => p.Id + ":" + p.Tier)), "different promotions get different trees");
var mentor = new MentorSnapshot("squad_leader.lim",
    new[] { "perk.stubborn", "perk.determined", "perk.fortify", "perk.power_in_numbers", "perk.command_rally", "perk.firing_line" });
int MentorCount(int? xp, MentorSnapshot m) => PerkRecord(xp, m).GeneratedPerks.Count(p => p.Origin == PerkGeneration.OriginMentor);
Check(MentorCount(0, mentor) == 0 && MentorCount(null, mentor) == 0, "Green or unknown XP inherits no mentor perks");
Check(MentorCount(250, mentor) == 1 && MentorCount(2000, mentor) == 2 && MentorCount(8000, mentor) == 2,
    "mentor inheritance is capped at one option, two from Veteran");
var mentored = PerkRecord(8000, mentor);
Check(mentored.MentorLeaderTemplateId == "squad_leader.lim" &&
    mentored.GeneratedPerks.Where(p => p.Origin == PerkGeneration.OriginMentor)
        .All(p => PerkGeneration.MentorTiers.TryGetValue(p.Id, out int tier) && tier == p.Tier) &&
    !mentored.GeneratedPerks.Any(p => p.Id == "perk.power_in_numbers" || p.Id == "perk.command_rally"),
    "mentor perks keep their tier and unique/command perks are never inherited");
Check(PerkRecord(500, new MentorSnapshot("squad_leader.lim", new[] { "perk.fortify" }))
    .GeneratedPerks.All(p => p.Origin != PerkGeneration.OriginMentor), "tier-3 mentor perks need Veteran XP");
var roundTrip = JsonSerializer.Deserialize<PromotionRecord>(JsonSerializer.Serialize(mentored));
roundTrip.Validate();
Check(roundTrip.GeneratedPerks.Select(p => p.Id + p.Tier + p.Origin)
    .SequenceEqual(mentored.GeneratedPerks.Select(p => p.Id + p.Tier + p.Origin)) &&
    roundTrip.XpAtPromotion == 8000, "perk snapshot survives the save sidecar unchanged");
void RejectPerks(Action<PromotionRecord> corrupt, string name)
{
    var bad = JsonSerializer.Deserialize<PromotionRecord>(JsonSerializer.Serialize(mentored));
    corrupt(bad);
    Reject(() => bad.Validate(), name);
}
RejectPerks(r => r.PerkSchemaVersion = 2, "unknown perk schema rejected");
RejectPerks(r => r.GeneratedPerks.RemoveAt(0), "missing perk entry rejected");
RejectPerks(r => r.GeneratedPerks[1].Id = r.GeneratedPerks[0].Id, "duplicate perk rejected");
RejectPerks(r => r.GeneratedPerks[0].Id = r.InitialPerkId, "initial perk duplicated in tree rejected");
RejectPerks(r => r.GeneratedPerks[0].Tier = 4, "out-of-order tier rejected");
RejectPerks(r => r.GeneratedPerks[0].Origin = "Gift", "unknown origin rejected");
RejectPerks(r => r.GeneratedPerks[0].Id = "skill.x", "non-perk ID rejected");
RejectPerks(r => r.InitialPerkId = "", "missing initial perk rejected");
RejectPerks(r => r.XpAtPromotion = -1, "negative XP snapshot rejected");
RejectPerks(r => r.GeneratedPerks = null, "missing snapshot list rejected");
Check(PromotionRules.Describe(selection, mentored).Contains("Personal perk tree: 16 options"), "promotion report shows the personal tree");

PromotionRecord PortraitCandidate(int id, int gender, int skinColor) =>
    R(new Selection(C(id, gender: gender, skinColor: skinColor), 1, false, false));
var portraitLedger = new PromotionLedger();
var femaleBrownPortrait = PortraitCandidate(910, 1, 1);
var firstBrownMalePortrait = PortraitCandidate(912, 0, 1);
var whiteMalePortrait = PortraitCandidate(911, 0, 0);
var blackMalePortrait = PortraitCandidate(917, 0, 2);
var blackFemalePortrait = PortraitCandidate(921, 1, 2);
var whiteFemalePortrait = PortraitCandidate(922, 1, 0);
femaleBrownPortrait.BadgeIndex = 0;
firstBrownMalePortrait.BadgeIndex = 1;
blackMalePortrait.BadgeIndex = 2;
blackFemalePortrait.BadgeIndex = 3;
whiteMalePortrait.BadgeIndex = 4;
whiteFemalePortrait.BadgeIndex = 5;
void AssignsPortrait(PromotionRecord record, string expected, string name)
{
    Check(PromotionPortraitAssignment.TryAssignRotatingProfile(portraitLedger, record) &&
        record.PortraitProfile == expected, name);
    portraitLedger.Records.Add(record);
}
AssignsPortrait(femaleBrownPortrait, PromotionPortraitAssignment.Female01, "female/Brown promotion receives promoted_female_01");
AssignsPortrait(firstBrownMalePortrait, PromotionPortraitAssignment.MaleBrown01, "male/Brown promotion receives promoted_male_01");
AssignsPortrait(blackMalePortrait, PromotionPortraitAssignment.MaleBlack02, "male/Black promotion receives promoted_male_02");
AssignsPortrait(blackFemalePortrait, PromotionPortraitAssignment.FemaleBlack02, "female/Black promotion receives promoted_female_02");
AssignsPortrait(whiteMalePortrait, PromotionPortraitAssignment.MaleWhite03, "male/White promotion receives promoted_male_03");
AssignsPortrait(whiteFemalePortrait, PromotionPortraitAssignment.FemaleWhite03, "female/White promotion receives promoted_female_03");
var secondPortraits = new[]
{
    (PortraitCandidate(918, 1, 1), PromotionPortraitAssignment.FemaleBrown04),
    (PortraitCandidate(913, 0, 1), PromotionPortraitAssignment.MaleBrown04),
    (PortraitCandidate(919, 0, 2), PromotionPortraitAssignment.MaleBlack05),
    (PortraitCandidate(927, 1, 2), PromotionPortraitAssignment.FemaleBlack05),
    (PortraitCandidate(928, 0, 0), PromotionPortraitAssignment.MaleWhite07),
    (PortraitCandidate(925, 1, 0), PromotionPortraitAssignment.FemaleWhite07),
};
var thirdPortraits = new[]
{
    (PortraitCandidate(932, 1, 1), PromotionPortraitAssignment.FemaleBrown06),
    (PortraitCandidate(933, 0, 1), PromotionPortraitAssignment.MaleBrown06),
    (PortraitCandidate(934, 0, 2), PromotionPortraitAssignment.MaleBlack08),
    (PortraitCandidate(935, 1, 2), PromotionPortraitAssignment.FemaleBlack08),
    (PortraitCandidate(936, 0, 0), PromotionPortraitAssignment.MaleWhite09),
    (PortraitCandidate(937, 1, 0), PromotionPortraitAssignment.FemaleWhite09),
};
var fourthPortraits = new[]
{
    (PortraitCandidate(938, 1, 1), PromotionPortraitAssignment.FemaleBrown10),
    (PortraitCandidate(939, 0, 1), PromotionPortraitAssignment.MaleBrown10),
    (PortraitCandidate(940, 0, 2), PromotionPortraitAssignment.MaleBlack12),
    (PortraitCandidate(941, 1, 2), PromotionPortraitAssignment.FemaleBlack12),
    (PortraitCandidate(942, 0, 0), PromotionPortraitAssignment.MaleWhite11),
    (PortraitCandidate(943, 1, 0), PromotionPortraitAssignment.FemaleWhite11),
};
var fifthPortraits = new[]
{
    (PortraitCandidate(944, 1, 1), PromotionPortraitAssignment.Female01),
    (PortraitCandidate(945, 0, 1), PromotionPortraitAssignment.MaleBrown01),
    (PortraitCandidate(946, 0, 2), PromotionPortraitAssignment.MaleBlack02),
    (PortraitCandidate(947, 1, 2), PromotionPortraitAssignment.FemaleBlack02),
    (PortraitCandidate(948, 0, 0), PromotionPortraitAssignment.MaleWhite03),
    (PortraitCandidate(949, 1, 0), PromotionPortraitAssignment.FemaleWhite03),
};
var sixthPortraits = new[]
{
    (PortraitCandidate(956, 1, 1), PromotionPortraitAssignment.FemaleBrown04),
    (PortraitCandidate(957, 0, 1), PromotionPortraitAssignment.MaleBrown04),
    (PortraitCandidate(958, 0, 2), PromotionPortraitAssignment.MaleBlack05),
    (PortraitCandidate(959, 1, 2), PromotionPortraitAssignment.FemaleBlack05),
    (PortraitCandidate(960, 0, 0), PromotionPortraitAssignment.MaleWhite07),
    (PortraitCandidate(961, 1, 0), PromotionPortraitAssignment.FemaleWhite07),
};
for (int i = 0; i < secondPortraits.Length; i++)
{
    secondPortraits[i].Item1.BadgeIndex = 40 + i;
    AssignsPortrait(secondPortraits[i].Item1, secondPortraits[i].Item2,
        "second promotion in a category gets its second set: " + secondPortraits[i].Item2);
}
for (int i = 0; i < thirdPortraits.Length; i++)
{
    thirdPortraits[i].Item1.BadgeIndex = 50 + i;
    AssignsPortrait(thirdPortraits[i].Item1, thirdPortraits[i].Item2,
        "third promotion gets its category's third set: " + thirdPortraits[i].Item2);
}
for (int i = 0; i < fourthPortraits.Length; i++)
{
    fourthPortraits[i].Item1.BadgeIndex = 60 + i;
    AssignsPortrait(fourthPortraits[i].Item1, fourthPortraits[i].Item2,
        "fourth promotion gets its category's fourth set: " + fourthPortraits[i].Item2);
}
for (int i = 0; i < fifthPortraits.Length; i++)
{
    fifthPortraits[i].Item1.BadgeIndex = 70 + i;
    AssignsPortrait(fifthPortraits[i].Item1, fifthPortraits[i].Item2,
        "fifth promotion cycles to the least-used set: " + fifthPortraits[i].Item2);
}
for (int i = 0; i < sixthPortraits.Length; i++)
{
    sixthPortraits[i].Item1.BadgeIndex = 80 + i;
    AssignsPortrait(sixthPortraits[i].Item1, sixthPortraits[i].Item2,
        "sixth promotion gets its category's second set: " + sixthPortraits[i].Item2);
}
Check(portraitLedger.Validated().Count == 36, "a save may contain repeated portrait sets across many leaders");
Check(PromotionPortraitAssignment.AllProfiles.Count() == 24 &&
    PromotionPortraitAssignment.AllProfiles.Distinct().Count() == 24 &&
    new[] { (0, 0, 4), (0, 1, 4), (0, 2, 4), (1, 0, 4), (1, 1, 4), (1, 2, 4) }
        .All(c => PromotionPortraitAssignment.ProfilesFor(c.Item1, c.Item2).Length == c.Item3),
    "twenty-four portrait sets: four per native gender/skin category");
foreach (string profile in PromotionPortraitAssignment.AllProfiles)
{
    int owners = new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) }
        .Count(c => PromotionPortraitAssignment.ProfilesFor(c.Item1, c.Item2).Contains(profile));
    Check(owners == 1, "portrait set belongs to exactly one category: " + profile);
}
var rotationPool = new[] { "set_a", "set_b", "set_c" };
var rotationLedger = new List<PromotionRecord>();
var rotationPicks = new List<string>();
for (int i = 0; i < 7; i++)
{
    string pick = PromotionPortraitAssignment.LeastUsed(rotationPool, rotationLedger);
    rotationPicks.Add(pick);
    rotationLedger.Add(new PromotionRecord { PortraitProfile = pick });
}
Check(rotationPicks.SequenceEqual(new[] { "set_a", "set_b", "set_c", "set_a", "set_b", "set_c", "set_a" }),
    "multiple sets in a category rotate least-used first");
Check(PromotionPortraitAssignment.LeastUsed(rotationPool,
        new[] { new PromotionRecord { PortraitProfile = "set_a" }, new PromotionRecord { PortraitProfile = "set_c" } }) == "set_b",
    "rotation fills the least-used set after loading an uneven save");
Check(PromotionPortraitAssignment.LeastUsed(Array.Empty<string>(), rotationLedger) == null,
    "an empty category keeps the default artwork");
Check(JsonSerializer.Deserialize<PromotionRecord>(JsonSerializer.Serialize(firstBrownMalePortrait)).PortraitProfile ==
    PromotionPortraitAssignment.MaleBrown01, "portrait assignment persists in the save record");
Check(JsonSerializer.Deserialize<PromotionRecord>(JsonSerializer.Serialize(whiteFemalePortrait)).PortraitProfile ==
    PromotionPortraitAssignment.FemaleWhite03, "female_03 persists in save records");
var duplicatePortrait = PortraitCandidate(914, 0, 1);
duplicatePortrait.BadgeIndex = 6;
duplicatePortrait.PortraitProfile = PromotionPortraitAssignment.MaleBrown01;
Check(new PromotionLedger { Records = new() { firstBrownMalePortrait, duplicatePortrait } }.Validated().Count == 2,
    "0.14.x sidecars and rotated saves with a repeated portrait set both load");
var legacyWhitePortrait = PortraitCandidate(915, 0, 0);
legacyWhitePortrait.PortraitProfile = PromotionPortraitAssignment.MaleBrown01;
Check(new PromotionLedger { Records = new() { legacyWhitePortrait } }.Validated().Single() == legacyWhitePortrait,
    "0.14.0 male White portrait assignment remains loadable");
var afterLegacyWhite = PortraitCandidate(929, 0, 1);
Check(PromotionPortraitAssignment.TryAssignRotatingProfile(
        new PromotionLedger { Records = new() { legacyWhitePortrait } }, afterLegacyWhite) &&
    afterLegacyWhite.PortraitProfile == PromotionPortraitAssignment.MaleBrown04 &&
    legacyWhitePortrait.PortraitProfile == PromotionPortraitAssignment.MaleBrown01,
    "legacy male_01 assignment is kept and counts as a use, so the next male/Brown leader gets male_04");
var legacyFemaleWhitePortrait = PortraitCandidate(923, 1, 0);
legacyFemaleWhitePortrait.PortraitProfile = PromotionPortraitAssignment.Female01;
Check(new PromotionLedger { Records = new() { legacyFemaleWhitePortrait } }.Validated().Single() == legacyFemaleWhitePortrait,
    "0.14.3 female White portrait assignment remains loadable");
var afterLegacyFemale = PortraitCandidate(930, 1, 0);
Check(PromotionPortraitAssignment.TryAssignRotatingProfile(
        new PromotionLedger { Records = new() { legacyFemaleWhitePortrait } }, afterLegacyFemale) &&
    afterLegacyFemale.PortraitProfile == PromotionPortraitAssignment.FemaleWhite03 &&
    legacyFemaleWhitePortrait.PortraitProfile == PromotionPortraitAssignment.Female01,
    "legacy female_01 assignment is kept; new female/White leaders get female_03");
var previousMaleWhite06 = PortraitCandidate(950, 0, 0);
previousMaleWhite06.BadgeIndex = 80;
previousMaleWhite06.PortraitProfile = PromotionPortraitAssignment.MaleBrown06;
var previousFemaleWhite06 = PortraitCandidate(951, 1, 0);
previousFemaleWhite06.BadgeIndex = 81;
previousFemaleWhite06.PortraitProfile = PromotionPortraitAssignment.FemaleBrown06;
Check(new PromotionLedger { Records = new() { previousMaleWhite06, previousFemaleWhite06 } }.Validated().Count == 2,
    "0.15.3/0.15.4 White assignments of set 06 remain loadable after its category changes");
var maleWhiteAfterLegacy06 = PortraitCandidate(952, 0, 0);
Check(PromotionPortraitAssignment.TryAssignRotatingProfile(
        new PromotionLedger { Records = new() { previousMaleWhite06 } }, maleWhiteAfterLegacy06) &&
    maleWhiteAfterLegacy06.PortraitProfile == PromotionPortraitAssignment.MaleWhite03 &&
    previousMaleWhite06.PortraitProfile == PromotionPortraitAssignment.MaleBrown06,
    "old White 06 portrait remains saved while new White leaders use the corrected pool");
var alreadyAssigned = PortraitCandidate(931, 0, 2);
alreadyAssigned.PortraitProfile = PromotionPortraitAssignment.MaleBlack02;
Check(!PromotionPortraitAssignment.TryAssignRotatingProfile(new PromotionLedger(), alreadyAssigned) &&
    alreadyAssigned.PortraitProfile == PromotionPortraitAssignment.MaleBlack02,
    "an existing portrait assignment is never replaced");
var legacyFemaleBlackPortrait = PortraitCandidate(924, 1, 2);
legacyFemaleBlackPortrait.PortraitProfile = PromotionPortraitAssignment.Female01;
Check(new PromotionLedger { Records = new() { legacyFemaleBlackPortrait } }.Validated().Single() == legacyFemaleBlackPortrait,
    "0.14.3 female Black portrait assignment remains loadable");
var ineligiblePortrait = PortraitCandidate(916, 1, 1);
ineligiblePortrait.PortraitProfile = PromotionPortraitAssignment.MaleBrown01;
Reject(() => ineligiblePortrait.Validate(), "male_01 cannot be stored on another gender");
var wrongSkinPortrait = PortraitCandidate(920, 0, 1);
wrongSkinPortrait.PortraitProfile = PromotionPortraitAssignment.MaleBlack02;
Reject(() => wrongSkinPortrait.Validate(), "male_02 requires the native Black skin color");
var wrongFemaleSkinPortrait = PortraitCandidate(926, 1, 2);
wrongFemaleSkinPortrait.PortraitProfile = PromotionPortraitAssignment.FemaleWhite03;
Reject(() => wrongFemaleSkinPortrait.Validate(), "female_03 requires the native White skin color");
var provenanceCandidate = C(703, sourceLeaderTemplateId: "squad_leader.weiwu");
var provenanceRecord = LeaderForge.CreateRecord(null, new Selection(provenanceCandidate, 1, false, false),
    2, null, PromotionProgression.RootSourceTemplateId(provenanceCandidate.SourceLeaderTemplateId, new PromotionLedger()));
Check(provenanceRecord.SourceLeaderTemplateId == "squad_leader.weiwu",
    "new promotion records retain a safe source leader template ID");
var rootSourceRecord = R(new Selection(C(701), 1, false, false));
rootSourceRecord.SourceLeaderTemplateId = "squad_leader.lim";
var chainedSourceRecord = R(new Selection(C(702), 1, false, false), badge: 1);
chainedSourceRecord.SourceLeaderTemplateId = rootSourceRecord.LeaderId;
var provenanceLedger = new PromotionLedger { Records = new() { rootSourceRecord, chainedSourceRecord } };
Check(PromotionProgression.RootSourceTemplateId(chainedSourceRecord.LeaderId, provenanceLedger) == "squad_leader.lim" &&
    PromotionProgression.RootSourceTemplateId("squad_leader.weiwu", provenanceLedger) == "squad_leader.weiwu" &&
    PromotionProgression.RootSourceTemplateId(PromotionRecord.LeaderPrefix + new string('a', 32), provenanceLedger) == "",
    "nested promotions keep their original source and unknown old sources are not guessed");
rootSourceRecord.SourceLeaderTemplateId = chainedSourceRecord.LeaderId;
Check(PromotionProgression.RootSourceTemplateId(rootSourceRecord.LeaderId, provenanceLedger) == "",
    "cyclic source provenance is rejected safely");
var invalidSourceRecord = R(new Selection(C(704), 1, false, false));
invalidSourceRecord.SourceLeaderTemplateId = "bad\nsource";
Reject(() => invalidSourceRecord.Validate(), "control characters are rejected from source template IDs");
var serviceLedger = new PromotionLedger();
var firstMission = new MissionServiceSnapshot("operation.1:0:100", new[]
{
    new MissionServiceGroup("unit.81", "81st Astral Assault", "Lim", new[] { 101, 102 }, new[] { 101 }),
});
Check(SquaddieServiceHistory.Record(serviceLedger, firstMission), "first mission service record accepted");
Check(!SquaddieServiceHistory.Record(serviceLedger, firstMission), "duplicate mission callback does not double-count service");
Check(SquaddieServiceHistory.Record(serviceLedger, new MissionServiceSnapshot("operation.2:1:101", new[]
{
    new MissionServiceGroup("unit.81", "81st Astral Assault", "Lim", new[] { 101 }, Array.Empty<int>()),
})), "second mission service record accepted");
Check(SquaddieServiceHistory.Record(serviceLedger, new MissionServiceSnapshot("operation.3:2:102", new[]
{
    new MissionServiceGroup("unit.12", "12th Void Company", "Edwin Pike", new[] { 101 }, new[] { 101 }),
})), "service across different units accepted");
var recordedService = serviceLedger.FindServiceRecord(101);
Check(recordedService.Units.Count == 2 && recordedService.Units.Sum(unit => unit.MissionsServed) == 3 &&
    recordedService.Units.Sum(unit => unit.WoundedMissions) == 2, "unit missions and wounds are attributed independently");
var serviceCandidate = C(101, missions: 5, lastUnitTitle: "81st Astral Assault", lastCommander: "Lim", rank: "Hardened");
var serviceRecord = R(new Selection(serviceCandidate, 1, false, false), 0, recordedService);
Check(serviceRecord.UnitTitle.Contains("81st Astral Assault") &&
    serviceRecord.UnitDescription.Contains("81st Astral Assault under Lim") &&
    serviceRecord.UnitDescription.Contains("Wounded in 2 recorded mission(s)") &&
    serviceRecord.UnitDescription.Contains("wounded in 1 mission(s)") &&
    serviceRecord.UnitDescription.Contains("Hardened") && serviceRecord.UnitDescription.Contains("\n- "),
    "promoted leader description carries readable service and veterancy history");
Reject(() => SquaddieServiceHistory.Record(new PromotionLedger(), new MissionServiceSnapshot("ambiguous", new[]
{
    new MissionServiceGroup("unit.a", "Unit A", "", new[] { 201 }, Array.Empty<int>()),
    new MissionServiceGroup("unit.b", "Unit B", "", new[] { 201 }, Array.Empty<int>()),
})), "one squaddie cannot be attributed to two units in one mission");
Reject(() => SquaddieServiceHistory.Record(new PromotionLedger(), new MissionServiceSnapshot("orphan-wound", new[]
{
    new MissionServiceGroup("unit.a", "Unit A", "", new[] { 201 }, new[] { 202 }),
})), "wounded IDs must belong to the deployed unit");
var baselineAttributes = new byte[] { 65, 65, 62, 50, 50, 5, 45 };
foreach (var (xp, bonus) in new[]
{
    (0,0), (249,0), (250,2), (499,2), (500,4), (999,4), (1000,6),
    (1999,6), (2000,8), (3999,8), (4000,10), (7999,10), (8000,12), (int.MaxValue,12)
})
{
    var attributes = PromotionRules.Attributes(xp);
    var expected = baselineAttributes.Select((value, index) =>
        (byte)(value + (index == 5 ? bonus / 2 : bonus))).ToArray();
    Check(attributes.SequenceEqual(expected), "rank-aligned XP tier " + xp);
}
var defaults = PromotionRules.Attributes(null);
Check(defaults.SequenceEqual(baselineAttributes), "unknown XP uses baseline attributes");
defaults[0] = 0;
Check(PromotionRules.Attributes(null).SequenceEqual(baselineAttributes), "attributes never shared");
Check(PromotionRules.Describe(null, null).Contains("No leader or soldier was changed"), "empty result is explicitly non-mutating");
Check(PromotionRules.Describe(selection, R(selection)).Contains("65 / 65 / 62"), "partial XP mode uses baseline attributes");
Check(PromotionRules.Describe(excluded, R(excluded)).Contains("1 already promoted skipped"), "skipped promoted soldiers reported");

// Ledger / record validation (managed values only).
var ledger = new PromotionLedger();
Check(ledger.Validated().Count == 0 && ledger.NextBadgeIndex() == 0 && ledger.PromotedSquaddieIds().Count == 0, "empty ledger");
var legacyRecord = R(selection);
legacyRecord.UnitTitle = "";
legacyRecord.UnitDescription = "";
var legacyLedger = new PromotionLedger { Version = 1, Records = new() { legacyRecord } };
Check(legacyLedger.Validated().Count == 1 && legacyLedger.Version == PromotionLedger.CurrentVersion &&
    legacyRecord.UnitTitle.StartsWith("TCRN Imperius") && legacyRecord.UnitDescription.Contains("not recorded"),
    "0.10.0 promotion ledger migrates with stable title and history fallback");
var textRecord = R(selection);
var donorTitle = new LocalizedLine(LocaCategory.Items, "UnitTitle", false);
donorTitle.SetDefaultTranslation("15th Vanguard Division");
var donorUnitDescription = new LocalizedMultiLine(LocaCategory.Items, "UnitDescription", false);
donorUnitDescription.SetDefaultTranslation("Source unit description");
var donorSpeakerDescription = new LocalizedMultiLine(LocaCategory.Items, "Description", false);
donorSpeakerDescription.SetDefaultTranslation("Source speaker description");
var textSource = new UnitLeaderTemplate { UnitTitle = donorTitle, UnitDescription = donorUnitDescription };
textSource.Badge = new UnityEngine.Sprite();
textSource.BadgeWhite = new UnityEngine.Sprite();
var speakerDonor = new SpeakerTemplate { Description = donorSpeakerDescription };
var cloneSpeakerDescription = new LocalizedMultiLine(LocaCategory.Items, "Description", false);
cloneSpeakerDescription.SetDefaultTranslation("Cloned source description");
var textClone = new UnitLeaderTemplate
{
    Id = textRecord.LeaderId,
    InfantryUnitTemplate = textSource,
    UnitTitle = donorTitle,
    UnitDescription = donorUnitDescription,
    SpeakerTemplate = new SpeakerTemplate { Description = cloneSpeakerDescription },
};
PromotionTemplateText.Apply(textRecord, textClone, textSource,
    new UnitLeaderTemplate { SpeakerTemplate = speakerDonor });
Check(textClone.UnitTitle.GetRawDefaultTranslation() == textRecord.UnitTitle && textClone.UnitTitle.Id == textRecord.LeaderId &&
    textClone.UnitDescription.GetRawDefaultTranslation() == textRecord.UnitDescription &&
    textClone.UnitDescription.Id == textRecord.LeaderId &&
    textClone.SpeakerTemplate.Description.GetRawDefaultTranslation() == textRecord.UnitDescription &&
    textClone.SpeakerTemplate.Description.Id == textRecord.SpeakerId,
    "promoted title and both native description fields use saved identity text");
Check(donorTitle.GetRawDefaultTranslation() == "15th Vanguard Division" &&
    donorUnitDescription.GetRawDefaultTranslation() == "Source unit description" &&
    donorSpeakerDescription.GetRawDefaultTranslation() == "Source speaker description",
    "promotion text does not mutate native donors");
Check(textClone.GetUnitTitle() == donorTitle && textClone.GetUnitDescription() == donorUnitDescription,
    "native infantry getters bypass rewritten leader fields and return shared entity text");
Check(textClone.GetBadge() == textSource.Badge && textClone.GetBadgeWhite() == textSource.BadgeWhite,
    "native infantry badge getters reproduce the Singh tactical icon regression");
try
{
    PromotionTemplateText.VerifyDisplay(textRecord, textClone);
    throw new Exception("FAILED: display verification accepted inherited infantry text");
}
catch (InvalidOperationException) { Check(true, "display readback catches the 0.11.1 regression before patching"); }
ledger.Records.Add(R(selection, 0));
ledger.Records.Add(R(excluded, 1));
Check(ledger.NextBadgeIndex() == 2 && ledger.PromotedSquaddieIds().SetEquals(new[] { 2, 1 }), "ledger exposes promoted IDs and next badge");
void RejectLedger(Action<PromotionRecord> corrupt, string name)
{
    var copy = new PromotionLedger();
    foreach (var r in ledger.Records) copy.Records.Add(r);
    var bad = R(selection, 7); corrupt(bad); copy.Records.Add(bad);
    Reject(() => copy.Validated(), name);
}
RejectLedger(r => r.LeaderId = "squad_leader.sachin", "vanilla leader ID rejected");
RejectLedger(r => r.SpeakerId = PromotionRecord.SpeakerPrefix + new string('0', 32), "speaker/leader token mismatch");
RejectLedger(r => r.NumericGuid = 0, "zero numeric GUID");
RejectLedger(r => r.NumericGuid = ledger.Records[0].NumericGuid, "duplicate numeric GUID");
RejectLedger(r => r.BadgeIndex = 1, "duplicate badge");
RejectLedger(r => r.SourceSquaddieId = 2, "same soldier promoted twice");
RejectLedger(r => r.Gender = 2, "invalid gender");
RejectLedger(r => r.VoicePitchPreset = VoicePitchProfiles.Count, "invalid voice pitch preset");
RejectLedger(r => r.InitialAttributes = new byte[] { 1, 2 }, "invalid attribute vector");
RejectLedger(r => r.InitialAttributes[0] = 101, "attribute above 100");
RejectLedger(r => r.Name = "", "empty name");
Reject(() => new PromotionLedger { Version = 99 }.Validated(), "unknown ledger version");
ledger.ServiceRecords.AddRange(serviceLedger.ServiceRecords);
ledger.ProcessedMissionKeys.AddRange(serviceLedger.ProcessedMissionKeys);
// Jiangyu sidecar layout: { "<type full name>": blob }. Must round-trip through ReadLedger.
var sidecarJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = ledger },
    new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
var reread = PromotionSystem.ReadLedger(sidecarJson);
Check(reread.Validated().Count == 2 && reread.Records[1].LeaderId == ledger.Records[1].LeaderId &&
    reread.Records[1].InitialAttributes.SequenceEqual(ledger.Records[1].InitialAttributes) &&
    reread.Records[1].VoicePitchPreset == ledger.Records[1].VoicePitchPreset &&
    reread.ServiceRecords.Count == serviceLedger.ServiceRecords.Count &&
    reread.ProcessedMissionKeys.SequenceEqual(serviceLedger.ProcessedMissionKeys), "sidecar round-trip");
Check(PromotionSystem.ReadLedger("{\"Other.Type\":{}}") == null, "sidecar without our blob is ignored");
Check(PromotionSystem.SidecarSuffix == ".jiangyu.SquaddieFieldPromotions.json", "sidecar suffix follows Jiangyu convention");

var preview = new DossierItemTemplate { Id = PromotionSystem.DossierId };
var vanilla = new DossierItemTemplate { Id = "dossier.squad_leader", Price = 25 };
var donorText = new LocalizedLine(LocaCategory.Items, "Title", false);
donorText.SetId(vanilla.Id);
donorText.SetDefaultTranslation("Vanilla dossier");
var donorDescription = new LocalizedMultiLine(LocaCategory.Items, "Description", false);
donorDescription.SetId(vanilla.Id);
donorDescription.SetDefaultTranslation("Vanilla description");
preview.Title = preview.ShortName = donorText;
vanilla.Title = vanilla.ShortName = donorText;
preview.Description = vanilla.Description = donorDescription;
var system = new PromotionSystem();
StrategyState.Current = new();
DataTemplateLoader.Template = preview;
system.OnInit();
var badgeArt = new StandalonePortraitAssets();
var badgeTexture = new UnityEngine.Texture2D(40, 40, UnityEngine.TextureFormat.RGBA32, false);
var badgePixels = new UnityEngine.Color32[40 * 40];
for (int y = 4; y < 36; y++)
    for (int x = 4; x < 36; x++) badgePixels[y * 40 + x] = new(0, 0, 0, 255);
for (int y = 12; y < 26; y++)
    for (int x = 14; x < 24; x++) badgePixels[y * 40 + x] = new(240, 26, 32, 255);
badgeTexture.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<UnityEngine.Color32>(badgePixels));
var badgeSource = UnityEngine.Sprite.Create(badgeTexture, new(0, 0, 40, 40), new(.3f, .7f), 73);
foreach (string name in new[] { "badge.png", "badge_mini.png", "badge_dragged.png", "badge_unit.png", "slot_badge.png", "big_badge.png" })
    badgeArt.Sprites[name] = badgeSource;
var badgePinned = new List<UnityEngine.Object>();
Check(DynamicBadgeFactory.Apply(textClone, badgeArt, "B", badgePinned) == "6/6" && badgePinned.Count == 12,
    "production badge factory replaces all six bundled variants and pins their private copies");
Check(textClone.BadgeWhite == textClone.Badge && textClone.Badge != badgeSource &&
    textClone.Badge.pixelsPerUnit == 73 && textClone.Badge.pivot.x == badgeSource.pivot.x &&
    textClone.Badge.pivot.y == badgeSource.pivot.y,
    "tactical overlay uses the same letter shield with original sprite scale and pivot");
Check(badgeTexture.GetPixels32().SequenceEqual(badgePixels) &&
    textClone.Badge.texture.GetPixels32()[0].a == 0 &&
    !textClone.Badge.texture.GetPixels32().SequenceEqual(badgePixels),
    "letter generation preserves source pixels and transparent background while changing the letter");
Check(textClone.GetBadge() == textClone.Badge && textClone.GetBadgeWhite() == textClone.BadgeWhite,
    "tactical getter callbacks expose SFP shield sprites instead of shared infantry badges");
Check(DynamicBadgeFactory.LabelForIndex(25) == "Z" && DynamicBadgeFactory.LabelForIndex(26) == "AA" &&
    DynamicBadgeFactory.LabelForIndex(52) == "BA", "production promotion labels continue beyond Z");
Check(textClone.GetUnitTitle() == textClone.UnitTitle &&
    textClone.GetUnitDescription() == textClone.UnitDescription,
    "SFP native getter postfixes expose generated infantry identity");
PromotionTemplateText.VerifyDisplay(textRecord, textClone);
Check(true, "display verification reads generated title and both descriptions through UI-facing APIs");
var secondTextRecord = R(selection, 1);
var secondTextClone = new UnitLeaderTemplate
{
    Id = secondTextRecord.LeaderId, InfantryUnitTemplate = textSource,
    UnitTitle = donorTitle, UnitDescription = donorUnitDescription,
    SpeakerTemplate = new SpeakerTemplate { Description = donorSpeakerDescription },
};
DynamicBadgeFactory.Apply(secondTextClone, badgeArt, "C", new List<UnityEngine.Object>());
Check(secondTextClone.GetBadge() != textClone.GetBadge() &&
    !secondTextClone.GetBadge().texture.GetPixels32().SequenceEqual(textClone.GetBadge().texture.GetPixels32()),
    "separate promoted squads retain different tactical letters");
PromotionTemplateText.Apply(secondTextRecord, secondTextClone, textSource,
    new UnitLeaderTemplate { SpeakerTemplate = speakerDonor });
PromotionTemplateText.VerifyDisplay(secondTextRecord, secondTextClone);
Check(secondTextClone.GetUnitTitle().ToString() != textClone.GetUnitTitle().ToString() &&
    textClone.GetUnitTitle().ToString() == textRecord.UnitTitle &&
    textSource.UnitTitle.ToString() == "15th Vanguard Division",
    "multiple SFP leaders sharing an infantry donor retain independent displayed identities");
var vanillaInfantry = new UnitLeaderTemplate
{
    Id = "squad_leader.sachin", InfantryUnitTemplate = textSource,
    UnitTitle = textClone.UnitTitle, UnitDescription = textClone.UnitDescription,
};
Check(vanillaInfantry.GetUnitTitle() == donorTitle && vanillaInfantry.GetUnitDescription() == donorUnitDescription,
    "vanilla infantry keeps shared entity text despite having different leader fields");
vanillaInfantry.Badge = textClone.Badge;
vanillaInfantry.BadgeWhite = textClone.BadgeWhite;
Check(vanillaInfantry.GetBadge() == textSource.Badge && vanillaInfantry.GetBadgeWhite() == textSource.BadgeWhite,
    "vanilla infantry badge getter results and shared donor sprites remain unchanged");
var vanillaPilot = new UnitLeaderTemplate
{
    Id = "squad_leader.pilot", IsInfantry = false, UnitTitle = donorTitle, UnitDescription = donorUnitDescription,
};
Check(vanillaPilot.GetUnitTitle() == donorTitle && vanillaPilot.GetUnitDescription() == donorUnitDescription,
    "vanilla pilot getter results are untouched");
var incompletePromotion = new UnitLeaderTemplate { Id = textRecord.LeaderId, InfantryUnitTemplate = textSource };
Check(incompletePromotion.GetUnitTitle() == donorTitle && incompletePromotion.GetUnitDescription() == donorUnitDescription,
    "missing promoted text preserves the original getter result");
Check(incompletePromotion.GetBadge() == textSource.Badge && incompletePromotion.GetBadgeWhite() == textSource.BadgeWhite,
    "missing SFP sprites preserve native getter results rather than blanking the HUD");
var anonymousInfantry = new UnitLeaderTemplate { InfantryUnitTemplate = textSource, UnitTitle = textClone.UnitTitle };
Check(anonymousInfantry.GetUnitTitle() == donorTitle, "missing template ID cannot receive the SFP override");
system.OnTemplatesApplied();
Check(preview.Title.GetRawDefaultTranslation() == "Field Promotion Dossier", "native localized dossier title");
Check(preview.ShortName.GetRawDefaultTranslation() == "Field promotion", "native localized short name");
Check(preview.Description.GetRawDefaultTranslation().Contains("is removed from their squad and the reserve"), "native localized removal description");
Check(LeaderForge.Last != null && LeaderForge.Last.ModFolder == system.Context.ModFolder, "forge bound to the mod folder");
Check(system.Context.Coroutines.Started.Count == 0, "no voice preparation while disabled and nothing pending");
Check(new BaseLocalizedString[] { preview.Title, preview.ShortName, preview.Description }.All(t =>
    t.Id == PromotionSystem.DossierId && t.Category == LocaCategory.Items), "preview-specific localization keys");
Check(preview.Title.Field == "Title" && preview.ShortName.Field == "ShortName" && preview.Description.Field == "Description", "distinct localization fields");
Check(!ReferenceEquals(preview.Title, donorText) && !ReferenceEquals(preview.ShortName, donorText) &&
    !ReferenceEquals(preview.Description, donorDescription), "all localized objects detached from donor");
Check(vanilla.Title == donorText && vanilla.ShortName == donorText && vanilla.Description == donorDescription &&
    donorText.Id == vanilla.Id && donorText.GetRawDefaultTranslation() == "Vanilla dossier" &&
    donorDescription.GetRawDefaultTranslation() == "Vanilla description", "vanilla localization untouched");
DataTemplateLoader.Template = vanilla;
system.OnTemplatesApplied();
Check(vanilla.Title == donorText && donorText.GetRawDefaultTranslation() == "Vanilla dossier", "wrong-template lookup cannot mutate vanilla");
Log.Warnings.Clear();
DataTemplateLoader.Template = preview;
var prefixes = system.Context.Patches.Prefixes;
var postfixes = system.Context.Patches.Postfixes;
BlackMarketUIScreen marketScreen = null;
void Open()
{
    marketScreen = new BlackMarketUIScreen();
    UIManager.Current.Active = marketScreen;
    postfixes["OnOpened"](new PatchInfo { Instance = marketScreen });
}
PatchInfo Redeem(DossierItemTemplate template)
{
    var info = new PatchInfo { Instance = template };
    prefixes["Redeem"](info);
    if (!info.Skip) template.Redeem();
    return info;
}
Open();
Check(StrategyState.Current.BlackMarket.Items.Count == 0, "stock OFF by default");
Check(!(bool)ModSettings.Values["DebugLogging"] && Log.InfoLines.Count == 0, "silent by default");
Check(ModSettings.Numbers[PromotionSystem.PriceKey] == (10, 1000, 100, 10), "price number defaults to 100 and steps by 10");
Check((bool)ModSettings.Values[PromotionSystem.GenericHeadKey], "generic promoted-leader heads default on");
foreach (var (input, expected) in new[] { (int.MinValue,10), (0,10), (10,10), (99,90), (100,100), (110,110), (999,990), (int.MaxValue,1000) })
    Check(PromotionSystem.NormalizePrice(input) == expected, "safe price normalization " + input);
Check(marketScreen.Updates == 1, "native market window refreshed after open");
ModSettings.Set(PromotionSystem.EnableKey, true);
Check(system.Context.Coroutines.Started.Count == 1, "enabling schedules the one-shot voice preparation");
system.Context.Coroutines.RunAll(); // Donor templates never appear in the stub: coroutine gives up silently.
Check(LeaderForge.Last.VoicePrepareCalls == 0 && Log.Warnings.Count == 0, "voice wait without donors is silent and leaves no attempt");
preview.Title = donorText;
Open(); Open();
Check(preview.Title != donorText && preview.Title.Id == PromotionSystem.DossierId, "market open repairs text even on existing stock");
Check(StrategyState.Current.BlackMarket.Items.Count == 1, "native stock idempotent");
Check(StrategyState.Current.BlackMarket.LastTimeout == PromotionSystem.PreviewStockTimeout &&
    PromotionSystem.PreviewStockTimeout == 3, "preview uses normal three-restock market timeout");
ModSettings.Set("DebugLogging", true);
Open();
Check(Log.InfoLines.Any(line => line.Contains("normal=1") && line.Contains("preview-visible=1")),
    "market diagnostics report native stock and merchant-grid visibility");
ModSettings.Set("DebugLogging", false);
var price = new PatchInfo { Instance = preview, Result = 77 };
prefixes["GetTradeValue"](price);
Check(price.Skip && (int)price.Result == PromotionSystem.DefaultPrice, "dossier default price is 100");
int updatesBeforePriceChange = marketScreen.Updates;
ModSettings.Set(PromotionSystem.PriceKey, 110);
preview.Price = 110; // Simulate the patched native GetTradeValue used by the stock/UI reader.
prefixes["GetTradeValue"](price);
Check((int)price.Result == 110 && marketScreen.Updates > updatesBeforePriceChange, "price change applies immediately and refreshes market");
ModSettings.Set(PromotionSystem.PriceKey, 100);
preview.Price = 100;
Check(marketScreen.m_TempFilteredRegularOffers.Any(i => i.Template == preview), "preview survives native price filter");
price = new PatchInfo { Instance = vanilla, Result = 25 };
prefixes["GetTradeValue"](price);
Check(!price.Skip && (int)price.Result == 25, "vanilla price untouched");
Check(!Redeem(vanilla).Skip && vanilla.VanillaRedeems == 1, "vanilla redemption untouched");
NativeCandidates.Value = selection;
Squaddie Seed(Candidate candidate, BaseUnitLeader owner = null)
{
    var soldier = new Squaddie { Id = candidate.Id, Name = candidate.Name, Nickname = candidate.Nickname,
        Gender = candidate.Gender, SkinColor = candidate.SkinColor, Owner = owner };
    StrategyState.Current.Squaddies.m_AliveSquaddies.Add(soldier);
    return soldier;
}
var squad = new BaseUnitLeader();
squad.m_SquaddieIds.AddRange(new[] { 80, selection.Soldier.Id, 81 });
var firstSource = Seed(selection.Soldier, squad);
var untouched = Seed(C(99));
int poolNotifications = 0;
StrategyState.Current.Squaddies.OnSquaddiesChanged = () => poolNotifications++;
var liveLedger = system.Context.State.Get<PromotionLedger>();
Check(Redeem(preview).Skip && preview.VanillaRedeems == 0, "custom redemption NEVER calls vanilla");
Check(UIManager.Current.Last.Text.Contains("is now a squad leader"), "promotion result visible");
Check(!UIManager.Current.Last.Label.enableRichText, "names cannot inject markup");
Check(LeaderForge.Last.Installed.Count == 1 && StrategyState.Current.Roster.Hirable.Count == 1, "leader installed once into Hire");
Check(liveLedger.Records.Count == 1 && liveLedger.Records[0].SourceSquaddieId == selection.Soldier.Id &&
    liveLedger.Records[0].LeaderId == LeaderForge.Last.Installed[0] &&
    liveLedger.Records[0].PortraitProfile == PromotionPortraitAssignment.MaleBrown01 &&
    system.Context.State.Saves == 1, "first eligible portrait assignment recorded with the promotion");
Check(liveLedger.Records[0].SourceRemoved && !StrategyState.Current.Squaddies.IsAlive(firstSource.Id) &&
    squad.m_SquaddieIds.SequenceEqual(new[] { 80, 81 }), "exact promoted soldier removed from assigned squad and pool");
Check(LeaderForge.Last.HeadModes.Last(), "new promoted leader uses generic heads by default");
Check(StrategyState.Current.Squaddies.m_AliveSquaddies.Single() == untouched &&
    StrategyState.Current.Squaddies.m_KilledSquaddies.Count == 0 && poolNotifications == 1, "other soldiers untouched, no fake death, one notification");
Check(NativeCandidates.LastExcluded.Count == 0, "first promotion excludes nobody");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("No eligible living squaddie"), "promoted soldier is never promoted twice");
Check(NativeCandidates.LastExcluded.SetEquals(new[] { selection.Soldier.Id }) && liveLedger.Records.Count == 1 &&
    LeaderForge.Last.Installed.Count == 1, "second redemption changes nothing");
var unitTitle = new LocalizedLine(LocaCategory.Items, "UnitTitle", false);
unitTitle.SetDefaultTranslation("81st Astral Assault");
var commanderName = new LocalizedLine(LocaCategory.Items, "Nickname", false);
commanderName.SetDefaultTranslation("Lim");
var deployedLeader = new BaseUnitLeader
{
    LeaderTemplate = new UnitLeaderTemplate
    {
        Id = "unit.81",
        UnitTitle = unitTitle,
        SpeakerTemplate = new SpeakerTemplate { Nickname = commanderName, FullName = "Lim" },
    },
    Statistics = new UnitStatistics(),
};
deployedLeader.m_SquaddieIds.Add(untouched.Id);
deployedLeader.Statistics.WoundedIds.Add(untouched.Id);
var completedMission = new Mission { Template = new MissionTemplate { Id = "operation.test" }, Index = 3, Seed = 8080 };
var deployedLeaders = new Il2CppSystem.Collections.Generic.List<BaseUnitLeader> { deployedLeader };
int savesBeforeHistory = system.Context.State.Saves;
var missionEndInfo = new PatchInfo { Args = new object[] { completedMission, deployedLeaders } };
prefixes["OnMissionEnd"](missionEndInfo);
postfixes["OnMissionEnd"](missionEndInfo);
var observedService = liveLedger.FindServiceRecord(untouched.Id);
Check(observedService.Units.Single().UnitTitle == "81st Astral Assault" &&
    observedService.Units.Single().CommanderName == "Lim" && observedService.Units.Single().WoundedMissions == 1 &&
    system.Context.State.Saves == savesBeforeHistory + 1, "mission-end callbacks persist deployed-unit and wound history");
prefixes["OnMissionEnd"](missionEndInfo);
postfixes["OnMissionEnd"](missionEndInfo);
Check(observedService.Units.Single().MissionsServed == 1 && system.Context.State.Saves == savesBeforeHistory + 1,
    "replayed mission-end callback is idempotent");
ModSettings.Set(PromotionSystem.GenericHeadKey, false);
Check(!LeaderForge.Last.HeadModes.Last(), "head setting restores unique leader heads");
ModSettings.Set(PromotionSystem.GenericHeadKey, true);
Check(LeaderForge.Last.HeadModes.Last(), "head setting reapplies generic leader heads");
NativeCandidates.Value = excluded;
var secondSource = Seed(excluded.Soldier);
LeaderForge.FailInstall = true;
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("Field promotion failed") && liveLedger.Records.Count == 1,
    "native clone failure keeps the ledger unchanged and tells the player");
LeaderForge.FailInstall = false;
Check(StrategyState.Current.Squaddies.m_AliveSquaddies.Contains(secondSource), "forge failure preserves source soldier");
system.Context.State.FailSave = true;
Redeem(preview);
Check(liveLedger.Records.Count == 1 && StrategyState.Current.Roster.Hirable.Count == 1 &&
    StrategyState.Current.Squaddies.m_AliveSquaddies.SequenceEqual(new[] { untouched, secondSource }) && poolNotifications == 1,
    "state failure rolls back Hire, source and ledger without notifying observers");
system.Context.State.FailSave = false;
StrategyState.Current.Squaddies.FailReadback = true;
Redeem(preview);
Check(liveLedger.Records.Count == 1 && StrategyState.Current.Roster.Hirable.Count == 1 &&
    StrategyState.Current.Squaddies.m_AliveSquaddies.Contains(secondSource), "failed removal readback rolls back Hire and source");
StrategyState.Current.Squaddies.FailReadback = false;
Check(Redeem(preview).Skip && liveLedger.Records.Count == 2 && liveLedger.Records[1].BadgeIndex == 1 &&
    liveLedger.Records[1].PortraitProfile == PromotionPortraitAssignment.MaleBrown04,
    "next promotion gets the next badge and the category's next portrait set");
Check(!StrategyState.Current.Squaddies.IsAlive(secondSource.Id), "unassigned promoted source also removed");
Check(UIManager.Current.Last.Text.Contains("1 already promoted skipped") || liveLedger.Records[1].SourceSquaddieId == excluded.Soldier.Id, "second leader from a different soldier");
Log.Warnings.Clear();
NativeCandidates.Fail = true;
Check(Redeem(preview).Skip && preview.VanillaRedeems == 0, "exception still blocks native unlock");
Redeem(preview);
Check(Log.Warnings.Count == 1, "actionable errors are one-shot");
NativeCandidates.Fail = false;
StrategyState.Current.BlackMarket.AddItem(vanilla.CreateItem("vanilla"), 0);
ModSettings.Set(PromotionSystem.EnableKey, false);
Check(StrategyState.Current.BlackMarket.Items.Count == 1 && StrategyState.Current.BlackMarket.Items[0].Template == vanilla, "disable removes ONLY preview stock");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("disabled"), "existing dossier disabled safely");
StrategyState.Current.Ironman = true;
ModSettings.Set(PromotionSystem.EnableKey, true);
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "no stock in Ironman");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("non-Ironman"), "Ironman promotion blocked");

// Save/load: the ExecLoad prefix rebuilds templates from the sidecar beside the save file.
string saveDir = Path.Combine(Path.GetTempPath(), "sfp-offline-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(saveDir);
string savePath = Path.Combine(saveDir, "slot1.sav");
File.WriteAllText(savePath, "native");
PatchInfo Load(string path)
{
    var info = new PatchInfo { Args = new object[] { new Il2CppMenace.Strategy.SaveState { FilePath = path } } };
    prefixes["ExecLoad"](info);
    postfixes["ProcessSaveState"](new PatchInfo { Instance = StrategyState.Current.Roster, Args = info.Args });
    postfixes["Close"](new PatchInfo { Instance = info.Args[0] });
    return info;
}
Log.Warnings.Clear();
Check(!Load(savePath).Skip && LeaderForge.Last.Rehydrated.Count == 0 && Log.Warnings.Count == 0, "save without sidecar loads silently");
Check(!Load(null).Skip && Log.Warnings.Count == 0, "missing save path ignored");
var savedLedger = new PromotionLedger();
foreach (var r in liveLedger.Records) savedLedger.Records.Add(r);
savedLedger.ServiceRecords.AddRange(liveLedger.ServiceRecords);
savedLedger.ProcessedMissionKeys.AddRange(liveLedger.ProcessedMissionKeys);
File.WriteAllText(savePath + PromotionSystem.SidecarSuffix, System.Text.Json.JsonSerializer.Serialize(
    new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = savedLedger }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
system.Context.Coroutines.RunAll(); // Drain the pending (donor-less) voice wait so a new one can be scheduled.
Load(savePath);
Check(LeaderForge.Last.Rehydrated.SequenceEqual(savedLedger.Records.Select(r => r.LeaderId)) &&
    LeaderForge.Last.Refreshed.SequenceEqual(savedLedger.Records.Select(r => r.LeaderId)) &&
    savedLedger.Records[0].PortraitProfile == PromotionPortraitAssignment.MaleBrown01 && Log.Warnings.Count == 0,
    "every saved leader and its portrait assignment are rebuilt before roster load");
Check(system.Context.Coroutines.Started.Count == 1, "rebuilt leaders schedule voice binding");
var broken = R(PromotionRules.Choose(new[] { C(9, 1) }), 5); broken.Name = "Broken";
savedLedger.Records.Add(broken);
File.WriteAllText(savePath + PromotionSystem.SidecarSuffix, System.Text.Json.JsonSerializer.Serialize(
    new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = savedLedger }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
LeaderForge.Last.Rehydrated.Clear();
LeaderForge.Last.Refreshed.Clear();
Load(savePath);
Check(LeaderForge.Last.Rehydrated.Count == 2 &&
    LeaderForge.Last.Refreshed.SequenceEqual(savedLedger.Records.Take(2).Select(r => r.LeaderId)) &&
    Log.Warnings.Count == 1 && Log.Warnings[0].Contains("Broken"), "one broken record never blocks the others");
File.WriteAllText(savePath + PromotionSystem.SidecarSuffix, "{ not json");
Load(savePath);
Check(Log.Warnings.Count == 2 && Log.Warnings[1].Contains("unreadable"), "corrupt sidecar warns once per save and loads nothing");
Load(savePath);
Check(Log.Warnings.Count == 2, "corrupt sidecar warning is one-shot");
Directory.Delete(saveDir, true);
Log.Warnings.Clear();
StrategyState.Current.Ironman = false;
preview.m_UnlockedLeaders = new object[1];
Open();
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "malformed donor list fails closed");
preview.m_UnlockedLeaders = Array.Empty<object>();
preview.Price = 4;
Open();
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "unexpected preview price fails closed");
preview.Price = PromotionSystem.DefaultPrice;
system.OnUnload();
Check(textClone.GetBadge() == textSource.Badge && textClone.GetBadgeWhite() == textSource.BadgeWhite,
    "unloaded badge callbacks leave native tactical results untouched");
Check(textClone.GetUnitTitle() == donorTitle && textClone.GetUnitDescription() == donorUnitDescription,
    "unloaded display callbacks leave native results untouched");
Open();
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "unloaded callbacks cannot stock");
if (args.Length > 0)
{
    string root = Path.Combine(args[0], "mods/jiangyu/SquaddieFieldPromotions");
    using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "jiangyu.json")));
    string version = manifest.RootElement.GetProperty("version").GetString();
    var privateVoiceCatalog = PromotionVoiceCatalog.Load(root);
    if (privateVoiceCatalog != null)
    {
        Check(privateVoiceCatalog.Profiles.Count == 56 &&
            privateVoiceCatalog.Profiles.Count(profile => profile.Gender == "male") == 38 &&
            privateVoiceCatalog.Profiles.Count(profile => profile.Gender == "female") == 18,
            "private XCOM catalog contains the 56 human profiles with gender-matched counts");
        Check(privateVoiceCatalog.Profiles.All(profile => profile.Kind == "human" &&
            profile.RoleEvidence.Length > 0 && profile.Routes.Count > 0) &&
            privateVoiceCatalog.Profiles.Any(profile => profile.FuturePilotCandidate) &&
            privateVoiceCatalog.Profiles.Any(profile => profile.AviationRadioCandidate),
            "private XCOM catalog validates human routes and future pilot/radio candidate evidence");
    }
    var xml = System.Xml.Linq.XDocument.Load(Path.Combine(root, "code/SquaddieFieldPromotions.csproj"));
    Check(xml.Descendants("Version").Single().Value == version, "source manifest/project version");
    string dll = Path.Combine(root, "compiled/code/SquaddieFieldPromotions.dll");
    var actual = System.Reflection.AssemblyName.GetAssemblyName(dll);
    Check(actual.Name == "SquaddieFieldPromotions" && actual.Version == new Version(version + ".0"), "real DLL AssemblyVersion and identity");
    Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(dll).FileVersion == version + ".0", "real DLL FileVersion");
    // Runtime assets are read by the DLL from <mod>/runtime_assets; names mirror StandalonePortraitAssets/RuntimeVoiceBundle.
    string assets = Path.Combine(root, "runtime_assets");
    string[] portraitFiles = { "slot.png", "slot_inactive.png", "bark.png", "stand_right.png", "stand_left.png",
        "stand_right_inactive.png", "badge.png", "badge_mini.png", "badge_dragged.png", "badge_unit.png", "slot_badge.png", "big_badge.png" };
    string portraitSource = File.ReadAllText(Path.Combine(root, "code/StandalonePortraitAssets.cs"));
    Check(portraitFiles.All(name => portraitSource.Contains('"' + name + '"')), "portrait file list mirrors production code");
    Check(new[] { "male", "female" }.All(gender => portraitFiles.All(name =>
        new FileInfo(Path.Combine(assets, gender, name)).Length > 0)), "bundled portrait assets complete for both genders");
    string voice = Path.Combine(assets, "voice");
    using var receipt = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(voice, "sfp_john_jane_voice.build.json")));
    using var sha = System.Security.Cryptography.SHA256.Create();
    using (var bundle = File.OpenRead(Path.Combine(voice, "sfp_john_jane_voice.sfpbundle")))
        Check(Convert.ToHexString(sha.ComputeHash(bundle)).Equals(receipt.RootElement.GetProperty("bundleSha256").GetString(), StringComparison.OrdinalIgnoreCase),
            "bundled voice bundle matches its build receipt");
    using var compiledManifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "compiled/jiangyu.json")));
    Check(receipt.RootElement.GetProperty("unityVersion").GetString() == compiledManifest.RootElement.GetProperty("compiledForUnity").GetString(),
        "voice bundle built for the pinned Unity version");
    using var xcomReceipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(voice, "sfp_xcom_voicepacks.build.json")));
    var xcomAssets = xcomReceipt.RootElement.GetProperty("clips").EnumerateArray()
        .Select(clip => clip.GetProperty("assetName").GetString()).ToHashSet(StringComparer.Ordinal);
    Check(xcomAssets.Count == xcomReceipt.RootElement.GetProperty("clips").GetArrayLength() &&
        xcomAssets.SetEquals(privateVoiceCatalog.Profiles.SelectMany(profile => profile.Routes.Values)
            .SelectMany(route => route.Assets)),
        "private receipt covers exactly the catalog's distinct recordings, with no duplicate or unused padding");
    var blackOps = privateVoiceCatalog.Find("trepang2blackops");
    Check(blackOps != null && blackOps.Routes.Count > 42 && blackOps.Routes.Values.Any(route => route.HasCaptionedVariants) &&
        blackOps.Routes.Values.All(route => route.AssetSubtitles?.Length == route.Assets.Length),
        "full-pack BlackOps candidate expands beyond the old minimum and captions every recording");
    using (var xcomBundle = File.OpenRead(Path.Combine(voice, "sfp_xcom_voicepacks.sfpbundle")))
        Check(Convert.ToHexString(sha.ComputeHash(xcomBundle)).Equals(
            xcomReceipt.RootElement.GetProperty("bundleSha256").GetString(), StringComparison.OrdinalIgnoreCase),
            "private XCOM bundle matches its rebuilt receipt");
    Check(xcomReceipt.RootElement.GetProperty("unityVersion").GetString() == compiledManifest.RootElement.GetProperty("compiledForUnity").GetString(),
        "private XCOM bundle uses the pinned Unity version");
    Check(File.Exists(Path.Combine(voice, "voice_routes.json")), "voice routes bundled");
}
SourceRemovalChecks.Run(Check);
PreviewMigrationChecks.Run(Check);

var cleanupSystem = new PromotionSystem();
cleanupSystem.OnInit();
StrategyState.Current = new StrategyState();
var cleanupLedger = cleanupSystem.Context.State.Get<PromotionLedger>();
var cleanupHirableRecord = R(PromotionRules.Choose(new[] { C(801, xp: 10) }), badge: 0);
var cleanupDismissedRecord = R(PromotionRules.Choose(new[] { C(802, xp: 20) }), badge: 1);
var cleanupHiredRecord = R(PromotionRules.Choose(new[] { C(803, xp: 30) }), badge: 2);
var cleanupUnburiedRecord = R(PromotionRules.Choose(new[] { C(804, xp: 40) }), badge: 3);
var cleanupBuriedRecord = R(PromotionRules.Choose(new[] { C(805, xp: 50) }), badge: 4);
cleanupHirableRecord.SourceRemoved = cleanupDismissedRecord.SourceRemoved = cleanupHiredRecord.SourceRemoved =
    cleanupUnburiedRecord.SourceRemoved = cleanupBuriedRecord.SourceRemoved = true;
cleanupLedger.Records.Add(cleanupHirableRecord);
cleanupLedger.Records.Add(cleanupDismissedRecord);
cleanupLedger.Records.Add(cleanupHiredRecord);
cleanupLedger.Records.Add(cleanupUnburiedRecord);
cleanupLedger.Records.Add(cleanupBuriedRecord);
var cleanupHirable = new UnitLeaderTemplate { Id = cleanupHirableRecord.LeaderId };
var cleanupDismissed = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = cleanupDismissedRecord.LeaderId } };
var cleanupHired = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = cleanupHiredRecord.LeaderId } };
var cleanupUnburied = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = cleanupUnburiedRecord.LeaderId } };
var cleanupBuried = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = cleanupBuriedRecord.LeaderId } };
var vanillaHirable = new UnitLeaderTemplate { Id = "vanilla.hirable" };
var vanillaHired = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = "vanilla.hired" } };
var vanillaDismissed = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = "vanilla.dismissed" } };
var vanillaUnburied = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = "vanilla.unburied" } };
var vanillaBuried = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = "vanilla.buried" } };
StrategyState.Current.Roster.m_HirableLeaders.Add(cleanupHirable);
StrategyState.Current.Roster.m_HirableLeaders.Add(vanillaHirable);
StrategyState.Current.Roster.m_DismissedLeaders.Add(cleanupDismissed);
StrategyState.Current.Roster.m_DismissedLeaders.Add(vanillaDismissed);
StrategyState.Current.Roster.m_HiredLeaders.Add(vanillaHired);
StrategyState.Current.Roster.m_HiredLeaders.Add(cleanupHired);
StrategyState.Current.Roster.m_UnburiedLeaders.Add(cleanupUnburied);
StrategyState.Current.Roster.m_UnburiedLeaders.Add(vanillaUnburied);
StrategyState.Current.Roster.m_BuriedLeaders.Add(cleanupBuried);
StrategyState.Current.Roster.m_BuriedLeaders.Add(vanillaBuried);
var sfpDeployment = new DeployedEntity { Leader = cleanupHired, Tile = 42 };
var vanillaDeployment = new DeployedEntity { Leader = vanillaHired, Tile = 43 };
StrategyState.Current.BattlePlan.m_EntitiesToDeploy.Add(sfpDeployment);
StrategyState.Current.BattlePlan.m_EntitiesToDeploy.Add(vanillaDeployment);
SaveState BeginCleanupSave(string path)
{
    var save = new SaveState { FilePath = path };
    cleanupSystem.Context.Patches.Prefixes["ExecLoad"](new PatchInfo { Args = new object[] { save } });
    return save;
}
void RosterLoaded(SaveState save, Roster roster = null) =>
    cleanupSystem.Context.Patches.Postfixes["ProcessSaveState"](new PatchInfo
    { Instance = roster ?? StrategyState.Current.Roster, Args = new object[] { save } });
void SaveClosed(SaveState save) =>
    cleanupSystem.Context.Patches.Postfixes["Close"](new PatchInfo { Instance = save });
void LoadCleanupSave(string path)
{
    var save = BeginCleanupSave(path);
    RosterLoaded(save);
    SaveClosed(save);
}
string cleanupSavePath = Path.Combine(Path.GetTempPath(), "test.save");
int cleanupSaveCalls = SaveSystem.SaveCalls;
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
var asyncSave = BeginCleanupSave(cleanupSavePath);
cleanupSystem.OnSceneLoaded(0, "Strategy");
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == cleanupSaveCalls && (bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey] &&
    StrategyState.Current.Roster.m_HirableLeaders.Contains(cleanupHirable),
    "ExecLoad and scene arrival cannot clean the previous roster or consume the request");
var populatedRoster = StrategyState.Current.Roster;
StrategyState.Current.Roster = new Roster(); // Scene loaded; native deserialization has not run yet.
cleanupSystem.OnSceneLoaded(0, "Strategy");
SaveClosed(new SaveState { FilePath = cleanupSavePath }); // Metadata-only save browser read.
RosterLoaded(asyncSave, populatedRoster); // Stale roster is not the active one.
SaveClosed(asyncSave);
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == cleanupSaveCalls && (bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "empty scene roster, metadata Close and stale roster callbacks cannot release cleanup");
StrategyState.Current.Roster = populatedRoster;
var savingState = new SaveState { FilePath = cleanupSavePath, Loading = false };
RosterLoaded(savingState);
SaveClosed(savingState);
var otherLoad = new SaveState { FilePath = Path.Combine(Path.GetTempPath(), "other.save") };
RosterLoaded(otherLoad);
SaveClosed(otherLoad);
RosterLoaded(asyncSave);
SaveClosed(new SaveState { FilePath = cleanupSavePath });
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == cleanupSaveCalls && (bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "saving, unrelated loads, and same-path metadata streams cannot finish the active native load");
// The same native stream may arrive through another managed proxy object.
SaveClosed(new SaveState { FilePath = cleanupSavePath, Pointer = asyncSave.Pointer });
cleanupSystem.Context.Coroutines.RunAll();
Check(!StrategyState.Current.Roster.m_HirableLeaders.Contains(cleanupHirable) &&
    !StrategyState.Current.Roster.m_HiredLeaders.Contains(cleanupHired) &&
    !StrategyState.Current.Roster.m_DismissedLeaders.Contains(cleanupDismissed) &&
    !StrategyState.Current.Roster.m_UnburiedLeaders.Contains(cleanupUnburied) &&
    !StrategyState.Current.Roster.m_BuriedLeaders.Contains(cleanupBuried),
    "one-time cleanup removes SFP leaders from all five native roster states");
Check(StrategyState.Current.Roster.m_HirableLeaders.Contains(vanillaHirable) &&
    StrategyState.Current.Roster.m_HiredLeaders.Contains(vanillaHired) &&
    StrategyState.Current.Roster.m_DismissedLeaders.Contains(vanillaDismissed) &&
    StrategyState.Current.Roster.m_UnburiedLeaders.Contains(vanillaUnburied) &&
    StrategyState.Current.Roster.m_BuriedLeaders.Contains(vanillaBuried),
    "one-time cleanup preserves unrelated leaders in every native roster state");
Check(SaveSystem.SaveCalls == cleanupSaveCalls + 1 && SaveSystem.LastSavedPath == cleanupSavePath &&
    cleanupLedger.Records.Count == 5 && !(bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "cleanup uses native save, retains its ledger, and resets the one-shot setting");
Check(StrategyState.Current.BattlePlan.m_EntitiesToDeploy.SequenceEqual(new[] { vanillaDeployment }),
    "cleanup removes matching deployment references and preserves unrelated deployment positions");

var duplicateRecord = R(PromotionRules.Choose(new[] { C(806, xp: 60) }), badge: 5);
duplicateRecord.SourceRemoved = true;
cleanupLedger.Records.Add(duplicateRecord);
var duplicateHirable = new UnitLeaderTemplate { Id = duplicateRecord.LeaderId };
var duplicateDismissed = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = duplicateRecord.LeaderId } };
StrategyState.Current.Roster.m_HirableLeaders.Add(duplicateHirable);
StrategyState.Current.Roster.m_DismissedLeaders.Add(duplicateDismissed);
LoadCleanupSave(cleanupSavePath);
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
cleanupSystem.Context.Coroutines.RunAll();
Check(!StrategyState.Current.Roster.m_HirableLeaders.Contains(duplicateHirable) &&
    !StrategyState.Current.Roster.m_DismissedLeaders.Contains(duplicateDismissed) &&
    SaveSystem.SaveCalls == cleanupSaveCalls + 2 && !(bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "duplicate native placements are removed from every list and saved");

LoadCleanupSave(cleanupSavePath);
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == cleanupSaveCalls + 2 && !(bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "repeated cleanup does not save when all promoted leaders are already absent");

var rollbackRecord = R(PromotionRules.Choose(new[] { C(807, xp: 70) }), badge: 6);
rollbackRecord.SourceRemoved = true;
cleanupLedger.Records.Add(rollbackRecord);
var rollbackHirable = new UnitLeaderTemplate { Id = rollbackRecord.LeaderId };
var rollbackHired = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = rollbackRecord.LeaderId } };
var rollbackDismissed = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = rollbackRecord.LeaderId } };
var rollbackUnburied = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = rollbackRecord.LeaderId } };
var rollbackBuried = new BaseUnitLeader { LeaderTemplate = new UnitLeaderTemplate { Id = rollbackRecord.LeaderId } };
StrategyState.Current.Roster.m_HirableLeaders.Add(rollbackHirable);
StrategyState.Current.Roster.m_HiredLeaders.Add(rollbackHired);
StrategyState.Current.Roster.m_DismissedLeaders.Add(rollbackDismissed);
StrategyState.Current.Roster.m_UnburiedLeaders.Add(rollbackUnburied);
StrategyState.Current.Roster.m_BuriedLeaders.Add(rollbackBuried);
var rollbackDeployment = new DeployedEntity { Leader = rollbackHired, Tile = 99 };
StrategyState.Current.BattlePlan.m_EntitiesToDeploy.Insert(0, rollbackDeployment);
LoadCleanupSave(cleanupSavePath);
int saveCallsBeforeRollback = SaveSystem.SaveCalls;
SaveSystem.FailSave = true;
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
cleanupSystem.Context.Coroutines.RunAll();
SaveSystem.FailSave = false;
Check(StrategyState.Current.Roster.m_HirableLeaders.Contains(rollbackHirable) &&
    StrategyState.Current.Roster.m_HiredLeaders.Contains(rollbackHired) &&
    StrategyState.Current.Roster.m_DismissedLeaders.Contains(rollbackDismissed) &&
    StrategyState.Current.Roster.m_UnburiedLeaders.Contains(rollbackUnburied) &&
    StrategyState.Current.Roster.m_BuriedLeaders.Contains(rollbackBuried) &&
    SaveSystem.SaveCalls == saveCallsBeforeRollback + 1 &&
    !(bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "failed native save restores all five roster states and resets the one-shot setting");
Check(StrategyState.Current.BattlePlan.m_EntitiesToDeploy.SequenceEqual(new[] { rollbackDeployment, vanillaDeployment }) &&
    rollbackDeployment.Tile == 99,
    "failed native save restores exact deployment objects, ordering and tile position");

var wrongPathEntry = new UnitLeaderTemplate { Id = cleanupHirableRecord.LeaderId };
StrategyState.Current.Roster.m_HirableLeaders.Add(wrongPathEntry);
LoadCleanupSave(Path.Combine(Path.GetTempPath(), "other.save"));
int saveCallsBeforeWrongPath = SaveSystem.SaveCalls;
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
cleanupSystem.Context.Coroutines.RunAll();
Check(StrategyState.Current.Roster.m_HirableLeaders.Contains(wrongPathEntry) &&
    SaveSystem.SaveCalls == saveCallsBeforeWrongPath && !(bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "cleanup refuses other save filenames and resets the one-shot setting");

// A new load cancels a queued cleanup, including when the strategy scene never changes.
LoadCleanupSave(cleanupSavePath);
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
BeginCleanupSave(Path.Combine(Path.GetTempPath(), "another.save"));
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == saveCallsBeforeWrongPath && StrategyState.Current.Roster.m_HirableLeaders.Contains(wrongPathEntry),
    "a later load cancels the queued cleanup and resets native-load readiness");

// A changed singleton must not receive a cleanup queued for a different native roster.
LoadCleanupSave(cleanupSavePath);
var originalState = StrategyState.Current;
StrategyState.Current = new StrategyState();
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == saveCallsBeforeWrongPath && (bool)ModSettings.Values[PromotionSystem.CleanupTestSaveKey],
    "queued cleanup refuses a replacement strategy singleton without consuming its request");
StrategyState.Current = originalState;
LoadCleanupSave(cleanupSavePath);
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == saveCallsBeforeWrongPath + 1 &&
    StrategyState.Current.BattlePlan.m_EntitiesToDeploy.SequenceEqual(new[] { vanillaDeployment }),
    "reload in the same strategy scene releases cleanup only after native completion");

// Retained sidecar records must not hide deployment-only references from the no-op check.
StrategyState.Current.BattlePlan.m_EntitiesToDeploy.Add(rollbackDeployment);
LoadCleanupSave(cleanupSavePath);
ModSettings.Set(PromotionSystem.CleanupTestSaveKey, true);
cleanupSystem.Context.Coroutines.RunAll();
Check(SaveSystem.SaveCalls == saveCallsBeforeWrongPath + 2 &&
    StrategyState.Current.BattlePlan.m_EntitiesToDeploy.SequenceEqual(new[] { vanillaDeployment }),
    "deployment-only SFP reference is removed and saved even when all roster lists are clean");
cleanupSystem.OnUnload();
Console.WriteLine($"PASS: {checks} behavior checks (production rules and callbacks; native APIs simulated).");
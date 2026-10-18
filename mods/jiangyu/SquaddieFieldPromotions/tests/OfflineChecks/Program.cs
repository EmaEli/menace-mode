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

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks++;
}
Candidate C(int id, int missions = 0, int? xp = null, bool assigned = false,
    string lastUnitTitle = "", string lastCommander = "", string rank = "") =>
    new(id, "Soldier", "Nickname", 0, 1, missions, xp, assigned, lastUnitTitle, lastCommander, rank);
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
selection = PromotionRules.Choose(new[] { C(1, 2, 999), C(2, 3) });
Check(!selection.UsesXp && selection.PartialXp && selection.Soldier.Id == 2, "partial XP does not mix metrics");
PromotionRecord R(Selection s, int badge = 0, SquaddieServiceRecord service = null) =>
    LeaderForge.CreateRecord(null, s, badge, service);
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
var vanillaPilot = new UnitLeaderTemplate
{
    Id = "squad_leader.pilot", IsInfantry = false, UnitTitle = donorTitle, UnitDescription = donorUnitDescription,
};
Check(vanillaPilot.GetUnitTitle() == donorTitle && vanillaPilot.GetUnitDescription() == donorUnitDescription,
    "vanilla pilot getter results are untouched");
var incompletePromotion = new UnitLeaderTemplate { Id = textRecord.LeaderId, InfantryUnitTemplate = textSource };
Check(incompletePromotion.GetUnitTitle() == donorTitle && incompletePromotion.GetUnitDescription() == donorUnitDescription,
    "missing promoted text preserves the original getter result");
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
    liveLedger.Records[0].LeaderId == LeaderForge.Last.Installed[0] && system.Context.State.Saves == 1, "promotion recorded in Jiangyu mod state");
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
Check(Redeem(preview).Skip && liveLedger.Records.Count == 2 && liveLedger.Records[1].BadgeIndex == 1, "next promotion gets the next badge");
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
    LeaderForge.Last.Refreshed.SequenceEqual(savedLedger.Records.Select(r => r.LeaderId)) && Log.Warnings.Count == 0,
    "every saved leader is rebuilt before and refreshed after roster load");
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
Check(textClone.GetUnitTitle() == donorTitle && textClone.GetUnitDescription() == donorUnitDescription,
    "unloaded display callbacks leave native results untouched");
Open();
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "unloaded callbacks cannot stock");
if (args.Length > 0)
{
    string root = Path.Combine(args[0], "mods/jiangyu/SquaddieFieldPromotions");
    using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "jiangyu.json")));
    string version = manifest.RootElement.GetProperty("version").GetString();
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
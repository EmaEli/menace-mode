using SquaddieFieldPromotions;
using Jiangyu.Sdk;
using MenaceModSettings;
using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Tools;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Strategy;

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks++;
}
Candidate C(int id, int missions = 0, int? xp = null, bool assigned = false) =>
    new(id, "Soldier", "Nickname", 0, 1, missions, xp, assigned);
void Reject(Action action, string name)
{
    try { action(); } catch (InvalidDataException) { Check(true, name); return; }
    throw new Exception("FAILED (accepted invalid data): " + name);
}

Check(PromotionRules.Choose(Array.Empty<Candidate>()) == null, "empty roster");
var selection = PromotionRules.Choose(new[] { C(2, 20, 5), C(3, 0, 100, true), C(1, 0, 100) });
Check(selection.UsesXp && selection.Soldier.Id == 1, "highest XP and lowest-ID tie");
Check(selection.LivingCount == 3, "assigned and unassigned included");
Check(PromotionRules.Choose(new[] { C(4, xp: 200, assigned: true), C(1, xp: 2) }).Soldier.Id == 4, "assigned top XP eligible");
selection = PromotionRules.Choose(new[] { C(1, 2), C(3, 9), C(2, 9) });
Check(!selection.UsesXp && !selection.PartialXp && selection.Soldier.Id == 2, "no Veterancy: native missions with ID tie");
selection = PromotionRules.Choose(new[] { C(1, 2, 999), C(2, 3) });
Check(!selection.UsesXp && selection.PartialXp && selection.Soldier.Id == 2, "partial XP does not mix metrics");
PromotionRecord R(Selection s, int badge = 0) => LeaderForge.CreateRecord(null, s, badge);
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
foreach (var (xp, bonus) in new[] { (0,0), (24,0), (25,2), (49,2), (50,4), (99,4), (100,6), (149,6), (150,8), (249,8), (250,10), (499,10), (500,12), (int.MaxValue,12) })
{
    var attributes = PromotionRules.Attributes(xp);
    Check(attributes[0] == 65 + bonus && attributes[5] == 5 + bonus / 2 && attributes.Length == 7, "XP tier " + xp);
}
var defaults = PromotionRules.Attributes(null);
defaults[0] = 0;
Check(PromotionRules.Attributes(null)[0] == 65, "attributes never shared");
Check(PromotionRules.Describe(null, null).Contains("No leader or soldier was changed"), "empty result is explicitly non-mutating");
Check(PromotionRules.Describe(selection, R(selection)).Contains("65 / 65 / 62"), "partial XP mode uses baseline attributes");
Check(PromotionRules.Describe(excluded, R(excluded)).Contains("1 already promoted skipped"), "skipped promoted soldiers reported");

// Ledger / record validation (managed values only).
var ledger = new PromotionLedger();
Check(ledger.Validated().Count == 0 && ledger.NextBadgeIndex() == 0 && ledger.PromotedSquaddieIds().Count == 0, "empty ledger");
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
// Jiangyu sidecar layout: { "<type full name>": blob }. Must round-trip through ReadLedger.
var sidecarJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = ledger },
    new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
var reread = PromotionSystem.ReadLedger(sidecarJson);
Check(reread.Validated().Count == 2 && reread.Records[1].LeaderId == ledger.Records[1].LeaderId &&
    reread.Records[1].InitialAttributes.SequenceEqual(ledger.Records[1].InitialAttributes), "sidecar round-trip");
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
system.OnTemplatesApplied();
Check(preview.Title.GetRawDefaultTranslation() == "Field Promotion Dossier", "native localized dossier title");
Check(preview.ShortName.GetRawDefaultTranslation() == "Field promotion", "native localized short name");
Check(preview.Description.GetRawDefaultTranslation().Contains("source soldier stays in the roster"), "native localized safety description");
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
Check(!ModSettings.Values["DebugLogging"] && Log.InfoLines.Count == 0, "silent by default");
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
Check(price.Skip && (int)price.Result == PromotionSystem.PreviewPrice && PromotionSystem.PreviewPrice >= 1,
    "preview price fixed and never below the native merchant-list minimum of 1");
Check(marketScreen.m_TempFilteredRegularOffers.Any(i => i.Template == preview), "preview survives native price filter");
price = new PatchInfo { Instance = vanilla, Result = 25 };
prefixes["GetTradeValue"](price);
Check(!price.Skip && (int)price.Result == 25, "vanilla price untouched");
Check(!Redeem(vanilla).Skip && vanilla.VanillaRedeems == 1, "vanilla redemption untouched");
NativeCandidates.Value = selection;
var liveLedger = system.Context.State.Get<PromotionLedger>();
Check(Redeem(preview).Skip && preview.VanillaRedeems == 0, "custom redemption NEVER calls vanilla");
Check(UIManager.Current.Last.Text.Contains("is now a squad leader"), "promotion result visible");
Check(!UIManager.Current.Last.Label.enableRichText, "names cannot inject markup");
Check(LeaderForge.Last.Installed.Count == 1 && StrategyState.Current.Roster.Hirable.Count == 1, "leader installed once into Hire");
Check(liveLedger.Records.Count == 1 && liveLedger.Records[0].SourceSquaddieId == selection.Soldier.Id &&
    liveLedger.Records[0].LeaderId == LeaderForge.Last.Installed[0] && system.Context.State.Saves == 1, "promotion recorded in Jiangyu mod state");
Check(NativeCandidates.LastExcluded.Count == 0, "first promotion excludes nobody");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("No eligible living squaddie"), "promoted soldier is never promoted twice");
Check(NativeCandidates.LastExcluded.SetEquals(new[] { selection.Soldier.Id }) && liveLedger.Records.Count == 1 &&
    LeaderForge.Last.Installed.Count == 1, "second redemption changes nothing");
NativeCandidates.Value = excluded;
LeaderForge.FailInstall = true;
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("Field promotion failed") && liveLedger.Records.Count == 1,
    "native clone failure keeps the ledger unchanged and tells the player");
LeaderForge.FailInstall = false;
Check(Redeem(preview).Skip && liveLedger.Records.Count == 2 && liveLedger.Records[1].BadgeIndex == 1, "next promotion gets the next badge");
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
PatchInfo Load(string path) { var info = new PatchInfo { Args = new object[] { new Il2CppMenace.Strategy.SaveState { FilePath = path } } }; prefixes["ExecLoad"](info); return info; }
Log.Warnings.Clear();
Check(!Load(savePath).Skip && LeaderForge.Last.Rehydrated.Count == 0 && Log.Warnings.Count == 0, "save without sidecar loads silently");
Check(!Load(null).Skip && Log.Warnings.Count == 0, "missing save path ignored");
var savedLedger = new PromotionLedger();
foreach (var r in liveLedger.Records) savedLedger.Records.Add(r);
File.WriteAllText(savePath + PromotionSystem.SidecarSuffix, System.Text.Json.JsonSerializer.Serialize(
    new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = savedLedger }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
system.Context.Coroutines.RunAll(); // Drain the pending (donor-less) voice wait so a new one can be scheduled.
Load(savePath);
Check(LeaderForge.Last.Rehydrated.SequenceEqual(savedLedger.Records.Select(r => r.LeaderId)) && Log.Warnings.Count == 0, "every saved leader rebuilt before roster load");
Check(system.Context.Coroutines.Started.Count == 1, "rebuilt leaders schedule voice binding");
var broken = R(PromotionRules.Choose(new[] { C(9, 1) }), 5); broken.Name = "Broken";
savedLedger.Records.Add(broken);
File.WriteAllText(savePath + PromotionSystem.SidecarSuffix, System.Text.Json.JsonSerializer.Serialize(
    new Dictionary<string, object> { [typeof(PromotionLedger).FullName] = savedLedger }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
LeaderForge.Last.Rehydrated.Clear();
Load(savePath);
Check(LeaderForge.Last.Rehydrated.Count == 2 && Log.Warnings.Count == 1 && Log.Warnings[0].Contains("Broken"), "one broken record never blocks the others");
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
preview.Price = PromotionSystem.PreviewPrice;
system.OnUnload();
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
Console.WriteLine($"PASS: {checks} behavior checks (production rules and callbacks; native APIs simulated).");
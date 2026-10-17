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
Check(PromotionRules.Describe(selection).Contains("Incomplete XP coverage"), "partial-coverage reason visible");
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
Check(PromotionRules.Describe(null).Contains("no leader is created"), "empty review is explicitly non-mutating");
Check(PromotionRules.Describe(selection).Contains("65 / 65 / 62"), "partial XP mode uses baseline attributes");

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
Check(preview.Title.GetRawDefaultTranslation() == "Field Promotion Dossier (PREVIEW)", "native localized preview title");
Check(preview.ShortName.GetRawDefaultTranslation() == "Promotion review", "native localized short name");
Check(preview.Description.GetRawDefaultTranslation().Contains("Does NOT recruit"), "native localized safety description");
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
ModSettings.Set("EnableDossierPreview", true);
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
Check(Redeem(preview).Skip && preview.VanillaRedeems == 0, "custom redemption NEVER calls vanilla");
Check(UIManager.Current.Last.Text.Contains("PREVIEW ONLY"), "review visible with no recruitment action");
Check(!UIManager.Current.Last.Label.enableRichText, "names cannot inject markup");
NativeCandidates.Fail = true;
Check(Redeem(preview).Skip && preview.VanillaRedeems == 0, "exception still blocks native unlock");
Redeem(preview);
Check(Log.Warnings.Count == 1, "actionable errors are one-shot");
NativeCandidates.Fail = false;
StrategyState.Current.BlackMarket.AddItem(vanilla.CreateItem("vanilla"), 0);
ModSettings.Set("EnableDossierPreview", false);
Check(StrategyState.Current.BlackMarket.Items.Count == 1 && StrategyState.Current.BlackMarket.Items[0].Template == vanilla, "disable removes ONLY preview stock");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("disabled"), "existing dossier disabled safely");
StrategyState.Current.Ironman = true;
ModSettings.Set("EnableDossierPreview", true);
Check(!StrategyState.Current.BlackMarket.ContainsItem(preview), "no stock in Ironman");
Check(Redeem(preview).Skip && UIManager.Current.Last.Text.Contains("non-Ironman"), "Ironman review blocked");
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
}
Console.WriteLine($"PASS: {checks} behavior checks (production rules and callbacks; native APIs simulated).");
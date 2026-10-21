using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Strategy;
using MenaceModSettings;

int checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) throw new Exception(name);
}
BaseItemTemplate Item(string id, int market = 0) => new() { Id = id, BlackMarketMaxQuantity = market };
var kpac = Item("weapon.generic_assault_rifle_tier1_kpac_grenade_launcher", 1);
var craft = Item("crafted.weapon");
var reward = Item("reward.weapon");
var initial = Item("initial.weapon");
var commodity = new CommodityTemplate { Id = "commodity.spare_parts" };
var enemy = Item("enemy.turret");
var future = Item("future.weapon", 1); future.ContentVisibility = ContentVisibilityType.FutureContent;
var dev = Item("dev.weapon", 1); dev.ContentVisibility = ContentVisibilityType.Dev;
var dossier = new Redeemable { Id = "dossier", BlackMarketMaxQuantity = 1 };
var blueprint = new BlueprintTemplate { Id = "blueprint", Result = craft,
    Materials = new() { new BlueprintMaterial { Template = commodity } } };
DataTemplateLoader.Set<BaseItemTemplate>(kpac, enemy, future, dev, dossier, commodity, null, kpac, Item("", 1));
DataTemplateLoader.Set(blueprint);
DataTemplateLoader.Set(new RewardTableTemplate { Items = new() { reward, kpac, future, dossier, null } },
    new RewardTableTemplate { ContentVisibility = ContentVisibilityType.Dev, Items = new() { enemy } });
DataTemplateLoader.Set(new GlobalDifficultyTemplate { InitialAdditionalUnlockedItems = new[] { initial } });
var state = new StrategyState(); StrategyState.Current = state;
var owned = state.OwnedItems;
Check(!InventoryDebugActions.TryGetCampaign(out _), "Singleton alone is insufficient");
Check(InventoryDebugActions.Grant(state.Pointer).StartsWith("Not applied"), "Title/loading blocked");
UIManager.Current.Screen = new Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase();
Check(InventoryDebugActions.Grant(state.Pointer).StartsWith("Not applied"), "Tactical screen blocked");
Check(owned.Calls == 0 && state.CheatMarks == 0, "Blocked actions make no changes");
UIManager.Current.Screen = new StrategyUIScreen();
Check(InventoryDebugActions.TryGetCampaign(out _), "Campaign screen available");
Check(InventoryDebugActions.Grant((IntPtr)99).StartsWith("Not applied"), "Stale campaign confirmation blocked");
StrategyState.Current = null;
Check(!InventoryDebugActions.TryGetCampaign(out _), "No campaign blocked");
StrategyState.Current = state;
owned.Stock[kpac.Id] = new() { Items = new() { new() { Equipped = true }, new(), new() { Temporary = true } } };
owned.Stock[commodity.Id] = new() { Items = Enumerable.Range(0, 15).Select(_ => new BaseItem()).ToList() };
string result = InventoryDebugActions.Grant(state.Pointer);
Check(result.Contains("0 failed") && result.Contains("6 item types"), "Unique full catalog: " + result);
Check(owned.GetInstances(kpac).Items.Count == 12, "Ten reserve plus existing equipped/temporary copies");
Check(owned.GetInstances(kpac).Items[0].Equipped, "Equipped copy preserved");
Check(owned.GetInstances(commodity).Items.Count == 15, "Stock above ten preserved");
foreach (var t in new BaseItemTemplate[] { craft, reward, initial, blueprint })
    Check(owned.GetInstances(t).Items.Count == 10, "Craft/reward/starting/blueprint included: " + t.Id);
foreach (var t in new[] { enemy, future, dev, dossier })
    Check(!owned.Stock.ContainsKey(t.Id), "Internal or auto-redeemable excluded: " + t.Id);
Check(state.CheatMarks == 1, "Native cheat flag once before writes");
int calls = owned.Calls;
result = InventoryDebugActions.Grant(state.Pointer);
Check(result.Contains("0 copies added") && owned.Calls == calls, "Repeated clicks are idempotent");
Check(state.CheatMarks == 1, "No-op does not mark cheat again");
owned.Stock[kpac.Id].Items.RemoveAt(2); // Temporary copy; should not change reserve.
owned.Stock[craft.Id].Items.RemoveAt(0);
result = InventoryDebugActions.Grant(state.Pointer);
Check(result.Contains("1 copies added"), "Refill only missing reserve");
owned.Stock[craft.Id].Items.Clear(); owned.Fail.Add(craft.Id);
owned.Stock[reward.Id].Items.Clear(); owned.Throw.Add(reward.Id);
var diagnosticLog = new List<string>();
result = InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add);
Check(result.Contains("2 failed") && result.Contains("Partial changes"), "Null and throwing AddItem reported");
Check(result.Contains("crafted.weapon [add item]: AddItem returned no item."), "Null AddItem identifies item and reason");
Check(result.Contains("reward.weapon [add item]: Exception: add failure"), "Thrown AddItem identifies item and exception");
Check(diagnosticLog.Any(line => line.Contains("2 failed")), "Diagnostic summary written on explicit request");
Check(diagnosticLog.Any(line => line.Contains("crafted.weapon [add item]: AddItem returned no item.")), "Null failure logged");
Check(diagnosticLog.Any(line => line.Contains("reward.weapon [add item]: Exception: add failure")), "Exception failure logged");
owned.Stock[kpac.Id].ThrowOnEnumerate = true;
diagnosticLog.Clear();
InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add);
Check(diagnosticLog.Any(line => line.Contains(kpac.Id + " [read current stock]: Exception: stock enumeration failure")),
    "Native stock-read failure distinguished from addition failure");
owned.Stock[kpac.Id].ThrowOnEnumerate = false;
state.ThrowOnCheatMark = true;
diagnosticLog.Clear(); calls = owned.Calls;
InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add);
Check(diagnosticLog.Any(line => line.Contains("[mark campaign cheated]: Exception: cheat flag failure")) && owned.Calls == calls,
    "Cheat flag failure logged and prevents additions");
state.ThrowOnCheatMark = false;
owned.Fail.Clear(); owned.Throw.Clear();
Check(InventoryDebugActions.Grant(state.Pointer, _ => throw new Exception("logger failure")).Contains("20 copies added"),
    "Broken diagnostic logger cannot prevent top-up or duplicate additions");
diagnosticLog.Clear();
Check(InventoryDebugActions.Grant((IntPtr)99, diagnosticLog.Add).StartsWith("Not applied") && diagnosticLog.Count == 0,
    "Stale diagnostic confirmation makes no changes or diagnostic calls");
DataTemplateLoader.Throw = true; calls = owned.Calls;
Check(InventoryDebugActions.Grant(state.Pointer).Contains("could not complete") && owned.Calls == calls,
    "Catalog failure before first write");
Check(InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add).Contains("while reading item catalogs (Exception: catalog failure)"),
    "Catalog failure reports phase and exception");
Check(diagnosticLog.Any(line => line.Contains("reading item catalogs: System.Exception: catalog failure")),
    "Catalog diagnostic includes full exception");
DataTemplateLoader.Throw = false; DataTemplateLoader.Catalog.Clear();
diagnosticLog.Clear();
Check(InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add).StartsWith("Not applied"), "Empty catalog not reported as success");
Check(diagnosticLog.Last().Contains("no obtainable item templates"), "Empty catalog reason logged");
var manyItems = Enumerable.Range(0, 25).Select(n => Item($"failed.{n:00}", 1)).ToArray();
DataTemplateLoader.Set<BaseItemTemplate>(manyItems);
foreach (var item in manyItems) owned.Fail.Add(item.Id);
diagnosticLog.Clear();
result = InventoryDebugActions.Grant(state.Pointer, diagnosticLog.Add);
Check(result.Contains("0 copies added") && result.Contains("25 failed"), "All-item failure reproduced with stand-ins");
Check(result.Contains("first 5 shown; 20 more not shown"), "UI diagnostics bounded independently");
Check(diagnosticLog.Count(line => line.StartsWith("Inventory debug failure:")) == 20, "Log failure details bounded to twenty");
Check(diagnosticLog.Last().Contains("5 further failures omitted"), "Log omission count correct");
for (int count = 0; count <= 25; count++)
{
    int additions = 0, marks = 0;
    var r = InventoryDebugStock.TopUp(new[] { "a", "a", " " }, x => x, _ => count,
        _ => { additions++; return true; }, () => marks++);
    Check(r.Types == 1 && r.Failed == 0 && additions == Math.Max(0, 10 - count), "Top-up arithmetic " + count);
    Check(marks == (count < 10 ? 1 : 0), "Cheat callback " + count);
}
var negative = InventoryDebugStock.TopUp(new[] { "a" }, x => x, _ => -1, _ => throw new Exception(), () => { });
Check(negative.Failed == 1 && negative.Added == 0, "Invalid counts fail closed");
Check(negative.Failures[0].ItemId == "a" && negative.Failures[0].Stage == "read current stock"
    && negative.Failures[0].Details == "InvalidOperationException: Invalid reserve count.", "Reserve failure details retained");
Console.WriteLine($"Inventory debug checks: {checks} passed (managed stand-ins, not gameplay)");
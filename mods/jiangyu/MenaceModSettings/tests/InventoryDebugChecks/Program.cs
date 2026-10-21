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
result = InventoryDebugActions.Grant(state.Pointer);
Check(result.Contains("2 failed") && result.Contains("Partial changes"), "Null and throwing AddItem reported");
owned.Fail.Clear(); owned.Throw.Clear();
Check(InventoryDebugActions.Grant(state.Pointer).Contains("20 copies added"), "Retry fills just failed items");
DataTemplateLoader.Throw = true; calls = owned.Calls;
Check(InventoryDebugActions.Grant(state.Pointer).Contains("could not complete") && owned.Calls == calls,
    "Catalog failure before first write");
DataTemplateLoader.Throw = false; DataTemplateLoader.Catalog.Clear();
Check(InventoryDebugActions.Grant(state.Pointer).StartsWith("Not applied"), "Empty catalog not reported as success");
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
Console.WriteLine($"Inventory debug checks: {checks} passed (managed stand-ins, not gameplay)");
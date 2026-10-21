using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Strategy;
using Il2CppInterop.Runtime.InteropTypes;

namespace MenaceModSettings;

internal static class InventoryDebugActions
{
    internal static bool TryGetCampaign(out StrategyState state)
    {
        state = null;
        try
        {
            // A strategy singleton also exists during combat/title transitions. Require an
            // actual open strategy screen; never grant from a tactical or loading screen.
            if (UIManager.Get()?.GetActiveScreen()?.TryCast<StrategyUIScreen>() == null) return false;
            state = StrategyState.Get();
            return state?.OwnedItems != null;
        }
        catch { return false; }
    }

    internal static string Grant(IntPtr confirmedCampaign, Action<string> diagnostics = null)
    {
        if (!TryGetCampaign(out var state) || state.Pointer != confirmedCampaign)
            return "Not applied: return to the campaign screen and confirm again.";
        // Only the explicit diagnostic confirmation supplies a logger. Normal play is silent.
        void Report(string message)
        {
            try { diagnostics?.Invoke(message); }
            catch { /* Logging must never interrupt or repeat an inventory operation. */ }
        }
        string phase = "reading item catalogs";
        try
        {
            Report("Inventory debug: confirmed diagnostic action; reading item catalogs.");
            // Complete the read-only catalog before changing any inventory. Do not use
            // Resources.FindObjectsOfTypeAll: native templates live in the loader cache.
            var templates = CollectObtainable();
            if (templates.Count == 0)
            {
                const string empty = "Not applied: no obtainable item templates found.";
                Report(empty);
                return empty;
            }
            var owned = state.OwnedItems;
            phase = "topping up campaign inventory";
            var result = InventoryDebugStock.TopUp(templates, t => t.GetID(),
                t => CountReserve(owned, t), t => owned.AddItem(t, false, false) != null,
                () => state.SetHasCheatBeenUsed());
            string summary = $"Debug inventory: {result.Added} copies added; {result.Types} item types checked; "
                + $"{result.Failed} failed. Reopen the Armory/Market to refresh.";
            Report(summary);
            const int maxLogged = 20;
            foreach (var failure in result.Failures.Take(maxLogged))
                Report($"Inventory debug failure: {failure.ItemId} [{failure.Stage}]: {failure.Details}");
            if (result.Failed > maxLogged)
                Report($"Inventory debug: {result.Failed - maxLogged} further failures omitted.");
            return summary
                + (result.Failed > 0 ? FormatFailures(result) : "");
        }
        catch (Exception error)
        {
            Report($"Inventory debug could not complete while {phase}: {error}");
            return $"Debug inventory could not complete while {phase} "
                + $"({error.GetType().Name}: {error.Message}). Some changes may remain; retry only fills missing copies.";
        }
    }

    private static string FormatFailures(InventoryDebugStock.Result result)
    {
        const int maxShown = 5;
        string details = string.Join("; ", result.Failures.Take(maxShown)
            .Select(failure => $"{failure.ItemId} [{failure.Stage}]: {failure.Details}"));
        string additional = result.Failed > maxShown
            ? $" (first {maxShown} shown; {result.Failed - maxShown} more not shown)" : "";
        return $" Partial changes remain; retry only fills missing copies. Failures: {details}{additional}";
    }

    private static int CountReserve(OwnedItems owned, BaseItemTemplate template)
    {
        // Equipped items are preserved and do not consume the ten reserve copies.
        int count = 0;
        var items = owned.GetInstances(template);
        if (items == null) return 0;
        // Native List<BaseItem>.Enumerator.MoveNext fails through the non-generic
        // IL2CPP bridge. Use IReadOnlyList's indexer and its separate inherited count
        // interface (the proxies do not flatten it). Finish counting before any AddItem.
        var collection = items.TryCast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<BaseItem>>();
        if (collection == null) throw new InvalidOperationException("Native stock count unavailable");
        int length = collection.Count;
        for (int index = 0; index < length; index++)
        {
            var item = items[index];
            if (item != null && item.GetContainer() == null && !item.IsTemporary()) count++;
        }
        return count;
    }

    private static IEnumerable<T> ReadNative<T>(Il2CppObjectBase collection) where T : Il2CppObjectBase
    {
        // Il2CppInterop does not flatten inherited collection interfaces. Enumerate through
        // native non-generic IEnumerable, as the suite template-loader shim does.
        var enumerable = collection?.TryCast<Il2CppSystem.Collections.IEnumerable>();
        if (enumerable == null) throw new InvalidOperationException("Native catalog collection unavailable");
        var iterator = enumerable.GetEnumerator();
        while (iterator.MoveNext())
        {
            var item = iterator.Current?.TryCast<T>();
            if (item != null) yield return item;
        }
    }

    private static List<BaseItemTemplate> CollectObtainable()
    {
        var result = new Dictionary<string, BaseItemTemplate>(StringComparer.Ordinal);
        void Include(BaseItemTemplate item)
        {
            // OwnedItems.AddItem automatically REDEEMS IRedeemable (dossiers, tokens,
            // vouchers, recruits). They cannot be stocked and must not be invoked here.
            if (item == null || item.ContentVisibility != ContentVisibilityType.Regular
                || item.TryCast<IRedeemable>() != null) return;
            string id = item.GetID();
            if (!string.IsNullOrWhiteSpace(id)) result.TryAdd(id, item);
        }

        foreach (var item in ReadNative<BaseItemTemplate>(DataTemplateLoader.GetAll<BaseItemTemplate>()))
        {
            if (item == null) continue;
            if (item.BlackMarketMaxQuantity > 0 || item.TryCast<CommodityTemplate>() != null)
                Include(item);
        }
        foreach (var table in ReadNative<RewardTableTemplate>(DataTemplateLoader.GetAll<RewardTableTemplate>()))
        {
            if (table == null || table.ContentVisibility != ContentVisibilityType.Regular || table.Items == null) continue;
            foreach (var item in table.Items) Include(item);
        }
        foreach (var blueprint in ReadNative<BlueprintTemplate>(DataTemplateLoader.GetAll<BlueprintTemplate>()))
        {
            if (blueprint == null || blueprint.ContentVisibility != ContentVisibilityType.Regular) continue;
            Include(blueprint);
            Include(blueprint.GetCraftingResult());
            var materials = blueprint.GetCraftingMaterials();
            if (materials != null)
                foreach (var material in materials) Include(material.Template);
        }
        foreach (var difficulty in ReadNative<GlobalDifficultyTemplate>(DataTemplateLoader.GetAll<GlobalDifficultyTemplate>()))
        {
            if (difficulty == null || difficulty.ContentVisibility != ContentVisibilityType.Regular
                || difficulty.InitialAdditionalUnlockedItems == null) continue;
            foreach (var item in difficulty.InitialAdditionalUnlockedItems) Include(item);
        }
        return result.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
    }
}
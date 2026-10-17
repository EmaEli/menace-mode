using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Strategy;
using Jiangyu.Sdk;
using MenaceModSettings;

namespace SquaddieFieldPromotions;

/// <summary>
/// Native dossier -> native review dialog. No runtime leader templates, recruitment,
/// save hooks, input polling, marker files or custom permanent UI panels.
/// </summary>
public sealed class PromotionSystem : JiangyuSystem
{
    internal const string DossierId = "mod.squaddiefieldpromotions.field_promotion_dossier";
    internal const int PreviewStockTimeout = 3;
    // BlackMarketUIScreen.FilterItems drops merchant items whose GetTradeValue() < 1.
    internal const int PreviewPrice = 1;
    private const string Group = "Squaddie Field Promotions";
    private const string DossierType = "Il2CppMenace.Items.DossierItemTemplate";
    private bool _ready;
    private bool _unloaded;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    public override void OnInit()
    {
        ModSettings.Register(Group, settings =>
        {
            settings.AddHeader("Native dossier preview - recruitment is not enabled");
            settings.AddToggle("EnableDossierPreview", "Offer 1-credit promotion-review dossier in Black Market", false);
            settings.AddHeader("Uses Veterancy XP when complete; otherwise native missions. No extra mod required.");
            settings.AddToggle("DebugLogging", "Diagnostic logging (OFF by default)", false);
        });
        // Assert exact game signatures before registering any hook. Never stock if
        // the native redemption guard cannot be installed.
        Require(typeof(DossierItemTemplate), "Redeem", Type.EmptyTypes, typeof(void));
        Require(typeof(DossierItemTemplate), "GetTradeValue", Type.EmptyTypes, typeof(int));
        Require(typeof(BlackMarket), "Restock", new[] { typeof(bool), typeof(bool) }, typeof(void));
        Require(typeof(BlackMarketUIScreen), "OnOpened", new[] { typeof(IOpenScreenParams) }, typeof(void));
        Context.Patches.Prefix(DossierType, "Redeem", 0, Review);
        Context.Patches.Prefix(DossierType, "GetTradeValue", 0, FreePreviewPrice);
        Context.Patches.Postfix("Il2CppMenace.Strategy.BlackMarket", "Restock", 2, _ =>
        {
            Stock();
            RefreshActiveMarket();
        });
        // Populate first, then stock and rebuild the native buy list. This keeps the
        // model update and the visible market window in sync on open and restock.
        Context.Patches.Postfix("Il2CppMenace.UI.Strategy.BlackMarketUIScreen", "OnOpened", 1, MarketOpened);
        ModSettings.Changed += SettingChanged;
        _ready = true;
    }

    public override void OnTemplatesApplied()
    {
        if (_unloaded) return;
        try { PrepareTemplate(); }
        catch (Exception error) { WarnOnce("text", "Cannot initialize preview dossier text: " + error.Message); }
    }

    private static LocalizedLine PreviewText(string field, string text)
    {
        // Never edit a localized object inherited from the vanilla donor. Give the
        // new object its own localization key, so donor translations cannot win.
        var line = new LocalizedLine(LocaCategory.Items, field, false);
        line.SetId(DossierId);
        line.SetDefaultTranslation(text);
        return line;
    }

    private static LocalizedMultiLine PreviewDescription(string text)
    {
        var line = new LocalizedMultiLine(LocaCategory.Items, "Description", false);
        line.SetId(DossierId);
        line.SetDefaultTranslation(text);
        return line;
    }

    private static DossierItemTemplate PrepareTemplate()
    {
        var template = DataTemplateLoader.Get<DossierItemTemplate>(DossierId, false);
        if (template == null) return null;
        if (template.GetID() != DossierId)
            throw new InvalidOperationException("Preview lookup returned a different dossier.");
        template.Title = PreviewText("Title", "Field Promotion Dossier (PREVIEW)");
        template.ShortName = PreviewText("ShortName", "Promotion review");
        template.Description = PreviewDescription(
            "Free candidate review. Uses complete Veterancy XP if available, otherwise native missions. " +
            "Does NOT recruit, remove a soldier, or create a leader. Enable in Menace Mod Settings; " +
            "test on a disposable non-Ironman campaign.");
        return template;
    }

    public override void OnUnload()
    {
        _unloaded = true;
        _ready = false;
        ModSettings.Changed -= SettingChanged;
        // Context owns patch lifetime. Never mutate a live roster or destroy objects.
    }

    private static bool IsPreview(object instance) =>
        instance is DossierItemTemplate template && template.GetID() == DossierId;

    private static void FreePreviewPrice(PatchInfo info)
    {
        if (!IsPreview(info.Instance)) return;
        info.Result = PreviewPrice; // Dossiers calculate prices independently of BaseItemTemplate.TradeValue.
        info.Skip = true;
    }

    private void Review(PatchInfo info)
    {
        if (!IsPreview(info.Instance)) return;
        info.Skip = true; // Always before ANY fallible lookup: never unlock a random vanilla leader.
        try
        {
            var state = StrategyState.Get();
            string text;
            if (_unloaded || !_ready || !Enabled)
                text = "Promotion review is disabled in Menace Mod Settings. No leader or soldier was changed.";
            else if (state == null || state.Squaddies == null || state.IsIronman())
                text = "Open a disposable non-Ironman campaign to review a candidate. No leader or soldier was changed.";
            else
            {
                var selection = NativeCandidates.Read(state); // Fresh snapshot at redemption, not stale market-open data.
                text = PromotionRules.Describe(selection);
                Debug(selection == null ? "Review: no living candidate." :
                    $"Review: source={selection.Soldier.Id}, criterion={selection.Criterion}, living={selection.LivingCount}.");
            }
            var ui = UIManager.Get() ?? throw new InvalidOperationException("Native UI manager unavailable.");
            var dialog = ui.AddSimpleDialog();
            dialog.Init("SFP.PromotionReview", "FIELD PROMOTION - PREVIEW ONLY", text,
                "CLOSE", null, null, null, false);
            dialog.GetTextLabel().enableRichText = false; // Soldier names are text, not markup.
        }
        catch (Exception error)
        {
            WarnOnce("review", "Cannot show promotion review; no leader or soldier changed. " + error.Message);
        }
    }

    private void Stock()
    {
        if (!_ready || _unloaded) return;
        try
        {
            var state = StrategyState.Get();
            if (state?.BlackMarket == null) return;
            var template = PrepareTemplate(); // Also covers existing stock and loaded inventory.
            if (template == null)
            {
                if (Enabled) WarnOnce("template", "Promotion dossier template missing; no stock added. Check Jiangyu template self-check.");
                return;
            }
            if (!Enabled || state.IsIronman())
            {
                RemovePreviewStock(state.BlackMarket);
                return;
            }
            if (state.BlackMarket.ContainsItem(template)) return;
            // Empty donor list is a second barrier against accidental vanilla unlocks.
            if (template.m_UnlockedLeaders == null || template.m_UnlockedLeaders.Length != 0 ||
                template.GetTradeValue() != PreviewPrice)
                throw new InvalidOperationException("Preview template safety/price validation failed.");
            var item = template.CreateItem(BaseItemTemplate.CreateGuid());
            if (item == null || item.GetBaseItemTemplate()?.GetID() != DossierId)
                throw new InvalidOperationException("Native preview item creation failed.");
            state.BlackMarket.AddItem(item, PreviewStockTimeout);
            if (!state.BlackMarket.ContainsItem(template))
                throw new InvalidOperationException("Native stock insertion readback failed.");
            Debug($"Promotion-review dossier stocked in native Black Market (price {PreviewPrice}, timeout {PreviewStockTimeout}).");
        }
        catch (Exception error)
        {
            WarnOnce("stock", "Promotion dossier unavailable: " + error.Message);
        }
    }

    private static void RemovePreviewStock(BlackMarket market)
    {
        // Enumerate both offer categories first, then remove ONLY this exact template.
        var items = new Il2CppSystem.Collections.Generic.List<BaseItem>();
        var previews = new Dictionary<string, BaseItem>(StringComparer.Ordinal);
        foreach (bool special in new[] { false, true })
        {
            items.Clear();
            market.GetInstances(items, special);
            for (int i = 0; i < items.Count; i++)
                if (items[i]?.GetBaseItemTemplate()?.GetID() == DossierId)
                    previews[items[i].GetGuid()] = items[i];
        }
        foreach (var item in previews.Values) market.RemoveItem(item);
    }

    private void MarketOpened(PatchInfo info)
    {
        Stock();
        try { RefreshMarketWindow(info.Instance as BlackMarketUIScreen); }
        catch (Exception error) { WarnOnce("refresh", "Cannot refresh Black Market after opening: " + error.Message); }
    }

    private void RefreshActiveMarket()
    {
        try
        {
            var market = UIManager.Get()?.GetActiveScreen()?.TryCast<BlackMarketUIScreen>();
            RefreshMarketWindow(market);
        }
        catch (Exception error) { WarnOnce("refresh", "Cannot refresh active Black Market: " + error.Message); }
    }

    private void RefreshMarketWindow(BlackMarketUIScreen market)
    {
        if (market == null) return;
        market.UpdateWindow();
        if (!ModSettings.Get<bool>(Group, "DebugLogging")) return;

        var state = StrategyState.Get();
        if (state?.BlackMarket == null) return;
        var regular = new Il2CppSystem.Collections.Generic.List<BaseItem>();
        var special = new Il2CppSystem.Collections.Generic.List<BaseItem>();
        state.BlackMarket.GetInstances(regular, false);
        state.BlackMarket.GetInstances(special, true);
        int regularPreview = CountPreviewItems(regular);
        int specialPreview = CountPreviewItems(special);
        // m_TempFilteredRegularOffers is what UpdateItemSlots renders in the merchant grid.
        var visible = market.m_TempFilteredRegularOffers;
        int visibleCount = visible == null ? 0 : visible.Count;
        int visiblePreview = visible == null ? 0 : CountPreviewItems(visible);
        Debug($"Market UI after refresh: normal={regularPreview}, special={specialPreview}, merchant-visible={visibleCount}, preview-visible={visiblePreview}.");
    }

    private static int CountPreviewItems(Il2CppSystem.Collections.Generic.List<BaseItem> items)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i]?.GetBaseItemTemplate()?.GetID() == DossierId)
                count++;
        return count;
    }

    private void SettingChanged(string group, string key, object value)
    {
        if (group != Group || key != "EnableDossierPreview") return;
        Stock();
        RefreshActiveMarket();
    }

    private static bool Enabled => ModSettings.Get<bool>(Group, "EnableDossierPreview");
    private static void Debug(string text)
    {
        if (ModSettings.Get<bool>(Group, "DebugLogging")) Log.Info("[SFP] " + text);
    }

    private void WarnOnce(string key, string text)
    {
        if (_reported.Add(key)) Log.Warn("[SFP] " + text);
    }

    private static void Require(Type type, string name, Type[] parameters, Type result)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameters, null);
        if (method?.ReturnType != result)
            throw new NotSupportedException("Unsupported native API: " + type.FullName + "." + name);
    }
}
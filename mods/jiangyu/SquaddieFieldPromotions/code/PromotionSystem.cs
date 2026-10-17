using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
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
/// Native Black Market dossier -> promotes the best living squaddie to a hirable squad leader.
/// Promotions are recorded in Jiangyu's per-save mod state and their native templates are
/// rebuilt before the roster deserializes, so hired and hirable promoted leaders survive load.
/// No input polling, marker files or custom permanent UI panels.
/// </summary>
public sealed class PromotionSystem : JiangyuSystem
{
    internal const string DossierId = "mod.squaddiefieldpromotions.field_promotion_dossier";
    internal const int PreviewStockTimeout = 3;
    // BlackMarketUIScreen.FilterItems drops merchant items whose GetTradeValue() < 1.
    internal const int PreviewPrice = 1;
    internal const string Group = "Squaddie Field Promotions";
    internal const string EnableKey = "EnableFieldPromotions";
    // Jiangyu's public sidecar convention (ModStateSidecar.PathFor): <save>.jiangyu.<modId>.json.
    internal const string SidecarSuffix = ".jiangyu.SquaddieFieldPromotions.json";
    private const string DossierType = "Il2CppMenace.Items.DossierItemTemplate";
    private bool _ready;
    private bool _unloaded;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private LeaderForge _forge;
    private bool _voiceCoroutineScheduled;
    private bool _voiceAttempted;

    public override void OnInit()
    {
        ModSettings.Register(Group, settings =>
        {
            settings.AddHeader("Field promotions - test on a disposable non-Ironman campaign");
            settings.AddToggle(EnableKey, "Offer 1-credit Field Promotion Dossier in Black Market", false);
            settings.AddHeader("Uses Veterancy XP when complete; otherwise native missions. No extra mod required.");
            settings.AddToggle("DebugLogging", "Diagnostic logging (OFF by default)", false);
        });
        // Assert exact game signatures before registering any hook. Never stock if
        // the native redemption guard cannot be installed.
        Require(typeof(DossierItemTemplate), "Redeem", Type.EmptyTypes, typeof(void));
        Require(typeof(DossierItemTemplate), "GetTradeValue", Type.EmptyTypes, typeof(int));
        Require(typeof(BlackMarket), "Restock", new[] { typeof(bool), typeof(bool) }, typeof(void));
        Require(typeof(BlackMarketUIScreen), "OnOpened", new[] { typeof(IOpenScreenParams) }, typeof(void));
        Require(typeof(Roster), "AddHirableLeader", new[] { typeof(UnitLeaderTemplate) }, typeof(void));
        Require(typeof(SaveState), "GetFilePath", Type.EmptyTypes, typeof(string));
        RequireStatic(typeof(SaveSystem), "ExecLoad", new[] { typeof(SaveState) }, typeof(void));
        _forge = new LeaderForge(Context.ModFolder);
        // Templates must exist before StrategyState.ProcessSaveState resolves the roster, so the
        // ledger is read directly from the sidecar here; Jiangyu's own state load runs in postfix.
        Context.Patches.Prefix("Il2CppMenace.Strategy.SaveSystem", "ExecLoad", 1, Rehydrate);
        Context.Patches.Prefix(DossierType, "Redeem", 0, Promote);
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
        catch (Exception error) { WarnOnce("text", "Cannot initialize dossier text: " + error.Message); }
        ScheduleVoices();
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Strategy" || sceneName == "MissionPreparation") ScheduleVoices();
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
        template.Title = PreviewText("Title", "Field Promotion Dossier");
        template.ShortName = PreviewText("ShortName", "Field promotion");
        template.Description = PreviewDescription(
            "Promotes your best living squaddie to a hirable squad leader (0 credits in Hire). " +
            "Uses complete Veterancy XP if available, otherwise native missions. The source soldier " +
            "stays in the roster. Promoted leaders are restored on load; test on a disposable non-Ironman campaign.");
        return template;
    }

    public override void OnUnload()
    {
        _unloaded = true;
        _ready = false;
        ModSettings.Changed -= SettingChanged;
        _forge?.Unload();
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

    private void Promote(PatchInfo info)
    {
        if (!IsPreview(info.Instance)) return;
        info.Skip = true; // Always before ANY fallible lookup: never unlock a random vanilla leader.
        string text;
        try
        {
            var state = StrategyState.Get();
            if (_unloaded || !_ready || !Enabled)
                text = "Field promotions are disabled in Menace Mod Settings. No leader or soldier was changed.";
            else if (state == null || state.Squaddies == null || state.Roster == null || state.IsIronman())
                text = "Open a disposable non-Ironman campaign to promote a squaddie. No leader or soldier was changed.";
            else
            {
                var ledger = Context.State.Get<PromotionLedger>();
                // Fresh snapshot at redemption, not stale market-open data; already promoted soldiers are skipped.
                var selection = NativeCandidates.Read(state, ledger.PromotedSquaddieIds());
                if (selection == null) text = PromotionRules.Describe(null, null);
                else
                {
                    if (!_forge.AssetsPresent)
                        throw new InvalidOperationException("Bundled portrait assets are missing; reinstall the mod.");
                    var record = LeaderForge.CreateRecord(state, selection, ledger.NextBadgeIndex());
                    string detail = _forge.Install(state, record);
                    ledger.Records.Add(record); // Persisted by Jiangyu beside the next game save.
                    Context.State.Save();
                    text = PromotionRules.Describe(selection, record);
                    Debug($"Promoted: source={selection.Soldier.Id}, criterion={selection.Criterion}, living={selection.LivingCount}, {detail}");
                    ScheduleVoices();
                }
            }
        }
        catch (Exception error)
        {
            WarnOnce("promote:" + error.GetType().Name, "Field promotion failed; no leader was added. " + error.Message);
            text = "Field promotion failed: " + error.Message + "\nNo leader was added and no soldier was changed.";
        }
        try
        {
            var ui = UIManager.Get() ?? throw new InvalidOperationException("Native UI manager unavailable.");
            var dialog = ui.AddSimpleDialog();
            dialog.Init("SFP.FieldPromotion", "FIELD PROMOTION", text, "CLOSE", null, null, null, false);
            dialog.GetTextLabel().enableRichText = false; // Soldier names are text, not markup.
        }
        catch (Exception error)
        {
            WarnOnce("dialog", "Cannot show promotion dialog: " + error.Message);
        }
    }

    private void Rehydrate(PatchInfo info)
    {
        if (_unloaded) return;
        string path = null;
        try
        {
            path = (info.Args.Count > 0 ? info.Args[0] as SaveState : null)?.GetFilePath();
            if (string.IsNullOrEmpty(path)) return;
            string sidecar = path + SidecarSuffix;
            if (!File.Exists(sidecar)) return;
            var ledger = ReadLedger(File.ReadAllText(sidecar));
            if (ledger == null) return;
            int restored = 0, failed = 0;
            foreach (var record in ledger.Validated())
            {
                try
                {
                    string detail = _forge.Rehydrate(record);
                    restored++;
                    Debug("Rehydrated " + record.LeaderId + ": " + detail);
                }
                catch (Exception error)
                {
                    failed++;
                    WarnOnce("rehydrate:" + record.LeaderId, "Promoted leader " + record.Name + " could not be rebuilt; " +
                        "if hired, it may be missing from the loaded roster. " + error.Message);
                }
            }
            if (restored + failed > 0) Debug($"Load {Path.GetFileName(path)}: {restored} promoted leader(s) rebuilt, {failed} failed.");
            if (restored > 0) ScheduleVoices();
        }
        catch (Exception error)
        {
            WarnOnce("rehydrate:" + path, "Promotion ledger for this save is unreadable; promoted leaders were not rebuilt. " + error.Message);
        }
    }

    internal static PromotionLedger ReadLedger(string json)
    {
        // Same layout Jiangyu writes: { "<type full name>": { ...blob... }, ... }.
        var options = new JsonSerializerOptions { IncludeFields = true };
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
        if (map == null || !map.TryGetValue(typeof(PromotionLedger).FullName, out var blob)) return null;
        return blob.Deserialize<PromotionLedger>(options);
    }

    private void ScheduleVoices()
    {
        if (_unloaded || _voiceAttempted || _voiceCoroutineScheduled || _forge == null) return;
        if (!Enabled && !_forge.HasPendingVoices) return;
        _voiceCoroutineScheduled = true;
        try { Context.Coroutines.Start(PrepareVoicesWhenReady()); }
        catch (Exception error) { _voiceCoroutineScheduled = false; WarnOnce("voice", "Voice setup not scheduled: " + error.Message); }
    }

    private IEnumerator PrepareVoicesWhenReady()
    {
        // The audio loader is one-shot: wait (silently) until the strategy singleton and the
        // vanilla gendered donor templates exist, then prepare exactly once.
        bool available = false;
        for (int frame = 0; frame < 300 && !_unloaded; frame++)
        {
            try
            {
                var lim = StrategyState.Get() == null ? null : DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.lim", false);
                var sy = lim == null ? null : DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.sy", false);
                available = lim != null && sy != null && lim.SpeakerTemplate != null && sy.SpeakerTemplate != null;
            }
            catch { available = false; }
            if (available) break;
            yield return null;
        }
        _voiceCoroutineScheduled = false;
        if (!available || _voiceAttempted || _unloaded) yield break;
        _voiceAttempted = true;
        try
        {
            if (!_forge.TryPrepareVoices(out string status))
                WarnOnce("voice", "John/Jane voices not ready; promoted leaders keep the native donor voice: " + status);
        }
        catch (Exception error)
        {
            WarnOnce("voice", "Voice setup failed; promoted leaders keep the native donor voice: " + error.Message);
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
        if (group != Group || key != EnableKey) return;
        Stock();
        RefreshActiveMarket();
        ScheduleVoices();
    }

    private static bool Enabled => ModSettings.Get<bool>(Group, EnableKey);
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

    private static void RequireStatic(Type type, string name, Type[] parameters, Type result)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
        if (method?.ReturnType != result)
            throw new NotSupportedException("Unsupported native API: " + type.FullName + "." + name);
    }
}
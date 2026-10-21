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
    internal const int DefaultPrice = 100;
    internal const int PriceStep = 10;
    internal const int MaximumPrice = 1000;
    internal const string PriceKey = "PromotionPrice";
    internal const string Group = "Squaddie Field Promotions";
    internal const string EnableKey = "EnableFieldPromotions";
    internal const string GenericHeadKey = "GenericLeaderHeads";
    internal const string CustomPortraitPreviewKey = "PreviewMaleBadgeAPortrait";
    internal const string CleanupTestSaveKey = "CleanupPromotedLeadersFromTestSave";
    // Jiangyu's public sidecar convention (ModStateSidecar.PathFor): <save>.jiangyu.<modId>.json.
    internal const string SidecarSuffix = ".jiangyu.SquaddieFieldPromotions.json";
    private const string DossierType = "Il2CppMenace.Items.DossierItemTemplate";
    private bool _ready;
    private bool _unloaded;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private LeaderForge _forge;
    private MissionServiceSnapshot _missionEnding;
    private IReadOnlyList<PromotionRecord> _recordsToRefresh;
    private bool _voiceCoroutineScheduled;
    private bool _voiceAttempted;
    private string _loadedSavePath;
    private object _cleanupCoroutine;
    private bool _strategySceneLoaded;
    private bool _cleanupAttempted;
    private SaveState _rosterLoadState;
    private Roster _loadedRoster;
    private bool _nativeLoadCompleted;

    public override void OnInit()
    {
        ModSettings.Register(Group, settings =>
        {
            settings.AddHeader("Field promotions - test on a disposable non-Ironman campaign");
            settings.AddToggle(EnableKey, "Offer Field Promotion Dossier in Black Market", false);
            settings.AddNumber(PriceKey, "Promotion dossier price (credits)", PriceStep, MaximumPrice, DefaultPrice, PriceStep);
            settings.AddToggle(GenericHeadKey, "Use generic heads for promoted leaders", true);
            settings.AddToggle(CustomPortraitPreviewKey,
                "EXPERIMENTAL: custom portrait for male badge A (fresh game launch required)", false);
            settings.AddHeader("!!! DANGER: BACK UP test.save BEFORE ENABLING !!!");
            settings.AddHeader("REMOVES ALL SFP LEADERS FROM EVERY ROSTER STATE AND DEPLOYMENT, THEN SAVES test.save.");
            settings.AddToggle(CleanupTestSaveKey, "I HAVE A BACKUP - REMOVE ALL SFP LEADERS FROM test.save (ONE-SHOT)", false);
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
        Require(typeof(UnitLeaderTemplate), "GetUnitTitle", Type.EmptyTypes, typeof(LocalizedLine));
        Require(typeof(UnitLeaderTemplate), "GetUnitDescription", Type.EmptyTypes, typeof(LocalizedMultiLine));
        Require(typeof(SaveState), "GetFilePath", Type.EmptyTypes, typeof(string));
        Require(typeof(SaveState), "IsLoading", Type.EmptyTypes, typeof(bool));
        Require(typeof(SaveState), "Close", Type.EmptyTypes, typeof(void));
        Require(typeof(Roster), "ProcessSaveState", new[] { typeof(SaveState) }, typeof(void));
        RequireStatic(typeof(SaveSystem), "ExecLoad", new[] { typeof(SaveState) }, typeof(void));
        RequireStatic(typeof(SaveSystem), "Save", new[] { typeof(SaveStateType), typeof(string), typeof(string) }, typeof(void));
        var missionEndArgs = new[] { typeof(Mission), typeof(Il2CppSystem.Collections.Generic.List<BaseUnitLeader>) };
        RequireStatic(typeof(Squaddies), "OnMissionEnd", missionEndArgs, typeof(void));
        _forge = new LeaderForge(Context.ModFolder);
        // Templates must exist before StrategyState.ProcessSaveState resolves the roster, so the
        // ledger is read directly from the sidecar here; Jiangyu's own state load runs in postfix.
        Context.Patches.Prefix("Il2CppMenace.Strategy.SaveSystem", "ExecLoad", 1, Rehydrate);
        // ExecLoad only schedules a coroutine. Even SceneLoaded precedes roster deserialization.
        Context.Patches.Postfix("Il2CppMenace.Strategy.Roster", "ProcessSaveState", 1, NativeRosterLoaded);
        Context.Patches.Postfix("Il2CppMenace.Strategy.SaveState", "Close", 0, NativeSaveClosed);
        Context.Patches.Prefix("Il2CppMenace.Strategy.Squaddies", "OnMissionEnd", 2, MissionEnding);
        Context.Patches.Postfix("Il2CppMenace.Strategy.Squaddies", "OnMissionEnd", 2, MissionEnded);
        Context.Patches.Prefix(DossierType, "Redeem", 0, Promote);
        Context.Patches.Prefix(DossierType, "GetTradeValue", 0, DossierPrice);
        // Infantry getters read the shared InfantryUnitTemplate, not the leader's fields.
        // Override only our leaders; never change the shared vanilla entity template.
        Context.Patches.Postfix("Il2CppMenace.Strategy.UnitLeaderTemplate", "GetUnitTitle", 0, LeaderTitle);
        Context.Patches.Postfix("Il2CppMenace.Strategy.UnitLeaderTemplate", "GetUnitDescription", 0, LeaderDescription);
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
        _strategySceneLoaded = sceneName == "Strategy";
        if (sceneName == "Strategy" || sceneName == "MissionPreparation") ScheduleVoices();
        if (_strategySceneLoaded) ScheduleTestSaveCleanup();
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
            "is removed from their squad and the reserve after a successful promotion. " +
            "Promoted leaders are restored on load; test on a disposable non-Ironman campaign.");
        return template;
    }

    public override void OnUnload()
    {
        _unloaded = true;
        _ready = false;
        ModSettings.Changed -= SettingChanged;
        if (_cleanupCoroutine != null)
        {
            Context.Coroutines.Stop(_cleanupCoroutine);
            _cleanupCoroutine = null;
        }
        _forge?.Unload();
        // Context owns patch lifetime. Never mutate a live roster or destroy objects.
    }

    private static bool IsPreview(object instance) =>
        instance is DossierItemTemplate template && template.GetID() == DossierId;

    internal static int NormalizePrice(int value) => System.Math.Clamp(value, PriceStep, MaximumPrice) / PriceStep * PriceStep;
    private static int Price => NormalizePrice(ModSettings.Get<int>(Group, PriceKey));

    private static void DossierPrice(PatchInfo info)
    {
        if (!IsPreview(info.Instance)) return;
        info.Result = Price; // Dossiers calculate prices independently of BaseItemTemplate.TradeValue.
        info.Skip = true;
    }

    private UnitLeaderTemplate PromotedTemplate(PatchInfo info) =>
        !_unloaded && info.Instance is UnitLeaderTemplate leader &&
        leader.GetID()?.StartsWith(PromotionRecord.LeaderPrefix, StringComparison.Ordinal) == true
            ? leader : null;

    private void LeaderTitle(PatchInfo info)
    {
        var leader = PromotedTemplate(info);
        if (leader?.UnitTitle != null) info.Result = leader.UnitTitle;
    }

    private void LeaderDescription(PatchInfo info)
    {
        var leader = PromotedTemplate(info);
        if (leader?.UnitDescription != null) info.Result = leader.UnitDescription;
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
                ReconcilePreviewSources(state, ledger);
                // Fresh snapshot at redemption, not stale market-open data; already promoted soldiers are skipped.
                var selection = NativeCandidates.Read(state, ledger.PromotedSquaddieIds());
                if (selection == null) text = PromotionRules.Describe(null, null);
                else
                {
                    if (!_forge.AssetsPresent)
                        throw new InvalidOperationException("Bundled portrait assets are missing; reinstall the mod.");
                    var record = LeaderForge.CreateRecord(state, selection, ledger.NextBadgeIndex(),
                        ledger.FindServiceRecord(selection.Soldier.Id));
                    var source = new SourceSquaddieRemoval(state, record);
                    if (!source.Present) throw new InvalidOperationException("Selected source soldier no longer exists.");
                    string detail;
                    try
                    {
                        detail = _forge.Install(state, record, ForceGenericHeads);
                        source.Remove();
                        record.SourceRemoved = true;
                        ledger.Records.Add(record); // Persisted beside the next game save, with the native pool.
                        Context.State.Save();
                    }
                    catch (Exception failure)
                    {
                        ledger.Records.Remove(record);
                        // Attempt both rollbacks even if restoring one native collection fails.
                        Exception rollbackFailure = null;
                        try { source.Restore(); }
                        catch (Exception error) { rollbackFailure = error; }
                        try { _forge.RollbackInstall(state, record); }
                        catch (Exception error) { rollbackFailure = new AggregateException(rollbackFailure ?? failure, error); }
                        if (rollbackFailure != null) throw new PromotionRollbackException(rollbackFailure);
                        throw;
                    }
                    NotifySourceChanged(source);
                    text = PromotionRules.Describe(selection, record);
                    Debug($"Promoted: source={selection.Soldier.Id}, sourceRemoved={record.SourceRemoved}, poolAlive={state.Squaddies.m_AliveSquaddies.Count}, criterion={selection.Criterion}, living={selection.LivingCount}, {detail}");
                    ScheduleVoices();
                }
            }
        }
        catch (Exception error)
        {
            WarnOnce("promote:" + error.GetType().Name + ":" + error.Message, "Field promotion failed. " + error.Message);
            text = "Field promotion failed: " + error.Message + (error is PromotionRollbackException
                ? "\nDo not save this session. Reload the previous save before continuing."
                : "\nThe attempted promotion was not committed.");
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

    private void NotifySourceChanged(SourceSquaddieRemoval source)
    {
        try { source.NotifyChanged(); }
        catch (Exception error) { WarnOnce("squaddie-ui", "Promotion committed, but the squad UI notification failed: " + error.Message); }
    }

    private void ReconcilePreviewSources(StrategyState state, PromotionLedger ledger)
    {
        // Upgrade the user's 0.9.x test saves when opening the market, not in the early load prefix.
        // Never delete a soldier for an orphaned ledger record or a mismatched identity.
        foreach (var record in ledger.Validated())
        {
            if (record.SourceRemoved || !LeaderForge.RosterContainsLeader(state, record.LeaderId)) continue;
            var source = new SourceSquaddieRemoval(state, record);
            try
            {
                source.Remove();
                record.SourceRemoved = true;
                Context.State.Save();
            }
            catch
            {
                record.SourceRemoved = false;
                try { source.Restore(); }
                catch (Exception error) { throw new PromotionRollbackException(error); }
                throw;
            }
            NotifySourceChanged(source);
            Debug("Removed preview source: " + record.SourceSquaddieId);
        }
    }

    private void Rehydrate(PatchInfo info)
    {
        _recordsToRefresh = null;
        if (_cleanupCoroutine != null)
        {
            Context.Coroutines.Stop(_cleanupCoroutine);
            _cleanupCoroutine = null;
        }
        _loadedSavePath = null;
        _cleanupAttempted = false;
        _rosterLoadState = null;
        _loadedRoster = null;
        _nativeLoadCompleted = false;
        if (_unloaded) return;
        string path = null;
        try
        {
            path = (info.Args.Count > 0 ? info.Args[0] as SaveState : null)?.GetFilePath();
            _loadedSavePath = path;
            if (string.IsNullOrEmpty(path)) return;
            string sidecar = path + SidecarSuffix;
            if (!File.Exists(sidecar)) return;
            var ledger = ReadLedger(File.ReadAllText(sidecar));
            if (ledger == null) return;
            var recordsToRefresh = new List<PromotionRecord>();
            int restored = 0, failed = 0;
            foreach (var record in ledger.Validated())
            {
                try
                {
                    string detail = _forge.Rehydrate(record, ForceGenericHeads);
                    restored++;
                    recordsToRefresh.Add(record);
                    Debug("Rehydrated " + record.LeaderId + ": " + detail);
                }
                catch (Exception error)
                {
                    failed++;
                    WarnOnce("rehydrate:" + record.LeaderId, "Promoted leader " + record.Name + " could not be rebuilt; " +
                        "if hired, it may be missing from the loaded roster. " + error.Message);
                }
            }
            _recordsToRefresh = recordsToRefresh;
            if (restored + failed > 0) Debug($"Load {Path.GetFileName(path)}: {restored} promoted leader(s) rebuilt, {failed} failed.");
            if (restored > 0) ScheduleVoices();
        }
        catch (Exception error)
        {
            WarnOnce("rehydrate:" + path, "Promotion ledger for this save is unreadable; promoted leaders were not rebuilt. " + error.Message);
        }
    }

    private void NativeRosterLoaded(PatchInfo info)
    {
        if (_unloaded || _nativeLoadCompleted || string.IsNullOrEmpty(_loadedSavePath)) return;
        var save = info.Args.Count > 0 ? info.Args[0] as SaveState : null;
        var roster = info.Instance as Roster;
        var activeRoster = StrategyState.Get()?.Roster;
        if (save == null || !save.IsLoading() || roster == null || activeRoster == null ||
            roster.Pointer != activeRoster.Pointer ||
            !string.Equals(save.GetFilePath(), _loadedSavePath, StringComparison.OrdinalIgnoreCase)) return;
        _rosterLoadState = save;
        _loadedRoster = roster;
    }

    private void NativeSaveClosed(PatchInfo info)
    {
        var save = info.Instance as SaveState;
        if (_unloaded || _nativeLoadCompleted || save == null || _rosterLoadState == null ||
            save.Pointer != _rosterLoadState.Pointer || !save.IsLoading() ||
            !IsLoadedRoster(StrategyState.Get()?.Roster)) return;
        // StrategyState closes this exact loading stream after ALL model processors, including
        // BattlePlan. Header-only reads and saving streams must never release the cleanup gate.
        _nativeLoadCompleted = true;
        _rosterLoadState = null;
        Debug("Native save load completed after roster and deployment deserialization.");
        RehydrateAfterLoad();
    }

    private bool IsLoadedRoster(Roster roster) =>
        roster != null && _loadedRoster != null && roster.Pointer == _loadedRoster.Pointer;

    private void RehydrateAfterLoad()
    {
        var records = _recordsToRefresh;
        _recordsToRefresh = null;
        if (_unloaded) return;

        int refreshed = 0, failed = 0;
        if (records != null)
        {
            foreach (var record in records)
            {
                try
                {
                    if (_forge.RefreshIdentity(record)) refreshed++;
                    else failed++;
                }
                catch (Exception error)
                {
                    failed++;
                    WarnOnce("rehydrate-text:" + record.LeaderId,
                        "Promoted leader title/description could not be refreshed after load. " + error.Message);
                }
            }
        }
        if (refreshed > 0) Debug("Refreshed and verified native displayed leader title/description after load: " + refreshed + ".");
        if (failed > 0)
            WarnOnce("rehydrate-text", "Some promoted leader titles/descriptions could not be refreshed after load.");
        if (_strategySceneLoaded) ScheduleTestSaveCleanup();
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
                template.GetTradeValue() != Price)
                throw new InvalidOperationException("Preview template safety/price validation failed.");
            var item = template.CreateItem(BaseItemTemplate.CreateGuid());
            if (item == null || item.GetBaseItemTemplate()?.GetID() != DossierId)
                throw new InvalidOperationException("Native preview item creation failed.");
            state.BlackMarket.AddItem(item, PreviewStockTimeout);
            if (!state.BlackMarket.ContainsItem(template))
                throw new InvalidOperationException("Native stock insertion readback failed.");
            Debug($"Promotion-review dossier stocked in native Black Market (price {Price}, timeout {PreviewStockTimeout}).");
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
        if (!_ready || _unloaded) return;
        try
        {
            var state = StrategyState.Get();
            if (state?.Squaddies != null && state.Roster != null && !state.IsIronman())
                ReconcilePreviewSources(state, Context.State.Get<PromotionLedger>());
        }
        catch (Exception error) { WarnOnce("preview-sources", "Cannot remove an old preview source soldier: " + error.Message); }
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
        if (group == Group && key == CleanupTestSaveKey)
        {
            if (value is bool enabled && enabled) ScheduleTestSaveCleanup();
            return;
        }
        if (group == Group && key == GenericHeadKey)
        {
            ApplyGenericHeadMode();
            return;
        }
        if (group != Group || (key != EnableKey && key != PriceKey)) return;
        Stock();
        RefreshActiveMarket();
        ScheduleVoices();
    }

    private void ScheduleTestSaveCleanup()
    {
        if (_unloaded || !_strategySceneLoaded || !_nativeLoadCompleted ||
            string.IsNullOrEmpty(_loadedSavePath) || _cleanupCoroutine != null ||
            !ModSettings.Get<bool>(Group, CleanupTestSaveKey)) return;
        _cleanupCoroutine = Context.Coroutines.Start(AfterFrames(2, () =>
        {
            _cleanupCoroutine = null;
            TryCleanupTestSave();
        }));
    }

    private static System.Collections.IEnumerator AfterFrames(int frames, Action action)
    {
        for (int i = 0; i < frames; i++) yield return null;
        action();
    }

    private void TryCleanupTestSave()
    {
        if (_unloaded || _cleanupAttempted || !_strategySceneLoaded || !_nativeLoadCompleted ||
            !IsLoadedRoster(StrategyState.Get()?.Roster) ||
            !ModSettings.Get<bool>(Group, CleanupTestSaveKey)) return;
        _cleanupAttempted = true;

        var removedHirable = new List<KeyValuePair<int, UnitLeaderTemplate>>();
        var removedHired = new List<KeyValuePair<int, BaseUnitLeader>>();
        var removedDismissed = new List<KeyValuePair<int, BaseUnitLeader>>();
        var removedUnburied = new List<KeyValuePair<int, BaseUnitLeader>>();
        var removedBuried = new List<KeyValuePair<int, BaseUnitLeader>>();
        var removedDeployment = new List<KeyValuePair<int, DeployedEntity>>();
        bool saveStarted = false;
        bool saveCompleted = false;
        Roster roster = null;
        Il2CppSystem.Collections.Generic.List<DeployedEntity> deployment = null;
        try
        {
            ModSettings.Set(Group, CleanupTestSaveKey, false);
            if (ModSettings.Get<bool>(Group, CleanupTestSaveKey))
                throw new InvalidOperationException("The one-time cleanup setting could not be turned off.");
            Log.Info("[SFP] test.save cleanup request disarmed before preflight.");

            if (!string.Equals(Path.GetFileName(_loadedSavePath), "test.save", StringComparison.OrdinalIgnoreCase))
            {
                WarnOnce("cleanup-target", "Cleanup was enabled, but the loaded save is not test.save; no roster was changed.");
                return;
            }
            var state = StrategyState.Get();
            roster = state?.Roster;
            if (state == null || roster == null || state.IsIronman())
            {
                WarnOnce("cleanup-state", "test.save cleanup requires a loaded non-Ironman strategy roster; no roster was changed.");
                return;
            }

            var records = Context.State.Get<PromotionLedger>().Validated();
            if (records.Count == 0)
            {
                WarnOnce("cleanup-empty", "test.save has no SFP promotion records; no roster was changed.");
                return;
            }
            var leaderIds = new List<string>(records.Count);
            foreach (var record in records)
            {
                if (!record.SourceRemoved)
                {
                    WarnOnce("cleanup-source", "Cleanup stopped because a promotion record does not confirm source removal; no roster was changed.");
                    return;
                }
                leaderIds.Add(record.LeaderId);
            }

            deployment = state.BattlePlan?.m_EntitiesToDeploy
                ?? throw new InvalidOperationException("Native deployment list unavailable; cleanup was not applied.");
            var targets = new HashSet<string>(leaderIds, StringComparer.Ordinal);
            var plan = PromotionRosterCleanup.BuildPlan(leaderIds, ReadRosterEntries(roster));
            if (!plan.CanApply)
            {
                WarnOnce("cleanup-preflight", "test.save cleanup stopped: " + plan.BlockReason);
                return;
            }
            int deploymentTargets = CountDeploymentTargets(deployment, targets);
            Log.Info("[SFP] Loaded test.save cleanup preflight: records=" + records.Count +
                ", hirable=" + plan.HirableIds.Count + ", hired=" + plan.HiredIds.Count +
                ", dismissed=" + plan.DismissedIds.Count + ", unburied=" + plan.UnburiedIds.Count +
                ", buried=" + plan.BuriedIds.Count + ", deployment=" + deploymentTargets + ".");
            if (!HasPlannedEntries(plan) && deploymentTargets == 0)
            {
                Log.Info("[SFP] test.save cleanup found no promoted leaders in the loaded roster or deployment; no change or save was needed.");
                return;
            }

            RemoveDeployments(deployment, targets, removedDeployment);
            RemoveTemplates(roster.m_HirableLeaders, new HashSet<string>(plan.HirableIds, StringComparer.Ordinal), removedHirable);
            RemoveLeaders(roster.m_HiredLeaders, new HashSet<string>(plan.HiredIds, StringComparer.Ordinal), removedHired);
            RemoveLeaders(roster.m_DismissedLeaders, new HashSet<string>(plan.DismissedIds, StringComparer.Ordinal), removedDismissed);
            RemoveLeaders(roster.m_UnburiedLeaders, new HashSet<string>(plan.UnburiedIds, StringComparer.Ordinal), removedUnburied);
            RemoveLeaders(roster.m_BuriedLeaders, new HashSet<string>(plan.BuriedIds, StringComparer.Ordinal), removedBuried);
            var verification = PromotionRosterCleanup.BuildPlan(leaderIds, ReadRosterEntries(roster));
            if (!verification.CanApply || HasPlannedEntries(verification) || CountDeploymentTargets(deployment, targets) != 0)
                throw new InvalidOperationException("Native roster readback still contains a promoted leader after removal.");

            saveStarted = true;
            SaveSystem.Save(SaveStateType.Manual, _loadedSavePath, Path.GetFileNameWithoutExtension(_loadedSavePath));
            saveCompleted = true;

            var savedRoster = PromotionRosterCleanup.BuildPlan(leaderIds, ReadRosterEntries(roster));
            if (!savedRoster.CanApply || HasPlannedEntries(savedRoster) || CountDeploymentTargets(deployment, targets) != 0)
                throw new InvalidOperationException("Native roster readback failed after the manual save.");

            Log.Info("[SFP] Saved test.save after removing promoted leader entries from all roster states: hirable=" +
                removedHirable.Count + ", hired=" + removedHired.Count + ", dismissed=" + removedDismissed.Count +
                ", unburied=" + removedUnburied.Count + ", buried=" + removedBuried.Count +
                ", deployment=" + removedDeployment.Count +
                ". Promotion sidecar retained for safe load recovery.");
        }
        catch (Exception error)
        {
            if (!saveStarted || !saveCompleted)
            {
                RestoreEntries(roster?.m_HirableLeaders, removedHirable);
                RestoreEntries(roster?.m_HiredLeaders, removedHired);
                RestoreEntries(roster?.m_DismissedLeaders, removedDismissed);
                RestoreEntries(roster?.m_UnburiedLeaders, removedUnburied);
                RestoreEntries(roster?.m_BuriedLeaders, removedBuried);
                RestoreEntries(deployment, removedDeployment);
            }
            WarnOnce("cleanup-failed", "test.save cleanup did not complete: " + error.Message +
                (saveStarted ? " Keep the pre-cleanup backup; the native save call may have started." : " No native save was started."));
        }
        finally
        {
            try
            {
                ModSettings.Set(Group, CleanupTestSaveKey, false);
                if (ModSettings.Get<bool>(Group, CleanupTestSaveKey))
                    throw new InvalidOperationException("The one-time cleanup setting remains enabled.");
            }
            catch (Exception error) { WarnOnce("cleanup-setting", "Could not turn off the one-time cleanup setting: " + error.Message); }
        }
    }

    private static bool HasPlannedEntries(PromotionRosterCleanupPlan plan) =>
        plan.HirableIds.Count > 0 || plan.HiredIds.Count > 0 || plan.DismissedIds.Count > 0 ||
        plan.UnburiedIds.Count > 0 || plan.BuriedIds.Count > 0;

    private static List<PromotionRosterEntry> ReadRosterEntries(Roster roster)
    {
        if (roster?.m_HirableLeaders == null || roster.m_HiredLeaders == null || roster.m_DismissedLeaders == null ||
            roster.m_UnburiedLeaders == null || roster.m_BuriedLeaders == null)
            throw new InvalidOperationException("A native roster state list is unavailable; cleanup was not applied.");

        var entries = new List<PromotionRosterEntry>();
        AddTemplateEntries(roster.m_HirableLeaders, PromotionRosterLocation.Hirable, entries);
        AddLeaderEntries(roster.m_HiredLeaders, PromotionRosterLocation.Hired, entries);
        AddLeaderEntries(roster.m_DismissedLeaders, PromotionRosterLocation.Dismissed, entries);
        AddLeaderEntries(roster.m_UnburiedLeaders, PromotionRosterLocation.Unburied, entries);
        AddLeaderEntries(roster.m_BuriedLeaders, PromotionRosterLocation.Buried, entries);
        return entries;
    }

    private static void AddTemplateEntries(Il2CppSystem.Collections.Generic.List<UnitLeaderTemplate> leaders,
        PromotionRosterLocation location, List<PromotionRosterEntry> entries)
    {
        for (int i = 0; i < leaders.Count; i++)
            if (leaders[i] != null) entries.Add(new PromotionRosterEntry(leaders[i].GetID(), location));
    }

    private static void AddLeaderEntries(Il2CppSystem.Collections.Generic.List<BaseUnitLeader> leaders,
        PromotionRosterLocation location, List<PromotionRosterEntry> entries)
    {
        for (int i = 0; i < leaders.Count; i++)
            if (leaders[i]?.LeaderTemplate != null)
                entries.Add(new PromotionRosterEntry(leaders[i].LeaderTemplate.GetID(), location));
    }

    private static void RemoveTemplates(Il2CppSystem.Collections.Generic.List<UnitLeaderTemplate> leaders,
        HashSet<string> ids, List<KeyValuePair<int, UnitLeaderTemplate>> removed)
    {
        for (int i = leaders.Count - 1; i >= 0; i--)
        {
            var leader = leaders[i];
            if (leader == null || !ids.Contains(leader.GetID())) continue;
            removed.Add(new KeyValuePair<int, UnitLeaderTemplate>(i, leader));
            leaders.RemoveAt(i);
        }
    }

    private static void RemoveLeaders(Il2CppSystem.Collections.Generic.List<BaseUnitLeader> leaders,
        HashSet<string> ids, List<KeyValuePair<int, BaseUnitLeader>> removed)
    {
        for (int i = leaders.Count - 1; i >= 0; i--)
        {
            var leader = leaders[i];
            if (leader?.LeaderTemplate == null || !ids.Contains(leader.LeaderTemplate.GetID())) continue;
            removed.Add(new KeyValuePair<int, BaseUnitLeader>(i, leader));
            leaders.RemoveAt(i);
        }
    }

    private static void RestoreEntries<T>(Il2CppSystem.Collections.Generic.List<T> leaders,
        List<KeyValuePair<int, T>> removed)
    {
        if (leaders == null) return;
        for (int i = removed.Count - 1; i >= 0; i--)
            leaders.Insert(removed[i].Key, removed[i].Value);
    }

    private static int CountDeploymentTargets(Il2CppSystem.Collections.Generic.List<DeployedEntity> deployment,
        HashSet<string> ids)
    {
        int count = 0;
        for (int i = 0; i < deployment.Count; i++)
        {
            string id = deployment[i]?.GetUnitLeader()?.LeaderTemplate?.GetID();
            if (id != null && ids.Contains(id)) count++;
        }
        return count;
    }

    private static void RemoveDeployments(Il2CppSystem.Collections.Generic.List<DeployedEntity> deployment,
        HashSet<string> ids, List<KeyValuePair<int, DeployedEntity>> removed)
    {
        // Native BattlePlan.RemoveUnit only removes this list entry. Keep the exact entity and
        // index rather than recreating it, so rollback also preserves tile position and scale.
        for (int i = deployment.Count - 1; i >= 0; i--)
        {
            var entity = deployment[i];
            string id = entity?.GetUnitLeader()?.LeaderTemplate?.GetID();
            if (id == null || !ids.Contains(id)) continue;
            removed.Add(new KeyValuePair<int, DeployedEntity>(i, entity));
            deployment.RemoveAt(i);
        }
    }

    private static bool Enabled => ModSettings.Get<bool>(Group, EnableKey);
    private static bool ForceGenericHeads => ModSettings.Get<bool>(Group, GenericHeadKey);

    private void ApplyGenericHeadMode()
    {
        if (_forge == null || _unloaded) return;
        try
        {
            bool missingTemplate = false;
            foreach (var record in Context.State.Get<PromotionLedger>().Validated())
                if (!_forge.SetHeadMode(record, ForceGenericHeads))
                    missingTemplate = true;
            if (missingTemplate)
                WarnOnce("head-mode", "Promoted leader templates are not loaded yet; head preference will apply on next load.");
        }
        catch (Exception error) { WarnOnce("head-mode", "Cannot update promoted leader head preference: " + error.Message); }
    }

    private void MissionEnding(PatchInfo info)
    {
        _missionEnding = null;
        if (!_ready || _unloaded) return;
        try
        {
            if (info.Args.Count < 2) throw new InvalidOperationException("Native mission-end arguments unavailable.");
            var mission = info.Args[0] as Mission;
            var leaders = info.Args[1] as Il2CppSystem.Collections.Generic.List<BaseUnitLeader>;
            _missionEnding = SquaddieServiceHistory.Capture(mission, leaders);
        }
        catch (Exception error)
        {
            WarnOnce("service-capture", "Cannot snapshot mission service history: " + error.Message);
        }
    }

    private void MissionEnded(PatchInfo info)
    {
        var snapshot = _missionEnding;
        _missionEnding = null;
        if (snapshot == null || _unloaded) return;
        try
        {
            var ledger = Context.State.Get<PromotionLedger>();
            var previousRecords = ledger.ServiceRecords;
            var previousMissionKeys = ledger.ProcessedMissionKeys;
            if (!SquaddieServiceHistory.Record(ledger, snapshot)) return;
            try { Context.State.Save(); }
            catch
            {
                ledger.ServiceRecords = previousRecords;
                ledger.ProcessedMissionKeys = previousMissionKeys;
                throw;
            }
            Debug("Recorded service history for mission " + snapshot.MissionKey + ".");
        }
        catch (Exception error)
        {
            WarnOnce("service-save", "Mission completed, but its SFP service history could not be saved: " + error.Message);
        }
    }
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

internal sealed class PromotionRollbackException : Exception
{
    internal PromotionRollbackException(Exception inner)
        : base("Could not fully restore the roster after a failed promotion. Do not save; reload the previous save. " + inner.Message, inner) { }
}
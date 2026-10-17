# Changelog

## 0.10.0

- Successful promotion now consumes the exact source squaddie: detach from the assigned squad,
  then remove from the native living pool. Never select a random soldier or mark them killed.
  Source objects and assignment ordering, the Hire offer and the ledger are restored on failure;
  an incomplete rollback explicitly tells the player not to save and to reload.
- Opening Black Market reconciles matching source duplicates from 0.9.x preview saves,
  only for leaders still hired/hirable and matching native soldier identities. Per-save,
  idempotent, with no direct edits to save files or Veterancy data.
- Menace Mod Settings: **Promotion dossier price (credits)**, default **100**, range **10–1000**
  with **10-credit** increments. Applies immediately to existing stock; native trade modifiers
  still apply. Hire remains 0 credits, with no second promotion fee.
- 0.9.2 promotion/persistence verified in game by the user; 0.10.0 gameplay acceptance pending.

## 0.9.2

- Fix: a redemption after a save load failed with `NullReferenceException` in
  `UnityEngine.Sprite.get_texture`. Portrait/badge textures and sprites created at runtime
  were destroyed by the engine's unused-asset unload on the next scene change (rehydration
  runs before the Strategy scene loads). They are now flagged `DontUnloadUnusedAsset`, and a
  cached portrait set whose Unity objects are gone is reloaded from `runtime_assets/`.

## 0.9.1

- Fix: promotion failed with "Native collection IReadOnlyCollection`1: cannot read item 0 of 22".
  `DataTemplateLoader.GetAll<T>()` returns an `IReadOnlyCollection<T>` without an indexer;
  `PrivateMember.Enumerate` now falls back to the native IL2CPP enumerator for such collections.

## 0.9.0

- Dossier redemption now promotes the selected living squaddie to a hirable squad leader:
  unique `UnitLeaderTemplate`/`SpeakerTemplate` cloned from the matching vanilla gendered
  leader, soldier name/nickname/skin, bundled portraits, lettered badge and John/Jane voices
  (recovered 0.6.1 `LeaderForge` path; donor perks/stats are not inherited).
- Promotions are recorded in Jiangyu mod state (`PromotionLedger`) and rebuilt by an early
  prefix on `SaveSystem.ExecLoad` from the sidecar beside the save, before native roster load.
  Missing sidecar is silent; corrupt data or a broken record warns once and never blocks the load.
- Already promoted soldiers are skipped on later redemptions. The source soldier is not removed yet.
- Setting renamed to **Offer 1-credit Field Promotion Dossier in Black Market** (`EnableFieldPromotions`).
- Shared kit/installer now ship a module's raw `runtime_assets/` next to the compiled package.

## 0.8.4

- Fix the preview dossier never appearing in the Black Market: the native
  `BlackMarketUIScreen.FilterItems` removes merchant items with `GetTradeValue() < 1`,
  so the price-zero preview was stocked but hidden. The preview now costs 1 credit.
- Diagnostics report merchant-grid visibility (`m_TempFilteredRegularOffers`) instead of the buy cart.

## 0.8.3

- Stock after native Black Market screen initialization and refresh the native buy list after open/restock.
- Add opt-in diagnostics for normal/special stock counts and buy-list membership; remove stale version text from log prefixes.

## 0.8.2

- Give manually stocked preview dossiers the normal three-restock market timeout instead of zero.
- Add a regression check that catches accidental zero-timeout stock insertion.

## 0.8.1

- Replace rejected raw Title/ShortName/Description template strings with fresh native
  LocalizedLine/LocalizedMultiLine objects and preview-only keys, without mutating vanilla donor text.
- Prepare localization after template application and before market display, including
  existing preview stock. Add regression coverage for donor isolation and wrong-ID lookup.
- No recruitment, persistence or source-soldier removal is enabled by this fix.

## 0.8.0

- Opt-in free native dossier and read-only candidate review through Menace Mod Settings.
- Veterancy XP optional; native mission-count fallback. No gameplay hotkeys.
- Windows build and installation verified; dossier gameplay acceptance pending.

## 0.1 (Jiangyu prototype)

- Ported prototype to Jiangyu native SDK (`JiangyuSystem`, loader-driven hotkeys, `jiangyu.json`, KDL template clone).
- Added F8 read-only Jiangyu control-clone and internal bridge inspection.
- Added explicitly gated F9 laboratory generation of a unique dynamic Squad Leader and SpeakerTemplate, added to vanilla Hire only after ID registration succeeds.
- Reused optional Squaddie Veterancy XP compatibility API, with conservative competent-rookie fallback attributes.
- Avoided running any mutations automatically or while the game is ticking. No recurring logging.
- Kept all squaddie-pool mutations, dossier redemption, cosmetic changes, and persistence out of scope for this first test.
- **Unverified authoring source**: build and live gameplay validation required.

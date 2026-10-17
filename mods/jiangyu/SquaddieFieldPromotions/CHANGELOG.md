# Changelog

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

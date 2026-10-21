# Changelog

## 0.12.1

- Restore the bundled black shields with dynamic promotion letters; remove the rejected
  source-emblem overlay. Applies on reconstruction to existing leaders too, without changing saves.
- Override native GetBadge/GetBadgeWhite only for SFP leaders: tactical infantry otherwise
  reads Singh's shared entity sprites instead of the rewritten leader badge fields.
- Replace the inherited white/status variant with the same letter shield and preserve sprite
  pivot and pixels-per-unit. No shared vanilla templates or acquired perks are changed.
- Add production badge-generation and tactical getter regression checks with simulated Unity/native
  APIs. Generic perks were accepted in game on 0.12.0; this badge correction awaits gameplay testing.

## 0.12.0

- Replace the promoted leader's donor perk tree with the Generic Low Level Squad Leaders
  infantry progression (16 perks across Tiers 1–4) and its generic initial perk. The tree
  assignment changes the template only; already acquired perks remain in the saved leader state.
- Persist the source leader template ID in each promotion record. Rebuilt badges overlay the
  promotion letter on the source unit's emblem, including the tactical/drag variants; nested SFP
  promotions resolve to their original source. Older records without provenance keep generic art.
- Validate the generated progression against the expected native perk IDs and tiers before
  registering a promoted leader. Add save-compatibility and provenance-chain regressions.

## 0.11.8

- Keep the one-time `test.save` cleanup, but make its destructive scope and required backup prominent
  in Menace Mod Settings. It remains OFF by default and one-shot; cleanup behavior is unchanged.
- Record 0.11.7 in-game acceptance: five hirable and three hired leaders removed, native save
  succeeded, and the toggle persisted OFF.
- User reports the current build works in game and turns progress normally; no turn-blocking
  regression was observed.

## 0.11.7

- Fix premature cleanup: `ExecLoad` only schedules loading, and the Strategy scene can arrive before
  roster deserialization. Wait for the active roster's loading processor and closure of that exact
  loading SaveState after all native model processors. Ignore metadata reads, saves, unrelated
  streams and stale/replaced rosters; reset the gate and cancel pending cleanup on every new load.
- Refresh reconstructed identity text only after native model loading, not in the ExecLoad postfix.
- Remove ledger-matching BattlePlan deployment entries alongside roster entries, including orphaned
  deployment references. Preserve exact objects/order/positions for rollback on save failure.
- The 0.11.6 no-match log was not proof of absence: the user confirmed leaders in Hire, squads and
  deployment. Add asynchronous-load regressions and per-state preflight counts. In-game acceptance
  subsequently passed with the 0.11.7 cleanup.

## 0.11.6

- Extend the guarded `test.save` cleanup to remove SFP-ledger IDs from all five native roster lists,
  including hired and casualty states; preserve unrelated leaders and remove duplicate placements
  from every matching list. Duplicate/invalid ledger IDs and unavailable roster lists still abort.
- Verify native roster readback before and after the manual save, and restore all five lists if the
  save does not complete. The promotion sidecar remains available for load recovery.

## 0.11.5

- Refine the `test.save` cleanup to remove only inactive Hire/Dismissed entries while preserving
  hired and casualty states. The follow-up run logged no matches, leading to the broader all-state
  cleanup in 0.11.6; this observation was later found unreliable due to premature roster inspection.

## 0.11.4

- Add an OFF-by-default, one-time cleanup for `test.save`. It plans against all five native
  roster states before mutation, removes only SFP hirable/dismissed entries, and aborts without
  saving if a promoted leader is hired, dead/buried, duplicated, or the save is Ironman.
- Uses MENACE's native manual-save API and retains promotion records in the Jiangyu sidecar;
  the setting turns itself off after the attempt. Back up `test.save` before enabling it.

## 0.11.3

- Align initial-attribute bonuses with Squaddie Veterancy ranks: +0/+2/+4/+6/+8/+10/+12
  at 0/250/500/1000/2000/4000/8000 XP, so only Elite receives the maximum bonus.
- Keep the existing baseline and half-bonus rule for Precision. Existing leaders retain their
  persisted attributes; old records do not contain enough source XP/rank data for safe recalculation.
- Offline tests cover both sides of every rank threshold. This version has not been deployed or
  tested in MENACE.

## 0.11.2

- Fix the actual infantry display path: native `GetUnitTitle` / `GetUnitDescription`
  return shared infantry-template text, ignoring the leader fields changed in 0.11.0–0.11.1.
  Override these getter results only for SFP leaders; vanilla/shared templates stay unchanged.
- Verify rendered localized strings through native getters after load and before a new
  Hire offer. A failed post-load display check warns without preventing roster reconstruction.
- Regression checks reproduce the native infantry indirection and verify donor isolation.
- In-game acceptance pending; 0.11.1 was confirmed ineffective by the user.

## 0.11.1

- Refresh promoted titles and descriptions after native roster loading, and write the generated
  description to both unit and speaker templates. This did not fix the infantry display path.
- Offline checks cover both localized description fields and the post-load refresh path.

## 0.11.0

- Track deployed squaddie service by mission and unit, including wounded/downed participants;
  mission keys make recording idempotent and the versioned Jiangyu sidecar persists the history.
  Tracking starts on upgrade; older saves are not given fabricated unit histories.
- Give each promoted leader a persistent unit designation and a description containing prior
  unit/commander, native lifetime missions, optional Veterancy rank, and tracked unit service.
- Generic heads are enabled by default for promoted leaders, with a setting to restore
  gender-matched custom heads. Existing 0.10.0 ledgers migrate on read.
- In-game acceptance pending.

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

# Changelog

## 0.17.0 — full-inventory BlackOps curation and captioned tactical variants (#73)

- Expand the private BlackOps test from 42 to 211 selected recordings across 167 native slots; 13 unsupported slots remain silent.
- Select audio, exact caption and isolated native Sound ID together for tactical variants, without modifying shared in-flight nodes.
- Preserve legacy same-wording playback, all other 55 profiles/607 recordings, five user wording checks and saved assignments.
- Account for all 1,525 contents/2,460 aliases privately; uncertain recordings and alternate deliveries remain outside the test pool.
- Native timing/speaker presentation is retained. Direct hiring/promotion barks use each route's first clip. Listening/gameplay acceptance remains pending.

## 0.16.0 — integrate the curated private BlackOps voice (#73)

- Add 42 distinct BlackOps recordings across 42 native event slots; the eligible pool is now 31 profiles.
- Replace mismatched visual-contact/recovery lines, remove eight weak or repetitive slots and add eight tactical events.
- Preserve the five user-checked captions, existing saved assignments and silence for unmapped events.
- Audio remains private. The remaining 37 clips and in-game playback still need listening acceptance.

## 0.15.9 — center two promoted-leader slot portraits (#69)

- Raise active and inactive slot crops for `promoted_female_04` and `promoted_male_05`.
- Preserve other portrait files, saved profile assignments, dynamic badges and save data.
- Windows/gameplay acceptance is pending.

## 0.15.8 — curate new voice assignments (#73)

- Restrict new voice assignments to the strongest 30 profiles; keep retired profiles loadable for existing saves.
- Document remaining voice coverage limits; gameplay acceptance is pending.

## 0.15.7 — keep XCOM speakers consistent (#73)

- Replace every missing XCOM event route with explicit silence and an empty caption, not John/Jane.
- Keep valid native sound IDs for tactical, hiring and promotion events; mute both the Sound and
  every variation with randomized volume disabled. Only the assigned profile's clips are referenced.
- Preserve mapped recordings, voice assignments, portraits and save files. Reject incomplete clip
  replacement rather than leaving audible donor variations behind.
- Check all 55 profiles across all 180 event routes. Gameplay acceptance remains pending.

## 0.15.6 — add portrait sets 10–12

- Add male/female portrait set `10` to Brown, `11` to White, and `12` to Black.
- Expand each gender/skin rotation pool to four sets; existing saved profile assignments remain unchanged.

## 0.15.5 — balance portrait rotation to three sets per category

- Place the red-haired `06` sets with Brown, making the pools Brown `01`/`04`/`06`,
  Black `02`/`05`/`08`, and White `03`/`07`/`09` for both genders.
- Keep existing 0.15.3/0.15.4 records that already assigned `06` to White valid and visually unchanged;
  new promotions use the corrected category pools.

## 0.15.4 — integrate portrait candidates 07–09 (#69)

- Add six new approved portrait sets from the latest issue update: male/female `07` (black hair, light
  skin) and `09` (red hair, light skin) join White; male/female `08` (black hair, dark skin) joins Black.
- Expand the rotation to Brown `01`/`04`, Black `02`/`05`/`08`, and White `03`/`06`/`07`/`09` for
  each gender. Existing portrait assignments remain untouched and count toward least-used rotation.
- Validate all 108 deployed portrait PNGs against their candidate files and native dimensions.

## 0.15.3 — six more promoted-leader portrait sets (#69)

- Integrate the approved `promoted_male_04`–`06` and `promoted_female_04`–`06` candidates from `main`
  into `runtime_assets`: two sets per native gender/skin category (Brown 01/04, Black 02/05,
  White 03/06). The 04 sets were assigned to Brown based on their skin tone; 06 sets (red-haired) to White.
- Portrait categories are now one table, so the rotation, compatibility checks and asset-presence
  check all derive from it. Existing assignments are kept and count towards the least-used choice.

## 0.15.2 — bind perk-promotion barks to John/Jane

- Route the direct per-tier `PromotionBarkSounds` to the promoted leader's isolated John/Jane
  SoundBank, using existing positive-response clips. Tactical and Hire voice paths were already
  customized; the cloned source leader's direct perk-promotion sounds were not.
- Copy the matching gender donor's bark entries before voice setup, so a pending audio bundle never
  falls back to the unrelated source leader's voice. Existing save records need no migration.

## 0.15.1 — rotate portrait sets for every promotion

- Give every new promotion the portrait set of its native gender/skin category instead of only the
  first one per save. Within a category the least-used set in the save is chosen, so additional
  sets added later rotate automatically; today each category has one set, which repeats.
- Allow the same portrait set on several leaders in one save. Existing assignments, including
  0.14.0 male/White and 0.14.3 female_01 records, are kept; older leaders without a set stay default.

## 0.15.0 — personal XP and mentor perk trees (#67, PR #71)

- Give each new promotion a fresh, per-leader native perk tree of 16 options (5/5/4/2 across
  Tiers 1–4) from a curated native infantry pool. Higher Veterancy XP bands add advanced options;
  unknown XP uses only the base pool.
- Offer up to one (two from Veteran) transferable perks actually learned by the mentor (current
  commander, else most-served hired leader, else native last leader) at their native tier. Nothing
  is granted automatically; unique, command and scripted perks are excluded.
- Keep the accepted generic initial perk. Save the generated tree in the sidecar and rebuild it
  exactly on load, before native roster deserialization; it is never rerolled.
- Records from earlier versions keep the shared generic tree; purchased perks are never changed.
- Ships together with the 0.14.4 six-portrait profile work. In-game acceptance is pending.

## 0.14.4 — complete the six approved portrait profiles

- Ship all six #69 portrait sets and assign the first future promotion per native gender/skin
  combination: male and female White, Brown and Black.
- Keep one profile per combination and save; later matching promotions retain default art and dynamic
  badges continue to come from the default gendered assets.
- Keep 0.14.0 male/White and 0.14.3 female_01 sidecars loadable without save migration.

## 0.14.3 — associate the approved portrait bank with matching leaders

- Assign the first future male/Brown promotion per save to `promoted_male_01`, the first female
  promotion to `promoted_female_01`, and the first male/Black promotion to `promoted_male_02`.
- Keep later promotions in those categories and new male/White promotions on the default portraits;
  retain 0.14.0 male/White sidecar compatibility.
- Ship all three six-image sets as runtime assets. Each profile changes only portrait fields; dynamic
  badges continue to come from the default gendered art.

## 0.14.2 — captioned normal-ready bark variety

- Add five separate tactical click-bark variants using existing John/Jane response recordings.
- Give each variant its own Sound ID and matching subtitle; do not mix different phrases under the
  single “Yes, sir.” route. The direct Hire-screen selection field remains a single fixed Sound ID.

## 0.14.1 — correct bespoke portrait target to male Brown

- Assign the first future male/Brown promotion per save (`SkinColor.Brown = 1`, the black-haired
  variant); male/White (`SkinColor.White = 0`, the redhead) no longer receives new assignments.
- Preserve 0.14.0 sidecar records that already assigned the profile to a male/White squaddie.
- Keep the one-assignment-per-save rule, six runtime images, default badges, and existing portraits.

## 0.14.0 — one-off bespoke portrait candidate (initial target; superseded by 0.14.1)

- Assign `promoted_male_01` to only the first future male/White promotion per save and persist that
  selection in its sidecar record. Existing promotions, other skin/gender combinations and later
  promotions retain the default balaclava portraits; dynamic badges remain on the default art.
- Include the six approved portrait images as runtime assets, with `slot.png` normalized to RGBA8.
- Carry the 0.13.2 voice-map/load fix; portrait and in-game voice playback acceptance remain pending.

## 0.13.2 — experimental voice mapping and bundle-load fix

- Keep the quiet-ready hidden-threat and shouty-ready contextual mappings, but accept intentional
  reuse: the route table requests 399 unique clips from the unchanged 429-clip John/Jane bundle.
- Validate that every requested route clip exists in the hash-verified bundle receipt instead of
  requiring the requested subset to equal the full bundle inventory. This fixes the 0.13.1 runtime
  fallback to native donor voices (reported as Lim); canonical installation passed, playback acceptance is pending.

## 0.13.0 — experimental voice-pitch test candidate

- Assign new promoted leaders one of five deterministic pitch presets, derived from badge
  index and persisted in the existing sidecar. Older sidecars default to neutral (1.00).
- Give each pitch/gender its own Stem bank and tactical speaker tag; tactical and Hire barks
  use the same profile while reusing the bundled John/Jane audio clips.
- Canonical Windows BuildOnly and installation passed on 21 October. The prototype eagerly
  prepares ten profiles (about 780 conversation clones); startup/memory measurement and real
  audio/playback verification remain required before release.

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

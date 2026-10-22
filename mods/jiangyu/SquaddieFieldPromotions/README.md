# Squaddie Field Promotions 0.17.0

> **Experimental 0.17.0 test candidate; in-game acceptance is pending.**
> Version 0.15.9 raises the active and inactive slot crops for `promoted_female_04` and
> `promoted_male_05`; existing saved profile assignments remain unchanged.
> Version 0.17.0 expands private Trepang 2 BlackOps to 211 selected recordings across 167/180 native slots,
> with per-recording captions and native tactical variants. Thirteen unsupported events remain silent.
> New voice assignments use a curated pool of 31 profiles (22 male/9 female), excluding 25 sparse
> profiles. Minimum: 42 mapped routes, 12 distinct clips and all 12 core event routes. Existing
> assigned voices remain loadable without rerolling. The other 55 profiles are unchanged; the original 30
> eligible profiles still have 138/180 silent routes. BlackOps uses the complete ASR inventory for curation,
> not the former 42-clip sample; only five clips have user wording checks. Listening remains pending.
> Canonical Windows BuildOnly/install passed on 22 October (SDK 10.0.113, zero build warnings/errors).
> Installed manifest/DLL versions are 0.17.0/0.17.0.0; all 607 legacy recordings passed decoded-audio
> comparison. Unrelated mods and UserData were unchanged. Native playback/gameplay is not yet verified.
> See [voice curation](VOICE_CURATION.md) for the retained/retired lists and limits.
> XCOM leaders use only their assigned speaker: every unmapped event is silent, with no caption,
> rather than borrowing John/Jane audio. This includes tactical reactions, hiring and promotion barks.
> Each gender/skin category now has exactly four portrait sets: Brown `01`/`04`/`06`/`10`,
> Black `02`/`05`/`08`/`12`, and White `03`/`07`/`09`/`11`. Each category rotates least-used-first.
> Version 0.15.6 adds set `10` to Brown, `11` to White, and `12` to Black.
>
> Version 0.15.5 placed set `06` with Brown. Existing saves that received set `06` as White in 0.15.3/0.15.4
> keep their assigned portrait and remain loadable.
>
> Version 0.15.5 balanced categories to three sets each: Brown `01`/`04`/`06`,
> Black `02`/`05`/`08`, and White `03`/`07`/`09`.
> Version 0.15.5 places set `06` with Brown. Existing saves that received set `06` as White in 0.15.3/0.15.4
> keep their assigned portrait and remain loadable.
>
> Version 0.15.4 added `07` (black hair/light skin), `08` (black hair/dark skin), and `09` (red hair/light skin).
> Sets `07` and `09` join White; `08` joins Black. Set `06` was temporarily grouped with White in that release.
>
> Version 0.15.3 added six approved #69 portrait sets, giving two per native gender/skin category:
> male/Brown `01`+`04`, male/Black `02`+`05`, male/White `03`+`06`, and the same numbers for female.
> Version 0.15.2 routes direct per-tier perk-promotion barks through each promoted leader's John/Jane
> voice bank. This closes the source-leader audio path that speaker tags and tactical barks do not use.
> Version 0.15.1 gives **every** new promotion a portrait set matching its native gender/skin,
> rotating least-used first within that category. Additional sets automatically join the rotation.
>
> Since 0.15.0, each new promotion receives its own saved 16-option perk tree (5/5/4/2 across Tiers 1–4), with
> more advanced options at higher Veterancy XP and up to two skills learned from the mentor offered
> as choices (#67/#71). Leaders promoted before 0.15.0 keep the generic tree and purchased perks.
>
> Version 0.14.4 shipped all six approved #69 portrait sets:
> male/Brown → `promoted_male_01`, male/Black → `promoted_male_02`, male/White → `promoted_male_03`;
> female/Brown → `promoted_female_01`, female/Black → `promoted_female_02`, female/White → `promoted_female_03`.
> Up to 0.15.0 only the first matching promotion per save received a set; dynamic badges remain unchanged.
> Previously persisted 0.14.0 male/White and 0.14.3 female_01 assignments remain loadable.
> Version 0.14.2's five separately captioned tactical normal-click responses remain included; each
> uses existing John/Jane recordings and a matching subtitle, while the direct Hire-screen field stays
> fixed to “Yes, sir.” Test all features only in a disposable campaign (#69/#70).
>
> The 0.13.1 test build loaded SFP but failed to mount John/Jane audio: its routes request 399 unique clips from the unchanged 429-clip receipt. Exact-count guards then kept native donor voices (the reported Lim voice). Version 0.13.2 allows this valid subset while still checking every requested clip.
>
> The prior 0.13.0 pitch-only candidate was installed on the MENACE PC (21 Oct). New promotions cycle through
> five deterministic pitch presets (1.00 / 0.90 / 0.95 / 1.05 / 1.10) by badge index;
> existing save records stay neutral. Tactical and Hire barks use the same isolated
> profile, reusing the bundled John/Jane audio. Its canonical Windows BuildOnly/install passed;
> initial user feedback confirms audible pitch differences and tentatively says badges look
> correct. Full profile, save/load and startup/memory checks remain pending. Do not use on a
> valuable save; see #68.

**Experimental: promotes real squaddies to hirable leaders and persists them across save/load.**
Tracked in [#27](https://github.com/EmaEli/menace-mode/issues/27) and
[#53](https://github.com/EmaEli/menace-mode/issues/53).

Vehicle crew promotions ([#66](https://github.com/EmaEli/menace-mode/issues/66)) are
**not implemented**; see the
[identity and promotion proposal](../../../docs/SFP_VEHICLE_PROMOTIONS_DESIGN.md).

## Requirements

- MENACE **0.7.17+22362**, MelonLoader and Jiangyu **1.4.8**.
- **Menace Mod Settings >= 0.6.1** is the only additional required mod.
- Squaddie Veterancy is **optional**. No Custom Leaders loader or legacy Modpack Loader.

## What the mod does

- Opt-in **Field Promotion Dossier** in the normal Black Market grid. In Menace Mod Settings,
  **Promotion dossier price (credits)** defaults to **100**, adjustable **10–1000 in steps of 10**.
  Changes refresh existing offers too. This is the native base trade price (normal market
  modifiers still apply), not an extra fee on redemption or in Hire.
- Redeeming the dossier selects the best living squaddie (assigned or unassigned):
  - Complete Veterancy XP coverage: highest XP, ties use lowest native soldier ID.
  - No/incomplete XP coverage: most native missions, same deterministic tie-break.
  - Soldiers already promoted are skipped; unknown XP is never fabricated as zero.
- The chosen squaddie becomes a **new squad leader in the Hire list (0 credits)**: a
  unique `UnitLeaderTemplate`/`SpeakerTemplate` cloned from the matching vanilla
  gendered leader, with the soldier's name, nickname, skin, balaclava portraits from
  `runtime_assets/<gender>/`, a black shield with a dynamic letter (A, B, C, ...), and the John/Jane voice
  bank from `runtime_assets/voice/` when it loads; otherwise the matching gender donor voice stays.
- Every new promotion receives an approved six-image set for its native gender/skin combination from
  `runtime_assets/promoted_<gender>_NN/`: Brown `01`/`04`/`06`/`10`, Black `02`/`05`/`08`/`12`, White `03`/`07`/`09`/`11`,
  for both `male` and `female`. Within a category the least-used set in the save is chosen
  (ties keep list order), so the sets alternate. Each profile changes portrait
  fields only, leaving dynamic badges unchanged. Leaders promoted before 0.15.1 keep their current art.
- The generated leader receives a persistent `UnitTitle` based on the squaddie's last unit
  (or a varied Imperius designation) and an individual section/badge. Its `UnitDescription`
  carries the prior unit/commander, native lifetime mission count, optional Veterancy rank,
  and per-unit missions/wounded-mission history recorded by SFP.
- Per-unit service tracking begins with 0.11.0: at mission end, deployed squaddie IDs and
  native wounded/downed IDs are attributed to their leader template and saved in the same
  per-save Jiangyu sidecar. Mission keys prevent duplicate callbacks from counting twice;
  earlier unit-by-unit history is explicitly reported as unavailable, not invented.
- **Use generic heads for promoted leaders** is ON by default. It clears `CustomHead` so
  promoted leaders use the game's generic appearance even if **Show Unique Leader Heads**
  is enabled. Turn it off to restore gender-matched custom heads.
- Attributes: baseline **65/65/62/50/50/5/45** (AGI/WS/VAL/TOU/VIT/PRE/POS) plus a
  Veterancy rank-aligned bonus. All promoted leaders keep the accepted generic initial perk
  instead of the donor's.
- **Personal perk trees (0.15.0):** at redemption, each new leader gets a fresh native perk tree
  of 16 options (5/5/4/2 across Tiers 1–4) drawn from a curated pool of native infantry perks,
  never Singh's tree. The choice is stable per leader and saved in the sidecar; save/load, later XP,
  mentor changes or mod updates never reroll it. Promotion Points and rank gates still decide
  when an option can be bought.
  - Veterancy XP at promotion adds advanced options per band (Green 0, Combat Tested 1,
    Hardened 2, Regular 3, Veteran 6, Crack 7, Elite 8). Unknown XP uses only the base pool.
  - Mentor: the soldier's current commander, else the hired leader with the most recorded
    service, else the native last leader. Up to one (two from Veteran) of the mentor's
    *learned* perks from a transferable allowlist are offered at their native tier; nothing
    is granted automatically. Unique, command and scripted perks are never inherited.
  - Leaders promoted before 0.15.0 keep the Generic Low Level Squad Leaders tree; already
    purchased perks are never removed, refunded or replaced.
- All promoted leaders, including older records, rebuild the black letter badge after loading.
  SFP-only overrides of native `GetBadge` / `GetBadgeWhite` bypass the shared infantry donor
  emblem used by the tactical overhead HUD. Vanilla leader/unit templates remain unchanged.
  Source leader template IDs remain in promotion records for compatibility, not badge rendering.
- **The exact source soldier is removed** from their assigned squad and the living pool after
  the new leader is offered in Hire. No random removal, fake death or replacement soldier.
  On a failed promotion, source objects/assignment order, Hire offer and ledger are rolled back.
  A failed rollback explicitly warns not to save and to reload the previous save.
- Old 0.9.x preview saves are reconciled **when opening Black Market**: a source is removed
  only if its recorded leader is hired/hirable and the native ID/name/nickname/gender/skin match.
  Already absent soldiers are left alone. No save files are edited directly; normal saving
  persists the updated pool and per-save ledger. Keep a separate pre-upgrade test save.
- Promotions are stored through Jiangyu mod state in a sidecar next to each save
  (`<save>.jiangyu.SquaddieFieldPromotions.json`). On load, an early prefix on
  `SaveSystem.ExecLoad` rebuilds every recorded leader **before native roster load**,
  so hired leaders deserialize against an existing template. A missing sidecar loads
  silently; a corrupt sidecar or a single broken record warns once and never blocks
  the other records or the native load. Afterward, SFP reapplies the promoted title and
  description to both the unit and speaker templates. SFP-only overrides of native
  `GetUnitTitle` / `GetUnitDescription` expose these values for infantry, whose getters
  otherwise read the shared vanilla infantry template. The shared template is never edited.
  Post-load verification reads the displayed strings through those native getters.
- **DANGER — BACK UP `test.save` BEFORE ENABLING THE ONE-SHOT CLEANUP.** It removes every
  ledger-matching SFP leader from every roster state and deployment, then saves over `test.save`.
  The option is OFF by default and disarms itself; restore your backup to undo the cleanup.
  After native model loading completes and a full preflight of all five roster lists and deployment,
  it removes matching leaders and BattlePlan entries through MENACE's native manual-save API.
  Unrelated leaders are preserved; invalid or duplicate ledger IDs, unavailable lists or an Ironman
  save abort without changing the roster. The promotion sidecar is retained for safe load recovery.
- No recruitment/debug hotkeys, marker files or polling. Voice setup runs once, after
  the strategy layer exists, only when the option is on or promoted leaders exist.
- Promotion and diagnostic logging are **OFF by default**; generic promoted-leader heads are
  **ON by default** in Menace Mod Settings.

Use a disposable campaign; do not test on a save containing old lab-generated leaders.

## Windows installation

Use the canonical bundled-source kit's **INSTALL-SquaddieFieldPromotions.cmd**.
It bootstraps the pinned private .NET SDK, builds from inventoried bundled sources,
validates real DLL/manifest versions, backs up the old SFP folder outside Mods,
then installs only SFP with rollback on failure. Menace Mod Settings and Jiangyu
are validated and **not overwritten**. No global CLI/Unity Editor setup is required.
Close the game before installation. The console remains open; failures open the
persistent **%TEMP%\MENACE-install.log** automatically.

The kit also ships the module's raw `runtime_assets/` (portraits, prebuilt John/Jane
voice bundle with its build receipt, voice routes); the installer stages them next to
the compiled package because `jiangyu compile --clean` does not preserve them.

## Acceptance test (0.17.0 test candidate; in-game checks pending)

Version 0.17.0 BlackOps: repeated tactical actions should produce meaningful variants with the caption
belonging to the actual spoken recording. Check movement, sightings, attacks, casualties and retreat;
enemy retreat must not play an order for our own squad to retreat. Unsupported damage/destruction events
remain captionless silence. Quiet/shout/hurt delivery and the audio mix still need listening feedback.
Other assigned speakers, portraits and settings must stay unchanged. No saves are changed by installation.

Version 0.15.9 portraits: confirm the `promoted_female_04` and `promoted_male_05` slot photos
sit higher in both active and inactive states. Existing leaders should retain their assigned
portraits after save, restart and load.

Version 0.15.8 curation: new promotions and legacy records without an assigned voice must use
only eligible profiles; selection should have a mapped recording. Previously assigned retired
profiles must retain their voice on load. No save rewrite or cross-speaker fallback is introduced.

Version 0.15.7 voices: load existing promoted leaders and test selection, movement, attacks,
combat reactions, hiring and perk purchases. Mapped lines must keep that leader's assigned XCOM
speaker; unmapped events must be silent without subtitles. No John/Jane voice should interrupt
an XCOM profile. Repeat after save/restart/load. Existing voice assignments and saves are unchanged.

Version 0.15.6 portraits: promote several soldiers of the same gender/skin. Brown should rotate
`01`, `04`, `06`, `10`; Black should rotate `02`, `05`, `08`, `12`; White should rotate `03`, `07`, `09`, `11`,
then repeat. Check Hire, strategy and tactical views and save/restart/load.

Version 0.15.2 perk-promotion bark: with male and female promoted leaders, buy a new perk at an
available tier. The spoken response should use that leader's assigned John/Jane voice profile,
not the source leader's voice. Repeat after save/restart/load to check the rebuilt template keeps
the direct bark assignment.

Version 0.15.1 portrait rotation: promote two soldiers of the same gender/skin. Both must show the
matching portrait set (not the balaclava default) with their own letter badge, and keep it after
save, restart and load. Leaders promoted earlier keep their existing portraits.

Version 0.15.0 personal perk trees: promote two soldiers with different XP (one assigned to a
leader with purchased perks). Each new leader must show 16 options by tier, different between
the two, with the generic initial perk. Buy a perk with Promotion Points, then save, restart and
load: the tree and purchase must be unchanged. Pre-0.15 promoted leaders must keep their tree.

The user verified the generic perk progression in 0.12.0, but rejected the source-emblem
overlay and reported Singh's icon still above tactical units. Version 0.12.1 restores black
letter badges and fixes the native tactical getter path; in-game confirmation is pending.
Version 0.14.1 targets the first future male/Brown (black-haired) promotion; verify the Hire card,
tactical views, and save/load reconstruction. It keeps previously persisted 0.14.0 male/White
assignments loadable without changing saves. Version 0.14.2 adds five separate tactical normal-click
responses (“Right.”, “Stay sharp.”, “I've got this.”, “Alright, let's move!”, and “Let's move!”),
each with a matching subtitle; repeatedly select a promoted leader and check that the spoken phrase
and subtitle stay paired. Hire-screen selection still uses its single fixed “Yes, sir.” sound.
Version 0.14.4 assigns one portrait set per native gender/skin category and save: male/Brown (`male_01`),
male/Black (`male_02`), male/White (`male_03`), female/Brown (`female_01`), female/Black (`female_02`),
and female/White (`female_03`). Verify all six in Hire and tactical views, confirm the dynamic badges
remain present, and reload the save to verify the assignments persist. Legacy 0.14.0 male/White and
0.14.3 female_01 sidecars must continue to load without changing saves.
Version 0.13.0 adds
five persisted pitch profiles; test startup time/memory and compare the new leaders' Hire and
tactical barks before treating this prototype as accepted. Version
0.13.2 retains the 0.13.1 quiet-ready and shouty-ready mappings and fixes the runtime's exact
bundle-count assumption, which blocked John/Jane voices and caused fallback to native donor
voices. Canonical Windows BuildOnly/install passed; in-game subtitle/audio confirmation is pending; see #70. Initial 0.13.0 user feedback is positive but preliminary:
pitch differences are audible between hired leaders and the in-game badges appear correct;
full profile and persistence checks remain pending.

Versions 0.8.0-0.8.4 verified in game that the native dossier is stocked, visible at
1 credit, redeemable and restocked. Version 0.9.0 reconnects the recovered 0.6.1 leader
creation (templates, portraits, badges, John/Jane voices) and adds save/load persistence;
0.9.1 fixes the `IReadOnlyCollection` enumeration failure seen on the first 0.9.0 redemption;
0.9.2 keeps runtime portrait/badge assets alive across scene changes (redemption after load).
On 0.9.2 the user verified hired/unhired leaders surviving restart/load, then further
redemptions (female and male) after load; the log rebuilt A/B/C and promoted D/E/F with no SFP errors.
Source removal, preview-save reconciliation and configurable pricing were added in 0.10.0.
Version 0.11.0 adds saved per-unit service and generic leader heads. Version 0.11.2 overrides
the infantry title/description getters for promoted leaders. Version 0.11.3 aligns attribute
bonuses with the Veterancy rank ladder. Version 0.11.4 adds guarded `test.save` cleanup;
0.11.6 extends it to all five native roster states. The 0.11.5/0.11.6 attempts logged no matches,
but the user still saw SFP leaders in Hire, squads and deployment. Native inspection confirmed
that `ExecLoad` only starts a coroutine and scene arrival precedes roster deserialization.
Version 0.11.7 waits for the active roster to deserialize and that exact loading stream to close
after all model processors; it also removes matching BattlePlan entries with rollback.
The user verified the cleanup in game: 5 hirable and 3 hired leaders removed, saving succeeded, and
the one-shot toggle persisted OFF. Version 0.11.8 makes the destructive action and backup warning
unmistakable in settings; visual confirmation of that warning remains pending.

1. Launch a **throwaway non-Ironman** campaign. Note the living soldier count and Hire list.
2. In Menace Mod Settings -> **Squaddie Field Promotions**, enable
   **Offer Field Promotion Dossier in Black Market**. Open Black Market.
3. Confirm one dossier at base price **100** and unchanged vanilla stock. In Mod Settings,
   change the price to **110**, then back to **100**; existing stock must update without duplicates.
4. Redeem it. Expect the **FIELD PROMOTION** dialog naming the soldier and criterion.
5. Open Hire: the new leader (soldier's name, black letter badge, portraits) is listed at 0 credits.
   The living soldier count must decrease by exactly one per new promotion, and its exact source
   ID must no longer be in the old squad/reserve or listed as killed. Hire and assign the new leader.
6. Redeem a second dossier: a different soldier is promoted (badge B); the first is skipped.
7. Inspect the leader's perks: it should have the generic initial perk and a personal 16-entry
   infantry tree from Tiers 1–4, not Singh's donor tree. Confirm black letter badges in strategy/drag views
   and above the promoted squad in tactical, with no Singh emblem (also when selected/status changes).
8. Buy a perk, **save, quit to desktop, relaunch, load**, and confirm the leader's identity,
   badge and saved progression rebuild while the purchased perk remains present. Check start/load
   time and memory. For five new same-gender leaders with badges A–E, compare Hire and tactical barks:
   they should retain the same voice/profile per leader and cycle through 1.00, 0.90, 0.95, 1.05,
   1.10; existing leaders from older sidecars should remain neutral.
9. Disable the option: only the dossier stock should disappear; vanilla dossiers must still work.
10. Send observations; the agent can pull `Latest.log` (enable **Diagnostic logging** for detail).
11. **DANGER: make a separate backup of `test.save` first** and confirm it is non-Ironman. Load it
    and enable **I HAVE A BACKUP - REMOVE ALL SFP LEADERS FROM test.save (ONE-SHOT)** only if you
    intend to remove all matching promoted leaders and save over that file. Verify SFP entries disappear from
    all five native roster lists and deployment while unrelated leaders and promotion records remain;
    cleanup must wait for native load completion even if the toggle was already armed at startup.
    The setting must turn itself off. Duplicate ledger IDs abort without saving; other save
    filenames are untouched. Save, quit and reload to confirm the result persists.
12. Complete missions with deployed soldiers (including one wounded soldier), then promote one.
    Check the leader's unit title/description for the correct unit, commander, missions and wound
    history. Earlier service is not backfilled; new mission service should persist after restart.
13. Confirm generic faces with the default setting, toggle **Use generic heads for promoted
    leaders** off and back on, and verify the preference survives save/load.
14. Promote a squaddie from a previously promoted squad: its badge should receive the new
    promotion letter on the same black shield, not the source squad's letter or Singh's emblem.

When loading a 0.9.x preview save, opening Black Market also removes matching old duplicate
sources once. Count those separately from the one soldier consumed by each new dossier.

Ironman is excluded. Coexistence with other mods, keyboard dismissal and native dialog
readability remain in-game acceptance gates.

## Remaining gates

1. In-game acceptance of 0.10.0 source removal (assigned/unassigned), old preview-save cleanup,
   price changes, and save → restart → load with no returning source soldier.
2. Without optional Veterancy, plus dismissal/death, divergent saves and mission transitions.
3. Failure-path dossier/resource preservation and attribute balance remain acceptance gates;
   offline tests of source/Hire/ledger rollback do not verify native purchase rollback.

Baseline AGI/WS/VAL/TOU/VIT/PRE/POS: **65/65/62/50/50/5/45**.
XP bonuses by Veterancy rank: Green **+0** at 0 XP; Combat Tested **+2** at 250 XP;
Hardened **+4** at 500 XP; Regular **+6** at 1,000 XP; Veteran **+8** at 2,000 XP;
Crack **+10** at 4,000 XP; Elite **+12** at **8,000+ XP**. Precision receives half
the bonus. These attribute values remain provisional and need in-game balance testing.

## Developer checks

Run the module's `tests/OfflineChecks` C# executable for production rules/callbacks
against explicitly simulated native APIs; `tests/verify_authoring.py` checks source/template contracts.
Use shared `build_kit.py --module SquaddieFieldPromotions --use-installed-settings --output <zip>`
after a real code/template build. It inventories source/artifacts and derives the launcher
from the shared Suite launcher. Never call an unbacked global CLI deploy for this test.

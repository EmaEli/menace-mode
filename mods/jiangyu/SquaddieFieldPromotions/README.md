# Squaddie Field Promotions 0.13.0

> **Experimental 0.13.0 voice-pitch test candidate installed on the MENACE PC (21 Oct).** New promotions cycle through
> five deterministic pitch presets (1.00 / 0.90 / 0.95 / 1.05 / 1.10) by badge index;
> existing save records stay neutral. Tactical and Hire barks use the same isolated
> profile, reusing the bundled John/Jane audio. Canonical Windows BuildOnly/install passed;
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
  gendered leader, with the soldier's name, nickname, skin, bundled portraits from
  `runtime_assets/<gender>/`, a black shield with a dynamic letter (A, B, C, ...), and the John/Jane voice
  bank from `runtime_assets/voice/` when it loads; otherwise the native donor voice stays.
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
  Veterancy rank-aligned bonus. Promoted leaders use the Generic Low Level Squad Leaders
  infantry perk tree (16 perks, Tiers 1–4) and its generic initial perk instead of the donor's
  progression. Rebinding the template does not rewrite already acquired perks saved on a leader.
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

## Acceptance test (0.13.0 voice-pitch, startup/load and gameplay verification pending)

The user verified the generic perk progression in 0.12.0, but rejected the source-emblem
overlay and reported Singh's icon still above tactical units. Version 0.12.1 restores black
letter badges and fixes the native tactical getter path; in-game confirmation is pending.
Version 0.13.0 adds five persisted pitch profiles; test startup time/memory and compare
the new leaders' Hire and tactical barks before treating this prototype as accepted.
Initial user feedback is positive but preliminary: pitch differences are audible between hired
leaders and the in-game badges appear correct; full profile and persistence checks remain pending.

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
7. Inspect the leader's perks: it should have the generic initial perk and 16-entry infantry
   tree from Tiers 1–4, not Singh's donor tree. Confirm black letter badges in strategy/drag views
   and above the promoted squad in tactical, with no Singh emblem (also when selected/status changes).
8. Buy a perk, **save, quit to desktop, relaunch, load**, and confirm the leader's identity,
   badge and generic progression rebuild while the purchased perk remains present. Check start/load
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

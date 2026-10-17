# Squaddie Field Promotions 0.8.4

**Experimental native dossier REVIEW, not a permanent-recruitment release.**
Tracked in [#27](https://github.com/EmaEli/menace-mode/issues/27).

## Requirements

- MENACE **0.7.17+22362**, MelonLoader and Jiangyu **1.4.8**.
- **Menace Mod Settings >= 0.6.1** is the only additional required mod.
- Squaddie Veterancy is **optional**. No Custom Leaders loader or legacy Modpack Loader.

## What can be tested now

- Opt-in **Field Promotion Dossier (PREVIEW)** in the normal Black Market grid.
- A native dossier transaction opens a native game dialog, not a permanent custom panel.
- Price is **1 credit**, the minimum the native merchant list displays (items priced
  below 1 are hidden by `BlackMarketUIScreen.FilterItems`). This test intentionally
  does not validate final dossier balance/costs.
- Fresh selection across all living squaddies, assigned and unassigned:
  - Complete Veterancy XP coverage: highest XP, ties use lowest native soldier ID.
  - No/incomplete XP coverage: most native missions, same deterministic tie-break.
  - Unknown XP is never fabricated as zero or compared against mission count.
- Review includes identity, gender/skin, criterion and provisional attributes.
- **No leader is created or recruited; no source squaddie is consumed.**
- No recruitment/debug hotkeys, marker files, polling or automatic voice setup.
- Preview and diagnostic logging are both **OFF by default** in Menace Mod Settings.

The native transaction may consume the test dossier and change market stock.
This is not a promise that native market/save interactions are game-verified.
Use a disposable campaign; do not test on a save containing old lab-generated leaders.

## Windows installation

Use the canonical bundled-source kit's **INSTALL-SquaddieFieldPromotions.cmd**.
It bootstraps the pinned private .NET SDK, builds from inventoried bundled sources,
validates real DLL/manifest versions, backs up the old SFP folder outside Mods,
then installs only SFP with rollback on failure. Menace Mod Settings and Jiangyu
are validated and **not overwritten**. No global CLI/Unity Editor setup is required.
Close the game before installation. The console remains open; failures open the
persistent **%TEMP%\MENACE-install.log** automatically.

The old authoring-only Build-and-Deploy.cmd and unsafe hotkey lab are retired;
their tracked source remains recoverable from Git history.

## Acceptance test (not yet performed in MENACE)

The 0.8.0 startup was observed after the player played a normal mission with preview
OFF; this did not exercise the dossier. Version 0.8.1 replaces the three rejected raw
text patches with fresh native localized objects. Version 0.8.2 gives manually stocked
preview items the normal three-restock timeout. Version 0.8.3 refreshes the native
market window after stocking. Version 0.8.4 fixes the actual cause of the invisible
dossier: the native merchant list hides items priced below 1, so the preview now costs 1.

1. Launch a **throwaway non-Ironman** campaign. Note the living soldier count and Hire list.
2. In Menace Mod Settings -> **Squaddie Field Promotions**, enable
   **Offer 1-credit promotion-review dossier in Black Market**. Open Black Market.
3. Confirm exactly one preview dossier appears, valued at **1**, alongside unchanged vanilla stock.
4. Complete its normal native trade/redemption. Expect the **PREVIEW ONLY** review dialog.
5. Check the candidate/criterion. Close the dialog; soldier count and Hire list must be unchanged.
6. Restock once and reopen the market: the preview should remain visible and must never stack on repeated opens.
7. Disable the option: only preview stock should disappear; vanilla dossiers must still work normally.
8. Leave the test campaign without relying on its save. Send observations; the agent can pull Latest.log.

Ironman is excluded. Save/restart, restock/duplicate-cleanup behavior, keyboard dismissal,
native dialog readability and coexistence with other mods remain in-game acceptance gates.

## Remaining gates before permanent promotion

1. Native dossier/review acceptance above, with and without optional Veterancy.
2. Early reconstruction of unique runtime leader/speaker templates **before native roster load**;
   versioned campaign/slot records, missing/corrupt/mismatched data must fail safely.
3. Confirm/revalidate the chosen identity; remove **that exact** source from alive/assignment
   collections, never random deletion or marking the soldier killed. Roll back partial failures.
4. Restore the recovered 0.6.1 portrait/badge and John/Jane voice paths, without donor stat/perk leakage.
5. Connect confirmed dossier use to real Hire/recruitment, then mission/save/restart/rollback tests.

Baseline AGI/WS/VAL/TOU/VIT/PRE/POS: **65/65/62/50/50/5/45**.
Complete XP adds **0/2/4/6/8/10/12** at **0/25/50/100/150/250/500** XP;
Precision receives half, cap 85. These remain provisional review values, not balanced recruitment.

## Developer checks

Run the module's `tests/OfflineChecks` C# executable for production rules/callbacks
against explicitly simulated native APIs; `tests/verify_authoring.py` checks source/template contracts.
Use shared `build_kit.py --module SquaddieFieldPromotions --use-installed-settings --output <zip>`
after a real code/template build. It inventories source/artifacts and derives the launcher
from the shared Suite launcher. Never call an unbacked global CLI deploy for this test.

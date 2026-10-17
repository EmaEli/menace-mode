# Squaddie Field Promotions 0.9.0

**Experimental: promotes real squaddies to hirable leaders and persists them across save/load.**
Tracked in [#27](https://github.com/EmaEli/menace-mode/issues/27).

## Requirements

- MENACE **0.7.17+22362**, MelonLoader and Jiangyu **1.4.8**.
- **Menace Mod Settings >= 0.6.1** is the only additional required mod.
- Squaddie Veterancy is **optional**. No Custom Leaders loader or legacy Modpack Loader.

## What the mod does

- Opt-in **Field Promotion Dossier** in the normal Black Market grid, priced **1 credit**
  (the minimum the native merchant list displays; items below 1 are hidden by
  `BlackMarketUIScreen.FilterItems`). Dossier cost is provisional, not balanced.
- Redeeming the dossier selects the best living squaddie (assigned or unassigned):
  - Complete Veterancy XP coverage: highest XP, ties use lowest native soldier ID.
  - No/incomplete XP coverage: most native missions, same deterministic tie-break.
  - Soldiers already promoted are skipped; unknown XP is never fabricated as zero.
- The chosen squaddie becomes a **new squad leader in the Hire list (0 credits)**: a
  unique `UnitLeaderTemplate`/`SpeakerTemplate` cloned from the matching vanilla
  gendered leader, with the soldier's name, nickname, skin, bundled portraits from
  `runtime_assets/<gender>/`, a lettered badge (A, B, C, ...) and the John/Jane voice
  bank from `runtime_assets/voice/` when it loads; otherwise the native donor voice stays.
- Attributes: baseline **65/65/62/50/50/5/45** (AGI/WS/VAL/TOU/VIT/PRE/POS) plus a
  Veterancy XP bonus; perks/stats of the donor template are not inherited.
- **The source soldier is not removed** from the roster in this version; the promotion
  is recorded so the same soldier is never promoted twice.
- Promotions are stored through Jiangyu mod state in a sidecar next to each save
  (`<save>.jiangyu.SquaddieFieldPromotions.json`). On load, an early prefix on
  `SaveSystem.ExecLoad` rebuilds every recorded leader **before native roster load**,
  so hired leaders deserialize against an existing template. A missing sidecar loads
  silently; a corrupt sidecar or a single broken record warns once and never blocks
  the other records or the native load.
- No recruitment/debug hotkeys, marker files or polling. Voice setup runs once, after
  the strategy layer exists, only when the option is on or promoted leaders exist.
- The option and diagnostic logging are both **OFF by default** in Menace Mod Settings.

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

## Acceptance test (not yet performed in MENACE)

Versions 0.8.0-0.8.4 verified in game that the native dossier is stocked, visible at
1 credit, redeemable and restocked. Version 0.9.0 reconnects the recovered 0.6.1 leader
creation (templates, portraits, badges, John/Jane voices) and adds save/load persistence.

1. Launch a **throwaway non-Ironman** campaign. Note the living soldier count and Hire list.
2. In Menace Mod Settings -> **Squaddie Field Promotions**, enable
   **Offer 1-credit Field Promotion Dossier in Black Market**. Open Black Market.
3. Confirm exactly one dossier appears, valued at **1**, alongside unchanged vanilla stock.
4. Redeem it. Expect the **FIELD PROMOTION** dialog naming the soldier and criterion.
5. Open Hire: the new leader (soldier's name, lettered badge, portraits) is listed at 0 credits.
   Hire and assign it; the living soldier count must be unchanged (source is not removed yet).
6. Redeem a second dossier: a different soldier is promoted (badge B); the first is skipped.
7. **Save, quit to desktop, relaunch, load**: both leaders must still be present in the
   roster/Hire with name, badge and portraits; confirm the voice bark in a mission.
8. Disable the option: only the dossier stock should disappear; vanilla dossiers must still work.
9. Send observations; the agent can pull `Latest.log` (enable **Diagnostic logging** for detail).

Ironman is excluded. Coexistence with other mods, keyboard dismissal and native dialog
readability remain in-game acceptance gates.

## Remaining gates

1. Save/load persistence acceptance above, with and without optional Veterancy.
2. Remove **that exact** source soldier from alive/assignment collections after a confirmed
   promotion, never random deletion or marking the soldier killed. Roll back partial failures.
3. Dossier cost/attribute balance.

Baseline AGI/WS/VAL/TOU/VIT/PRE/POS: **65/65/62/50/50/5/45**.
Complete XP adds **0/2/4/6/8/10/12** at **0/25/50/100/150/250/500** XP;
Precision receives half, cap 85. These remain provisional values, not balanced recruitment.

## Developer checks

Run the module's `tests/OfflineChecks` C# executable for production rules/callbacks
against explicitly simulated native APIs; `tests/verify_authoring.py` checks source/template contracts.
Use shared `build_kit.py --module SquaddieFieldPromotions --use-installed-settings --output <zip>`
after a real code/template build. It inventories source/artifacts and derives the launcher
from the shared Suite launcher. Never call an unbacked global CLI deploy for this test.

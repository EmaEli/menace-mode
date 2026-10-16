# Weapon Mechanics Rebalanced 0.4.9

**0.4.9 MEN-004 fix:** the extra ammunition cost of high-volume alternate fire (Sustained Fire / Spray) is no longer applied to simulated or free `Skill.Use` calls (`Fake` / `NoUsesConsumed` usage flags). The game issues such calls during the enemy turn; each one silently removed 2 rounds from the squad weapon, draining a full magazine in one AI phase.

**0.4.8 MEN-004 ammo ledger (debug only):** with **Debug logging** ON, every change to a limited-use skill's counter that bypasses `Skill.Use` is logged as `[MEN-004 LEDGER]`: direct `SetUses`/`ConsumeUse` calls (with the managed caller, e.g. Overwatch), and `OnTurnStart`/`OnTurnEnd`/`OnMissionStarted` only when the counter actually changed. OFF by default; no gameplay change.

**0.4.5 MEN-004 diagnostic build:** In **Mod Options -> Weapon Mechanics Rebalanced -> Debug logging**, enable debug before firing the assault rifle's **Sustained Fire (ability 2)**. In `MelonLoader/Latest.log`, search for `[MEN-004 AMMO]`. Each high-volume activation logs ammo uses/charges **before -> after native MENACE -> after WMR**, `consumedPerUse` and extra `ConsumeUse()` calls, together with actor/weapon and flags. Compare the values with the UI counter before/after a shot, then switch debug OFF. This version contains **diagnostics only**, not an ammo fix; MEN-004 remains open.

Weapon Mechanics Rebalanced (WMR) contains non-range weapon gameplay changes separated from Weapon Ranges Rebalanced. It deliberately does **not** own weapon ranges or the long-range indirect-fire AI positioning policy.

**0.4.4 visibility fix:** the double border now uses two separate child GameObjects, one LineRenderer per child. This resolves the 0.4.3 component conflict without touching shared off-map marker channels.

**0.4.3 highlight fix:** the independent mortar-ranging square now uses a thick dual world-space border, raised above terrain and rendered on top for reliable visibility. It still never reuses MENACE delayed-AoE TileHighlighter channels, so Supply Drop/off-map markers are unaffected.

**0.4.2 runtime fix:** ranging state now clears on stance changes only when the crew actually transitions out of the deployed state; unrelated `SetStance` calls cannot erase the remembered firing solution.

## Mortars

WMR changes mortar behavior while leaving range ownership to WRR or vanilla:

- HE and smoke mortar firing AP cost is 50% of the captured native value;
- limited HE and smoke mortar uses are doubled;
- HE physical HP damage and armor-durability damage are multiplied by 0.5;
- native suppression output is not reduced;
- HE base Scatter is 3 / 4 / 5 for light / medium / 120 mm mortar fire;
- light/heavy ScatterChance remains 85%, medium remains 80%;
- suppression AoE falloff is broadened and a suppression-only outer ring extends one tile beyond the native HE blast AoE at 50% suppression;
- no artificial repetition-based mortar salvos are added.

### Cumulative ranging fire

Each physical mortar remembers intended target tiles while its firing position remains established. Ranging tightens the **size of the dispersion area**; it does not increase exact-hit probability by reducing ScatterChance.

- each prior shot at the same tile reduces effective `Scatter` by 1 for the next shot;
- misses can reduce Scatter only to **2**;
- only an actual impact on the exact intended tile finds the firing solution and unlocks **Scatter 1**;
- therefore the default progressions are light `3 -> 2 -> 1(hit only)`, medium `4 -> 3 -> 2 -> 1(hit only)`, and 120 mm `5 -> 4 -> 3 -> 2 -> 1(hit only)`;
- ScatterChance is unchanged at every ranging stage;
- different tiles keep independent firing solutions;
- HE and smoke from the same physical mortar share the same solution;
- infantry mortar solutions are retained only while the crew remains deployed;
- vehicle-mounted mortar solutions are retained while stationary and clear when the platform moves.

### Ranging feedback

When the selected mortar has ranged a tile, WMR draws an independent world-space border around that tile. The border progresses red -> orange/amber -> yellow-green -> green as effective Scatter tightens; full green is reserved for the solved Scatter 1 state.

Version 0.4.1 deliberately does **not** use MENACE's `DelayedAoE3` or `DelayedEnemyAoE3` highlight channels. Those channels are used by delayed off-map abilities such as supply drops, and sharing them could keep an expired off-map marker visible. WMR now leaves MENACE tile-highlight channels, enable flags and color overrides untouched.

With Debug logging enabled, WMR reports `baseScatter`, `effectiveScatter`, target tile, impact tile, direct-hit state and next Scatter. Debug is OFF by default.


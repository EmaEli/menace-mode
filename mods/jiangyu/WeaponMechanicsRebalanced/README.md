# Weapon Mechanics Rebalanced 0.4.14

**0.4.14 mortar feedback (#61; live check pending):** ranging markers use a muted dark-red-to-green palette based on effective Scatter, with brighter green reserved for a solved shot. The displayed scatter value and marker color stay at their previous state while a shell is in flight and update only after the impact callback is observed. This is presentation-only; mortar ballistics and action economy are unchanged.

**0.4.13 enemy-only mortar correction (#59):** the 0.4.12 PC balance test failed. The log revealed a separate `active.enemy_only_launch_medium_mortar_frag` skill that WMR did not cover. It now uses the medium dispersion baseline (Scatter 4, ScatterChance 80%) and activation ranging, without changing its native AP, ammo, repetitions, damage or suppression. Debug-only baseline readback and shot skill IDs support the next live test; balance acceptance remains pending.

**0.4.12 activation ranging (#59):** covered hostile mortars tighten Scatter by one for each later firing action during the same actor activation, regardless of target tile. Misses stop at 2; an exact impact makes later shots use Scatter 1 until the next activation. Player ranging is unchanged. Ranging never changes ScatterChance; 0.4.13 separately corrects the omitted enemy-only skill's baseline.

**0.4.11 targeting feedback (#56):** fixes two runtime failures observed with 0.4.10: its screen borders reached an unsupported `GUI.DrawTexture` proxy stub, and world borders failed in generic `AddComponent<LineRenderer>`. Borders now use a native `GUI.Label` background and non-generic component creation. The yellow dispersion envelope (`Spread N`) remains separate from blast AoE. Proxy call-path checks supplement the pure/source checks; in-game visibility still requires confirmation.

**0.4.9 MEN-004 fix:** the extra ammunition cost of high-volume alternate fire (Sustained Fire / Spray) is no longer applied to simulated or free `Skill.Use` calls (`Fake` / `NoUsesConsumed` usage flags). The game issues such calls during the enemy turn; each one silently removed 2 rounds from the squad weapon, draining a full magazine in one AI phase.

**0.4.8 MEN-004 ammo ledger (debug only):** with **Debug logging** ON, every change to a limited-use skill's counter that bypasses `Skill.Use` is logged as `[MEN-004 LEDGER]`: direct `SetUses`/`ConsumeUse` calls (with the managed caller, e.g. Overwatch), and `OnTurnStart`/`OnTurnEnd`/`OnMissionStarted` only when the counter actually changed. OFF by default; no gameplay change.

**0.4.5 MEN-004 diagnostic build:** In **Mod Options -> Weapon Mechanics Rebalanced -> Debug logging**, enable debug before firing the assault rifle's **Sustained Fire (ability 2)**. In `MelonLoader/Latest.log`, search for `[MEN-004 AMMO]`. Each high-volume activation logs ammo uses/charges **before -> after native MENACE -> after WMR**, `consumedPerUse` and extra `ConsumeUse()` calls, together with actor/weapon and flags. Compare the values with the UI counter before/after a shot, then switch debug OFF. This version contains **diagnostics only**, not an ammo fix; MEN-004 remains open.

Weapon Mechanics Rebalanced (WMR) contains non-range weapon gameplay changes separated from Weapon Ranges Rebalanced. It deliberately does **not** own weapon ranges or the long-range indirect-fire AI positioning policy.

**0.4.4 visibility fix:** the double border now uses two separate child GameObjects, one LineRenderer per child. This resolves the 0.4.3 component conflict without touching shared off-map marker channels.

**0.4.3 highlight fix:** the independent mortar-ranging square now uses a thick dual world-space border, raised above terrain and rendered on top for reliable visibility. It still never reuses MENACE delayed-AoE TileHighlighter channels, so Supply Drop/off-map markers are unaffected.

**0.4.2 runtime fix:** ranging state now clears on stance changes only when the crew actually transitions out of the deployed state; unrelated `SetStance` calls cannot erase the remembered firing solution.

## Mortars

WMR changes mortar behavior while leaving range ownership to WRR or vanilla:

The economy/damage/suppression adjustments below apply to the regular player/vehicle skill profiles. The separate enemy-only medium skill receives **only** Scatter 4 / ScatterChance 80% and activation ranging; all its other native values are retained.

- HE and smoke mortar firing AP cost is 50% of the captured native value;
- limited HE and smoke mortar uses are doubled;
- HE physical HP damage and armor-durability damage are multiplied by 0.5;
- native suppression output is not reduced;
- HE base Scatter is 3 / 4 / 5 for light / medium / 120 mm mortar fire;
- light/heavy ScatterChance remains 85%, medium remains 80%;
- suppression AoE falloff is broadened and a suppression-only outer ring extends one tile beyond the native HE blast AoE at 50% suppression;
- no artificial repetition-based mortar salvos are added.

### Cumulative ranging fire

Player mortars remember intended target tiles while their firing position remains established. Hostile AI mortars instead track shots per physical weapon and actor activation, independent of target tile. Ranging tightens the **size of the dispersion area**; it does not increase exact-hit probability by reducing ScatterChance.

- for players, each prior shot at the same tile reduces effective `Scatter` by 1 for the next shot;
- for hostile mortars, each later firing action in the same activation reduces `Scatter` by 1, even when aimed at a different tile;
- misses can reduce Scatter only to **2**; an exact impact on the intended tile makes subsequent shots by that mortar use **Scatter 1** until its next activation;
- the hostile-mortar counter resets at the next actor activation; HE and smoke from the same physical weapon share it;
- ScatterChance is unchanged at every ranging stage;
- player firing solutions remain independent per tile;
- HE and smoke from the same physical mortar share the same solution;
- infantry mortar solutions are retained only while the crew remains deployed;
- vehicle-mounted mortar solutions are retained while stationary and clear when the platform moves.

### Ranging feedback

When the selected mortar has ranged a tile, WMR draws independent world-space and screen-space borders around that tile, with an `S` label for the next shot's scatter. The border follows a muted dark-red-to-green palette as effective Scatter tightens; a solved Scatter 1 state uses brighter green. The displayed value and color advance only after the projectile impact is observed. Selecting a specific mortar shows only that physical weapon's history.

While targeting, a **yellow outer envelope** and `Spread N` label show the maximum dispersion radius for the current mortar/target combination, including fresh targets. Scatter 3 spans offsets -3 through +3 (a 7-by-7 tile envelope), Scatter 2 spans 5-by-5, and Scatter 1 spans 3-by-3. This is the scatter envelope, not guaranteed damage or an exact-hit probability; native valid-impact filters may exclude tiles. The native blast AoE inside it is deliberately left unchanged.

Version 0.4.1 deliberately does **not** use MENACE's `DelayedAoE3` or `DelayedEnemyAoE3` highlight channels. Those channels are used by delayed off-map abilities such as supply drops, and sharing them could keep an expired off-map marker visible. WMR now leaves MENACE tile-highlight channels, enable flags and color overrides untouched.

With Debug logging enabled, WMR reports `baseScatter`, `effectiveScatter`, target tile, impact tile, direct-hit state and next Scatter. Debug is OFF by default.


# Weapon Mechanics Rebalanced 0.6.1

**0.6.1 correction (#59; gameplay re-test pending):** apply mortar priority and target memory on the main thread immediately before native execution, rather than skipping worker-thread evaluations. Recover legal positive native candidates even when their integer utility left no target selected. Native action execution, AP/ammo and emergency decisions remain authoritative. The 0.6.0 gameplay test did not sustain fire beyond one round; build/install success alone is not gameplay acceptance.

**0.6.0 sustained enemy mortar AI (#59; gameplay check pending):** F9 → Weapon Mechanics Rebalanced → Enemy mortar AI has a default-ON option to prefer HE mortar fire over movement/Idle. Native-scored legal targets receive 6× priority with a floor of 250; a ready mortar is preferred to Move/Idle. Fire missions last **3 rounds by default**, configurable from **2–4**, including the acquisition round. During that window, keep shelling the saved tile even after contact is lost; never track a hidden unit's new position. Blind fire does not extend the window. New maps, round rollback, disabling the option or disabling WMR clear memory. Native AP, ammo, deployment, rotation and skill legality still apply; friendly units inside the conservative scatter/AoE area prevent blind fire. Other weapons, smoke mortar decisions and player AI are unchanged. This is a preference, not a guarantee of a shot when the mortar cannot legally fire. Fire-mission memory is session-only, not saved.

**0.5.1 smoke mortar dispersion (#59):** smoke shells now use the same size-based starting Scatter as HE: light 3, 80 mm medium 4 and 120 mm heavy 5. MENACE shares the standard smoke skill between light and 80 mm mortars, so WMR identifies the weapon at shot/preview time and restores the native shared template afterward. Smoke and HE continue to share per-target ranging: prior shots tighten to 2, while a lucky exact hit solves only that target to 1.

**Aimed-tile feedback (#59):** while aiming a mortar, the pointed-at in-range square now shows both `Spread N` and the exact `Bullseye NN%` chance. The readout shares the same effective-range and line-of-sight calculation as firing; it is omitted if the exact-target scatter filter is unavailable.

**0.5.0 mortar bullseye chance (#59; gameplay check pending):** all covered player and hostile mortars now have a 25% actual chance to hit the aimed tile at half effective range when the mortar can see the target, or 10% when firing indirectly at a target it cannot see. Each tile closer/farther changes the chance by 2 percentage points; the result is clamped to 5–70%. The shot temporarily adjusts native ScatterChance and removes the aimed tile from scatter outcomes, then restores the template value, so these are actual hit odds rather than a scatter-roll approximation.

**0.4.17 corrected enemy ranging (#59; gameplay check pending):** enemies now use the player's size-based starting scatter, -1 per prior shot at the same target down to 2, and Scatter 1 after a lucky exact hit on that target. Enemy histories, including lucky-hit zeroing, reset every turn at `Actor.OnTurnStart`. The enemy-only medium skill restores the player's ScatterChance 80%; the previous 0.4.16 widening rule was a mistaken interpretation, not the intended design. Its 33-AP repeated-action allowance is retained.

**0.4.16 enemy mortar fire cadence and turn dispersion (#59):** the enemy-only medium mortar can take up to three separate firing actions in a 100-AP activation (33 AP per action, `MarkAsActed=false`). Each later shot widens Scatter by one, from 4 to 5 to 6; additional shots remain at 6. The counter resets on `Actor.OnTurnStart`, so later turns begin at Scatter 4 again. Native repetitions remain 1; player mortar behavior and native ammo uses are unchanged. The PC gameplay test must confirm AI action selection and turn reset.

**0.4.15 suppression-first enemy mortar correction (#59; gameplay retest pending):** the enemy-only medium mortar now receives the intended 50% HP/armor-durability damage and broader suppression profile, while retaining its native AP/ammunition. Its ScatterChance is 100% (always roll dispersion, not always miss). Hostile ranging retains at least Scatter 3 for HE and no longer turns a lucky hit into Scatter 1 against every target. Native Attack template casts also fix payload adjustments being skipped when handlers arrive as base wrappers. Player tuning/ranging is unchanged; the cast fix restores the intended damage/suppression tuning for all covered HE profiles.

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

The economy adjustments below apply to regular player/vehicle skill profiles. The separate enemy-only medium skill receives **Scatter 4 / ScatterChance 80%, half HP/armor-durability damage and the medium suppression profile**. Its 33 AP cost and `MarkAsActed=false` allow up to three independent actions in a 100-AP activation; native ammo uses and single repetition are retained.

- HE and smoke mortar firing AP cost is 50% of the captured native value;
- limited HE and smoke mortar uses are doubled;
- HE physical HP damage and armor-durability damage are multiplied by 0.5;
- native suppression output is not reduced;
- HE and smoke base Scatter is 3 / 4 / 5 for light / medium / 120 mm mortar fire;
- baseline light/heavy ScatterChance remains 85%, medium remains 80%;
- actual bullseye chance at half of the skill's effective range is 25% with direct line of sight and 10% for indirect fire; each tile closer/farther changes it by 2 percentage points, clamped to 5–70%;
- shot-time ScatterChance and the scatter candidate set are adjusted together so the aimed tile's exact landing probability matches that chance, then the native template ScatterChance is restored;
- suppression AoE falloff is broadened and a suppression-only outer ring extends one tile beyond the native HE blast AoE at 50% suppression;
- no artificial repetition-based mortar salvos are added.

### Cumulative ranging fire

Both sides remember intended target tiles independently for each physical mortar. Player histories remain while the firing position is established; hostile histories additionally reset every turn.

- the first shot at a target uses the size-specific baseline: light 3, medium 4, heavy 5;
- each prior shot at the same tile reduces effective `Scatter` by 1 for the next shot;
- misses can reduce Scatter only to **2**; a lucky exact impact solves that target tile to **Scatter 1**;
- the lucky-hit chance uses native shot dispersion, as for players, not an additional custom roll;
- a different target starts at base scatter: neither corrections nor lucky-hit zeroing transfer;
- all hostile target histories, including solved Scatter 1, reset at `Actor.OnTurnStart`;
- per-target scatter ranging leaves the baseline ScatterChance unchanged; shot-time bullseye accuracy is calculated separately and restored after firing;
- firing solutions remain independent per tile for players and enemies;
- HE and smoke from the same physical mortar share the same solution;
- infantry mortar solutions are retained only while the crew remains deployed;
- vehicle-mounted mortar solutions are retained while stationary and clear when the platform moves.

### Ranging feedback

When the selected mortar has ranged a tile, WMR draws independent world-space and screen-space borders around that tile, with an `S` label for the next shot's scatter. The border follows a muted dark-red-to-green palette as effective Scatter tightens; a solved Scatter 1 state uses brighter green. The displayed value and color advance only after the projectile impact is observed. Selecting a specific mortar shows only that physical weapon's history.

While targeting, a **yellow outer envelope** and `Spread N` label show the maximum dispersion radius for the current mortar/target combination, including fresh targets. Scatter 3 spans offsets -3 through +3 (a 7-by-7 tile envelope), Scatter 2 spans 5-by-5, and Scatter 1 spans 3-by-3. This is the scatter envelope, not guaranteed damage or an exact-hit probability; native valid-impact filters may exclude tiles. The native blast AoE inside it is deliberately left unchanged.

Version 0.4.1 deliberately does **not** use MENACE's `DelayedAoE3` or `DelayedEnemyAoE3` highlight channels. Those channels are used by delayed off-map abilities such as supply drops, and sharing them could keep an expired off-map marker visible. WMR now leaves MENACE tile-highlight channels, enable flags and color overrides untouched.

With Debug logging enabled, WMR reports `baseScatter`, `effectiveScatter`, target tile, impact tile, direct-hit state and next Scatter. Debug is OFF by default.


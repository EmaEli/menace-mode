## 0.4.4

- Fixed the missing mortar ranging squares caused by attaching two `LineRenderer` components to a single Unity `GameObject`. The dark and colored border strokes now have separate child GameObjects.
- Destroy partially created highlight roots on failures; log at most one render failure per scene, Debug ON only.
- No native `TileHighlighter` usage; delayed support/supply-drop marker lifetime is unaffected. Gameplay, per-tile ranging, Scatter and normal logging are unchanged.

## 0.4.3

- Fixed the independent world-space mortar ranging border being too easy to lose or effectively invisible after the 0.4.1 Supply Drop compatibility change.
- Kept the renderer isolated from MENACE `TileHighlighter` / delayed-AoE channels.
- Each ranged tile now uses two world-space lines: a thick dark under-stroke and a narrower red-to-green ranging stroke.
- Raised the border above terrain and configured the material/sorting to render on top, avoiding terrain occlusion and low-contrast backgrounds.
- Added one Debug-only renderer/shader diagnostic on first creation; normal logging remains OFF.
- No ranging state, Scatter, ScatterChance, smoke, AP/ammo, damage, suppression, or reset rules changed from 0.4.2.

## 0.4.2

- Hardened mortar ranging persistence against unrelated stance calls introduced by other runtime systems.
- Firing solutions now clear on `SetStance` only when the actor actually transitions from deployed to not deployed.
- Successful vehicle movement still clears the solution as before.
- Added debug-only `MORTAR RESET` reasons for actual undeploy/movement resets.
- No mortar Scatter, ScatterChance, AP/ammo, damage, suppression, or highlight color balance changed from 0.4.1.

## 0.4.1

- Fixed mortar ranging feedback interfering with delayed off-map markers such as supply drops.
- Removed all use of MENACE's shared `DelayedAoE3` / `DelayedEnemyAoE3` tile-highlight channels, their enable flags, and delayed-AoE color overrides.
- Ranging feedback now uses an independent world-space `LineRenderer` border, preserving the red-to-green Scatter progression without touching off-map ability rendering.
- No mortar balance/ranging rules changed from 0.4.
- Debug logging remains OFF by default.

## 0.4

- Reworked mortar ranging to tighten effective Scatter rather than reduce ScatterChance.
- Each prior shot reduces Scatter by 1; misses stop at Scatter 2; only an exact intended-tile impact unlocks Scatter 1.
- Default progressions: light 3 -> 2 -> 1(hit only), medium 4 -> 3 -> 2 -> 1(hit only), 120 mm 5 -> 4 -> 3 -> 2 -> 1(hit only).
- Added Debug-only per-shot diagnostics for target, impact, direct hit, shot Scatter, next Scatter and unchanged ScatterChance.

# Changelog

## 0.3

- Reworked mortar ranging values around an explicit **exact aimed-tile probability** instead of an abstract scatter bonus.
- Default ranging gain is now +15 percentage points per prior shot, with an 85% normal exact-tile cap and 95% exact-tile chance after an exact-impact firing solution is found.
- The light-mortar default progression is 15% base -> 30% -> 45% -> 60% -> 75% -> 85%; an exact impact promotes the tile immediately to 95%.
- Replaced the single white native marker with a native red/green two-channel blend: red at early registration, amber/yellow through intermediate stages, green near the cap, and strongest green for a found solution.
- The optional center label now reports the real exact-tile probability (`xx%`) instead of `+xx`.
- Old 0.2.x ranging setting keys are no longer used; the new probability settings get the clearer 15/85/95 defaults even on existing installs.
- Debug logging remains OFF by default.

## 0.2.2

- Moved progressive ranging application and per-shot registration to `Skill.ApplyToTile`, so the state no longer depends on the private `Skill.Scatter` helper being the execution hook.
- Kept `Skill.Scatter` only as the exact-impact probe for the immediate +60 firing-solution promotion.
- Added a native MENACE tile-highlight marker for registered target tiles of the selected mortar actor, while retaining the centered green `+xx` label and green projected border.
- No balance changes: +10 per prior shot, +50 cap, +10 direct-hit bonus. Debug logging remains OFF by default.

## 0.2.1

- Fixed cumulative mortar ranging state identity: actor/weapon keys now use stable native IL2CPP pointers instead of managed wrapper `GetHashCode()` values.
- Fixed the accuracy bonus not being found on later shots at the same tile; the existing `ScatterChance` reduction now persists for the same physical mortar while its firing position remains established.
- Bound the ranging hook directly to the real `Skill.Scatter(Tile)` argument instead of reading a boxed `object[]` argument list.
- Ranging feedback now appears when the mortar actor itself is selected, not only while a mortar fire skill is actively targeting. Selecting a specific mortar skill still narrows the overlay to that physical weapon.
- Debug mode now emits one concise `RECORD` line after a mortar scatter result and one `APPLY` line when a saved bonus is used; Debug remains OFF by default.
- No balance values changed: +10 per prior shot, +50 normal cap, and +10 direct-hit solution bonus (+60 total) remain the defaults.

## 0.2

- Added tactical-map feedback for cumulative mortar ranging: selecting a mortar fire skill draws a green border around tiles previously ranged by that physical mortar and shows the current `+xx` bonus at tile center.
- Added firing-solution detection from MENACE's actual `Skill.Scatter` resolution: if the impact lands on the exact intended tile, that tile immediately jumps to the normal ranging cap plus the direct-hit solution bonus.
- Defaults remain +10 per successful prior shot and +50 normal cap; the new direct-hit bonus defaults to +10, so a found solution is +60.
- Added `MortarDirectHitBonus` to Mod Settings (0-25 percentage points).
- Found solutions remain per physical mortar/per tile, are shared between HE and smoke on the same weapon, and still clear when infantry undeploys or a vehicle mortar moves.
- Overlay rendering is local UI only and does not consume or recolor MENACE's native tile-highlight layers.
- Routine gameplay logging remains silent; diagnostics remain Debug-only.

## 0.1

- Initial standalone extraction from Weapon Ranges Rebalanced.
- Moved mortar AP/ammo economy, HE damage/armor-durability scaling, scatter, broader suppression, suppression-only outer ring and cumulative per-tile ranging into this module.
- Moved Assault Rifle Sustained Fire / Battle Rifle Spray AP+ammo economy and Aimed Fire AP ceiling into this module.
- Does not modify weapon ranges.
- Does not contain the long-range indirect-fire AI `InfluencesPositioning=false` turn-performance fix; that remains in WRR.

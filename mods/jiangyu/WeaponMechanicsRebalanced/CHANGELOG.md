## 0.4.15 — muted mortar feedback, defer display update until impact (#61)

- Ranging markers now use a muted dark-red-to-green palette based on effective Scatter, with a brighter green for a solved shot.
- The displayed ranging value and color remain at their prior state while a shell is in flight and advance only after the impact callback is observed. If the impact hook is unavailable, the feedback stays hidden rather than updating early.
- Presentation only: mortar ballistics, Scatter, ScatterChance, action economy and damage are unchanged.
- The user confirms the in-game visual/timing acceptance for #61 on the Windows test install; this isolated 0.4.15 candidate excludes the separate, unaccepted #59 balance change.

## 0.4.11 — observed Unity proxy failures (#56; visual retest pending)

- The Windows 0.4.10 test loaded successfully but both screen overlays failed with `NotSupportedException: Method unstripping failed`. Its `GUI.DrawTexture(Rect, Texture)` overload chain ends in an explicit unsupported proxy stub. Solid lines now use the native `GUI.Label(Rect, GUIContent, GUIStyle)` entry point with a white background texture; background tint is isolated/restored too.
- World borders failed in `MethodInfoStoreGeneric_AddComponent_Public_T_0<LineRenderer>`. Component creation now uses non-generic `AddComponent(Il2CppType.Of<LineRenderer>())` and a managed cast, avoiding the stripped native generic instantiation.
- Failed world renderers stop retrying for the scene. Debug-only error entries retain stack traces; normal logging remains unchanged. No balance or off-map highlight changes.
- Added metadata-only checks against actual proxy IL and the compiled WMR DLL, including a positive control demonstrating the old DrawTexture failure. These are not native rendering tests.

## 0.4.10 — mortar targeting feedback (#56; in-game verification pending)

- Added a separate yellow cursor dispersion envelope and `Spread N` label. Native blast AoE remains unchanged: it is not the mortar's scatter preview. The envelope follows the game's inclusive square scatter bounds and uses the same per-weapon/per-target calculation as firing, even before the first shot.
- Hardened remembered-tile screen borders with a dark outline, camera fallback and isolated/restored IMGUI color, matrix, depth and enabled state. Feedback no longer relies solely on the world-space line shader being visible.
- Both renderers now prefer the selected mortar's actor and physical weapon; an unregistered second mortar cannot inherit another mortar's registered markers. HE/smoke labels use the selected skill's own base scatter with the shared ranging history.
- Added offline C# scatter/outline checks and source-wiring guards. Balance, scatter chance, blast damage and off-map highlight channels are unchanged. These checks do not establish in-game visibility.

## 0.4.9 — MEN-004 ammo drain fix

- Fixed the alternate-fire ammo drain (GitHub issue #4). The `Skill.Use` postfix that charges the extra round for Sustained Fire / Spray ignored the usage flags and also ran on the game's simulated usages (`Fake | LowerAudioVolume | Free`, native ammo delta 0), which MENACE issues repeatedly during the enemy turn. Each simulated call removed 2 rounds and synchronized the loss to the sibling fire mode; the 0.4.8 ledger recorded a squad going 22 → 0 across one AI phase without firing. Usages flagged `Fake` or `NoUsesConsumed` are now left untouched by both the pre-use guard and the extra-cost postfix. Real player shots are unchanged (native 1 + extra 1).

## 0.4.8 — MEN-004 ammo ledger

- Added a **debug-only `[MEN-004 LEDGER]`** trace for limited-use skills: `Skill.SetUses` and `Skill.ConsumeUse` (with the managed caller chain, so mod-driven changes such as Overwatch reconciliation are attributable), plus `OnTurnStart`/`OnTurnEnd`/`OnMissionStarted` when the counter changes across the callback (native turn-boundary handlers). Hooks return immediately with Debug logging OFF. No gameplay change. GitHub issue #4.

## 0.4.7

- Fixed `SkillTemplate` lookups silently failing under Jiangyu, so runtime skill adjustments that depend on them were not applied. The Jiangyu host resolved template types only in `Il2Cpp*` proxy assemblies, but the game's own proxy keeps its original name (`Assembly-CSharp.dll`), so no game template type could be resolved. The host now also searches `Assembly-CSharp` and reads templates through `DataTemplateLoader.GetAll<T>()` / `TryGet<T>()`, the per-type cache the game itself uses. No balance changes.

## 0.4.6

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## 0.4.5 — MEN-004 opt-in ammunition tracing

- Added **one debug-only `[MEN-004 AMMO]` entry per high-volume alternate-fire `Skill.Use`** (never per projectile, frame, or turn), including unit/weapon identity, usage flags, success/guard result, and ammo/use/charge counters at **before -> native -> after WMR** stages.
- Records `GetUsesConsumed()`, `GetChargesPerUse()`, native ammo delta, requested extra consumption and actual `ConsumeUse()` calls to distinguish native use cost from WMR's extra synchronization.
- No gameplay, AP, ammunition-cost or suppression changes. `DebugLogging` remains **OFF** by default. Diagnosis requires rebuilding and in-game testing; GitHub issue #4 remains OPEN.

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

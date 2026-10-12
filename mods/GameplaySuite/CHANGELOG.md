## Repository / Suite 0.33.2 — MEN-001 enemy-oriented smoke fix (unverified)

- Field test of 0.6.1: smoke grenades are **used** by enemies, but often thrown in the wrong or opposite direction.
- Root cause: our direct AI action chose smoke targets from `Actor.GetDirection()` and rotation offsets, which are **not** the source of incoming fire or the front toward known enemies.
- Fix 0.6.2: select the nearest *currently detected*, AI-known opponent; suppressed/pinned infantry may prefer their last detected attacker when within 2x nearest threat distance. Smoke only on tiles which **reduce distance to this known threat**. No currently detected enemy => no forced speculative throw.
- Complexity capped at 24 known-opponent checks and six local tile checks per eligible AI turn, without a pathfinder, world scan, fog-of-war cheat, new periodic hooks or normal logging.
- Verified geometry tests in all four cardinal and diagonal quadrants; still needs Windows compilation and in-game behavior/performance validation. MEN-001 remains open, PR draft.

## Repository / Suite 0.33.1 — MEN-001 proactive smoke regression fix (unverified)
- Enemy AI threw zero smoke grenades with 0.6. Native GetTargetValue score floors alone were not sufficient.
- Smoke Overhaul 0.6.1 uses one native Agent.Execute opportunity per AI infantry unit/turn for Pinned Down, Suppressed, or exposed flamethrowers with known opponents. Checks at most 6 local tiles (2 forward for flamethrowers), no pathfinding or hidden-enemy query.
- Native smoke skill IsUsable, IsAffordable, IsUsableOn, limited uses and AI-only cooldown are checked before invoking Skill.Use. Pinned smoke retains zero-AP exception only for two smoke IDs.
- Late-priority Agent.Execute Harmony prefix honors earlier mods such as Infantry Assault. Aggregated in-memory diagnostics, only one unexpected exception logged; default debug OFF.
- Windows build, gameplay smoke activation and 96x96 turn-performance validation still pending. PR #12 remains draft, issue #1 open.

## Repository / Suite 0.33 — MEN-001 tactical smoke (source-only, unverified)

- Smoke Overhaul 0.6: allow pinned smoke grenades with 0 AP exclusively in Pinned Down; native normal/Suppressed AP unchanged.
- Lightweight native immediate-target score floors for Pinned Down, Suppressed and exposed flamethrower infantry. No speculative search, no new map scan, no force-moving AI and no default logs.
- Settings AllowSmokeWhilePinnedDown and SimpleAISmokeRules default ON, no game verification yet.

## Repository / Suite 0.32.2 — MEN-004 alternate-fire ammo diagnostic

- **Weapon Mechanics Rebalanced 0.4.4 -> 0.4.5:** added opt-in, per-activation `[MEN-004 AMMO]` snapshots of sustained/spray fire, distinguishing native skill consumption from WMR's own extra-ammo cost.
- Logs actor/weapon, `UsageParameter`, success/guard, `GetUses`, `GetMaxUses`, `GetCharges`, `GetUsesConsumed` and `GetChargesPerUse` (before/native/final where available), plus extra `ConsumeUse()` calls.
- Entire trace is enabled only by **Weapon Mechanics Rebalanced -> Debug logging**. Normal logging remains quiet; no changes to ammo economy, AP, mortar mechanics or other modules.
- Build and in-game test pending. **MEN-004 remains open** until actual ammo-counter behavior is verified.

## Repository / Suite 0.32.1 — MEN-005 Assault aim-cache fix

- **Infantry Assault 0.5.2 → 0.5.3**: validates native cached executing-soldier indices during player Assault hover/preview. If squad casualties invalidate the cached list, rebuild it from MENACE's current executing-element selection before the native `Actor.AimAt(Vector3)` path.
- Covers the 110 repeated `ArgumentOutOfRangeException` entries reported in the October 12 log (three bursts immediately before Assault confirmation). No global exception suppression or blanket `Actor.AimAt` patch; Mount Up and ordinary weapon targeting remain untouched.
- Per-skill/squad-size caching prevents repetitive checks on every mouse-move. Fail-closed behavior affects only an unsafe Assault preview frame, with at most one debug-only warning per tactical scene. Default debug logging stays OFF.
- No change to AP, movement, CQB, garrison combat, AI, ammo, or other suite modules. Windows build and actual in-game acceptance tests pending; MEN-005 stays open.

## Repository / Suite 0.32 Starting Options

- New standalone/suite module **Starting Options 0.1**. In Mod Options, `InitialLeaderMaximum` extends the native New Game / Customize Difficulty Initial Leaders slider maximum from 6 to 12 by default (configurable 6-12). The limit is reduced to the available pickable leader pool if fewer than requested exist.
- `HeadstartSparePartsCount` controls the **Black Market Headstart** reward: 36 Spare Parts by default, configurable 0-1000. The existing checkbox remains authoritative; no bonus if it is off. Only the matching `commodity.spare_parts` items are adjusted and each retains vanilla trade value 5.
- Both settings apply only to new campaigns. No tactical hooks, no per-frame/per-turn logs, optional diagnostics OFF by default. Gameplay validation on a Windows MENACE installation remains required.

## Repository / Suite 0.31.8 Fast AI smoke selection fix

- Smoke Overhaul updated **0.5.1 -> 0.5.2**. Built from the installed **0.31.6** suite source; version 0.31.7 already belongs to a separate no-Assault test variant.
- Fixed pathological AI `CreateLOSBlocker.Collect` evaluations on 80x80 battles (observed up to 82.6 seconds cumulatively for mortar turns). MENACE's native `Assist.OnCollect` reads `SkillTemplate.AIConfig.InfluencesPositioning` and skips speculative future-position evaluation when false.
- `FastAISmokeSelection` is ON by default: native smoke behaviors evaluate using their current firing position, still allowing smoke to be thrown/fired, without searching future positions solely to deploy smoke. Applies to both infantry smoke grenades (including free smoke carried by mortar squads) and mortar smoke shells.
- Original `InfluencesPositioning` values are saved by native IL2CPP **profile pointer**, not by skill ID, to safely handle profiles shared between infantry and mortar skills; restored when disabled or unloaded. Existing AI smoke grant and two-round mortar smoke cooldown are preserved.
- One activation log per session, no per-turn AI logging; no changes to `FindPath`, weapon ranges, smoke AoE, ammunition, mortar ranging, player movement, or any other gameplay component. Performance and actual smoke usage remain to be verified in-game.

## Repository / Suite 0.31.6 Mortar ranging renderer hotfix

- Weapon Mechanics Rebalanced updated 0.4.3 -> 0.4.4 (rendering bugfix). Infantry Assault 0.5.2 and all other modules remain unchanged.
- Fixed a Unity renderer-component conflict: 0.4.3 tried adding both the dark outline and colored inner `LineRenderer` to the same `GameObject`. The new version places them on two separate child GameObjects and destroys partial roots on errors.
- Preserves independent world-space overlays and does not touch native delayed-AoE highlight channels, so Supply Drop/off-map marker expiration remains unmodified.
- Adds at most one world-renderer error diagnostic per scene when Debug is explicitly enabled. Debug stays OFF by default.
- No changes to Scatter, suppression, AP, ammunition, per-tile ranging, AI, or saved data.

## Repository / Suite 0.31.5 Infantry Assault skill detection fix

- Infantry Assault updated 0.5.1 -> 0.5.2 (bugfix). All other suite components are unchanged.
- Fixed false `missing Assault skill` counts for existing player/AI infantry. `SkillContainer.GetAllSkills()` and the add queue contain `BaseSkill` wrappers; the old code called a `Skill`-specific cached reflected method on them and failed to recognize present actions.
- Assault skill recognition now uses the live wrapper's `GetID()` with a template-ID fallback and caches confirmed native IL2CPP pointers instead of managed wrapper identities.
- Before any loaded-save fallback creates a skill, the current and queued collections are checked again to avoid duplicate Assault entries after a native partial refresh.
- No AP, CQB, cover, AI assault selection, targeting, or victory-advance change. Debug remains OFF by default; no routine logging added.

## Repository / Suite 0.31.4 Field Logistics concrete Skill use-state fix

- Field Logistics updated from 1.0.5 to 1.0.6; repository/suite updated from 0.31.3 to 0.31.4.
- Fixed player-vehicle resupply use initialization on current MENACE/Il2CppInterop builds: `SkillContainer.GetAllSkills()` returns `BaseSkill` wrappers, so reflection on the wrapper could not find `Skill.SetMaxUses(int)`, `SetUses(int)`, `SynchronizeUses()`, `GetMaxUses()`, `GetUses()`, or `GetCharges()`.
- Matching Field Logistics actions are now safely re-cast to the concrete `Skill` wrapper with `TryCast<Skill>()`; vehicle resupply then uses the generated interop methods directly.
- The 1.0.5 runtime missing-skill recovery path and Mount Up/load-order compatibility hardening are retained unchanged. No balance or default-logging changes.

## Repository / Suite 0.31.3 Field Logistics runtime skill-injection hardening

- Field Logistics updated from 1.0.4 to 1.0.5 without balance changes; repository/suite updated from 0.31.2 to 0.31.3.
- Player vehicles missing `Resupply Infantry` now retry MENACE's native `AddInherentSkills(EntityTemplate)` path and, only if still missing, append an instance of the already-registered Field Logistics `SkillTemplate` to the live `SkillContainer`.
- The same idempotent recovery path protects `Redistribute Ammo` on player infantry.
- The fallback never replaces the existing skill list and remains silent unless Field Logistics Debug logging is enabled.

## Repository / Suite 0.31.2 mortar ranging highlight visibility fix

- Weapon Mechanics Rebalanced updated from 0.4.2 to 0.4.3; suite updated from 0.31.1 to 0.31.2.
- Kept the 0.31.1 smoke-delivery and ranging-reset fixes unchanged.
- Reworked only the independent mortar ranging renderer introduced after the supply-drop compatibility fix: each ranged tile now uses a thick dual world-space border (dark under-stroke + red-to-green inner stroke), raised above terrain and forced to render on top.
- The renderer still does not touch MENACE TileHighlighter channels, delayed-AoE enable flags, or color overrides, so Supply Drop/off-map markers remain isolated.
- Debug-only diagnostics report the selected shader once when the first mortar ranging border is created. Normal logging remains unchanged.

## Repository / Suite 0.31.1 mortar highlight runtime fix

- Weapon Mechanics Rebalanced updated from 0.4.1 to 0.4.2 and Smoke Overhaul from 0.5 to 0.5.1; suite updated from 0.31 to 0.31.1.
- Removed Smoke Overhaul's unnecessary 0.5-second reapplication of mortar smoke-delivery template/AI mutations during tactical play. Delivery profiles are now applied only at initialization, scene load, or when the relevant settings actually change.
- Hardened mortar ranging reset logic: `Actor.SetStance` now clears a firing solution only when a mortar actually transitions from deployed to not deployed, instead of clearing on any non-Deployed `SetStance` call.
- Added debug-only `MORTAR RESET` reasons for real undeploy and movement resets.
- No smoke-footprint, AI-smoke, mortar Scatter/ScatterChance, AP/ammo, or normal logging behavior changed.

## Repository / Suite 0.31 smoke-delivery footprints + AI use

- Smoke Overhaul updated from 0.4 to 0.5; suite updated from 0.30.1 to 0.31.
- Infantry smoke grenades and light-mortar smoke now affect only the targeted tile.
- The 80 mm medium mortar gains the existing vanilla mortar-smoke skill and creates a five-tile cross-shaped smoke footprint.
- Heavy/120 mm mortar smoke retains the vanilla 3x3 footprint.
- `Skill.GetAoeTiles` and `Skill.GetUnsortedAoeTiles` are filtered to the configured footprint for targeting/AI evaluation, with a second `SpawnTileEffectHandler.OnApply` guard enforcing the actual spawned smoke area.
- Mortar smoke skills inherit MENACE's native `CreateLOSBlocker` AI profile when missing and reuse the infantry smoke skill's two-round AI-only cooldown, enabling deliberate AI smoke use without a custom per-frame scanner or consecutive smoke spam.
- AI smoke-use diagnostics are debug-only; normal logging remains silent.

## Repository / Suite 0.30.1 mortar highlight compatibility fix

- Weapon Mechanics Rebalanced updated from 0.4 to 0.4.1; suite updated from 0.30 to 0.30.1.
- Fixed a regression where mortar ranging feedback reused MENACE's `DelayedAoE3` / `DelayedEnemyAoE3` channels. Those channels are also used by delayed off-map abilities such as supply-drop markers, so WMR could keep a completed supply-drop area visible by repeatedly forcing the shared highlight layer enabled.
- Mortar ranging feedback now uses an independent world-space `LineRenderer` border. WMR no longer calls `SetHighlightEnabled`, `SetColorOverrides`, `Set`, `Unset` or `Replace` on delayed-offmap highlight channels.
- Mortar ranging gameplay is unchanged: prior misses reduce effective Scatter only to 2, exact impact alone unlocks Scatter 1, and ScatterChance remains unchanged.
- Debug logging remains OFF by default.

## Repository / Suite 0.30 mortar scatter-ranging redesign

- Weapon Mechanics Rebalanced updated from 0.3 to 0.4; suite updated from 0.29.1 to 0.30.
- Mortar ranging no longer modifies `ScatterChance`. The configured light/medium/120 mm chances stay unchanged on every shot.
- Each prior shot at the same tile reduces effective `Scatter` by 1 for the next shot: light 3->2, medium 4->3->2, 120 mm 5->4->3->2. Misses are hard-capped at Scatter 2.
- Only an exact impact on the intended tile finds the firing solution and unlocks Scatter 1. Moving the platform or leaving the deployed stance still clears remembered solutions.
- Native ranging feedback now tracks effective Scatter rather than exact-tile probability. The optional center label is `S2`, `S1`, etc.; full green is reserved for the solved Scatter 1 state.
- Debug-only mortar diagnostics now report target, resolved impact when available, direct-hit status, Scatter used for the shot, Scatter prepared for the next shot, unchanged ScatterChance, shot count, and solution state. Debug remains OFF by default.

## Repository / Suite 0.29.1 Infantry Assault stale-targeting fix

- Infantry Assault updated from 0.5 to 0.5.1; suite updated from 0.29 to 0.29.1.
- Fixed the remaining stale-targeting path after a committed player Assault: native `SkillAction.HandleMouseMoveOnTile` is now suppressed only for the exact Assault action instance that already launched combat.
- Prevents repeated `Actor.AimAt` `ArgumentOutOfRangeException` errors when CQB casualties change squad element counts before the stale targeting action is discarded.
- Existing immediate/deferred TacticalState cleanup is retained. No Assault balance, AI behavior, AP/ammo rules, victory movement, or default logging changed.

## Repository / Suite 0.29 mortar ranging readability + balance

- Weapon Mechanics Rebalanced updated to 0.3; suite updated to 0.29.
- Mortar ranging is now expressed as actual exact aimed-tile probability instead of abstract `+xx` scatter bonuses.
- New defaults: +15 percentage points exact-tile chance per prior shot, 85% normal cap, 95% after an exact-impact firing solution.
- Native ranging feedback now blends from red through amber/yellow to green, with the strongest green reserved for a found firing solution.
- Optional center text now reports `xx%` exact-tile chance.
- Old 0.2.x ranging settings are superseded by the clearer probability-based settings; normal gameplay logging remains silent.

## Repository / Suite 0.28.1 mortar ranging execution + visible feedback fix

- Weapon Mechanics Rebalanced updated from 0.2.1 to 0.2.2; suite updated from 0.28 to 0.28.1.
- Moved progressive mortar ranging application/recording from the private `Skill.Scatter` helper to the stable public `Skill.ApplyToTile` execution boundary. A shot now records +10 even if the private scatter detour is bypassed; later shots apply the saved reduction for the full native tile-resolution call.
- `Skill.Scatter` is now only used to detect an exact impact on the intended tile and promote that registration immediately to the default +60 firing solution.
- Added a native MENACE `TileHighlighter` mark for every registered target of the selected mortar actor, while retaining the green `+xx`/border overlay. This gives the feedback a game-native visual fallback instead of relying solely on IMGUI projection.
- Ranging still resets when the mortar firing position is broken (infantry undeploy or successful vehicle movement).
- Balance remains unchanged: +10 per prior shot, +50 normal cap, +10 direct-hit bonus; Debug logging remains OFF by default.

## Repository / Suite 0.28 assault state rules + integrated runtime fixes

- Integrated the current mortar-ranging runtime fixes from Suite 0.27.1 unchanged: Weapon Mechanics Rebalanced remains 0.2.1 with native-pointer mortar identity, typed Scatter target binding, and selected-mortar firing-solution overlay behavior.
- Infantry Assault updated to 0.5. Assault cannot be initiated while the attacker is deployed/kneeling, Suppressed, or Pinned Down; player and AI paths share the same gate.
- Preserved the post-0.27 CQB fixes: Suppressed defenders cannot defensive-fire, while Pinned Down defenders can defensive-fire with half CQB accuracy and half cover-reduction bonus; ready defenders and assaulters retain full configured CQB bonuses.
- Preserved deferred Assault targeting cleanup to prevent stale `SkillAction` / `Actor.AimAt` errors after CQB casualties.
- Coordinated Fire updated to 1.6.7, including guarded recovery of the verified asynchronous `NoApConsumed` AP-to-zero side effect before applying the single coordinated-fire fee.
- Normal gameplay logging remains minimal and debug diagnostics remain OFF by default.

## Repository / Suite 0.27.1 mortar ranging runtime fix

- Weapon Mechanics Rebalanced updated from 0.2 to 0.2.1; suite updated from 0.27 to 0.27.1.
- Fixed mortar solution lookup by replacing managed IL2CPP wrapper `GetHashCode()` keys with stable native actor/item pointers. This fixes both cumulative accuracy not carrying into later shots and the overlay failing to find saved tiles.
- Bound the ranging prefix/postfix directly to `Skill.Scatter(Tile)`'s typed target argument.
- The green tile-border / `+xx` overlay now appears when the mortar unit itself is selected; selecting a mortar fire skill narrows it to that physical weapon.
- Debug-only per-shot diagnostics now report `RECORD` and `APPLY` state transitions; normal logging remains silent.
- Ranging balance is unchanged: +10 per prior shot, +50 normal cap, exact-tile hit immediately marks +60 with the default direct-hit bonus.
- Infantry Assault remains 0.4 from suite 0.27; no assault balance changed in this patch.

## Repository / Suite 0.27 symmetric Infantry Assault CQB

- Infantry Assault updated from 0.3.2 to 0.4; suite updated from 0.26.1 to 0.27.
- Defensive fire now receives the same configurable CQB accuracy bonus as assault fire (default +20 percentage points).
- Assault Cover is applied symmetrically to the target in both directions: defender firing at attacker and attacker firing at defender.
- 40 AP cost, ammo consumption, AI suppression gate, weapon selection and post-victory advance are unchanged.
- Debug logging remains OFF by default.

## Repository / Suite 0.26.1 build fix

- Forest Traversal updated from 0.1.1 to 0.1.2.
- Fixed Forest Traversal compilation by importing the `Menace.SDK` namespace that provides the existing `ModSettings` API used by the module.
- No mortar, forest gameplay, balance, or UI behavior changed from 0.26.

## Repository / Suite 0.26 mortar ranging feedback and firing solutions

- Weapon Mechanics Rebalanced updated from 0.1 to 0.2.
- When a mortar firing skill is selected, tiles already ranged by that physical mortar now show a thin green tactical-map border and a small centered `+xx` ranging value.
- Exact impact detection is taken from MENACE's actual `Skill.Scatter` result. If the resolved impact tile equals the intended target tile, the firing solution is immediately considered found.
- Default ranging remains +10 per successful shot with a +50 normal cap; the new direct-hit solution bonus defaults to +10, producing +60 once the solution is found.
- Added configurable `MortarDirectHitBonus` (0-25 percentage points).
- HE and smoke on the same physical mortar continue to share solutions; infantry undeploy and vehicle movement continue to clear them.
- Overlay drawing is independent of MENACE's native highlight textures/layers, avoiding conflicts with movement, skill-range and AoE overlays.
- No additional routine logging was added; Debug remains OFF by default.

## Repository / Suite 0.25.1 Dense Forest traversal fix

- Forest Traversal updated from 0.1 to 0.1.1.
- Fixed Dense Forest still being unusable as an infantry destination: the template now uses MENACE's native `EntityContainerType.Forest` in addition to `IsTraversableByInfantry=true`.
- Infantry can enter/end movement inside Dense Forest and can path through it; the existing `cover_inside_forest` profile and native 30 AP enter / 30 AP leave values are preserved.
- Both standalone and suite manifests patch `environment_backbone_forestdense` directly through Modpack Loader, with a typed runtime reapply as fallback.
- Vehicles remain blocked because vehicle actors are not containable and the traversal flag is infantry-only.
- No custom pathfinder/AP override was added; Dense Forest LoS, external cover, destruction and visuals are unchanged.
- Normal logging remains silent; diagnostics are still opt-in.

## Squaddie Veterancy 1.10 compatibility API

- Added the public read-only `SquaddieProbe.CompatibilityApi` for optional integrations with other mods.
- `GetSquaddieXp(int squaddieId)` returns current persistent individual XP, or `-1` when the runtime is unavailable.
- `GetSquaddieRank(int squaddieId)` returns the individual rank display name, or an empty string when unavailable.
- Uses MENACE native squaddie IDs; no gameplay, persistence, balance, or logging behavior changed.

## Repository / Suite 0.25 Squaddie Veterancy compatibility API

- Squaddie Veterancy updated from 1.9.6.1 to 1.10.
- Added the public read-only `SquaddieProbe.CompatibilityApi` for optional integrations with other mods.
- Added package cleanup so each standalone build removes older `FeatureName-*.zip` artifacts before writing the current package; suite packaging similarly removes older `MenaceGameplaySuite-*.zip` artifacts.
- No gameplay, balance, persistence, or logging behavior changed.

## Repository / Suite 0.24.2 Coordinated Fire Run & Gun AP fix

- Coordinated Fire updated from 1.6.5 to 1.6.6.
- Fixed the `AP accounting uncertain after child attack` failure triggered when Lim's Run & Gun / Full Send one-shot AP discount expires between the two coordinated child attacks.
- Coordinated child attacks now use MENACE's native `UsageParameter.NoApConsumed`, preserving native ammo/limited-use consumption while Coordinated Fire settles its single max-cost AP fee once at the end.
- Defensive AP reconciliation now snapshots the live child AP cost immediately before each native launch, so temporary cost modifiers cannot leave a stale expected debit.
- No other suite gameplay modules or balance values changed.

## Repository / Suite 0.24.1 indirect-fire AI positioning fix

- Weapon Ranges Rebalanced updated to 2.8.1.
- Generalized the proven medium-mortar `AIConfig.InfluencesPositioning=false` large-map fix to all current long-range no-Line-of-Fire attack families: light/medium/120 mm mortars, pirate/modular rocket organs, Local Forces/Rogue Army artillery, and current ATGM indirect-fire modes.
- Direct-fire artillery such as the 15 cm assault howitzer is deliberately excluded so normal line-of-fire repositioning remains available.
- The 2026-10-05 pirate test log showed `active.rocket_organ_barrage.fire` taking about 8.5 seconds from AI attack observation to completed native fire, without crossing the 12-second timeout or producing `Collection was modified`; this motivated closing the same positioning-search hole across indirect artillery before it reaches the proven mortar failure mode.
- No range, damage, AP, suppression, animation or ammo changes in this patch. Normal logging remains silent; Debug emits only one indirect-AI policy summary per apply pass plus failures.

## Repository / Suite 0.24 WRR / weapon-mechanics split

- Weapon Ranges Rebalanced updated to 2.8 and narrowed to range changes plus the verified medium-mortar `AIConfig.InfluencesPositioning=false` turn-performance fix.
- Added Weapon Mechanics Rebalanced 0.1 as a separate standalone/suite module.
- Moved mortar AP/ammo economy, HE damage/armor-durability scaling, scatter/suppression behavior, suppression-only outer ring and cumulative per-tile ranging memory from WRR into WMR.
- Moved Assault Rifle Sustained Fire / Battle Rifle Spray AP+ammo economy and the Aimed Fire AP ceiling from WRR into WMR.
- WRR retains realistic weapon/vehicle ranges, 34/42/48 mortar reach, proportional secondary-skill range synchronization, family sliders and JSON exact range overrides.
- No intended gameplay change when both modules are enabled; the change is ownership/separation so users can install range+AI-fix without the additional weapon-balance mechanics.

## Repository / Suite 0.23 Dense Forest traversal

- Added Forest Traversal 0.1 as a standalone module and to MENACE Gameplay Suite.
- Vanilla `environment_backbone_forestdense` (Dense Forest) is now traversable by infantry.
- Dense Forest remains a structure with `SurfaceType.Forest`; MENACE's native path-cost calculation therefore retains both the Forest terrain cost and the normal structure-tile movement surcharge instead of using a custom AP hack.
- Vehicles and other non-infantry units remain blocked because only `IsTraversableByInfantry` is changed.
- Dense Forest LoS blocking, cover, HP/armor, destruction behavior and visuals are unchanged.
- Debug logging remains OFF by default.

## Repository / Suite 0.22 cumulative mortar ranging

- Weapon Ranges Rebalanced updated to 2.7.
- Mortar ranging now accumulates **-10 percentage points ScatterChance per successful prior shot at the same intended tile**, capped at **-50** by default.
- Ranging memory is per physical mortar and per tile: switching targets does not erase already-ranged tiles.
- HE and smoke from the same mortar share their firing solution.
- Infantry mortar firing solutions are cleared when the crew leaves the deployed stance. Vehicle-mounted 120 mm mortar solutions are cleared when the platform moves.
- Added separate Mod Settings for ranging step (default 10, 0-25) and maximum bonus (default 50, 0-100).
- Existing doubled mortar ranges (34/42/48), AP/ammo/damage/suppression balance, cover behavior and AI positioning fix are unchanged.

## Repository / Suite 0.21 mortar range and ranging fire

- Weapon Ranges Rebalanced updated to 2.6.
- Doubled default WRR mortar maximum ranges: light 17 -> 34, medium 21 -> 42, heavy 120 mm 24 -> 48.
- Smoke mortar rounds inherit the same doubled parent-weapon reach; range-overriding 120 mm HE skills are synchronized to 48.
- Added repeat-target ranging fire for all current HE and smoke mortar skills: firing again at the exact same intended tile temporarily reduces ScatterChance by 25 percentage points by default.
- Added a Mod Settings slider for the repeat-target bonus (0-50 percentage points). The shared SkillTemplate value is restored immediately after target resolution, so a successful shot at a different tile replaces the stored firing solution.
- Existing mortar AP/ammo/damage/suppression balance, medium-mortar AI positioning policy, and alternate rifle fire-mode economy are unchanged.

## Repository / Suite 0.20 alternate rifle fire-mode economy

- Weapon Ranges Rebalanced updated to 2.5.
- Assault-rifle **Sustained Fire** and battle-rifle **Spray** now cost 40 AP, matching the corresponding normal rifle shot.
- Those high-volume alternate modes now consume exactly 2 shared weapon-ammo uses per successful activation. Native one-use consumption is reconciled at runtime and the synchronized sibling fire modes are updated immediately.
- Sustained Fire/Spray become unusable when fewer than 2 shared ammunition uses remain, including AI decisions and direct `Skill.Use` calls that bypass normal usability checks.
- Battle-rifle **Aimed Fire** keeps normal one-ammo consumption. Its current 40 AP cost is unchanged; any future/current-game variant above 50 AP is capped at 50 rather than forced down to 40.
- Applies to all current v0.7.15 Assault Rifle Sustained Fire variants (5.56, 7.62, suppressed, RMC/MRS and enhanced EM) and both current Battle Rifle Spray variants.
- Existing mortar, cover, smoke, grenade, veterancy and range behavior is otherwise unchanged.

## Repository / Suite 0.19.2 light/medium mortar sandbag protection fix

- Cover Destruction Rebalance updated to 0.3.1.
- The startup `DestroyHalfCover=0` edit for `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` is now backed by a runtime `AttackHandler.OnTileHit` guard.
- This fixes the case where MENACE/Modpack Loader rebuilt or replaced the mortar Attack handler after the startup template pass and restored sandbag destruction.
- The runtime guard is silent in normal gameplay and only logs installation/execution failures.
- 120 mm mortar behavior, WRR mortar damage/AP/ammo/suppression changes, and all other cover-destruction tiers are unchanged.

## Repository / Suite 0.19.1 vehicle Crew Pressure control fix

- Squaddie Veterancy 1.9.6.1 fixes vehicle control semantics for all Crew-Pressure-managed vehicles, regardless of side.
- Native MENACE morale is no longer allowed to make a tracked vehicle Wavering/Fleeing in gameplay while Crew Pressure is active; Crew Pressure is now the authoritative vehicle combat-stress state machine.
- Buttoned Up remains usable and applies only the configured AP and accuracy penalties.
- Break Contact remains the separate forced-retreat state at the higher pressure threshold.
- Break Contact no longer transitions visually to Buttoned Up while the same forced-retreat activation still has AP forced to zero; the pressure reset happens when that activation ends, so a vehicle shown as Buttoned Up is usable on its next activation.
- No XP, rank, pressure thresholds, damage-shock values, or infantry veterancy balance changes.

## Repository / Suite 0.19 grenade AP + persistent smoke

- Smoke Overhaul updated to 0.4.
- All hand-thrown `active.throw_*` grenade/bomb skills now cost 20 AP by default (half the vanilla 40 AP). Weapon-launched grenades, mortars, off-map bombs and deployable charges are excluded.
- Timed smoke tile effects now last 2x their vanilla number of rounds by default, configurable from 1x to 4x.
- Smoke visuals, free smoke/frag ammunition counts, soft-LOS behavior and fire-through penalties are unchanged.
- Per-tile staggered dissipation was investigated but not forced: MENACE exposes runtime tile-effect lifetime only for reading while round expiry uses private native state, so 0.4 uses the stable duration-multiplier fallback.

## Repository / Suite 0.18.1 grenade duplicate fix

- Smoke Overhaul updated to 0.3.1.
- Prevent duplicate runtime `active.throw_smoke_grenade` and `active.throw_frag_grenade_tier2` instances when MENACE adds the same skill through more than one inherent/item pass.
- If both limited and normal smoke grenade skills are presented, the normal vanilla three-use smoke skill is preferred.
- No mortar, smoke LOS, accuracy, damage, ammo or suppression balance changes from 0.18.

## Repository / Suite 0.18 Mortar smoke economy and field-cover protection

- Weapon Ranges Rebalanced updated to 2.4.
- Mortar smoke fire now follows the HE firing economy: 50% action-point cost and 2x limited uses for infantry smoke mortar, 120 mm autoloader smoke, and twin-fire smoke. Smoke pattern, VFX and tile effects are unchanged.
- Cover Destruction Rebalance updated to 0.3. Light and medium infantry mortar HE are explicitly `DestroyHalfCover=0`, so they do not destroy sandbag/low-wall half-cover.
- 120 mm mortar cover destruction is no longer in the mortar-immunity exception and follows the normal dynamic cover-destruction tier rebalance.
- Existing HE damage scaling, scatter, medium-mortar AI performance fix and suppression-only outer ring are unchanged.

## Repository / Suite 0.17 Mortar endurance and suppression

- Weapon Ranges Rebalanced updated to 2.3.
- Doubles limited HE mortar ammunition because the existing 50% firing AP cost allows roughly twice as many fire missions per turn/mission.
- Adds a suppression-only outer ring one tile beyond the native mortar blast AoE at 50% of the native suppression value; physical damage and armor-durability blast radius are not enlarged.
- The broader suppression applies through native `Actor.ApplySuppression`, so normal suppression resistance/modifiers still apply.
- Smoke-mortar ammunition is unchanged because the smoke skill does not use the halved HE firing-cost policy.

## Repository / Suite 0.16 Tier 2 fragmentation grenades

- Smoke Overhaul 0.3 now grants normal humanoid infantry the vanilla `active.throw_frag_grenade_tier2` skill by default.
- The granted fragmentation-grenade ability keeps its native 3 uses, 40 AP cost, range 1-3, cover-ignoring behavior and vanilla AI configuration.
- Player and AI infantry use the same grant path; vehicles, structures, aliens, constructs, drones and decoys remain outside the existing `infantry_default` scope.
- Added a separate Mod Settings toggle for the Tier 2 frag grant, ON by default.
- Templates that already directly contain the Tier 2 frag skill, or whose default equipment already grants it, are not given a duplicate frag button.
- Existing smoke LOS behavior, fire-through penalties and the working three-use smoke grenade grant are unchanged.

## Repository / Suite 0.15.4 Smoke Overhaul three-use infantry grenade

- Smoke Overhaul now grants the vanilla `active.throw_smoke_grenade` skill instead of `active.throw_smoke_grenade_limited`.
- Each eligible infantry unit therefore receives one normal smoke-grenade ability with the vanilla 3 uses rather than one-use limited copies.
- Existing smoke-grenade skills are still detected to avoid deliberately stacking another smoke ability.
- Smoke Overhaul version is now 0.2.4; smoke LOS and fire-through penalty behavior are unchanged.

## Repository / Suite 0.15.3 Smoke Overhaul infantry grenade fix

- Fixed the free infantry smoke-grenade grant not appearing in tactical skill bars. The previous implementation treated its tracking dictionary as authoritative even after the Modpack Loader replaced `EntityTemplate.Skills`, so it believed the grenade was still present and would not re-add it.
- Smoke Overhaul now hooks `SkillContainer.AddInherentSkills(EntityTemplate)` and guarantees the one-use vanilla smoke skill is present immediately before MENACE builds an actor's inherent skills, removing the previous load-order/timing dependency.
- The grant now targets normal humanoid combat infantry through the vanilla `infantry_default` skill group instead of every actor reported as `ActorType.Infantry`, avoiding aliens, constructs, drones and decoys.
- Smoke Overhaul version is now 0.2.3; this is a bug fix with no intended balance change.

## Repository / Suite 0.15.2 Smoke Overhaul build compatibility

- Build-only fix for Smoke Overhaul 0.2.2; no gameplay or balance changes from 0.2.
- Qualified `System.Math` calls to avoid the `Il2CppMenace.Tools.Math` name collision.
- Replaced direct `foreach` enumeration of IL2CPP `IReadOnlyCollection<EntityTemplate>` with a Unity `Resources.FindObjectsOfTypeAll<EntityTemplate>()` array scan, preserving the same infantry-only free-smoke-grenade grant.

## Repository / Suite 0.15.1 Smoke Overhaul

- Build fix: added the nullable metadata compatibility shim to Smoke Overhaul for MENACE/Unity interop assemblies.
- Smoke Overhaul version is now 0.2.1; no gameplay behavior changed from 0.2.

- Added Smoke Overhaul 0.2 as a standalone module and to MENACE Gameplay Suite.
- Smoke no longer fully blocks line of sight by default; intervening smoke reduces shared player/AI hit chance by 20 percentage points per tile, capped at 60 by default.
- Shooter/target tiles are excluded from the added line penalty so vanilla `effect.smoke` remains authoritative for units standing in smoke.
- Added an enabled-by-default grant of `active.throw_smoke_grenade_limited` to every `EntityTemplate` for which MENACE reports `IsInfantry()`, giving player and AI infantry one free vanilla smoke grenade.
- Existing direct normal/limited smoke-grenade skills are not duplicated. Vehicles and structures are excluded.
- Vanilla smoke VFX and existing smoke status effects remain untouched. Debug logging remains OFF by default.
- Suite version advances from 0.14.3 to 0.15 because this is a gameplay feature release.

## Repository / Suite 0.14.3 Infantry Assault defensive-fire diagnostic

- Infantry Assault 0.3.2 adds a narrowly scoped Debug-only diagnostic around defensive fire.
- `[ASSAULT_DEBUG] defensive-pre` records defender/attacker tiles, the exact target tile passed to native `Skill.Use`, the tile occupant, resolved target, weapon, HP, armor durability, suppression and morale.
- `[ASSAULT_DEBUG] defensive-post` records target resolution plus HP/armor/suppression/morale deltas after the native shot finishes.
- No assault mechanics, hit chance, damage, AP, ammo, cover or target selection were changed. Debug remains OFF by default.

## Repository / Suite 0.14.2 mortar economy and lethality rebalance

- Weapon Ranges Rebalanced updated to 2.2.2.
- Mortar attack AP cost is now 50% of the native skill cost.
- Mortar direct physical damage and armor-durability damage are now 50% of native via attack multipliers; suppression/morale output is deliberately not halved.
- Scatter increased again: light 3, medium 4, 120 mm heavy/twin 5. Existing broader suppression falloff, half-cover immunity and the verified medium-mortar AI positioning fix remain intact.
- Infantry Assault is unchanged in this build: defensive fire still executes the defender's native attack. A morale-only result can occur when that native shot misses while suppression still lands; this is not an intentional damage-zeroing rule in the mod.

## Repository / Suite 0.14.1 mortar salvo rollback

- Weapon Ranges Rebalanced updated to 2.2.1.
- Rolled back the experimental 4/3/2 mortar salvo implementation based on `SkillTemplate.Repetitions`. In-game testing showed repetitions share one firing/aim cycle and do not present as distinct full mortar shots.
- Light, medium and single heavy mortar attacks therefore keep their native repetition counts; native twin-fire remains unchanged.
- Retained the accepted mortar dispersion changes (greater scatter / scatter chance), broader suppression falloff, heavy-mortar accuracy reduction, half-cover immunity, secondary-fire range synchronization, and the verified medium-mortar AI `InfluencesPositioning=false` performance fix.
- No new hot-path logging or runtime sequencing hooks were added.

## Repository / Suite 0.14 veterancy HUD rank-position update

- Squaddie Veterancy updated to 1.9.6.
- Moved the 18x18 veterancy service-bar/chevron marker from below the unit HP bar to the right side of the combined HP/suppression block.
- Marker placement is derived from live UI Toolkit `worldBound` geometry rather than a fixed offset and is recalculated for pooled/rebuilt UnitHUD instances.
- Infantry uses MENACE's native suppression-bar geometry; vehicles use the mod-owned Crew Pressure meter when present.
- Marker remains an absolute overlay, preserving the no-layout-growth / no-black-rectangle behavior.
- No veterancy balance, XP, save-format, Crew Pressure gameplay, mortar, range or other module changes. No new normal-gameplay logging.

## Repository / Suite 0.13 mortar & secondary-range rebalance

- Weapon Ranges Rebalanced updated to 2.2 and Cover Destruction Rebalance updated to 0.2.
- Mortar HE now fires area salvos: 4 light rounds, 3 medium rounds and 2 heavy rounds per mortar; twin heavy fire produces 4 total rounds.
- Increased mortar scatter and removed the heavy mortar's vanilla +50 accuracy bonus. Damage, armor-durability damage and suppression are divided across the salvo so projectile count does not simply multiply aggregate output; suppression AoE falloff is reduced to broaden the beaten zone.
- Mortar HE now uses `DestroyHalfCover=0`, protecting sandbags and low-wall half-cover from mortar destruction while preserving damage/suppression against units.
- Audited current v0.7.15 range-overriding skills and added proportional range scaling for 11 previously missed secondary attack skills (12 parent-skill associations).
- Retained the verified medium-mortar `AIConfig.InfluencesPositioning=false` large-map performance fix and realistic mortar ranges.
- No new hot-path logging; Cover Destruction Rebalance is now silent on successful application.

## Repository / Suite 0.12.9.4 medium-mortar diagnostic cleanup

- Weapon Ranges Rebalanced updated to 2.1.4.4; accepted weapon and mortar ranges remain unchanged.
- Removed the temporary read-only `[MORTAR_AI_PROBE]` / `Attack.OnCollect` diagnostic instrumentation after the stress test confirmed the fix through round 8 with multiple mortar teams.
- Kept the verified IL2CPP `AIConfig.InfluencesPositioning=false` runtime policy for `active.launch_medium_mortar_frag`; this is the actual large-map AI performance fix.
- The `[MORTAR_AI_POLICY]` write/read-back verification remains available only when WRR Debug logging is enabled. Debug logging remains OFF by default.
- Removed the experimental per-agent `Agent.Evaluate()` guard; it was already absent from 0.12.9.3 and is not part of the final fix.

## Repository / Suite 0.12.9.3 medium-mortar AI positioning-policy runtime fix

- Weapon Ranges Rebalanced updated to 2.1.4.3; all accepted weapon and mortar ranges remain unchanged.
- The 0.12.9.2 stress test showed the per-agent re-entrancy guard was not the main bottleneck: it skipped only two duplicate evaluations, while round-5 medium-mortar `Attack.OnCollect()` calls still reached roughly 12 s median / 26 s peak and continued to generate `Collection was modified` failures.
- Root cause in WRR's intended 2.1.3 policy: the IL2CPP `AIConfig.InfluencesPositioning` member is exposed through a runtime wrapper/property, but the mod only looked for a managed `FieldInfo`. When that lookup failed it returned silently, so the move-to-fire search was never verified as disabled.
- The positioning-policy setter now resolves either a field or property, writes through the runtime wrapper, reads the value back, and emits a single `[MORTAR_AI_POLICY]` verification line when WRR Debug logging is enabled. A failed verification produces one warning rather than silently continuing.
- Removed the 0.12.9.2 `Agent.Evaluate()` re-entrancy guard because the stress test showed it did not materially reduce turn time and it is unnecessary if the native positioning search is successfully disabled.
- The read-only mortar probe remains available under Debug logging for verification; it still never mutates native AI collections or changes weapon range/targeting.
- No changes to Visibility Overhaul or other gameplay modules.

## Repository / Suite 0.12.9.2 medium-mortar AI re-entrancy fix

- Weapon Ranges Rebalanced updated to 2.1.4.2; weapon ranges and target-selection values are unchanged.
- Runtime probe reproduction with six Rogue Army mortar teams showed the same mortar AI pipeline being re-entered while earlier `Agent.Evaluate()` work was still active: round 1 max concurrency 1, round 2 max 2, round 3 max 3, round 4 max 8.
- At round 4 the mortar `Attack.OnCollect()` calls were taking about 12.1 seconds and failing with `Collection was modified`; six mortar actors but eight active mortar collects proves duplicate/re-entrant evaluation rather than merely six independent actors.
- Added a narrow per-native-agent re-entrancy guard for `enemy.rogue_army_mortar_weapon_team` only. If MENACE schedules a second `Agent.Evaluate()` for the same mortar while its previous evaluation is still running, only the duplicate evaluation is skipped. Different mortar teams still evaluate in parallel.
- The guard does not clamp range, filter targets, mutate AI collections, cancel tasks, modify `AIFaction.GetOpponents()`, or alter other units.
- The read-only `[MORTAR_AI_PROBE]` instrumentation remains available when WRR Debug logging is enabled so the fix can be stress-tested. Debug logging remains OFF by default.
- No changes to Visibility Overhaul or other gameplay modules.

## Repository / Suite 0.12.9.1 diagnostic probe loader fix

- Weapon Ranges Rebalanced updated to 2.1.4.1; no gameplay, range, AI-policy, or balance values changed.
- Fixed the medium-mortar diagnostic probe runtime type lookup to use IL2CPP wrapper names (`Il2CppMenace...`) for `Attack`, `Agent`, and `TacticalManager`.
- The 0.12.9 test run proved the probe had not installed, so this patch only makes the intended read-only diagnostic instrumentation actually attach.

## Repository / Suite 0.12.9 medium-mortar AI diagnostic probe

- Weapon Ranges Rebalanced updated to 2.1.4; no weapon range or gameplay balance values changed.
- Added a diagnostic-only, read-only Harmony probe around native `Menace.Tactical.AI.Behaviors.Attack.OnCollect()` for `active.launch_medium_mortar_frag`.
- The probe is gated by WRR Debug logging and is silent by default. It records `[MORTAR_AI_PROBE]` lines containing round, actor, worker thread, concurrent mortar evaluations, elapsed time, Agent task count, input/agent tile counts, attack candidate/target/origin counts and the native exception type.
- The diagnostic hook does not replace arguments, filter opponents, mutate collections, alter ranges, cancel AI tasks, suppress exceptions, or force turn completion.
- Purpose: test the observed progression from normal early rounds to mortar timeouts and `Collection was modified` failures in later rounds on enlarged maps, including whether timed-out AI work overlaps subsequent evaluations.
- Visibility Overhaul and all other gameplay modules are unchanged from Suite 0.12.8.

## Repository / Suite 0.12.8 native Vision property integration

- Visibility Overhaul updated to 3.1.9.
- Replaced the `EntityProperties.GetVision` postfix bonus with a direct additive `Vision` change during MENACE's normal `Skill.OnUpdate(EntityProperties)` property rebuild.
- This matches the stage used by vanilla `ChangeProperty` Vision effects and lets MENACE's own `SkillContainer.Update()` old/new vision comparison set `PropertyDirtyFlag.Vision` natively.
- Visibility Overhaul now uses only two runtime hooks: `SkillContainer.Update` and `Skill.OnUpdate(EntityProperties)`.
- No `Map.UpdateVisibilityForActor`, movement-finished, selection, per-frame, or manual/global visibility rebuild hooks are present.
- Movement-based infantry concealment is unchanged. Debug logging remains OFF by default.
- No other gameplay module changed.

## Repository / Suite 0.12.7 occupied multi-tile collapse fix

- Building Combat Rules updated to 0.1.4.
- Fixed the remaining hard crash when a player unit is still inside `building_civilian_shelter_1x2_01` as the structure is destroyed.
- Root cause from runtime logs: native occupant ejection starts a `Leave` movement, but 0.1.3 allowed `Structure.Die()` to continue immediately in the same call stack; the process terminated before `OnMovementFinished` could occur.
- For the exact multi-tile Garrisonable whitelist only, occupied collapse is now deferred. The module requests native ejection if needed, waits until the occupant is no longer contained and `Actor.IsMoving()` is false, keeps a short grace window for movement-finished callbacks, then resumes the original native `Structure.Die()` path.
- Empty multi-tile structures still use the existing synchronous vanilla-template normalization from 0.1.3.
- Repeated lethal hits while a collapse is pending are ignored rather than starting another ejection/death sequence.
- No changes to Visibility Overhaul, Weapon Ranges Rebalanced, Garrisonable Buildings entry/exit rules, or other gameplay modules.

## Repository / Suite 0.12.6 Visibility Overhaul stable-runtime rollback

- Visibility Overhaul updated to 3.1.8.
- Restored the exact stable runtime architecture used by Visibility Overhaul 3.1.4: `SkillContainer.Update`, `Skill.OnUpdate(EntityProperties)`, and `EntityProperties.GetVision` only.
- Removed the 3.1.7 `Map.UpdateVisibilityForActor(Actor)` hook and its temporary `Vision` / `VisionMult` mutation, which caused severe tactical visibility flicker.
- No manual map-ready or movement visibility rebuilds are present.
- The configurable flat vision bonus and movement-based infantry concealment behavior are otherwise unchanged.
- Known legacy behavior is intentionally accepted again: MENACE may visually refresh fog/visibility on its own selection/state lifecycle rather than immediately after every movement event.
- No other gameplay module changed.

## Repository / Suite 0.12.5 visibility hard-crash fix

- Visibility Overhaul updated to 3.1.7 after 3.1.6 restored the extended LoS radius but produced an early native process crash.
- 3.1.6 performed a second `Map.UpdateVisibility(...)` traversal from the `Map.UpdateVisibilityForActor()` postfix. The logs ended without a managed exception, so that re-entrant/duplicate native visibility pass has been removed.
- `Map.UpdateVisibilityForActor(Actor)` now receives the bonus through a scoped prefix/postfix override: the actor's effective bonus-aware vision is exposed temporarily as raw `Vision` with `VisionMult=1`, MENACE executes its single normal native visibility pass, then the original raw fields are restored immediately.
- `EntityProperties.GetVision()` keeps the same public +vision behavior; it suppresses the extra postfix bonus only while the scoped native visibility override is active, preventing double application.
- No global visibility rebuilds, no second `UpdateVisibility()` call, and no movement/turn/UI hooks were added.
- Weapon Ranges Rebalanced remains 2.1.3; mortar behavior is unchanged from 0.12.4.

## Repository / Suite 0.12.4 visibility lifecycle fix

- Visibility Overhaul 3.1.6 fixes the regression where the +vision radius could appear correctly at turn/map start and then collapse to vanilla after moving a player unit.
- Root cause: 3.1.5 added manual `RecalculateVisibilityBasedOnVision()` calls after map-ready/player movement, while MENACE's native `Map.UpdateVisibilityForActor()` reads its own raw vision fields instead of the modded `GetVision()` result. The manual rebuild therefore discarded the extended radius.
- Removed the map-ready/movement refresh hooks, deferred refresh queue, and all mod-triggered global visibility rebuilds.
- Added a single narrow postfix to `Map.UpdateVisibilityForActor(Actor)` that extends MENACE's own native actor visibility pass with the bonus-aware effective vision range.
- This intentionally avoids forcing visibility/state rebuilds during movement and may also resolve the reported movement-tile highlight regression; that UI behavior must be confirmed in-game.
- Weapon Ranges Rebalanced remains 2.1.3 and the 0.12.3 medium-mortar positioning change is otherwise untouched.

## Repository / Suite 0.12.3 medium-mortar AI positioning fix

- Weapon Ranges Rebalanced updated to 2.1.3.
- Keeps the medium mortar's accepted realistic WRR range unchanged; there is no range clamp or diagnostic nerf.
- For `active.launch_medium_mortar_frag` only, sets `AIConfig.InfluencesPositioning=false` while WRR is enabled. MENACE can still use the mortar from a valid current position, but it no longer builds the expensive attack-specific move-to-fire positioning search that timed out on 64x64 maps.
- Restores the skill's original `InfluencesPositioning` value if WRR is disabled.
- Adds no runtime hooks to `AI.Behaviors.Attack.OnCollect`, `AIFaction.GetOpponents`, target collections, AI tasks, turn handling, or watchdogs.
- No gameplay-source changes to the other suite modules.

## Repository / Suite 0.12.2 emergency mortar-AI rollback

- Weapon Ranges Rebalanced updated to 2.1.2.
- Fully removed the experimental AI mortar hooks from 0.12/0.12.1 after runtime testing reproduced repeated mortar AI timeouts and `Collection was modified` exceptions in native `AI.Behaviors.Attack.OnCollect()`.
- Restored WRR's known pre-optimizer runtime behavior; realistic mortar ranges remain unchanged, including the accepted medium mortar target.
- Cargo-container LoS, VMAT range synchronization, Visibility Overhaul 3.1.5, Building Combat Rules 0.1.3 and all other suite modules remain unchanged.

# Changelog

## Repository / Suite 0.12.1 build fix

- Fixed Weapon Ranges Rebalanced build failure caused by an ambiguous `Path` reference after adding the mortar AI optimizer (`Il2CppMenace.Tactical.Path` vs `System.IO.Path`).
- Qualified the seven filesystem path calls as `System.IO.Path`; mortar AI optimizer behavior and all weapon ranges are unchanged from 0.12.
- Weapon Ranges Rebalanced bumped to 2.1.1; suite bumped to 0.12.1.

## Repository / Suite 0.12 AI mortar optimizer

- Weapon Ranges Rebalanced updated to 2.1 and restores the normal realistic mortar ranges; the 0.11.3.1 max-range-14 diagnostic clamp is not part of this release.
- Added an AI-only mortar fire-mission optimizer for light/medium infantry mortars and the modular 120 mm mortar.
- Mortar `Attack.OnCollect()` receives a stable one-target opponent snapshot instead of repeatedly evaluating every known opponent at long range; the native faction opponent list is never modified.
- Fire missions persist for 2 rounds by default and are invalidated when the target dies or leaves the mortar's real current range. New targets favor enemy clusters, then suppressed/damaged units.
- The optimizer is scoped only to the mortar attack-collection phase; movement, cover, threat and all other AI evaluation still see the full opponent set.
- Optional `Avoid expensive mortar attack-position searches` disables attack-specific reposition scanning while leaving normal AI movement behaviors intact.
- New Mod Settings allow the optimizer, target-lock duration (1-4 rounds) and attack-position search suppression to be configured.
- Debug logging remains OFF by default; no per-turn/per-candidate logs are emitted unless WRR Debug logging is explicitly enabled.

## Repository / Suite 0.11.3 LoS / visibility / VMAT fixes

- Buildings Block Line of Sight 0.2.2: the two large structures titled `Cargo Containers` now block LoS; the smaller singular `Cargo Container` remains non-blocking.
- Visibility Overhaul 3.1.5: replaces the stale-fog limitation with one deferred event-driven visibility recalculation after tactical map-ready and player/player-AI movement; no continuous/full-map refresh loop.
- Weapon Ranges Rebalanced 2.0.1.2: `active.launch_vmat_missile` is now handled directly with max-range target 18 while preserving vanilla min/ideal ranges.
- Mortar ranges and AI behavior are intentionally unchanged pending separate review.
- Debug logging defaults remain OFF.

## Repository / Suite 0.11.2 multi-tile garrison collapse fix

- Building Combat Rules updated to 0.1.3 after a reproducible hard process crash when `building_civilian_shelter_1x2_01` is destroyed.
- The crash occurs at the native structure-death boundary; the logs do not require a live occupant to be present.
- Before native collapse, any contained entity is still ejected through MENACE's own containment API.
- For the exact multi-tile templates modified by Garrisonable Buildings, `Structure.Die()` now runs with the two vanilla template fields restored (`ContainerType=None`, no `ProvidesCoverInside`) and restores the garrison values immediately afterward.
- No changes to garrison entry, AP surcharges, cover balance, weapon balance, or any other suite module.

## Repository / Suite 0.11.1 stability fixes

- Fixed Infantry Assault synchronization for actors already materialized from an existing save by using MENACE's native `SkillContainer.AddInherentSkills(EntityTemplate)` path before the synthetic `CreateSkill` fallback.
- Infantry Assault is now 0.3.1; queued inherent skills are also recognized to avoid duplicate runtime injection.
- Suite deployment now removes the legacy root-level `Mods\CoverDestructionRebalance.dll` so the suite copy can load without an assembly-name collision.
- No balance or feature changes to the other gameplay modules.

## Repository / Suite 0.11 Visibility Overhaul integration

- Added Visibility Overhaul 3.1.4 as an independent feature DLL in the unified repository and MENACE Gameplay Suite.
- Gameplay source is unchanged from the accepted standalone 3.1.4 release.
- Preserves the configurable flat vision bonus, movement-based infantry concealment, stability-mode behavior, and Debug logging OFF by default.
- Suite deployment removes a standalone VisibilityOverhaul folder to prevent duplicate loading.

## Repository / Suite 0.10 Buildings Block Line of Sight integration

- Added Buildings Block Line of Sight 0.2.1 as an independent feature DLL in the unified repository and MENACE Gameplay Suite.
- Gameplay source is unchanged from the accepted standalone 0.2.1 release.
- Preserves native LoS blocking for the explicit building scope plus compatible Building/Tower containers, including Garrisonable Buildings compatibility.
- Debug logging remains OFF by default.
- Suite deployment removes a standalone BuildingsBlockLineOfSight folder to prevent duplicate loading.

## Repository / Suite 0.9 - Garrisonable Buildings integration

- Added Garrisonable Buildings 1.0.4 as the tenth isolated feature DLL in the unified repository and Gameplay Suite.
- Imported the accepted standalone 1.0.4 `Plugin.cs` byte-for-byte; gameplay behavior, candidate whitelist, footprint validation, AP surcharges, inside-cover settings and 3x3 default remain unchanged.
- Suite deployment now removes a standalone `GarrisonableBuildings` folder to prevent duplicate loading; standalone 1.0.4 packaging remains available from the same repository.
- Existing Building Combat Rules stale-actor compatibility guard continues to recognize `GarrisonableBuildings.dll` inside the suite.
- Debug logging remains OFF by default.
- No gameplay source changes to the nine modules already present in Suite 0.8.

## Repository / Suite 0.8 Weapon Ranges Rebalanced integration

- Added Weapon Ranges Rebalanced 2.0.1.1 as a ninth independent feature DLL and standalone package.
- Preserved the accepted 2.0.1 realism-oriented weapon and vehicle range tables, linked SkillTemplate MaxRange synchronization, future/unlisted auto-classification, family/overall strength controls, and external exact overrides.
- Compatibility-only WRR patch: `weapon-overrides.json` resolves from either the standalone WRR modpack or the unified Gameplay Suite folder. No balance target or gameplay logic changed.
- Packaged `weapon-overrides.example.json` with both standalone WRR and the suite; the active file is created only when missing.
- Suite deployment removes a standalone `WeaponRangesRebalanced` folder to prevent duplicate loading.
- Debug logging and full weapon-table logging remain OFF by default.
- No gameplay changes to the other eight suite modules.

## Repository / Suite 0.7 Cover Destruction Rebalance integration

- Added Cover Destruction Rebalance 0.1.4 as an eighth independent feature DLL and standalone package.
- Preserved the accepted 0.1.4 gameplay rules: DestroyHalfCover 1 stays 1, 2 -> 1, 3 -> 2, with `active.laser_rifle_gunslinger` 3 -> 1.
- Preserved the dynamic current-template scan introduced by 0.1.4; no hardcoded weapon/skill manifest was reintroduced.
- Adapted only the host lifecycle from standalone `MelonMod` to `IModpackPlugin` so the feature can live inside the suite; the original supplied source is retained under `docs/imported` for audit/reference.
- Cover Destruction Rebalance and Building Combat Rules remain separate modules: CDR owns attack-template half-cover destruction class, while Building Combat Rules owns building structure immunity/safe-collapse behavior.
- Normal operation logs only the one-time dynamic scan summary plus real errors; no per-skill runtime spam.
- No gameplay changes to the other seven suite modules.

## Repository / Suite 0.6.1 stabilization

- Field Logistics 1.0.4: removed hot-path `Redistribute Ammo candidate` and `Redistribute Ammo cache` diagnostics; gameplay unchanged.
- Building Combat Rules 0.1.2: added a narrow Garrisonable Buildings compatibility finalizer that converts stale actor-property failures from that mod to an unavailable/null result instead of allowing reflected `TargetInvocationException` spam.
- The Garrisonable guard is stack-scoped; MENACE and other mods keep normal `GetCurrentProperties()` behavior and exception semantics.
- No feature additions or balance changes; suite version uses a patch increment.


## Repository / Suite 0.6 Field Logistics integration

- Added Field Logistics 1.0.3 as a seventh independent feature DLL and standalone package from the same unified repository.
- Field Logistics gameplay source is unchanged from the supplied 1.0.3 authoring package; Debug logging remains OFF by default and infantry ammo sharing remains experimental/OFF by default.
- Merged Field Logistics template clones/patches into the Suite manifest.
- For the 16 player infantry templates shared with Infantry Assault, the Suite manifest explicitly preserves both `active.fieldlogistics_redistribute_ammo` and `mod.infantry_assault`, matching the previous standalone Field Logistics (load 180) + Suite/Assault (load 190) result.
- Suite deployment now removes a standalone `FieldLogistics` folder to prevent duplicate loading.
- No gameplay changes to Squaddie Veterancy, Coordinated Fire, Overwatch, Infantry Assault, StrongerCover or Building Combat Rules.

## Repository / Suite 0.5.1 build fix

- Building Combat Rules updated to 0.1.1.
- Fixed the Modpack Loader Harmony type signature and added the required `OnSceneLoaded` plugin callback.
- No gameplay changes from 0.5.

## Repository / Suite 0.5 Building Combat Rules

- Added Building Combat Rules 0.1 as a sixth independent feature DLL.
- Direct-fire small arms no longer damage building HP/armor; attacks against infantry garrisoned inside still resolve normally.
- Small-arms `DestroyHalfCover` is suppressed only when the impacted tile contains a building, so normal sandbag/field-cover behavior remains owned by Cover Destruction Rebalance.
- Before `Structure.Die()` removes an occupied building, the occupant is ejected with MENACE's native containment API while the structure is still valid. This targets the crash reproduced when a multi-tile garrisonable building collapses with infantry inside.
- Explosives, launchers, mortars, RPG/rockets, grenades, flamethrowers, crew-served weapons, medium/heavy MGs, anti-materiel/AT weapons and cannons remain destructive.
- Debug logging remains OFF by default.
- No gameplay changes to Squaddie Veterancy, Coordinated Fire, Overwatch, Infantry Assault or StrongerCover.

## Repository / Suite 0.4.1 Veterancy refresh

- Updated bundled Squaddie Veterancy from 1.9.5.6 to the accepted 1.9.5.10 source.
- Vehicle Crew Pressure now uses the dedicated mod-owned HUD meter from 1.9.5.10; MENACE's stale native vehicle suppression bar is hidden instead of being reused.
- Restored the 1.9.5.10 veterancy marker HUD fixes, including proper service-bar/chevron artwork and absolute overlay layout that avoids black HP-bar rectangles.
- Retained 1.9.5.10 hot-path logging suppression and rate-limited optional debug logging; debug remains OFF by default.
- No changes to Infantry Assault, StrongerCover, Coordinated Fire or Overwatch gameplay.

## Repository / Suite 0.4 StrongerCover integration

- Added StrongerCover 2.2.3 to the unified repository and MENACE Gameplay Suite as an independent feature DLL.
- StrongerCover remains independently configurable through its existing Mod Settings group and remains available as a standalone package built from the same source tree.
- Suite deployment now removes a standalone StrongerCover folder as well, preventing duplicate loading.
- StrongerCover gameplay, balance and logging behavior are unchanged from standalone 2.2.3; Debug logging remains OFF by default.
- Existing Squaddie Veterancy, Coordinated Fire, Overwatch and Infantry Assault gameplay source was not changed.

## Repository / Suite 0.3 Infantry Assault AI suppression gate

- Updated Infantry Assault to 0.3.
- AI now considers Assault only against adjacent infantry whose native MENACE `SuppressionState` is `Suppressed` or `PinnedDown`.
- Unsuppressed adjacent targets are left to normal MENACE AI behavior.
- Player-controlled Assault behavior is unchanged.
- No changes to Squaddie Veterancy, Coordinated Fire or Overwatch gameplay.

## Repository / Suite 0.2.3 Infantry Assault cleanup

- Updated Infantry Assault to 0.2.4.
- Restricted Assault and defensive-fire weapon selection to direct-fire small arms; tripod AT guns, ATGM, cannons/autocannons, crew-served MGs, launchers, mortars and suppressive/sustained-fire modes are excluded from CQB.
- Runtime Assault-skill synchronization now treats a successful `SkillContainer.Add` as success even if MENACE throws during the optional UI refresh, eliminating misleading startup synchronization warnings on loaded-save infantry.
- No changes to Squaddie Veterancy, Coordinated Fire or Overwatch gameplay.

## Repository / Suite 0.2.2 combat fix

- Updated Infantry Assault to 0.2.3.
- Defensive fire now works with transient/dev-spawned infantry whose valid close-range attacks are not item-backed.
- Added configurable CQB hit-chance bonus for assault fire, default +20 percentage points.
- No changes to Squaddie Veterancy, Overwatch or Coordinated Fire.
- Debug logging remains OFF by default.

## Repository / Suite 0.2.1 build fix

- Fixed Infantry Assault compilation in the unified repository by importing `System.Runtime.CompilerServices` for `RuntimeHelpers.GetHashCode`.
- Suite version bumped from 0.2 to 0.2.1 as a build-only patch; no feature-set or gameplay changes.

## 0.2

- Added Infantry Assault to the unified repository and Gameplay Suite.
- Suite now builds/distributes four independent feature DLLs from the same source tree.
- Infantry Assault standalone version updated to 0.2.2 (bug/build-fix only).
- Fixed stale player `SkillAction` state after Assault target confirmation by completing TacticalState cleanup; this targets the repeated `SkillAction.HandleMouseMoveOnTile` null-reference storm seen in the 0.2 runtime log.
- Restricted Infantry Assault runtime synchronization to templates that actually declare the Assault skill, preventing fallback injection attempts on xeno/pseudo-infantry actors.
- Merged Infantry Assault's manifest clone/template patches into the Suite manifest.
- Suite load order moved to 190 so Infantry Assault skill appends occur after Field Logistics, matching the standalone compatibility ordering.
- Debug logging remains OFF by default.

## Repository / Suite 0.1

- Created one repository for Squaddie Veterancy 1.9.5.6, Coordinated Fire 1.6.5 and Overwatch.
- Added a single build pipeline that produces both standalone distributions and one MENACE Gameplay Suite distribution.
- Centralized target framework, compiler settings and MENACE/MelonLoader references in `Directory.Build.props`.
- Centralized nullable metadata compatibility and common global usings under `src/Shared`.
- Added shared mod-presence detection so optional cross-mod integrations work both as standalone mods and when bundled in the suite.
- Overwatch becomes 0.32.3.1: compatibility-only patch; no gameplay balance change.
- Kept feature gameplay runtimes isolated to reduce regression risk during the repository migration.

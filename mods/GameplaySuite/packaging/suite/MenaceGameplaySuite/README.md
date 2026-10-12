# MENACE Gameplay Suite 0.32

## 0.32 Starting Options

New module **Starting Options 0.1**: the New Game `Initial Leaders` slider maximum is now configurable from 6 to 12 (default 12). The `Black Market Headstart` checkbox still controls the extra Spare Parts reward; its count is configurable from 0 to 1000 (default 36) through Mod Options. Spare Parts keep their vanilla trade value of 5. New campaigns only. Debug OFF by default, no repeated gameplay logging. For the leader setting, reopen the New Game window after changing the mod option.

## 0.31.8 AI smoke selection performance fix

Smoke Overhaul **0.5.2** disables speculative positioning for MENACE's native smoke AI profiles while keeping **CreateLOSBlocker** as the action-selection behavior. Infantry smoke and mortar smoke can still be used from the actor's actual position, but the AI no longer scores hypothetical moves made just to deploy smoke. This addresses the excessive mortar smoke `Collect` workload observed on 80x80 maps. The new `FastAISmokeSelection` toggle is ON by default and can be disabled to restore the original scoring strategy; no per-turn logging was added. All other suite components are unchanged from 0.31.6.

## 0.31.6 Mortar ranging renderer fix

Weapon Mechanics Rebalanced 0.4.4 gives the dark and colored mortar-ranging border strokes separate child GameObjects, fixing the missing world-space squares while keeping off-map support markers untouched. No change to the native TileHighlighter channels, mortar mechanics or other Suite modules.

## 0.31.5 Infantry Assault native skill discovery fix

Infantry Assault 0.5.2 fixes BaseSkill-vs-Skill interop identity when scanning and recovering the native Assault action. It prevents spurious missing-skill reports and needless runtime attempts to re-add skills already in a live or pending container. All combat balance, AI assault rules, and default logging are unchanged.


## 0.31.4 Field Logistics concrete Skill use-state fix

Field Logistics 1.0.6 fixes vehicle-resupply uses on current MENACE/Il2CppInterop builds. `SkillContainer.GetAllSkills()` yields `BaseSkill` wrappers, so the previous reflection path could find the action ID while missing the concrete `Skill` use-state API. Field Logistics now safely casts the matching native object back to `Skill` and uses `SetMaxUses`, `SetUses`, `SynchronizeUses` and the native getters directly. The 1.0.5 runtime skill-injection/load-order fix remains in place. No balance changes; normal logging remains minimal.

## 0.31.3 Field Logistics runtime skill-injection fix

Field Logistics 1.0.5 recovers missing `Resupply Infantry` and `Redistribute Ammo` actions at runtime through MENACE's native inherent-skill refresh first, then a narrow direct container append only if needed. No balance changes; Debug logging remains OFF by default.

## 0.31.2 mortar ranging highlight visibility fix

Weapon Mechanics Rebalanced 0.4.4 changes only the independent mortar-ranging renderer. Ranged tiles now use a thick dual world-space border (dark under-stroke plus red-to-green inner stroke), lifted above terrain and forced to render on top. The renderer remains completely separate from MENACE delayed-offmap TileHighlighter channels, preserving the Supply Drop marker fix. Smoke Overhaul 0.5.1 and all 0.31 smoke/AI behavior are unchanged.

## 0.31.1 mortar highlight runtime fix

Smoke Overhaul 0.5.1 stops periodically reapplying mortar smoke-delivery template/AI mutations during tactical play. Weapon Mechanics Rebalanced 0.4.2 also keeps a firing solution unless the crew actually leaves the deployed state (or the platform moves). This restores mortar ranging-highlight persistence without changing the 0.31 smoke footprints or AI-smoke behavior.

## 0.31 smoke-delivery footprints + AI use

Smoke Overhaul 0.5 adds caliber-based smoke footprints: infantry grenades and light-mortar smoke use one tile, 80 mm mortar smoke uses a five-tile cross, and heavy/120 mm mortar smoke stays vanilla 3x3. The current 80 mm mortar is granted the existing mortar-smoke skill. MENACE's native `CreateLOSBlocker` AI behavior remains authoritative for infantry smoke and is enabled for mortar smoke skills; its target evaluation sees the same filtered footprint as the spawned smoke, and mortar smoke reuses the native two-round AI-only smoke cooldown. Debug-only AI-use diagnostics are available without adding normal gameplay log spam.



## 0.30.1 mortar highlight / delayed-offmap compatibility fix

Weapon Mechanics Rebalanced 0.4.1 stops borrowing MENACE's delayed-offmap tile-highlight channels for mortar ranging. The old native-layer implementation could force a completed supply-drop marker to remain visible because mortar feedback repeatedly enabled the same `DelayedAoE3` / `DelayedEnemyAoE3` layers used by off-map abilities. Mortar ranging now uses an independent world-space border and leaves MENACE's delayed-offmap layers and color overrides untouched. Ranging balance is unchanged from 0.4.

## 0.29.1 Infantry Assault stale-targeting fix

Infantry Assault 0.5.1 guards the exact player Assault targeting action after it commits combat, preventing stale native `HandleMouseMoveOnTile` calls from reaching `Actor.AimAt` with an element list invalidated by CQB casualties. Existing immediate/deferred targeting cleanup remains enabled. Assault balance, AI behavior, AP/ammo rules, and default logging are unchanged.

## 0.28 assault state rules + integrated runtime fixes

Infantry Assault 0.5 blocks initiation by deployed/kneeling, Suppressed, or Pinned Down attackers while retaining lethal symmetric CQB. Suppressed defenders cannot react; Pinned Down defenders can defensive-fire with half CQB accuracy and half cover-reduction bonus. Coordinated Fire 1.6.7 adds guarded recovery for the verified asynchronous `NoApConsumed` AP-to-zero side effect. Weapon Mechanics Rebalanced 0.3 replaces the old abstract ranging bonus with explicit exact-tile probability and native red-to-green registration feedback.


## 0.27.1 mortar ranging runtime fix

Weapon Mechanics Rebalanced 0.3 fixes cumulative ranging persistence and adds visible ranging feedback and exact-tile firing solutions. When the mortar unit or a mortar fire skill is selected, tiles already ranged by that physical mortar progress from red through amber/yellow to green as the solution improves. Defaults are +15 percentage points of exact-tile chance per prior shot, 85% normal exact-tile cap, and 95% after MENACE resolves an exact intended-tile impact. HE/smoke sharing and movement/undeploy reset rules are unchanged.

## 0.25.1 Dense Forest traversal fix

Forest Traversal 0.1.2 retains the Dense Forest movement fix by using MENACE's native forest-container path. The suite patches `environment_backbone_forestdense` with `IsTraversableByInfantry=true` and `ContainerType=Forest`, so infantry can both path through it and enter/end movement inside it. The existing `cover_inside_forest` profile and native 30 AP enter / 30 AP leave values remain in use; vehicles remain blocked.

## 0.25 Squaddie Veterancy compatibility API

Squaddie Veterancy 1.10 adds the public read-only `SquaddieProbe.CompatibilityApi`, allowing optional integrations to query a persistent squaddie's individual XP and rank by MENACE native squaddie ID. No gameplay, balance, persistence or logging behavior changed.

## 0.24.2 Coordinated Fire Run & Gun fix

Coordinated Fire 1.6.6 fixes the AP-accounting shutdown caused when Lim's Run & Gun / Full Send one-shot AP discount expires between the two coordinated child attacks. Child attacks use MENACE's native `NoApConsumed` usage flag, so normal ammunition/limited-use consumption remains native while Coordinated Fire applies its single combined AP fee once after the volley.

## 0.24.1 indirect-fire AI positioning fix

Weapon Ranges Rebalanced 2.8.1 now applies the verified `InfluencesPositioning=false` large-map turn-performance policy to all current long-range indirect-fire attacks rather than only the medium mortar. This includes light/medium/120 mm mortars, pirate/modular rocket organs, Local Forces/Rogue Army artillery and the current ATGM indirect-fire modes; direct-fire artillery retains normal AI positioning. Weapon Mechanics Rebalanced 0.1 owns the former non-range WRR mechanics: mortar AP/ammo economy, damage/armor-durability scaling, dispersion/suppression, cumulative per-tile ranging, and alternate rifle fire-mode AP/ammo economy.

## 0.22 cumulative mortar ranging

Weapon Ranges Rebalanced 2.7 makes mortar ranging cumulative and per-tile. Each successful prior shot at a tile reduces later `ScatterChance` from the same physical mortar by **10 percentage points**, capped at **50 points** by default. The mortar remembers multiple ranged tiles simultaneously. Infantry mortar solutions are retained only while deployed; vehicle 120 mm solutions are retained while stationary. HE and smoke from the same mortar share the firing solution.

## 0.21 mortar range and ranging fire

Weapon Ranges Rebalanced 2.6 doubles current mortar maximum reach to **34 light / 42 medium / 48 heavy** at the default 100% strength. Smoke rounds inherit the same reach and the range-overriding 120 mm HE/twin-fire skills are synchronized to 48. Mortars also gain ranging fire: repeated shots by the same mortar skill at the exact same intended tile temporarily reduce scatter chance by **25 percentage points** by default (configurable 0-50). Changing target removes the bonus.

## 0.20 alternate rifle fire-mode economy

Weapon Ranges Rebalanced 2.5 makes Assault Rifle **Sustained Fire** and Battle Rifle **Spray** cost 40 AP like their normal shot, while consuming two shared ammo uses per successful activation. The modes require at least two ammo remaining. **Aimed Fire** remains one-ammo-per-shot; its current 40 AP cost is unchanged, with a 50 AP ceiling for any higher-cost variant.

## 0.19.2 light/medium mortar sandbag protection fix

Light and medium infantry HE mortar impacts now enforce `DestroyHalfCover=None` at runtime immediately before native impact handling, closing the load-order/template-rebuild hole that could still let those mortars erase sandbags. The 0.19.1 Crew Pressure control fix is retained unchanged.

One installation containing these feature modules:

- Squaddie Veterancy 1.10
- Coordinated Fire 1.6.7
- Overwatch 0.32.3.1
- Field Logistics 1.0.6
- Infantry Assault 0.5.1
- StrongerCover 2.2.3
- Building Combat Rules 0.1.4
- Forest Traversal 0.1.2
- Cover Destruction Rebalance 0.3.1
- Weapon Ranges Rebalanced 2.8.1
- Weapon Mechanics Rebalanced 0.4.4
- Garrisonable Buildings 1.0.4
- Buildings Block Line of Sight 0.2.2
- Smoke Overhaul 0.5.2
- Visibility Overhaul 3.1.9

Each feature keeps its existing Mod Settings group and can be disabled independently where the standalone mod exposes an enable toggle. The suite does not merge the gameplay runtimes into one giant class or one Harmony patch set; it packages the feature DLLs together and builds them from the same repository.

## Important

Do not install standalone copies of the modules above at the same time as this suite. Suite deployment removes those standalone folders automatically to prevent duplicate loading.

## Why this layout

The suite and standalone packages are generated from the same source files. Shared compatibility helpers and build settings live under `src/Shared`, while feature-specific gameplay remains isolated. This keeps failures contained and makes individual modules easier to test and release.

## Logging

Normal gameplay logging remains minimal. Existing feature debug options stay OFF by default and should be enabled only for diagnostics.

## Squaddie Veterancy

Squaddie Veterancy 1.10 keeps the 18x18 rank service-bar/chevron marker to the right of the unit HP/suppression block. The marker remains an absolute overlay, so it does not resize MENACE's HUD. Position is derived from live UI geometry; infantry uses the native suppression bar and vehicles use the custom Crew Pressure meter when present. Gameplay, XP, rank thresholds and save data are unchanged.

## Field Logistics

Field Logistics 1.0.6 keeps its gameplay/settings behavior unchanged: player vehicles can resupply adjacent infantry, the Field Ammunition Supply module adds extra uses, and experimental adjacent infantry ammo redistribution remains configurable. Its manifest grants are merged with Infantry Assault so player infantry retain both `Redistribute Ammo` and `Assault`. Version 1.0.5 added runtime recovery when a custom skill is missing from a live SkillContainer; 1.0.6 fixes use-state initialization by safely converting the `BaseSkill` wrapper returned by `GetAllSkills()` back to concrete `Skill` before calling the native use APIs.

## Infantry Assault

Assault is an abstract adjacent combat sequence: defender small-arms fire first if available, then surviving assaulters fire with a configurable CQB hit-chance bonus and reduced defender cover. A kill advances the assaulters into the target tile/container; a surviving defender repulses the assault.

## StrongerCover

StrongerCover 2.2.3 keeps its standalone gameplay and settings unchanged: configurable directional cover, inside-cover protection, indirect-fire cover handling, squad-size scaling and Garrisonable Buildings compatibility.

## Building Combat Rules

Direct-fire small arms no longer damage building HP/armor, while infantry inside can still be hit. Version 0.1.4 fixes occupied multi-tile collapse by deferring `Structure.Die()` until MENACE's native occupant exit has fully finished, then temporarily using the exact templates' vanilla non-container fields for the synchronous death path before restoring the garrison template. The existing narrow stale `Actor.GetCurrentProperties()` guard for Garrisonable Buildings remains unchanged.

## Cover Destruction Rebalance

Cover Destruction Rebalance 0.3.1 keeps the accepted dynamic `DestroyHalfCover` tier reduction while narrowing mortar-specific protection to the light and medium infantry HE mortar skills. Those two use `DestroyHalfCover=0`, so sandbags and low-wall half-cover are not erased by their impacts; 120 mm mortar attacks follow the normal tier rebalance. Version 0.3.1 additionally enforces the two mortar exceptions in a silent runtime `AttackHandler.OnTileHit` prefix, preventing a later MENACE/Modpack Loader handler rebuild from undoing the startup template edit.

## Weapon Ranges Rebalanced

Weapon Ranges Rebalanced 2.8.1 is range-focused. It retains the realism-oriented configurable max-range table, doubled mortar reach (34/42/48 light/medium/heavy), proportional range scaling for explicit secondary attack modes, VMAT/direct-skill range synchronization, auto-balancing, family strength controls and JSON exact overrides. The verified large-map AI fix is now generalized: long-range indirect-fire attacks matching the mortar/artillery/rocket-organ/indirect-fire families have `AIConfig.InfluencesPositioning=false`, preventing the expensive attack-specific move-to-fire origin search while preserving actual firing whenever normal AI movement leaves a valid target in range. Direct-fire artillery is excluded.

It no longer changes weapon AP, ammunition, damage, scatter, suppression or ranging memory.

## Weapon Mechanics Rebalanced

Weapon Mechanics Rebalanced 0.4.4 contains the non-range weapon behavior extracted from WRR. Mortar HE/smoke firing costs 50% AP and gets 2x limited uses; HE physical and armor-durability damage are halved while suppression stays unscaled; scatter/suppression are broadened; and a suppression-only outer ring is added. Cumulative per-tile ranging now tightens effective Scatter by one step per prior shot, with misses stopping at Scatter 2 and only an exact intended-tile impact unlocking Scatter 1; ScatterChance remains unchanged. Selecting the mortar shows a red-to-green independent world-space border that does not reuse MENACE delayed-offmap highlight channels. Assault Rifle Sustained Fire and Battle Rifle Spray cost 40 AP and consume two shared ammo uses; Aimed Fire remains one ammo and is capped at 50 AP only if a variant exceeds that cost.

## Garrisonable Buildings

Garrisonable Buildings 1.0.4 is included from the accepted standalone source unchanged at gameplay-code level. It retains native building containers, footprint validation, configurable inside-cover behavior and size-based entry/exit AP surcharges.

## Buildings Block Line of Sight

Buildings Block Line of Sight 0.2.2 adds the two large obstacles titled `Cargo Containers` to the explicit native LoS-blocking scope while deliberately leaving the smaller singular `Cargo Container` non-blocking. Existing building/tower behavior and LoS-matrix preservation are unchanged.

## Smoke Overhaul

Smoke Overhaul 0.5.2 keeps smoke as a soft obstruction instead of a complete LOS wall, with configurable fire-through penalties, 2x default duration, 20 AP hand-thrown grenades, and free three-use smoke/frag skills for normal humanoid infantry. It now adds caliber-based smoke footprints: infantry and light-mortar smoke are 1 tile, 80 mm mortar smoke is a five-tile cross, and heavy/120 mm smoke keeps the vanilla 3x3 area. The 80 mm mortar receives the existing vanilla mortar-smoke skill, and mortar smoke inherits MENACE's native `CreateLOSBlocker` AI behavior plus the native two-round AI-only smoke cooldown so AI units can deliberately use smoke without consecutive-turn spam. Footprint filtering is shared by AI/preview evaluation and actual smoke spawning.

## Visibility Overhaul

Visibility Overhaul 3.1.9 moves the flat vision bonus into MENACE's native `EntityProperties` rebuild path. `Skill.OnUpdate(EntityProperties)` now adjusts raw `Vision` once per rebuild, so MENACE's own `SkillContainer.Update()` sees the changed effective vision and can mark `PropertyDirtyFlag.Vision` normally. The old `EntityProperties.GetVision` postfix has been removed, leaving only two runtime hooks and no direct map-visibility/manual rebuild integration. Movement-based infantry concealment remains unchanged.

# MENACE Mods Unified Repository 0.32.5

## Current offline follow-up (not a verified Suite release)

The headings below retain historical release notes. See `../../docs/MIGRATION_STATUS.md`
for current independently versioned Jiangyu packages and `docs/TESTING.md` for acceptance.
Smoke Overhaul **0.6.4** ports PR #12's bounded proactive smoke rules (60/45/30%) and
pinned-only emergency smoke permission/AP behavior. Native use, coexistence and AI
timing are unverified; no global scheduler or new pathfinder was added. The previous
migration ZIP is unchanged and does not contain this subsequent AI follow-up.
Garrisonable Buildings **1.2.0** mirrors the native deployed-stance outgoing accuracy
property for attacks from supported garrisons (#62); source tests pass, live comparison pending.
Weapon Mechanics Rebalanced **0.4.13** corrects the enemy-only medium mortar skill omitted by 0.4.12 (#59): medium dispersion baseline and activation ranging, without changing native enemy action economy or damage. The 0.4.12 live balance test failed; the corrected candidate requires a new in-game test.

### 0.32.5 MENACE 0.7.16 compatibility (Field Logistics)

Field Logistics **1.0.7** restores `Resupply Infantry` and `Redistribute Ammo` on MENACE v0.7.16, which removed the skill template both were cloned from. The clones now derive from `active.rogue_officer_whistle` with the Refill Ammo behavior re-applied by the manifest, and all suite `EntityTemplate.Skills` patches use `$append` so the grenade skills 0.7.16 adds to player squads are kept. Other modules are unchanged. Verified in game on 0.7.16. See issue #41.

### 0.32.4 MENACE 0.7.16 compatibility (Infantry Assault)

Infantry Assault **0.5.4** restores compatibility with MENACE v0.7.16, which removed `Skill.GetDescription()`. The mod no longer patches that method; the Assault tooltip text comes from the manifest-defined `SkillTemplate.Description`. Unresolved Harmony targets are now reported by handler name. Other modules are unchanged. Verified in game on 0.7.16. See issue #41.

### 0.32.3 MEN-019 Berserk AP refund fix

Coordinated Fire **1.6.8** preserves AP refunds that would otherwise be clipped before the combined salvo fee is charged. The recovered amount is bounded by that fee and works with sequential or synchronized player volleys. Other modules are unchanged. Source/model checks passed; live Windows MENACE gameplay validation remains pending. See issue #19.

### 0.32.2 MEN-004 debug-only ammo trace

Weapon Mechanics Rebalanced **0.4.5** adds one `[MEN-004 AMMO]` log entry per Sustained/Spray Fire activation **only when its Debug logging option is ON**.
The line records ammo before native `Skill.Use`, after native processing and after WMR's extra use, alongside unit/weapon and per-call consumption. To reproduce, enable that module's Debug logging in Mod Options, use ability 2, and compare the UI ammo before/after with `MelonLoader/Latest.log`. Turn Debug logging back OFF afterward. No ammo-cost changes; MEN-004 is still open.

### 0.32.1 Infantry Assault stale aim cache fix (MEN-005)

Infantry Assault 0.5.3 refreshes MENACE's cached executing-soldier index list if squad casualties make it invalid during Assault targeting preview. This prevents native `Actor.AimAt(Vector3)` index errors without changing regular weapon aiming, Mount Up, CQB combat rules, or AI assaults. Checks are cached per squad size; debug remains OFF by default. Runtime gameplay verification is still required.

### 0.32 Starting Options

The new `Starting Options 0.1` module adds configurable leader cap (6-12, default 12) and Black Market Headstart Spare Parts count (0-1000, default 36) to the Mod Options screen. Game checkbox stays authoritative, old saves unchanged, debug OFF by default.

### 0.31.8 Fast AI smoke selection fix

Smoke Overhaul 0.5.2 now uses MENACE's native `InfluencesPositioning=false` mode for smoke skills. The AI can still decide to throw smoke from its present tile, but no longer does expensive future-position scans specifically for smoke deployment. This addresses the observed `CreateLOSBlocker.Collect` slowdown for long-range mortars. The change is optional (`FastAISmokeSelection`, ON by default), reversible, and does not change normal smoke effects, weapon attacks, or pathfinding. Only Smoke Overhaul has code changes compared with 0.31.6. 0.31.7 was a separate no-Assault test branch.

### 0.31.6 Mortar ranged-tile visibility fix

Weapon Mechanics Rebalanced 0.4.4 fixes a Unity limitation in the 0.4.3 dual-stroke overlay: each LineRenderer now occupies a separate child GameObject. No shared MENACE delayed-AoE channels are used, preserving off-map support markers. Infantry Assault 0.5.2 and all other modules remain unchanged.

### 0.31.5 Infantry Assault native skill discovery fix

Infantry Assault 0.5.2 fixes BaseSkill-vs-Skill interop identity when scanning and recovering the native Assault action. It prevents spurious missing-skill reports and needless runtime attempts to re-add skills already in a live or pending container. All combat balance, AI assault rules, and default logging are unchanged.


### 0.31.4 Field Logistics concrete Skill use-state fix

Field Logistics 1.0.6 fixes resupply-use initialization on current MENACE/Il2CppInterop builds. `SkillContainer.GetAllSkills()` exposes entries as `BaseSkill`, so the previous reflection path could identify `Resupply Infantry` but could not see the concrete `Skill` methods `SetMaxUses`, `SetUses`, `SynchronizeUses`, `GetMaxUses`, `GetUses`, or `GetCharges`. Field Logistics now safely `TryCast<Skill>()`s the same native object before touching use state and calls the generated interop API directly. The 1.0.5 load-order/runtime skill-injection compatibility fix remains unchanged; balance and default logging are unchanged.

### 0.31.3 Field Logistics runtime skill-injection fix

Field Logistics 1.0.5 hardens runtime skill injection without changing balance. Player vehicles and player infantry now recover the missing Field Logistics action through MENACE's native inherent-skill refresh first, with a narrow live `SkillContainer` fallback only when the registered custom skill is still absent. This avoids relying exclusively on the manifest/template merge and improves compatibility with other template/vehicle mods and loaded-save actors. Debug logging remains OFF by default.

### 0.31.2 mortar ranging highlight visibility fix

Weapon Mechanics Rebalanced 0.4.4 keeps the independent renderer required by the Supply Drop compatibility fix, but makes the mortar ranging square deliberately high-visibility: a thick dark under-stroke plus the red-to-green ranging colour, lifted above terrain and rendered on top. No MENACE delayed-AoE/TileHighlighter channel is reused. Smoke Overhaul 0.5.1 and the 0.31 smoke footprints/AI behavior are unchanged.

### 0.31.1 mortar highlight runtime fix

Smoke Overhaul no longer re-touches mortar smoke templates/AI profiles every 0.5 seconds during tactical play, and WMR now clears ranging state only on a real deployed -> undeployed transition or successful movement. This fixes the 0.31 regression where mortar ranging highlights could disappear while preserving the 0.31 smoke-footprint/AI features. Normal logging remains silent.

### 0.31 smoke-delivery footprints + AI use

Smoke Overhaul 0.5 gives smoke sources distinct tactical footprints: infantry grenades and light-mortar smoke affect only the target tile, the 80 mm medium mortar gets a five-tile cross-shaped smoke pattern, and heavy/120 mm mortar smoke keeps the vanilla 3x3 area. The 80 mm mortar is granted the existing vanilla mortar-smoke skill. MENACE's native `CreateLOSBlocker` AI behavior is retained for infantry smoke and enabled for mortar smoke skills, with AI/preview scoring seeing the same filtered footprint as the actual effect; mortar smoke also gets the native two-round AI-only smoke cooldown. Normal logging remains silent; AI smoke-use diagnostics are debug-only.



### 0.30.1 mortar highlight / off-map marker compatibility fix

Weapon Mechanics Rebalanced 0.4.1 no longer uses MENACE's `DelayedAoE3` / `DelayedEnemyAoE3` tile-highlight channels for mortar ranging feedback. Those channels belong to MENACE's delayed off-map ability display (including supply-drop markers), and forcing them enabled could make a completed supply-drop area remain visible. Mortar ranging now draws its red-to-green tile border with an independent world-space `LineRenderer`, so it does not modify delayed-offmap highlight enable state, texture channels, or color overrides. Mortar ranging gameplay and Scatter progression are unchanged from 0.4.

### 0.30 mortar scatter-ranging redesign

Weapon Mechanics Rebalanced 0.4 changes ranging to tighten dispersion rather than inflate exact-hit probability. `ScatterChance` is left unchanged. Each prior shot at the same tile reduces effective `Scatter` by 1 for the next shot, but misses stop at Scatter 2; only an exact impact finds the firing solution and unlocks Scatter 1. Default HE progression is light `3 -> 2 -> 1(hit only)`, medium `4 -> 3 -> 2 -> 1(hit only)`, and 120 mm `5 -> 4 -> 3 -> 2 -> 1(hit only)`. Native red-to-green tile feedback now reflects effective Scatter, with full green reserved for the solved Scatter 1 state. Debug-only diagnostics report target, impact, direct-hit state, shot Scatter, next Scatter and unchanged ScatterChance.

### 0.29.1 Infantry Assault stale-targeting fix

Infantry Assault 0.5.1 closes the remaining stale `SkillAction` path after a player Assault. Once an Assault click commits the action, native mouse-move aiming is blocked only for that exact targeting action, preventing `Actor.AimAt` index errors when CQB casualties changed the squad element count. Existing immediate/deferred targeting cleanup remains in place; combat balance and default logging are unchanged.

### 0.29 mortar ranging readability + balance

Infantry Assault 0.5 keeps the lethal symmetric CQB model but now requires the assaulter to be mobile and combat-capable: deployed/kneeling, Suppressed, and Pinned Down infantry cannot initiate Assault. Suppressed defenders cannot return defensive fire; Pinned Down defenders can return fire with half CQB accuracy and half cover-reduction bonus. Coordinated Fire 1.6.7 includes guarded recovery for the verified asynchronous `NoApConsumed` AP-to-zero side effect. Weapon Mechanics Rebalanced 0.3 replaces the fragile private-Scatter ranging hook with ApplyToTile-based registration/application and adds a native tile-highlight fallback for registered mortar targets.


### 0.27.1 mortar ranging runtime fix

Weapon Mechanics Rebalanced 0.2.1 fixes mortar-ranging runtime identity by using stable native actor/item pointers instead of managed wrapper hashes, binds the Scatter target argument directly, and keeps the firing-solution overlay visible when the mortar unit itself is selected. The 0.27 ranging balance remains unchanged in this historical release.

### 0.25.1 Dense Forest traversal fix

Forest Traversal 0.1.2 retains the 0.1.1 gameplay behavior and fixes its build integration by using MENACE's native forest-container path. `environment_backbone_forestdense` now gets both `IsTraversableByInfantry=true` and `ContainerType=Forest`: infantry can path through Dense Forest and can enter/end movement inside it, retaining the existing `cover_inside_forest` profile and native 30 AP enter / 30 AP leave values. Vehicles remain blocked and no forest LoS/destruction/visual properties change.

### 0.25 Squaddie Veterancy compatibility API

Squaddie Veterancy 1.10 adds a small public read-only compatibility API for other mods to query a persistent squaddie's individual XP and veterancy rank by MENACE native squaddie ID. No gameplay, balance, persistence or logging behavior changed.

### 0.24.2 Coordinated Fire Run & Gun fix

Coordinated Fire 1.6.6 fixes the AP-accounting shutdown reported with Lim's Run & Gun / Full Send. Coordinated child attacks use MENACE's native `NoApConsumed` flag so ammo remains native while the combined AP fee is charged once after the volley; the defensive fallback reads each child's live AP cost at launch.

### 0.24.1 indirect-fire AI positioning fix

Weapon Ranges Rebalanced 2.8.1 keeps its range-only scope but generalizes the verified `InfluencesPositioning=false` turn-performance fix from the medium mortar to all current long-range indirect-fire attack families: light/medium/120 mm mortars, pirate/modular rocket organs, Local Forces/Rogue Army artillery, and modular/twin/sentry/tripod ATGM indirect-fire modes. Direct-fire weapons such as the 15 cm assault howitzer keep normal AI positioning. All non-range weapon mechanics previously living in WRR moved into the new standalone/suite module **Weapon Mechanics Rebalanced 0.1**: mortar AP/ammo economy, HE damage/armor-durability scaling, scatter/suppression behavior, cumulative per-tile ranging memory, Sustained Fire/Spray AP+ammo economy, and the Aimed Fire AP ceiling. The actual range table, 34/42/48 mortar reach, secondary-skill range synchronization, family sliders and JSON range overrides remain in WRR.

### 0.23 Dense Forest traversal

Added Forest Traversal 0.1. Vanilla `Dense Forest` (`environment_backbone_forestdense`) is now traversable by infantry while remaining a structure with `SurfaceType.Forest`. MENACE therefore keeps its native Forest terrain cost plus the native structure movement surcharge, so crossing dense woods is slower than open ground without adding a custom movement/AP hook. Vehicles remain blocked. Dense Forest LoS blocking, cover, destruction and visuals are unchanged.

### 0.22 cumulative mortar ranging

Weapon Ranges Rebalanced 2.7 changes mortar ranging from a single repeat-target bonus to a persistent per-tile firing solution. Each successful prior shot at a tile gives the same physical mortar **-10 percentage points ScatterChance** on later shots at that tile, capped at **-50** by default. Separate tiles keep separate bonuses. Infantry mortar solutions last only while the crew remains deployed; vehicle 120 mm solutions last while the platform stays stationary. HE and smoke from the same mortar share the solution.

### 0.21 mortar range and ranging fire

Weapon Ranges Rebalanced 2.6 doubled the default WRR maximum range of all current mortar families: light 17 -> **34**, medium 21 -> **42**, and heavy 120 mm 24 -> **48**. Version 2.7 keeps those ranges and upgrades ranging fire to cumulative per-tile solutions: **-10 ScatterChance percentage points per successful prior shot at that tile, capped at -50 by default**, with separate remembered solutions for multiple tiles.

### 0.20 alternate rifle fire-mode economy

Weapon Ranges Rebalanced 2.5 changes Assault Rifle Sustained Fire and Battle Rifle Spray from 80 to 40 AP while making each successful use consume two shared ammunition uses. They require at least two ammo remaining. Battle Rifle Aimed Fire keeps one-ammo consumption; its current 40 AP cost remains unchanged and any higher-cost variant is capped at 50 AP.

### 0.19.2 light/medium mortar sandbag protection fix

Cover Destruction Rebalance 0.3.1 adds a runtime guard on mortar impact so light and medium infantry HE cannot regain half-cover destruction after MENACE/Modpack Loader rebuilds handlers. The 0.19.1 Crew Pressure fix remains unchanged.

### 0.19.1 vehicle Crew Pressure control fix

Squaddie Veterancy now treats Crew Pressure as the authoritative combat-stress system for tracked vehicles on every side. Native vehicle morale can still change internally, but it no longer pushes Crew-Pressure vehicles into MENACE's generic panic/flee behavior. **Buttoned Up is now strictly the intended -AP / -accuracy state and the vehicle remains controllable/usable.** Break Contact remains a separate high-pressure state and continues to use the mod's controlled retreat. After a Break Contact retreat the HUD stays in Break Contact until that activation ends, then transitions to Buttoned Up for the next usable activation.

This repository is the common authoring tree for Luca's MENACE gameplay mods.

## Distribution model

Every module ships as its own [Jiangyu](https://github.com/antistrategie/jiangyu) package under `../jiangyu/<Module>/`. This directory holds only the shared C# gameplay sources; each Jiangyu package's `code/<Module>.csproj` compiles them together with its host binding (`<Module>System.cs`). Data changes (template clones and patches) are authored as KDL under the package's `templates/`. Options live in the MenaceModSettings window (End key).

Modules stay isolated in separate DLLs rather than one monolithic gameplay DLL. That keeps failures contained, preserves existing save/runtime behavior and lets each feature retain its own version.

## Repository layout

```text
src/
  Shared/                 shared host/compatibility source (IHostedPlugin, ModLog, Jiangyu/*)
  SquaddieVeterancy/      veterancy feature
  CoordinatedFire/        coordinated-fire feature + pure Core
  Overwatch/              reaction-fire feature
  StartingOptions/        new campaign leader/Headstart reward settings
  CustomSupplySliders/    wider Player/Enemy Supply Points sliders (Customize Difficulty)
  FieldLogistics/         vehicle resupply + infantry ammo sharing feature
  InfantryAssault/        adjacent assault feature
  StrongerCover/          configurable cover feature
  BuildingCombatRules/     building damage/collapse safety feature
  ForestTraversal/          infantry Dense Forest traversal using native movement costs
  CoverDestructionRebalance/ half-cover destruction rebalance feature
  WeaponRangesRebalanced/ realistic weapon-range rebalance feature
  GarrisonableBuildings/   native-style building garrison feature
  BuildingsBlockLineOfSight/ native LoS blocking for enterable buildings
  SmokeOverhaul/            soft-obstruction smoke + optional infantry smoke/frag grants
  VisibilityOverhaul/      configurable vision/concealment overhaul
tests/                     offline authoring regression checks (python3 -m unittest discover -s tests)
docs/                      testing notes and investigations
```

Per-module README/CHANGELOG files, manifests (`jiangyu.json`), KDL templates and locales live in `../jiangyu/<Module>/`.

## Build

Each package is built with the Jiangyu CLI from its own directory:

```text
cd ../jiangyu/<Module>
jiangyu compile --release --clean
```

`jiangyu compile` fingerprints only `code/`, so pass `--clean` after editing the shared sources here. The output in `compiled/` is what gets installed into `Mods\<Module>\`. Requirements: .NET SDK, the Jiangyu CLI and the game's `Il2CppAssemblies` (see `code/Directory.Build.props` in each package). `Menace.ModpackLoader.dll` is no longer required for these modules.

## Shared-code policy

The repository still keeps gameplay modules isolated. Squaddie Veterancy 1.10 keeps the corrected vehicle Crew Pressure HUD and exposes a small read-only compatibility API for per-squaddie XP/rank and moves veterancy markers to the right of the live HP/suppression block using absolute UI geometry. Field Logistics 1.0.6 keeps the 1.0.5 runtime skill-injection hardening and fixes native resupply use-state initialization by casting `BaseSkill` wrappers back to concrete `Skill` before calling the MENACE use APIs; hot-path candidate/cache debug spam remains removed. Infantry Assault 0.5.1 keeps the lethal symmetric CQB model, prevents deployed/kneeling, Suppressed, or Pinned Down attackers from initiating Assault, blocks Suppressed defensive fire, lets Pinned Down defenders react with half CQB bonuses, and guards committed Assault targeting from stale native mouse-move aiming after CQB casualties. StrongerCover 2.2.3 is included unchanged as its own feature DLL. Building Combat Rules 0.1.4 adds structure-only small-arms immunity, a narrow stale-actor guard for Garrisonable Buildings, and a multi-tile collapse guard that defers native death until an occupied structure's exit movement has finished before temporarily restoring vanilla container fields for `Structure.Die()`. Forest Traversal 0.1.2 makes only vanilla Dense Forest traversable and enterable by infantry through MENACE's native `EntityContainerType.Forest`, while preserving native Forest/structure/container movement costs; vehicles and all forest LoS/destruction/visual properties are unchanged. Cover Destruction Rebalance 0.3.1 keeps the accepted dynamic `DestroyHalfCover` rebalance and protects sandbag/low-wall half-cover specifically from light and medium infantry mortar HE; 120 mm mortar fire follows the normal tier rebalance. Weapon Ranges Rebalanced 2.8.1 owns realistic/configurable ranges plus the generalized long-range indirect-fire `InfluencesPositioning=false` performance fix. Mortar maximum reach remains 34/42/48 for light/medium/heavy, and secondary explicit-range skills continue to scale proportionally with their parents. Weapon Mechanics Rebalanced 0.4.4 now owns the non-range mortar economy/ballistics/ranging behavior, scatter-tightening firing-solution feedback, and alternate rifle fire-mode economy. Normal gameplay remains silent unless the relevant Debug logging option is explicitly enabled.

## Logging policy

Routine logging stays silent by default. Debug/verbose logging remains opt-in per feature.

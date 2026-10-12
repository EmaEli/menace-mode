# Unified repository architecture

## Goal

Maintain each MENACE gameplay feature once while supporting two release shapes:

- standalone modpacks;
- a single MENACE Gameplay Suite modpack containing multiple plugin DLLs.

The current Modpack Loader scans every `*.dll` in a modpack's `dlls/` directory, loads every assembly and discovers every concrete `IModpackPlugin` implementation. That makes the multi-DLL suite a native loader pattern rather than a custom bootstrap hack.

## Why multiple DLLs inside the suite

A single monolithic DLL would force unrelated feature lifecycles, assembly metadata and failure modes together. The suite instead keeps one DLL per feature while sharing source/build infrastructure. This gives us:

- one install for suite users;
- independent feature toggles via existing Mod Settings;
- existing persistence paths and feature namespaces;
- isolated Harmony instances supplied by Modpack Loader;
- independent standalone version numbers;
- a suite version independent from feature versions.

## Shared layer

`src/Shared` is compiled into every feature DLL. It is source sharing, not a runtime dependency DLL, so users cannot end up with an incompatible `MenaceCore.dll` version.

Current shared items:

- global usings;
- Roslyn nullable metadata compatibility for MENACE Unity references;
- mod/assembly presence detection used by optional integrations.

Good next candidates after runtime testing:

1. IL2CPP pointer identity helpers (`Key`, `Valid`, `Same`).
2. MENACE 0.8.15 player-ownership compatibility resolver.
3. common infantry/vehicle classification helpers.
4. common weapon-role / attack enumeration helpers used by Overwatch and Coordinated Fire.
5. common diagnostic logger facade with debug OFF by default.

Do not move a helper to Shared just because its name matches. First confirm that its semantics are identical in every feature.

## Feature boundaries

### Squaddie Veterancy
Owns persistent XP, ranks, reliability, vehicle-crew veterancy/pressure, roster UI and its save sidecar. No persistence code should be shared until its save semantics are explicitly versioned.

### Overwatch
Owns reaction reservations, reaction execution, hit-chance penalty context and Overwatch HUD indicators.

### Coordinated Fire
Owns paired-weapon planning/resolution and its HUD. Its pure rule/controller classes remain feature-local unless another feature actually needs the same abstraction.

### Field Logistics
Owns vehicle-to-infantry ammunition resupply, the Field Ammunition Supply accessory and experimental adjacent infantry ammo redistribution. The Suite manifest carries its accessory/skill clones and unit grants. On the 16 player infantry templates also touched by Infantry Assault, the Suite manifest stores the final combined skill list so both features remain available.

### Infantry Assault
Owns adjacent assault targeting, defensive fire, assault-fire cover reduction, post-kill occupation and AI assault selection. The Suite manifest carries its template clone/skill grants, so Suite load order currently follows Infantry Assault's standalone compatibility order (190).

### StrongerCover
Owns directional cover, inside-cover protection, indirect-fire cover handling and squad-size scaling. It has no suite-specific template patches and remains an isolated 2.2.3 feature DLL.

### Forest Traversal
Owns the narrow Dense Forest infantry-traversal rule. It changes only the vanilla Dense Forest template's infantry traversability flag and deliberately leaves Forest surface type, structure identity, cover, LoS and destruction behavior untouched so MENACE's native path-cost system remains authoritative.

### Weapon Ranges Rebalanced
Owns weapon/vehicle `MaxRange` rebalance, explicit secondary-skill range synchronization, range settings/JSON overrides, and the generalized long-range indirect-fire `InfluencesPositioning=false` AI turn-performance fix. It must not own AP, ammo, damage, scatter, suppression or ranging-memory mechanics.

### Weapon Mechanics Rebalanced
Owns non-range weapon behavior extracted from WRR: mortar action economy, damage/armor-durability scaling, scatter/suppression behavior, cumulative per-tile mortar ranging memory, and alternate rifle fire-mode AP/ammo economy. Mortar ranging tightens effective Scatter one step per prior shot, misses stop at Scatter 2, exact impact unlocks Scatter 1, and ScatterChance remains unchanged. It must not write weapon ranges or own the indirect-fire AI positioning fix.

### Smoke Overhaul
Owns smoke LOS/accuracy/duration changes, hand-grenade AP economy, free infantry smoke/frag grants, smoke-delivery footprints, the 80 mm mortar smoke-skill grant, and smoke AI enablement. Infantry and light-mortar smoke are center-only, 80 mm smoke is a five-tile cross, and heavy/120 mm smoke remains vanilla 3x3. MENACE's native `CreateLOSBlocker` remains the AI decision engine; Smoke Overhaul supplies missing mortar AIConfig and filters `Skill.GetAoeTiles` / `Skill.GetUnsortedAoeTiles` so AI/preview evaluation matches the enforced spawn footprint. Mortar smoke also reuses the infantry smoke skill's native two-round AI-only cooldown. It must not reuse shared delayed-offmap TileHighlighter channels.

## Compatibility rule

Feature detection must not assume another feature has its own `Mods/<Feature>` directory. Suite builds place all feature DLLs under `Mods/MenaceGameplaySuite/dlls/`, so detection should prefer loaded assembly/type presence and use standalone directories only as fallback.

## Build/release rule

Feature versions change only when that feature changes. The suite has its own simple version line starting at `0.1`; the current suite is `0.31`.

The first repository migration changes Overwatch from `0.32.3` to `0.32.3.1` because suite-safe mod detection is a compatibility fix, not a gameplay feature.

## Logging rule

Routine event/per-frame logging remains disabled by default. Shared infrastructure must never introduce unconditional gameplay logging. Errors/warnings should be rare and actionable; verbose tracing stays behind each feature's debug option.


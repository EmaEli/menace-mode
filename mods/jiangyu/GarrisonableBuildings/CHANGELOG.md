# Changelog

## 1.0.5

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## 1.0.4
- Compatibility refresh for the latest MENACE patch using the newly generated game dump/IL2CPP files.
- Revalidated the runtime APIs used by the mod: `DataTemplateLoader.Get<T>`, `EntityTemplate.ProvidesCoverInside`, `Tactical.MovementSystem.GetPath`, `Actor.MoveTo`, and `APEnterCost` / `APLeaveCost` are still present with compatible signatures.
- Existing building whitelist, cover values, AP balance, StrongerCover integration, and debug-only logging are unchanged.
- No gameplay changes.

## 1.0.3
- Added a Debug Logging option under Diagnostics, disabled by default.
- All Garrisonable Buildings runtime messages, warnings, and errors are now emitted only when Debug Logging is enabled.
- No gameplay changes.

## 1.0.2

- Fixed the dedicated `cover_inside_building` clone so it preserves the runtime `InsideCoverTemplate` type and can be assigned to `EntityTemplate.ProvidesCoverInside`.
- Fixed retry handling so buildings whose `ContainerType` was already set still receive and verify the dedicated inside-cover profile.
- Removed `building_civilian_habitat_large_01` from the whitelist after runtime validation showed it is 6x4, outside the mod's maximum 3x3 scope.
- No AP balance changes from 1.0.1.

## 1.0.1

- Added `build-nexus-release.bat` to build a clean Nexus-ready release ZIP without deploying to the game.
- No gameplay changes from 1.0.

## 1.0
- First stable release.
- Rebalanced building-size AP surcharges for both entering and leaving: 1x2 +10 AP, 1x3 +20 AP, 2x2 +20 AP, 2x3 +30 AP, 3x3 +40 AP.
- 1x1 buildings keep the vanilla container AP cost.
- Kept the dedicated configurable building inside-cover profile and StrongerCover integration.
- Kept 3x3 support optional and OFF by default.

## 0.8
- Replaced direct reuse of the vanilla bunker inside-cover profile with a dedicated runtime `cover_inside_building` clone.
- Added configurable building defaults: Accuracy 0.40, Damage 0.60, Suppression 0.60, Concealment +2.
- Added Mod Settings sliders for all four building inside-cover values.
- Added the dedicated profile name used by StrongerCover 2.2 for optional integration.
- Kept the existing garrison whitelist, AP costs and optional 3x3 behavior unchanged.

## 0.7
- Reduced the optional 3x3 garrison surcharge from +80 AP to +40 AP.
- 3x3 buildings now cost +40 AP when entering and +40 AP when leaving (70 AP total each way with vanilla infantry).
- No changes to the existing 1x1, 1x2, 1x3, 2x2 or 2x3 costs.

## 0.6
- Added small rectangular garrisons up to 2x3 by default.
- Added Civilian Shelter 1x2, Water Storage Plant 1x2, Pirate Makeshift Storage 1x2, Water Treatment Plant 1x3 and Water Treatment Plant 2x3.
- Added footprint-scaled AP surcharges: 1x2 +20 AP, 1x3/2x2 +40 AP, 2x3 +60 AP.
- Added experimental 3x3 support behind a Mod Settings toggle that is OFF by default.
- Optional 3x3 candidates are Greenhouse and Storage Facility, with +80 AP enter/leave surcharge.
- Added rotated-footprint validation so 1x2/2x1 and 2x3/3x2 runtime orientations are treated as the same size.
- Kept the scope capped at 3x3; larger structures remain untouched.
- Kept missile silos, tanks, machinery, towers, walls and other non-garrison structures excluded.

## 0.5
- Removed `building_military_2x2_missile_silo` from the garrison whitelist.
- Missile silos and similar underground infrastructure are explicitly excluded.
- Kept 2x2 testing limited to clearly occupiable above-ground buildings.
- Retained the +40 AP enter/leave surcharge for managed 2x2 buildings.

## 0.4
- Added runtime-validated 2x2 garrison candidates.
- Added a +40 AP surcharge when entering or leaving a managed 2x2 building.
- Added diagnostic logging for footprint detection and surcharge execution.

## 0.3
- Fixed the ModpackLoader plugin signature and switched to the working `Il2CppMenace.*` runtime/reflection pattern.
- Added runtime 1x1 footprint validation.
- Enabled Small Civilian Shelter, Control Building and Pirate Shelter.

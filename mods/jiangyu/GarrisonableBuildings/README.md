# Garrisonable Buildings 1.3.0

MENACE mod that reuses the game's native `Building` container system so infantry can garrison selected small map structures.

The scope remains limited to buildings up to 3x3. Structures larger than 3x3 are not touched.

## Current patch compatibility

Version 1.0.4 was revalidated against the latest uploaded MENACE IL2CPP dump. The container, inside-cover and movement/AP APIs used by the mod remain compatible; no gameplay rebalance was required.

## Enabled by default

### 1x1 - vanilla AP cost
- `building_civilian_shelter_01` - Small Civilian Shelter
- `building_civilian_water_treatment_building_01` - Control Building
- `building_pirate_shelter_01` - Pirate Shelter

### 1x2 - +10 AP enter / leave
- `building_civilian_shelter_1x2_01` - Civilian Shelter
- `building_civilian_water_storage_plant_1x2` - Water Storage Plant
- `building_pirate_makeshift_storage_01_1x2` - Makeshift Storage

### 1x3 - +20 AP enter / leave
- `building_civilian_water_storage_plant_1x3` - Water Treatment Plant 1x3

### 2x2 - +20 AP enter / leave
- `building_civilian_water_storage_plant_2x2` - Water Treatment Plant 2x2

### 2x3 - +30 AP enter / leave
- `building_civilian_water_storage_plant_2x3` - Water Treatment Plant 2x3

## Optional 3x3 - OFF by default

Enable **Enable 3x3 Buildings** in Mod Settings to use:

- `building_civilian_greenhouse_01` - Greenhouse
- `building_civilian_storage_01` - Storage Facility

3x3 buildings use **+40 AP** entering and **+40 AP** leaving. With the normal 30 AP container cost this means 70 AP each way. Restart after changing the 3x3 toggle so templates initialize cleanly.

## Building inside cover

Garrisonable buildings use a dedicated runtime inside-cover profile:

`cover_inside_building`

Default values:

- Base hit-chance / accuracy multiplier: **0.40**
- Damage multiplier: **0.60**
- Suppression multiplier: **0.60**
- Concealment: **+2**

All four values are configurable in the **Garrisonable Buildings** Mod Settings panel.

## Garrisoned occupant offense

Infantry automatically enters MENACE's native **Deployed** stance after settling inside a supported garrison. The game supplies its normal deployed effects through the native property system, so the unit's current stats and shot previews use the same effects. There is no separate accuracy modifier, hard-coded bonus or extra deploy AP charge.

Leaving or being ejected clears the garrison deployment. Pinned-down and moving units are not forcibly deployed; normal native deployment resumes after recovery/settling inside. Existing occupants are reconciled when MENACE rebuilds their properties. Vanilla bunkers, towers and vehicle passengers are not changed. Incoming cover and existing enter/leave AP costs are unchanged. In-game stats-panel/exit acceptance of 1.3.0 is pending (#62).

## StrongerCover compatibility

Garrisonable Buildings works without StrongerCover.

With StrongerCover 2.2 installed, StrongerCover recognizes `cover_inside_building` and overrides only its hit-chance / `AccuracyMult` value while StrongerCover is enabled. The StrongerCover default for garrisoned buildings is **30%**. Damage, suppression and concealment remain controlled by Garrisonable Buildings.

## Runtime footprint validation

Every candidate is checked with `GetBlockedTilesWidth()` and `GetBlockedTilesHeight()` before modification. Rotated rectangles such as 1x2/2x1 and 2x3/3x2 are accepted. Any other mismatch is skipped and logged.

## AP progression

With vanilla infantry costs:

- 1x1: **30 AP**
- 1x2: **40 AP**
- 1x3: **50 AP**
- 2x2: **50 AP**
- 2x3: **60 AP**
- 3x3: **70 AP** when enabled

The surcharge applies both when entering and when leaving. The mod temporarily changes `APEnterCost` / `APLeaveCost` only while MENACE calculates or executes the relevant movement, then restores the unit's original values.

## Safe garrison collapse

MENACE's native collapse path was reproduced hard-crashing the process when one of the multi-tile buildings made enterable by this mod died, with or without a live occupant. Since 1.1.0 the guard is part of this mod (it lived in Building Combat Rules 0.1.2-0.2.0, GitHub #50):

- For the exact multi-tile whitelist, an occupied lethal collapse is deferred instead of destroying the structure in the same call stack as occupant ejection. The mod uses MENACE's native ejection path when needed, waits until the occupant is detached and its movement has finished, then resumes the original native `Structure.Die()` call. Repeated lethal hits during the pending exit do not restart ejection or death.
- While `Structure.Die()` executes, only those multi-tile templates temporarily restore the two vanilla fields changed by this mod (`ContainerType=None`, no inside-cover profile). The garrison template is restored immediately afterward, so other live structures stay garrisonable.
- A narrow finalizer on `Actor.GetCurrentProperties()` treats a read that fails because MENACE invalidated the actor between this mod's reflection probe and the call as unavailable (`null`) instead of a `TargetInvocationException`. Calls from the base game and other mods keep vanilla exception behavior.

Option `Safely eject occupants before a multi-tile garrison collapses` (ON by default) under the Building collapse header.

## Explicit exclusions

The whitelist excludes tanks, generators, antenna/radar towers, factory connectors, industrial tanks/vents, sewage machinery, missile silos, walls, junk piles and other structures that are not sensible infantry garrisons.

The 3x3 military large bunker is still excluded; current 3x3 support is limited to clearly above-ground civilian buildings.

## Build

From `mods/jiangyu/GarrisonableBuildings/`, run `jiangyu compile --release --clean`. The clean flag is required after editing shared GameplaySuite sources. This builds the package without deploying it to the game; the compiled package is written under `compiled/`.

## Validation (#62)

`python3 -m unittest mods/GameplaySuite/tests/test_garrison_deployment.py -v` runs the production deployment hooks with managed native API stand-ins. It covers enter/leave, failed transitions, repeated entry, loaded occupants, suppression/recovery, collapse ejection and unsupported actors/containers. It does not execute native Harmony detours or render the stats panel.

Live check: compare infantry Aim and attack preview outside, after settling inside an enabled civilian building, then after leaving. Repeat entry to check no stacking, verify exit movement still works, and confirm pinned-down/recovery behavior. Existing inside-cover settings and entry/exit AP surcharges should remain unchanged.

Deployment is currently blocked: on 5 November 2026 the PC's GameAssembly SHA256 was `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`, different from the canonical 0.7.17 pin (`871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`). Cached 0.7.17 compilation is not verification of the changed native DLL. Revalidate the installed game/proxies and shared compatibility baseline before building/installing a PC candidate; do not bypass the installer hash guard.

## Diagnostics

**Debug Logging** is disabled by default. Garrisonable Buildings emits no plugin runtime logs unless this option is enabled in Mod Settings.

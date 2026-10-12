# Buildings Block Line of Sight 0.2.2

MENACE mod that makes enclosed buildings and enterable building/tower containers block line of sight using the game's native structure/LoS system.

## What 0.2.2 contains

Version 0.2.2 contains the original proof of concept so the LoS rule also covers every structure that is currently enterable as a native `Building` or `Tower` container.

At runtime the mod treats a structure as eligible when either:

- it is one of the explicit enclosed-building targets listed below; or
- its current `ContainerType` is `Building` (1) or `Tower` (4).

This runtime check is intentional: if another mod such as Garrisonable Buildings turns a structure into a building container before it is spawned, Buildings Block Line of Sight will recognize it automatically when `Structure.Create` runs.

Vehicles (`ContainerType=Vehicle`) and forests (`ContainerType=Forest`) are not included.

## Explicit enclosed / garrison-compatible targets

### Garrisonable Buildings default candidates

- `building_civilian_shelter_01` - Small Civilian Shelter
- `building_civilian_water_treatment_building_01` - Control Building
- `building_pirate_shelter_01` - Pirate Shelter
- `building_civilian_shelter_1x2_01` - Civilian Shelter
- `building_civilian_water_storage_plant_1x2` - Water Storage Plant
- `building_pirate_makeshift_storage_01_1x2` - Makeshift Storage
- `building_civilian_water_storage_plant_1x3` - Water Treatment Plant 1x3
- `building_civilian_water_storage_plant_2x2` - Water Treatment Plant 2x2
- `building_civilian_water_storage_plant_2x3` - Water Treatment Plant 2x3

### Optional Garrisonable Buildings 3x3 candidates

- `building_civilian_greenhouse_01` - Greenhouse
- `building_civilian_storage_01` - Storage Facility

These two already block LoS in current vanilla data. The mod detects that and leaves their existing LoS geometry untouched.

### Large cargo-container obstacles

- `object_civilian_stacked_containers_01` - Cargo Containers - blocks LoS
- `obstacle_pirate_container_02_corner` - Cargo Containers - blocks LoS

The smaller singular `obstacle_pirate_container_01_1x1` - Cargo Container - is deliberately not included.

## Vanilla enterable structures

Current extracted game data contains these native enterable building/tower structures:

- `bunker` - Conrete Bunker - Building container
- `building_military_1x1_bunker` - Small Military Bunker - Building container
- `building_military_1x1_bunker_corner` - Military Bunker Corner - Building container
- `building_military_1x1_bunker_corner_machinegun` - Military Bunker Corner - Building container
- `building_military_1x1_bunker_straight_wall` - Military Bunker Wall - Building container
- `building_military_1x1_bunker_straight_wall_rogue_infantry` - Military Bunker Wall - Building container
- `building_military_1x1_watchtower` - Military Watchtower - Tower container

Most military bunker variants already block LoS in vanilla. Version 0.2.2 preserves their existing `BlockedLineOfSight` matrix instead of replacing it. The important vanilla corrections are the generic `bunker` template and the Military Watchtower, which are enterable but are not marked as LoS blockers in the extracted data.

## How it works

For an eligible structure that does not already have working LoS blocking, the mod:

1. Sets `IsBlockingLineOfSight = true`.
2. Reuses the structure's existing `BlockedTiles` footprint as `BlockedLineOfSight`.
3. Lets vanilla `Structure.BlockLineOfSight()` and `Structure.UnblockLineOfSight()` perform the actual map updates.

If an eligible template already has `IsBlockingLineOfSight = true` and an existing LoS matrix, the mod leaves that matrix unchanged.

This preserves special vanilla LoS geometry where it exists and means destruction should reopen LoS through the game's normal structure-destruction path.

## Garrisonable Buildings compatibility

Garrisonable Buildings loads after this mod in the supplied manifests. That is safe because the `Structure.Create` prefix checks the template's **current runtime** `ContainerType` immediately before vanilla creates the structure.

Therefore buildings made garrisonable by Garrisonable Buildings are recognized as enterable Building containers when they actually appear on the tactical map.

## What it does NOT change

- No changes to hit chance or cover strength.
- No changes to movement or pathfinding.
- No LoS conversion for vehicle containers.
- No LoS conversion for forest containers.
- No per-frame or per-turn logging.
- Existing LoS matrices are not overwritten when a structure already blocks LoS correctly.

## Diagnostics

Debug Logging is OFF by default. With the default setting the plugin emits no runtime diagnostic spam.

## Suggested test

1. Test one of the small civilian/pirate buildings from the original list.
2. Test the Military Watchtower with a unit directly behind it.
3. Test a vanilla military bunker and confirm its existing LoS behavior is unchanged.
4. With Garrisonable Buildings installed, enter one of the converted civilian buildings and test visibility both from inside and across the building.
5. Destroy a blocking building and confirm LoS reopens through its former footprint.

## Build / deploy

Run `build-and-deploy.bat`.

The package deploys to:

`MENACE/Mods/BuildingsBlockLineOfSight/`

# LoS target scope - 0.2.2

Version 0.2.2 combines an explicit compatibility list with runtime detection of enterable structure containers.

## Runtime rule

A spawned structure is eligible when:

- it is an explicit target below; **or**
- its current `ContainerType` is `Building` (1) or `Tower` (4).

`Vehicle` (2) and `Forest` (3) containers are deliberately excluded.

## Original enclosed-building targets / Garrisonable Buildings default candidates

| Template | Name | Vanilla container | Vanilla LoS | 0.2.2 behavior |
|---|---|---:|---:|---|
| building_civilian_shelter_01 | Small Civilian Shelter | None | No | Block LoS |
| building_civilian_water_treatment_building_01 | Control Building | None | No | Block LoS |
| building_pirate_shelter_01 | Pirate Shelter | None | No | Block LoS |
| building_civilian_shelter_1x2_01 | Civilian Shelter | None | No | Block LoS |
| building_civilian_water_storage_plant_1x2 | Water Storage Plant | None | No | Block LoS |
| building_pirate_makeshift_storage_01_1x2 | Makeshift Storage | None | No | Block LoS |
| building_civilian_water_storage_plant_1x3 | Water Treatment Plant 1x3 | None | No | Block LoS |
| building_civilian_water_storage_plant_2x2 | Water Treatment Plant 2x2 | None | No | Block LoS |
| building_civilian_water_storage_plant_2x3 | Water Treatment Plant 2x3 | None | No | Block LoS |

## Optional Garrisonable Buildings 3x3 candidates

| Template | Name | Vanilla container | Vanilla LoS | 0.2.2 behavior |
|---|---|---:|---:|---|
| building_civilian_greenhouse_01 | Greenhouse | None | Yes | Preserve existing LoS matrix |
| building_civilian_storage_01 | Storage Facility | None | Yes | Preserve existing LoS matrix |

When Garrisonable Buildings enables these as Building containers, the runtime container check also recognizes them automatically.

## Explicit large cargo-container obstacles

| Template | Name | Vanilla LoS | 0.2.2 behavior |
|---|---|---:|---|
| object_civilian_stacked_containers_01 | Cargo Containers | No | Block LoS |
| obstacle_pirate_container_02_corner | Cargo Containers | No | Block LoS |

The singular `obstacle_pirate_container_01_1x1` (`Cargo Container`) remains non-blocking.

## Vanilla enterable Building/Tower containers

Current extracted `EntityTemplate` data contains six native `Building` containers and one native `Tower` container:

| Template | Name | ContainerType | Vanilla LoS | 0.2.2 behavior |
|---|---|---:|---:|---|
| bunker | Conrete Bunker | Building | No | Block LoS |
| building_military_1x1_bunker | Small Military Bunker | Building | Yes | Preserve existing LoS matrix |
| building_military_1x1_bunker_corner | Military Bunker Corner | Building | Yes | Preserve existing LoS matrix |
| building_military_1x1_bunker_corner_machinegun | Military Bunker Corner | Building | Yes | Preserve existing LoS matrix |
| building_military_1x1_bunker_straight_wall | Military Bunker Wall | Building | Yes | Preserve existing LoS matrix |
| building_military_1x1_bunker_straight_wall_rogue_infantry | Military Bunker Wall | Building | Yes | Preserve existing LoS matrix |
| building_military_1x1_watchtower | Military Watchtower | Tower | No | Block LoS |

## Safety behavior

If `IsBlockingLineOfSight` is already true and `BlockedLineOfSight` already exists, 0.2.2 does not replace it with `BlockedTiles`. This is important for vanilla bunker variants or future structures with custom LoS geometry.

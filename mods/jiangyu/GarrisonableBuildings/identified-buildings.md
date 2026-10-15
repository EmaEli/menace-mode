# Garrison candidate scope - 1.0.4

## Default enabled

| Template | Expected footprint | Extra enter/leave AP | Notes |
|---|---:|---:|---|
| building_civilian_shelter_01 | 1x1 | +0 | Small Civilian Shelter |
| building_civilian_water_treatment_building_01 | 1x1 | +0 | Control Building |
| building_pirate_shelter_01 | 1x1 | +0 | Pirate Shelter |
| building_civilian_shelter_1x2_01 | 1x2 | +10 | Civilian Shelter |
| building_civilian_water_storage_plant_1x2 | 1x2 | +10 | Water Storage Plant |
| building_pirate_makeshift_storage_01_1x2 | 1x2 | +10 | Makeshift Storage |
| building_civilian_water_storage_plant_1x3 | 1x3 | +20 | Water Treatment Plant |
| building_civilian_water_storage_plant_2x2 | 2x2 | +20 | Water Treatment Plant |
| building_civilian_water_storage_plant_2x3 | 2x3 | +30 | Water Treatment Plant |

## Optional 3x3 - default OFF

| Template | Expected footprint | Extra enter/leave AP | Notes |
|---|---:|---:|---|
| building_civilian_greenhouse_01 | 3x3 | +40 | Greenhouse |
| building_civilian_storage_01 | 3x3 | +40 | Storage Facility |

The 3x3 military large bunker is intentionally excluded from this first 3x3 experiment.

## Explicitly excluded examples

- missile silos
- tanks / tank arrays
- energy generators / storage machinery
- antenna and radar towers
- factory connectors / industrial vents
- sewage machinery
- walls
- junk piles / wreckage

All expected footprints are validated against the runtime template before `ContainerType` is changed.

## Runtime correction in 1.0.2

- `building_civilian_habitat_large_01` is excluded: the current game reports a 6x4 footprint, so it is outside this mod's maximum 3x3 scope.

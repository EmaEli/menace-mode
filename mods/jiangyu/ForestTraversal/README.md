# Forest Traversal 0.1.2

Makes the vanilla `environment_backbone_forestdense` **Dense Forest** usable by infantry while keeping vehicles blocked.

Version 0.1.2 includes the 0.1.1 Dense Forest traversal fix and corrects the standalone build integration:

- `IsTraversableByInfantry=true` lets MENACE's native infantry pathfinder cross Dense Forest structure tiles.
- `ContainerType=Forest` lets infantry actually **enter and end movement inside** a Dense Forest tile, using MENACE's existing forest-container logic. This matches the game's native `EntityContainerType.Forest` path rather than inventing a custom movement system.

Dense Forest already ships with `ProvidesCoverInside=cover_inside_forest` and 30 AP enter / 30 AP leave values, so the mod keeps those native forest-container rules. The tile remains `SurfaceType.Forest`, and ordinary traversal still uses MENACE's native Forest and structure movement costs.

Vehicles remain blocked: vehicle templates are not containable in entities, and `IsTraversableByInfantry` is only honored for infantry pathfinding.

Dense Forest line-of-sight blocking, external cover, hit points, armor, destruction behavior and visuals are unchanged.

## Settings

- **Infantry can traverse Dense Forest**: ON by default. This controls both infantry traversal and Forest-container entry.
- **Debug logging**: OFF by default.

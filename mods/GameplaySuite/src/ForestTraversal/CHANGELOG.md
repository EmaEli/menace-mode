## 0.1.2
- Build-only fix: import `Menace.SDK` so the existing `ModSettings` API resolves when compiling Forest Traversal.
- No gameplay values or Dense Forest behavior changed from 0.1.1.

## 0.1.1

- Fixed Dense Forest still being unusable as an infantry destination.
- Dense Forest now uses MENACE's native `EntityContainerType.Forest`, allowing infantry to enter and end movement inside the forest.
- Keeps `IsTraversableByInfantry=true` so infantry can also path through Dense Forest structure tiles.
- Added direct Modpack Loader data patches for both fields in the standalone package and Gameplay Suite, with typed runtime reapplication as fallback.
- Keeps the native `cover_inside_forest` inside-cover profile and the template's native 30 AP enter / 30 AP leave costs.
- Vehicles remain blocked because vehicle actors are not containable and the traversal flag is infantry-only.
- Forest surface, LoS, cover, HP/armor, destruction and visuals are unchanged.
- Debug logging remains OFF by default.

## 0.1

- Initial attempt to make Dense Forest traversable by infantry by changing `IsTraversableByInfantry` only.
- Uses MENACE's native Forest + structure movement costs.
- Vehicles and other non-infantry movement remain unchanged.
- Dense Forest cover, LoS blocking, destruction and visuals are unchanged.
- Debug logging is OFF by default.

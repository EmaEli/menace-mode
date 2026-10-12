# Changelog

## 0.2.2

- Added the two large obstacles titled `Cargo Containers` to the explicit native LoS-blocking scope.
- The smaller singular `Cargo Container` (`obstacle_pirate_container_01_1x1`) remains deliberately non-blocking.
- No changes to cover strength, movement, pathfinding, or other building targets.

## 0.2.1

- Fixed compilation against the current Menace Modpack Loader by using the explicit `HarmonyLib.Harmony` type in `IModpackPlugin.OnInitialize` and the Harmony hook installer.
- No gameplay or Line-of-Sight behavior changes from 0.2.


## 0.2

- Expanded scope to include all currently enterable vanilla Building and Tower structures.
- Added the generic `bunker` and `building_military_1x1_watchtower`, both of which are enterable in current game data but are not marked as LoS blockers.
- Added all native military bunker variants to the recognized scope.
- Added the two optional Garrisonable Buildings 3x3 candidates to the explicit compatibility scope.
- Added runtime detection for any structure whose current `ContainerType` is `Building` or `Tower`, so structures made garrisonable by another mod are automatically recognized at `Structure.Create` time.
- Vehicle and forest containers remain excluded.
- Existing native/custom `BlockedLineOfSight` matrices are now preserved when a structure already blocks LoS correctly.
- Debug logging remains OFF by default.

## 0.1

- Initial proof-of-concept release.
- Makes nine clearly enclosed small building templates block line of sight.
- Uses MENACE's native `IsBlockingLineOfSight`, `BlockedLineOfSight`, `Structure.BlockLineOfSight()` and destruction/unblock behavior.
- Reuses each target building's blocked movement footprint as its LoS-blocking footprint.
- Includes a `Structure.Create` prefix as a timing-safe fallback so templates are patched before vanilla structure creation evaluates LoS blocking.
- Debug logging is OFF by default.

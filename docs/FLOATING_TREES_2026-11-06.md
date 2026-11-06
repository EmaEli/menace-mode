# Floating desert trees — #92

## Observed environment

- User reports only some trees floating on desert maps.
- PC: MENACE 0.7.18+22828, Larger Maps manifest 2.5.0 / assembly 2.5.0.0.
- GameAssembly SHA256: 494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9.
  The local 0.7.18 native reference matches the PC hash.
- Current log: blueprint replaced 42x42 -> 64x64; stamp footprint scale 0.744.
  Gameplay helper and decorative/GPU-spawner patches are installed. No LargerMaps
  failure/exception/ERROR entry was found. Terrain diagnostics are not enabled;
  the exact floating tree asset, biome template, seed and coordinates are unknown.
- MENACE was running during inspection. No live mod, save or settings writes.

## Native evidence

The existing correction hooks PointPropGenerator.ReprojectGameplayPoint
(RVA 0x72F7E0). It converts border-shifted world X/Z to the fixed heightmap domain.

However, PointPropGenerator.SpawnPrefabs (RVA 0x730420) inlines that helper:
at RVAs 0x730665–0x73067D it loads blueprint BorderWorldUnits at +0x2C,
adds point world X/Z, and directly calls GetWorldHeightBilinear (RVA 0x723DD0).
There is no call to ReprojectGameplayPoint and no world-to-heightmap scaling.
Patching the helper therefore cannot fix this caller. A bounds clamp alone also
cannot fix an in-range sample of the wrong terrain location.

SpawnObjects (RVA 0x7300C0) calls SpawnPrefabs only for ScatterType.Prefab.
SpawnPrefabs walks GameplayTerrainPoints, matches config.PointType, consumes one
input.Rng.NextEntry for each matching point, and instantiates it with the sampled Y,
point X/Z/orientation and TacticalManager.GetLevelRoot parent. It does not apply
point.Scale or slope/flag filtering. InitForestPrefab.Start has its own child
LODGroup grounding, so not every tree prefab necessarily exhibits the bad root Y.

This proves a missed height-sampling path, not the identity of the user's trees.

## Candidate 2.5.1

Replace only the ForestDense/Sparse/Single prefab loop on maps larger than 42.
Correct its samples and preserve the native selection/order/transform behavior.
Leave other prefab types (including water), entities/features, grass/GPU points,
terrain stamps, AI and mission areas unchanged. No global offsets or broad
GetWorldHeightBilinear scope. On a partial failure, never replay native spawning.

102 managed checks execute the actual production prefix with stand-ins: three
forest types at 64/80/96, border/far-edge samples, selection/RNG/parent/orientation,
source immutability, native-sized/non-forest exclusions and partial failures.
These do not execute IL2CPP or verify visible tree placement. Local compilation
against the matching 0.7.18 proxies succeeds (one pre-existing unused-field warning).

Additional local validation: LargerMaps Python discovery ran 143 tests (135 passed,
8 optional skips); GameplaySuite Python discovery passed 196/196. The existing
offline runner passed 18,098 assertions, including selected LargerMaps/settings
artifact checks: manifest 2.5.1, assembly/file 2.5.1.0, companion 0.8.2 with its
retained assembly identity 1.0.0.0. No dependency changes were needed.

## Deployment / next verification

The canonical catalog does not currently allow LargerMaps on the 0.7.18 hash;
its existing native extended-pathfinder table also has no 0.7.18 entry. Do not
weaken those gates or copy a DLL manually. PC remains on 2.5.0. Windows canonical
BuildOnly/install and visual confirmation are pending. Before test installation,
review scoped 0.7.18 compatibility and keep unsupported extended-map modes gated.
The game must also be closed. Then test a freshly generated desert mission and
capture a floating tree screenshot if any remain; retrieve the fresh log.
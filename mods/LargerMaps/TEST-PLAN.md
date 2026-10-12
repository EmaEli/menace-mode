# Larger Maps 1.3.12 — targeted validation (game required)

## 1. Confirm both safeguards loaded

Build and deploy `LargerMaps-1.3.12-authoring` using `build-and-deploy.bat` with your normal Menace installation and .NET 6 SDK. Keep MENACE Gameplay Suite 0.31.4 unchanged. In `Latest.log` confirm the `AI task lifetime safety active` and `terrain-hole array indexing safety active` startup lines. If you see `AI task safety could not be installed`, stop: the patch is NOT active. No per-turn debug logging should be enabled.

## 2. Validate the terrain-hole fix (new in 1.3.12)

- Re-run the Rogue Army defend-outpost mission if feasible. The 1.3.11 Player.log had `IndexOutOfRangeException -> Map.AddHole -> TerrainHole.Start` during map generation.
- Make sure at least one terrain-hole object (e.g. excavated ground / scenery requiring a cutout) is visible. Verify that the terrain cutout is present and that its placement aligns with the object; visually inspect holes/cutouts close to map borders.
- The new Player.log must have **zero** `IndexOutOfRangeException` stacks originating from `Map.AddHole` or `TerrainHole.Start`.
- If `Latest.log` reports `unexpected IL2CPP bool[,] shape` or `managed Map.AddHole replacement failed`, the safety path did not run correctly; keep 1.3.11 and share both logs plus a screenshot.
- The cutout should be carved, not silently skipped. Full 64x64 map and all existing terrain/foliage features must remain intact.

## 3. AI freeze regression (unchanged from 1.3.11)

Re-run `mission.rogue_army_defend_outpost` if possible; the old log showed a 64x64 map for operation `operation.rogue_army_turn_the_tide` (seed `3689322`). Include several Rogue Army mortars, officer, sniper, ATGM and MG units. Run at least 3 enemy turns if feasible. Observe real wall-clock enemy-turn durations. The initial 64x64 generation should still show `42x42 -> 64x64` in `Latest.log`.

## 4. Validate by outcomes, not missing timeout messages

PASS conditions: enemy turn completes and gameplay resumes; NO `Collection was modified; enumeration operation may not execute`; NO `A concurrent update was performed on this collection and corrupted its state`; no new AI/thread exceptions or regressions. The old `AI task timed out` log will disappear **by construction**, and therefore is NOT independently a performance test.

Potential regression: an enemy turn that is very slow but still progressing. **Do not assume fixed performance.** Record estimated durations and the first real exception if a turn still fails. Do not lower map size or change the Suite until the test is evaluated.

## 5. Regression passes

- Weapon teams with indirect fire and standard infantry direct fire still choose targets and complete actions.
- Overwatch / Coordinated Fire / Infantry Assault and the rest of the installed Suite still work.
- Capture, evac, timed missions, foliage, elevation texture, tile highlighters and zoom still work.
- If the turn remains stuck but dictionary errors are gone, collect `Latest.log` and `Player.log` to investigate **performance** independently from the now-addressed detached-task corruption.
- If tile/LoS null exceptions persist in a clean run, investigate that separately; 1.3.12 does not suppress these exceptions.

## Build status

This package is a **source/authoring archive**. The runtime DLL was not compiled or in-game-tested here because the current environment does not include the .NET SDK, MENACE Modpack Loader DLL or a running Windows game installation. You must build the ZIP on the development game installation. The source and package metadata were checked offline.

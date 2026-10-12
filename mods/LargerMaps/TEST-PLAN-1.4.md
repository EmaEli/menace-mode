# Larger Maps 1.4 test plan (critical: do not skip)

## Preflight

- Backup saves and current 1.3.12 package; game **closed** while deploying.
- Game version must be `v0.7.15+21883` with exact `GameAssembly.dll` SHA-256:
  `877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db`.
- Build from extracted ZIP using `build-and-deploy.bat` with .NET SDK installed. Confirm no compiler warnings/errors.
- Confirm startup logs indicate 80x80 backend **active**, 1.3.11 `AI task lifetime safety active`, and 1.3.12 terrain-hole safety active. If backend is not active, expected map size is 64x64: stop the 80x80 test.
- Disable unrelated developer/debug logging and preserve Gameplay Suite unchanged during initial test.

## 1. Baseline and map creation

1. Start at 64x64 in Mod Options (restart) to ensure 1.3.11/1.3.12 still work, if practical.
2. Set map size to **80** (restart) and enter a new mission. Log must say `42x42 -> 80x80` and `tiles=6400`.
3. Verify complete map rendering, elevation, foliage, terrain holes, zoom-out, deployment zones, capture/evac and enemy reinforcement positions.
4. The tactical highlighter/cursor and weapon targeting must reach the new map boundary.

## 2. Pathfinding across the old boundary

- For **infantry**, test movement `x=62 -> x=67`, `z=62 -> z=67`, and a path crossing both `x=63/64`, `z=63/64`.
- Try long paths, backwards movement, difficult terrain, forest, narrow passage and blocked tiles.
- Try **vehicles** and their turning radius / multitiale movement and AP preview; compare with the actual AP charged.
- Try path previews repeatedly on the same destination and after modifying an obstacle. No flicker, invalid tile, broken path or teleport.
- Check pathfinding also works from/to the new outer area (coordinates 70-79).
- If possible, test maps with buildings/garrisons and Overwatch, but keep any new failure isolated.

## 3. Enemy AI / concurrency / performance

- Use the known problematic Rogue Army defend-outpost mission (seed `3689322`, operation `operation.rogue_army_turn_the_tide`) with multiple mortar and machine-gun teams.
- Run >= 5 complete enemy turns; preferably an entire battle. Compare wall-clock AI turn durations with 64x64, same seed and similar army.
- No `Collection was modified` / `A concurrent update was performed on this collection and corrupted its state` / unexpected NullReferenceException / AI hang.
- Missing `AI task timed out` lines is NOT evidence of performance (1.3.11 removed that timeout wrapper).

## 4. Bigger map only after 80 passes

- Change setting to 96 and **restart**. Confirm `BLUEPRINT REPLACED 42x42 -> 96x96`, `tiles=9216`.
- Repeat movement tests across x/z 63/64 and at x/z 80-95, enemy AI turns, terrain holes, objectives, and elevation textures (96*32=3072).
- If frame-time / AI time is unacceptable, remain on 80 (or restore 1.3.12).

## Stop / rollback immediately

On crash, black/flickering map, unexpected zero-length path, infinite turn, path teleport, units unable to cross 63/64, corrupted dictionary, or stalled load: quit the game and restore 1.3.12 (or use the files in `artifacts/`). Attach `MelonLoader/Latest.log`, `Player.log`, `build-and-deploy.log`, and ideally the problematic mission seed and screenshots. Do not publish this 1.4 yet.

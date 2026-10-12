# Larger Maps 1.4.7 — in-game validation (experimental)

## Preparation

1. Back up saves; completely exit MENACE.
2. Extract the package, execute `build-and-deploy.bat`, and keep the deploy log.
3. Ensure the game is **v0.7.15+21883** and the native SHA-256 matches the supported assembly.
4. Set `DiagnoseMissionAreas=ON` temporarily in Mod Options and restart after each map-size change.
5. Capture `Latest.log` **before the next restart**; it is overwritten.

## Test A — baseline, Repel Counterattack at 64x64

Use the same seed `7235667` if you can. Record the preview. The reference v1.4.6 log had:

- DEFEND: `(28,27,9,11)` (smaller than deployment)
- Deployment: `(25,24,15,17)`
- North enemy spawn border from native `(0,38,42,3)` to `(0,58,64,5)` (old 1.3.x band adjustment)

Confirm v1.4.7 selects `native 64x64 pathfinding selected (no native byte patches)` and does **not** change the native mission bounds. The mission seed and setpiece arrangement should match baseline.

## Test B — same mission at 96x96

- Confirm `installed 26 verified native byte edits (including MissionTemplate.GetBounds NxN)` (or the exact logged equivalent) and 96x96 backend active.
- **DEFEND** must have nonzero dimensions and be correctly centered on the mission's actual objective.
- **Deployment** must cover a meaningfully larger area than DEFEND and remain centered on the outpost.
- North/south spawn strips must span ~96 tiles horizontally. East/west strips must span ~96 tiles vertically. Enemy icons must occupy the correct four edges, not one corner.
- Compare dimensions/relative coverage using tile counts (not pixels). Exact coordinates can change if the native bounds correction changes setpiece placement, so do not assert pixel-perfect same center.
- Start mission and test a few turns. Verify defender-zone scoring and enemy spawn validity; observe AI movement past coordinate 63.

## Test C — intermediate sizes

Repeat a preview at **72x72, 80x80, 88x88**. Verify DEFEND, Deployment, and all four edge spawns remain valid and proportional. Keep route diagnostics off unless pathfinding stalls.

## Test D — other mission roles

Sample at least one mission involving rescue, evacuation, civilian spawn, capture, extraction, reinforcements, and scripted edge attackers. Check actual mission success/failure conditions, not only preview graphics.

## Rollback

Quit MENACE. The build/deploy script saves the first previous DLL and manifest in `artifacts/*before-1.4.7*`. Restore them according to the script output (or restore your saved v1.4.6 package), then restart. Do not continue a campaign with corrupt-looking spawn or objective areas.

Turn off `DiagnoseMissionAreas` after testing to keep logging minimal.

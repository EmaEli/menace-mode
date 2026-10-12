# Larger Maps 1.4.5 — native 64×64 zone geometry as reference

**Experimental authoring/source package.** Needs .NET 6 and MENACE v0.7.15+21883 (Windows x64) to build. No DLL or proprietary game binaries are included. Not tested inside MENACE in this environment.

## Why 1.4.4 was wrong

1.4.4 used the entire placed primary setpiece (e.g. a 20×22 COP) as the *minimum* DEFEND and deployment area. The native mission intentionally uses different rectangles: `PrimaryChunks` deployment is authored with `BaseExpand=-3` and the DEFEND scoring area has its own `MissionAreaConfig`. They must not be replaced by the same large box.

**Important:** The user's 64×64 screenshot came from 1.4.4, so it shows a **modified** area. It cannot be used to measure the original 64×64 footprint. Version 1.4.5 instead uses the **actual native `MissionAreaConfig.AddToList` result** as the 64×64 reference, which avoids guessing its size from an image.

## Rules

| Role / area type | Source and transformation on maps >64 |
|---|---|
| DEFEND, CAPTURE, other objective areas anchored to `PrimaryChunks` or `Chunk` | Scale the native resolved footprint by N/64. Keep the area's intentional offset relative to the matched primary setpiece, when available; **never impose the setpiece's full footprint**. |
| Primary-chunk player deployment | Same ratio, but preserve native half-open `RectInt` width/height. Deployment and DEFEND remain **separate**. |
| Explicit `Rect` control, extraction and trigger zones | Scale their native 64×64 start/end coordinates by N/64, preserving signed directions and clipping to the map. |
| North/South/East/West and corner border zones | Preserve the length/percentage already generated natively for the current map; proportionally increase *only* their border thickness and edge insets, not the long dimension a second time. |
| Anchored enemy spawn/reinforcement/drop-off | Scale the native size around its placed anchor by N/64; invalid/zero-size spawn rectangles still have the 1.4.2 safety fallback. Valid narrow rectangles are no longer forced to 5×5. |
| Authored 42×42 spawn rectangles | Retain the original 1.3.12 path for 42→N conversion; this is a separate, already-working coordinate system. |
| `ObjectiveConfig.AIZones[].Area` with authored `Rect` | Keep the existing *single* 42→N scaling; the generic hook deliberately skips it to prevent double transformation. |

**64×64 reference:** new area-proportional logic is inactive at 64×64. Existing 1.3.12 safety fixes for degenerate evacuation/capture areas and some 42→64 spawn/AI-zone conversions remain; the reference is thus the *working Larger Maps 1.3.x* behavior, not the original 42×42 vanilla map.

### Supported sizes

**64, 72, 80, 88, 96**; default 80. Set `TacticalMapSize` in Mod Options and **restart MENACE**. Ratios relative to 64: 1.000, 1.125, 1.250, 1.375, 1.500. The physical buildings **do not grow**; mission-zone footprints do.

A native 64×64 zone 12×14 would become approximately 14×16 at 72, 15×18 at 80, 17×19 at 88 and 18×21 at 96. **12×14 is illustrative, not a measured dimension of Repel Counterattack.**

## Installation & rollback

1. Quit MENACE and back up saves.
2. Extract the archive, run `build-and-deploy.bat` (requires existing .NET 6 / MelonLoader / Menace Modpack Loader).
3. The script makes a one-time backup of the previous DLL and manifest under `artifacts/*before-1.4.5*`.
4. To roll back, close MENACE, restore *both* DLL and manifest and restart. Native patches modify memory only, **never GameAssembly.dll on disk**.

## Testing

See `TEST-PLAN-1.4.5.md` for a step-by-step baseline comparison using **the same mission seed at 64×64 and 96×96**. Check both mission-prep rendering and actual tactical scoring. All tests in `tests/` are offline geometry / source / native opcode checks; they cannot verify Unity UI or native IL2CPP behavior.

## Unchanged

The 24-site extended pathfinding patch, AI task lifetime fix, terrain holes, elevation, camera, enemy count, tactical combat, settings and all other mod features remain as 1.4.4. Debug remains OFF and no per-frame/per-turn logs are added. A script creating zones without `MissionAreaConfig.AddToList` can require a separate hook; the change cannot automatically cover arbitrary future custom scripts.

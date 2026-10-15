## 2.0 - Jiangyu package

Larger Maps is built and installed as the Jiangyu package in [`../jiangyu/LargerMaps`](../jiangyu/LargerMaps) (`jiangyu compile --release --clean` from that directory, then copy `compiled/` into `Mods\LargerMaps\`). Options live in the MenaceModSettings window (F9). This directory keeps the gameplay sources (`src/LargerMaps`), the offline regression tests (`run-tests.bat` / `python3 -m unittest discover -s tests`) and the technical notes. The legacy Modpack Loader build (`build-and-deploy.bat`, `modpack.json`) has been removed; references to it in the version notes below are historical.

---

## 1.8 - Up to 5x terrain stamp density (MEN-003 experiment)

In **Mod Settings > Larger Maps** choose `IncreaseTerrainStampDensity=ON` and set `TerrainStampDensityMultiplier` from **1x to 5x** (default **1.5x**, feature toggle OFF for new installs). Restart the game to apply. On an existing install the saved toggle and multiplier remain as set by the user.

Suggested progression for a **96x96** map: **3x**, then **4x-5x** if there is still enough room for vehicles and objectives. Vanilla 4 large + 10 medium stamp attempts become 12 + 30 at 3x, or 20 + 50 at 5x, with a safety ceiling of 200 attempts per group (never below native counts). Placement validation can reject attempts; visible density is not guaranteed to scale linearly.

64x64 maps remain unchanged, and 72/80/88 interpolate smoothly. This only adjusts **terrain stamp attempt counts**, not per-tile grass, foliage, buildings, or mission-authored setpieces. `DiagnoseTerrainStampDensity` enables up to 10 generation summaries and remains OFF by default. Test visuals, generated roads/cover and FPS; revert by turning the toggle OFF and restarting if needed. Follow the version-specific [1.8 A/B test plan](TEST-PLAN-1.8.md).

---

## 1.7.1 compile fix

Fixes CS0118 (`Harmony` namespace/type ambiguity) in the 1.7 stamp-density integration. No behavior changes. Re-download the source branch before building. Build and game tests remain outstanding.

---

## 1.7 experimental MEN-003: terrain stamp density

Set `IncreaseTerrainStampDensity=ON` and `TerrainStampDensityMultiplier=1.25` (restart required). The effect gradually ramps from zero at 64x64 to full strength at 96x96. It changes **terrain-landform stamp attempt budgets**, not per-tile vegetation, grass or mission setpieces. Default OFF for safe A/B testing. `DiagnoseTerrainStampDensity` optionally logs up to 10 generation summaries. Native placement may reject attempts, and no gameplay validation has yet been performed. See `TEST-PLAN-1.7.md`.

---

## 1.6.1 note

The 1.6 startup log proved the simplified AI feature was **not activated** (`no eligible skill templates updated`). 1.6.1 adds a direct game-template-ID fallback, without changing the native pathfinder. Before spending time testing gameplay, verify the startup line `simplified large-map combat planning: N attack / M buff skills adjusted` has **N > 0**. If it does not, stop and share the startup log. Live speedup is not yet established.

# Larger Maps 1.6 — Simplified Combat AI Planning

**Experimental authoring package.** The 80x80 v1.5 test proved that cutting path searches by over half did **not** shorten AI turns meaningfully. This version targets the actual high-cost `Attack/Assist.Collect` positioning branch using MENACE's own flag rather than patching the pathfinder.

- `SimplifyCombatAIPlanning`: **ON by default for maps above 64x64**, never applied to 64x64. Changes `SkillTemplate.AIConfig.InfluencesPositioning` to `false` for attack skills and recognized support/buff configs at tactical scene entry, and restores original values on return to menus. The native game still handles attack validity, skills, movement, cover and objectives. It no longer proactively searches many hypothetical firing/support positions for these selected abilities. AI may be more direct but less adept at repositioning for an ideal shot.
- `UseLocalTacticalRouting`: **OFF by default now.** This was the separate 1.5 `NearTileLimit` / `CullTilesDistances` experiment, left optional for comparisons. If previously enabled and saved in Mod Settings, **disable it explicitly** for the initial 1.6 comparison.
- No new Harmony hooks; no replacement of `FindPath`, no cache, and no changes to native movement buffers. The stable 1.4.13 native pathfinder and 64x64 reference are retained.
- Build via `build-and-deploy.bat` with the game closed (Windows .NET 6 SDK and installed MENACE required). `run-tests.bat` checks code and native references offline; **compilation and gameplay are not verified here**.
- Before spending time testing AI performance, open `MelonLoader/Latest.log` and check for `simplified large-map combat planning: <number> attack / <number> buff skills`. If it says that no templates were updated, the feature did not activate and the AI will remain unoptimized. Do not run a long performance test in that case.
- Back up your save and retain 1.5 / 1.4.13 for rollback. See `TEST-PLAN-1.6.md`.

---

# Larger Maps 1.4.13 - Conservative Mission Areas (candidate)

**Experimental authoring package, not a confirmed runtime fix.** Built from 1.4.12. On 64x64 the area hook path remains the exact 1.3.11 reference that was fast in the reported A/B test. On 72/80/88/96 it now uses **narrow, conservative area handling** instead of the new broad Objective.Init area-role pipeline introduced in 1.4.x.

- **Native pathfinding** and verified mission-bounds patches for >64 are unchanged from 1.4.10; 64x64 remains unpatched.
- Keep 1.3.11's limited area scope hooks for all map sizes. For larger maps, preserve map-generated anchored zones, scale only the **short thickness** of native border bands, scale intentionally authored rectangles from 42 to N, and normalize only malformed spawn ranges.
- Disable 1.4.x's broad area-discovery and objective-role scaling hook installation on **all sizes**, since the 64x64 version with those hooks was slow; the precise cause has not been proven.
- Preserve the existing native mission boundary, elevation, terrain holes, AI task-lifetime fix, optional profiler (OFF) and invalid-only spawn safety on maps >64.
- **Trade-off:** on large maps some previously stretched objective/defend/control zones may be smaller than in 1.4.10. Their correctness must be checked before public release.

Build with `build-and-deploy.bat` outside the game's Mods folder, with MENACE closed. Keep the known-fast 1.3.11 backup. Use `run-tests.bat` for static checks and `TEST-PLAN-1.4.13.md` for a single eventual gameplay check. There is no compiled DLL in this archive; Windows .NET 6 + installed MENACE required.

---

# Larger Maps 1.4.9 — diagnostic AI performance instrumentation

**Authoring/source package. Not a compiled mod, not tested inside MENACE.** This diagnostic-only update retains ALL v1.4.7 gameplay/AI/native patch logic and reuses the same two optional AI timing settings (both OFF by default for clean installs). The performance cause is not established, so no speculative AI optimization is included. Compile and deploy from Windows with `build-and-deploy.bat`, MENACE closed. Back up your save data and keep the previous mod package for rollback.

For this diagnostic use `TEST-PLAN-1.4.13.md` (historical 1.4.10 notes remain below). Run offline checks with `run-tests.bat`. Optional native CPU sampling helper: `capture-ai-cpu.ps1`. Previous mission bounds details below describe the unchanged v1.4.7 baseline.

## The actual root cause of 96x96 mission geometry failures

In `Repel Counterattack` using v1.4.6, the 64x64 diagnostic log confirms `DEFEND (28,27,9,11)` and deployment `(25,24,15,17)`. The 96x96 log recorded `DEFEND (42,42,0,0)` and deployment `(41,40,1,2)` (then changed to `(37,36,2,3)` by our fallback scaling). North/south and east/west enemy spawn bands stayed 42 tiles long. Thus the map blueprint and native pathfinder were enlarged while **mission area creation remained clipped by a 42x42 rectangle**.

The game binary v0.7.15+21883 confirms that `MissionTemplate.GetBounds()` (RVA `0x5F61C0`) returns a fixed `(0,0,42,42)` and `MissionAreaConfig.AddToList()` clamps against `Mission.GetBounds()` which calls it. Scaling the resulting 0x0 area *afterwards* cannot reconstruct it.

## Changes from 1.4.6

- The existing version-locked native patch transaction now has **26 validated instruction sites** instead of 24: both 42 constants in `MissionTemplate.GetBounds` are changed in memory to the selected N (72/80/88/96) **before mission preview**, together with the existing 128x128 backend changes. 64x64 remains on the original native implementation, with no new native byte modifications.
- Border spawn regions now use the true N-wide extent from the game. Their *short axis only* is expanded to preserve the border-band thickness proportions already used at 64x64. No second expansion of the N-long axis.
- DEFEND and deployment remain distinct, derived from native mission rectangles. The speculative full-compound-minimum rule from 1.4.4 remains removed. Previous terrain, AI timeout, and pathfinder fixes remain unchanged.
- Detailed mission-area logging is optional (`DiagnoseMissionAreas`, OFF by default, max 160 records per mission preview).

## Limits / safety

The patch is locked to `GameAssembly.dll` SHA-256 `877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db`. If it does not match, Larger Maps falls back to 64x64 rather than patching unknown instructions. The DLL on disk is never modified. No release DLL is bundled.

**This fix is not yet validated in the game.** Offline tests verify only the instructions, math, and source/package consistency. Expanding the mission-wide bounds can also affect other game systems that read `MissionTemplate.GetBounds()`. Test objective scoring, enemy placement, movement, and save loading before continuing a campaign.

Follow the short scenario matrix in `TEST-PLAN-1.4.7.md`.


## 1.5 experimental local tactical routing

In the historical 1.5 release (not the 1.6 default), for 72x72 and larger, `UseLocalTacticalRouting` (ON by default there) changes the game's built-in AI weights when entering Tactical: `NearTileLimit: 64 -> 20` and `CullTilesDistances: 20 -> 12` with configurable sliders. These limit the AI's tactical position search, allowing the native scorer to keep prioritizing cover, attack utility and distance. Full native pathfinding is unchanged for actual legal movement (and is still used if movement requires a detour).

**Important:** No artificial 16x16 sector walls are created and no custom pathfinding is introduced. This first stage trims native candidate search; it does not guarantee straight-line movement or speedup. 64x64 reference behavior and player controls are unaffected by this feature. To disable, set `UseLocalTacticalRouting` OFF and restart MENACE. Diagnostic logging remains OFF by default.

Try 80x80: first ensure Lim and a vehicle can move, then time the AI round progression vs 1.4.13. If the AI becomes passive or seems to stop taking cover, try `LocalNearTileLimit=32` or disable the feature and restart. Build requires MENACE installed on Windows; offline source tests cannot prove game runtime.

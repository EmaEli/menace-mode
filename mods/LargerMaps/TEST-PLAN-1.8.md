# Larger Maps 1.8 — MEN-003 extended terrain-density A/B test

**Version 1.8 not runtime verified.** Requires MENACE v0.7.15+21883 and working Larger Maps 1.7.1 baseline.

1. Back up save and LargerMaps DLL + modpack.json before any build/deploy. Close MENACE.
2. Build and deploy the `feature/men-003-stamp-density` branch (1.8 high-density experiment) on the Windows machine with the installed game's .NET 6 dependencies.
3. Use 96x96. Keep `UseLocalTacticalRouting=OFF`, other mod settings unchanged, and compare the **same mission seed and biome**.
4. Run A: `IncreaseTerrainStampDensity=OFF`; record screenshot, generation duration, FPS, and number/spacing of landforms.
5. Run B: `IncreaseTerrainStampDensity=ON`, `TerrainStampDensityMultiplier=1.5`; restart, same seed. Set `DiagnoseTerrainStampDensity=ON` only for a one-off diagnostic if needed.
6. Run controlled C and D with density **3x** and **5x**, respectively (same seed / biome) and compare screenshots, generation time and cover/open areas. Example 4/10/0 native limits yield adjusted 12/30/0 and 20/50/0 at 96x96. The 5x target may be capped at 200 attempts per group. Compare 72/80/88 separately before generalizing.
7. Verify vegetation/grass not elevated or missing, terrain stamps not clipping compounds, roads/vehicle routes/deploy areas usable, objectives reachable, AI rounds terminate, cover and line of sight remain tactically useful, and no new exception or noticeable FPS regression.
8. Review startup logs and at most 10 optional `STAMP DIAGNOSTIC` records. The records show budgets and placed entries, not guaranteed valid gameplay objects.
9. For rollback set `IncreaseTerrainStampDensity=OFF` and restart, or restore backed-up 1.7.1 DLL and manifest together.

The multiplier changes *attempts*, not guaranteed stamp placement. Setpiece/grass/per-tile environment spawns are intentionally unchanged. Keep MEN-003 open until build and live A/B test pass.

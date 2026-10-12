# Larger Maps 1.4.10 - behavior ID profiling under existing flags

**2026-10-09 build correction:** The original ZIP failed on Windows with `CS0656` at `AIProfiling.cs:356`. This corrected package uses a regular typed list and sorting instead of a nested LINQ anonymous projection. No setting or AI algorithm changed; re-run `build-and-deploy.bat` from the corrected archive.

This is a source/authoring build, not a prebuilt mod DLL. It does not fix AI computation time.

1. Leave your existing `DiagnoseAIPerformance=ON` and `DiagnoseAIPathfinding=ON` settings in place. **No new settings were added.** The already-enabled flags are read at startup; there is no need to launch the game first to switch on another debug flag. A fresh install still defaults both OFF to avoid routine log spam.
2. Close MENACE. Run `build-and-deploy.bat` and start normally. Confirm `Larger Maps 1.4.10` and `AI PERF diagnostic hooks=13, unavailable=0, deep-operations=True` in `Latest.log`.
3. Run the familiar 80x80 Rogue Army / Relief Outpost mission (seed 5320037) for one or two rounds. Do not change other mods/settings between comparison runs.
4. In `Latest.log`, inspect `AI PERF behavior turn=#... actor=... template=enemy.vehicle_rogue_army_heavy_tank id=... collect=n=...,sum=...ms,max=...ms` immediately after the associated `AI PERF unit` summaries. These identify which behavior ID occupies the long `Behavior.Collect` interval. `AI PERF slow` rows also identify slow behavior IDs when they fit the existing bounded queue.
5. Share `Latest.log`. Do not interpret `collect` + `findpath` + `evaluate` as additive: they are nested and worker operations may run concurrently. If the 13-hook startup count is lower, share `build-and-deploy.log` too.
6. After diagnostics, switch existing `DiagnoseAIPathfinding=OFF` and `DiagnoseAIPerformance=OFF`, then restart for normal play (no high-frequency diagnostics).

No native gameplay changes; 26 validated native patch sites and the no-orphan-task protection must remain intact.

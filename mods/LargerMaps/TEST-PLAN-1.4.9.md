# Larger Maps 1.4.9: repeat the slow enemy actor diagnostic

This build is a diagnostic accounting fix, NOT a performance optimization. Use a disposable save and retain 1.4.8 backup. It compiles/deploys through `build-and-deploy.bat` on Windows (not compiled in the authoring environment).

1. In Larger Maps settings choose 80x80 and set `DiagnoseAIPerformance=ON`; restart MENACE. Set `DiagnoseAIPathfinding=OFF` for the first measurement (the previous session had deep profiling ON, which can alter runtime performance).
2. Reuse `Rogue Army - Relief Outpost`, seed 5320037. Play a complete enemy round (preferably two if practical). Do not intentionally wait through a freeze beyond the normal test.
3. Verify startup `AI PERF diagnostic hooks=5, unavailable=0, deep-operations=False`.
4. Inspect `AI PERF turn=#... scope=actor-activation actor=0x... template=... closedBy=next-activation` rather than using only the `Finish` summaries. If no actor template appears, correlate the activation order with `Player.log` and note the uncertainty.
5. Compare durations for the heavy tank with neighboring infantry. Report `evaluate`, `task-observed`, `execute`, `movement-path` and `collect` metrics, without adding nested wall times.
6. Only if slow actor capture is successful, repeat with `DiagnoseAIPathfinding=ON` in a separate session to separate pathfinding versus scoring costs. Keep all other settings/mods fixed.
7. Send `Latest.log`, `Player.log`, and any Windows build failure log.

This version must not alter gameplay behavior, native mission geometry or the v1.3.11 no-orphan-task safety fix. With both new diagnostics OFF, no `AI PERF` lines should appear.

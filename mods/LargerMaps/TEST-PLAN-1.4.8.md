# Larger Maps 1.4.8 - low-overhead AI profiling test protocol

This is a diagnostic authoring ZIP, not a compiled or in-game-verified release. Use a disposable save. Close MENACE before running `build-and-deploy.bat` in Windows. Retain the v1.4.7 ZIP and a known-good DLL for rollback.

## Configuration

In Larger Maps mod settings (all require a restart):

- `TacticalMapSize = 96`
- `DiagnoseAIPerformance = OFF` by default; turn ON only for measured sessions.
- `DiagnoseAIPathfinding = OFF` by default; leave OFF on the first diagnostic session.
- `DiagnoseAIRoutes = OFF` and `DiagnoseMissionAreas = OFF`.
- In other mods, disable debug/verbose logging (especially Visibility Overhaul and Weapon Ranges Rebalanced).

The old diagnostic hooks guarded by `AIDiagnostics()` remain disabled; this build does not enable them.

## Small, controlled sequence

1. **Baseline**: v1.4.7, map 96x96, same mission `Rogue Army - Relief Outpost`, seed 5320037, all diagnostics OFF. Record wall time for **one** complete enemy faction turn with a stopwatch; do not endure four slow rounds.
2. **Stage 1**: v1.4.8, 96x96, same seed and mod list, `DiagnoseAIPerformance=ON`, `DiagnoseAIPathfinding=OFF`. Time the same phase. Capture new `Latest.log` and `Player.log` and compare wall time versus baseline. Expect `AI PERF turn=`, `AI PERF unit`, and optional `AI PERF heartbeat` lines. If hooks are missing or no summary appears, the profiler is *not* validated: revert settings and send the log.
3. **Stage 2 only if needed**: restart, set `DiagnoseAIPathfinding=ON` too. Run one enemy turn. Compare `collect`, `postprocess`, `pick`, `movement-path`, `path-pool`, `findpath` and `forward-back`. For fast repeated operations, use `n` and `sum`; for isolated stalls use `max` and `AI PERF slow` samples. Beware interception overhead.
4. **Mod isolation**: same 96x96 seed with Overwatch off, leaving other mods unchanged; then (if necessary) restore Overwatch and test Visibility Overhaul off, then Coordinated Fire. Keep one variable changed per run. Some changes may alter AI decisions: compare cautious trends rather than exact call equality.
5. **Map comparison**: if mission generation permits, compare 80x80 and 96x96 under otherwise matching settings and units. Map size affects layout, so this is a scaling experiment, not a strict identical-state benchmark.
6. **CPU profile**: if `task-observed` is large but measured suboperations remain small or tests conflict, optionally run `capture-ai-cpu.ps1` for a single slow enemy phase. ETW traces may contain sensitive process names and large data; review before sharing.

## Log interpretation examples

- `wall=90000ms`, `evaluate max=85000ms`: a long Agent.Evaluate invocation was measured; stage 2 / WPR identifies its internals.
- `wall=90000ms`, `evaluate max=15ms`, `task-observed max=87000ms`: work/queue/wait outside the measured evaluation method is plausible. Inspect the Task state and use CPU sampling; do not lower timeout.
- `findpath sum=50000ms`, `collect sum=80000ms`: these may be overlapping; do NOT sum to 130 seconds.
- `pending=1` at end: potentially asynchronous work remained at summary time. Do not infer an orphaned task from this flag without correlating its true completion state.
- No heartbeat while the whole game is frozen: the main thread may not be calling the mod's OnUpdate; absence of a log entry by itself is insufficient to identify where execution is stalled.

## Mandatory behavioral regression checks before release

- Verify backend 64, 72, 80, 88, 96 selected as configured, with native fallback on unexpected game binary.
- Repeat native mission bounds 96x96 `Repel Counterattack`, seed 7235667; validate DEFEND vs Deployment, border spawn placement and victory triggers.
- Test `Rogue Army - Relief Outpost`, seed 5320037; observe enemy movement and correct turn completion, with task lifetime safety enabled.
- Test a non-DEFEND objective, reinforcements, saves, loading, manual mission exit and restart.
- Confirm no `AI PERF` lines with both new diagnostics OFF, and negligible default CPU overhead.
- Compare the game under normal and deep profiling to determine diagnostic perturbation.

## Verification matrix

Offline: Python `unittest`, native instruction bytes, source parity, manifest consistency, fixtures. Windows game DLL compilation: **not performed in this environment**. Actual Harmony/IL2CPP hook execution, AI phase timing, native patch operation at runtime, and interactions with other mods: **not yet verified**.

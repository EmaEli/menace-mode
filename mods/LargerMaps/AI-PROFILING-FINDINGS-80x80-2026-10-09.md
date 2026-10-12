# MENACE 80x80: Larger Maps AI profiling findings (2026-10-09)

## Evidence from real game logs (Larger Maps 1.4.8)

Source files in the development investigation (not redistributed in this archive):
- `Latest(20261009-074843).log`
- `Player(20261009-074852).log`

Runtime version: MENACE v0.7.15+21883, Larger Maps 1.4.8, 80x80 map with 128x128 pathfinder, mission `mission.rogue_army_relief_outpost`, seed 5320037.

- The optional profiler installed successfully: `AI PERF diagnostic hooks=13, unavailable=0, deep-operations=True` at 09:45:12.475.
- It unexpectedly generated repeated `previous faction turn ... did not emit a Finish summary` warnings, despite actual enemy turns progressing.
- The deep profiler was enabled in this session, so durations are not a clean no-profiling baseline.
- The only complete summaries in the original implementation mostly show `wall=0.0ms`, `evaluate=n=0`, `findpath=n=0`, while `heartbeat` has already observed expensive work. These zeros are a **profiling accounting error**, not proof of negligible AI cost.
- The heartbeat for activation #7 shows 10.457 s at 09:46:10.339 and 20.591 s at 09:46:20.473, with one task pending. The next activation starts at 09:46:21.532, so its approximate duration was 21.65 s.
- Activation #15 shows 10.162 s at 09:46:51.594, 20.314 s at 09:47:01.746 and 30.449 s at 09:47:11.880, with one task pending. The next activation starts at 09:47:15.396, so its approximate duration was 33.96 s.

## Actor correlation

`Player.log` contains exactly 26 nonplayer `OnTurnStart` events (12 in round 1 and 14 in round 2), matching the 26 profiler activation IDs. The 7th actor in round 1 and the 15th actor across both rounds (third in round 2) are both `enemy.vehicle_rogue_army_heavy_tank`.

This is a **strong order-based correlation, not a confirmed direct measurement**: v1.4.8 did not attach the active actor's template to the slow activation. A different asynchronous worker or an overlap could distort the correlation. The next test must confirm explicit actor labels.

## Root cause of the 1.4.8 profiler failure

The original implementation created a fresh `AiPerfTurn` on every `AIFaction.OnTurnStart()`, but this method is invoked for **each AI actor activation**, not once per faction round. `Interlocked.Exchange(ref _aiPerfActive, turn)` replaced the previous metric collector. If the previous activation did not invoke the patched `Finish()`, its summary and all its recorded costs were discarded. This produced misleading short/zero reports for later activations and repeated irrelevant warnings.

1.4.9 changes diagnostic accounting only: close the previous activation on the next `OnTurnStart()`, queue its summary until its already-running task completes, report actual counts and actor labels. It does not change AI selection, task completion, pathfinding, native patches, objectives or turn sequencing. Its new diagnostics are still OFF by default.

## What is not established

Neither the 1.4.8 logs nor this offline investigation proves that native pathfinding, vehicle movement, AI tile evaluation, cover, line-of-sight, or another mod is the main CPU bottleneck. Exact costs require a new 1.4.9 runtime test; a clean comparison also requires disabling the deep pathfinding profiler during the initial measurement.

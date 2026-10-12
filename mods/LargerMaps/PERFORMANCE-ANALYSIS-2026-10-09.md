# MENACE Larger Maps - AI slowdown study (2026-10-09)

Status: diagnosis ongoing. No bottleneck has been proven, so **there is no performance algorithm fix in v1.4.8**. This package is diagnostic-only and derives directly from v1.4.7.

## Evidence and provenance

Input: `Latest(20261009-044735).log` and `Player(20261009-044743).log` from the handover, MENACE v0.7.15+21883, Larger Maps v1.4.7, 96x96. The raw logs are deliberately NOT bundled in the authoring release (privacy/size). Input files and game binaries can be retained locally for further comparison.

- `Rogue Army - Relief Outpost` (`mission.rogue_army_relief_outpost`), seed `5320037`: player reports roughly 15 minutes over four rounds. Player.log has `OnRoundStart` through round 4 and ongoing enemy movement. It does not provide timestamps for individual pathfinding or AI decisions.
- Latest.log includes empty-timestamp intervals of 98.942, 92.109 and 91.206 seconds in the slow session, but log gaps cannot assign CPU usage to any particular operation or even prove that CPU was busy throughout the gap.
- Previous `PatchAiTaskLifetime` replaces `AIFaction.RunWithTimeout(Action, Actor)` with the *real* `Task.Run(action)` task returned to the game's scheduler. It does not abandon a worker after vanilla's 12 second timeout. This must remain unchanged.
- `Agent.Evaluate` can schedule criterion-evaluation subtasks (`Agent.ScheduleCriterionEvaluation`) and later examine completion. An elapsed task is not equivalent to an equal interval of CPU time on a thread, and the evaluation method's duration may not include all asynchronously scheduled work.
- 320 `AI ROUTE` and 75 `AREA DIAG` messages occurred with the two optional diagnostics ON, along with numerous MENACE Gameplay Suite / Overwatch entries and Visibility Overhaul debug output. Logging overhead is an uncontrolled confounder, but it is not proven responsible for the 90+ second intervals.
- Thousands of `Il2CppInterop` trampoline errors containing `LineOfSight.HasLineOfSight` and `Skill.IsUsableOn` appear after the shutdown sequence begins at 06:46:32. These errors warrant a separate investigation; the session does not prove they caused earlier slow turns.
- v1.4.7's 26 native patch sites (24 pathfinder and 2 mission-template bounds) are version-locked to GameAssembly SHA-256 `877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db`, which matches the user's provided binary. The v1.4.8 native patcher is byte-for-byte unchanged.

## Hypotheses (not established facts)

1. Large candidate sets increase `Agent.Evaluate` / behavior collection / criterion scoring time on a 96x96 battlefield.
2. Native route finding or route-process contention scales poorly at 96x96.
3. Real AI jobs take long and are now correctly awaited, whereas an unsafe timeout previously made later turns appear to progress despite unfinished jobs.
4. Overwatch, Coordinated Fire or Visibility Overhaul amplify LOS/target/action-evaluation work on large maps.
5. Scheduling, waiting on subtasks, frame progression or other code outside the profiled AI methods consumes the time.

These mechanisms are NOT mutually exclusive. The 96x96 area has 2.25 times as many tiles as 64x64; this fact alone is insufficient to infer the time-complexity or diagnose the cause.

## Instrumentation plan and semantic limitations

`DiagnoseAIPerformance` (OFF by default): only five Harmony targets, `AIFaction.OnTurnStart`, `AIFaction.Finish`, `AIFaction.RunWithTimeout`, `Agent.Evaluate`, and `Agent.Execute`. Records monotonic Stopwatch intervals per faction turn and per actor. The real `Task` is **observed** using `IsCompleted` on the main thread every 0.5s; no task is cancelled, waited upon, rescheduled, or replaced. Task duration includes scheduler queueing plus execution and is approximate (poll-lag <= about 0.5s while the game is updating); `Agent.Evaluate` duration is a direct method-entry-to-return measurement.

`DiagnoseAIPathfinding` (second-stage setting, also OFF): additionally times `Agent.PostProcessTileScores`, `Agent.PickBehavior`, `Behavior.Collect`, `Behavior.Execute`, `Tactical.MovementSystem.GetPath`, `PathfindingManager.RequestProcess`, `PathfindingProcess.FindPath`, and `FindPathForwardOrBackward`. This level necessarily adds interception overhead to hot loops: it must be compared with the first-stage profile and an OFF baseline. Each actor has capped per-operation aggregate metrics when the method directly exposes an Actor; process-pool time cannot always be attributed to an individual unit.

Aggregate `sum` fields for nested methods and concurrent tasks **must not be added together**: their time intervals overlap. The `max` of a method and `n` calls are useful in diagnosing long single operations versus repeated short work. Large `task-observed` with short `Agent.Evaluate` indicates a wait/scheduling/other-work problem is plausible, not proven. CPU sampling with WPR (ETW) is recommended as an independent check of true CPU hotspots.

Safety properties: `Stopwatch.GetTimestamp` is monotonic; concurrent counters use Interlocked; reporting is bounded (at most five top units, 12 slow operation samples and six 10-second heartbeat lines per faction turn). Task observation is capped at 2048 outstanding references; overflow is counted, not blocked. No profiler Harmony hooks are registered at startup when `DiagnoseAIPerformance` is OFF. Diagnostic failures are handled best-effort and should never change the AI outcome. None of these properties can be fully verified without a game run.

## Decision rule

Do **not** optimize the AI based only on log-gap duration. Use v1.4.7 with other diagnostics OFF to establish a baseline; use v1.4.8 stage 1 at the identical seed; use stage 2 only if stage 1 cannot localize the delay. If native code remains invisible, record one short WPR CPU sample during a slow enemy phase. Compare one mod disabled at a time, always restarting MENACE.

A future code-level optimization is justified only after the top operation, actor, and correlation with the 96x96 slowdown are demonstrated. Such a fix should be delivered in a subsequent reviewed patch; v1.4.8 is not claimed to be that fix.

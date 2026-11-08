# AIPerformance 1.3.1: movement restored, but advance remains too slow

Related: [#2](https://github.com/EmaEli/menace-mode/issues/2),
[#17](https://github.com/EmaEli/menace-mode/issues/17).
[Installation/source evidence](AI_MISSION_PASSIVITY_2026-11-08.md).

## User feedback and scope

After twenty AI turns the user reports no observed civilian attacks and enemies advancing,
but very slowly. This accepts neither overall combat initiative nor civilian-targeting
behavior as fixed. Version 1.3.1 changes multi-order recovery, not civilian targeting.

This follow-up captures/analyzes evidence only. No production code, installed mod,
settings, diagnostic toggle or save was changed. No new deployment or long replay requested.

## Capture and identity

- MENACE closed at capture, 8 November 2026 16:08:31 UTC.
- File: `.tmp/Latest-ai131-feedback-20261108-01.log`, **7,135,989 bytes**.
- SHA256: `2043618a25b8fe974ab01779a816ad3b4e1f43a12e373abd51c2226d49428b0d`.
- Independent remote/local file length and SHA256 match. Script transfer round trip also
  hash-matched; the source log was opened read-only and copied to a unique evidence path.
- Installed manifest/assembly **1.3.1 / 1.3.1.0**; DLL SHA256
  `3248324066a87fbc03d3cc5303a5a4f6ef8816ea7836c2c11e027b4dc329d8d1`, matching installation.
- Recovery enabled and 226 attack / 3 buff positioning profiles simplified at startup.
  No recovery-specific diagnostic records; individual moves cannot be directly attributed.

## What changed in gameplay

- One map, round-start records **1–21**, matching twenty completed AI turns.
- **18 initial hostiles, all 18 change round-start position**; 17 alive/one dead at round 21.
  No survivor has ten or more stationary rounds at the final observation.
- All four heavy teams also advance. Their sampled cumulative round-start displacement
  is only 8–12 tiles over twenty turns (Chebyshev differences, not actual path lengths).
- A machinegun team changes position at round starts **2, 5, 8, 11, 14, 17, 20**.
  After its opening move it usually gains only one tile per three rounds. An autocannon
  likewise mostly steps one tile at round starts 7, 10, 13, 16, 19.
- Across all hostiles after opening, consecutive round-start observations at round 5+
  contain 212 unchanged positions, 72 one-tile, ten two-tile, three three-tile and one
  four-tile displacement. These are sampled displacements, not exact movement costs.
- Explicit order count remains two: 1,030 agent-start records with two zones, plus 18
  opening zero-zone records. Units are not simply unassigned or sleeping.

The previous 1.3.0 capture had 15/16 survivors stationary for 15+ turns. This run supports
a real movement improvement, but map/roster differ; this is not a controlled same-seed
timing/behavior benchmark (the earlier mission had six heavy teams, this one four).

## Attacks and civilians

- **11 accepted enemy attack skill calls**: ten medium-mortar launches by one team,
  all explicitly targeting `player_squad.greifinger`; one sustained-fire rifle skill
  by a conscript at round 18 (tile target, no actor identified in the target description).
- No accepted logged enemy attack directly targets a civilian. Ten civilian actors are present,
  but no civilian appears as a known contact in the recorded hostile round snapshots.
  This cannot prove a civilian-targeting fix or absence of collateral damage.
- Enemy attack evaluation: **6,206 records; 6,191 zero-score/zero-candidate**, eleven
  positive with candidates, four zero with candidates. Infantry combat remains sparse.
- Three additional attack skill calls returned false and are not counted as accepted.
- Route budgets cap 163 hostile ATTACK-EVAL entries, but **no SKILL events are capped**;
  no buffer-overflow/observation-failure warning appears. Counts refer to logged calls,
  not guaranteed projectile impacts or all possible attack evaluations.

## Supported reason for the slow cadence

`AdvanceRecoveryState.Observe` requires two completed idle rounds and resets eligibility
when position changes. A successful recovered step therefore restarts the waiting period.
A small managed reproduction linking the actual 1.3.1 state class confirms recovery only
at rounds **3, 6, 9** when each recovery succeeds and no other actions occur.

For multiple orders, 1.3.1 intentionally leaves native distance scores intact and sets
the same native utility threshold on every no-less-safe tile closer to the allocated
zone. That added utility does not prefer substantial progress over a one-tile improvement.
Native Move records show such one-tile destinations despite some AP remaining. Ranking,
terrain/path cost and native AP clipping need separating before changing that choice.

Together these mechanisms explain the observed pattern plausibly; without optional
recovery tracing they do not directly attribute every live move to the fallback. The
managed reproduction proves the policy's rearm delay, not native target ranking.

The next bounded change should address **continued useful objective advance**, retaining
all order/context, no-useful-shot, safety, AP and arrival guards. Review both rearming
after legitimate progress and preference for meaningful progress among already-computed
candidates. Do not solve it by disabling fast planning, forcing movement through combat,
inventing hidden contacts or adding path/attack searches. No such change is made here.

## Timing, separate from movement speed

- 552 complete activation summaries; pending/dropped counts zero.
- Max recorded Agent.Evaluate **1,953.3 ms**, at round 20 for a conscript. 47 activation
  summaries have an evaluation maximum above 100 ms; only one above 1,000 ms.
- Aggregate per-activation evaluation-maximum median 9.1 ms, 95th percentile 151.5 ms.
  The 200 summaries with explicit hostile template labels have median 62.4 ms and
  95th percentile 346.7 ms; these are not per-call percentiles or whole-round timings.
- Maximum activation wall time 20,146.5 ms includes execution/animation/observation; it
  must not be reported as twenty seconds of AI computation or summed with nested metrics.
- No timeout, NullReferenceException or recovery-disabled message. There are 102 existing
  Structure-to-Actor InvalidCastException mentions; tracked separately by open #96,
  not evidence that this movement fix caused them or that all exceptions are resolved.

## Checks and disposition

- `.tmp/check-ai131-feedback-20261108.py`: PASS for capture/version integrity, roster,
  cadence, attacks/targets, evaluation counts, two-order context and uncapped skill coverage.
- `.tmp/ai131-cadence-checks/Ai131CadenceChecks.csproj`: PASS, links current production
  managed state and reproduces recovery at 3/6/9. No IL2CPP/native gameplay executed.
- Source revision remains `2bc037b`; follow-up is documentation/evidence only. Unrelated
  worktree changes preserved. #2 and #17 remain open: movement no longer stalls mission-wide,
  but advance speed and broad combat initiative are not accepted as solved.
# MEN-002: mission-long diagnostics candidate, 19 October 2026

**Latest finding (20 October, 2.3.1 diagnostic run):** recovery history works, but
there are still zero recovery applications and 29/31 hostiles remain stationary
in rounds 4–10. Twenty actors fail Progress and nine fail TileUtility in rounds
6–9. Native review identifies an invalid pre-path APCost requirement in recovery;
2.3.2 removes only that premature check, leaving actual path/AP validation to Move.
Local/clean-source checks and canonical Windows BuildOnly/install pass; fresh
2.3.2 gameplay acceptance remains pending.
See the final section for evidence and the still-unexplained TileUtility guard.

## Scope

Larger Maps **2.2.2** advances the mission-long route diagnostics and adds v3 native
Move utility observations. No gameplay behavior or setting was changed. Source
changes remain local/uncommitted; HEAD and origin/main were both `6903647` at
packaging. Existing unrelated work was preserved. This is a diagnostic deployment,
not a release or resolution of #2/#57.

Existing `DiagnoseAIRoutes` remains OFF by default and requires restart to install
probes. No dependency on the performance/deep pathfinding flags was added.

- Round-start and actor-end snapshots: position, AP, faction, sleep/turn state,
  suppression/morale, zone rectangle and geometric distance (not path length).
- Per-faction contact identities/known status/TTL and dead-actor count each round.
- Native behavior choices/scores, attack-evaluation scores and sampled candidates.
- Movement decisions/state versus actual movement-completed callbacks.
- Skill-use return value and effect processing, explicitly not proof of hit/kill.
- Independent actor/event/round budgets, consecutive-poll deduplication, explicit
  limit warnings and turn-end counters. Different snapshot phases have separate
  budgets. Real repeated shots are not deduplicated. Bounded memory resets per
  map/round and on mission initialization, including reuse of native pointers.

## Validation and installation

- Managed budget/distance executable: **10,120 checks passed**.
- LargerMaps Python suite: **125 tests, 5 skipped**, including supplied 0.7.17 dump
  signature checks. No native IL2CPP execution is claimed by those tests.
- Selected-package OfflineChecks: **17,794 checks passed**, including actual DLL
  AssemblyVersion/FileVersion. ZIP inventory: **103 files, 2 packages**, verified.
- macOS and Windows Release builds succeeded against the matching 0.7.17 proxies.
  Windows BuildOnly and the full canonical standalone installation passed with
  private SDK **10.0.113**, pinned Jiangyu **1.4.8**, and the game closed. Existing
  CS0169 (LargerMaps) and JIA005 (settings) warnings remain.
- The rollback journal contains the original `Jiangyu.Loader.dll`, `LargerMaps`, and
  `MenaceModSettings`. The installer did not write UserData or modify CustomLeader.
- Windows/.NET **6.0.428**, using the game's Harmony runtime: **28 managed Harmony
  checks passed**. These are not IL2CPP runtime checks.

Private candidate ZIP: `.tmp/MENACE-LargerMaps-2.2.2-candidate-20261019.zip`.
SHA256: `f2bf966fadedbec32d8d3f6337bf1514efd490d4535548915e59d2039a8d03ac`.
Installed Windows DLL: **2.2.2.0**, SHA256
`aa6e9cd08bac469f7b650b2548aa07dea2d4891c0a527ced41b1c3ef9b0c0558`.
Recoverable canonical backup/journal:
`_MENACE_Backups/20261019-213456-9ad681eeefbe42e7a4cfe5804b33bc28`.

## Previous 2.2.1 test run (completed)

At that run, the PC had DiagnoseAIRoutes=ON. Existing settings were retained: 96x96,
SimplifyCombatAIPlanning=ON, PreserveBasicInfantryFirePositioning=OFF,
UseLocalTacticalRouting=ON, both performance diagnostic flags ON. These observations
do not assert that another configuration would behave equivalently.

Restart MENACE and replay the same civilian-defense mission through round 8–10.
Check startup reports **AI ROUTE v2 hooks=12/12**, then retrieve Latest.log before
another launch overwrites it. Check per-actor round coverage and explicit limits,
completed positions and distance reduction, contact/attack scores, chosen actions,
accepted skill uses and application callbacks. Check actual civilian casualties
against gameplay observations; do not infer them from movement or skill acceptance.

These were pre-run instructions, not a report of successful hook installation.
The actual results and remaining verification gates follow.

## Observed 2.2.1 game run: advance stops, not merely a long travel time

Preserved log: `.tmp/men002-round10-20261019.log`. The user reports **not seeing
enemies**, not merely not seeing civilian casualties. Do not infer their visibility
or absence of all attacks from that observation alone.

- Round 9 finishes; round 10 begins. Startup reports **11/12** route hooks: the
  `Behavior.Execute/1` diagnostic failed with `HarmonyException`.
- All **31 enemies occupy exactly the same tiles at the end of rounds 3 and 9**.
  Their geometric zone distances are identical: min **4.5**, median **26.0**, max
  **44.9** tiles. This uses actor position snapshots, not movement callback absence.
- Each round 4–9 has **31 enemy choices, all Idle**, with Move score 0. At round 9
  choice time, **16 units have 100 AP; 15 have 120 AP**. End-turn AP=0 does not mean
  they lacked AP when choosing Idle. All 31 are awake at round-9 actor end.
- The AdvanceTowards zone remains `(47,40,13,17)`. The faction knows one civilian
  by round 4; a known faction contact does not establish a legal shot for each unit.
- Existing enemy MOVE execution records stop in round 3. No enemy skill use was
  observed by the installed skill probes. Do not equate a missing execution hook
  with proof that it caused the AI inactivity.

## Diagnostic source revision v3 (2.2.2 candidate installed)

The missing hook has a concrete observer conflict: route callbacks and deep
profiling callbacks both belonged to `Plugin`, but used different `__state` types
on `Behavior.Execute` (string versus AiPerfState). Harmony keys that storage by
declaring type. Route prefix/postfix now live together in `AIRouteBehaviorProbe`.

- A managed Harmony test against the installed game's `0Harmony.dll` passes on
  Windows/.NET **6.0.428**, in both installation orders and with route logging
  ON/OFF. **28 checks passed**. This is not IL2CPP.
- New opt-in probes observe native `Move.OnEvaluate`, `OnNewTurn`, and `HasUtility`
  results; no additional evaluations or path searches are requested. Banner is
  **AI ROUTE v3 hooks=15/15** when all install successfully.
- Choice logs include movement flags, below-utility counter/last-update marker,
  destination count and stored target score components even when Idle wins.
  Base Behavior proxies are safely rewrapped as Move before reading derived fields.
  Stored target fields are explicitly labelled potentially stale.
- Hook failures now include the innermost exception message rather than just its
  type. Diagnostic budgets and OFF-by-default behavior remain unchanged.

Native 0.7.17 `Move.OnEvaluate` (RVA `0x7D0E90`) consults utility and the counter
at offsets `0x38/0x3C`; `HasUtility` (RVA `0x7D0BB0`) compares tile utility against
the native threshold. These identify what to observe, **not the proven cause**.
Simplified skill positioning remains a hypothesis, not grounds for changing it.

Validation of v3: macOS and Windows Release builds against 0.7.17 proxies passed;
**125 Python tests, 5 skipped**; **10,120 budget/distance checks**; **17,782
selected-package OfflineChecks**; and **28 Windows managed Harmony checks**.
The first macOS Harmony run failed inside MonoMod's IL detour while patching the
baseline profiler hook, before the candidate route callback ran; it was not counted
as a pass. The equivalent current-source test passed on the target Windows runtime.

The **2.2.2** BuildKit passed integrity verification, Windows BuildOnly, and the
canonical install. The installer backed up the previous loader and both package
trees; UserData/settings were not edited.

## Observed v3 game run

Captured log: `.tmp/men002-v3-game-run-20261019.log` (**8,001 lines**), SHA256
`e60ea3ccb1ac1d6877f98c00ca8d3bc5acd0762e11279f4820a7846365a33b00`.

- Startup reported **AI ROUTE v3 hooks=15/15**. No hook/diagnostic exceptions or
  buffer-limit warnings occurred. The 6 explicit event-limit warnings were for
  civilian workers in rounds 1–2; enemy entries were not limited.
- Round-start snapshots cover rounds **1–10**. The enemy faction had 31 living
  actors throughout. All 31 changed tile between round 1 and 3; only 1 changed
  between rounds 3 and 4; none changed between rounds 4 and 9 or 9 and 10.
- The `CHOICE` probe records `PickBehavior` polls, not committed actions: agents
  can be polled again before `Agent.Execute` finishes. Unique enemies with at least
  one positive `Move.OnEvaluate` result were **31, 29, 15, then 0** in rounds 1–4.
  The `Move.OnExecute` probe recorded **31, 30, 1, then 0** calls. Thus round 3 is
  the transition: 15 enemies still had a positive Move evaluation, but only one
  reached Move execution. From round 4 onward none did. Final `Agent.Execute`
  snapshots show Idle active for all 31 enemies in rounds 1–9; in rounds 1–3 this
  follows earlier movement iterations, so that field alone is not an action count.
  Round 10 had started, but no choices from that round were captured. In rounds 4,
  8 and 9, all 31 were awake, not moving, and had 100 or 120 AP when Idle won.
- In rounds 4–5 and 7–9, every enemy `Move.OnEvaluate` callback returned **0**
  (31 callbacks per round; 30 in round 6). Every captured `Move.HasUtility` result
  was false, including rounds 1–3 when Move did execute, so that boolean by itself
  does not explain the stop. The below-utility counter rose from 1 in round 1 to 9
  in round 9; this is correlated with the score collapse, but not yet proven to be
  its cause. The mission-zone distance median remained 26 tiles from round 4 through
  round 9.
- Although all 15 hooks installed and no observer exception was logged, the
  `Behavior.Execute` observer emitted **zero** records. Use the Agent-level final
  behavior and the Move-specific `OnExecute` records as the runtime evidence; the
  generic Behavior observer has not been validated against IL2CPP calls.
- This run had `SimplifyCombatAIPlanning=ON`,
  `PreserveBasicInfantryFirePositioning=OFF`, and `UseLocalTacticalRouting=ON`
  (runtime `NearTileLimit=12`, `CullTilesDistances=15`, originally 64 and 20).
  All are confounders: this run cannot tell whether the local candidate limits or
  simplified planning contributed to the evaluation collapse.

The v3 run reproduces the late-round inactivity and narrows the decisive transition
to Move evaluation/re-evaluation before execution. The false `HasUtility` result
alone is ruled out as a sufficient explanation; the exact native scoring gate is
still unknown.

## Follow-up A/B run: local tactical routing OFF

Captured log: `.tmp/men002-v4-toggle-off-20261020.log` (**11,409 lines**), SHA256
`b5f97aa4ee391daa73b837a81562804dd3b4b2dbfa09482cfb7db839a9d053bb`.
The user replayed from the same mission start with `UseLocalTacticalRouting` OFF;
the 31 enemy starting template/tile pairs and their round-1 and round-2 positions
match the earlier run. Simplified combat planning remained active.

- Hooks again installed **15/15**. Through rounds 4–6, the inactivity persisted:
  Move-positive actors were **31, 31, 15, 0, 0, 0** in rounds 1–6, and
  `Move.OnExecute` counts were **31, 31, 1, 0, 0, 0**. The round-3/4 score collapse
  is therefore not fixed by disabling local routing.
- Unlike the prior ON run, Move activity returned in rounds 7–8: **31 positive
  actors / 31 executions** in round 7 and **28 / 10** in round 8. It stopped again
  in rounds 9–10. Round-start positions confirm **31/31** enemy tiles changed
  between rounds 7 and 8, all closer geometrically to AdvanceTowards; **10/31**
  moved closer again between rounds 8 and 9. No enemy tile changed between rounds
  9 and 10. For example, a Stinger moved from `(10,21)` to `(15,22)`, reducing its
  zone distance from **41.6** to **36.7**. This is actual progress, not just a
  behavior-choice poll.
- The destination list observed at Move execution expanded substantially: median
  `m_Destinations` count was **18–19** in the ON run's moving rounds 1–2, versus
  **72–73** in OFF rounds 1–2; it was **72** in OFF round 7. The candidate-count
  change is confirmed. Attribution of the later movement to these limits is
  **confounded by different known-contact lifetimes**, as established below.
- The run still had simplified combat planning enabled (226 attack and 3 buff
  skills adjusted). AI profiler records showed no pending/dropped tasks, but peak
  actor-activation wall time rose from **1.74 s** in the ON run to **4.85 s** in
  this run. Disabling local routing has a measurable cost; avoid assuming OFF is
  a suitable permanent setting without a broader performance check.
- There were **107** explicit per-actor event-limit warnings (93 enemy STATE,
  14 civilian events), but no buffer overflow and no enemy Move/Execute limit
  warnings. The generic `Behavior.Execute` observer still emitted zero records.

Corrected conclusion: matching initial positions and unchanged other settings do
not establish matching contact history. The OFF run's round-7/8 movement coincides
with expiry of the known civilian contact; the ON run retains that contact.
These runs demonstrate different candidate counts and activation costs, but do
**not isolate local routing as the cause of the later movement difference**.
The following native investigation supersedes the previous stronger attribution.

## Order-to-action investigation: the faction-wide known-contact gate

### Native hierarchy actually established

Reviewed MENACE **v0.7.17+22362**, original `GameAssembly.dll` SHA256
`871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`,
against the complete IL2CPP dump and annotated x64 disassembly:

1. `AIFaction.Think` calls `StrategyData.Update` (call at RVA `0x7B0436`).
   `Update` (`0x7D72F0`) clears the faction's operational-zone list and obtains
   zones via unfinished/non-failed objectives' `UpdateAI`, counts actors inside,
   then updates priorities and target-designator skill data.
2. `Agent.GetZones` (`0x7B29B0`) directly returns its faction's `OperationalZones`
   (agent offset `0x10`, faction offset `0x70`). This is a shared zone/order
   list, not evidence of a private multi-turn plan or coordinated group maneuver.
3. `ConsiderZones.IsValid` (`0x7DBB20`) checks movement availability, deployment
   phase and positive `RoleData.ZoneImportanceWeight`. Zone presence alone does
   not establish that a movement incentive is applied.
4. `ConsiderZones.Evaluate` (`0x7DB600`) calls **`HasKnownOpponent`**, not
   `HasKnownOpponentIgnoreCivilians`, and skips the `AdvanceTowards` distance
   contribution when it is true (branch at `0x7DB9E8`).
5. `ConsiderZones.PostProcess` (`0x7DBC00`) also skips the `AdvanceTowards`
   inside-zone utility contribution with known opponents (branch `0x7DBEF1`).
   Do not generalize this to `CaptureAndHold`, which has separate logic.
6. `HasKnownOpponent` (`0x7AE980`) accepts any opponent with **TTL >= 0**, without
   filtering civilians or requiring a legal shot by the evaluating actor. The
   distinct `HasKnownOpponentIgnoreCivilians` (`0x7AE840`) filters faction index 3,
   but is **not** the method used at these two zone-scoring call sites.

Thus an order may remain visibly assigned while its scoring incentive is disabled
for the entire faction. The examined strategy/zone methods do not contain a
multi-turn no-progress recovery mechanism. This is a bounded finding, not proof
that no such logic exists anywhere else in the game.

### Existing logs match the gate transitions

Only native faction **7**, **31 alien enemies**, is considered here; this is not
evidence for a Rogue/Pirate operational director (tracked separately in #17).
Every listed round-start opponent snapshot is complete, including unknown player
entries; no player entry is marked known in these snapshots.

| Run / round | Known civilian at round start | Move-positive actors | Move callbacks |
| --- | --- | ---: | ---: |
| ON / 3 | None; contact first observed during the round | 15 | 1 |
| ON / 4–9 | Same worker, TTL 2 at every snapshot | 0 each | 0 each |
| OFF / 3 | None; contact first observed during the round | 15 | 1 |
| OFF / 4–6 | Worker TTL 2, 1, 0 respectively | 0 each | 0 each |
| OFF / 7 | Previous worker TTL -1, no known contacts | 31 | 31 |
| OFF / 8 | Previous worker TTL -2; another contact during round | 28 | 10 |
| OFF / 9–10 | Another worker, TTL 2 | 0 each | 0 each |

In **both round-3 runs**, all 15 positive actors and the single Move callback occur
before the first enemy STATE with `knownOpponents=1`; after that observation,
there are **0 positive evaluations / 0 Move callbacks**. The first positive-contact
STATE is at baseline line **3030** / OFF line **3020**. In OFF round 8, all
**28 positive actors / 10 callbacks** precede the same observed transition at
line **7807**, with **0 / 0** afterward. These STATE boundaries are observations,
not precise sighting timestamps; no claim is made about every concurrent poll.

### Remaining cause and next controlled check

The native gate is established by actual instructions, and its transitions fit
the stop/resume pattern. This is stronger evidence than the earlier utility-counter
correlation, but is **not a tested gameplay fix or proof of the sole cause**.
With `SimplifyCombatAIPlanning` active in both runs, eligible attack profiles have
`InfluencesPositioning=false`: ordinary combat repositioning may fail to take over
when the advance incentive is removed. That interaction remains unisolated.

Do not globally falsify `HasKnownOpponent`, remove civilian opponents or force
movement: civilians are real targets in this alien mission, and changes could
break targeting, threat assessment, defense orders or fog of war. Raising the zone
weight alone cannot repair a branch that skips the zone contribution entirely.

The next narrow in-game check remains the same pre-mission save with local routing
OFF and **only `SimplifyCombatAIPlanning` changed to OFF**, through the first
contact/round 4. Compare contact timing as well as movement, attack positioning
and activation time; stop on abnormal slowdown. No settings or installed game
files were changed in this offline investigation, and no new run was performed.

### Reproducibility and checks

- Added `mods/LargerMaps/tools/analyze_ai_orders.py`: pass a captured log and
  `--faction 7`. It reports round-start contact/TTL coverage and positive polls /
  Move callbacks on either side of the first observed positive-contact STATE.
  It explicitly warns about event limits, missing snapshots and repeated map/round
  summaries; it does not infer causality or completed movement from callbacks.
- **7 synthetic parser regressions** passed; the parser was also run on both
  private v3/v4 logs and reproduced the table and within-round boundaries above.
- **4 version-locked native byte checks** passed with `MENACE_GAME_ASSEMBLY`
  pointing to the original 0.7.17 DLL. They verify shared zones, the strategy update
  call, both known-opponent gate sites and the TTL/civilian-filter distinction.
  These are static checks, not native execution or proof of AI behavior by themselves.
- Existing route-diagnostic tests: **6 passed, no skips**, with `MENACE_DUMP_CS`
  pointing to the full 0.7.17 dump. No new dependency or runtime mod code was added.

## Planning OFF mission: combat recovers, distant initiative remains incomplete

### Capture and configuration

Retrieved after the user closed the game on **20 October 2026**. Private capture:
`.tmp/men002-planning-off-20261020.log`, **21,830 lines**, raw SHA256
`9c48b8e5b6ee400cbdefb9454ec2ae691661a6bc3258f1fcb09e208220bdaba4`.
One tactical map, round-start snapshots 1–20; round 20 is partial, with no next
snapshot or established mission outcome. Larger Maps 2.2.2, 96x96 backend,
route hooks 15/15 and performance hooks 13 with deep operations active.

Read only an explicit settings allowlist after exit: map size 96;
`SimplifyCombatAIPlanning=false`, `PreserveBasicInfantryFirePositioning=false`,
`UseLocalTacticalRouting=false`; all three AI diagnostic flags true. Settings
last-write UTC remains **06:10:06.4341592**, matching the earlier requested edit.
There is no simplified-planning setup line in this session, unlike the ON run's
226 attack / 3 buff modifications. No game setting or DLL was changed in this
follow-up. This capture does not establish that editing JSON hot-reloads a mission.

The initial 31 enemy template/tile pairs match the previous routing-OFF run at
round starts 1, 2 and 3. Composition: 15 Spiderlings, 5 young Warriors,
7 Stingers and 4 Dragonflies, native faction 7. Contact histories subsequently
differ, so later rounds are not a perfectly controlled causal/performance A/B.

### Observed movement and attacks

| Round(s) | Unique enemies with Move callback | Actual tile changes to next round |
| --- | ---: | ---: |
| 3 | 4 | 4/31 |
| 4–10 | 2 each, the same two Dragonflies | 2/31 each |
| 11 | 7 | 7/31 |
| 12 | 19 | 19/31 |
| 13 | 30 | 29/30 surviving common actors |
| 14 | 28 | 26/26 surviving common actors |

The known civilian remains TTL 2 at every round-start snapshot from 4 through 12.
The first observed positive-contact STATE in round 3 is followed by **3 positive
Move actors / 3 callbacks**, versus **0/0** in the previous ON run. Objective
incentives are still gated, but a combat-related movement contribution can now
exist. This agrees with the user reporting that combat recovered.

It is not faction-wide recovery from the start: **29 actors do not change tile
between round starts 4 and 11**. Representative round-4 Warrior, Spiderling and
Stinger choices still have Move=0 and attack=0 despite 100/120 AP and destination
candidates. Two other Dragonflies also remain stationary. Exact rejection reasons
for those candidate positions are not logged; a short-range/visibility/reachability
limitation is a hypothesis, not a demonstrated explanation. Player contacts first
appear as known in the faction round-start summary at round 13; this is not an
exact timestamp for first sighting during round 12.

Enemy-only skill observations, selected by the **event actor**, not a target's
template or faction embedded later in the line:

- **29** `active.alien_jump_attack`, first accepted in round 15.
- **4** `active.fire_alien_needle_spitter_dragonfly`, first in round 10.
- **4** `active.fire_alien_needle_spitter_stinger`, first in round 16.
- All **37** uses are accepted and have matching per-skill APPLIED counts.
  These are effect callbacks, not proof of 37 hits, kills or civilian casualties.
- No Warrior stab use was observed; movement alone does not validate its attack.

There are **202** explicit route limits, of which **112** are enemy STATE limits.
No enemy Move/Skill limit or route buffer overflow was reported. Cross-round
positions therefore corroborate movement independently of repeated choice polls.
Generic Behavior.Execute still has no validated observations.

### Performance: measurable cost, not the previous 38-second evaluation

Largest observed enemy `Agent.Evaluate`: **2,361.4 ms**, a Spiderling in round 15.
Stinger evaluations reach **1,340.3 ms** in round 13; the most expensive recorded
InflictDamage collection is **859.2 ms**, also a Stinger. Thus costs are not confined
to melee. A civilian evaluation reaches 2,365.2 ms in round 19.

Largest enemy-labelled activation wall interval is **10,657.2 ms** (round 15),
then 8,216.2 ms (round 16). These intervals include waiting/execution/animation and
potential pause time; they are **not CPU or attack-search duration**. All reported
summaries have pending=0 and droppedTasks=0. The round-15 heartbeat temporarily
observes one pending task; the completed summary does not show a stuck task.

Profiler boundaries also produce 21 near-empty, unknown-actor summaries in early
31-activation enemy rounds; other intervals include several agents' worker metrics.
Do not use their median as typical unit decision latency or assign every worker
cost to the interval's actor label. Compare measured per-agent evaluations instead.
This run does not reproduce #57's 37.99-second evaluation, but different opponents,
contact histories and active deep profiling prevent declaring #57 solved.

### Alternatives and recommended next step

1. **Preserve only melee positioning:** feasible as a profile exemption, but not
   sufficient for this evidence. The first recovered movement is ranged Dragonfly
   movement, and Stinger ranged attacks recover later. Globally simplified ranged
   profiles can still lose essential combat movement.
2. **Exclude this faction:** possible in principle, but current code mutates
   shared `SkillTemplate.AIConfig` objects at scene load, not faction-owned state.
   A true actor/faction-specific policy needs a different verified interception
   point or isolated configuration; flipping shared flags during concurrent worker
   evaluation is unsafe. Skill exemptions have a smaller implementation surface.
3. **Preserve the four observed alien attack profiles:** a bounded diagnostic
   candidate using the existing native-pointer preservation pattern (currently
   five explicit infantry profiles). It covers these 31 actors, not all aliens;
   aliases must be gathered before any writes and original flags retained. Since
   all enemy attack profiles in this battle would remain native, do not promise
   lower alien AI cost than the current OFF run. Other factions remain unvalidated.
4. **Force objective movement:** not recommended. It can override useful fire,
   cover, AP constraints or defensive orders and does not repair attack positioning.
   A narrower recovery is worth investigating because native planning alone leaves
   29 actors stalled: only an actual `AdvanceTowards` order, sustained no-progress,
   no usable attack/useful combat move, and a legal reachable destination making
   progress; use normal utility competition, not a forced Execute or teleport.
   Keep CaptureAndHold/defense and legitimate contact information untouched.

**Recommendation:** retain the user's current OFF setting for now; do not deploy
an untested faction/melee exception or enable local routing as another variable.
Separate restoring essential combat positioning from fixing residual initiative.
Next isolate why native combat movement stays zero for the 29 stalled actors;
then evaluate a guarded AdvanceTowards fallback against those exact samples.
Any profile-selective experiment must include both ranged and melee cases, plus
a same-save timing check for #57. Optimize measured search work rather than
removing all movement incentives. No fallback, whitelist or gameplay patch has
been implemented or tested by this analysis.

Supporting export: v0.7.16 SkillTemplate/EntityTemplate data maps the four observed
units to the four skills above, with native positioning enabled (range 1 for
jump/stab, 7 for the two needle attacks). Those older exported values are **not**
asserted as live 0.7.17 template values. Current 0.7.17 dump metadata independently
documents `InfluencesPositioning` as the flag for moving into ability-use positions.

Validation for this follow-up: re-ran **7 analyzer + 4 native byte-contract +
6 route-diagnostic tests (17 passed, no skips)** with the 0.7.17 artifacts.
Additional assertions on this captured log verified 37 accepted/APPLIED counts,
the same 2 movers and 29 stationary actor IDs across rounds 4–10, 19 round-12
movers and 29/30 round-13 surviving actors changing tile. `git diff --check`
passed. Documentation only; no new gameplay build, deployment or test replay.

## Conditional advance recovery candidate — 20 October 2026

Following approval to proceed, Larger Maps **2.3.0** adds the
`RecoverStalledAdvance` setting (**ON by default**, restart required; it remains
user-toggleable). This is a gameplay
candidate, not evidence that inactivity or turn cost has been fixed.

### Intervention and bounds

- Hook `ConsiderZones.PostProcess` after native attack/tile scoring and the main
  score transform, before final behavior scoring and `PickBehavior`. Restore only
  the missing single-advance-zone distance penalty and inside-zone utility using
  live native weights, role weights, priority and native rectangle geometry.
- Track actual positions and final native choice polls across round boundaries.
  Require two consecutive idle-only rounds at the same position/order. Repeated
  polls do not count as rounds; any positive non-Idle choice/score invalidates the
  evidence. Changed agent/order, movement, skipped/backward round, map/mission
  reset also invalidate it. State is synchronized and bounded to 2,048 actors.
- Current evaluated tile utilities and collected attack candidates must also be
  absent. Positive action scores visible at this phase veto recovery. Final
  behavior scoring has not yet occurred: normal action competition still runs;
  this is not a guarantee about every later non-attack skill evaluation.
- Only hostile faction **types** 5–9 (not faction indices) are eligible; allied
  status and avoidance roles veto recovery. Exactly one pure `AdvanceTowards`
  zone is required. Mixed/defensive orders, arrival, garrisons, sleeping/deployment,
  suppression, morale loss, spent AP and already-moving/moved actors are excluded.
- Require the current tile and at least one closer, AP-affordable native candidate.
  Validate all edits before writing; restore original distance/utility fields if
  writing fails. Runtime exceptions disable recovery for the session. No attack,
  skill-usability, LoS or path search is invoked by this patch.
- The only score writes are `DistanceScore` and inside-zone `UtilityScore`.
  Contacts, safety, attack utility, AP, paths, orders, Move state and behavior
  scores are untouched. Normal native Move can still reject all destinations;
  preservation of native safety scoring is not proof of safe/aggressive gameplay.
- Install the hooks only for fingerprint-reviewed **0.7.17+22362**, with the large
  map backend active, and reject actual maps at/below 64x64. Other supported game
  builds retain native behavior. No omniscient centre target or patrol is added.
  Normal logging is one activation/failure line; detailed recovery entries use
  the existing opt-in `DiagnoseAIRoutes` budget and are not movement evidence.

### Validation and remaining acceptance

The C# harness links the production state/runtime sources unchanged against managed
stand-ins. It covers faction/order exclusions, cross-round evidence, poll bursts,
concurrent actors, bounds, invalid numbers, combat utility/candidates, AP/progress,
native-weight arithmetic, geometry failure and partial-write rollback. These are
not IL2CPP or Harmony execution tests. **12,167** assertions passed; the retained
route budget/distance runner also passed **10,120** assertions. Native byte contracts pin the restored
coefficients, utility threshold and hook-phase call order to the reviewed binary.

The local net6.0 game-proxy build passed (one pre-existing unused-field warning).
Version/artifact checks confirmed manifest **2.3.0**, assembly/file **2.3.0.0**;
focused shared OfflineChecks passed **17,794** assertions. LargerMaps Python checks
ran **143 tests: 139 passed, 4 skipped** with both native-binary environment variables and the
0.7.17 dump supplied; the seven native order/weight/phase contracts ran without skip.
The four remaining skips require existing authoring/export fixtures (including an
older reference binary); they are not skipped recovery checks. The broader shared
Python suite ran 139 tests with two failures outside this change: the unrelated
Overwatch manifest/migration-row version mismatch and a camera RollChance source
assertion against the separately edited camera. Those modules were not changed
or rebuilt for this candidate; do not label the entire shared suite green.

After changing the recovery toggle to **ON by default**, the canonical builder
prepared a standalone package (LargerMaps + unchanged
MenaceModSettings only):
`.tmp/MENACE-LargerMaps-2.3.0-MEN002-candidate-20261020-r3-default-on.zip`.
ZIP integrity, a second build from clean extracted sources against 0.7.17 proxies,
and real DLL metadata checks all passed: **107 inventoried files, 2 packages**.
SHA256: `432acfd14936fd9a85ff59d9fe2046645d1881f9b9c49dd5e4503784b3347216`.
This r3 ZIP supersedes r2 and the earlier candidate. Clean verification initially attempted
to inspect unbundled modules; fixed `verify_kit.py` to pass its detected package
selection to OfflineChecks, with an executable mocked-command regression.
All **19** distribution tests pass; the Windows installer itself was not changed.

### 2.3.0 gameplay result and 2.3.1 diagnostic build — 20 October 2026

- The 2.3.0 run log is 5,799,651 bytes, SHA256
  `73225bb0d06217eee226e0da4200f2459778abb07a646cf033970c95b949707b`.
  It reports the recovery enabled, all 15 AI-route hooks installed, and no recovery
  disable, exception, or `ADVANCE-RECOVERY` event. This disproves a successful
  recovery in that run; it does not identify which conservative guard rejected it.
- On the 96x96 mission, 31 hostile actors were tracked. Their positions changed
  between rounds 1–3; during rounds 4–10, only two actors moved per round and 29
  remained at their previous tiles. Movement then increased as the mission state
  changed. Treat the later activity as a different contact/round condition, not
  evidence that recovery worked.
- LargerMaps 2.3.1 adds opt-in, budgeted `ADVANCE-CHECK` rejection-stage and
  `ADVANCE-CHOICE` idle-history diagnostics. It does not relax eligibility or
  alter native choices. Managed tests exercise each reported rejection stage.
- Local validation: 144 LargerMaps Python tests passed (4 optional skips), 12,232
  recovery policy checks passed, 10,120 route-budget checks passed, and the
  17,794-assertion offline C# suite passed. Seven version-locked native-byte checks,
  19 installer distribution tests, ZIP inventory, and a clean extracted-source
  build also passed. These are not gameplay tests.
- Windows PowerShell 5.1 BuildOnly and canonical install of 2.3.1 passed with the
  pinned private SDK 10.0.113. LargerMaps manifest/assembly 2.3.1/2.3.1.0 and
  MenaceModSettings 0.6.1/1.0.0.0 were verified against actual DLLs. A single
  existing warning in each build reports an unused field; there were no errors.
  Mods and UserData were unchanged by BuildOnly; after installation, unrelated
  Mods and all UserData were verified unchanged. Installed DLL hashes match the
  canonical staged build. The former 2.3.0 package is backed up outside Mods at
  `Menace\_MENACE_Backups\20261020-215420-eac8e36aa1d247b595b6768bbb793682`.
- The game has not been launched since the 2.3.1 installation. No new startup log,
  recovery rejection reason, movement result, or gameplay acceptance exists yet.

Next: ask the user to launch the same 96x96 save/seed with the existing settings
held fixed (`RecoverStalledAdvance` ON; `DiagnoseAIRoutes` enabled). Let the stalled
phase occur, then close the game and retrieve the fresh `Latest.log` to identify
the first rejection stage. Only after that can an evidence-based policy change and
the OFF/ON A/B acceptance be considered. Keep #2/#57 and shared #34 open.

### 2.3.1 diagnostic gameplay and pre-path cost bug — 20 October 2026

User completed the requested run; Menace.exe was closed when the log was retrieved.
Private artifact: `.tmp/men002-231-run.t6mowY/Latest.log`, 5,339,928 bytes,
SHA256 `aff75773a34f84c697435a235299775f0dcab6ae6164d0f1225e7b21d4a55b51`.
Startup confirms 0.7.17+22362, LargerMaps 2.3.1, 96x96, route hooks 15/15 and
conditional recovery enabled. No recovery/route disable or runtime error was found;
the single `ReportException` string is a loader debug patch-install message, not
an exception. Round-start snapshots cover rounds 1–14; action events cover 1–13.

- Exact ADVANCE-CHECK records: 944 (not substring matches inside COUNTS summaries):
  388 History, 215 Progress, 197 TileUtility and 144 Orders (civilians).
  There are 698 ADVANCE-CHOICE records and **zero ADVANCE-RECOVERY** records.
- In rounds 6–9, 20 hostile actors per round reach Progress and nine TileUtility;
  their histories have reached two idle rounds. The proposed history-hook failure
  is ruled out for those evaluations. Neither rejection identifies its component
  values; do not infer a particular positive utility source from TileUtility alone.
- Enemy Move callbacks: 31/31 in rounds 1/2; four in round 3; two each in 4–10;
  four in 11, 12 in 12, 22 in 13. Round-start tile changes confirm two movers and
  29 stationary actors each transition from 4 to 5 through 10 to 11; later changes
  rise to four (11 to 12), 12 (12 to 13), 17 (13 to 14). Callback counts are not
  completed arrivals. Five enemy skill uses reach effect callbacks (1/1/3 in
  rounds 10/12/13). No recovery is responsible for the later activity.
- There are 43 event-limit warnings, including 31 enemy STATE limits in round 13;
  the MOVE/EXECUTE limits are civilian-only. No recovery-event limit or buffer
  overflow is recorded. Missing limited events must not be interpreted as zero.

**Code error identified:** recovery runs at ConsiderZones.PostProcess, before
native Move.OnEvaluate. Yet its Progress guard required an already-populated,
positive, affordable TileScore.APCost. Native 0.7.17 TileScore.Reset zeros this
field (`0x7D7F61`); Move treats zero as not processed (`0x7D198A`), then calls
FindPathForwardOrBackward (`0x7D1CDC`) and ClipPathToCost (`0x7D1E02`) before
assigning the clipped cost (`0x7D1E30`). Thus the managed tests' pre-populated cost
was an incorrect model of this hook phase. The log does not directly expose the
per-tile costs/distances, so do not claim every Progress rejection has this cause.

**2.3.2 intervention:** require only a geometrically closer existing tile at this
pre-path phase. Do not inspect or write cached destination costs/paths. Native
Move still decides reachability, affordable movement and action selection. Retain
actor AP>0, two idle rounds and every combat/order/state/utility exclusion. In
particular, do not relax TileUtility without more evidence. This restores an
incentive, not a forced Move or proof of an affordable route.

Validation: change the managed fixture to the native zero-APCost initial state;
it fails against 2.3.1 at the first eligible hostile case, then passes with the
correction. **12,237** managed assertions, **eight** version-pinned native-byte
tests and six wiring checks pass. The full LargerMaps Python suite runs **146**
tests (**142 pass, four optional authoring/export skips**); the new native checks
are not skipped. Route-budget checks pass **10,120** assertions; focused offline
C# checks pass **17,794**; all **19** distribution tests pass. A game-proxy build
and clean extracted-source build both pass. The initial local build invocation
omitted MelonLoaderDir and failed on unresolved references; rerunning with the
existing loader cache resolved it without code/dependency changes.

Candidate: `.tmp/MENACE-LargerMaps-2.3.2-MEN002-native-cost-20261020.zip`,
SHA256 `0e67b66c4ffccb42978cb5675ad6b6a4356385454cf2f45e4ba1d3f227498580`,
107 inventoried files / two packages. On Windows PowerShell 5.1, canonical
Standalone BuildOnly and installation pass against pinned 0.7.17 with the private
SDK 10.0.113. Builds report JIA005 (settings injection constructors) and CS0169
(unused LargerMaps field), zero errors. LargerMaps manifest/assembly/file are
2.3.2/2.3.2.0/2.3.2.0; settings manifest/assembly are 0.6.1/1.0.0.0. Exact live DLL
hashes match staging. BuildOnly leaves Mods/UserData unchanged; installation
preserves unrelated Mods and all UserData. The 2.3.1 original and rollback journal
are verified at `Menace\_MENACE_Backups\20261020-221322-8a8ccb4c56184ceaa91ef4b160a29b40`.
Logs: `C:\Users\madwo\_MENACE_Staging\LargerMaps-2.3.2-MEN002-native-cost-20261020\`
(`build-only.log`, `install.log`) and `%TEMP%\MENACE-install.log`.

### 2.3.2 fresh gameplay run — 20 October 2026

Fresh `Latest.log`: `.tmp/men002-232-run-20261020/Latest.log`, 5,353,149 bytes,
SHA256 `d9bea0b52edece3d5de87e9f187efee81fc038330ed23458645e237adf39a14d`.
Startup confirms MENACE 0.7.17+22362, LargerMaps 2.3.2, 96x96, conditional
advance recovery enabled and AI route hooks 15/15. No recovery disable, route
observer failure or buffer overflow was logged.

The initial scenario matches 2.3.1: all 31 hostiles have the same template/tile
positions in round-start snapshots for rounds 1–4. In 2.3.1, only 2/31 changed
tile in each transition from rounds 4→5 through 10→11. In 2.3.2, 35 recoveries
were logged (12 in round 5, eight in round 6, ten in round 8, five in round 9).
At the next round-start, 26/35 recovered actors had changed tile; all 26 moves
reduced their logged geometric distance to the AdvanceTowards zone. Per recovery
round: 11/12, 3/8, 8/10 and 4/5 moved closer on the next snapshot.

Overall hostile tile changes were 2/31 (4→5), 13/31 (5→6), 5/31 (6→7), 2/31
(7→8), 13/31 (8→9), 26/31 (9→10), 31/31 (10→11) and 27/31 (11→12). Broader
movement from round 9 coincides with more known contacts and is not attributed
solely to recovery. No `Progress` rejection remains in 2.3.2, but 83 first-rejection
`TileUtility` records remain across rounds 5–10 (event counts, not unique actors);
the combat-utility guard was not relaxed. Round 7 had no recovery applications.

The run records 19 accepted hostile attack-skill calls; this does not prove a
hit/damage/kill. There are 90 route-limit warnings in rounds 1, 2 and 9–11, but
no recovery-event limit or buffer overflow; all 31 hostile round-start snapshots
remain present through round 12. The log does not yield a comparable enemy-turn
timing metric.

### 2.3.2 recovery OFF control — 20 October 2026

Fresh OFF log: `.tmp/men002-232-off-run-20261020/Latest.log`, 7,483,678 bytes,
SHA256 `8c6253cdd3172374cf6478544f9727c87b9d8b03c043dfa08eac3def36d4813c`.
The game was closed before and after the run. The authoritative setting was
verified `RecoverStalledAdvance=false` before launch and after exit;
`DiagnoseAIRoutes=true` remained enabled. Route hooks were 15/15, with no observer
failures. There were 216 route-limit warnings across the longer OFF log. The 31
hostile round-start snapshots are complete through round 10; 30 remain in rounds
11–13 after one death, and 20 remain in round 14 after further deaths.

The round-start positions of all 31 hostiles exactly match the ON run through
round 4. Counts below are **actual tile changes between round-start snapshots**,
not Move polls or execution callbacks:

| Transition | Recovery ON | Recovery OFF |
| --- | ---: | ---: |
| 3→4 | 4/31 | 4/31 |
| 4→5 | 2/31 | 2/31 |
| 5→6 | 13/31 | 2/31 |
| 6→7 | 5/31 | 2/31 |
| 7→8 | 2/31 | 2/31 |
| 8→9 | 13/31 | 28/31 |
| 9→10 | 26/31 | 29/31 |
| 10→11 | 31/31 | 29/30 |
| 11→12 | 27/31 | 28/30 |

With recovery OFF, no `ADVANCE-CHECK`, `ADVANCE-CHOICE`, or
`ADVANCE-RECOVERY` records were emitted, as expected because the feature's patch
is not installed when its toggle is false. The early control supports a
contribution from recovery in rounds 5–7: ON had more actual movement in 5→6 and
6→7, while both runs remained nearly stationary immediately after round 3. The
later OFF movement begins as contacts increase and is not attributable to
recovery. This is evidence of a limited benefit, not proof that recovery resolves
the overall stall; the runs diverge after the matched initial rounds.

After preserving the log, the original settings backup was restored exactly:
`RecoverStalledAdvance` is again absent and therefore uses its default ON value;
`DiagnoseAIRoutes=true` and legacy `ModSettings.json` are unchanged. No comparable
enemy-turn timing was collected, so #57 remains open. MEN-002 was subsequently
closed with the user's gameplay acceptance; shared packaging issue #34 remains separate.

**Assessment:** the user judged the tested gameplay reasonable. The controlled
ON/OFF run supports a limited recovery benefit in rounds 5–7, not a complete cure
for every post-round-3 stall. Accept this bounded gameplay improvement for MEN-002;
no exhaustive faction/order/map/reload QA matrix is required for this scope. Keep
the distinct severe slowdown investigation #57 and the shared packaging/release
gate #34 open for their own work; neither blocks this gameplay acceptance.
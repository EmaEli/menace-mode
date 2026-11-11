# AIPerformance 1.11.0: pirate mobilization failure (11 November 2026)

## Player verdict overrides action-count optimism

The player reports a new pirate battle with roughly 40 enemies versus eight
player units: enemies stayed near map edges, did not regroup or push observed
player positions, and boarding commandos were passive. Attacks seen in the
earlier log occurred because the player approached the enemies. Turns remained
reasonable, but enemies spent considerable time evaluating ineffective actions.

**Reject coordinated-combat/initiative acceptance.** Movement and accepted skill
counts establish activity, not initiative, pressure, platoons or good decisions.
The preceding optimistic reading of those counts must not be used as acceptance
of #17, #13 or #2. No identical replay is requested.

## Read-only capture and provenance

- Closed-game capture at 2026-11-11 15:49:05+01:00; 21,340,696 bytes / 56,747 lines.
- SHA256 `a2683b6e850309b3e1704a259697114246d521056f7498da4093ed1f419ea1dd`,
  independently identical on Windows and Mac.
- Installed manifest/assembly: 1.11.0 / 1.11.0.0. DLL SHA256 still
  `d51dd9ed985a2dd4fb1c4019b32750f8a2c0d1031a033ecbb1ec955ad4346c94`.
- One 96x96 tactical map, pointer `2BE1E1BD480`; round-start records 1–19.
  Initial pirate composition: 32 outcasts, six scavengers, three flamethrower
  teams, three chaingun teams, one boarding-commando unit. Eight player actor
  identities appear in route records. Round 19 starts with 37 pirates recorded;
  the log ends during that round, not after a complete round-19 enemy end-state.

## Recorded mobilization gap

| Round start | Pirate actors | Native sleeping flag | Native known-opponent count | Native zones per pirate |
| --- | ---: | ---: | ---: | ---: |
| 1 | 45 | 45 | 0 | 0 |
| 3 | 45 | 45 | 1 | 0 |
| 8 | 44 | 35 | 6 | 0 |
| 10 | 44 | 28 | 8 | 0 |
| 19 | 37 | 23 | 8 | 0 |

- All recorded pirate round-start headers throughout rounds 1–19 have zero
  native zones. The known-opponent count is a faction-list count, **not** proof
  every listed actor is currently visible or its live position may be read.
- All 19 contact-advance refresh records say `contacts=0`; all 19 deduplicated
  scope records are closed (`NotReady` or `NoAdvanceOrder`). No group choice or
  group fire/smoke signal is logged. This contact trace is not at its log cap.
- Combined-arms publication records through round 6 show zero eligible actors.
  Initially all 45 are rejected as Sleeping; by round 6 the rejection breakdown
  is Sleeping:37, Contained:1, LowMorale:1, InfantryWithoutAdvanceOrder:6.
  Its 256-record cap is reached in round 6; do not infer late roster counts from
  absent CA diagnostics after that point.
- Many enemies were already at the edges before movement: 36/45 initial actors
  are within 12 tiles of an edge; 31/37 round-19 actors are within that band.
  This confirms peripheral deployment and continued peripheral presence, **not**
  that every observed move was flight away from a known player position.
- Sleeping is a native AI flag, not literal immobility: these actors still move.
  Unalerted roaming is consistent with the trace; the exact mission/native roam
  contract has not been re-reviewed here.

## Boarding commandos: concrete example

Actor `2BE73B3F900` starts at `(84,1)`. Its recorded native moves wander through
`(88,4)`, `(91,7)`, `(89,3)`, `(89,6)`, `(93,9)`, `(94,5)`, `(93,3)`, `(89,5)`,
`(94,4)`, `(90,6)`, `(94,5)` and `(93,10)` in rounds 1–12. It remains on `(93,10)`
at round starts 13–19. The native sleeping flag stays true through round 13,
then false from round 14; all its native zone lists remain empty.

Its six logged accepted attacks occur **only in round 18**: a thermite grenade
at the player's vehicle at `(95,8)`, and five PDW calls at vehicles at `(91,9)`
or `(90,16)`. The player has reached the corner; these calls do not demonstrate
a commando push. Later suppression/morale can legitimately constrain movement;
this does not justify the earlier missing mobilization.

The whole pirate force has 85 logged accepted attack calls, many repeated or
reactive. They must not be counted as 85 proactive attacks or confirmed hits.

## Concrete source boundary, not a tuning diagnosis

- `CombinedArmsRuntime.Snapshot()` in
  `mods/jiangyu/AIPerformance/code/CombinedArmsRuntime.cs` rejects infantry without
  a native advance order as `InfantryWithoutAdvanceOrder` (line 94).
- `PublishAdvanceGroups()` in `CombinedArmsAdvance.cs` constructs members from
  accepted support snapshots **and** `_contactAdvance.TryAim()` results. Empty
  contact memory prevents shared groups even if another readiness gate passes.
- `ContactAdvanceRuntime.Alert()` requires a contact aim to wake an agent.
  `KeepAdvanceZones()` preserves existing advance orders, not new objectives for
  unordered pirates. The original convergence fallback is capped at three
  responders and also excludes sleeping actors; it is not a faction director.

Thus the local support/crossing implementation does **not** satisfy #17's
operational director for this unordered, initially sleeping pirate mission.
Removing safety checks or increasing aggression/timeout/roster caps alone is not
a supported fix. Why legitimate native sightings/attacks never populate this
contact memory is not yet isolated: the opponent-list count does not answer it.

## Evaluation cost

762 completed AI performance summaries all report pending=0 and droppedTasks=0.
The maximum recorded `Agent.Evaluate` call is **7,916.4 ms** in round 16; boarding
commandos also have calls above 2.5 seconds. This is measured evaluation latency,
not a measured whole-faction CPU total or proof which native/mod stage dominates.
The player still considers turn lengths reasonable. No ERROR/NRE or contact
thread-mismatch record appears in this capture.

## Next supported scope

1. Verify the actual native sighting/last-seen path feeding contact memory; keep
   detection-before-position reads and never substitute hidden live positions.
2. Supply a bounded faction alert and stable shared operational objectives for
   eligible unordered pirates, including appropriate initially sleeping units.
   Preserve mission/script defenders and native action/path/AP/safety ownership.
3. Build loose pirate reaction groups from those objectives, not only existing
   `AdvanceTowards` assignments. Reuse the tactical support layer after units
   really have a common task; no blob, no fabricated orders into hidden targets.
4. Update only on meaningful events/rounds; profile and cap assignment/scoring
   costs for 45-unit rosters before another PC candidate.

Captured-log assertions and source-guard checks passed with the retained local
`.tmp/check-ai1110-pirates.py`; scoped diff checks passed. This investigation
changes documentation/tracking only, not gameplay code, installed mods, settings
or saves. #17/#13/#2 remain open. No new build or fix is claimed.
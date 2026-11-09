# AIPerformance 1.6.1: ineffective support and retreating walkers

Tracks [#13](https://github.com/EmaEli/menace-mode/issues/13),
[#17](https://github.com/EmaEli/menace-mode/issues/17), and related initiative issue #2.
This is rejected gameplay, not acceptance of combined arms. The user reports no useful
vehicle support and walkers running away when player infantry appears.

## Capture and activation

- Closed-game log captured **2026-11-09 13:42:45 UTC**; local timestamps approximately
  14:28–14:40, round-start records 1–17, one map, 18 initial Rogue actors.
- Local evidence: `.tmp/ai161-feedback-9c31.log` and allowlisted `.json` summary;
  **6,398,560 bytes / 18,445 lines**, SHA256
  `8ca926170af396c63fbbecb255aa0fdb5baf5db52ef6bcafbeb2f3e054d676bd`.
- Read-only source access; immutable prefix captured before hashing and transfer.
  Script round-trip and both-machine hashes verified. Remote retained evidence:
  `E:/MENACE-Work/Builds/ai161-feedback-9c31/`.
- Installed manifest/assembly **1.6.1 / 1.6.1.0**, DLL SHA256
  `cf839a5ca1a531ce2265c2b1171afe512e537e2ab40acfa6780185b95380ca58`.
- Support and diagnostic settings true; startup explicitly confirms combined arms,
  diagnostics and offensive planning enabled. No AIPerformance disabled message.
  An absent persisted offensive-planning key means the source default applies;
  the startup message, not a missing JSON field, establishes activation.
- Exactly **256 CA-DIAG records**, reaching the mission cap at round 11:
  11 publish, 71 plan and 174 choice. Deduplication reports only the first admitted
  actor/round/phase observation; later re-evaluations are not fully represented.

## Confirmed support-policy failure: rear geometry can reward retreat

The planner did run and make assignments: **19 score-seeded records**, 11 vehicle
and eight heavy-team. All **48 logged seeded destinations have attack=0.00**.
These are lane/positioning projections, not proof of actual covering fire.

Two concrete moves connect seeded positions to executed withdrawal:

| Walker / round | Evidence |
| --- | --- |
| East `20740DA2000`, 5 | Current (83,70), request infantry (81,72) advancing toward (41,54). Planner seeds (87,71) among three rear positions. Native MOVE-EVAL selects that exact tile; completed MOVE reaches (87,71), leaving 19 AP. Attack utility is zero. |
| West `2073EACA000`, 6 | Current (19,51), request (18,37) toward (60,50). Planner seeds (14,48)/(14,49). MOVE records ultimate=(14,49), AP-clipped destination (15,50), completed movement and 19 AP remaining. Attack utility is zero. |

Both moves increase geometric distance to both unchanged mission rectangles.
Thus **some withdrawal is associated with our support scoring**, not merely an
unrelated native retreat. No controlled OFF comparison establishes the complete
counterfactual; this does not prove every backward step has the same cause.

Source mechanism (`CombinedArmsRuntime.Origins`, `SupportQuality`, and `Plan`):
- Hard rear-half-plane eligibility can exclude holding/forward continuation.
- Quality prefers roughly six tiles from the requesting infantry; it does not
  compare tactical benefit against moving backward from the current position.
- A sampled clear mission lane yields positive utility without a detected shot
  (`SupportProjection.Evaluate`). No explicit useful-progress or withdrawal-cost
  guard distinguishes holding useful cover from retreating solely for spacing.
- The reported 40-AP firing reserve is not a demonstrated execution guarantee:
  these completed moves leave only 19 AP. Native path/AP clipping remains active.
  Do not describe score seeding as ensuring a move-and-fire sequence.

## Other failure paths must remain separate

- Northeast `20567F91000`: nine of ten logged plans (rounds 1–10) return no-request;
  round 1 has stale order context, rounds 2/3/5–10 have nearby infantry but none
  compatible. Round 4 reaches projection but produces no score seed.
  `CombinedArmsState.Compatible` requires the same allocated advance rectangle
  for ordered assets. Nearby infantry alone is not enough to acquire support.
  Do not remove mission-order constraints blindly to make matching succeed.
- West rounds 2–5: all candidate origins rejected as ahead (107 in round 2),
  despite a compatible infantry request. Geometry can disable support outright.
- West rounds 9/10: first logged plans produce no seed; accepted native moves
  (14,49)→(10,47)→(9,42) have zero utility, attack utility and distance score.
  Morale=1 and suppression=0 exclude panic from those fields, not legitimate AT
  avoidance. Safety/threat components and enemy weapon classification are not
  logged here. Their precise withdrawal cause remains unproven.
- Returning policy ownership after failed projection bypasses generic offensive
  planning, while native movement still runs. An assignment/score is therefore
  neither a useful shot nor an invariant maintained through re-evaluation.

## Combat qualification

There are **52 accepted logged hostile attack calls**. One walker, the northeast
unit, makes **six accepted auto-laser calls in rounds 12–15**, all toward (63,48),
with `targetEntity=[actor=<null>]`. The other two walkers have no accepted attack
calls in this capture. No autocannon attack appears. Seven machinegun suppressive
calls and 16 mortar calls also appear.

Consequently, do not claim literally zero vehicle firing. These six late calls do
not prove hits, identify a target actor, or demonstrate coordinated infantry support.
The user's assessment of ineffective support remains valid.

## Validation and proposed next change

`python3 .tmp/check-ai161-feedback-9c31.py` passes: capture/hash/version, diagnostic
counts, matching failures, exact seeded/evaluated/executed retreat correlations,
unseeded native retreats and qualified attack counts. It is a log-evidence check,
not an IL2CPP execution test, performance benchmark or gameplay fix.

Next targeted gameplay change should prevent **spacing-only withdrawal**: holding
or continuing useful support must beat backing away merely to sit behind infantry;
prefer legal useful firing positions and account for movement/real attack AP.
Keep genuine known-AT avoidance, native path/footprint/visibility and mission orders.
Reproduce these cases in linked production fixtures before another test build.
Separately investigate unassigned assets and native zero-utility withdrawal, without
making all vehicles charge or relaxing safety indiscriminately.

No gameplay source, installed mod, settings or saves changed. No new build/deployment,
native safety attribution, turn-time acceptance or request for an identical replay.
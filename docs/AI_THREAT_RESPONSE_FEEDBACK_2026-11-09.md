# Threat-response gameplay feedback — 9 November 2026

Related: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#13](https://github.com/EmaEli/menace-mode/issues/13),
[#1](https://github.com/EmaEli/menace-mode/issues/1),
[terrain-density history #3](https://github.com/EmaEli/menace-mode/issues/3).
Candidate: [AIPerformance 1.8.0 / SmokeOverhaul 0.9.0](AI_THREAT_RESPONSE_2026-11-09.md).

## User result

The vehicle at the top of the map still did not provide the required support.
Vehicles appear too cautious, like infantry preferring cover. The user suspects that
96×96 map spacing leaves too little useful cover. Smoke was visibly deployed: not
terrible, but not particularly smart or concentrated where most needed. This is partial
smoke execution evidence, **not acceptance of support usefulness or smoke placement**.

## Capture and limits

- Closed-game `Latest.log`, 7,732,452 bytes. Local and PC SHA256 match:
  `230e17445f9a74703b20470ad20306a771a2a1c85db79812e66c6425ade6d808`.
- Retained locally at `.tmp/ai180-support.93n9TZ/Latest-feedback-20261109.log`.
- Startup confirms AI 1.8.0, Smoke 0.9.0 with 12/12 hooks, combined-arms support and
  diagnostics enabled, and LargerMaps 2.6.2 on 96×96.
- All 256 combined-arms diagnostic slots are consumed by round 9. Route records continue
  through round 20; later absence of CA records is **not absence of support evaluation**.
- No screenshot-to-actor identification was established. Coordinates below identify
  logged actors, not a guaranteed mapping to the user's phrase “vehicle on top.”

## Vehicles: provisional movement is not executed movement

The walker `213B9F2A480` starts at `(94,35)`, reaches `(84,37)` by round 3 and remains
there at every round start through round 10. In round 5:

1. At 100 AP, native Move receives `(81,40)`, utility `1.742131`, score 49; selected Move.
2. At the same tile/AP, a later evaluation receives `(80,39)`, utility `1.7480435`;
   selected Move again.
3. A later evaluation has a null destination and Move score 0; selected Idle.
4. Execute actually runs Idle with 100 AP; no movement occurred.

Rounds 6/7 show the same initial forward choices without round-start progress.
Threat-response diagnostics for rounds 5–7 generate three positions each for this
walker and the western walker, but zero for `213BAFDF480` near `(87,73)`.
All nine recorded vehicle response entries use last-known threat `(57,59)` with
`detected=False`; they are staging attempts, not available native shots.
The zero-position entries do not distinguish safety, AP or candidate-cap rejection.

Source provides a concrete repeated-evaluation suspect:
`OffensivePlanningBudget` permits 48 cost units per actor per round. `PlanVehicle`
spends four per probe, rebuilds edits for each evaluation, and retains no reusable plan.
Its eight-probe pass can consume 32 units; subsequent fresh score collections can lose
the useful seeds once that shared budget is exhausted. Existing idempotence coverage
reuses edited scores; it does not recreate native scores through budget exhaustion.
This matches the observed loss, but **causation is not yet reproduced in a fresh-score
linked fixture**, nor is native safety/path execution excluded as an additional cause.

Across the full route capture, six accepted vehicle laser calls exist: three from the
western walker at `(37,54)` (target actor unresolved), and three from `213B9F2A480` at
the civilian worker `(73,47)`. `213BAFDF480` has no accepted attack call. These calls
do not establish useful vehicle support against the observed firing threats.

## Smoke: execution works; crossing coordination remains weak

Four accepted hand-smoke uses match four successful smoke-executor records:

| Round | Origin | Threat bearing | Smoke target | AP before → after |
| --- | --- | --- | --- | --- |
| 15 | (78,48) | (64,47) | (77,48) | 20 → 0 |
| 17 | (78,48) | (64,47) | (76,48) | 55 → 35 |
| 18 | (48,65) | (57,59) | (49,64) | 100 → 80 |
| 19 | (78,48) | (64,47) | (77,48) | 100 → 80 |

Three uses belong to the same rifle squad. The round-15 throw leaves no AP for an
immediate move. Current requests select a nearby remembered threat and one-/two-tile
local smoke offsets; they do not rank the faction's most dangerous blocked crossing,
reserve movement AP, or prove the planned route will be protected. Direction alone is
not effective screening, and successful Use does not prove successful smoke-and-advance.

Clarification of earlier “no grants” wording: the **support bridge** grants nothing.
This run's existing SmokeOverhaul setting is `freeInfantryGrenade=True`; startup reports
three-use smoke granted to 73 human infantry templates. The wider mod can grant smoke
when that separate setting is enabled. No setting was changed during this investigation.

## Terrain hypothesis and next step

The existing optional density feature was already active at **5×** on this map.
That multiplies stamp-placement attempts, not guaranteed cover count or coverage.
No placement-success inventory or measured cover distribution was captured. Sparse
useful cover remains plausible; a disabled density toggle is not the explanation.
Do not reopen closed #3 or claim a new terrain regression without stronger evidence.

Next bounded work: reproduce fresh-score support loss, preserve only still-valid plans
across repeated evaluations without extra probes, then tie smoke to an actual blocked
crossing with appropriate AP and route checks. Keep legitimate AT caution, native action
legality and mission orders; do not globally lower danger or assume hidden AT is absent.

Validation for this investigation: captured-log assertions and scoped Markdown/diff
checks. No gameplay-code, installed-mod, settings or save changes; no new build/deploy.
Issues #17/#13/#1 remain open.
# AIPerformance 1.7.0 — support still ineffective against a fixed MG

Follow-up to [the installed follow/flank candidate](AI_SUPPORT_FOLLOW_2026-11-09.md),
tracked in **#13 / #17**. **Gameplay not accepted.** The user reports essentially the
same vehicle behavior, only one vehicle more aggressive, and infantry repeatedly
exposing itself to an MG that killed several enemy units without meaningful pressure.
Movement alone is not coordinated combat support. No new identical replay is requested.

## Capture identity

- Closed-game read-only snapshot, **2026-11-09 14:28:46 UTC**.
- **7,022,048 bytes / 20,224 lines**, SHA256:
  `5D90A9BA39FE553B55054A96B8CE6FBCE7AB0F6070F5D1FB42D0712354AA5C36`.
- Installed AI manifest/assembly **1.7.0 / 1.7.0.0**; DLL matches the installed candidate:
  `F766C34AC22CF7C5854B9D624677C73C23A7E885AC92DDFF0FD9BF7B733E7715`.
- Startup confirms combined-arms support, bounded diagnostics and offensive planning
  enabled. No AI-component disable record; no logged NullReferenceException,
  AccessViolationException or AiEvaluationTimeoutException.
- Retained PC evidence: `E:/MENACE-Work/Builds/ai170-feedback-a4f2/`.
  Private local capture/summary: `.tmp/ai170-feedback-a4f2.log` / `.json`.
  Capture script upload/download and capture SHA256 verified across both machines.
  No live mod, settings, save or unrelated files changed.

## What this run records

Nineteen hostile round-start records, initially **18 units**, finally **12 alive / six
dead**. The missing original actors are five conscript squads and the western walker.
These are not six proven MG kills; the raw diagnostics do not attribute each death.

| Walker pointer | First position | Last sampled alive position | Logged accepted laser calls |
| --- | --- | --- | --- |
| `20A3DBA4D80` | `(93,79)` | `(78,72)`, round 19 | 0 |
| `20AB6A4DD80` | `(4,51)` | `(22,51)`, round 16 | 0 |
| `20AB6B91D80` | `(94,35)` | `(73,39)`, round 19 | 4, rounds 11–13, all targeting `(63,48)` |

After initial advances, walkers again have multi-round holds and many one-tile changes.
Only one of the three logs laser use; it is not shooting at the fixed MG tile below.
This supports the reported lack of effective vehicle pressure, not a successful fix.

### The fixed MG and qualified enemy counterfire

- Greifinger (`20A96857900`) logs **11 accepted MG-related calls**, including background
  variants, from the same tile **`(68,58)`**, in rounds **4, 7, 8, 11, 12 and 18**.
- Targets at `(78,67)`, `(77,59)`, `(74,63)` and `(71,65)` match native same-round
  officer/conscript location samples. These joins identify exposure, not hits or kills.
- The officer at `(78,67)` enters its round-4 activation with suppression **0.8065454**
  and morale **0.65624994**, selects Idle with zero AP, and cannot run the healthy-unit
  flank policy. This does not prove which intervention would have saved it.
- Do **not** claim no counterfire: the capture has **32 accepted hostile attack calls**,
  including ten mortars, four lasers and **nine sustained-rifle calls targeting the MG
  tile**, from conscripts in rounds **11, 12, 17 and 18**. No mortar/laser call targets
  `(68,58)`. Accepted calls do not establish hits, damage, danger or adequate coordination.
- Only **33 of 5,128** native hostile attack-evaluation records have positive scores.

### What support diagnostics do and do not establish

The **256-record mission cap is reached in round 10**: ten publish, 75 plan and 171
choice records. Plans contain 34 seeded outcomes (20 vehicle / 14 heavy); **all 49
listed seed attack-utility values are zero**. Spacing/position utility is not an actual
firing opportunity. All 30 logged vehicle final choices retain the native behavior ID
(ten Move / twenty Idle); the trace alone does not establish the exact reason for holds.

There is **no logged flank phase before the cap**. This does not prove no flank later,
nor whether observation, readiness, coverage, AP, budget or another guard rejected an
earlier response: 1.7.0 only logs successful flank seeding. Greifinger's native opponent
memory is intermittently known/expired; that TTL is not proof of current detection/LOS.

## Three gaps reproduced against unchanged 1.7.0 production source

Private diagnostic harness `.tmp/ai170-policy-probe/Probe.csproj` links the production
hooks/runtime and existing managed stand-ins unchanged. It does **not** execute native
methods, replay the real map, or prove which branch selected each real movement.

1. **Known danger can fall through to exposed advance.** With remembered MG position
   `(68,58)`, infantry at `(78,67)`, a forward mission order, and no local tile outside
   that firing lane, `Flank()` returns zero. `OffensivePlanningScores()` then invokes
   generic planning; `Advance()` gives exposed `(75,65)` **utility 20**, despite the
   remembered threat. No hidden position read occurs. Native safety remains unchanged,
   but the mod adds progress utility instead of treating danger as an unresolved response.
2. **Failed early observation consumes the round slot.** `BeginObservation()` is called
   before finding a valid threatened ally. A first out-of-range observation prevents a
   second same-round observation after infantry moves within range; the next round works.
3. **Victim readiness controls whether danger is remembered.** Observation reuses the
   support-advance `Snapshot()` gate. If the only exposed infantry is already suppressed
   or low-morale, no threat memory is created, even with a detected armed enemy in range.
   Keeping native emergency actions is appropriate; forgetting danger for healthy allies
   because the first victim is pinned is a separate limitation.

The existing green tests verify bounds/hidden-read protection and successful covered
flanks. They did not establish adequate behavior when no covered flank exists or when
the first exposed unit has already been suppressed. Compilation is not acceptance.

## Targeted next scope, not another aggression multiplier

- Separate **observing danger** from whether its victim may advance; retain native
  suppression/morale emergency decisions. Allow a strictly bounded contextual retry
  after an unsuccessful observation, without hidden-position refresh or unbounded scans.
- Distinguish no threat, usable covered response and **unresolved known danger**. The
  last must not silently return to generic exposed-advance incentives. Preserve genuine
  useful attacks, AT avoidance, mission context and expiring memory; do not globally idle
  all infantry or bring back long native planning stalls.
- A covered destination alone does not establish safe transit through an MG lane. Review
  the native movement/path contract before claiming exposure-safe routes.
- Meaningful support still needs threat-directed fire/setup/smoke/indirect-fire decisions,
  not merely proximity. Those actions were not implemented in 1.7.0; no new mortar/smoke
  legality, dispersion or friendly-fire contract is claimed here.

**Checks run:** captured-log integrity/count assertions pass; all three linked-source
diagnostic reproductions pass. No gameplay code changed, no new mod build/install and
no Windows/native behavior test performed in this investigation. Keep #13/#17 open.
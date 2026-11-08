# AI Performance 1.4.0: movement without coherent combat initiative

Related: [#2](https://github.com/EmaEli/menace-mode/issues/2),
[#17](https://github.com/EmaEli/menace-mode/issues/17).
[1.4.0 source and installation](AI_CONTINUOUS_ADVANCE_2026-11-08.md).

## User feedback and scope

The user reports enemies moving around "like headless chickens". This is **not acceptance**
of tactical initiative. More movement is insufficient; do not respond with another general
movement boost or ask for another identical twenty-turn replay.

This follow-up is read-only investigation and evidence recording. No gameplay source,
installed mod, diagnostic toggle, setting or save changed. The game was running at capture.

## Capture and identity

- Immutable prefix copied from read-only Latest.log at **20:54:29 UTC, 8 November 2026**.
  Local capture `.tmp/Latest-ai140-feedback-20261108-a8f2.log`, **5,130,686 bytes**.
- SHA256 `30168f42826a38de5ebe88bf783d160a1e45c3066d58b9e147f2b72f87ffa2f8`.
  Script upload/download round trip, remote script hash, length and capture hash verified.
- Installed manifest/assembly **1.4.0 / 1.4.0.0**; DLL SHA256
  `8ed977a85b6d88518d7b1ee1bcf5f98c6dcd2e0b9dcfe944ea3e9f7369d1ae5f`, matching installation.
- One map; round-start records **1–14**, 18 initial enemies, 17 sampled survivors at round 14.
  All 18 change round-start position. Recovery enabled; 226 attack / 3 buff positioning
  profiles simplified. No optional recovery-stage records, so individual recovery
  applications cannot be directly attributed. This is a live prefix, not the completed mission.

## Concrete movement evidence

The two orders remain **AdvanceTowards (60,50,5,6)** and **(36,50,6,5)**, priority 1 each.
There are 694 agent-start records with these orders and 18 opening zero-order records.
The orders are not being repeatedly swapped or replaced during the observed wandering.

Joining accepted MOVE events with the preceding same-unit, same-round MOVE-EVAL and matching
target yields 131 distinct observed source/target moves. Six increase geometric distance to
**both** mission rectangles, using the existing diagnostic's inclusive-rectangle metric:

| Unit | Round | Source → target | Distance increase, zone 1 / zone 2 |
| --- | --- | --- | --- |
| Machinegun team | 4 | (86,39) → (87,39) | 0.90 / 0.97 |
| Conscripts | 9 | (14,70) → (10,70) | 3.82 / 3.33 |
| Autocannon team | 9 | (54,81) → (54,83) | 1.95 / 1.81 |
| Walker | 9 | (12,57) → (11,58) | 1.05 / 1.13 |
| Same conscripts | 10 | (10,70) → (7,69) | 2.62 / 2.12 |
| Officer squad | 10 | (46,79) → (46,80) | 0.87 / 0.98 |

All six matching evaluated destinations have **UtilityScore=0, UtilityByAttacksScore=0,
DistanceScore=0**, but a positive native Move behavior score. All logged attack evaluations
for these unit/round pairs have zero score and zero candidates. The officer later returns
from (46,80) to (46,79) in round 13. Two sampled unit trajectories revisit a prior tile.
Many other units do make progress: this is not evidence that every move is random.

The zero score fields do **not** reveal SafetyScore or prove every backwards move is wrong:
cover, threats and path detours may justify some of them. The stored-target warning is
handled by requiring a matching subsequent accepted MOVE; remaining MOVE coverage is capped
once. These are diagnostic geometry deltas, not exact path lengths or native integer costs.

## Combat and mechanism boundary

- Ten accepted logged enemy attack calls, **all medium mortar**. No accepted hostile
  infantry attack or direct civilian-targeted attack in this prefix. This does not prove
  absence of collateral damage. No known civilian is recorded in hostile faction snapshots.
- 3,588 logged attack evaluations; **3,564 zero-score/zero-candidate**. ATTACK-EVAL is capped
  in 78 actor/round budgets, but SKILL is not capped. No route-buffer/observer failure.
- No recovery-disable, TimeoutException or NullReferenceException. There are 156 existing
  InvalidCastException mentions; the known structure-to-actor issue remains separate (#96).
- Exact native 0.7.18 byte checks reconfirm that known-opponent state suppresses native
  advance distance/inside-zone contributions. Simplified planning also removes attack
  positioning influence. This combination remains a suspect for zero mission/attack values,
  not proof of the complete native safety/ranking cause for each move.
- The current recovery can replace **Idle**, not an already-positive native Move. A managed
  reproduction linking the actual current production choice hook preserves a native Move
  from (10,70) to (7,69), despite armed idle recovery and no attack candidates. It confirms
  this recovery boundary; it does not execute native cover/path ranking or reproduce live causality.

## Checks and disposition

- `.tmp/check-ai140-feedback-20261108.py`: capture, identity, roster, stable orders,
  counterexample score fields, empty evaluations and uncapped attack-call coverage PASS.
- `.tmp/ai140-wander-checks/Ai140WanderChecks.csproj`: managed production-hook boundary
  reproduced. No IL2CPP methods executed.
- Eleven exact-binary native order/utility/phase/path contracts PASS on current 0.7.18.
- Next target: **coherent destination choice when known contacts provide no useful shot**,
  retaining mission direction without overriding valid attacks, emergency safety or path/AP.
  Separate missing mission/attack direction from legitimate cover before choosing a new policy.
  Do not add more blind movement, invent hidden contacts or disable fast planning as a quick fix.
- #2/#17 remain open. 1.4.0 startup is observed; combat initiative and wandering are not solved.
  No new build/install or additional user replay requested by this investigation.
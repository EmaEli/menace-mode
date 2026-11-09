# Vehicle staging re-evaluation correction — AIPerformance 1.8.1

Issues: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#13](https://github.com/EmaEli/menace-mode/issues/13).
Follow-up to [the rejected 1.8.0 support replay](AI_THREAT_RESPONSE_FEEDBACK_2026-11-09.md).
User approved fixing the disappearing vehicle move first; smoke and terrain stay unchanged.

## Reproduction and confirmed defect

The new linked-source `CombinedArmsStagingChecks` recreates native tile scores before
each evaluation, with a ready two-weapon walker, a safe affordable forward tile and a
legitimate last-known enemy position. It retains one round's original planning budget.

Unchanged 1.8.0 fails deterministically at **evaluation four**, before any Execute:
three passes consume the 48-token actor budget although the hidden target causes zero
native usability/value probes. The fourth pass returns no forward utility. This explains
one reproducible source of the provisional-Move-to-Idle loss in the captured replay;
it does not prove every observed stall had that cause or that native paths will succeed.

## Minimal correction, not a stale-plan cache

- Charge existing four-token target samples only when a detected-target usability/value
  query actually starts. Keep the eight-query/soft-10-ms start gate, actor/faction budgets
  and unchanged native shot checks. Neither budget is raised or reset early.
- If no query is possible, re-seed only current native candidate scores for safe forward
  staging. Recompute safety, movement/fire/setup AP, weapon availability, readiness,
  orders and remembered threat context on every evaluation. No native wrapper/attack or
  stale score is retained; no hidden target position is read.
- A higher-danger origin still requires a fresh positive native shot evaluation. Budget
  exhaustion cannot manufacture one. Native Move continues to select targets, calculate
  paths and execute; no direct action, extra pathfinding or aggression multiplier added.
- SmokeOverhaul, LargerMaps, runtime settings, saves and unrelated mods are outside scope.
  Smoke placement coordination remains future work, not part of this candidate.

## Local verification

- New regression first failed against unmodified 1.8.0 at evaluation four.
- Corrected linked AI/recovery checks pass, including fresh-score repetition, unchanged
  native probe caps/reset, faction exhaustion, no hidden reads/fake shots, fresh danger,
  movement/setup/fire AP, empty weapons, changed defensive orders, morale/suppression,
  actual movement and expired memory.
- 208 GameplaySuite Python tests, 302 AI production-behavior checks and 193 native
  metadata contracts pass. Production Release build has zero warnings/errors.
- The initial Python run found the expected source/compiled manifest mismatch during
  the version bump; refreshing the generated manifest resolved it. Final full run passes.
- PC preflight confirms game closed and native SHA256
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`
  (0.7.18+22828); all 121 proxy hashes match local references. A harmless SFTP upload/
  download round trip is hash-matched. Protected inventory covers 1,450 files, including
  SmokeOverhaul, LargerMaps, settings/loader and UserData.

## Candidate and Windows status

Windows staging: `E:\MENACE-Work\Builds\ai181-staging-KShdaE`.
Local evidence: `.tmp/ai181-staging.KShdaE/`.
Build from an archived committed `dev` snapshot so unrelated pre-existing working-tree
edits do not enter the one-module kit. Reuse the canonical builder/verifier and installer
with installed-settings mode; never copy individual outputs into live Mods.

Clean kit verification, Windows BuildOnly/install, installed metadata/backups and
startup/gameplay remain separate pending gates at this source checkpoint.
Issues #17/#13 remain open; automated reproduction is not gameplay acceptance.
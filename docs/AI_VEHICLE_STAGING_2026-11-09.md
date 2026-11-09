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
- 13,118 corrected linked AI/recovery checks pass, including fresh-score repetition, unchanged
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

## Verified candidate and install

- Published gameplay source: `383807e3b156e784e499c8c967988476f77335a4` on `dev`.
- One-module BuildKit: 73 inventoried files; all 68 tracked source files match that
  commit byte-for-byte. ZIP SHA256:
  `0a0c544eead58103bee3108230d5607c6c194d153010a28fb92ead7dd7b1b28f`.
- Canonical kit generation and clean extracted-source Release rebuild pass, including
  17,986 selected shared/artifact checks, 302 AI behavior, 13,118 linked AI checks and
  193 native contracts. These totals overlap; do not add them together.
- The local archive needed a temporary .NET 10 SDK selection to override the scratch
  parent's SDK 6 pin. That config is excluded from the kit; published source is unchanged.
  The shared offline harness emits existing nullable warnings; the production DLL has
  zero warnings/errors in local clean builds and both Windows runs.
- Hash-matched transfer, extracted inventory and canonical Windows **BuildOnly** pass
  with no live changes. The canonical **Install** then passes on private SDK 10.0.113,
  rerunning 193 native, 302 behavior and 13,118 linked checks before deployment.
- Installed `Mods\AIPerformance`: manifest **1.8.1**, assembly/file **1.8.1.0**.
  DLL SHA256: `BF10A3E9FC70DB75F2CBA2D14E97E99E3106579A10CEDA564DFDC7C61E14C41B`.
- Backup/journal: `_MENACE_Backups\20261109-195006-7e847dd70242466e8908770926052a5d`.
  Only AIPerformance is affected. Old backup and new installed inventories are hash-verified;
  the previous version is recoverable through the retained canonical rollback journal.
- All **1,450** protected files retain their count/hash, including SmokeOverhaul,
  LargerMaps, settings companion, loader and UserData. No settings or saves changed.
- Logs: `E:\MENACE-Work\Temp\MENACE-install.log`, plus
  `AIPerformance-BuildOnly.log` / `AIPerformance-Install.log` in the staging directory.
  Local SSH transcripts are `windows-buildonly.log` / `windows-install.log`.

Startup/native dispatch and useful support in the original encounter have **not** been
tested on 1.8.1. Replay that encounter and check executed movement/firing, not merely
provisional choices. Retrieve the fresh Latest.log after the user's result. Issues
#17/#13 remain open; automated reproduction and successful install are not gameplay acceptance.
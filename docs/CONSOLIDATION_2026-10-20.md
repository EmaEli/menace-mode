# One playable baseline, one development branch

## Decision — 20 October 2026

The user accepts their current playable setup as the practical baseline. Normal
play is acceptance, not a requirement to run every diagnostic iteration or a full
QA matrix. Integrate the current work together instead of leaving it scattered
across issue branches while the PC runs newer builds than the repository.

- `main`: user-accepted playable source baseline.
- `dev`: the only active development/integration branch, including work for all issues.
- No new feature/fix/issue/release branches or temporary development worktrees.
- Keep real bugs and future proposals as issues; source integration does not close
  them automatically. Reopen accepted scopes if the user reports a regression.
- Installer/public Nexus release checks remain necessary for those deliveries,
  but #34 is not a universal source-integration gate.

## Snapshot being consolidated

Start from fetched `origin/main` at `6903647`, preserving the complete current
source, tests and documentation, including previously untracked implementation files:

- LargerMaps 2.3.2: route diagnostics/budget, conditional advance recovery and tests.
  Windows install and ON/OFF gameplay are documented in
  [the AI report](AI_DIAGNOSTICS_2026-10-19.md); MEN-002 is closed by user acceptance.
- CinematicActionCamera and ThirdPersonCamera 1.2.2: framing/control fixes.
- WeaponMechanicsRebalanced 0.4.14: impact-gated muted ranging feedback, hostile
  per-activation scatter and enemy-only medium-mortar coverage.
- GarrisonableBuildings 1.2.0: native deployed-stance accuracy handling and tests.
- CoverDestructionRebalance 0.4.1: light/medium mortar cover-impact guard and tests.
- Overwatch 0.32.3.8: hostile-turn JIT guard and regression checks.
- Shared BuildKit verification fixes, package inventory and regression updates.

Existing source on `main` also contains SFP 0.11.8 and the earlier migration work.
Private `.tmp/` worktrees, logs, settings, game binaries and release archives are
not source deliverables and must not be committed.

## Historical refs

Most local issue branches are ancestors of current `origin/main`, or point at
the same commit. Re-merging their old distributions is unnecessary. Other old
PRs refer to pre-Jiangyu implementations or earlier versions; do not overwrite
the current packages merely to make the historical branch graph appear merged.

Two isolated local commits need explicit accounting:

- `6f5373c` / #51: Smoke Overhaul 0.7.0 opt-in free infantry grenades; recovered by
  cherry-pick `0c0d32b` on `dev`. This package was delivered to Downloads, not
  proven installed; do not describe it as an exact copy of the played PC build.
- `9564de7` / #61: mortar feedback already present in the broader current 0.4.14
  implementation; retain hostile-mortar additions rather than replacing them with
  that isolated 0.4.15 branch. Its other runtime delta moves impact-hook installation
  later in the same method; retain the current earlier installation. Feedback
  production helpers, overlay code and regression assertions are already included.
- Camera worktree: current source retains its changes; remaining differences are
  method/settings ordering, comments and UI/depth-of-field restoration order.
- Older detached MEN-002 worktree: diagnostics/budget are included; its 2.2.x
  plugin/version is superseded by the current tested 2.3.2 recovery implementation.

Preserve historical tips as remote and local tags under
`archive/2026-10-20/local/<branch>` and `archive/2026-10-20/origin/<branch>`
before removing old branch refs. Tags retain unique history, including older
documentation and prototypes not copied into current runtime packages. A branch
can be recovered from its corresponding tag; no force-push/reset is needed.
Existing dirty worktrees remain on disk, detached at their original commit.
They are inactive archives, not additional development lines.

**Completed:** verified 32 local/remote archive tags, then retired 17 local and
15 remote branch refs. Only `main` and `dev` remain locally and on GitHub. Closed
obsolete PRs #8, #12, #16, #18, #28 and #36 with links to the single
[integration PR #64](https://github.com/EmaEli/menace-mode/pull/64).
The working tree on `dev` is committed/pushed; the PR is not merged. The archived
Camera/WMR worktrees retain their original dirty-file hashes and commits.

## Consolidation checks

- GameplaySuite: 141 Python tests passed.
- LargerMaps: 146 Python tests run, 142 passed and four optional fixture checks
  skipped; available 0.7.17 native-binary and dump contracts were included.
- C# managed regression checks: 17,764 Suite, 12,237 recovery and 10,120 route-budget
  assertions passed. Existing nullable-reference compiler warnings remain.
- Camera: 28 static checks and the legacy settings-bridge checks passed.
- Corrected the source inventory for Smoke Overhaul/Overwatch and an obsolete
  camera test assertion after its actor-aware probability signature changed.
- `git diff --check` passed. No production rebuild, new kit, Windows install or
  new gameplay session was performed during consolidation.

## Evidence boundary

This consolidation does not install anything or change settings/saves on Windows.
The PC is off; a fresh complete installed-file/hash comparison has not been run.
Do not claim every source version above is byte-for-byte the current PC payload.
The user's overall gameplay acceptance is recorded without inventing individual
feature test results. Specific unresolved bugs remain tracked separately.

The integration target is one `dev` to `main` promotion, not another set of
per-issue PRs. Until that promotion happens, do not report `main` as synchronized.
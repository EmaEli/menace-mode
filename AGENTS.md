# MENACE project tracking

This repository (`EmaEli/menace-mode`, https://github.com/EmaEli/menace-mode) is the **canonical issue tracker** for the user's MENACE game-modding project.

## Mandatory installer standard (ALL mods / ALL future chats)

**Do not reinvent Windows build/deploy installers.** Before authoring, changing or delivering **any** MENACE installer, READ [docs/INSTALLER_STANDARD.md](docs/INSTALLER_STANDARD.md) and inspect the reusable one-click MEN-006 example linked there. This is a project rule, not an optional preference or conversation-specific suggestion.

Before any MENACE mod build, PC test deployment or Nexus package/metadata update, also read [docs/MOD_BUILD_DEPLOY_WORKFLOW.md](docs/MOD_BUILD_DEPLOY_WORKFLOW.md). It records the issue-review, BuildKit, Windows verification and Downloads hand-off steps; always re-check live issue status and paths.

- Ship **one double-click installer** that downloads pinned/verifiable public sources (or uses bundled sources), builds every required component, stages and verifies files/assembly+manifest versions, **backs up** affected installed versions and **installs automatically**. Include integrated companion mods (e.g. Third Person Camera) in the Suite's **single logical distribution** rather than handing the user multiple build ZIPs.
- Do not change live game files before all builds and version checks succeed; keep backups outside `Mods`, prevent duplicates and implement rollback. Do not touch unrelated mods, save files or user data.
- **Keep the console open even after error or keypress** (persistent `cmd /k`; `pause` alone is not sufficient). Always save a complete log at a stable, advertised path **from the first download step**; **open it automatically in Notepad on any failure**.
- Verify **real DLL assembly metadata**, the manifest, output files, the published source commit, bootstrap checksum and installed versions. A successful build is not evidence of successful deployment or an in-game fix. Explicitly disclose Windows/game tests not run.
- Reuse and update the existing launcher/PowerShell pattern; do not create another bespoke kit per issue/chat. Any installer regression discovered must be fixed in the shared pattern AND captured in the standard document.

## Workflow

- **Use only two active branches: `main` and `dev`.** `main` is the user-accepted playable baseline; `dev` is the single shared development/integration branch. Do not create per-issue, feature, fix or release branches/worktrees. Issues track work, not separate code lines. See [the consolidation record](docs/CONSOLIDATION_2026-10-20.md).
- **Sync before substantial edits/builds/delivery:** fetch current `main` and `dev`, preserve all local work, and integrate changes deliberately on `dev`. Never silently discard changes, force-reset or force-push. Repeat the comparison before packaging. Commit working source promptly so the PC is not the only place containing the latest behavior.
- Promote the complete accepted `dev` snapshot to `main` together, not one stalled PR per issue. Do not blindly merge historical branches over newer migrated code. Retain old refs until their unique work has been accounted for; archive/cleanup is separate from development.
- Before investigating, planning or fixing a MENACE issue, review existing GitHub Issues here and link the relevant one.
- Create a new GitHub Issue for a distinct bug, feature or investigation that is not already tracked. Avoid duplicates.
- Update the existing Issue with technical findings, reproduction details, affected mod versions, proposed fixes and test results as work progresses.
- The user's normal gameplay feedback is valid acceptance for the scope they accept. Close the corresponding issue with that feedback and known limitations; reopen for a concrete regression. Do not demand log parsing after every session, an exhaustive QA matrix, or verification of unrelated modules before integrating accepted work. Run relevant automated checks yourself and disclose unperformed checks; do not claim every feature was explicitly tested.
- Keep real unresolved bugs and future proposals separate from integration. Issue #34 concerns installer/public-release work, not a universal gate preventing accepted gameplay source from reaching `main`. Never close all issues just because source has been consolidated.
- Keep normal-release logging minimal (preferably silent); detailed diagnostic logging must be optional and OFF by default.
- Keep version numbers meaningful: minor increments for features/functional changes, patch increments for bug fixes; avoid unwarranted alpha identifiers.
- This repository is the shared tracking location; individual mod source/build artifacts need not be stored here unless specifically requested.

## Existing backlog (12 October 2026)

- #1 MEN-001: AI smoke grenade use.
- #2 MEN-002: Situational AI initiative.
- #3 MEN-003: Large-map terrain feature density.
- #4 MEN-004: Suspected ammunition drain on alternate fire.
- #5 MEN-005: Repeated `Actor.AimAt` exceptions.

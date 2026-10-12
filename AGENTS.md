# MENACE project tracking

This repository (`EmaEli/menace-mode`, https://github.com/EmaEli/menace-mode) is the **canonical issue tracker** for the user's MENACE game-modding project.

## Mandatory installer standard (ALL mods / ALL future chats)

**Do not reinvent Windows build/deploy installers.** Before authoring, changing or delivering **any** MENACE installer, READ [docs/INSTALLER_STANDARD.md](docs/INSTALLER_STANDARD.md) and inspect the reusable one-click MEN-006 example linked there. This is a project rule, not an optional preference or conversation-specific suggestion.

- Ship **one double-click installer** that downloads pinned/verifiable public sources (or uses bundled sources), builds every required component, stages and verifies files/assembly+manifest versions, **backs up** affected installed versions and **installs automatically**. Include integrated companion mods (e.g. Third Person Camera) in the Suite's **single logical distribution** rather than handing the user multiple build ZIPs.
- Do not change live game files before all builds and version checks succeed; keep backups outside `Mods`, prevent duplicates and implement rollback. Do not touch unrelated mods, save files or user data.
- **Keep the console open even after error or keypress** (persistent `cmd /k`; `pause` alone is not sufficient). Always save a complete log at a stable, advertised path **from the first download step**; **open it automatically in Notepad on any failure**.
- Verify **real DLL assembly metadata**, the manifest, output files, the published source commit, bootstrap checksum and installed versions. A successful build is not evidence of successful deployment or an in-game fix. Explicitly disclose Windows/game tests not run.
- Reuse and update the existing launcher/PowerShell pattern; do not create another bespoke kit per issue/chat. Any installer regression discovered must be fixed in the shared pattern AND captured in the standard document.

## Workflow

- Before investigating, planning or fixing a MENACE issue, review existing GitHub Issues here and link the relevant one.
- Create a new GitHub Issue for a distinct bug, feature or investigation that is not already tracked. Avoid duplicates.
- Update the existing Issue with technical findings, reproduction details, affected mod versions, proposed fixes and test results as work progresses.
- Close an Issue only when a fix or conclusion has been verified and clearly documented. Do not claim untested fixes are resolved.
- Keep normal-release logging minimal (preferably silent); detailed diagnostic logging must be optional and OFF by default.
- Keep version numbers meaningful: minor increments for features/functional changes, patch increments for bug fixes; avoid unwarranted alpha identifiers.
- This repository is the shared tracking location; individual mod source/build artifacts need not be stored here unless specifically requested.

## Existing backlog (12 October 2026)

- #1 MEN-001: AI smoke grenade use.
- #2 MEN-002: Situational AI initiative.
- #3 MEN-003: Large-map terrain feature density.
- #4 MEN-004: Suspected ammunition drain on alternate fire.
- #5 MEN-005: Repeated `Actor.AimAt` exceptions.

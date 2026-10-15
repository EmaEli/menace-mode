# Offline work handoff — 15 October 2026

**Historical snapshot:** the user subsequently requested resumption. See
[MIGRATION_STATUS.md](MIGRATION_STATUS.md) for the continuing work. This ZIP does not
contain the later Field Logistics 1.0.11 / Infantry Assault 0.5.8 icon changes.

Latest local continuation artifact: `.tmp/MENACE-Suite-migration-followup.zip`.
SHA256: `a2f4369e355ea1758b486162bbcf42974691f4d1e50942402d499ad0a0d572b9`.
72 Python tests / 379 C# checks pass; clean extracted-source build passed for all
17 code packages plus camera. Final ZIP differs only in verification documentation;
276 inventoried files / 19 packages. No Windows/PowerShell/game acceptance or deployment.
The remaining text below describes the **earlier** handoff, not this continuation.

## Status and scope

Work wrapped at the user's request. **The original backlog is not complete.**
Changes remain local and uncommitted on `offline/menace-fixes-43-35-24-23`.
Base revision: `ba9543494f5f049305196eb832213b92c1a1e2aa`.
Latest main was fetched/compared before final packaging; no new merge was needed.
No Windows PC access, deployment, gameplay testing, release publication or issue closure.

## Implemented locally

- #43: shared HUD pointer capture for Coordinated Fire/Overwatch; map clicks remain
  suppressed through release after pressing a HUD control, with lifecycle cleanup.
- #35: display-name/nickname resolver rejects GUID-looking display strings without
  changing identity, XP, rank or save mappings (SquaddieVeterancy 1.10.2).
- #24: partial K-PAC Overwatch tag fix; no speculative parent-skill tag fallback.
  ARC Range and PPP CQB are still undiagnosed.
- #23: Overwatch calls the confirmed native range/angle gate both when queuing and
  before firing; metadata/native errors reject the reaction, not grant a free shot.
- #33/#15: Cinematic Action Camera 1.0.0 local candidate: one MelonLoader camera owner,
  Jiangyu settings companion, compatible preference identifiers and preserved HUD fix.
- MenaceModSettings 0.5.0: shared capture API, missing-value legacy preference import,
  modern-value priority, explicit-default preservation and corrupt-file protection.
- #34: shared installer/launcher candidate, pinned SDK/loader hashes, all-builds-before-
  install staging, DLL metadata checks, known-duplicate handling, timestamped backup,
  partial-copy rollback journal, persistent log and console. **PowerShell not executed.**
- Added deterministic ZIP assembler/verifier and clean extracted-source build checker;
  no machine-local props, game proxy assemblies or debug symbols are packaged.

## Final local artifact

`menace-mode/.tmp/MENACE-Suite-offline-handoff.zip` (workspace-relative).

SHA256: `4d51e4bedd45b8cb310ccdac11a12166ed849d7e52389577300ee6a0203de068`.

One logical distribution: 19 Jiangyu packages (17 code + 2 data-only), camera runtime
source and single double-click launcher; 275 SHA256-inventoried files plus `kit.json`.
It is an **offline candidate**, not an approved installer/release. Double-clicking it
attempts a real build AND install; do not confuse it with a harmless build-only test.
Earlier scratch ZIPs in `.tmp` are superseded; nothing was deleted.

## Verification evidence

- 58 Python regression tests passed (including actual ZIP/integrity tests and installer
  source-wiring guards; those guards do not execute PowerShell).
- 206 C# checks passed, including real DLL AssemblyVersion/FileVersion and source/project/
  compiled manifest comparisons across all catalogued packages and the camera runtime.
- 17 camera static checks and legacy settings-bridge checks passed.
- All 17 Jiangyu code packages, both KDL-only packages and camera runtime rebuilt locally.
- Clean extracted-source build of all code/camera components passed using SDK DLLs from
  the hash-verified Windows CLI archive and cached game references. Build warnings remain;
  this is not a warning-free build or a Windows build.
- Final ZIP inventory/hash checks and independent pinned loader hash check passed.
- `git diff --check` passed.

## Still outstanding

- PowerShell 5.1 parsing/execution; launcher/log behavior on Windows; deployment, injected
  copy failure and real rollback; standalone/full cutover and game startup acceptance.
- In-game confirmation of every behavioral change above, old saves/new campaigns and
  mission transitions. See `mods/GameplaySuite/docs/TESTING.md` and `INSTALLING.md`.
- Remaining batch 2 audits (#31/#38/#29), including Field Logistics icon request, are
  not complete. Existing ports were rebuilt, not newly verified in play.
- Batches 3–5 investigations/AI/feasibility work remain deferred. Existing regression
  tests passing is not completion of those issue investigations.
- SFP canonical source was not available in the inspected branch; no promotion port
  or campaign/persistence changes were implemented.

## Resume safely

Read `AGENTS.md` and `docs/INSTALLER_STANDARD.md`; preserve all current tracked AND
untracked work before changing branches. Unrelated `mods/CustomSupplySliders/` is not
part of this handoff. Nothing was committed/pushed, and no remote issue should be
closed on these offline results. Update the shared installer rather than making
another per-issue kit. Do not deploy to the PC without the user's subsequent request.
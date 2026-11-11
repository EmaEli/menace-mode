# AIPerformance startup crash — 11 November 2026

Tracking: [#100](https://github.com/EmaEli/menace-mode/issues/100), parent #17.

## Rejected 1.12.0 player trial

Player reports crashing during loading immediately after 1.12.0 installation. This
capture never reaches the menu or a tactical scene. It is not evidence of save damage.
Game was closed during collection; mods, settings and saves were not modified.

- Latest.log: 56,341 bytes; SHA256
  `eec2dec28ed080f731d0a06ba2366ffd42b66c9c50146cde58759dbcce5af995`.
  Original, frozen snapshot and local download match.
- Player.log: 1,759 bytes; SHA256
  `8935a65131f6abbfa3816d4d8b0f8223d5d6ccd1f4fbe0da4a60ff1ca4cd9365`.
- Installed manifest/assembly: 1.12.0/1.12.0.0. DLL SHA256
  `33606b1bd03fa046daf0af97dd57f185bbe2e00e8d205a2e99c3e4b5e0fd25ed`
  matches the canonical Windows stage. Veterancy1.10.5 also matches its own install.
- Windows Application Error at 16:45:07+01:00: coreclr.dll, c0000005, offset0x3c129.
- Remote existing dotnet-dump reads only exception-thread frame names/addresses; no
  arguments, locals or registers requested. The raw process dump stays on the PC.
- Stack: MonoMod CompileMethodHook -> RuntimeHelpers.PrepareMethod ->
  AIPerformance.Plugin.PatchContactAdvance+406 -> PatchOffensivePlanning -> Initialize
  -> Jiangyu InitAll. The latest log ends after combined-arms patch initialization.

This specifically identifies the eager contact-helper preparation sweep as the failing
entrypoint. Managed catch cannot recover the native access violation. The exact helper
being prepared is not identified; normal on-demand compilation is not proven broken.
Distinct from Veterancy #99 and the older unattributed loading crash #63.

## 1.12.1 correction

Remove the speculative reflection/PrepareMethod loops from contact setup. Retain its
bounded native-free managed-state warmup, normal runtime compilation, all seven native
hook targets/phases/priorities/cleanup and the same AI gameplay policy/settings.

- New metadata-only production-binary IL gate rejects the retained 1.12.0 artifact at
  the exact forbidden eager-preparation path. It does not execute the game or reproduce
  the native access violation in a different host.
- Managed production setup tests register all seven hooks before a tactical manager
  exists, check opt-out, hook phases/callbacks, cleanup priority, no native-state reads
  and no fictional warmup contacts.
- Existing all-faction/contact/worker/native-action safeguards are tested unchanged.

Earlier successful local/Windows builds did not run the game and could not establish
startup safety. Corrected native startup and gameplay still require the player trial.

## Local validation

- New production-binary startup contract fails first against the retained 1.12.0 DLL
  and passes the 1.12.1 build; 224 total native/artifact contracts, metadata-only.
- 19,705 production-linked checks pass, including 25 initialization assertions; 303
  settings/planning behavior checks pass. These use managed stand-ins, not IL2CPP dispatch.
- 210 GameplaySuite Python tests and 156 LargerMaps Python tests pass (8 expected skips).
- Real-reference production Release build: zero warnings/errors. Main is already
  included in dev; unrelated working-tree changes are preserved.
- Canonical packaging and Windows build/install results follow below; player startup
  remains pending.

## Canonical candidate and Windows installation

- Source `65fa7e53761018c9c493e0d149169953165f75ce` is pushed to dev. Isolated archive
  from that commit excludes unrelated local edits. Matching generated AI/settings
  artifacts seed the standard installed-companion BuildKit; no installer changes.
- One package, 89 inventoried files. ZIP SHA256:
  `d0bf34ab689518fc62f566039309fa5e15fb6d2c431b38b0bbf598183456a17d`.
  New files include the two startup test helpers and generated AIPerformance.deps.json.
- Isolated packaging passes 17,986 shared/303 behavior/19,705 linked checks. Clean
  extracted-source compilation and real DLL metadata verification pass; the current
  verifier's 18,011 shared checks also pass. Existing nullable fixture warnings remain.
  Local SDK10.0.402; a snapshot-local pin avoids an unrelated parent temp SDK6 pin.
- Closed-game PC preflight verifies the original 1.12.0 crash DLL, reviewed native hash
  and all 177 local compiler/proxy files against the PC. The game remains 0.7.18+22828.
- Standard PowerShell5.1 BuildOnly and installed-companion standalone install pass with
  pinned private SDK10.0.113, zero production warnings/errors, 224 metadata/303 behavior/
  19,705 production-linked checks. BuildOnly leaves the original live AI unchanged.
- Installed manifest/assembly/file: 1.12.1/1.12.1.0/1.12.1.0. DLL SHA256:
  `8ae34b655c1301281cf80341dfe3a29019a8241cd7c10efaea555e26c42bc44f`.
- Journal/backup outside Mods:
  `_MENACE_Backups/20261111-170843-679259cdc5d240c19fe24290b90c63f7`.
  Both original 1.12.0 files match the preflight hashes; installed files match the
  validated Windows stage. Only AIPerformance was replaced; the loose-DLL conflict
  target was absent and untouched. Settings companion and Jiangyu.Loader were reused.
- All 1,462 protected unrelated Mods/UserData/save files and 177 references remain
  unchanged; native game hash unchanged; game closed during final verification.
- PC archive/evidence: `E:/MENACE-Work/Builds/AI1121-startup-20261111`.
  Canonical build logs: `E:/MENACE-Work/Temp/MENACE-install.log`; captured BuildOnly,
  install and final journal verification outputs are retained locally.

No corrected game startup, native on-demand AI dispatch, mission behavior or turn-time
acceptance has been performed. Player should launch to the menu first, then load a
mission if successful; retrieve the fresh Latest.log after their result. #100 and #17
stay open. No Nexus/release/Downloads package was requested or published.
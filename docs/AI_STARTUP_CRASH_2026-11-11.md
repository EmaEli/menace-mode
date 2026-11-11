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
- Canonical packaging, Windows build/install and player startup results follow separately.
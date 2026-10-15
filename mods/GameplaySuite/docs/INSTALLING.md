# Shared installer candidate (#34)

## Status

This is an **offline candidate**, not a Windows-tested release. The ZIP is assembled
locally from explicitly bundled source and existing compiled authoring data. Its SHA256
inventory detects accidental changes; it is not a digital signature or substitute for
checking the ZIP checksum against a trusted publication. No release is published yet.

## One logical distribution

The package contains `INSTALL-SUITE.cmd`, `kit.json`, and `source/`. The launcher runs
the canonical `tools/Install-IntegratedSuite.ps1`. Its default is to build **and install**
the independent Jiangyu packages plus the single MelonLoader camera DLL. No second ZIP
or manual file-copy step is required. Unrelated mods and ModpackLoader are preserved.

Prerequisites: the pinned MENACE 0.7.16+22323 native game hash, installed MelonLoader and
generated IL2CPP proxies, Internet access for the pinned Microsoft .NET and Jiangyu 1.4.8
downloads. Do **not** preinstall an SDK: the installer bootstraps SDK 10.0.113 automatically
under `<kit>\tools\dotnet`, using a pinned Microsoft script and SHA512-verified SDK archive.
It leaves the machine-wide SDK 6 and User/Machine environment untouched; build environment
changes are process-local and restored on exit. Downloads, disk checks and extraction are
logged; a matching private SDK is reused. Exact script/archive pins are in `kit.json.dotnetSdk`
and [the shared standard](../../../docs/INSTALLER_STANDARD.md#private-net-sdk-bootstrap-34-15-october-2026).
The Jiangyu archive and loader have separate SHA256 checks. Unity Editor is not required:
only code is rebuilt on the player's machine; authored data was compiled for the pinned
game. The game must be closed both at preflight and immediately before live changes.
The new 0.7.17 game build remains a separate, unpassed verification gate in #45; do not
relabel this candidate or its earlier 0.7.16 checks as a verified 0.7.17 release.

The scripts verify the bundled inventory, copy sources to a unique temporary workspace,
build/validate every selected project and stage all outputs before changing `Mods`.
Source `.csproj` versions are parsed as XML. Real DLL AssemblyVersion and FileVersion are
checked separately; MenaceModSettings retains assembly identity 1.0.0.0.

The console stays open on success/failure. Diagnostic log: `%TEMP%\MENACE-install.log`.
Failures open the log in Notepad. Backups and the rollback journal live under the game's
`_MENACE_Backups`, **outside Mods**. Rejected/partial new files are moved there, not deleted.

## Optional products (same installer)

- `-Product Camera`: settings companion, camera menu and single camera runtime DLL.
- `-Product Standalone -Modules Overwatch`: one feature plus shared settings/runtime.
- `-BuildOnly`: opt-in staging without live mod changes.
- `-RollbackPath <exact dated backup directory>`: restore verified originals. Manual
  rollback refuses changed live files; backups are retained. A legacy monolithic Suite
  requires the full Suite cutover rather than a partial install that duplicates plugins.

Pass `-BundleRoot <extracted ZIP directory>` and optionally `-GamePath <MENACE directory>`
when invoking the bundled PowerShell file directly. The double-click launcher handles
BundleRoot automatically and asks for GamePath only if discovery fails.

The installer does not write UserData or saves. Existing modern settings win over recognized
legacy settings. A legacy WRR override is carried into staging, where the existing runtime
migration can copy it to UserData only when the modern override is absent.

## Developer checks

Run the Python regression suite and compile the current Jiangyu packages/camera first.
`python3 mods/GameplaySuite/tools/build_kit.py --output <new ZIP path>` then checks actual
DLL metadata for every catalogued package and assembles a deterministic, inventoried ZIP.
It refuses existing output paths and excludes machine-local props, proxies and debug symbols.
The source snapshot includes local edits; `gitBaseRevision` is provenance, **not** a claim
that those edits were committed/published.

`python3 mods/GameplaySuite/tools/verify_kit.py <ZIP path>` verifies the final ZIP's inventory
and every payload hash. Add `--game-dir <cached game references> --sdk-dir <Jiangyu SDK>`
to rebuild the clean extracted sources locally and recheck their actual DLL metadata.
This writes only a fresh temporary source/build tree, not the game directory. It still
does not execute the Windows installer or test PowerShell transactions.

Still required on Windows: PowerShell 5.1 parsing/execution, full local builds, double-click
error logging, interrupted-copy rollback, standalone/full installs and startup/gameplay
acceptance. Source-wiring tests are not evidence that those checks passed.
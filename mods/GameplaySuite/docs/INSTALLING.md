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

Candidate build target (not gameplay acceptance): MENACE 0.7.17+22362 and its pinned native
game hash. Prerequisites: installed MelonLoader and
generated IL2CPP proxies, Internet access for the pinned Microsoft .NET and Jiangyu 1.4.10
downloads. Do **not** preinstall an SDK: the installer bootstraps SDK 10.0.113 automatically
under `<kit>\tools\dotnet`, using a pinned Microsoft script and SHA512-verified SDK archive.
It leaves the machine-wide SDK 6 and User/Machine environment untouched; build environment
changes are process-local and restored on exit. Downloads, disk checks and extraction are
logged; a matching private SDK is reused. Exact script/archive pins are in `kit.json.dotnetSdk`
and [the shared standard](../../../docs/INSTALLER_STANDARD.md#private-net-sdk-bootstrap-34-15-october-2026).
The Jiangyu 1.4.10 archive and loader have separate SHA256 checks. Existing authored data
retains its separately verified `compiledForJiangyu` version (currently 1.4.8). Unity Editor
is not required: only code is rebuilt on the player's machine; authored data was compiled for
the pinned game. The game must be closed both at preflight and immediately before live changes.
The 0.7.17 game build remains an unpassed gameplay verification gate in #45; do not
relabel this candidate or its earlier 0.7.16 checks as a verified 0.7.17 release.

The scripts verify the bundled inventory, copy sources to a unique temporary workspace,
build/validate every selected project and stage all outputs before changing `Mods`.
Source `.csproj` versions are parsed as XML. Real DLL AssemblyVersion and FileVersion are
checked separately; MenaceModSettings retains assembly identity 1.0.0.0.

### Scoped v0.7.18 test candidate (#86 / #62)

The catalog additionally permits standalone GarrisonableBuildings and the opt-in
SquaddieFieldPromotions 0.22.7 candidate on **0.7.18+22828**, native SHA256 `494c9e21…`.
SFP requires `-Product Standalone -Modules SquaddieFieldPromotions -UseInstalledSettings`:
its installed settings companion and pinned loader are verified, not replaced.
Fresh MelonLoader proxies and native metadata contracts were checked. SFP's B Team
templates were recompiled with pinned Jiangyu 1.4.10 for the new property array;
its canonical build also runs metadata-only native contract checks before staging.
SmokeOverhaul 0.8.4 is also allowed as a standalone code/locales-only candidate (#85).
All 121 proxy hashes match the PC, and its 12 hook signatures and smoke property contracts
are checked by a metadata-only test in the canonical build before staging. This permits
testing the native-stat fix and bounded lifecycle observations, not a claim that cloud
disappearance is fixed. No game-indexed templates/bundles need recompilation for this mod.
WeaponMechanicsRebalanced 0.6.1 (#59) can also use `-UseInstalledSettings`; its standalone
candidate validates and reuses the existing companion and loader, staging only WMR. Smoke
shells now use the HE Scatter baseline for their mortar size (light 3, 80 mm medium 4,
120 mm heavy 5); preview and shot ranging share the same baseline, with native template
restoration after firing. Its native contract checks include effective skill range, direct
actor/tile line of sight and `GetAvailableScatterTiles(Tile)` before staging. Actual
bullseye and smoke-range balance still need gameplay. Default-ON sustained enemy mortar AI
adds legal-fire priority and 2–4-round last-known-tile fire missions (default 3), with
production-hook managed checks and native AI contract checks. The 0.6.1 correction applies
the policy on the main thread before native execution; 0.6.0 did not sustain fire in the
reported gameplay test. Gameplay re-test remains pending.
CinematicActionCamera 1.2.3 is a scoped standalone code-only candidate for the controller
namespace move (#88). Its bundled checks validate autofocus behavior, native event
signatures and runtime binary type references before staging. Select
`-Product Standalone -Modules CinematicActionCamera` for this test.
Its 5 November Windows BuildOnly/install passed, with runtime and companion 1.2.3
metadata/hashes verified and all 1,397 protected Mods/UserData files unchanged.
Startup/gameplay acceptance remains pending; prior camera evidence is not a 1.2.3 test.
WeaponMechanicsRebalanced 0.4.15 is a scoped standalone code-only mortar correction (#59).
Its canonical build and clean-source verifier inspect native mortar hook/property contracts
and WMR binary type references before staging. No indexed templates/bundles are shipped.
InfantrySandbagCover 0.3.1 (#89) is an opt-in standalone construction test on this exact build.
It adds a C# runtime to the native self-tile sandbag skill: configurable work rounds (default
two), 80 AP per action, stationary progress and mortar-style tile markers. Its canonical build
runs managed production-hook/marker tests and metadata-only native contracts before staging.
Use `-UseInstalledSettings` to validate/reuse both MenaceModSettings and Jiangyu.Loader without
replacing either. Windows/gameplay acceptance remains separate from these automated checks.
LargerMaps 2.6.2 and AIPerformance 1.8.0 are independent standalone selections (#2/#93).
Neither requires or silently bundles the other; the default Suite explicitly selects both.
Each supports `--use-installed-settings` / `-UseInstalledSettings` to preserve settings/loader.
AIPerformance 1.3.0 adds a separate OFF-by-default experimental recent-contact approach
option for unassigned pirate/Rogue infantry on reviewed 0.7.18 (#17). Native detection
events supply coordinates; no hidden-position polling or mission-order replacement.
Three responders per faction maximum; no artillery targeting changes in this first stage.
AIPerformance owns conditional recovery (reviewed 0.7.17/0.7.18, actual maps above 64x64)
and runs managed recovery/planning and native proxy metadata checks before staging.
Its simplified planning remains ON; old Larger Maps AI settings are not migrated.
Version 1.3.1 also covers up to eight exclusively AdvanceTowards zones: the native
Entity.ID allocation chooses the zone, multi-zone distance scores are left unchanged,
and the entire ordered zone context is revalidated before utility/choice. Mixed or
defensive orders and arrival remain excluded.
Optional candidate limits and recovery diagnostics remain OFF. The exact scoped 0.7.18 hash
has a reviewed 28-site native pathfinding plan, including its inlined map-generator
grid fill (#94). Unknown hashes/conflicting instructions still fall back to 64x64.
Native gameplay acceptance remains pending; no whole-suite compatibility is implied.
The 1.7.1 gameplay retest still showed weak support. AIPerformance 1.8.0 adds
threat-directed vehicle origins independent of infantry survival, positive native attack/
artillery priority and guarded infantry route choices. Optional SmokeOverhaul 0.9.0
answers equipped-grenade requests through its existing executor. Each module supports
standalone `-UseInstalledSettings`; only that module is replaced, not settings/loader.
Smoke also runs linked runtime checks before staging. Canonical Windows BuildOnly/install
of both candidates passed on 9 November with zero production warnings/errors; affected-only
backups/installed versions verified and 1,445 protected files unchanged. Gameplay acceptance
is pending; [evidence](../../../docs/AI_THREAT_RESPONSE_2026-11-09.md).

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
# LargerMaps 2.6.1 — 0.7.18 native support test installation

Tracks [#94](https://github.com/EmaEli/menace-mode/issues/94). Related AI-speed work
is separate: #57 / #93. This is a private test candidate, not a public release or
gameplay acceptance. Source commit: `818163f` on local `dev`, **not pushed**.

## Scope and provenance

The user supplied `LargerMaps-v0.7.18-handoff.md` and `patch_largermaps.py`
from the PC's Downloads. Transfer hashes were verified in the preceding investigation.
The handoff credits @Daddy Slop Master and describes a 2.2.0 binary workaround.
We did **not** execute that script or alter an existing mod DLL on disk.
Instead, the reviewed changes were implemented in the current LargerMaps source.

- Exact game: MENACE **0.7.18+22828**, GameAssembly SHA256
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
- Added the eight method RVAs confirmed by the local IL2CPP dump; nine FindPath
  stride offsets are overridden only for this exact build.
- Added the inlined Map.GenerateMap state-machine grid-fill bounds at RVAs
  `0x6C3C27` / `0x6C3C32` (`83FB40` / `83FE40`). They now use the selected map size.
- All **28** instructions are checked before any memory write. Verification and
  rollback use the same complete selected plan. No game file is patched on disk.
- The three older builds retain their original **26-site** plans. Unknown hashes
  or conflicting instructions retain the safe 64x64 fallback; 64x64 installs no edits.
- AIPerformance remains **1.0.0** and is still bundled automatically. Its behavior
  is unchanged here. Advance recovery remains restricted to reviewed 0.7.17.

The handoff reports that an incomplete 0.7.18 patch leaves null Tile references
outside row/column 63 and can exhaust the 12-process pool. This is **not** a failure
observed in our previous 2.6.0 install, which correctly fell back to 64x64.

## Automated checks

- LargerMaps Python: **145 tests**, eight optional skips. The 13 native-plan tests
  also passed with MENACE_GAMEASSEMBLY pointing at the original 0.7.18 DLL.
- Linked production NativePatchChecks: **1,098 assertions** against 0.7.18;
  **1,096 each** against original 0.7.16 and 0.7.17 DLLs. These run actual production
  selection/RVA/immediate logic but never executable-memory writes.
- A canonical legacy-plan JSON digest proves all old manifest fields are unchanged.
- GameplaySuite Python: **198 tests passed**. Full C# regression/artifact runner:
  **18,297 assertions passed**. Scoped candidate/artifact runner: **18,018 passed**.
- AIPerformance: **25** managed production-behavior checks and **22** native proxy
  metadata contracts passed, also in canonical Windows build/install.
- Fresh PC comparison: **125** cached proxy/loader references matched, zero mismatches.
- Local Release build, 119-file/two-package BuildKit inventory and clean extracted-source
  rebuild/DLL metadata checks passed. One existing LargerMaps CS0169 unused-field
  warning remains. The standalone net6 test runner also emits its SDK end-of-support warning.

## Canonical Windows build and installation

The harmless SFTP upload/download round trip matched SHA256 on both machines.
The candidate ZIP and every extracted inventory entry were verified before execution.
Windows canonical **BuildOnly passed**, then the canonical scoped standalone install
passed with MENACE closed and private pinned .NET SDK **10.0.113**. No manual copying
into Mods was used. Both builds had zero errors and the same existing CS0169 warning.

- Candidate: `.tmp/MENACE-LargerMaps-2.6.1-818163f.zip`.
- ZIP SHA256: `fde0fec933a441f58f128d9b6e06db560fba388013949e106b0558a311754235`.
- PC staging: `E:\MENACE-Work\Builds\LargerMaps-2.6.1-native94-20261106a`.
- Installer log: `E:\MENACE-Work\Temp\MENACE-install.log`.
- Backup: `Menace\_MENACE_Backups\20261106-140022-82485c1eea854cacbdc9e029ec9a8fa6`.
  Its journal confirms only **AIPerformance** and **LargerMaps** were moved/installed.
- Installed LargerMaps manifest/assembly/file: **2.6.1 / 2.6.1.0 / 2.6.1.0**;
  DLL SHA256 `e0f46c1ec396389dc3103977b0904e5d4232179937ad1f26339fda1edbb8f17b`.
- Installed AIPerformance manifest/assembly/file: **1.0.0 / 1.0.0.0 / 1.0.0.0**;
  DLL SHA256 `991d15dfc398dabafd405ebf0bba0cb0b684626edf6639b51bcf04ba2da70800`.
- All **1,416** protected unrelated Mods/UserData files and **125** reference files
  retained their hashes; protected file count is unchanged. Installed settings and
  Jiangyu.Loader were validated/reused, not replaced. GameAssembly remained unchanged.

## Remaining acceptance

No 2.6.1 startup, native in-memory patch execution, gameplay, AI timing comparison
or rollback drill has been performed. Launch a new 96x96 mission, check movement
in extended rows/columns and several enemy turns. Retrieve the fresh Latest.log
after the user's result. Do not infer an in-game fix or speedup from these build checks.
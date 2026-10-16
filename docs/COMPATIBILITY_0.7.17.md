# MENACE 0.7.17 — candidate compatibility evidence (#45)

**Partial gameplay verification. Not a release.** The earlier 0.7.16 acceptance does not
carry over. No README/CHANGELOG verified-on line has been advanced.

**16 October:** [long-mission report](MISSION_VALIDATION_2026-10-16.md) adds real
rounds 1–18 / 459 AI activations and user acceptance of #43/#46. This supersedes the
older limited tactical coverage below, not the remaining feature/installer gates.
The new #47/#48/#49 source fixes were prepared afterward and were not in that mission.

## Actual inputs

- User launch: 15 October 2026; log last write 16:34:17 UTC.
- Exact version line: `[18:33:33.698] Game Version: v0.7.17+22362`.
- Main menu: `[18:33:56.037] [Menace_Data_Extractor] Main menu loaded`.
- Steam buildid observed: 25958176; **not yet an accepted/verified build marker**.
- GameAssembly SHA256: `871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`.
- Assembly-CSharp proxy SHA256: `fe8eea7fc2775db28fe48455685e3434057bdf84028c3ecc300351cc8c0646d9`.
- The 121 proxies were already regenerated at 16:19:44 UTC; the last launch reused
  them. Generator last/current SHA512 matches the independently hashed new binary.
- Ignored cache: `game_files/v0.7.17/`; `reference-manifest.json` records native,
  metadata, all proxies and runtime DLL hashes. Manifest SHA256:
  `a0e8d58483079c84ddd90b02ab97fd8c82aa6931eb7cd0ec9ed061319ae1e77c`.
- Local builds explicitly use this versioned cache, not the old `/tmp/jy/game` refs.
  The older cache and original dump were not overwritten.

## Restricted contract findings

Compared complete dumps from both original binary/metadata pairs, each containing
15,940 types. Inventory covers 83 Suite/shared/camera C# files, 8 KDL files and 187
source-named Menace types, including `MenaceSdk.cs` TacticalEventHooks. Checked all
existing declarations/field offsets as a superset to cover dynamic/string access.

No existing Harmony method signature, shim field/property type or KDL set-path
contract changed or disappeared. No field-layout changes. Only declaration deltas:

- Added private static `StrategyState.ShowLeaderPerResetDialog(BaseUnitLeader, int)`.
- `SaveState.SAVE_STATE_VERSION`: 125 → 126.
- Added `SaveState.LIM_PERK_RESET_VERSION = 126`.

Native pathfinding methods moved +0x3A0; `MissionTemplate.GetBounds` stayed at
0x60F630. Exact new RVAs are recorded in `pathfinding-sites.json`. All seven methods
were matched by declaration in both dumps; **26/26 original instruction patterns**
were checked against actual 0.7.17 PE bytes. Only the build map/fingerprint changed;
patch descriptors, algorithm and old build maps are untouched. Unknown builds
still fall back to 64x64. This is not evidence of successful live native patching.

## Checks completed locally

- 17 code packages and camera runtime rebuilt against the new proxy cache.
- Both data packages rebuilt with Jiangyu CLI; unchanged KDL contracts above.
- Actual DLL assembly/file versions, manifests and dependencies checked for all
  19 packages and camera; MenaceModSettings assembly identity remains 1.0.0.0.
- 96 GameplaySuite Python tests passed.
- 116 LargerMaps Python tests passed, 4 optional checks skipped, with real 0.7.17
  GameAssembly/dump inputs. Native simulation changes exactly 26 bytes.
- 15,681 C# offline checks passed. These do not execute Harmony/IL2CPP gameplay.

## Initial installed-mod findings and remaining gates

The initial launch used the **old installed builds**, not this candidate. Two ERROR
lines describe one external Custom Leader Harmony failure on `BlackMarket.FillUp`.
LargerMaps correctly fell back to 64x64. StartingOptions' property-first lookup also
probes an unused field fallback and emits a warning; no new native field removal
was found. External mods and CustomSupplySliders are not modified by this work.

## Windows checks

Canonical PowerShell 5.1 Suite build-only completed against the actual installed
0.7.17 proxies: 19 packages plus camera runtime, all real DLL/version checks passed,
zero compiler errors, and `BUILD-ONLY: no live mod changed`. The local SSH wait timed
out at 40 seconds; completion was independently confirmed in the remote transcript,
not inferred from the interrupted local output.

Candidate ZIP: `bdeb17d1798dade1acafee3c0152c00213a9b0af23905bf89ff9a35b5fb13f99`,
279 inventoried payload files; source base `12446f9`. Clean extracted-source rebuild
and real DLL metadata checks passed locally against the same fresh cache.

`Test-DeploymentTransactions.ps1` passed on Windows against the production helpers:
synthetic install, recognized legacy alias retirement, unrelated mod/settings/save
preservation, explicit hash-verified restore, and injected copy failure followed by
automatic verified rollback. Only a new temporary synthetic game tree was used;
its fixtures/backups were retained. This is not a live-game crash-recovery claim.

The canonical Suite installer then rebuilt and installed all 19 packages, camera
runtime and pinned Jiangyu loader, with every staged/live tree hash-verified. Backup:
`_MENACE_Backups/20261015-190105-332f429947994ae78a4c42c76707f0cc/`, including the
rollback journal and recognized replaced/legacy originals. No manual partial copy
or deletion was used. Before/after content hashes confirm **748 protected files**
(UserData and unrelated mods, including CustomSupplySliders) unchanged. The game
was closed during installation. Installer never
writes save directories. Earlier save acceptance is not carried forward.

## First candidate title check — passed, gameplay still pending

User launched to the main menu and exited. Retrieved the fresh log directly from
the PC; last write 15 October 2026, 17:21:04.5196602 UTC, 82,643 bytes. Native binary
SHA256 is unchanged from the validated input above. Log SHA256:
`2401de31a9f070776aad5604764d44ce0c015dd79b6ce16aeda50e0cbdb0c116`.

- Game version: `v0.7.17+22362` at 19:20:31.640 local time.
- Larger Maps: experimental **96x96 backend active**, 128x128 pathfinder and
  verified native instructions at 19:20:41.415. No 64x64 fallback message.
- Main menu reached at 19:20:54.431. All 19 package names appear in the log.
- Two ERROR lines are the already-known external Custom Leader Harmony failure
  on `BlackMarket.FillUp`. No new Suite errors were found. Nine warnings remain,
  including the known StartingOptions field fallback warning. All eleven current
  ERROR/WARNING entries match the original pre-install log after timestamp/native
  address normalization; none is a new diagnostic. This is not an error-free log.
- Game confirmed closed. No save was loaded and no tactical turn tested.

## Tactical test — AI progression confirmed from the log

Retrieved the subsequent test log: 15 October 2026, 17:36:45.3558431 UTC,
234,496 bytes, SHA256
`2cefcd029c66e338bcd00831dea1bf5ac1b5628333b346a08d02bdc40007fe5e`.
The initial analysis incorrectly said AI turn completion could not be confirmed:
its overly narrow event search missed the `AI PERF turn=#...` summaries. The
following corrected findings come from this same log, not a repeated user test.

- 64 consecutive actor-activation summaries, numbered 1 through 64. These are
  individual AI activations, **not 64 complete faction turns or rounds**.
- Round 1: 16 activations; round 2: 22; round 3: 25; round 4: 1.
- First summary: 19:34:05.479, round 1. Round 2 appears at 19:34:59.986;
  round 3 at 19:35:33.258; round 4 at 19:36:11.612.
- All 64 report `pending=0` and `droppedTasks=0`. Closure reasons: 60 `finish`,
  4 `next-activation`. Confirmed their semantics in `AIProfiling.cs`: emitted
  summaries track actor activations and the actual tactical manager round.
- This session progressed beyond the first AI round: the first-round stall did
  not reproduce. This does not close #44 or prove every gameplay regression fixed.
- Larger Maps remains 96x96 with no fallback. Two ERROR lines remain the known
  external Custom Leader failure. Twelve warnings include three additional
  scene-related entries versus title-only; these are not yet classified.

The title and tactical AI-progression portions of step 5 passed for this session.
Save-load provenance and the feature-specific gameplay checks remain pending.
Minimum: clean Suite title startup, load save, end turn/AI turn 1 (#44), WRR
debug range pass with zero write failures then debug OFF, OW/CF/Smoke activations,
and Field Logistics resupply. Only afterward record a verified buildid and implement
the mismatch warning/explicit-confirmation installer gate (not a blanket hard block).
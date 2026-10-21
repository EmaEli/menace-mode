# MENACE migration status — 15 October 2026

**20 October consolidation:** work now uses only `main` and `dev`; see
[the current consolidation record](CONSOLIDATION_2026-10-20.md). Older release-gate
language below is historical, not a requirement to re-test every feature before
integrating accepted gameplay source. The source inventory is not a fresh PC audit.

Tracks [#29](https://github.com/EmaEli/menace-mode/issues/29),
[#31](https://github.com/EmaEli/menace-mode/issues/31),
[#33](https://github.com/EmaEli/menace-mode/issues/33),
[#34](https://github.com/EmaEli/menace-mode/issues/34) and
[#38](https://github.com/EmaEli/menace-mode/issues/38).

**Current checkpoint:** [daily wrap-up](DAILY_WRAP_UP_2026-10-15.md) and
[0.7.17 evidence](COMPATIBILITY_0.7.17.md). Source is committed/pushed on the offline
branch; the fresh-proxy candidate is installed on Windows. Canonical build/install,
synthetic rollback, title/native 96x96 activation and tactical rounds 1→4 passed their
scoped checks. Full feature/save/installer acceptance remains open. **Not a release.**

**16 October update:** [long-mission evidence](MISSION_VALIDATION_2026-10-16.md)
records 459 AI activations across rounds 1–18 and user acceptance of #43/#46.
The source inventory includes the subsequent #47/#48/#49 candidates below; these
three new versions are locally built, **not deployed or game-verified**. The report
separately lists every installed version actually observed in the long mission.

The package inventory below remains current. The earlier offline evidence and
remaining-gate descriptions are a historical checkpoint, superseded where the two
linked documents record subsequent Windows/game checks. Older ZIPs are not the
current installation target; use the exact 0.7.17 candidate recorded in the wrap-up.

## Current source/package inventory

The authoritative package list is `mods/GameplaySuite/tools/distribution.json`.
All 19 source/compiled manifests and actual code DLL versions/dependencies were checked
locally. There are 17 code packages and two data-only packages; the camera runtime is
an additional MelonLoader DLL built from its source, not a twentieth Jiangyu package.
KDL counts below are **source files**, not patch-operation counts.

| Package | Version | Runtime / data authoring |
| --- | --- | --- |
| MenaceModSettings | 0.8.2 | Jiangyu settings/UI; indexed stock-read fix (#65), canonical Windows BuildOnly/install verified 2026-10-21; 0.8.2 gameplay pending; stable assembly identity 1.0.0.0 |
| BuildingCombatRules | 0.3.0 | Shared SuiteSystem host, Harmony/runtime; tree protection candidate #48 |
| BuildingsBlockLineOfSight | 0.2.3 | Shared SuiteSystem host, Harmony/runtime |
| CoordinatedFire | 1.6.11 | Shared SuiteSystem host, native attacks/HUD |
| CoverDestructionRebalance | 0.4.1 | KDL tiers + runtime light/medium mortar impact guard; #58 gameplay verification pending |
| CustomSupplySliders | 0.2.1 | Shared SuiteSystem host; difficulty-menu slider ranges |
| FieldLogistics | 1.0.11 | Shared SuiteSystem host + 4 KDL files; ammo support |
| ForestTraversal | 0.2.0 | KDL only, 1 source file |
| GarrisonableBuildings | 1.2.0 | Shared SuiteSystem host; #62 native deployed-stance accuracy mirror, live comparison pending |
| InfantryAssault | 0.5.9 | Shared SuiteSystem host + 2 KDL files; defensive suppression fix candidate #47 |
| LargerMaps | 2.3.2 | Jiangyu system; recovery ON-default, selective positioning OFF; Windows installed and ON/OFF gameplay compared; user accepted the bounded gameplay improvement and MEN-002 is closed; distinct slowdown #57 remains separate |
| Overwatch | 0.32.3.9 | Shared SuiteSystem host, native reactions/HUD; JIT guard plus bounded derived-weapon source fallback; ARC Range and PPP CQB verified in live Overwatch, K-PAC with GL pending |
| SmokeOverhaul | 0.7.0 | Shared SuiteSystem host; bounded AI smoke rules retained, free infantry grenade grants opt-in OFF (#51); package delivered to Downloads, installation not verified |
| SquaddieVeterancy | 1.10.2 | Shared SuiteSystem host, native hooks/HUD/save sidecars |
| StartingOptions | 0.1.1 | Shared SuiteSystem host, new-campaign settings |
| StrongerCover | 3.0.1 | Jiangyu system, SDK prefix + runtime inside-cover settings |
| VisibilityOverhaul | 3.2.0 | Jiangyu host, native visibility and opt-in pirate diagnostics |
| WeaponMechanicsRebalanced | 0.4.14 | Shared SuiteSystem host, skill/ammo/mortar hooks; #59 enemy-only skill coverage plus muted impact-gated ranging feedback (#61); Windows installed 2026-10-19, gameplay check pending |
| WeaponRangesRebalanced | 2.9.2 | Shared SuiteSystem host, ranges/linked skills/override JSON |
| CinematicActionCamera | 1.2.2 | Windows test install and post-install startup log verified on MENACE 0.7.17+22362; manifests/DLLs verified, settings preserved, backup retained; no camera startup errors/warnings; positive user feedback but explicit allied Jetpack auto-follow acceptance still pending (#60) |

Experimental package (not included in the default Suite): **SquaddieFieldPromotions 0.12.1**.
The user accepted the generic infantry perk tree in 0.12.0, but rejected source-emblem badges
and reported Singh's tactical overhead icon (#67). The 0.12.1 candidate restores black dynamic
letter shields and overrides the two native infantry badge getters only for SFP leaders.
Source revision: `59a422f` on `dev`. Local real-reference build (zero warnings/errors), 202 C#
checks (native/Unity APIs simulated), 16 Python authoring tests and BuildKit verification passed.
Canonical Windows BuildOnly and installation passed on MENACE 0.7.17+22362 with private SDK
10.0.113: 202 behavior checks plus 6 installed-settings helper checks, zero build warnings/errors.
Installed manifest/assembly: **0.12.1 / 0.12.1.0**; only SFP replaced, installed settings and
Jiangyu loader reused. No saves or UserData modified. Backup/rollback journal outside Mods:
`_MENACE_Backups/20261021-121656-c94708eb12784d92baff3f0b895e8fcc`.
Candidate ZIP SHA256: `7ba269f1e757a7caa1c9689351cc932cc4858489910e9885d688006395ed2f8c`.
Installed DLL SHA256: `1987bbfe60b22880adc0733a5ab1dfa018dbeef47e9a42f9f78544d7bf45a5b5`.
Installer log: `M:\SFP-0.12.1-20261021-Build\Temp\MENACE-install.log`.
**No 0.12.1 startup or graphical acceptance yet**; existing promoted leaders should rebuild
black letter badges on relaunch/load, including the native tactical overhead path.

**21 October — SFP #68 voice-pitch prototype integrated into `dev`:** merge `f6edd42` brings
in five persisted, deterministic pitch presets shared by tactical and Hire barks; old sidecars
default to neutral. This is source-only WIP, not the installed 0.12.1 candidate. The merged source
builds against local MENACE 0.7.17 references with 0 warnings/errors; 217 C# behavior checks
(native/Unity APIs simulated) and 17 Python authoring tests pass. It has **not** been Windows-built,
installed or play-tested. All ten gender/pitch profiles currently initialize eagerly, creating
about 780 conversation clones; load-time and memory cost need measurement before packaging. Keep
issue #68 open and do not deploy this prototype.

## Evidence levels: do not confuse them

- **Current local builds:** both changed icon packages rebuilt through the Jiangyu CLI;
  all 17 code packages plus camera rebuilt from the clean extracted candidate sources
  with the hash-verified Windows CLI SDK. Both KDL-only payloads were integrity-checked.
  This ran locally, not on Windows; existing compiler warnings remain.
- **Migration checkpoint before AI follow-up:** 72 Python tests and 379 C# checks
  passed, including actual DLL metadata/binary references. Its migration ZIP has
  276 files / 19 packages; it intentionally does not contain Smoke Overhaul 0.6.4.
  The subsequent regression review is in `REGRESSION_REVIEW_2026-10-15.md`.
  AI follow-up source/build/acceptance evidence is tracked separately; no game gate
  has been waived by increasing offline test coverage.
- **Current offline checks:** actual DLL AssemblyVersion/FileVersion, compiled/source
  dependencies, minimum dependency versions, pinned Jiangyu payload target, settings
  assembly-reference ABI and no binary ModpackLoader dependency in the Jiangyu DLLs.
- **Earlier startup/session evidence:** #31 records the 07:10 session and template
  self-check for earlier feature versions. That is not proof that today's changed
  HUD/Overwatch/Veterancy/camera/settings/icon candidates work in game.
- **Earlier targeted evidence:** #38 confirms WRR 2.9.2 changed ranges in a loaded
  mission after the template-loader fix. WMR/FL template-dependent behavior was not
  fully play-tested there. #32 is closed with recorded LargerMaps 2.0.1 64/80/96-map
  evidence; #14's water rendering problem remains separate and open.
- **Unperformed here:** Windows builds; PowerShell 5.1 parsing/execution; launcher/log
  behavior; fault-injected install/rollback; standalone/full cutover; all gameplay.

## Shared contracts and persistence

- StrongerCover registers `StrongerCover/EnableGameplay` and `BuildingCover` (30%).
  Garrisonable Buildings reads those keys while cover is loaded/enabled, otherwise its
  own `Garrisonable Buildings/BuildingAccuracy` (40%). Changing the base updates the
  saved restore value; actual settings code is tested with an in-memory store, while
  IL2CPP template writes/handler ordering remain runtime checks.
- WRR's current override path stays `UserData/WeaponRangesRebalanced/weapon-overrides.json`.
  Existing modern files win; legacy `Mods/WeaponRangesRebalanced/weapon-overrides.json`
  is copied once when the modern file is absent. The installer stages the legacy file
  for this existing runtime migration rather than modifying UserData during install.
  Disable/type toggles restore only WRR-owned weapon/linked-skill values.
- Veterancy persistence is unchanged against main: `ExperienceStore.cs` still uses
  `UserData/SquaddieProbe/Saves/<save-name>.squaddieveterancy.json` and existing legacy
  migration paths. The display-name change does not change IDs, crew keys or XP schema.
  This is a source audit, **not** an on-disk or real-save round-trip test.
- MenaceModSettings 0.5.0 keeps existing group/key aliases and stores preferences at
  the established path; import fills only missing values, preserving modern saved
  defaults and corrupt-file protection. No user preference/save files were read.

## Distribution and runtime assumptions

- Suite is a convenience install of the **same independently versioned packages** as
  standalone, not a merged DLL. Standalone selection always includes settings first;
  selecting the camera companion also builds/stages its MelonMod runtime. A partial
  standalone cutover refuses the known legacy monolithic Suite.
- Reuse `INSTALL-SUITE.cmd` / `Install-IntegratedSuite.ps1`; see the installer standard
  and `mods/GameplaySuite/docs/INSTALLING.md`. Do not create per-issue installers.
- Pinned game: 0.7.16+22323, native GameAssembly SHA256 in distribution.json. Existing
  MelonLoader/game proxies are required; historical baseline is 0.7.4-ci.2607 with
  Unity 6000.0.82f1. Installer file-presence checks are not a full MelonLoader API audit.
- Jiangyu CLI/SDK/loader: 1.4.8 public Windows archive with SHA256 plus independent loader
  hash. .NET 10 SDK compiles the net6-targeted mods; MelonLoader supplies the game runtime.
  Bundled precompiled templates avoid requiring Unity Editor for a code-only rebuild.
- Keep ModpackLoader for third-party SquadSizeModifiers/CustomSupplySliders. Those are
  not silently migrated or removed. Unrelated untracked CustomSupplySliders work is
  left untouched. SFP canonical 0.7 source is unavailable in this branch; earlier 0.6.1
  startup evidence is not a 0.7 port. No SFP save/promotion implementation was added.
- A verified Suite release version/cutover is still pending; snapshot hashes identify
  offline candidates. Do not market the current working tree as a finished Suite release.

## Remaining release gates

Use `mods/GameplaySuite/docs/TESTING.md` for per-feature checks. In addition:

1. On a disposable Windows install, test build-only and full/standalone install, camera
   ownership and load order; failure before first move must leave Mods unchanged.
2. Inject a partial-copy failure and exercise automatic/manual rollback; verify backups,
   logs, console persistence, unrelated mods and user data before claiming safety.
3. Run a new campaign AND existing saves, tactical player/AI turns and mission transitions;
   compare settings, override precedence, XP/IDs/crew and before/after sidecar hashes.
4. Check 64x64 and 96x96 map performance independently; no AI improvement claim without
   a controlled same-save/seed comparison. Keep detailed runtime diagnostics OFF normally.
5. Publish source/artifact provenance and verify the downloadable launcher/checksums only
   after acceptance. Keep #29/#31/#33/#34/#38 open until their own gates are met.
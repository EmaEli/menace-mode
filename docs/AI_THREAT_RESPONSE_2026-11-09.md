# Threat-directed support candidate — 9 November 2026

Related: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#13](https://github.com/EmaEli/menace-mode/issues/13),
[#1](https://github.com/EmaEli/menace-mode/issues/1).

## Why another policy change

The previous support fallback depended on a ready infantry anchor and mostly rewarded
rear/local positioning. Remembering a dangerous MG did not itself direct a vehicle toward
it. Healthy rifle squads could not use the proactive Smoke Overhaul path: that path
required a flamethrower. Suppressing generic advance scores alone did not veto a native
Move choice through the remembered firing lane. Movement alone is not support acceptance.

## Implemented, not gameplay-proven

- **AIPerformance 1.8.0:** threat-directed crewed vehicle origins may be ahead of infantry
  and do not need a surviving infantry anchor after legitimate threat observation. Native
  damage evaluations against a currently detected threat can compete against unchanged
  native danger. Uncertain-position staging must be no more dangerous than the current tile.
- Four existing local origins, two weapons, eight setup-aware native probes maximum;
  shared actor/faction planning budgets and a soft 10 ms start limit. Native Move retains
  pathfinding, movement costs, setup/stance/fire reserves and execution.
- Positive native immediate attack valuations against an observed, currently detected
  threat tile receive 2x priority. This includes native artillery valuations, but is **not**
  a new mortar/HE executor, hidden-tile bombardment, guaranteed kill or suppression policy.
  Native range, deployment, blast/friendly-fire evaluation and target legality remain.
- Forward infantry Move choices into remembered firing lanes are held unless native
  danger decreases or inspected destination/transit waypoints are protected by terrain or
  actual smoke. Useful attacks and emergency/native non-Move behaviors are retained.
  At most 16 native waypoints, 32 LOS probes, soft 5 ms; missing/over-budget route evidence
  is conservative. This does not certify every interpolated segment or make smoke immune
  to fire. Multiple remembered threats are checked, not just the nearest.
- **SmokeOverhaul 0.9.0:** optional request bridge lets ordinary advance-ordered rifle
  infantry use an equipped smoke grenade toward the threat, preferring an adjacent forward
  tile. The existing main-thread executor owns the one-attempt-per-turn guard, remaining
  uses, AP, target legality and native Use result. No grenade/equipment/ammo is granted.
  Real successful smoke can enable a forward step; no companion means no imaginary cover.
- CombinedArmsSupport remains opt-in, restart required; fast planning stays enabled.
  Existing defensive smoke and its previously authored attacker-bearing correction are
  retained. No saves, settings, Overwatch or unrelated mod behavior is changed.

## Local validation

- Current native hash: `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`
  (MENACE 0.7.18+22828). All 121 PC proxy hashes match local references.
- Both production Release builds: zero warnings/errors.
- 208 GameplaySuite Python tests; 13,063 linked AI/recovery checks; 302 AI behavior checks;
  21 linked smoke-runtime checks; 193 AI and 49 smoke native metadata contracts pass.
  These are managed stand-ins/metadata checks, not IL2CPP dispatch or gameplay evidence.
- Selected shared regression/artifact check: 18,027 pass for settings + both modules.
  Each individual kit: 18,011 pass. These totals overlap and must not be added together.
- Both SHA256-inventoried BuildKits pass integrity and clean extracted-source builds.
  Smoke standalone now supports verified installed settings/loader without replacement;
  canonical installer and clean verifier execute its native and linked support checks.

## Candidate provenance

Feature source: `d94b5c6`; own packaging/test changes: `af5e4a5` on `dev`.
Unrelated/pre-existing dirty edits were retained, not committed with this feature.
The kits are exact inventoried working-tree snapshots (their base revision is provenance,
not a claim that every bundled pre-existing edit belongs to these commits).

| Candidate | Files | ZIP SHA256 |
| --- | ---: | --- |
| AIPerformance 1.8.0 | 72 | `acfd7b7b8da0b3617018222a2a39c7798b01c8d070d4f6b4413a36aa5e823f0b` |
| SmokeOverhaul 0.9.0 | 110 | `3f434d8c326aff606c545076f10a3020edf79a67911553fce66005c1ddd19639` |

PC work: `E:\MENACE-Work\Builds\ai180-support-93n9TZ`.
Harmless SFTP upload/download SHA256 round trip passed before archive transfer.
Native/proxy/version checks and baseline inventory completed with the game closed;
1,445 unrelated Mods/UserData files are protected by count and SHA256 comparison.

## Windows and gameplay status

Both canonical Windows BuildOnly gates passed on private SDK 10.0.113 without replacing
live mods; the completion marker and per-module logs are retained in the PC work directory.
Both canonical standalone installs then passed with zero production warnings/errors.
The installer reran 193 AI native contracts, 302 AI behavior and 13,063 linked AI checks;
Smoke reran 49 native contracts/12 hook signatures and 21 linked runtime checks.

| Installed module | Manifest / assembly / file | DLL SHA256 |
| --- | --- | --- |
| AIPerformance | 1.8.0 / 1.8.0.0 / 1.8.0.0 | `2DFA11CF393E887959AEAB4D66A7995540429ACF4D534300F5D303DCCACB958C` |
| SmokeOverhaul | 0.9.0 / 0.9.0.0 / 0.9.0.0 | `79AF316500FF3C6AED80328A57741998D5C1E0FD49898502608FE21220392E93` |

Affected-only journals and every retained-original/installed inventory hash were verified:

- `_MENACE_Backups\20261109-171852-09a7afff4e1049f68b27c0f647ed0aa6` (AI).
- `_MENACE_Backups\20261109-171909-d5cd0473f8ab4671b3483e0fc7121c01` (smoke).

All 1,445 protected unrelated Mods/UserData files are unchanged. MenaceModSettings 0.8.2
and Jiangyu.Loader were reused, never replaced; saves/settings were not modified.
The game was closed at installation and final verification. Local full installation
output: `.tmp/ai180-support.93n9TZ/windows-install.log`; per-module transcripts remain
in the PC work directory; the canonical transcript is `E:\MENACE-Work\Temp\MENACE-install.log`.
The initial private verification wrapper had a PowerShell 5.1 JSON-array counting error;
correcting that read-only wrapper passed the original 1,445-file inventory unchanged.
It caused no build/deployment or game-file change. This was not an installer defect.

Startup, native hook dispatch, the MG encounter, support usefulness and turn times have
**not** been tested on these versions. Issues #1/#13/#17 remain open.
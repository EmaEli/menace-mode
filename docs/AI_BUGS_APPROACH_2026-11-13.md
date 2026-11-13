# Bugs approach — 13 November 2026

Tracking: [#102](https://github.com/EmaEli/menace-mode/issues/102), related #2/#17.

## Player feedback and current mission evidence

Player reports bugs frequently moving only one tile. Requested behavior is rapid
closing toward troops, then a legal native assault or ordinary attack.

- Read-only capture while MENACE is running; installed AI manifest/assembly
  1.14.0/1.14.0.0, DLL SHA256
  `509c45a2a076908e66cdd79694b1c75d2a7ff7bb7c253d7f0f4e5594cf3b56a6`.
- Latest.log snapshot: 13,020,516 bytes, SHA256
  `1810620ba31d664c8a749e4a40a6651d1a09cc005bae50b29748fbc506e68a50`.
  Harmless SFTP upload/download and snapshot hashes match. Retained locally at
  `.tmp/bugs102-latest-20261113.log`, remotely at
  `E:/MENACE-Work/Builds/Bugs102-20261113/Latest-snapshot.log`.
- Round 8 spiderling `2072923D6C0`: Move from (36,24) to (36,25), then Idle
  with 104/120 AP, movementDone/hasMoved true and native jump-attack score zero.
- Dragonflies `206997DE480` and `20699D06B40` similarly move one tile, then
  Idle with 82/100 AP. Stingers also show one-tile moves then Idle with 84/100 AP;
  other stinger records select positive ordinary attacks. This is not total AI inactivity.
- Faction 7 has legitimate contact reports and all 39 eligible actors assigned in
  the captured round-6 turn-alert. The existing march correction sometimes selects
  long paths, but does not cover every engagement situation.

Source-confirmed policy gaps: MarchChoice returns native movement upon own sight
of any enemy, and rejects all destinations reserving nonzero attack AP. Equal-utility
attack origins also retain the native cheap-step winner. The log does not expose
each candidate list/own-sight result, so these are reproduced code gaps consistent
with the symptom, not proof of the precise branch for every captured actor.

## AIPerformance 1.14.1

- Only bugs (native faction 7) retain contact approach when troops are visible.
  Other factions' own-sight tactical-positioning guard is unchanged.
- Re-rank existing native affordable candidates without the short-travel preference;
  allow real nonnegative attack reserves only when movement plus reserve fits AP.
  Equal-merit bug destinations prefer more forward progress toward known contact.
- Retain useful native actions/attacks, mission/defense/deployment restrictions,
  danger and mission-distance scoring, actual paths/costs and fog of war. No new
  pathfinder, forced skill execution, extra AP, movement reset or hidden tracking.
- Existing native positive jump-assault and ordinary attack selection stays first.
  Managed tests do not execute actual native movement/attack sequences.

## Validation and deployment status

- Failed-first regression reproduces visible-contact one-tile choice. Production-linked
  checks then cover full progress, equal-utility attack lanes, exact AP boundary,
  negative/overflow/sentinel reserve, native danger/mission costs, useful emergency/
  attack priority, hidden-coordinate guard and non-bug isolation.
- 19,804 linked managed checks pass. Production Release build has zero warnings/errors.
- 303 AI behavior and 232 native metadata contracts pass. Python: 210 GameplaySuite
  tests pass; LargerMaps runs 158 tests with eight expected skips. These do not execute IL2CPP.
- Source `d5d5115` is pushed on dev. The isolated canonical AI-only installed-companion
  BuildKit passes 17,991 shared artifact checks; clean extracted-source rebuild and
  the current verifier pass 18,011. Existing nullable warnings are in test fixtures,
  not the production DLL build. Package inventory: 90 files, one module.
- Candidate `.tmp/AIPerformance-1.14.1-bugs102-20261113.zip`, SHA256
  `220a1521f60076b05edf5498bb5f2da06f1c33a6c2dffba597f02f46ec4a1dfb`.
- All 177 current PC proxy/runtime reference hashes match local build inputs;
  reviewed native SHA256 is
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
  Jiangyu 1.4.10 CLI archive matches the pinned catalog SHA256.
- Canonical Windows BuildOnly and AI-only installation passed with MENACE closed on
  13 November: .NET 10.0.113, zero production warnings/errors, 232 native metadata,
  303 behavior and 19,804 linked managed checks. Manifest/assembly/file versions
  verified as 1.14.1/1.14.1.0/1.14.1.0. Installed DLL SHA256:
  `ead1c6864c5cd3d96fb1b862301d3b5e4848a50f52f7fc238b850d6924f981b2`.
- Verified original backup and installed hashes against the transaction journal:
  `M:\SteamLibrary\steamapps\common\Menace\_MENACE_Backups\20261113-144041-9115b8845f1946c1b299f5cf1fa0ebaf`.
  Only AIPerformance was replaced; settings/loader were reused. All 1,479 protected
  unrelated Mods/UserData files and 177 compiler references remained unchanged.
- Windows evidence and per-run logs:
  `E:\MENACE-Work\Builds\Bugs102-ExtractFix-20261113`; full installer transcript:
  `E:\MENACE-Work\Temp\MENACE-install.log`. Local build/install captures:
  `.tmp/bugs102-windows-buildonly.log` and `.tmp/bugs102-windows-install.log`.
- Earlier #34 extraction failure initially blocked deployment. On the user's request
  to resolve and proceed, the same pinned CLI archive passed every ZIP-entry stream
  read and standard PowerShell Expand-Archive in isolated staging. The analyzer was
  present, SHA256 `1a3499a6b8c4cdc3f707783e1ce6efdd8a60ede8b77af880ba55fde680ba7ed0`.
  Both subsequent canonical runs passed extraction without installer changes,
  alternate extraction or security changes. The historical failure's cause remains
  unconfirmed; neither quarantine nor an installer defect was established.
- Post-install `Latest.log` was retrieved after a SHA256-matched SFTP probe and verified
  against the PC copy: 4,797,689 bytes, SHA256
  `af4271a25c530a8bccfd4857446db1cf1180121dba6bdb1299b0e0244a0e2778`. Retained locally
  at `.tmp/bugs102-postinstall-Latest.log`.
- The log confirms AIPerformance 1.14.1 loaded (`1 system`), with no AIPerformance
  warning/error/exception entries. Contact-advance produced 108 march-choice records
  over rounds 2–5: 107 selected a different tile, usually spending most or all of the
  available AP budget; one retained its current tile with 96/100 AP. Round summaries
  show contact counts 3, 9 and 11 in rounds 3–5 and faction-7 assignments 43, 43 and
  40. This supports that the new long-approach selection is active, but the trace logs
  choices, not confirmed native movement completion or attack execution.
- The same session has 36 repeated WMR mortar warnings reporting `Structure` to `Actor`
  invalid casts, plus a mortar tile-highlight IL2CPP class-pointer warning. These are
  separate from AIPerformance and merit their own investigation; no AIPerformance
  exception was logged. A TacticalRoundCheckpoint warning says its capture-only probe
  is disabled on this unsupported native build.
- Startup and native attack/assault outcome are not established from this log. Issue
  #102 stays open until the player confirms useful closing and attacks in game.
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
- Windows BuildOnly/install and actual native gameplay are pending. MENACE is running;
  no live mod/settings/save files were changed. Last installed AI remains 1.14.0.
- Local canonical packaging checks passed. Resume only with the game closed and the
  shared installer gates; never copy the DLL manually. Issue #102 stays open until
  the player confirms useful closing/attacks in game.
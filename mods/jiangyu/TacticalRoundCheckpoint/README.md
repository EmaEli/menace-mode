# Tactical Round Checkpoint 0.1.0 — capture-only prototype (#83)

**NOT an autosave mod. It cannot resume or load a battle.** This opt-in experiment
records partial tactical observations so the full-round boundary can be verified
before attempting a custom battle save/load system. Never rely on it for recovery.
It is excluded from the default Suite.

## What it does

- Hooks the end of native `TacticalManager.NextRound()`, after actors/structures
  refresh and native actor-state updates finish. Unit activations do not trigger it.
- Records round, mission ID/seed and each faction roster's template, tile, health,
  AP, morale, suppression, turn-done and containment observations.
- Retains three rotating JSON slots **per battle, per process session**, under
  `<game>/_MENACE_Diagnostics/TacticalRoundCheckpoint/<session>/<battle>/`.
- Labels every file `CanResume: false` with the missing state categories. Roster
  indices are diagnostic identifiers, not stable cross-load entity identities.
- Does not call native save/load, change actors, pause play, change visibility,
  expose enemy positions in the UI, or write into Saves or UserData.
- Disables itself with one warning on an unsupported game hash, wrong thread,
  busy/moving roster, invalid observation, or file error. No delayed mid-round retry.

No ammo, terrain, objectives, RNG, skill effects, dead units, mod state or restore
support is implemented. The files are observations, not complete battle snapshots.
Three slots bound each battle's output; old battle/session folders are retained.
One startup warning explicitly identifies the diagnostic; no per-turn log spam.

## Build and checks

Use the shared `build_kit.py --module TacticalRoundCheckpoint --use-installed-settings`
and canonical installer. Installed MenaceModSettings and Jiangyu.Loader are validated
and preserved. The settings dependency is an installation safety guard; this probe
does not alter settings or add UI. Do not copy the DLL into Mods manually.

The independent `tests/OfflineChecks` runner executes the production checkpoint
writer and full-round predicate without loading game assemblies. Pass a writable
scratch directory as its only argument. It checks round filtering, rotation,
serialization failure, interrupted writes, battle isolation and untouched save data.

For the first game check, play through a full-round transition. Check for one
observation per new round, refreshed AP/turn-done values (native exceptions such as
contained units remain intact), no additional checkpoint on unit activation, and
three-slot rotation after round four. No load test is possible yet.

Native evidence and remaining restore work: `docs/TACTICAL_SAVE_FEASIBILITY_83.md`.
Local, Windows and in-game results are recorded separately in issue #83.
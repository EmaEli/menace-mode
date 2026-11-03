# Tactical round checkpoints — issue #83

## Scope confirmed by the user

Checkpoint at the **start of a complete new round**, after native round refresh,
not after each unit activation. Do not artificially refill AP, clear effects or
change native exceptions (for example contained passengers).

## Native investigation, 3 November 2026

Target: MENACE 0.7.17+22362, GameAssembly SHA256
`871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`.
Read-only Apple LLVM objdump inspection of the local binary, annotated using the
matching Il2CppDumper `script.json`; the binary and dumps remain gitignored.
Addresses below are RVAs for this exact build, **not runtime patch addresses**.

| Method / evidence | RVA | Finding |
| --- | --- | --- |
| SaveSystem.Save | 0x5e7b90 | Constructs strategy SaveState; calls StrategyState.ProcessSaveState at 0x5e7f0e. Uses shared temporary/latest save paths, even with a custom destination. Never call this for the probe. |
| StrategyState.ProcessSaveState iterator MoveNext | 0x745060 | Full 0x745060–0x747450 body inspected. Serializes campaign objects: roster, squaddies, battle plan, operations, inventory, offmap ability availability, etc. No tactical map/actor serialization found. |
| Same iterator, Played mission branch | 0x746634 | Checks mission status 2. Ironman logs that it is aborting; non-Ironman logs an unexpected Played mission. Both flow to Mission.OnMissionFinished at 0x7467fd and Operation.EndMission at 0x74683e. Bypassing the Save button cannot yield battle resume. |
| SaveSystem.LoadSaveGameCoroutine MoveNext | 0x603c10 | Finishes an existing TacticalManager (0x603fd5), removes tactical state, ensures Strategy scene and processes strategy state. No battle reconstruction path found. |
| TacticalManager.NextRound | 0x6c3470 | Ends prior-round effects, increments round (0x6c3899), invokes faction/Actor.OnRoundStart (0x6c3a46), structure skill refresh (0x6c3b51), then OnRoundStart event (0x6c3c0c). UpdateAllActorStates follows at 0x6c3c14 before return. |
| Actor.OnRoundStart | 0x63c3c0 | Resets turn state, consults native AP and updates skills. Native rules, not an assumption that all flags/AP must have identical values. |

Metadata cross-check: TacticalState, TacticalManager, Actor, Entity, Map,
SkillContainer, Structure and Objective have no native ISaveStateProcessor contract.
This is strong evidence against a simple native-save unlock, not a proof that a
custom tactical persistence mod is impossible. Shared/generic native functions can
have multiple metadata names; annotations alone are not complete call-graph proof.

## Implemented first experiment

`mods/jiangyu/TacticalRoundCheckpoint`, version 0.1.0, explicit experimental opt-in.
**Capture-only observations, no loader and no recovery guarantee.**

- NextRound prefix records old round; last-priority postfix captures only on an
  actual positive round advance, after the original function returns.
- Observes faction roster templates/tiles/HP/AP/morale/suppression/turn flags.
- Main-thread and native-hash guarded. Aborts capture on moving/busy actors rather
  than waiting until units have begun acting; never pauses or mutates tactical state.
- Managed JSON only, three slots in a new per-session/per-battle diagnostics folder
  outside Saves, Mods and UserData. Serialize first; flush a newly created pending
  file then rename. Write failures preserve previous slots and disable the probe.
- Every file explicitly states CanResume=false and lists missing state. No native
  object pointers are persisted; roster indices are not stable restoration IDs.
- Installed-settings canonical mode preserves settings and loader. No bespoke installer.

## What the experiment can and cannot prove

Offline tests exercise the real writer and round predicate: full-round filtering,
rotation, JSON labels, failure preservation, battle isolation and no campaign writes.
A build against real game proxies checks API binding, not execution in MENACE.
Only a game run can show whether this hook observes each usable round consistently,
including the first round and modded effects at the boundary.

This intentionally incomplete capture must **not** be offered as an autosave or
as crash protection. It does not yet save ammo/elements, objectives, map changes,
effects, RNG, initiative/AI, stable entity identity or other mods' tactical state.

## Next restore experiment, gated separately

1. In an isolated test mission, establish a reproducible map/deployment baseline
   without loading a native Played mission (which currently ends that mission).
2. Prove cross-process stable identities and restore one unit's complete combat
   state, including squad elements, equipment, ammunition and skill-owned effects.
3. Add terrain destruction/hazards, objectives/reinforcements/delayed abilities,
   fog, random generators, AI/initiative and each active mod's state. Reject
   unsupported state rather than quietly dropping it.
4. Compare a canonical state digest before exit and after a **fresh process** restore;
   verify subsequent actions, objective progression and mission completion.
5. Only then add rotating loadable slots and user-facing resume UI.

Action replay is not a shortcut without deterministic AI, RNG, map generation and
mod behavior. Process-memory dumps are not portable or supported battle save files.

Build, deployment and game evidence are tracked separately in
https://github.com/EmaEli/menace-mode/issues/83.

## Validation checkpoint

- Release build against cached matching 0.7.17 IL2CPP proxies: zero warnings/errors.
- Production writer/boundary checks: 39 passed; Python regressions: 174 passed;
  C# regression and selected real-DLL metadata checks: 17,882 passed.
- Canonical installed-settings candidate: 96 inventoried files, one module;
  ZIP SHA256 `3ac3dd9e3c4a77bfbb83b343633427e0a8b8e6cc4e9e925882a4c5275ee4aebf`;
  build base `4ad24d2`, `MENACE-TacticalRoundCheckpoint-0.1.0-issue83-verified.zip`.
  Inventory/hash verification and clean extracted-source rebuild passed locally.
- Clean verification exposed an external-settings metadata-witness bug in the
  shared verifier. Fixed and regression-tested: inspect project XML after selected
  builds, without rebuilding/bundling/installing the external settings companion.
- Canonical Windows BuildOnly and install passed 3 November on MENACE 0.7.17+22362
  with pinned SDK 10.0.113. Windows compilation: zero warnings/errors. Installed
  manifest/assembly/file 0.1.0/0.1.0.0/0.1.0.0; installed DLL SHA256
  `dbeba3bbfa17fc38990065923f823836cbfe04e9b26e75a9567b35429cdcc0e3`.
- Installer log: `E:\MENACE-Work\Temp\MENACE-install.log`. Backup/rollback journal
  outside Mods: `_MENACE_Backups/20261103-191717-c9e52ec720fd47d898ff8363564d88e3`;
  the originals directory is empty because this was a new package install. All 537
  unrelated Mods files and 817 UserData files were hash-identical before/after.
  Installed settings and loader were validated/reused; campaign saves were untouched.
- Fresh Windows `Latest.log` (3 November 2026, 19:46 local) contains the probe's
  capture-only initialization banner and no probe-disable, game-hash, hook-binding,
  thread, or busy-boundary warning. The error-like log text is not attributed to
  this probe; a `ReportException` marker has no nearby stack frames.
- Read-only inspection of the new diagnostics found two readable observations:
  rounds 1 and 2, 44 and 59 actors, each with eight declared missing-state groups
  and `CanResume=false`; zero pending files. This confirms the hook captured those
  two observed boundaries, **not** that it can restore either one.
- Full tactical persistence/resume remains unimplemented. These are limited
  startup and two-boundary gameplay observations, not an autosave or recovery test.
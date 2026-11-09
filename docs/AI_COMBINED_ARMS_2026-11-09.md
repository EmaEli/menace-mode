# AI Performance 1.6.1: combined-arms diagnostics follow-up

Version 1.6.1 adds optional tracing to the 1.6.0 policy below; it does not change
eligibility, support assignments, tile scores, reservations, native action choice,
movement, setup/fire or AP. The policy remains experimental and OFF by default.

## Opt-in diagnostic trace (1.6.1)

Enable **Log bounded combined-arms planning diagnostics** and **Infantry-led vehicle and
heavy-weapon support (0.7.18)**, then restart. `CA-DIAG` lines in `Latest.log` include:

- Faction snapshot publication counts and rejection reasons; per-asset eligibility gates.
- Nearby/compatible support requests, occupied slots, truncation and assignment outcomes.
- Candidate-origin filters, detected-target/probe counters, lane/damage results, budget
  stops, score seeds and the native versus final selected behavior.
- At most 256 deduplicated records per mission, keyed by round/faction/actor/phase.

The trace reports counts for detected targets and reads coordinates only after the existing
native detection gate. It is opt-in, bounded and diagnostic only; it makes no gameplay or
turn-time claim.

Tracking: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#2](https://github.com/EmaEli/menace-mode/issues/2),
[#13](https://github.com/EmaEli/menace-mode/issues/13).
Reaction fire remains separate in [#55](https://github.com/EmaEli/menace-mode/issues/55).
No issue is closed on automated/build evidence alone.

## Implemented scope

- Independent AIPerformance; no LargerMaps dependency. `CombinedArmsSupport` is
  experimental, OFF by default and restart-required; fast planning must be enabled.
- Advancing infantry supplies its native allocated mission direction, not a fabricated
  enemy position or predicted hidden movement. Purely defensive/mixed orders and arrival
  remain native. Support assets retain compatible advance orders or are unassigned.
- Human-crewed Rogue autocannon/laser/mercenary walkers, battle/heavy tanks and pirate
  chaingun/laser/heavy-MG trucks are narrowly recognized. MACHINE is not broadly allowed;
  alien constructs, drones, artillery, AoE and indirect fire are excluded.
- Direct-fire weapons teams use real weapon/deployment/setup capabilities and resources.
  Existing safe nearby candidates gain rear-covering utility. Native Move still decides
  reachability, paths, packing, movement and post-move setup; native Attack revalidates fire.
- Full native safety/AT costs are unchanged; newly rewarded positions are no more dangerous
  than the current position. Native useful support/emergency actions are not replaced.
- Main-thread faction snapshot (128 units/faction, four factions maximum), spatial buckets,
  four nearby partners and one vehicle plus one heavy-team slot per infantry request.
  Two-round leases, progress evidence and movement/death/order/mission invalidation.
- Eight local origins maximum, two direct-fire weapons, four currently detected targets.
  Only actual detection precedes enemy-coordinate reads. Lane geometry does not create an
  attack candidate, damage value, visibility, AP, ammunition, setup state or reaction shot.
- Existing 24-token evaluation / 48-token actor-round budget is shared, with an additional
  512-token faction-round cap. Support charges two tokens/lane or four/damage sample.
  Probe starts stop after a soft 20 ms; native calls are not interruptible. This is not a
  guaranteed turn/frame-time bound. Managed write failures restore all edited fields.

## Deliberately deferred

Stationary setup facing an empty lane, explicit facing orders, infantry waiting for cover,
and Overwatch arming are **not implemented**. No verified native standalone deployment
behavior was established for that empty-target case. Do not substitute an attack on empty
ground, initial-spawn Deploy, forced flags or direct skill execution. Native Move/Attack
may still choose legal deployment/setup through their existing execution paths.

The first version is order-led rear support, not the complete operational director.
No turn-time, real deployment, support coordination or reaction-fire improvement is claimed.

## Contract and automated evidence

- Reviewed native GameAssembly SHA256:
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9` (0.7.18).
- Native SkillBehavior.HandleDeployAndSetup checks usability/uses/executing elements and
  AP before executing setup. Move owns its post-move stance/setup queue. Exact byte/call
  assertions cover these contracts, alongside pre-normalization score access.
- Runtime compiles against hash-matched PC proxy references with zero warnings/errors.
  Metadata checks cover lifecycle hooks, native skill/LOS/resource contracts and included
  runtime types, without executing IL2CPP.
- Managed linked production-hook checks exercise crewed MACHINE scope, rear/AT safety,
  heavy setup reserves, hidden-read rejection, exhausted infantry, native useful actions,
  rollback, stale requests, short leases, worker exclusion and concurrent ownership/budgets.
  They do not prove real Harmony dispatch, native paths, setup animations or gameplay.

## PC validation

### 1.6.0 first-stage install (historical baseline)

Transfer roundtrip and remote script hashes passed; game closed; canonical E: build/temp
environment active. Reviewed game hash and all 119 installed proxy DLL hashes match the
local build references. Pre-install AIPerformance remains 1.5.0, DLL SHA256
`3045357b697acc8dd3a9fa0b4795d1a7ff226c07c96a4ec23fbe09f1581fa6a8`.

Source `5b1ce76` is committed/pushed on `dev`; unrelated dirty work is preserved.
The 65-file single-module BuildKit and clean extracted-source rebuild passed:
ZIP SHA256 `af53bbac07d50e30d3165bc60599a585b23fcb323115f8355aff5dd6daf50f7e`.
Scoped shared artifact checks: 18,009; linked hook checks: 13,002; planning/settings: 32;
proxy metadata: 185. Suite Python: 207; LargerMaps Python: 155 (eight explicit skips),
including six exact native phase/setup/movement byte-contract tests. Production builds
have zero warnings/errors; the shared offline runner retains existing nullable warnings.

Canonical Windows BuildOnly and standalone installation passed on 0.7.18+22828 with
private SDK 10.0.113, zero production warnings/errors and the same 185/32/13,002 checks.
Installed manifest/assembly/file: **1.6.0 / 1.6.0.0 / 1.6.0.0**; DLL SHA256:
`83acb611ec7b752a4f40ea6c30a5061778384b90d39bb99546aceb0cd2ba4400`.

- Affected-only backup: `_MENACE_Backups/20261109-112950-e67d342d461a48d594f13f019eee6796`.
  Journal originals match the retained 1.5.0 baseline; all installed-file hashes verified.
- **1,564 protected Mods/UserData/proxy files unchanged**. Settings and Jiangyu.Loader
  were reused, not replaced. No saves, unrelated mods or UserData were modified.
- Retained stage/logs: `E:/MENACE-Work/Builds/AIPerformance-1.6.0-combined-20261109/`;
  canonical persistent log: `E:/MENACE-Work/Temp/MENACE-install.log`.
- New support remains OFF by default. Enable **Infantry-led vehicle and heavy-weapon
  support (0.7.18)** in AI Performance, leave fast planning enabled, then restart.

### 1.6.1 diagnostic follow-up — canonical test install (9 November)

Source `d3caab9` on `dev`; this patch adds only bounded, default-OFF combined-arms
diagnostics. The experimental support policy and action selection are unchanged. The
single-module 66-file BuildKit passed inventory and clean extracted-source verification;
18,009 scoped shared-artifact checks, 17,984 offline C# regressions, 302 behavior checks,
13,002 recovery checks, 185 native metadata contracts, 34 distribution tests and 10
migration-contract tests passed. Production builds had zero warnings/errors.

Transferred archive hash and 66-file inventory matched the local candidate. Canonical
Windows BuildOnly and standalone installation passed on MENACE 0.7.18+22828 with private
.NET SDK 10.0.113. The Windows build reran the 185 metadata, 302 behavior
and 13,002 recovery checks with zero production warnings/errors. Installed manifest /
assembly / file versions are **1.6.1 / 1.6.1.0 / 1.6.1.0**; installed DLL SHA256:
`CF839A5CA1A531CE2265C2B1171AFE512E537E2AB40ACFA6780185B95380CA58`.

- Affected-only backup and journal:
  `_MENACE_Backups/20261109-133445-018f2c85293b412fbbb3441eba1be951`. All three
  retained AIPerformance 1.6.0 files match the journal; the original DLL hash is
  `83acb611ec7b752a4f40ea6c30a5061778384b90d39bb99546aceb0cd2ba4400`. The only other
  journal target was the expected loose `AIPerformance.dll` conflict; it was absent and
  not moved or installed.
- MenaceModSettings remained **0.8.2**, SHA256
  `990CE86D5DE3E06BD2C6D2897F8505D227E690C9ABB56CD642F171DB62344CB7`; settings and
  Jiangyu.Loader were reused. The game remained closed. No other install target was
  changed and no saves were touched.
- Staging/logs: `E:/MENACE-Work/Builds/AIPerformance-1.6.1-diagnostics-d3caab9/`;
  canonical installer log: `E:/MENACE-Work/Temp/MENACE-install.log`.

### In-game acceptance still pending

On the next test, enable **Log bounded combined-arms planning diagnostics** and
**Infantry-led vehicle and heavy-weapon support (0.7.18)**, restart, then try a short
pirate/Rogue engagement. Retrieve a fresh Latest.log after the user's result. Startup,
native hook dispatch, actual setup/fire behavior, coordination and turn-time acceptance
remain **untested**; 1.6.1 adds diagnostics, not a gameplay fix. No repeated 20-turn
identical replay is prescribed and no issue is closed on build/install evidence alone.
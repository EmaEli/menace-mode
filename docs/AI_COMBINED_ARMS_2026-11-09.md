# AI Performance 1.6.0: first-stage combined-arms support

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

Transfer roundtrip and remote script hashes passed; game closed; canonical E: build/temp
environment active. Reviewed game hash and all 119 installed proxy DLL hashes match the
local build references. Pre-install AIPerformance remains 1.5.0, DLL SHA256
`3045357b697acc8dd3a9fa0b4795d1a7ff226c07c96a4ec23fbe09f1581fa6a8`.

Canonical BuildOnly, installation and final installed hashes are pending at this checkpoint.
Startup/gameplay acceptance remains pending even after those gates pass.
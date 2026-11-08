# AI Performance 1.5.0: bounded offensive planning

Tracking: [#2](https://github.com/EmaEli/menace-mode/issues/2),
[#17](https://github.com/EmaEli/menace-mode/issues/17).
Follow-up to the rejected [1.4.0 wandering test](AI_WANDERING_FEEDBACK_2026-11-08.md),
not a claim that moving more solves tactical initiative.

## Native boundary and implementation

Reviewed binary: MENACE 0.7.18+22828, GameAssembly SHA256
`494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.

- Native Attack.OnCollect exits when InfluencesPositioning is false; Attack.OnEvaluate
  can still evaluate immediate shots. Re-enabling native collection on a smaller supplied
  dictionary would not bound its internal map-origin enumeration. Do not use that workaround.
- New prefix on Agent.PostProcessTileScores operates on the current raw native dictionary
  **before** utility/safety powers and role scaling. Native Evaluate makes GetTiles' +0x60
  reference point to the +0x58 working dictionary at entry. Initial native behavior evaluation
  precedes normalization; criterion post-processing and movement evaluation follow it.
- Use at most eight existing origins within six tiles, four detected targets and two eligible
  unlimited-use targeted direct-fire skills. Native IsUsableOn with the positioning preset,
  IsApplicableTo, unchanged final user tile and GetTargetValue(false, ...) validate each probe.
  AoE, setup/deployment, limited-use and indirect positioning retain previous behavior.
- Fill UtilityScore, best UtilityByAttacksScore and MinimumUtilityAPCost. No attack candidates,
  path, AP, safety, distance score, faction orders or runtime skill uses are fabricated.
  Repeated observations add only improvement over previous best projected attack value.
  Native reserve reset is 1024, checked against the exact binary.
- 24 probes/evaluation, 48/actor/round, 256 actor entries; stop starting probes after 20 ms.
  This is a **soft** elapsed check, not cancellation of an individual expensive native call.
  Repeated polls and old-map tasks cannot renew the hard count. No additional task/path search.
- Native positive InflictDamage with real native candidates can replace Move/Idle at
  PickBehavior. Other useful actions and severe suppression/morale conditions are preserved.
  Native attack execution remains authoritative; no direct skill.Use or free attacks.
- One/two nearby, alive, uncontained, awake companions outside deployment add 25/50% raw
  offensive utility, capped before native scaling. This models local support, **not** hidden
  enemy counts, full platoon coordination or a new operational director.
- With no useful attack/support utility, exclusively AdvanceTowards orders get immediate
  directional utility before normalization, including worse-cover positions. Preserve native
  actor-ID allocation and arrival; do not redirect defend/mixed/invalid/ignored orders.
- Target coordinates come only after native faction detection. Mission-civilian objective
  matching reuses the 1.4.0 guard; no hidden actor positions or incidental-splash priority.
  The setting for civilian priority also controls its use by projected positioning.

Default-ON OffensiveCombatPlanning is separately switchable; runtime hooks fail closed on
other builds. Organic pirate/Rogue scope is intentional. Non-human factions, machines/drones,
contained actors, weak morale and severe suppression retain their previous policy. Native
immediate attacks remain available for weapon categories excluded from projected positioning.

## Validation

Local real-reference build: zero warnings/errors against 0.7.18 proxies and pinned Jiangyu
1.4.10 SDK. Production-linked fixtures cover projected firing utility, shot preference,
AP reserve, idempotence, finite/legality/state guards, hidden/mission targets, local support,
direction/multi-order allocation, native-failure rollback and concurrent hard budgets.
Metadata-only native contract checks cover the additional methods and writable fields.
Four exact-native phase/flag/reserve checks pass; these are not native execution tests.
Final local totals: 12,952 production-linked checks, 30 planning checks, 156 native metadata
contracts, 207 Suite Python tests, 153 LargerMaps Python tests (eight skips), and 18,009
scoped shared behavior/artifact checks. The shared fixture build reports existing nullable
warnings; the production AI build has zero warnings/errors.

Windows canonical BuildOnly/install and installed hashes are recorded below when completed.
No 1.5.0 startup, native dispatch, combat acceptance or turn-time comparison has been run.
Keep #2/#17 open. Do not request another identical twenty-turn replay: first check a short
engagement for direct-fire use, coherent advance and any new delay, then inspect the fresh log.
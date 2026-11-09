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
- Prime lazy skill IDs during main-thread scene setup, then read only cached m_ID on AI
  workers. Unity object names and lazy GetID calls are forbidden in the probe path; missing
  cache entries fail closed. Fixtures throw if worker probes try reading a Unity name.
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
Final local totals: 12,953 production-linked checks, 31 planning checks, 161 native metadata
contracts, 207 Suite Python tests, 153 LargerMaps Python tests (eight skips), and 18,009
scoped shared behavior/artifact checks. The shared fixture build reports existing nullable
warnings; the production AI build has zero warnings/errors.

## Verified Windows test installation

Source commits 08c60d6 (policy), 741b790 (positive support guard) and e629959 (worker ID
safety) are pushed to dev. Final canonical kit: 60 inventoried files, SHA256
`d2eff1c904c6d0443c63091ac8749a768cc7d9879943b4e147aa8f460313b038`.
Its gitBaseRevision is e629959; inventoried shared installer files include pre-existing
working-tree changes, preserved and not committed as AI work. The hash identifies the
actual complete kit, not a claim that the whole original worktree was clean.

Clean extracted-source build and canonical Windows BuildOnly/install pass on MENACE
0.7.18+22828 with .NET SDK 10.0.113: zero production warnings/errors; 161 metadata,
31 planning and 12,953 linked production checks pass again on Windows. All 119 local
proxy hashes match the current PC. No native game methods were executed by these checks.

- Installed manifest/assembly/file: **1.5.0 / 1.5.0.0 / 1.5.0.0**.
- Installed DLL SHA256: `3045357b697acc8dd3a9fa0b4795d1a7ff226c07c96a4ec23fbe09f1581fa6a8`.
- Affected-only backup/journal outside Mods:
  `_MENACE_Backups/20261108-223940-d7f067e0086e4868b2a10d83a91a6572`.
  Original 1.4.0, all original/installed file hashes and journal scope verified.
- **1,563 protected Mods/UserData/proxy files unchanged**; settings/loader reused.
  No saves, settings or unrelated mod files changed. Game remained closed.
- Stage: `E:\MENACE-Work\Builds\AIPerformance-1.5.0-offensive-20261108-f68c-r2`.
  Complete canonical logs retained there as buildonly.log and install.log; standard
  current installer log is `E:\MENACE-Work\Temp\MENACE-install.log`.

**Startup/native dispatch, direct-fire behavior and real turn-time comparison remain
unverified.** #2/#17 stay open. Next: a short direct-fire engagement, watching whether
infantry shoot when able and use coherent firing/mission positions without long turns.
Retrieve fresh Latest.log after the user's feedback; do not prescribe another identical
twenty-turn waiting test as evidence of a fix.

## Gameplay feedback — 9 November 2026

User reports **some improvement**, with vehicles still timid; caution around spotted
anti-tank weapons is explicitly desired. This is partial positive gameplay feedback,
not full acceptance of #2/#17 or proof of specific native hook dispatch/turn timings.
No fresh log retrieved: the PC was shut down at the user's request.
Next requested work is **planning**, not deployment: infantry-led vehicle/heavy-weapon
support, rear vehicle spacing of 3–10 tiles and heavy weapons covering the advance.
See the [combined-arms proposal](AI_COMBINED_ARMS_PLAN_2026-11-09.md), tracked in #13/#17.
# Changelog

## 1.12.0 — 11 November 2026

- Fix missed native sightings: observe AIFaction.OnOpponentSighted because normal
  Actor.OnDiscovered writes detection inline rather than calling SetDetectedByFaction.
  Refresh faction memory after native turn-start visibility, not before it.
- Mobilize all hostile factions, including wildlife, bugs and constructs, from legitimate
  contact reports; remove human/rifle/crewed-template and pre-existing-order prerequisites.
  Preserve useful native actions, explicit defense, deployment, movement and resource guards.
- Share stable two-round contact-sector objectives among nearby units, with bounded
  per-faction memory, assignment capacity, duplicate-report work and mission cleanup.
- Allow unordered pirate/Rogue infantry into existing optional local support groups through
  managed contact objectives; native zones are untouched. Keep combined-arms class guards.
- Keep mobilization active independently of simplified skill-positioning profiles.
  Production-linked 45-unit/all-faction regressions and real proxy contracts pass;
  Windows installation and gameplay acceptance are recorded separately.

## 1.11.0 — 11 November 2026

- Fix native advance-zone scopes on AI worker threads, with thread-local eligibility,
  faction isolation, processing-generation invalidation and exception cleanup.
- Add shared local infantry/support groups, actual native fire/forward-smoke receipts,
  bounded staging/crossing, infantry screening and nonrenewable waiting deadlines.
- Require equipped affordable smoke and a feasible native forward move; favor real smoked
  forward tiles and block repeat same-position requests until infantry makes progress.
- Do not treat current-origin geometric lanes as executable shots. Clear the native 1024
  post-utility AP sentinel for movement-only closing; preserve actual path/setup/fire costs.
- Existing combined-arms diagnostic opt-in includes contact/group traces, with reserved
  late-action budget. Normal defaults remain silent. No settings, saves or equipment grants.
- Production-linked managed regressions and exact native proxy contracts added. Gameplay
  acceptance remains pending; hostile equipped mortar-smoke availability is not established.

## 1.10.1 — 11 November 2026

- Bound the combined-arms exposed-route hold to two rounds per unit per mission. If no
  safer native route or smoke/fire response becomes available, the original native Move
  is allowed again on the third round; no alternate path or attack is fabricated.
- Add default-OFF `DiagnoseContactAdvance`, with at most 256 deduplicated records per
  mission for contact observations, native-zone scope/thread eligibility, known-opponent
  interception/missing reads and exposed-route guard expiry. Logging failures are isolated.
- Add regression coverage for expiry, mission/map reset, state capacity, diagnostics and
  the hidden-position read boundary. Gameplay behavior still requires an in-game trial.

## 1.10.0 — 10 November 2026

- Keep native `AdvanceTowards` zones active after contact. Reviewed 0.7.18
  `ConsiderZones.Evaluate/PostProcess` skip advance zones for the whole faction once
  `AIFaction.HasKnownOpponent()` is true, which stopped the 1.9.0 mission's enemies from
  pushing once any squad saw the player. The faction flag now reads false only inside
  those two native calls, on the AI thread, for ready pirate/Rogue infantry, direct-fire
  teams and recognized crewed vehicles holding advance orders without a useful native
  attack/suppression candidate. Engaged units, explicit defense, deployment, indirect
  fire, drones/xenos, morale/suppression guards and all other `HasKnownOpponent` readers
  (including advance recovery) remain native. Scoring magnitudes are the game's own.
- Local checks only; in-game escalation is not verified by this release.

## 1.9.0 — 9 November 2026

- Replace brief three-responder approaches with faction contact-led alert and persistent
  range closing under the existing OffensiveCombatPlanning setting. Unassigned infantry,
  direct-fire teams and recognized crewed vehicles can react to a legitimate sighting;
  explicit defense, deployment, emergency, morale/suppression and faction guards remain.
- Approach the known area over multiple turns with modest lateral spread. Cover-only
  stops cannot replace progress while no firing lane is available. Currently detected
  targets allow bounded range/LOS/setup-aware firing-cover checks; native Move/Attack
  still own paths, legal setup, ammunition and execution. No attack utility is fabricated.
- Native suppression-fire candidates can win over Move/Idle, not just damage attacks.
  Geometric staging survives the shared expensive-probe limit and fresh score resets.
- Request existing equipped smoke through Smoke Overhaul 0.9.0 when the approach is
  exposed within observed enemy range and lacks a usable own firing lane. No grants.
- Last-known areas expire after 12 rounds or arrival; hidden positions are not refreshed.
  Local checks are not in-game acceptance. Windows deployment remains a separate gate.

## 1.8.1 — 9 November 2026

- Fix vehicle threat-response staging disappearing before Execute after repeated native
  tile-score resets. Geometric staging no longer spends unused live-target query tokens;
  exhausted query budgets still permit only freshly safe, affordable forward staging.
- Preserve target-probe/faction limits, native safety/path/attack ownership and setup/fire
  AP reserves. Do not cache stale attacks/positions, add aggression, or change smoke/terrain.
- Add fresh-score, exhausted-budget and context-change regression checks. The original
  production method reproduces destination loss on evaluation four; gameplay remains a
  separate acceptance gate.

## 1.8.0 — 9 November 2026

- Add bounded threat-directed vehicle positions independent of a surviving/ready infantry
  anchor. Existing local origins may be ahead of infantry; actual detected shots retain
  native danger penalties, while last-known staging cannot increase native danger.
- Double only positive native immediate damage valuations at an observed, currently
  detected threat tile, including native artillery evaluations. No fabricated candidates,
  hidden-coordinate fire, new bombardment executor or bypass of native legality.
- Guard forward infantry Move choices against remembered firing lanes and exposed native
  route waypoints. Preserve native danger reduction, attacks and emergency behaviors.
- Optional Smoke Overhaul 0.9.0 integration requests equipped, legal smoke toward the
  remembered threat for ordinary rifle infantry; real smoke can enable a forward step.
  No equipment grants, second grenade scheduler or custom path search.
- Add linked production behavior/smoke checks and clipped-path metadata contracts.
  Windows deployment and gameplay acceptance are separate gates.

## 1.7.1 — 9 November 2026

- Observe armed, detected threats to advance-ordered infantry independently of that
  soldier's suppression/readiness; retain native detection, applicability, range and LOS
  checks. Allow one bounded retry after an incomplete observation, without hidden reads.
- When a remembered firing lane still threatens the planner and no safe covered flank can
  be seeded, omit only mod-generated objective-advance utility for that pass. Native shots,
  emergency actions and ordinary no-threat advances remain available.
- Add production-hook regression checks for suppressed victims, retry-after-range-miss,
  exposed advance suppression, direct-fire preservation and the no-threat fallback.
- Managed tests do not establish Windows installation or in-game acceptance.

## 1.7.0 — 9 November 2026

- Remove rear-gap incentives for vehicle withdrawals: hold near infantry, catch up only
  forward/laterally, and preserve native danger reduction and useful firing positions.
- Match neighboring advance allocations in a shared forward corridor, not only identical
  zone rectangles. Opposite/defensive orders remain excluded.
- Check known movement plus fire costs and recheck the actual clipped-path reserve at
  choice time. Native Move still calculates paths/AP; no extra path search is introduced.
- Remember observed, armed threats to advancing infantry for two rounds. Favor existing
  local, no-more-dangerous forward flanks outside their possible firing lane. Lost contacts
  never refresh hidden positions/weapons; uncertainty grows and records expire.
- Bound observations and share the existing combat probe budget. Optional diagnostics
  add a `flank` phase. No synthetic mortar, smoke, suppression or reaction-fire actions;
  those retain native handling. Windows install and gameplay evidence are separate.

## 1.6.1 — 9 November 2026

- Add opt-in `DiagnoseCombinedArmsSupport` traces for snapshot rejection, candidate
  matching/slot contention, support scoring/probe gates, claims and final native choice.
- Emit at most 256 deduplicated records per mission; the option defaults OFF and requires
  restart. No support policy, native action, task order, safety, setup, fire or AP changed.
- Extend managed checks for diagnostic budget and candidate counts; logs are not gameplay
  acceptance evidence.

## 1.6.0 — 9 November 2026

- Optional OFF-by-default infantry-led support positions for narrowly recognized
  crewed pirate/Rogue vehicles and direct-fire weapons teams on reviewed 0.7.18.
- Main-thread advancing-infantry snapshots, bounded nearby matching, separate vehicle/
  heavy-team slots and short assignments with movement/death/order/mission invalidation.
- Prefer safe rear covering lanes and real detected-target damage positions. Preserve
  native threat/AT costs, movement/path decisions, useful actions and execution; reserve
  deployment, setup and firing AP without forcing flags or executing skills directly.
- Share probe accounting with organic offensive planning, including a hard 512-token
  faction-round cap. Roll back partial score/reserve writes and reject stale identities.
- Keep stationary anticipatory deployment, explicit facing, infantry waiting and
  Overwatch integration deferred; the first stage does not manufacture these actions.
- Managed, native-layout and proxy-contract checks are distinct from gameplay acceptance.

## 1.5.0 — 8 November 2026

- Restore bounded native direct-fire positioning under fast planning for organic
  pirate/Rogue forces, rather than re-enabling the full map-wide firing search.
- Prefer already-valid native damage actions over movement/Idle; preserve other useful
  native actions, emergency states, weapon/AP rules and native execution.
- Reward nearby support with capped projected utility and immediately value progress
  toward allocated advancing mission orders when no useful attack/support is present.
  Unlike stalled recovery, advancing tiles need not be equally safe as the current tile.
- Use native-detected mission civilians for positioning without hidden coordinates,
  generic civilian hostility or fabricated immediate attacks.
- Bound work to 24 probes/evaluation, 48/actor/round and a soft 20 ms probe-start limit.
  Keep paths/safety native; roll back partial utility/reserve writes on managed failure.
- Default-ON `OffensiveCombatPlanning`; reviewed 0.7.18 only. Automated/native-contract
  and installation evidence are distinct from gameplay acceptance (#2/#17).

## 1.4.0 — 8 November 2026

- Continue a recovered advance in the next round after actual progress toward the same
  complete mission order, rather than waiting two idle rounds again after every step.
- Prefer greater safe progress among already-computed native tiles with a bounded
  1–2x native utility threshold. Native distance weighting, paths and AP remain untouched.
- Add default-ON `PrioritizeMissionCivilians` on reviewed 0.7.18: double positive native
  immediate damage value only for visible, non-allied civilian/local escort targets of
  ongoing ProtectUnit/RescueUnit or matching native Collateral conditions. No generic
  civilian hostility, hidden contacts, incidental splash priority or fabricated attacks.
- Keep fast planning ON, all combat/order/arrival guards and optional convergence unchanged.
  Linked hook/metadata checks cover continuation, cancellation, progress and objective scope.
  Build/install evidence is separate from gameplay acceptance (#2/#17).

## 1.3.1 — 8 November 2026

- Recover stalled units assigned multiple AdvanceTowards orders (#2), rather than
  excluding the two-order mission observed in the rejected 1.3.0 gameplay test.
- Bound recovery to at most eight exclusively advancing orders; use native Entity.ID
  allocation, preserve multi-zone distance scores, and seed only no-less-safe progress
  utility toward the allocated zone after existing idle/no-shot guards.
- Revalidate the entire ordered zone context before utility/choice; changes to any
  area's geometry, priority, count or order invalidate stale recovery eligibility.
- Keep fast planning, contact convergence, settings/defaults and native path/AP intact.
  No hidden-player targeting, new path search or shared mission-order changes.
- Linked production-hook and exact-binary checks cover multi-order allocation and guards.
  Windows installation and gameplay evidence are recorded separately in #2.

## 1.3.0 — 7 November 2026

- Add optional contact memory and approach utility for otherwise unassigned pirate/Rogue
  infantry on the reviewed 0.7.18 build (#17). OFF by default; restart required.
- Snapshot only native detection events, retain no hidden Actor positions, expire lost
  contacts after two rounds, cap responders at three per faction and bound attempts.
- Preserve all mission zones, combat actions, support teams and native movement/path/AP
  decisions. No new artillery targeting in this first stage.
- Add linked production-runtime/hook tests and native proxy metadata contracts.
- Existing 1.2.2 performance/advance-recovery settings and defaults remain unchanged.

Validation: automated and canonical Windows build/deploy results are recorded in issue
#17. A successful build or installation is not gameplay acceptance.
# Changelog

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
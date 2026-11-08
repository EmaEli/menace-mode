# AI Performance 1.4.0 — continuous guarded advance and mission targets

Scope: [#2](https://github.com/EmaEli/menace-mode/issues/2),
[#17](https://github.com/EmaEli/menace-mode/issues/17).
User requested more continuous advance and stronger aggression toward mission civilians.
This follows the rejected slow-cadence [1.3.1 test](AI_MULTI_ORDER_FEEDBACK_2026-11-08.md).

## Behavior

- Two idle rounds still start recovery. After a legal recovered Move is selected,
  actual distance reduction toward the same complete order enables the next round,
  without another two-round wait. A provisional choice alone does not count.
- Native completion polls cannot grant another recovered move that round. Changed orders,
  no progress, arrival, missed rounds, useful other actions and mission reset stop continuation.
  Combat, morale, suppression, sleep, AP and exclusively-advance order guards remain.
- Already-computed no-less-safe progress tiles receive native utility threshold times
  `1 + progress/currentDistance`, bounded to 1–2x. Native distance weights, destination
  ranking, paths, clipping, AP, contact lists and mission zones are not replaced.
- New `PrioritizeMissionCivilians` defaults ON and requires restart. On reviewed 0.7.18,
  it doubles a positive native immediate InflictDamage target value only when the aimed-at
  civilian/local escort is currently detected, non-allied and a target of an ongoing
  ProtectUnit/RescueUnit or fulfills native Collateral conditions. Civilian faction is **3**;
  local escorts may be faction **4**. Exact native mission relevance is required in both cases.
- Other objective types, hidden/unrelated/allied/dead/contained actors, hypothetical
  positioning, incidental splash, invalid and zero-value attacks retain native behavior.
  At most 32 objectives inspected per qualifying native evaluation. No new contact,
  visibility, attack or path search; fast planning remains ON. Existing settings untouched.
- This is not a visibility fix or guaranteed firing. Previous missing civilian contacts
  cannot be solved by multiplying target value; unseen civilians remain unseen.

## Validation and boundaries

- Linked production hooks: 12,885 managed checks including 18 consecutive progress rounds
  with three native-completion polling orders, interruptions and mission-target exclusions.
- Planning/task/restore: 29 managed checks. Current 0.7.18 proxies/binary: 125 metadata checks.
- Real-reference Release build succeeded. All 119 local proxies match fresh PC SHA256 values;
  exact native hash `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
- Transfer round trip hash-verified; PC closed, installed AI 1.3.1 baseline confirmed.
- Windows canonical BuildOnly/install, final package checks and gameplay remain pending.
  No claim of live behavior or better turn timings until tested. #2/#17 remain open.
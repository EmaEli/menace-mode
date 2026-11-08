# Changelog

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
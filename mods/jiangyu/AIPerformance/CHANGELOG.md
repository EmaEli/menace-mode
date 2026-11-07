# Changelog

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
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
- Python: 207 Suite checks and 149 LargerMaps checks (8 skips) pass. Scoped shared
  runtime/artifact runner: 18,009 checks pass. Existing nullable fixture warnings remain;
  production builds have zero warnings/errors.

## Canonical Windows installation

- Source `7cdf906` committed/pushed to `dev`; unrelated worktree changes preserved.
- Canonical one-module installed-settings BuildKit: **56 inventoried files**, SHA256
  `a115c7afb03bc9ad1c6845e84817a0dffeff99349360c9636e94f10a1cd3a16f`.
  ZIP inventory and clean extracted-source build/real DLL metadata checks passed locally.
- Windows canonical BuildOnly and standalone install both passed with private SDK 10.0.113
  on MENACE 0.7.18+22828. Each build had zero warnings/errors and reran 125 metadata,
  29 planning and 12,885 recovery/contact/objective checks before staging.
- Verified on disk: manifest **1.4.0**, assembly/file **1.4.0.0**. DLL SHA256:
  `8ed977a85b6d88518d7b1ee1bcf5f98c6dcd2e0b9dcfe944ea3e9f7369d1ae5f`.
- Affected-only backup outside Mods:
  `_MENACE_Backups/20261108-195145-0505a7fbc8f647f580e6bb541a2d1eb9`.
  Journal targets are only AIPerformance and its legacy root DLL conflict. Original 1.3.1
  DLL hash and every backup/live file hash match the journal. Settings and loader reused.
- All **1,562 protected Mods/UserData/proxy files** match preflight hashes; saves untouched.
  Game closed throughout. Script transfer round trip, archive and inventory hashes verified.
- PC stage/logs: `E:\MENACE-Work\Builds\AIPerformance-1.4.0-continuous-20261108`.
  Stable canonical log: `E:\MENACE-Work\Temp\MENACE-install.log`.
- Startup/native dispatch, actual battle behavior and turn timings **not yet tested**.
  No claim of a live targeting or movement fix. #2/#17 remain open; retrieve fresh Latest.log
  after the user's next gameplay feedback. Do not request another identical twenty-turn run.
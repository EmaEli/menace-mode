# Bounded AI follow-up — 15 October 2026

Local/uncommitted follow-up to migration/regression checkpoints. No PC access,
deployment, merging, Windows/PowerShell execution or game/CPU acceptance performed.
Earlier migration/handoff ZIPs are historical and must remain unchanged.

## #1 — Smoke Overhaul 0.6.4

- Ported PR #12 0.6.3 at `0426b4484581e355cbd5b831f85fb40c483f114c` to current Jiangyu.
  The 0.5.3 port did not contain these rules. Retains 60/45/30% proactive chances,
  actor/turn guard, 24 known+detected opponents / 6 local tile candidates, native
  usability/affordability/target/consumption checks, failed-use fallback and late
  coexistence with Infantry Assault. No facing-only targeting or hidden coordinates.
- Pinned-only permission/zero queried AP cost remains a separate toggle; other
  grenade states/costs unchanged. Added main-thread fallthrough consistent with
  existing AI integration; owned template permissions restore on disable/unload.
- Confirmed signatures in cached 0.7.16 proxy metadata; local release build and
  Jiangyu packaging passed, real DLL metadata 0.6.4.0. Pure production geometry is
  tested across 578 bearing/mode cases. These do not execute native AI.
- #1 updated, remains open; PR draft not merged. Runtime checklist at the top of
  `mods/GameplaySuite/docs/TESTING.md`. Proactive odds do not predict total smoke use.

## #2 — Larger Maps 2.1.0

- PR #18 at `3f50705dffe21fa729813001ac2fe5378d30b33f` was absent from current 2.0.1,
  including its setting/test. Adapted only the five-profile opt-in, not older map
  code, versioning or installer. **OFF by default**, restart required, >64x64 only
  with simplified combat planning ON. No wake-ups, orders or new AI scheduler.
- Captures original true, attack+line-of-fire profiles by native pointer before
  mutations; shared aliases are preserved and originally false profiles untouched.
  Maximum five ID lookups for selection per setup. Other attacks/buffs and normal
  restore/backend behavior remain baseline. Native work can increase when enabled.
- Actual production setup/restore runs against in-memory host/reflection/object
  doubles: 64/80/96 × simplify × preserve matrix, default OFF, idempotence, shared
  wrappers, missing/invalid profiles, original false preservation, failed GetAll ID
  fallback, post-write rollback and scene transition. Not an IL2CPP execution test.
- Native 0.7.16 proxy attack/line-of-fire property signatures confirmed. Local release
  build/Jiangyu packaging passed; actual DLL metadata 2.1.0.0. No current gameplay
  evidence follows from the historical 1.9 log on a different mission.
- Controlled test: #1 accepted first, same save/seed/mod settings, current binary
  selective OFF/ON, local routing/deep profilers OFF, 64 control / 96 test, offensive
  and sleeping/defensive missions separately. Record actual shots/repositions and
  ~3 turn durations. Full checklist in `mods/jiangyu/LargerMaps/README.md`.

## Offline validation / evidence boundary

- Suite: **92 Python tests passed**. Larger Maps: **109 passed / 6 skipped** (115
  discovered); before port 104 passed / 6 skipped. Nine initial post-port failures
  were stale 2.0.1 version assertions; updated expectations without relaxing guards.
- C# runner: **15,681 assertions passed**, including both changed DLLs and all 19
  package versions/dependencies. Linked legacy-style sources emit nullable warnings.
- `git diff --check` passed. Neither native execution nor CPU performance is proved
  by these tests. Per-module CLI builds are local, not Windows builds.
- Separate canonical BuildKit: `.tmp/MENACE-Suite-ai-followup.zip`, **279 inventoried
  files / 19 packages**, SHA256
  `3728e1c96caf1fd9c51f57c4848c7668c9b3c865baff4d8b9769af7e2d07ac8a`.
  ZIP inventory/path/hash integrity passed. All 17 code packages plus camera rebuilt
  from its clean extracted sources and actual DLL metadata/dependencies rechecked.
  This was local, not Windows; existing warnings remain. Evidence logs:
  `.tmp/ai-build-kit.log` and `.tmp/ai-clean-build.log`.
- Reverified the pinned Windows CLI archive and loader SHA256, plus exact equality
  of all three local SDK DLLs with that archive. No SDK/dependency version changed.
- Original migration ZIP remains intact at SHA256
  `a2f4369e355ea1758b486162bbcf42974691f4d1e50942402d499ad0a0d572b9`.
  Do not treat it as containing these new versions. This review is repository-only;
  package READMEs are bundled, full runtime checklists also live in Suite TESTING.md.

## Gates retained

#1 and #2 remain open until their native/controlled performance tests pass. #13/#17
integration remains blocked on those prerequisites; no global state machine or
blanket re-enabling was added. Windows installer, rollback, saves, UI/other module
acceptance gates from the migration handoff still apply.
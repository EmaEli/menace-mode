# MENACE — daily wrap-up, 15 October 2026

## Current status

**Work paused at the user's request. The original backlog is not complete.**
The current 0.7.17 candidate is installed on the Windows game PC. Title startup,
native 96x96 backend activation and tactical AI progression are evidenced below.
**Not a fully gameplay-verified release.** No release, merge or verified-on marker.

Branch: `offline/menace-fixes-43-35-24-23`. Source/evidence pushed through `db7608b`
before this documentation checkpoint. Latest remote main `24b96bf` was fetched and
confirmed an ancestor; no merge was needed. Earlier offline handoffs are historical.

## Implemented and consolidated

- **#43, HUD click-through:** shared pointer capture for Coordinated Fire/Overwatch,
  capture through release and lifecycle cleanup. Offline checks pass; targeted
  in-game click-through acceptance remains open.
- **#35, Veterancy names:** display-only nickname/name resolution and GUID rejection;
  no XP/identity/schema changes. SquaddieVeterancy 1.10.2.
- **#24/#23, Overwatch:** partial K-PAC tag fix and confirmed native range/angle gate
  before queuing and firing. Overwatch 0.32.3.5. ARC Range/PPP CQB remain undiagnosed.
- **#33/#15, camera/settings:** Cinematic Action Camera 1.0.0 with Jiangyu settings
  companion and one MelonLoader camera runtime; MenaceModSettings 0.5.0, stable
  assembly identity 1.0.0.0, preference-preservation guards. Full camera acceptance open.
- **#29/#31/#38, consolidation:** 19 independently versioned Jiangyu packages,
  dependency/metadata checks, shared host/settings contracts, WRR override/owned-restore
  audit and Field Logistics 1.0.11 / Infantry Assault 0.5.8 icon corrections.
- **#1, Smoke Overhaul 0.6.4:** bounded native smoke-decision rules adapted from PR #12;
  no unbounded enemy search or performance claim. Smoke activation still needs evidence.
- **#2, Larger Maps 2.1.0:** selective positioning experiment adapted from PR #18,
  OFF by default and restricted to larger maps. Setup/restore tested offline;
  round progression does not establish this optional feature's gameplay benefit.
- **#34, shared installer:** one staged build/install flow, actual DLL/manifest checks,
  recognized duplicate retirement, timestamped backup and verified rollback journal.
  Automatic pinned private .NET SDK 10.0.113 bootstrap; machine-wide SDK 6 unchanged.
- **#44/#38 host checkpoint:** main-thread pre-JIT/template lookup fixes already in
  today's starting source checkpoint `ba95434`; later runtime evidence is scoped below.

## Regression investigations — not claimed fixed

- #19 Berserk: exercised existing fee/accounting code; native refund/interruption
  scenarios still require controlled gameplay tests.
- #4 ammunition drain: request-count/diagnostic guards; actual ammo-unit cause unproven.
- #14 water/terrain alignment: pipeline/coordinate review; no speculative Y-offset fix.
- #20 First Aid/Reinforcements: cached native/template contracts and identity guards;
  no pristine-vanilla runtime conclusion or invented second-leader ability.
- #21 Field Repair Kit: third-party source read-only audit and our negative-damage
  guard; third-party mod not patched and compatibility root cause not established.
- #13/#17 tactical coordination/director and #25/#26 passenger fire/leaderless squads
  remain deferred. #27 promotion port blocked on missing canonical SFP 0.7 source.

## 0.7.17 adaptation and validation

See [COMPATIBILITY_0.7.17.md](COMPATIBILITY_0.7.17.md) for hashes and detailed evidence.

- Actual game: `v0.7.17+22362`; observed Steam buildid 25958176, not yet accepted marker.
- Fresh native/metadata dump and 121 proxies cached separately from the old references.
- Compared 15,940 types, 83 Suite/shared/camera C# sources and 8 KDL sources: no existing
  required signature/field-layout/set-path break. Save-state version changed 125 to 126.
- Rebased Larger Maps' new native build map; verified all **26/26 instruction sites**
  against actual new PE bytes. Unknown binaries still retain safe 64x64 fallback.
- Rebuilt all 17 code packages, both data packages and camera against fresh references;
  real assembly/file versions, manifests and dependencies passed.
- Passed earlier in this checkpoint: **96 Suite Python tests, 116 Larger Maps Python
  tests (4 optional checks skipped), 15,681 C# offline checks**. Not Harmony gameplay tests.
- Real Windows PowerShell 5.1 private-SDK bootstrap passed 36 behavior checks.
- Canonical Windows full Suite build-only and full installation passed, zero compiler
  errors. Original local SSH wait timed out; remote completion independently confirmed.
- Windows synthetic deployment tests passed: recognized aliases/protected files,
  manual hash-verified rollback, partial destination write then injected copy failure
  and automatic verified rollback. Not a real power-loss/reboot recovery test.

## Windows deployment and user gameplay evidence

- Full canonical installer installed 19 packages, camera runtime and pinned Jiangyu
  loader; staged/live hashes verified. No manual partial deployment.
- Recoverable originals/legacy targets and rollback journal are outside Mods:
  `_MENACE_Backups/20261015-190105-332f429947994ae78a4c42c76707f0cc/`.
- Installer before/after hashes: **748 protected UserData/unrelated-mod files unchanged**.
  Installer never writes saves. Later game-driven persistence is a separate question;
  this is not a claim that gameplay cannot write settings/save sidecars.
- CustomSupplySliders source/install and external Custom Leader were not modified.
- User title test passed: native **96x96 backend active**, 128x128 pathfinder, no fallback.
- Subsequent tactical log proves **64 consecutive AI actor activations**, rounds **1→4**,
  all `pending=0`/`droppedTasks=0`; 60 closed by finish, 4 by next activation.
  These are not 64 faction turns. First-round stall did not reproduce in this session.
- Initial analysis missed those summaries; corrected explicitly in docs and #45.
  No repeat test is needed to establish this already-recorded AI progression.
- Two ERROR lines remain the known external Custom Leader/BlackMarket.FillUp failure.
  Twelve warnings include three additional scene-related entries not yet classified.
  No new ERROR entry versus title baseline. Last observation: game closed.

## Artifact and repository state

- Current local kit: `.tmp/MENACE-Suite-0.7.17-candidate.zip`, 279 inventoried payload files.
- SHA256: `bdeb17d1798dade1acafee3c0152c00213a9b0af23905bf89ff9a35b5fb13f99`.
- Payload source base `12446f9`; later commits add tests/evidence, not new runtime code.
- ZIP/hash rechecked at wrap-up. Older ZIPs retained, not current installation targets.
- No tracked pending code edits at wrap-up. Untracked `.tmp/`, CustomSupplySliders and
  camera `.jiangyu/` build cache deliberately excluded; no blanket add/cleanup performed.
- Main checkpoints: `7263ada` source consolidation; `16df948` private SDK; `12446f9`
  0.7.17 adaptation; `18ddb60` Windows transactions; `715a800` title; `db7608b` AI evidence.

## Resume here — outstanding gates

1. Read this handoff, compatibility evidence and #45. Check current game version/hash
   before further native testing; do not silently reuse old proxies after a patch.
2. Classify the three scene warnings and establish existing-save load provenance.
3. Finish #45 step 5: WRR debug range pass with zero write failures then debug OFF;
   actual Overwatch/Coordinated Fire/Smoke activations and Field Logistics resupply.
   Vehicle skill configuration alone is not proof of successful resupply.
4. Preserve separate acceptance for HUD, names, camera/preferences, ammo/restore/XP and
   old saves/new campaign/transitions. Do not turn a short smoke test into full approval.
5. Finish installer standalone/double-click/error/cancel/logging/failure-matrix tests.
6. Only after all step-5 checks: accept the buildid and implement mismatch warning plus
   explicit confirmation. No global release claim, issue closure or PR merge implied.

Keep diagnostics minimal/OFF normally. No controlled AI performance improvement has
been demonstrated. User launches the game; retrieve logs directly rather than asking
for screenshots or unnecessary repeat tests. No further deployment requested at wrap-up.
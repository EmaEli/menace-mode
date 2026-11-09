# AIPerformance 1.7.0: follow infantry and remember observed threats

Tracking: [#13](https://github.com/EmaEli/menace-mode/issues/13),
[#17](https://github.com/EmaEli/menace-mode/issues/17).
Supersedes the 1.6.1 support policy, not its historical installation evidence.
The rejected gameplay and correlated East/West retreats are recorded in
[the feedback investigation](AI_COMBINED_ARMS_FEEDBACK_2026-11-09.md).

## Changed behavior

- Support scoring never adds backward origins. Holding remains eligible even when
  ahead of/close to infantry. Empty-lane positioning rewards moving only when catching
  up from more than six tiles and reducing the gap; real useful fire can justify other
  forward/lateral positioning. No ideal-gap withdrawal incentive remains.
- Vehicle Move choices with a compatible nearby infantry request are held if moving
  backward without better native safety or attack utility. Useful native attacks win;
  actual danger reduction, panic and other native emergency restrictions remain native.
- Distinct advance allocations can match within a 24-tile destination corridor with
  agreeing directions. Defense, mixed/opposite orders and arrival remain excluded.
- Known movement cost plus stance/setup/fire reserve must fit AP; final vehicle choice
  rechecks the native clipped path cost because preprocessing often has APCost=0.
  This can hold an expensive move rather than promise an impossible move-and-fire.
  It neither adds path searches nor creates a cheaper alternative path.

## Limited threat memory and local flank

Only detected, live, non-contained hostile player/ally actors are inspected. An enabled,
armed, applicable direct-fire attack must reach and have LOS to advancing infantry.
Main-thread observations store faction-scoped managed coordinates/range/round, not
Actor/Skill references. Detection is checked before position, weapon or faction reads.
Turn-start observations also establish memory after loading; stale native TTL entries
never refresh hidden coordinates. Up to four visible opponents are inspected at turn
start; repeated detection callbacks share a 32-observation round cap.

Memory lasts two rounds after observation, with a 3/5/7-tile uncertainty radius. A
bounded infantry response can seed up to four existing local forward/lateral tiles
outside the possible firing lane and uncertainty area, no more dangerous than current.
It adds movement utility, not attack candidates or claimed enemy damage. This is a
heuristic using the last observed firing position and current terrain, not proof that
the opponent remains there or that the whole uncertainty region is screened.

Native Move retains final ranking, reachability, paths and AP. The response has 16
shared probe tokens and a soft 5 ms probe-start limit; there is no global flank search,
extra task, guaranteed squad maneuver or guaranteed frame-time bound. Sorting is
managed-only. Partial writes restore the previous utility values. No map/round rewind,
faction crossover or hidden-position refresh is permitted.

## Explicitly not implemented

Mortar/HE, smoke and suppressive responses to uncertain contacts remain **native**.
This candidate implements the requested flanking alternative; it does not inject
synthetic mortar/smoke candidates, ignore AP/setup/range, fire on hidden Actor handles,
or bypass native friendly-fire/area legality. Those actions need a separately verified
uncertain-ground-target and dispersion/safety contract before implementation.
Stationary anticipatory deployment, infantry waiting and reaction fire remain deferred.

## Verification status

The runtime builds against reviewed 0.7.18 proxies with zero warnings/errors. Focused
managed fixtures include the captured East/West withdrawal geometry, near-infantry
holding, forward follow, real danger avoidance, shot priority, clipped-path reserve,
neighboring/opposite orders, memory expiry/isolation/hidden-read guards, and the linked
production observation/scoring hooks. Offline checks are not IL2CPP dispatch/gameplay.

Local evidence: 18,009 selected shared-artifact checks, 302 settings/behavior checks,
13,035 linked recovery/support/threat checks, 189 native metadata contracts, 207 Suite
Python tests and 155 LargerMaps Python tests (eight explicit skips). The 69-file
single-module installed-companion BuildKit passed inventory and clean extracted-source
builds. Shared offline tests retain pre-existing nullable warnings; production builds
have zero warnings/errors.

## Canonical Windows test installation — 9 November 2026

- Reviewed native game hash and all **119 proxy DLLs** match the local references.
  Game closed; MENACE **0.7.18+22828**, Windows PowerShell 5.1, private SDK **10.0.113**.
- Transfer roundtrip/script integrity, archive SHA256 and 69-file inventory verified.
  ZIP SHA256: `cc191404d24a6ce54f2d38a248e13085e9ad6e6e6b40584729f0903c73ac0f0a`.
  Candidate uses the working snapshot based on dev `d3caab9`; unrelated dirty gameplay
  source files are not selected/installed. Documentation evidence was updated afterward.
- Canonical Standalone **BuildOnly and installation passed**, zero production warnings/
  errors. Both Windows runs passed 189 metadata / 302 behavior / 13,035 linked checks.
- Installed manifest / assembly / file: **1.7.0 / 1.7.0.0 / 1.7.0.0**.
  DLL SHA256: `F766C34AC22CF7C5854B9D624677C73C23A7E885AC92DDFF0FD9BF7B733E7715`.
- Affected-only backup: `_MENACE_Backups/20261109-150957-a9a71abcb1d646548cb960db58bd9d4a`.
  Original 1.6.1 DLL matches `CF839A5CA1A531CE2265C2B1171AFE512E537E2AB40ACFA6780185B95380CA58`;
  all moved-original and installed journal hashes verified. Only AIPerformance and its
  loose-DLL conflict are journal targets. MenaceModSettings/Jiangyu.Loader were reused.
- **1,567 protected Mods/UserData/proxy files unchanged**, with matching file count and
  hashes before/after. No settings, saves or unrelated mod files were changed. Game closed.
- Retained stage and Build/Install logs:
  `E:/MENACE-Work/Builds/AIPerformance-1.7.0-follow-20261109/`.

## Next gameplay check

Keep the existing **Infantry-led vehicle and heavy-weapon support** and bounded
diagnostics enabled, restart, and try a short Rogue/pirate engagement. Check whether
vehicles hold/fire or catch up rather than back away merely for spacing, and whether
advancing infantry takes a covered side route after losing an observed ranged threat.
Native AT avoidance may still retreat; no flank is promised where no covered local
candidate exists. Mortar/smoke uncertain-contact coordination is still not implemented.

No fresh 1.7.0 startup, native hook dispatch, gameplay or turn-time acceptance yet.
Retrieve Latest.log after the user's result. No issue is closed and no gameplay
improvement is claimed from compilation/installation alone.
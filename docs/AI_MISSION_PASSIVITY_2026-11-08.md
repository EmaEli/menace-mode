# AIPerformance 1.3.0: two-order mission still fails gameplay

Tracking: [#2](https://github.com/EmaEli/menace-mode/issues/2), related
[#17](https://github.com/EmaEli/menace-mode/issues/17).

## User result

The user stopped the mission because very few enemies appeared and considers this
mission type broken. This is rejected gameplay, not acceptance of 1.3.0. Explaining
that a walker is outside contact convergence does not address the mission-wide fault.

## Capture and scope

- MENACE was confirmed closed by the capture script. Installed AI manifest: 1.3.0;
  startup confirms MENACE 0.7.18+22828, LargerMaps 2.6.2, 96x96, advance recovery ON,
  contact convergence ON and fast planning modifying 226 attack/three buff profiles.
- Full final log: 8,152,780 bytes, SHA256
  `b2ecd4295da45b23de3203c661cfe4488bf83e03b122b2fe747f9fefe350aae4`.
  It contains the previously reviewed 6,068,177-byte live snapshot as an exact prefix.
- Private capture: `.tmp/Latest-mission-final-20261108-a.log`; no raw logs published.
  Script upload/download round trip and remote/local script and log hashes agree.
- One observed map, rounds 1-26. The final round need not have completed before exit.
  The exact mission template ID is not established by this log.

## Observed behavior

- 18 hostile teams/vehicles initially; 16 alive/two dead at round 26. Enemies are
  present, but presence is not participation. All 446 hostile round-start snapshots
  show sleeping=False; this is not a sleeping-faction explanation.
- Every hostile changes round-start tile, but only on 1-5 observed transitions.
  At round 26, 15/16 survivors have stationary counters of at least 15 turns
  (range 15-24); the other survivor has stationary=1. Most units stall far short
  of their assigned areas, rather than merely disappearing from the player's view.
- Only two accepted hostile attack Use calls are recorded, both medium-mortar
  launches against a civilian.worker in round 17. No accepted direct-fire attack
  call is logged. These are calls, not a projectile/hit count.
- 6,730 logged attack evaluations: 6,701 zero/empty, 26 zero with candidates,
  three positive. 159 route-limit records exist across the full log, but none for
  SKILL or STATE-round-start; no route-buffer overflow or observation failure.
  Treat action totals as observed evidence, not universal instrumentation proof.
- Concrete round-20 infantry example: AP=100, awake, morale=1, suppression=0,
  two AdvanceTowards zones, 65 movement candidates, null destination, Move=0 /
  Idle=1. The same actor remains at (88,85) through round 26, stationary=23.

## Confirmed mod coverage gap, not sole-root-cause proof

All hostile round-start records after round 1 have two AdvanceTowards orders,
areas (60,50,5,6) and (36,50,6,5). There are 1,256 hostile agent-start observations
with two zones, plus 18 initial observations with zero zones before assignment.

- `AdvanceRecoveryRuntime.TryApply` rejects `zones.Count != 1` at the Orders guard.
- Its destination/utility context also rejects `zones.Count != 1`.
- `ContactConvergenceRuntime.TrySeed` rejects `zones.Count != 0`.

Consequently the stable two-order state is excluded from both mod helpers. Startup
activation does not imply recovery coverage, and this is not just a walker exclusion.
Removing the guard blindly is unsafe: multi-zone scoring and order selection need
review; existing single-zone formulas must not be applied to an arbitrary first zone.
Fast planning's disabled firing-position search remains another known concern;
the guard mismatch does not prove it is the sole reason for sparse combat.

## Previous session is a different scenario

The preceding captured match had 29 enemies initially, 22 alive/seven dead at round
18, zero assigned zones, and 48 logged accepted hostile attacks. Many actors moved
frequently; 353/456 round-start records were sleeping=True, the rest False.
This is not an ON/OFF comparison: roster, orders, mission and sleep states differ.

## Next target and verification status

Investigate native multi-AdvanceTowards scoring, then implement a bounded stalled-order
recovery only for compatible advance orders. Preserve defensive/mixed orders, fog of
war, native path/AP/safety ownership and fast turns. Test order changes and conflicting
destinations rather than extending contact convergence to every assigned unit.

Captured-log integrity/count/source-guard assertions pass. No gameplay code, installed
mods, settings or saves were changed. No build or new gameplay test was performed.
Both issues remain open; no request for another identical long user test.

## 1.3.1 correction — 8 November

User authorized implementation and the normal PC test installation. Reviewed native
ConsiderZones distance/inside-utility branches on both exact supported binaries:
multiple advance areas use Entity.ID modulo advance-count allocation; the allocated
zone has distinct distance weighting, and only that zone receives inside utility.
The new path therefore does **not** sum single-zone repairs or choose the first area.

- Accept at most eight exclusively AdvanceTowards zones with valid areas/priorities.
  Preserve native multi-zone distance scores. After two completed idle rounds, recover
  only the normal Move utility threshold on no-less-safe tiles closer to the ID-allocated
  zone. Native destination/path/AP and all existing actor/combat/arrival guards remain.
- Preserve exact ordered area/priority context, including unselected zones; revalidate
  it before utility and choice. Changes invalidate pending eligibility/history.
- No contact-convergence, simplified-planning, settings or off-map targeting changes.
- Actual-hook two-order fixture failed on 1.3.0 before production edits. The corrected
  fixtures pass 12,633 recovery/contact checks, including odd/even ID, stale choices,
  repeat evaluation, order mutations, mixed orders, safety, arrival and count bounds.
- 11 exact-native tests pass on each original 0.7.17/0.7.18 binary. 107 current proxy
  metadata contracts, 29 planning checks and 207 Suite Python tests pass. LargerMaps
  Python: 149 run, 141 pass, eight environment skips. Updated three stale source-literal
  unload assertions for the existing 1.3.0 lifecycle; no unload gameplay code changed.
- 18,009 scoped shared C# checks pass. Production 1.3.1 builds with zero warnings/errors;
  the shared managed fixtures emit existing nullable-context warnings on rebuild.
- Closed-PC preflight confirms original 1.3.0 DLL, reviewed native hash and all 119
  local proxy hashes match the current PC. Canonical packaging/installation follows;
  these offline checks do not execute native IL2CPP or prove restored combat.

## Canonical Windows installation completed

- Source: local `dev` commit `2bc037b`. Canonical standalone installed-settings BuildKit:
  54 inventoried files, AIPerformance only. ZIP SHA256
  `ad1fc434318a0bc1224f8df32b3b796a226839ca915c23fbb53bd2498011bcd9`.
  Local inventory/clean extracted-source rebuild and actual DLL metadata gates pass.
- Canonical Windows BuildOnly and install pass on MENACE 0.7.18+22828 with private SDK
  10.0.113, zero production warnings/errors; 107 metadata, 29 planning and 12,633
  recovery/contact fixture checks pass again before staging. MENACE closed throughout.
- Installed manifest/assembly/file versions: **1.3.1 / 1.3.1.0 / 1.3.1.0**. DLL SHA256
  `3248324066a87fbc03d3cc5303a5a4f6ef8816ea7836c2c11e027b4dc329d8d1`.
- Backup: `_MENACE_Backups/20261108-154531-223bf5a14969475ba633bf8e9ca2d655`.
  Journal scope is AIPerformance and its known legacy DLL slot only. Backup originals
  match 1.3.0; all installed files match the canonical journal. No manual live copying.
- All **1,561 protected Mods/UserData/proxy files** unchanged across BuildOnly/install.
  Installed MenaceModSettings and Jiangyu.Loader validated/reused; settings untouched.
  Saves were not read or modified. No additional diagnostics or settings enabled.
- Retained kit/logs: `E:\MENACE-Work\Builds\AIPerformance-1.3.1-multiorder-20261108`.
  Local capture: `.tmp/ai131-windows-install.log`; verification metadata downloaded.
- Post-install startup, native dispatch, combat and turn-time acceptance **not tested**.
  #2 and #17 remain open. Test a normal mission of this type; no prescribed 20–30-turn
  replay. Retrieve fresh Latest.log when the user reports the result.

Tracker records: [#2 deployment](https://github.com/EmaEli/menace-mode/issues/2#issuecomment-6722905423),
[#17 scope](https://github.com/EmaEli/menace-mode/issues/17#issuecomment-6722905694).
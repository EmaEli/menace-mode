# Contact-led alert and persistent range closing — 9 November 2026

## Request and rejected baseline

Tracks #2 / #13 / #17, after the rejected 1.8.1 mission in
[individual engagement feedback](AI_INDIVIDUAL_ENGAGEMENT_FEEDBACK_2026-11-09.md).
The immediate request is not another attack multiplier or the larger battlefront director:
after a legitimate sighting, eligible units should converge broadly toward the known area,
keep closing until they can engage, use firing cover rather than cover-only stops, and
use equipped smoke when exposed within the opponent's range. Vehicles participate too.

The captured mission showed all hostile snapshots with no advance zones, many sleepers,
and weapon teams/vehicles left outside the replacement planning scope. It did not prove
that every zero attack score was caused by simplified future-position search.

## Implemented candidate: AIPerformance 1.9.0

- Existing default-ON `OffensiveCombatPlanning`, restart required; no new setting.
- Main-thread detection/turn/before-processing hooks observe only faction-detected
  player/allied-player opponents. Snapshots retain coordinates and direct-fire range,
  never native actor objects. Detection precedes enemy position/weapon/death reads.
- A legitimate contact alerts all eligible faction actors, not three leased responders.
  Scope: unassigned or exclusively advance-ordered pirate/Rogue infantry, direct-fire
  teams and the existing recognized human-crewed vehicle whitelist. Explicit defense,
  mixed orders, avoid-opponent roles, deployment, drones/xenos and indirect fire remain native.
  Suppressed/broken units are not forced to advance; useful native emergency actions win.
- Managed areas persist up to 12 rounds without hidden refresh. Arrival within the
  uncertain last-known area hands control back to native search. Mission reset, faction
  isolation, expiry and monotonic-round guards prevent stale cross-mission pursuit.
- Local existing native candidates receive forward positioning utility and modest stable
  lateral preferences. Non-progress cover-only utility is removed only while this policy
  owns an eligible no-useful-action decision; native safety and path costs are unchanged.
- Currently detected targets permit up to two direct-fire profiles, 16 range/LOS lane
  checks and a soft 10 ms probe-start cutoff. Setup/deployment/fire reserves use the real
  native skill costs, including moving packed teams. Expensive native usability/value
  probes share the existing 24/48/512 evaluation/actor/faction token budget.
- Geometric lane staging survives exhausted expensive-probe budgets and fresh score
  collections. It is positioning utility, **not** fabricated damage/attack utility,
  guaranteed target legality or forced execution. Native Move/Attack retains all final
  resource, target, setup, path and execution decisions.
- Positive native damage or suppression-fire candidates can take precedence over Move/Idle.
  No fabricated candidates, AP grants, setup state writes or new artillery executor.
- Existing Smoke Overhaul 0.9.0 bridge accepts contact-led ordinary infantry requests
  when exposed within the observed opponent's direct-fire range and without its own
  firing lane. Covered/out-of-range approaches do not request smoke merely for distance.
  The existing equipped-grenade executor owns inventory, throw legality, AP and execution.
- The older wait-for-support route veto does not stop an eligible contact-led advance.
  This deliberately changes the rejected passive behavior; native safety is still scored,
  but an exposure-free route or automatic successful smoke throw is not promised.

## Evidence and limits

- Release production DLL: zero warnings/errors against the reviewed 0.7.18 proxies.
- 13,230 production-linked managed checks; 302 planning/settings checks; 196 metadata
  contracts. These use stand-ins/metadata and do not execute IL2CPP gameplay.
- Regression cases include 32-squad alert, fresh repeated scoring, multiround hidden-area
  approach without hidden reads, stable left/right preference, crewed vehicles, setup AP,
  suppression shots, smoke range/LOS gates, expiry/arrival/reset/faction guards and rollback.
- Exact reviewed native hash is pinned by the existing byte-contract tests. `SetSleeping`
  at RVA `0x4D5ED0` is `88 51 51 C3`: only the sleeping flag is assigned, no AP/action call.
  The new setter contract joins the existing native phase/path/setup contracts.
- Broader regressions: 208 Suite Python tests passed; LargerMaps ran 156 tests with
  eight expected unavailable-fixture skips and no failures. One initial Suite failure
  exposed the stale source inventory row; that row was corrected and all 208 reran cleanly.
- Canonical installed-companion one-module BuildKit: 78 inventoried files; local clean
  extracted-source build and real manifest/assembly/file versions 1.9.0/1.9.0.0/1.9.0.0
  verified; 17,986 scoped shared offline checks passed. Production compile: zero
  warnings/errors. Existing nullability warnings occur only in the shared test harness.
- Committed/pushed implementation: `e017a2c40389b47f05ca757286d51e363bcb2115` on `dev`.
  All 73 tracked source payloads in the kit exactly match that commit; no unrelated
  pending gameplay, installer or logging changes are bundled. Settings/loader are reused.
- Windows build/install on menace-pc (10 November): canonical standalone
  `AIPerformance -UseInstalledSettings` BuildOnly and install both passed against
  MENACE 0.7.18+22828 and the exact allowlisted native SHA256. Windows production build
  had zero warnings/errors; 196 metadata, 302 managed planning, and 13,230 advance
  recovery checks passed. On disk, manifest/assembly/file versions are 1.9.0/1.9.0.0/
  1.9.0.0; installed DLL SHA256 `4647a44163ee983ace724b09833c4acb151503e8ab6497cdea0133706afe440a`
  matches the canonical Windows build output. Settings 0.8.2 and the pinned Jiangyu
  loader were preserved. Affected-only backup/journal:
  `M:\SteamLibrary\steamapps\common\Menace\_MENACE_Backups\20261110-081616-3058aa2e2da14c33b193fd32843b62e7`;
  only `AIPerformance` and `AIPerformance.dll` were targeted. Candidate ZIP SHA256:
  `57eec5baa309531147e25fb01b00d0c236204e512a620f0cac392fb4374f5794`.

## Next test and limits

The game was left closed after installation. Startup, native hook dispatch, movement,
setup/fire, smoke, route quality and AI turn-time remain unverified; no gameplay
acceptance or Nexus release is claimed. Launch a pirate/Rogue engagement: distant
squads/vehicles should close over several turns, seek a firing position, use native
setup/fire, and request smoke only when equipped and legally executable. Retrieve the
fresh `MelonLoader/Latest.log` after the user reports the in-game result. Keep the
relevant gameplay issues open until then.
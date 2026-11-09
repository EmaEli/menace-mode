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
- Candidate: `.tmp/ai190-source.u_4h4xt3/AIPerformance-1.9.0.zip` (local, not a release).
  SHA256: `57eec5baa309531147e25fb01b00d0c236204e512a620f0cac392fb4374f5794`.

## Deployment blocker and next test

`ssh menace-pc` currently fails with **No route to host** at the configured LAN address;
`DESKTOP-H4PILPA.local` also fails local hostname discovery.
No current game-closed check, Windows BuildOnly, installation, backup or gameplay check
was possible. No PC saves, settings, installed mod files or unrelated mods were changed.
The prior verified PC version remains 1.8.1; it has not been freshly audited here.

When the PC is available, run the canonical one-module BuildOnly/install with the game
closed, reuse settings/loader and verify affected-only backup, installed versions/hashes
and protected files. Then test a short pirate/Rogue engagement: distant squads/vehicles
should close over several turns, choose cover that permits fire, use native setup/fire,
and throw smoke only when equipped and legally executable. Retrieve fresh Latest.log.
Actual dispatch, shots, smoke, route quality and AI turn-time remain unverified; keep
the issues open. No Nexus release or gameplay acceptance is claimed.
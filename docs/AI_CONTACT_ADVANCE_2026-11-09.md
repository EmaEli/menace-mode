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

## 1.9.0 gameplay feedback and 1.10.0 follow-up — 10 November 2026

The 1.9.0 mission was played to the end. Enemies used smoke sensibly (fire first, smoke
with spare AP, or when suppressed) and the units that were met fought well, but vehicles
and infantry only moved to cover: nobody pushed or flanked, and most of the map's enemies
never joined. Battle escalation came only from the player side.

Log analysis (`.tmp/latest-20261110-ai190.log`) and the 0.7.18 disassembly explain it:

- Native `ConsiderZones.Evaluate` adds a per-tile `DistanceScore` term for `AdvanceTowards`
  zones (`DistanceToZones` 1.0 × `DistanceToAdvanceZones` 2.0 × zone importance × priority),
  scaled by `TileScore.DistanceScale = 50`: about 100 points per tile toward the zone.
  That gradient produced the 3–5 tile advances of rounds 1–4 (`targetDistanceScore`
  2131/3350 in `AI ROUTE MOVE-EVAL`).
- Both `ConsiderZones.Evaluate` and `ConsiderZones.PostProcess` skip `AdvanceTowards`
  zones entirely when `AIFaction.HasKnownOpponent()` is true. The flag is per faction: one
  squad seeing the player removes the advance objective from everyone. From round 5 on,
  `knownOpponents` was non-zero on every agent-start snapshot and `targetDistanceScore`
  was 0 for all evaluated moves; the 1.9.0 tile utilities (+12..72) cannot replace a lost
  ~100/tile gradient, so cover/threat criteria won and units stayed put.

1.10.0 reuses the native mechanism instead of out-scoring it: `ContactAdvanceZones.cs`
adds a Harmony prefix on `AIFaction.HasKnownOpponent` that returns false only while a
`[ThreadStatic]` scope is open. The scope is opened by `Priority.First` prefixes on
`ConsiderZones.Evaluate(Actor, TileScore)` and `PostProcess(Actor, Dictionary<Tile,
TileScore>)` on the AI thread and closed by their postfixes, so the advance recovery
postfix and every other reader still see the native value. Eligibility
(`ContactAdvanceRuntime.KeepAdvanceZones`) is read once per actor pass: `Ready` (human
non-indirect pirate/Rogue infantry or recognized crewed vehicle, role not
`AvoidOpponents`, morale ≥ 75 %, suppression ≤ 25 %), advance orders present
(`AdvanceRecoveryRuntime.TryReadAdvanceOrders`), and no useful native attack/suppression
candidate (`CombinedArmsResponseRuntime.HasUsefulAction`). Engaged units therefore hold
and shoot while reserves converge. The scope is reset on turn start, before-processing
and on any disable; exceptions disable the contact advance feature and retain native AI.

Checks: 197 native metadata contracts (three patched signatures added), 13,248 advance
recovery checks (18 new scope/eligibility checks), 302 behavior checks, 17,986 scoped
offline checks, 208 Suite and 156 LargerMaps Python tests, clean extracted-source kit
rebuild against the 0.7.18 proxies and the pinned Jiangyu 1.4.10 SDK. Implementation
commit: `ed27176b3cb16093bb652b15f37d16547561193b` on `dev`; all 74 tracked payloads of
the 79-file one-module kit match it. Candidate ZIP SHA256
`cb0b16346c5c683a16ef6e4dcf65d1d5de863dcb6eb9d6105a7151aa5cd193dd`.

Windows (menace-pc, 10 November): transfer round-trip verified; preflight confirmed the
game closed, native SHA256 `494c9e21…2d12e9`, `Assembly-CSharp.dll` `aee30124…c80be`
(identical to the local build refs) and the installed 1.9.0 DLL
`4647a441…fe440a`. Canonical standalone `AIPerformance -UseInstalledSettings` BuildOnly
and Install both passed with zero warnings/errors and the same 197/302/13,248 checks.
Installed manifest/assembly/file versions 1.10.0/1.10.0.0/1.10.0.0; installed DLL SHA256
`63030dcfc651f4b5259fcc0d63e40d22fb26bce0f7c528e89ca56e106b90f46b`. Settings 0.8.2 and
the pinned loader were reused; 1,455 protected Mods/UserData files unchanged.
Affected-only backup/journal:
`M:\SteamLibrary\steamapps\common\Menace\_MENACE_Backups\20261110-101643-61b85d68c1064609b6feb21d270a6fe4`
(only `AIPerformance` / `AIPerformance.dll` targeted; prior DLL hash verified inside it).
The game was left closed. Startup, hook dispatch and the intended post-contact push
remain unverified in-game.

## 1.10.0 mission log — 11 November 2026

Log evidence from the first mission played with 1.10.0 on menace-pc. This section records
only what the log shows; the player's own assessment is tracked in the issue.

Capture: `Latest.log` copied read-only with the game closed (SHA256 `1b7e58a4…f65f21`,
9,161,710 bytes, 28 rounds, one 96-tile map, 18 Rogue units, 12 player units: 7 squads
and 5 vehicles). Installed
manifest/assembly 1.10.0/1.10.0.0, DLL `63030dcf…90f46b`, native `494c9e21…2d12e9`.
Startup lines show contact convergence, combined-arms support and offensive planning all
enabled; no AIPerformance feature disabled itself.

What the log confirms:

- First contact in round 5. About 51 distinct hostile attack uses were logged (the skill
  hook writes some calls twice; counted by round/actor/skill/target/origin/AP); 45 had the
  MG team at (68,58) as target entity. Rounds 13–17 were a converging attack: conscripts,
  infantry, a Rogue MG team, the mortar and one walker.
- That walker reached the adjacent tile (68,59) in round 13, used native
  `special.vehicle_plow_through_infantry` four times in rounds 15–17, then fired its auto
  laser in rounds 18–22 while the team fell back to (57,48). The team's suppression rose
  to 0.39 and morale fell to 0.53. AIPerformance has no plow-through code; the skill's
  half-cover destruction comes from CoverDestructionRebalance. Cover destruction and squad
  strength are not logged, and the team still had activations through round 27.
- Post-contact movement, same metrics as the 1.9.0 capture (different mission and force,
  so indicative only): unit-rounds with a tile change 77 % (1.9.0: 53 %); median net
  displacement per unit 13 tiles (6); units within 2 tiles of their contact position 0 (5).
- Hostile `Evaluate` time per round: median 1.5 s, maximum 6.2 s (1.9.0 capture: 11.7 s /
  204 s, with 26 units instead of 18). One single evaluation took 4.3 s in round 14.

What the log does not confirm:

- The 1.10.0 mechanism itself. Chosen-tile `targetDistanceScore` was non-zero in only 9
  of 691 post-contact hostile move evaluations (1.9.0: 21 of 726). Three never-engaged
  units with full AP, no suppression, full morale and two `AdvanceTowards` zones showed
  5,000–11,000 before contact and 0 in all 23 post-contact evaluations each.
- The advance that did happen matches the 1.9.0 contact-led utility (move score 49 or 37)
  and is slow: 60 % of post-contact unit-rounds are a one-tile step, and 113 of 276
  post-contact non-vehicle unit-rounds ended with 80 or more AP unspent. The west-flank
  conscripts, MG team and officer squad needed 23 rounds to cover 15–23 tiles and never
  fired.

Unverified hypothesis: `ContactAdvanceZoneEnter` returns early unless the caller is the
thread that applied the patches, while tile criteria are evaluated on AI worker tasks, so
the scope never opens. The offline fixture is single-threaded and cannot show this. A
compiler-inlined `HasKnownOpponent` read or a failing eligibility read are alternatives.
The hook has no diagnostics, so the log cannot distinguish them.

Unrelated warnings in the same log: 51 Weapon Mechanics Rebalanced mortar outer-ring
`InvalidCastException` (`Structure` to `Actor`) and 2 Garrisonable Buildings
`Actor.GetCurrentProperties()` failures.

## 1.10.1 gameplay feedback — 11 November 2026

**Not accepted.** The player reports northeastern infantry arriving singly into MG fire,
no effective vehicle support there, a western vehicle ambushed without an infantry
screen, infantry attacking the tower mortar, and southwestern smoke/vehicle presence
without a useful follow-through. These compass descriptions are player observations;
log coordinates have not been independently mapped to those directions.

Closed-game read-only capture: 9,564,092 bytes, 26,981 lines, SHA256
`d66b93c44631d2e2205e44da77aaa6597349210e3c2d4f15b2a58572ddc52461`.
Local/remote snapshot hashes and transfer-probe hashes match. Installed manifest/assembly
1.10.1/1.10.1.0, DLL SHA256
`85eebee89fc09f8211995477ce43ec676769eba3d5750cbab376d2909eece3e1`;
native binary `494c9e21…2d12e9`. One 96-tile map, rounds 1–25, 18 initial Rogue units.

### What the log establishes

- All 18 hostiles changed round-start position during the mission. After contact in
  round 3, 314/386 adjacent unit-round samples changed tile (81%). Movement is not
  evidence of coordinated combat or successful expiry of this particular guard.
- 17 distinct accepted hostile attack records: 10 mortar fragmentation and 7 sustained
  rifle records from three conscript squads. Deduplication uses round, actor, skill,
  target, origin and pre-use AP; these are hook records, not projectile/hit counts.
  Only round 20 has two distinct attackers; no round has three. No attacks are logged
  from any of the three walkers, either MG team, or the autocannon team.
- Four accepted hand-smoke throws. One infantry squad throws from (75,41) in rounds
  15, 17 and 19 toward adjacent tiles, then remains/shuffles around (75–76,41) through
  round 25 without firing. The nearby walker ends at (77,40), also without firing.
  Another conscript squad throws in round 21 and logs rifle fire in round 24; smoke
  is therefore not universally followed by permanent inactivity.
- Combined-arms diagnostics publish no eligible infantry in round 1 because advance
  orders are absent, but all 11 are eligible from round 2. This is not a persistent
  whole-mission exclusion. The 70 plan records include 43 score-seeded plans; every
  logged seed has attack utility zero. Pairing/staging did not establish fire support.
- Three exposed-route Move-to-Idle replacements are logged in round 9. Existing
  combined-arms diagnostics exhaust their 256-record mission cap during round 10.
- Native-zone distance scoring remains nearly absent after contact: only 14/858
  hostile Move evaluations have nonzero selected-target distance score. This alone
  does not identify which eligibility/thread/native-read boundary failed.

### Limits and next step

The new default-OFF `DiagnoseContactAdvance` option was not enabled for this test.
There are zero `CONTACT-ADVANCE` records. The agent's previous test instructions
omitted that prerequisite; neither guard expiry nor the suspected thread mismatch is
directly verified. Do not ask the player for another identical mission as if it were.

The installed Smoke Overhaul 0.9.0 integration selects equipped hand-grenade skills,
not mortar smoke. The log does not establish that the hostile mortar had a legal
smoke-round skill or ammunition; adding that capability requires checking the actual
equipment/native firing contract, not granting an invented attack.

Recommended follow-up: resolve the contact-advance execution boundary and add a
bounded shared support/crossing decision for nearby infantry and support, rather than
another isolated aggression/timeout adjustment. Smoke should protect a feasible
crossing; fire support must be usable, not just geometrically nearby. Preserve native
paths, legality and legitimate contact information, with bounded fallback so groups
cannot wait forever. This is proposed work, not an implemented or accepted feature.
No gameplay code, installed mods, settings or saves were changed during this analysis.

## 1.11.0 implementation — 11 November 2026

The player authorized the follow-up above. This is a materially different candidate,
not acceptance of 1.10.1 or another request to repeat the same test.

- Native zone scopes no longer require the patch-installing thread. Eligibility is cached
  per worker/actor/processing generation; scoped faction reads are thread-local, nested
  scopes restore their caller, and Harmony finalizers clear exceptional exits. Processing
  and scene changes invalidate other workers' old scopes. Main-thread contact observation
  remains main-thread-only. Managed tests exercise worker, nesting, exception and reset paths;
  actual IL2CPP hook dispatch remains to be observed in the game.
- Small local groups share roster/contact, cohesion, fire/smoke receipts and staging deadline.
  Four infantry/two support maximum; bounded spatial matching, no global pathfinder or
  native initiative/scheduler replacement. Exposed lead infantry can stage for support;
  vehicles cannot run past their infantry screen during the coordinated window. Accepted
  native fire at the legitimately known sector can release the group's advance. Nearby
  artillery can contribute actual native fire without being given a fabricated attack.
- Smoke requests require an equipped, enabled grenade with uses, its actual AP cost and
  an existing affordable forward movement candidate afterward. Successful forward smoke
  is shared, visible smoke must exist before exposed crossing, and forward smoked tiles
  get movement utility. Another grenade is not requested from the same stationary group
  until infantry makes three tiles of progress. No equipment, ammo or AP is granted.
- Two-round waiting is shared, and regrouping, leader loss and contact changes cannot
  restart spent waiting time. Native positive attacks/suppression and emergencies win.
- A projected lane at the current origin no longer counts as an executable native shot
  that can park/shuffle a no-shot unit. Movement-only utility clears the native 1024
  post-utility AP sentinel; the separate native movement cost stays unchanged. Real firing
  origins still reserve actual setup/fire AP. This sentinel issue is regression-tested;
  it is not retrospectively asserted to explain the player's entire previous mission.
- Both diagnostic settings remain OFF by default. The already enabled combined-arms trace
  opt-in also enables contact/group evidence, without modifying settings. Half the 256-line
  contact budget is reserved for group evidence; repeated choice sampling leaves room
  for late actual fire/smoke receipts. A once-per-mortar opt-in capability record checks
  the friendly equipped skills/uses on the next live mission.

Catalog check: the extracted `enemy.rogue_army_mortar_weapon_team` equips
`specialweapon.tripod_medium_mortar_80mm`, granting `active.launch_medium_mortar_frag`,
`active.infantry_deploy_heavy_weapon` and `passive.encumbered`, with no explicit unit
skills and only `infantry_default` skill group. Its description mentions smoke, but
that is not an equipped smoke skill. This older extracted catalog is not proof of the
current live modded loadout; mortar smoke automation is deliberately not implemented.

Source and test changes are confined to AIPerformance, its linked advance test harness
and this evidence/inventory. Unrelated pending gameplay/installer work is preserved and
will not be bundled in the standalone candidate. Local/Windows checks are recorded below
as they complete. Gameplay acceptance remains pending and #17 stays open.

Local gates: production Release build has zero warnings/errors; 13,442 linked recovery,
contact and shared-group checks, 303 AI behavior checks and 208 exact native/proxy/artifact
contracts pass. Suite Python: 208 passed. LargerMaps Python: 156 run, eight expected
unavailable-fixture skips. Isolated shared/package checks: 17,986 passed (existing shared
test-fixture nullability warnings only). Initial Python inventory failure was the not-yet
refreshed compiled manifest; after rebuilding the 1.11.0 payload, all 208 tests reran cleanly.
The isolated snapshot initially inherited a scratch SDK6 pin; an isolated local SDK10 pin
corrected that test setup without changing repository/global/machine SDK configuration.

Closed-PC preflight: transfer round-trip hashes match; current native binary and proxy
hashes match the reviewed 0.7.18 references. Existing combined-arms gameplay/diagnostic
flags are ON; no settings edits required. Captured 1,458 protected Mods/UserData files,
177 runtime/proxy references and three affected 1.10.1 files for later verification.
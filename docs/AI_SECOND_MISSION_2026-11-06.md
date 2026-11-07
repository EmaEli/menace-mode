# Second-mission stalled advance on MENACE 0.7.18

Regression investigation for [#2](https://github.com/EmaEli/menace-mode/issues/2),
related to #57, #93 and #94. Initial diagnosis followed by the scoped candidate below.

## User feedback and capture

The user reports: first mission AI was fast and good; second mission had no AI
in sight, resembling the previous bug-faction problem. Accept that first-mission
feedback without extending it to all missions or factions.

Private captured log: `.tmp/Latest-second-mission-20261106.log`, 20,004,950 bytes,
57,150 lines, through 16:26:19.689. Local SHA256:
`646a1cf519f594fb8051cbdaa5ad0371d50eda56749f2798c6575d711fa55cce`.
This is a point-in-time copy of a live log; the remote source may continue growing.
No raw log is published. Startup: game 0.7.18+22828, LargerMaps 2.6.1, 96x96.

## Second map, isolated from the successful first map

First map: `2847025D510`. Second map: `2853CFEF360`.
The second tactical setup at 16:14:45 reports 226 attack / three buff profiles
adjusted again; this is not evidence of a missing second-scene setup.

- **18 Rogue Army enemies spawned**, not aliens. Seventeen remain at round 16.
- Six heavy teams never change round-start tile from rounds 1 through 16:
  two mortars, three medium machineguns, one autocannon.
- Two walkers move initially, then remain stationary for 14 rounds by round 16.
- The stalled heavy teams are awake and have a single `AdvanceTowards` order
  toward `(47,43,9,13)`; several remain 33-60 tiles from that area.
- Example mortar at `(94,65)`, round 15: AP=100, morale=1, suppression=0,
  19 movement candidates, Move score=0, Idle score=1, all other behavior scores=0.
  It is not simply waiting for an attack animation or lacking AP.
- Some enemies do move and attack: 11 accepted enemy skill calls are logged.
  Do not describe the entire faction as absent or completely inactive.

Actual position changes between consecutive round-start snapshots:

| Transition | Changed / common surviving enemies |
| --- | --- |
| 4 to 5 | 9 / 18 |
| 5 to 6 through 10 to 11, each | 1 / 18 |
| 11 to 12 through 13 to 14, each | 0 / 17 |
| 14 to 15 and 15 to 16, each | 8 / 17 |

Route logging has limits (76 limit records on this map); round-start positions
corroborate the persistent stalls rather than relying only on action-log absence.
No timeout, concurrent-dictionary or NullReferenceException pattern was found in
the second-map interval. Separate WMR mortar InvalidCastException warnings exist
in the session; no causal link to this stalled advance is established.

## Relationship to the previous alien bug

Issue #2 records the same symptom family on 0.7.17: awake aliens with advance
orders but zero movement scores; the bounded `RecoverStalledAdvance` fix showed
limited early movement benefit and was accepted by the user.

The current `AdvanceRecovery.cs` deliberately enables that fix only on exact
`v0.7.17+22362`. This session explicitly logs that recovery is unavailable on
0.7.18 and native AI is retained. The native pathfinder compatibility added in
2.6.1 did not extend recovery compatibility.

This is a confirmed missing recovery capability, not proof of the sole cause.
The old recovery also requires a known opponent; contacts expire on the second
map, with none recorded at round starts 14-16. Merely allowing 0.7.18 would not
guarantee pre-contact movement. Do not remove contact/combat/mission safeguards
or restore expensive hypothetical firing-position searches as a blanket remedy.

Next: review the 0.7.18 native zone/Move scoring against the reviewed 0.7.17
implementation before porting recovery; distinguish known-contact recovery from
pre-contact mission-order progress. The running game, settings and saves remain
untouched. Captured-log assertions verified 18 enemies and six never-moving teams;
no new gameplay replay or native compatibility validation was performed.

## Independent recovery candidate — AIPerformance 1.1.0 / LargerMaps 2.6.2

The user requested the fix in the AI DLL and Larger Maps standalone. Recovery hook,
state and runtime sources now live under `mods/jiangyu/AIPerformance/code/` and are
absent from LargerMaps.dll. Neither package requires the other. Standalone kits and
installer module selections no longer silently expand LargerMaps into AIPerformance;
the default Suite explicitly lists both independent packages. Settings and loader remain
required and can be reused without replacement.

Recovery independently hashes GameAssembly; only exact reviewed 0.7.17+22362 and
0.7.18+22828 are allowed. It checks actual map dimensions, not requested Larger Maps
settings. Known-contact, two-idle-round, combat, morale/AP, sleep, faction and pure
AdvanceTowards guards are retained. No contacts/orders, safety, paths or AP are changed.
Fast planning remains ON; optional routing remains OFF. Recovery settings now live in
AI Performance; existing Larger Maps values are not migrated or written.

Native 0.7.18 review verified the same missing contributions and hook phase:
- `HasKnownOpponent` at 0x7B9300; distance gate 0x7E6585, inside gate 0x7E6A91.
- Distance weights at 0x7E6603, native threshold 0x7E93C3 and inside utility 0x7E6BBD.
- Agent criterion PostProcess dispatch 0x7BC223 precedes behavior evaluation/choice.
- APCost remains uncomputed until native Move; path/clip calls 0x7DD3EC/0x7DD512.

This is **not** a pre-contact override or proof of the sole cause of the second-map
stall. Native pre-contact scoring is retained; expired contacts still reject recovery.
New recovery diagnostics are opt-in OFF, capped at 200 lines per mission.

Local checks: both DLL builds pass with zero warnings/errors; 12,257 managed recovery
policy/production-hook assertions, 27 planning/task checks, 79 proxy contracts and eight
native order-contract tests on each original 0.7.17 and 0.7.18 binary pass. Python:
146 LargerMaps tests (eight skipped without native fixture), 199 Suite tests pass.
Selected-package C# regression/artifact checks: 18,016 pass. These do not execute IL2CPP.
Canonical BuildKit and clean extracted-source verification passed for each single-module
kit (18,000 selected C# checks each). Native/binary contracts now total 81, including
proof that AIPerformance.dll has no LargerMaps assembly reference and owns recovery.

## Windows installation — 6 November, 16:46

Source is local commit `c08101d` on dev, **not pushed**, as requested. BuildKit inventories
pin the actual working-source snapshot; unrelated edits remain outside this commit.
Both archives contain exactly one module and passed local/remote SHA256 and full inventory:
- AIPerformance 1.1.0: 46 files, ZIP `1ab663753423275ada01af3adf9fdeb9b3f86e34bc9aa84d62c3487fe3d9fd4b`.
- LargerMaps 2.6.2: 34 files, ZIP `0ad2934a684ec66408d6a3807381d082d857d30e6a1c1ca5bf1c0d6d13101d04`.

Game closed and exact 0.7.18 native hash confirmed. Both canonical Windows BuildOnly
gates passed before either installation. Canonical standalone installs then passed,
preserving the other module's hashes during each operation and reusing settings/loader.
AI has zero warnings; LargerMaps retains the existing unused-field CS0169 warning.

Installed manifest / assembly / file versions: AI `1.1.0 / 1.1.0.0 / 1.1.0.0`,
LargerMaps `2.6.2 / 2.6.2.0 / 2.6.2.0`. Installed DLL SHA256:
- AI: `cd33ed4d11043f07bccdd05c358770768e71922f84e9c9db66be4cfe36000168`.
- LargerMaps: `a25467631ea49580c6772665724f1f97c5f3dece531e97f89ff291484467d613`.

Backup journals under the game's `_MENACE_Backups` contain only the affected module:
- AI: `20261106-164600-3a0738a41a1748b1b412063b0f39cbbd`.
- LargerMaps: `20261106-164610-f2105090e32c46229db4dd4e96540c69`.

1,419 protected unrelated mod/UserData files and 177 runtime/proxy files retain their
hashes; protected file count and native binary are unchanged. Neither installed manifest
depends on the other. No saves/settings or unrelated mods were changed.
Retained source/inventory/install evidence is at
`E:\MENACE-Work\Builds\AI-recovery-1.1.0-LM-2.6.2-20261106c`; private local Windows logs are
`.tmp/ai110-windows-buildonly.log` / `.tmp/ai110-windows-install.log`.

**Startup and post-install gameplay remain untested.** Ask the user to restart and replay
the affected second mission with fast planning ON and recovery ON under AI Performance,
then retrieve the fresh Latest.log. Observe actual enemy advance, not just recovered scores.
No Nexus/public release or remote source push was performed.

## Post-install gameplay — 17:02 to 17:12

User feedback: "seems I did not see many enemies, but a few yes". This is not acceptance
that stalled advance is fixed. Fresh immutable log capture, while game closed:
`.tmp/Latest-ai110-feedback-20261106d.log`, 8,015,969 bytes / 22,698 lines,
SHA256 `cb9c04e3041299ce5c4bdeef154ddd13834c7cf99be197b09c8a4a21f2962f12`.
Upload/download probe and snapshot hashes matched. No raw log is published.

Startup confirms game 0.7.18+22828, AIPerformance 1.1.0, LargerMaps 2.6.2 and 96x96.
Startup reports recovery enabled; fast planning adjusts 226 attack / three buff profiles. No
reported recovery disable/failure, timeout, concurrent-dictionary or null-reference
exception appears. Recovery-stage diagnostics are OFF: no `ADVANCE-CHECK` or
`ADVANCE-CHOICE`, so enabled hooks are not proof that score recovery applied.

One tactical map, `21763E9D360`, covers rounds 1–27:
- **18 Rogue Army teams/vehicles initially**, not a spawn failure. At round 27,
  16 remain alive and two are dead. These counts are teams/vehicles, not individual soldiers.
- Twelve teams change round-start position at least once. Accepted enemy skill calls:
  two rifle attacks, six mortar attacks, seven officer whistles and six stance/deployment
  actions (21 total). Calls are not proof of hits/kills or player visibility.
- All six heavy teams retain their initial round-start tile through round 27:
  three medium machinegun teams, two mortars and one autocannon. One mortar fires;
  staying on its tile does not make that team entirely inactive.
- Two surviving walkers move once, then remain stationary for 25 rounds. The third
  moves toward the order area and disappears from live snapshots after round 6.
- Several infantry also stop: a conscript team is stationary 23 rounds, another 17,
  and one infantry team 18 by the final snapshot.
- Example round 15 machinegun team at (59,88): awake, 100 AP at agent-start,
  morale 1, suppression 0, one known opponent, AdvanceTowards order 33.2 tiles away.
  It has 45 movement candidates but Move=0, Idle=1 and every other behavior score=0.
  A walker at (88,21) similarly has 66 candidates, Move=0 and Idle=1.

The stall is **not exclusively pre-contact**: known-opponent counts remain positive
through rounds 11–23 and at round 27. Recovery stages, candidate state at its hook,
and tile utility are not logged, so do not name an exact blocking guard from these
traces. Candidate 1.1.0 did not eliminate the observed stalls; #2 remains open.
134 route-limit records exist; the persistent positions are independently supported
by round-start snapshots rather than inferred solely from missing action entries.

The separate WMR mortar Structure-to-Actor InvalidCast warning occurs 35 times.
This is already tracked separately; no causal connection to these movement stalls is proven.
Private analysis `.tmp/analyze-ai-feedback-20261106d.py` and captured-log assertions
verify the counts, six never-moving teams, two stationary walkers and enabled recovery.
No game, settings, saves or mod files changed during this investigation.

Next opt-in: targeted, bounded hostile recovery-stage diagnostics to identify whether
history/actor/candidate/tile guards reject recovery or whether native Move still rejects
recovered scores. Do not weaken combat/contact safeguards or change settings silently.

## Diagnostic candidate installation — 6 November

AIPerformance 1.1.1 adds the opt-in `DiagnoseAdvanceRecovery` trace; recovery behavior
remains ON by default on supported maps above 64x64. BuildKit created a one-package,
46-file candidate (ZIP SHA256
`e2edd9c804ce82337db5306bf70185ff9aa0680fd12439f9b707bfaf3891c7be`). Local ZIP
inventory, clean extracted-source Release build against fresh 0.7.18 references, 81
native metadata contracts, 27 managed behavior checks, and 12,268 managed recovery
fixtures passed. None of the managed/metadata checks executes IL2CPP gameplay.

Canonical Windows BuildOnly and installation passed on MENACE 0.7.18+22828 with the
pinned .NET SDK 10.0.113. The Windows AIPerformance Release build had zero warnings and
errors; 81 native metadata contracts, 27 managed behavior checks and 12,268 managed
recovery fixtures passed. Installed manifest/assembly/file versions are
**1.1.1 / 1.1.1.0 / 1.1.1.0**; installed DLL SHA256 is
`06ABCFE83E092A3E63F756F9974B1D8F1502423A2580EFDE0466B3FB4032EE98`. MenaceModSettings
and Jiangyu.Loader were validated and reused, not replaced. The outside-Mods backup
`_MENACE_Backups/20261106-213826-006c98bc11ca473d92d7edae5eb59295` records only
AIPerformance and its legacy root-DLL conflict target, AIPerformance.dll. MENACE remained
closed after installation. No fresh post-install Latest.log or gameplay result exists yet.

Original test instructions requested **AI Performance / DiagnoseAdvanceRecovery** ON
and a restart; this toggle defaults OFF and is capped at 200 lines per mission.
Expected markers are `ADVANCE-CHECK` (recovery result/armed/applied state) and
`ADVANCE-CHOICE` (chosen behavior and Move/Idle scores). Asking the user to observe
a hidden hostile was inappropriate: use existing `DiagnoseAIRoutes` round-start
positions and decisions for movement evidence, independently of player visibility.
No release or source push was performed.

## User diagnostic run — 7 November

User completed a 20-turn run. The fresh Latest.log is retained privately at
`.tmp/Latest-AIPerformance-1.1.1-user-test-20261107.log` (5,720,037 bytes; SHA256
`905e220e098621ec8386af488806106ba07feff8063ff70a300bcd258ed60326`). It reports
MENACE 0.7.18+22828, AIPerformance 1.1.1, LargerMaps 2.6.2, and the conditional
advance-recovery-enabled startup message. AI task records span rounds 1–19 (519
activations, zero pending/dropped tasks); the log has no error records.

There are **zero `ADVANCE-CHECK` and zero `ADVANCE-CHOICE` records**, so this run does
not show whether recovery was applied. The saved `DiagnoseAdvanceRecovery` value was
read as true after the session, but the log cannot establish whether it was true when
the plugin initialized. The absence does not identify the cause: the setting is cached
at startup, and early input/map gates can also suppress these records.

The user could not see AI units. Initial analysis incorrectly treated that as making
the whole run inconclusive; existing route diagnostics establish movement independently
of visibility. All 15 route hooks were installed. The same capture contains 2,729 route
state records, 160 faction-round records and 638 behavior-choice records on one map:

- Round-start snapshots cover **rounds 1–20**, unlike task summaries (rounds 1–19).
- **18 hostile teams/vehicles initially; 17 alive and one dead from round 7 through 20.**
  This is not an absence of enemy spawning, and these counts are not individual soldiers.
- Twelve hostiles change round-start tile at least once. All **six heavy-weapon teams**
  (three machineguns, two mortars, one autocannon) retain their initial tile throughout
  rounds 1–20, each with 19 recorded Idle choices and 19 zero-scored Move evaluations.
- Two surviving walkers move once, then have 18 consecutive unchanged round-to-round
  positions. Several infantry also stop well short of their advance order area.
- At round 15, the machinegun at (59,88) and walker at (88,21) have AP100 at agent-start,
  morale1, suppression0, are awake and have one known faction opponent. Each has a single
  AdvanceTowards order outside its current position. Choice records show **Move=0,
  Idle=1**, with 24 and 22 movement candidates respectively. Round-start AP0 is a prior
  turn boundary observation, not evidence that they lacked AP when choosing behavior.
- Five accepted hostile skill calls are recorded (three officer whistles, two stance
  deployments), but no accepted hostile attack calls. There are 57 route-limit records;
  the stationary positions are independently supported by round-start snapshots.

**The movement stall persists in 1.1.1; #2 is not fixed.** Recovery application and its
precise rejection stage remain unknown. These records do not prove a rendering or
visibility-system bug. Next investigate the zero Move scores and silent recovery hooks
using these concrete stalled actors, not another request to visually track hidden units.
No game, mod, setting or save was changed in this follow-up. No raw log is published.

### User clarification: central position, one approaching walker

The user clarifies that they were in the map centre and one walker reached them;
the earlier "no AI visible" wording should not be read as literally zero encounters.
The matching walker progresses from (50,83) to (50,58) by round6, then is absent from
the living roster from round7. Recorded central player positions include (49,51) and
(50,51), consistent with that encounter; this does not independently identify its killer.

Of the 18 initial enemies, six foot units continue making intermittent progress late
in the run (four conscript squads, one infantry squad, one officer squad). Three other
foot units stop earlier, two surviving walkers move once then stop, six heavy teams
never change round-start tile, and the approaching walker dies. Only two enemies change
round-start position between rounds19 and20; six move at least once in rounds17–20.
Several late movers advance approximately every third round, not continuously.

All **2,014 recorded hostile STATE/EXECUTE sleep flags are False**; all 420 recorded
MOVE-EVAL delaying flags are False. This does not support IsSleeping or the observed
movement-delay flag as the explanation, although other intentional timing is not ruled out.

All ten civilians remain alive at every round-start snapshot. From rounds11–19 the only
known faction opponent at round-start is a worker, with contact TTL cycling 2,1,0;
round20 has another known worker. Round7 instead has a known Carda contact. Every one
of **4,200 recorded hostile attack evaluations** has score0 and candidates0; there are
no accepted hostile attack calls recorded. Contact knowledge is not proof of a legal
shot, range or line of sight for any particular shooter.

The nearest surviving advancing squad ends around 12 tiles from the recorded central
player position, with other advancing squads farther away. Terrain/vision limits are
a plausible reason they remained unseen, not established visibility results. A leading
movement hypothesis is the previously reviewed known-contact gate suppressing advance
incentives while attack positioning fails to take over; civilian contact and the
three-round cycle are consistent with this, not a newly proven cause. It cannot by
itself explain heavy teams already stationary before any known contact.

## Historical alien fix comparison — 7 November

The user correctly points out that this resembles the previously accepted alien
problem, not a new sleeping/visibility theory. Reviewed #2's original acceptance,
the consolidated accepted source at `f1a58c9`, extraction `bdeff9e`, planning change
`c80ed83`, recovery migration `c08101d`, and the current uncommitted 1.1.1 changes.

The accepted 20 October setup had **two separate parts**:

1. **SimplifyCombatAIPlanning OFF** (local routing and selective preservation OFF):
   restoring native future firing-position evaluation recovered combat. That capture
   recorded 37 accepted/APPLIED enemy attacks, although 29/31 aliens still stalled early.
2. **RecoverStalledAdvance ON, LargerMaps 2.3.2**: stopped requiring positive/affordable
   cached APCost at ConsiderZones.PostProcess, because native Move computes paths/costs
   afterward. Recovery added a limited early objective-movement benefit (13/31 versus
   2/31 tile changes in 5→6, then 5/31 versus 2/31 in 6→7). The user accepted that
   bounded result; do not retrospectively claim a universal fix.

### What changed

- The first 0.7.18 regression really did disable recovery: the previous code required
  exact 0.7.17. This was already addressed by `c08101d`; current startup reports recovery
  enabled, so the old unsupported-version warning is not the current explanation.
- Recovery runtime and history at migration are gameplay-equivalent to the accepted
  2.3.2 source after namespace/comment/whitespace normalization. The corrected geometric
  progress test remains; the premature APCost check has **not** returned. Current
  recovery still includes hostile type9 (Rogue Army), not only type7 aliens.
- **The accepted planning configuration was not preserved by extraction.** Settings
  moved from `Larger Maps` to `AI Performance`; the new setting defaults ON and ignores
  the old OFF value. This was deliberate source behavior, even explicitly exercised
  by OfflineChecks: an old Larger Maps OFF value still allows the new AI setting to
  disable InfluencesPositioning. It nevertheless differs from the accepted alien setup.
- The accepted 2.3.2 capture has no planning-simplification setup message and 35 recovery
  applications. The current 1.1.1 capture reports **226 attack / three buff profiles
  simplified**, with no recovery-stage records. `c80ed83` also removed an optional five
  infantry-profile preservation experiment, but that experiment was OFF in the accepted
  alien run and is not the fix to restore blindly.

### Verification and decision

Re-ran eight exact native-byte checks on **each** original 0.7.17 and 0.7.18 binary:
known-contact gates, score weights, inside-zone utility, hook phase and later Move/AP
ownership all pass. Current production fixtures pass **12,268 recovery checks** and
**27 planning checks**, including old-setting isolation and explicit new-setting OFF.
These are static/managed checks, not evidence of native hook execution or a gameplay fix.

Confirmed: a configuration mismatch from the accepted setup, not a lost 2.3.2 APCost
correction. The game patch exposed the old compatibility restriction, but the reviewed
native gates themselves still match the intended port. Whether current live recovery
is called/applies remains unresolved. Different factions and maps also prevent a direct
causal conclusion from the two captures.

The smallest discriminating next step is a **user-approved, backed-up change only to
AI Performance / SimplifyCombatAIPlanning = OFF**, restart, same affected mission, with
recovery ON and other settings fixed. Native firing-position work may restore slow AI
turns (#57), so do not change it silently or call this a verified solution. No source
gameplay, installed mod, setting or save was changed during this historical comparison.

## AIPerformance 1.2.0 gameplay — 7 November, 13:30–13:34

User feedback: **"20 turns passed. no enemies attacked civilians"**. This is not
acceptance. Fog of War prevents the user from tracking hidden enemies; use actual
logged positions, not visibility, to assess movement. The user rejected planning OFF
because it previously made a mission take three hours; keep fast planning ON.

Private capture `.tmp/Latest-AIPerformance-1.2.0-20turns-20261107.log`:
5,890,944 bytes / 16,735 lines, SHA256
`ef10c1ef619638c7cdafd8b2f16d5176117be5a998f0b613c0896df2cf95911d`.
Script upload/download and remote/local hashes matched; game was closed at capture.
Installed AIPerformance assembly 1.2.0.0 and DLL hash match the verified installation.
Startup identifies LargerMaps 2.6.2, 96x96, enabled recovery and 226 attack / three buff
profiles simplified. No settings, saves, installed mods or gameplay source changed.

### Result: persistent stalls, with live recovery evidence now available

- One map, rounds 1–20: 18 Rogue Army teams/vehicles initially; 17 alive and one dead
  at round 20. All six heavy teams retain their initial round-start tile throughout:
  two mortars, three machineguns and one autocannon. Two surviving walkers move once,
  then remain stationary for 18 rounds. Other infantry do move; this is not a total
  faction freeze or missing spawn.
- Five accepted hostile skill calls are logged: two stance deployments and three
  officer whistles. No hostile attack skill call is logged. There are 57 route-limit
  records; absence of action records alone cannot establish complete attack coverage.
  The user's report remains the direct evidence of no civilian attacks.
- Unlike the prior run, targeted diagnostics **are present**: 101 `ADVANCE-CHECK` and
  100 `ADVANCE-CHOICE` records across rounds 3–12. Check results: Contact 25,
  Applied 40, AlreadyApplied 36. The bounded trace stops before round 20; do not infer
  later recovery activity from its silence. No logged choice has `forcedMove=True`.
- Recovery does apply to stalled actors, yet choices still show Idle / Move=0 /
  Idle=1. Thus "recovery never runs" and "0.7.18 unsupported" are not explanations
  for this capture. Score application alone does not prove completed movement.
- Across 195 logged choices for the six heavy teams and two surviving walkers,
  193 show a null stored movement target. Example: round 3 mortar at (94,65), AP=100,
  awake, no suppression, 19 movement candidates, `hasUtility=False`, target null,
  Move=0 / Idle=1. Candidate tiles are not a selected, reachable movement destination.
- The 1.2.0 fallback in `AdvanceRecoveryRuntime.TryGetAdvanceMove` requires an existing
  target (`Move.GetTargetTile()`), affordable positive APCost and strict order progress.
  A null target necessarily rejects it. This is a concrete coverage gap in the fallback,
  not proof of which native gate prevents every unit from selecting a target.
- Fast-turn evidence remains positive: 519 complete actor-activation summaries, zero
  pending/dropped tasks, maximum observed activation 1,962.9 ms and maximum individual
  Evaluate call 80.1 ms. These are activation/call timings, not whole-faction turn times.
  No recovery-disable, null-reference or invalid-cast record appears.

Private parser and `.tmp/verify-ai120-20turns-20261107.py` assertions passed, including
capture/DLL integrity, positions, exact recovery-stage counts and skill attribution.
The parser preserves the event actor rather than replacing it with nested target fields.

**Next target:** review the native utility/target-selection step before PickBehavior.
The existing-target-only fallback cannot cover the demonstrated null-target case.
Any replacement must keep fast planning ON, retain native path/AP/safety ownership,
and avoid hypothetical firing-position search. No new native override is validated or
installed by this investigation. Issue #2 remains open; 1.2.0 did not fix these stalls.

## AIPerformance 1.2.1 — missing-destination candidate, 7 November

User approved reworking the fallback, keeping fast planning ON. Source committed locally
on dev as `7970fbd` (not pushed). Existing unrelated working-tree changes are preserved.

### Implementation

Reviewed native Move.HasUtility in both exact supported binaries: it compares raw tile
UtilityScore against the inherited Behavior.GetUtilityThreshold before native Move's
destination/path/AP work. Existing distance recovery alone need not satisfy that gate.
The new prefix supplies the configured native threshold only to strictly closer order
tiles whose raw SafetyScore is no worse than the current tile. It requires the existing
two-idle-round eligibility, one explicit hostile AdvanceTowards zone, full morale, no
suppression, no attack candidates/positive competing actions or positive tile utility.
Movement-done, moved, delayed and inert states are preserved. Unknown data fails closed.

It does not require an existing target or computed APCost. Native Move still performs
target selection, pathfinding/clipping, AP accounting and safety ranking. No second AI
pass, hypothetical shot search, fabricated contacts, attack candidates, path edits,
teleportation or setting change. Existing utility is not overwritten; failed partial
utility writes are rolled back. Optional ADVANCE-UTILITY reports seeded tile counts.

### Checks and deployment (not gameplay acceptance)

- 12,355 managed recovery checks, including null-target regression with/without contact,
  unsafe/current tiles, no history, attack/utility guards, native movement flags, repeated
  polls, transaction failure and actual prefix map gating. 27 planning checks pass.
- Nine exact-byte native contract tests pass on each reviewed 0.7.17/0.7.18 binary.
  91 proxy metadata contracts pass; none execute native IL2CPP methods.
- 199 GameplaySuite Python tests and 18,295 full offline C# assertions pass. Production
  AIPerformance DLL builds with zero warnings/errors. Offline fixtures emit pre-existing
  nullable warnings when rebuilt; this is separate from the zero-warning production build.
- Current PC reviewed native hash and all 119 proxy hashes match local references.
  Transfer round trip, ZIP SHA256, all 46 inventoried payloads and clean extracted-source
  build pass. Kit has exactly one package; settings companion and loader are reused.
- BuildKit `.tmp/MENACE-AIPerformance-1.2.1-nulltarget-20261107.zip`, SHA256
  `9e6bcb4bd2b7a0ad91b72a16235ffd2fdcfcf4e45d373fa3a556df2b90963f40`.
- Canonical Windows BuildOnly and standalone install pass on 0.7.18+22828 with private
  SDK 10.0.113; zero production build warnings/errors. All three bundled check suites
  (91 metadata / 27 planning / 12,355 recovery) pass again before staging.
- Installed manifest/assembly/file versions: 1.2.1 / 1.2.1.0 / 1.2.1.0. Installed DLL
  SHA256 `8CA4A7122C5EA7C5A795DEB804D1F66551A789FFD9B3929300A35B6313CE5A17`.
- Affected-only verified original backup/journal:
  `_MENACE_Backups/20261107-134840-cccd428fc84a40beb40c2f3ef4bce00a`.
  Targets exactly AIPerformance and the known root AIPerformance.dll conflict; installed
  files and retained originals match journal hashes. All 1,426 protected Mods/UserData
  files match the preflight inventory. Game stayed closed. No saves/settings changed.
- Installer itself reported success. A private post-verification wrapper could not
  uniquely discover the new backup folder; verification was rerun successfully against
  the exact installer-reported journal, without reinstalling or changing live files.
  This was a verification-helper failure, not an installer/build failure.

PC staging/evidence: `E:\MENACE-Work\Builds\AIPerformance-1.2.1-20261107-nulltarget`.
Stable installer log: `E:\MENACE-Work\Temp\MENACE-install.log`; BuildOnly/install logs
and verified installation metadata are retained in that staging directory.

**Next:** user plays the same affected mission normally for several enemy turns. Agent
retrieves fresh Latest.log and checks actual hidden-unit movement/utility and turn
timings; do not ask the user to observe units hidden by fog. No post-install startup or
gameplay has run yet; no claim that civilian attacks or all stalls are fixed. #2 stays open.

## 1.2.1 gameplay feedback and 1.2.2 re-evaluation correction — 7 November

User reports enemies still rarely appear and seem reluctant to attack. Captured a new
read-only snapshot with the game closed; verified source/download hashes and installed
AI DLL against the 1.2.1 deployment. Capture: 6,264,903 bytes, SHA256
`6752D04F0F69F6911F8BA2B3AA8D4F977536153D7612DB1C2A289137145BDCB8`;
private local `.tmp/Latest-AIPerformance-1.2.1-feedback-20261107b.log`, PC snapshot
`E:\MENACE-Work\Builds\AI121-feedback-20261107b`. No raw log is published.

### Observed result: some movement, still no accepted gameplay fix

- One map, rounds1–21, 18 hostile actors initially, 17 surviving plus one dead by round9.
  Infantry now change round-start tiles on 6–10 rounds. Of six heavy teams, three never
  change tile; the others change on one, two and five rounds. One surviving walker moves
  on five rounds, the other once then remains stationary for19 rounds.
- 80 bounded ADVANCE-UTILITY records cover all18 enemies (2,096 seeded tiles), alongside
  41 CHECK /79 CHOICE records. All79 recovery choices choose positive-score Move;
  forcedMove stays false because native selection itself now sees the recovered utility.
- All4,856 logged hostile attack evaluations have score0 and candidates0. The14 accepted
  hostile skills are nine deploy/five get-up stance actions; no hostile attack Use call
  is logged. This is not evidence they refused an existing valid shot or proof of why
  native attack candidates are absent. Route limits exist, so these are logged counts.
- In136 actor-rounds, a Move choice with a non-null destination is followed by Idle with
  a null destination in the same round. Two stationary machine-gun teams show this in
  round3 with AP100 at agent-start, flags false, scores108/109 and targets(59,87)/(26,86).
  Subsequent evaluation loses utility/target without any round-start displacement.
  AP0 at faction round-start is prior-turn state, not evidence they lack fresh AP.
- 547 complete activation summaries; max wall1,931.5ms, max Evaluate83.4ms, pending/dropped0.
  No logged recovery-disable, NullReferenceException, InvalidCastException or timeout.
  Fast planning/recovery active; all15 route probes installed. Captured-log assertions pass.

### Concrete code defect and scoped correction

AdvanceRecoveryState.Choice treated every non-idle selection as completed useful activity.
The first provisional native Move therefore marked the actor busy and cleared eligibility before
the next native evaluation could rebuild tile scores. The new actual-hook fixture fails
on1.2.1 at the second utility pass, before changing production code.

Local dev source `7e7d360` (not pushed), AIPerformance1.2.2: retain this round's existing
eligibility only for the same native Move instance with an affordable, strictly closer
target and all actor/order/no-shot guards. Actual position/order changes, moved/done/
delaying/inert states and useful non-movement actions still invalidate recovery. Merely
selecting Move is neither proof of movement nor another completed idle round. No extra
evaluation/path search, contact/attack fabrication, AP edits or changed settings.

The two-pass fixture now passes with/without known contacts, including five repeated
re-evaluations; actual movement cancels recovery. Twelve invalid-choice cases still cancel
it, and a provisional flag cannot resurrect useful activity or create another idle round.
12,386 recovery checks, 27 planning checks, 91 proxy metadata contracts, 199 Python tests
and18,295 full offline C# assertions pass. Production build has zero warnings/errors.
The first Python run found the migration inventory still said1.2.1; updated the row and
reran all199 successfully. Managed fixtures remain distinct from native gameplay proof.

### 1.2.2 Windows test installation

- Reviewed game binary and all119 current proxy hashes match local build references.
  Transfer round trip, ZIP and all46 payload hashes, clean-source build/metadata checks pass.
  One-package BuildKit SHA256:
  `7ddbfdf2a3a8feb828bc0bb8ef672752469f585265c77026723fa35b71c8fef1`.
- Canonical Windows BuildOnly and standalone install pass on0.7.18+22828 / SDK10.0.113,
  zero build warnings/errors. Bundled91/27/12,386 checks pass again before staging.
  Installed manifest/assembly/file:1.2.2/1.2.2.0/1.2.2.0. DLL SHA256:
  `54151AB9C28636E2D8AF9C91CAAD275792CB4011CE8F43CCF24D092327D8BD90`.
- Affected-only backup/journal:
  `_MENACE_Backups/20261107-143021-45a573e5462c4a3dbebc0a87c9906de9`.
  Targets exactly AIPerformance and the known root-DLL conflict. All retained-original and
  installed-file hashes verified; retained DLL matches preflight1.2.1 exactly. All1,427
  protected Mods/UserData files unchanged. Settings/loader reused, saves untouched.
- Canonical install reported success. A private post-verification helper expected one
  backup entry in the stable transcript but found11 historical installs because the
  canonical transcript deliberately appends. Corrected the helper to select the latest
  entry, then verified versions, originals/live hashes and protected files without
  reinstalling. No installer/build failure or live-file workaround.

PC staging: `E:\MENACE-Work\Builds\AIPerformance-1.2.2-20261107-recheck`;
stable installer log `E:\MENACE-Work\Temp\MENACE-install.log`. Game stayed closed.
**1.2.2 startup/gameplay remains untested.** This fixes a reproduced state-lifetime defect,
not proven enemy attack behavior. User plays normally; agent checks the next fresh log.
Keep fast planning ON and #2 open. Do not require another20-turn diagnostic marathon.

## 1.2.2: movement improves, civilian pressure and shooting still inadequate — 7 November

User reports enemies ignoring civilians and hardly shooting. Captured a read-only,
fixed-length live snapshot at15:26:20UTC without interrupting the running game or changing
Mods/settings/saves.16,248,142 bytes, SHA256
`54A6F0A2B009D286161FB9915C48E72888F53F7979C84E1E66085E66CE545DC5`;
installed AI DLL remains exactly the verified1.2.2 hash above. Private capture:
`.tmp/Latest-AIPerformance-1.2.2-feedback-20261107c.log`, PC
`E:\MENACE-Work\Builds\AI122-feedback-20261107c`. This is not an end-of-session log.

### What the capture establishes

- One map, rounds1–60. All18 hostile actors change round-start tile at least five times;
  all six heavy teams now move on16–19 rounds. Latest hostile round-start has9 alive/9 dead.
  This supports improved movement, not accepted combat behavior or a controlled speedup.
- 22 logged accepted hostile attack Use calls:16 enemy medium-mortar launches, four
  sustained-fire assault-rifle uses, one battle-rifle use and one anti-tank grenade.
  The remaining225 accepted skills are mostly deploy/get-up stance changes, not attacks.
- Two mortar uses explicitly target a civilian.worker in round14. Two civilian identities
  occur as known hostile-faction contacts in12 round-start entries across rounds12–26.
  Therefore civilian attack targeting is not entirely disabled. Contact TTL cycles2/1/0;
  faction memory and proximity do not prove a particular shooter currently has line of sight.
  Other attack target tiles lack an actor, so do not claim those were all player-targeted.
- 9,481 logged attack evaluations:9,426 zero-score/empty,51 positive with candidates and
  four zero-score with candidates. Positive evaluations appear only in rounds6/14/20/21/
  23/24/55. Route-budget caps mean these are observed counts, not complete attack totals.
- 1,384 completed activation summaries, max Evaluate77.3ms; pending/dropped0. Max wall
  22,156.2ms includes other activation work/waits; no cause or computation-time claim from
  wall alone. No recovery-disable, timeout or NullReferenceException is recorded.

### Supported next target, not another movement override

Fast planning remains active:226 attack/three buff profiles adjusted. Source deliberately
sets AIConfig.InfluencesPositioning=false for every eligible attack profile. Independently
rechecked native0.7.18 OnCollect at RVA7D4920: flag check7D4BF2 branches to false-return
7D6465 before opponent/line-of-sight/skill-applicability positioning collection. Exact
reviewed binary hash and relevant instruction bytes pass private assertions. This verifies
the loss of firing-position planning, not that it is the sole cause of sparse direct fire.

The leading suspect is now that blanket planning trade-off; the movement recovery does
not replace tactical firing-position selection. Do not ship another speculative movement
patch or claim all soldiers have valid shots from existing contacts. Before redesigning
combat planning, request approval for a controlled mission-start comparison with simplified
attack planning OFF. It may restore the severe slow turns from #57, so no setting change
without consent, no interruption of the current mission, and no promise of a quick replay.
Any eventual solution must preserve useful firing-position planning with bounded work,
rather than simply forcing Move or leaving all native searches unbounded.

Separately,122 WeaponMechanicsRebalanced outer-ring suppression warning entries accompany
244 InvalidCastException mentions (Structure→Actor). Source's ring loop calls Tile.GetActor
without HasActor; exact throwing callsite is not logged. Its catch is in a suppression
postfix after effects, not proof of blocked attack selection. Tracked separately in #96
(related to #59), not as the cause of #2;
do not silently change that mod during the AI investigation.

Captured-log integrity/count assertions and native-byte assertions pass. No production
source, settings or installed files were changed; no new build/deployment/game fix claimed.
#2 remains open; gameplay feedback is a failure of the combat goal despite better movement.
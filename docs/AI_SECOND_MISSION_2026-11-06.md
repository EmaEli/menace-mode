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
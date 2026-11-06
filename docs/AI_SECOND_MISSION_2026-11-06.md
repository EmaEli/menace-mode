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
Windows BuildOnly, installation and post-install gameplay still pending at this point.
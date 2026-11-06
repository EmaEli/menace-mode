# Larger Maps 2.6.2 — standalone maps and native pathfinding

The current host compiles the shared `mods/LargerMaps/src/LargerMaps` sources.
Use the canonical Suite installer in `mods/GameplaySuite`; do not revive the old
PR #18 one-click installer or install a second legacy Modpack Loader copy.

## AI companion (#93 / #57)

Larger Maps works without AIPerformance. Its manifest and standalone installer no longer
require or automatically include that DLL. Jiangyu and MenaceModSettings remain required.
Worker lifetime safety, simplified attack/buff planning, optional candidate limits and
conditional advance recovery live only in the independent **AIPerformance 1.1.0** package.
The Suite explicitly includes both; standalone updates replace only the selected module.
Optional general AI diagnostics remain in Larger Maps. Native map/pathfinding behavior
is unchanged from 2.6.1.

## MENACE 0.7.18 native support (#94)

The exact 0.7.18+22828 GameAssembly SHA256 `494c9e21…` now has a reviewed
28-site plan: the existing 26 edits with nine build-specific FindPath offsets, plus
both node-grid fill bounds in the map generator. All instructions are verified before
any write; write verification and rollback cover the complete selected plan. Earlier
builds retain their 26-site plans; unknown builds or conflicts still fall back to 64x64.
No game file is patched on disk. 64x64 does not install native edits. Conditional advance
recovery has moved to AIPerformance 1.1.0. The earlier 2.6.1 Windows BuildOnly/install passed
on 6 November; manifest/assembly/file 2.6.1/2.6.1.0/2.6.1.0 and protected-file hashes
were verified. Gameplay/pathfinding acceptance is pending. See
`docs/LARGERMAPS_0.7.18_2026-11-06.md` at the repository root for evidence.

Run the linked production-plan checks from the repository root with
`dotnet run --project mods/LargerMaps/tests/NativePatchChecks -c Release -- . game_files/v0.7.18/GameAssembly.dll`.
These read the original binary without executing native writes or proving AI speed.

## Floating forest prefabs (#92)

ForestDense/Sparse/Single prefab placement now converts world coordinates to the
actual heightmap domain, including the native inlined path that bypassed the old
helper patch. Other prefab types, terrain stamps and native-sized maps are unchanged.
102 managed production-hook assertions and local 0.7.18 compilation pass; visual
desert-map confirmation remains pending. The 2.6.0 Windows install passed on 6 November;
this is not visual acceptance. See `docs/FLOATING_TREES_2026-11-06.md`
at the repository root for the evidence and deployment gates.

## City-layout area anchoring (#81, on by default)

`AnchorCityLayoutAreas` (ON, restart required) applies to maps above 64x64 and to
missions whose primary setpieces form a fixed city grid. Their authored `Rect`
deployment, spawn and objective AI zones keep vanilla size and follow the map centre,
so they stay on the city. Setpieces and all other missions are unchanged. OFF
restores 2.3.2. Not yet gameplay-verified.

## Conditional advance recovery (#2, moved to AIPerformance)

In the optional AIPerformance package, `RecoverStalledAdvance` is **ON by default**, restart required; it can be switched
off for comparison. It applies only
to reviewed 0.7.17+22362 / 0.7.18+22828 and actual maps above 64x64, independently of simplified
combat planning and local tactical routing. No PC settings are changed by building.

- Requires two consecutive observed idle rounds at the same position/order, not
  repeated choice polls. Any useful choice, movement, changed order/agent, skipped
  round, new map or mission reset invalidates the evidence.
- Hostile faction types 5–9 are eligible, not only aliens. Allies, civilians and
  avoidance roles are excluded. Exactly one pure `AdvanceTowards` zone is required;
  mixed/defensive orders and arrival inside the zone retain native behavior.
- No positive tile utility, attack candidate or other positive action score is
  allowed at the recovery hook. Suppression, morale loss, spent AP, garrisons,
  deployment, sleep and already-moving/moved units also prevent recovery.
- Restores the reviewed native zone-weight contributions on existing candidates.
  Contacts, safety, AP, paths and behavior scores are not rewritten. Native Move
  and normal action competition can still reject movement. No extra AI pass or
  attack/path search, omniscient target, centre order or local patrol is added.
  The pre-path recovery requires geometric progress, not a positive cached APCost:
  that field is still uncomputed here. Native Move subsequently validates actual
  reachability and AP; restoring scores does not prove an affordable path exists.
- AIPerformance's `DiagnoseAdvanceRecovery` toggle defaults OFF and caps output at
  200 lines per mission. `ADVANCE-CHECK` reports rejection/applied scores and
  `ADVANCE-CHOICE` reports idle history, not proven movement/arrival/attack.
  Diagnostics do not relax gameplay eligibility guards.

### Recovery A/B acceptance (pending)

Use the same save/seed on 96x96, restart between OFF/ON runs and hold all other
settings fixed (current candidate: `SimplifyCombatAIPlanning=true`,
`UseLocalTacticalRouting=false`). Observe through the first contact and at least
three subsequent rounds. Compare actual tile/zone-distance changes, accepted
attacks and visible turn durations; the two idle rounds must precede any recovery.
Repeat on a different hostile faction, defensive/mixed-order missions, 64x64
(inert control), and after loading/restarting a mission. Reject oscillation,
abandoning defense, loss of useful attacks, errors or unacceptable extra turn time.
2.3.0 passed Windows build/install, but its first run showed no recovery applications
and 29/31 hostiles stationary in rounds 4–10. 2.3.1 passed Windows BuildOnly and
canonical installation; its game run again recorded zero recovery applications.
History works, but Progress and TileUtility block recovery. 2.3.2 fixes only the
premature APCost check. Its local/clean-source builds and canonical Windows
BuildOnly/install passed; the prior 2.3.1 is backed up outside Mods. Settings and
unrelated mods are unchanged. No 2.3.2 game run or movement improvement is claimed.

Local recovery checks: 12,237 managed assertions and eight version-locked native-byte
contracts pass, alongside 146 Python tests (four optional skips), 10,120 route-budget
checks and 17,794 focused offline C# assertions. Run recovery checks from the repo
root with `dotnet run --project mods/LargerMaps/tests/AdvanceRecoveryChecks -c Release`.
These do not execute native Harmony hooks or establish gameplay acceptance.

## Mission-long AI investigation (#2)

Keep the existing `DiagnoseAIRoutes` toggle ON before restarting. No gameplay
toggle needs changing. It now logs positions/zone distance each round and at turn
end, action scores, attack candidates, AP, movement completion and accepted skill
uses. Detailed repeated polls have separate budgets per actor/event/round; any
missing coverage is labelled `AI ROUTE LIMIT`, with counters at actor turn end.
There is no session-wide cutoff. OFF remains the default. Deep pathfinding logs
are not required. Distinguish `CHOICE`/`MOVE` (decisions), `ARRIVED` (movement
completion), `SKILL accepted=True` and `APPLIED` (not proof of a hit/kill).
The v3 build additionally reports `Move.OnEvaluate`, `Move.OnNewTurn` and
`Move.HasUtility`, the Move state even when `Idle` wins, and the installation
count (`AI ROUTE v3 hooks=15/15`). These are observations of native results, not
extra evaluations or path searches. Record any missing-hook warning verbatim.
Windows BuildOnly and canonical installation passed. The 19 October game run
reported v3 hooks=15/15 and 31 hostile actors choosing Idle in rounds 4–9, with
Move evaluation at 0. Later native review identified the faction-contact gate that
suppresses `AdvanceTowards`; candidate recovery is not yet gameplay-verified.
See `../../../docs/AI_DIAGNOSTICS_2026-10-19.md` for the run summary and next investigation.

## Global AI skill-positioning simplification (#93 / #57)

With `SimplifyCombatAIPlanning=ON` (the default), every eligible shared attack/buff
skill template whose native `InfluencesPositioning` flag is true is changed, regardless
of which faction or unit uses it. The old `PreserveBasicInfantryFirePositioning` option
is removed, and there is no preserved-infantry exception. Therefore all AI factions
using those skills stop searching hypothetical positions specifically to use that skill;
ordinary movement, objectives, cover and attacks from the current tile remain part of
native decision-making. Skills already marked false are left alone.

The setup enumerates the game's skill templates and also checks the known game skill
IDs, so an empty, interrupted or partial enumeration does not leave known skills out.
Changes are restored on scene exit/unload. This can alter AI positioning and is not
behavior-identical; it is not a proven gameplay speedup until the same mission is
measured in-game. Native pathfinding is unchanged.

## Required A/B acceptance (not performed)

1. Validate #1 smoke first. For this test hold all Smoke Overhaul / Infantry Assault
   settings identical; preferably keep proactive smoke OFF to isolate positioning.
2. Use the same save/seed, mission and mod set. Restart between OFF/ON runs.
   Compare `SimplifyCombatAIPlanning=ON` versus OFF, keep `UseLocalTacticalRouting=OFF`;
   all deep AI diagnostic flags OFF. Saved settings may override new-install defaults.
3. Test both 64x64 and larger maps; simplification is active at 64x64 too. Test
   offensive and sleeping/defensive missions separately. Do not compare different
   missions or assume sleep is a positioning bug.
4. Record ~3 AI turns per run: actual shots, useful repositioning, idle/sleep states,
   defended objectives, visible turn durations and any oscillation/suicidal advance.
5. Separately inspect the setup log for the modified attack/buff counts. An absent
   activation or zero changed skills is not evidence of a successful gameplay test.
6. Check return to strategy, next mission and unload. If behavior/CPU regresses, turn
   simplified planning OFF. In-game timing and Windows installation remain unverified.
# Larger Maps 2.3.2 — native Move cost-validation candidate

The current host compiles the shared `mods/LargerMaps/src/LargerMaps` sources.
Use the canonical Suite installer in `mods/GameplaySuite`; do not revive the old
PR #18 one-click installer or install a second legacy Modpack Loader copy.

## Conditional advance recovery (#2, on by default)

`RecoverStalledAdvance` is **ON by default**, restart required; it can be switched
off for comparison. It applies only
to reviewed 0.7.17+22362 and actual maps above 64x64, independently of simplified
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
- Detailed `ADVANCE-RECOVERY` entries require the separate `DiagnoseAIRoutes` toggle.
  They prove only score recovery, not selection, movement, arrival or an attack.
  2.3.1 also records bounded `ADVANCE-CHECK` rejection stages and `ADVANCE-CHOICE`
  idle-history observations. Diagnostics do not relax gameplay eligibility guards.

### Recovery A/B acceptance (pending)

Use the same save/seed on 96x96, restart between OFF/ON runs and hold all other
settings fixed (current investigation: `SimplifyCombatAIPlanning=false`,
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

## Selective infantry positioning (#2)

Ports PR #18 commit `3f50705dffe21fa729813001ac2fe5378d30b33f`, not merged.
`PreserveBasicInfantryFirePositioning` is **OFF by default**, restart required.
With `SimplifyCombatAIPlanning=ON` and map size **above 64x64**, it preserves only
already-enabled native positioning profiles resolved from these five skills:

- `active.fire_pirate_assault_rifle`
- `active.fire_pirate_pdw`
- `active.fire_pirate_outcast_pipe_gun`
- `active.fire_assault_rifle_tier1_556`
- `active.fire_battle_rifle_tier_1`

Profiles must be attacks requiring line of fire, with nonzero native pointers.
Selection precedes all mutations and deduplicates pointers, not managed wrappers.
Aliases sharing a preserved profile also retain it; five IDs are not necessarily
five distinct profiles. Disabled/missing/incompatible candidates are never forced
ON. Other attacks and recognized buffs keep the existing simplification behavior.
Scene exit/unload restores owned changes and clears preserved identities.

No new agent/turn hooks, wake-ups, orders or custom pathfinder. Preserving native
move-to-fire scoring **can increase native CPU work**; it is not a proven speedup,
cover-and-fire controller or implementation of #13/#17.

## Required A/B acceptance (not performed)

1. Validate #1 smoke first. For this test hold all Smoke Overhaul / Infantry Assault
   settings identical; preferably keep proactive smoke OFF to isolate positioning.
2. Use the same save/seed, mission, mod set and 2.2.1 baseline binary. Restart between OFF/ON
   runs. `SimplifyCombatAIPlanning=ON`, `UseLocalTacticalRouting=OFF`; all three deep
   AI diagnostic flags OFF. Saved settings may override new-install defaults.
3. Test 64x64 as an inert control, then 96x96; offensive and sleeping/defensive missions
   separately. Do not compare different missions or assume sleep is a positioning bug.
4. Record ~3 AI turns per run: actual shots, useful repositioning, idle/sleep states,
   defended objectives, visible turn durations and any oscillation/suicidal advance.
5. Separately inspect the setup log: retained profiles must be 0 with option OFF,
   and at most 5 when ON above 64. No activation line is expected at 64. Missing-ID
   fallback must not silently be called a successful initiative test.
6. Check return to strategy, next mission and unload. If behavior/CPU regresses, leave
   the option OFF. Native 0.7.16 use, timing and Windows installation remain unverified.
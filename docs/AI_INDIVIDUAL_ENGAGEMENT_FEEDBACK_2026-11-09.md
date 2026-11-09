# Last mission: movement without engagement — 9 November 2026

Primary: [#2 individual cover-and-fire](https://github.com/EmaEli/menace-mode/issues/2).
Related: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#13](https://github.com/EmaEli/menace-mode/issues/13).
User requests this investigation **before** the proposed battlefront prototype.
Enemies running to cover without applying pressure is rejected gameplay.

## Capture and mission isolation

- Closed-game Latest.log captured **2026-11-09 20:42:28 UTC**, source last modified
  20:40:30 UTC: **15,370,203 bytes / 42,698 lines**. SHA256:
  `bda43a3d6d9d689acce559d1baf6d5c21414e9f7843ce862e1fbb74b36460ff4`.
- Harmless SFTP round trip and capture-script round trip verified. Log, script and
  test-file hashes match on Mac/PC. Snapshot reads the game log; writes only new staging.
  Remote SSH PATH lacks hostname/PowerShell lookup; absolute Windows PowerShell works.
  No SSH configuration or environment was changed to work around this.
- Evidence: `.tmp/ai181-unit-feedback.7kAAtJ/`; PC retained snapshot:
  `E:/MENACE-Work/Builds/ai181-unit-feedback-7kAAtJ/`.
- AIPerformance manifest/assembly **1.8.1 / 1.8.1.0** and DLL SHA256 match the canonical
  installed artifact recorded in [the staging correction](AI_VEHICLE_STAGING_2026-11-09.md).
  Startup confirms 1.8.1, offensive planning and combined-arms diagnostics. Settings
  confirm support and simplified planning ON, local routing OFF; no policy-disable record.
- Log contains **two** missions. Latest map **2308D1F1360**, approximately local
  21:16–21:40, is identified at service-history completion as
  `mission.rogue_army_mine_roads`. Round-starts 1–13; round 13 is only partly executed.
  Do not combine with earlier map 22FBC845510 (rounds 1–17), which has different activity.
- Latest mission starts with 32 Rogue actors; round 13 has 28 alive/four dead. No causal
  attribution of casualties or screenshot-to-actor identification is made.

## What actually happened

### 1. Most units remain sleeping; none have movement orders

All 32 start sleeping. At round 13, **17 of 28** still have sleeping=True.
All **398 recorded hostile round-start snapshots have zones=0**. Sleeping is a native
AI mode, not literal immobility: these units still perform native roaming/cover moves.

The latest mission's 256 combined-arms diagnostic records reach their cap in round 9:
218 plan records reject Sleeping; 13 report no-request. Every recorded faction support
snapshot has zero eligible requesting infantry and zero advancing units. Later absence
of diagnostics does not prove later absence of evaluation.

### 2. Waking up alone does not restore useful fighting

Concrete executed examples, not merely provisional choices:

| Unit | Captured execution |
| --- | --- |
| MG team 2308B8AD6C0 | Round 7: awake, no suppression, near-full morale, executes Idle at **100 AP**. Round 10: moves (51,71) to (52,72), then Idle at **40 AP**. No accepted MG attack in this mission. |
| Walker 2308E556B40 | Awake from round-start 8; round 10 moves (30,74) to (30,75), then Idle at **82 AP**. No accepted weapon attack in this mission. |
| Autocannon team 230975DB000 | Round 7: awake and healthy, Idle at **100 AP**. Round 10: moves (23,31) to (21,29), leaving **0 AP**. No accepted autocannon attack. |
| Rifle squad 2308E69A6C0 | Round 10: awake, unsuppressed, moves (35,35) to (38,34), then Idle at **37 AP**. No accepted attack throughout this mission. |

There are **37 logged awake Idle executions at 100 AP**. These are events, not 37
distinct units. Actor-end resets AP to zero, so that later field must not be mistaken
for proof that the unit spent its AP fighting. Faction knownOpponents counts do not
prove personal line of sight, current detection or a legal shot for any of these units.

### 3. Some shots occur, but almost all assets contribute no recorded fire

- **42 accepted attack calls from five of the initial 32 actors**: 28 battle-rifle,
  four ordinary assault-rifle, six sustained-fire and four vehicle laser calls.
- Only **one of eight walkers** records attacks. No accepted MG, autocannon or mortar
  weapon attack appears. The mortar team's three attacks use its **battle rifle**.
- Calls can be bursts/reactions or repeated execution steps, not distinct activations,
  proven hits or effective support. Do not describe the encounter as literally shot-free.
- Logged choices: 929 Move, 951 Idle and nine attack/suppression choices. Re-evaluation
  repeats these events; these are not counts or percentages of completed unit turns.
- All 929 logged Move choices show **targetAttackUtility=0**, including many awake
  examples with total target utility zero. This is not proof each destination lacks LOS;
  it shows no recorded attack-derived value guiding the selected destination.

### 4. The missing piece is not demonstrated to be a shot-priority multiplier

Of 6,854 written attack-evaluation records, only 11 have positive native scores.
No captured Move/Idle choice lists a positive InflictDamage/InflictSuppression score.
Some positive attacks really execute. Thus this capture does **not** show a general
pattern of choosing cover over an already-scored valid shot.

There are 198 ATTACK-EVAL limit notices, so these counts are sampled, repeated and
incomplete; no SKILL limit or route-buffer/observation failure is recorded. No new
exception appears during the last mission. Exact zero-score reasons (LOS, detection,
range, setup, target value, resources) and hypothetical legal alternatives are not logged.

## Source-confirmed coverage gaps, not a proven cause of every move

Relevant runtime source is unchanged from published 1.8.1; local pending logging edits
were preserved and are not assumed installed.

1. `SimplifiedCombatPlanning.TryModifySkillPlanning` disables native future-position
   search for attack/buff profiles globally; this mission logs **226 attack / three
   buff profiles adjusted**. Immediate shots are still evaluated.
2. `OffensivePlanningRuntime.Eligible` rejects sleeping units and MACHINE actors.
   Its replacement firing-position probes exclude setup/deployment weapons, AoE and
   limited-use attacks. Generic progress additionally requires an AdvanceTowards order.
3. `CombinedArmsRuntime.Snapshot` requires an advance order for requesting infantry.
   Awake weapon teams/crewed vehicles can qualify without one, but have no infantry
   request here. `CombinedArmsResponseRuntime.Threat` and threat observation likewise
   require advancing-order context. This mission does not supply it.
4. Optional contact convergence excludes sleepers, vehicles and weapon teams, and
   preserves any existing native tile utility. It is not a replacement firing-position
   policy for those assets or for native roaming that already has positive utility.

**The broad performance tradeoff and narrow replacement do not cover the same units
and situations.** This is a concrete coverage problem; a controlled OFF run or linked
native-legality reproduction is still needed to attribute a particular missing shot.
Do not globally re-enable expensive positioning, wake every unit or lower all safety.

## Next work: repair individual engagement before adding a front

1. Reproduce an **awake, zero-zone rifle/team/vehicle** with detected enemies and no
   native shot. Add bounded, setup/AP-aware useful firing-position evaluation independent
   of support requests; preserve defensive/scripted orders and actual native legality.
2. Preserve an already-useful legal shot and recheck real move/setup/fire AP. Cover
   movement should have a fighting purpose, while genuine emergency withdrawal remains.
3. Review native mission wake/alert triggers separately. Permit only legitimate contact
   response, not global waking or hidden-player tracking; sleeping guards are deliberate
   until this contract is verified. Awake non-engagement must not be blamed on sleep.
4. Add bounded rejection diagnostics for zero-shot cases and repeated fresh-score tests;
   compare actual attacks/positions and turn cost, not just selected Move scores.

Validation: `python3 .tmp/ai181-unit-feedback.7kAAtJ/check-evidence.py` checks integrity,
mission separation, counts, diagnostic limits and individual execution/AP traces.
This is log-evidence validation, not an IL2CPP gameplay test or an implemented fix.
No gameplay source, live mod, settings or save changes; no build/deployment. #2/#13/#17
remain open. The [battlefront proposal](AI_BATTLEFRONT_PLAN_2026-11-09.md) is deferred
behind this individual-engagement work, not treated as the remedy for missing shots.

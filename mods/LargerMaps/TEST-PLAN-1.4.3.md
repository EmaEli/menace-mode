# Larger Maps 1.4.3 — full mission-area validation plan

**Status: source-only experimental build (compile and runtime testing needed).**
Game build: MENACE `v0.7.15+21883` x64. Always retain working 1.4.2 / 1.3.12 backups and a backup save.

## Map size selector

In `Mod Options > Larger Maps > TacticalMapSize` select one of **64, 72, 80, 88, 96** (the slider snaps to multiples of eight) and **fully restart MENACE**. Use 80 for the first trial, then 96, then 72 and 88; verify 64 separately. `Latest.log` should identify the matching backend or explicit safe fallback. Never change the native pathfinding mode during a running session.

## Required objective tests (do not rely on the mission-preview drawing alone)

1. **Repel Counterattack / Rogue Army Defend Outpost (DefendArea)**: compare target area to 1.4.2 in Mission Prep. It must cover a larger region **around the compound**, not relocate to a corner. In Tactical, send a hostile unit across the new outer boundary and verify ticket/round scoring changes at the matching position. Verify all linked `DefendAreaBonus` thresholds.
2. **CaptureArea / InvestigateArea**: both preview and actual capture/ticket updates must use the same enlarged zone; verify a unit at the edge is counted and one beyond it is not.
3. **EvacuateToArea / RecoverObject / RescueUnit**: loading/pickup/evac markers and actual use areas must agree; victims/object items must still spawn and path to their intended destinations.
4. **SabotageArea / Destroy/Defend Structure**: expanded gameplay zone must stay centered on target building/POI; no objective trigger should activate for an unrelated distant building.
5. **CustomBarks and mission-specific triggers**: triggers should activate only in the enlarged corresponding region; do not enlarge tiny scripted triggers to a blanket 5x5 if they are not scoring zones.
6. **StoryMission1 and CombatTutorial**: ensure any target/landing/scripted zones still permit mission progression and no scripts trigger too early. Some tutorial-only rectangles may bypass the shared AddToList pipeline.
7. **Map-border zones**: verify North/South/East/West and corner areas stay along the intended map edges and that percentage-derived lengths do not grow twice.
8. **Setpiece anchoring**: verify deployment zones, objective zones and enemy spawns remain attached to their associated structures after the resize.
9. **AI behavior**: units should advance, guard, react and receive destinations sensibly. Check 3+ enemy turns per size and a full mission for 80 and 96.

## Log validation

- First successful mission objective resize gives one `objective-zone scaling active for NxN` message per game launch.
- Verify no new `MissionAreaConfig`, `Harmony`, `IndexOutOfRangeException`, collection corruption, timeout or actor-spawn errors.
- Spawn-rect safety from 1.4.2 should still work (`civilian.worker`, `police_chief`, pirate groups).
- Send both `MelonLoader/Latest.log` and `Player.log` with screenshots showing objective markers and current map size if a mission's scoring fails.

## Known limitations

Offline unit tests mirror C# geometry and validate game binary signatures, **not** native runtime semantics, Unity/Harmony dispatch, combat AI or objective activation. Objective implementations that construct RectInt regions without calling `MissionAreaConfig.AddToList` are not covered automatically. The 1.4.3 scope hooks and precise scoring behavior remain unverified until running MENACE. A map larger than 64 can still have unrelated mission placement or performance issues.

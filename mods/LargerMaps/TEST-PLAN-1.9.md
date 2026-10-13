# Larger Maps 1.9 / MEN-002 — firing-position A/B test (draft)

**Status:** uncompiled, no Windows/in-game verification. This tests one narrow hypothesis, not a completed fix for sleeping enemies or global AI coordination.

## Why this experiment
On maps larger than 64×64 the current `SimplifyCombatAIPlanning=ON` disables speculative move-to-fire for many attacks. Five common infantry direct-fire AI profiles can now optionally keep that vanilla logic while other attack skills remain optimized. This may help pirate/Rogue infantry reposition to fire, but can increase AI CPU cost.

## Set up
1. With MENACE closed, keep a backup of existing Larger Maps and game saves. Build/deploy 1.9 from the branch using the **existing** Windows build workflow. Do not use this until compile and install checks pass.
2. Leave `UseLocalTacticalRouting=OFF`, `DiagnoseAIRoutes=OFF`, `DiagnoseAIPerformance=OFF` and `DiagnoseAIPathfinding=OFF` for the initial comparison. Keep all other mods, map size, mission, seed and squad deployment constant.
3. Select a **96×96** mission with known pirate/Rogue infantry and save or record the repeatable initial conditions.
4. In Mod Settings set `SimplifyCombatAIPlanning=ON` and **`PreserveBasicInfantryFirePositioning=OFF`**, then restart. Run 2–3 enemy turns, note elapsed AI turn time, visible enemy shots, reposition-to-shoot movement and units still in sleep mode. Stop if a turn takes more than ~3 minutes.
5. Set **`PreserveBasicInfantryFirePositioning=ON`** (keep simplified combat planning ON), restart, replay the same situation, capture the same observations.
6. Repeat only if necessary at 80×80 and then 64×64 as a no-change reference. The optional feature must have **no effect** on 64×64.
7. A second diagnostic run (with `DiagnoseAIPerformance=ON`) may be useful for unexplained differences, but do not leave diagnostics enabled for normal play.

## Acceptance / rollback
- Only five specifically listed small-arms profiles retain native position seeking; mortar/smoke/indirect skills remain simplified.
- Positional improvement must be observable: e.g. enemy shifts to acquire a firing line then attacks. Units in sleep mode are measured **separately**, since this setting does not awaken them.
- AI turns must remain comfortably playable, with no substantial or repeated slowdown, stalling, new errors, or pathfinding regressions.
- If slower or no visible tactical benefit: **turn PreserveBasicInfantryFirePositioning OFF and restart**. Do not merge or close MEN-002 based on static tests alone.
- Runtime logs should contain at most the existing single startup summary. Optional deep diagnostics OFF by default.

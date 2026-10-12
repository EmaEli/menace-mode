# 1.4.12 diagnostic test — 64x64 areas

## Goal

Compare **1.3.11 (fast)** → **1.4.10 (slow)** → **1.4.12 (unknown)** with exactly the same 64x64 mission and same external mods/settings. This isolates the new mission-area hooks/geometry while retaining the 1.4.10 terrain-hole patch and AI safety. It does **not** isolate other 1.4.x differences.

## Setup

1. Close MENACE and preserve a copy of the known-good 1.3.11 mod DLL + JSON and your save.
2. Extract this authoring ZIP outside the MENACE game/Mods folder.
3. Run `build-and-deploy.bat`. The script needs the Windows .NET 6 SDK and the installed game. Set `MENACE_GAME` if not installed in `M:\SteamLibrary\steamapps\common\Menace`.
4. In Mod Options, select **TacticalMapSize=64**, disable `DiagnoseAIPerformance`, `DiagnoseAIPathfinding`, `DiagnoseAIRoutes` and `DiagnoseMissionAreas` unless needed; restart MENACE after changing settings. Keep all other mods and options identical to the successful 1.3.11 test.
5. Verify in `MelonLoader/Latest.log` **all three** lines:
   - `native 64x64 pathfinding selected (no native byte patches)`
   - `DIAG 1.4.12 64x64 area hooks/geometry from 1.3.11`
   - AND `scoped proportional enemy spawn-area expansion (DIAG: original 1.3.11 area handlers)`
6. Launch mission `mission.rogue_army_relief_outpost`, seed `5320037`, and play to combat/rounds 5–7. Report whether the AI is fast (like 1.3.11) or slow (like 1.4.10). Provide `Latest.log` and `Player.log`.

## Interpretation

- **Fast:** The differences in the area-hook code/geometry are implicated. Does not yet identify which part; next bisect individual area changes, retaining all safety mechanisms.
- **Slow:** Revert this branch; investigate the other changes between 1.3.11 and 1.4.10 (e.g., terrain-hole handling), ideally by a further single-variable 64x64 comparison. No justification to touch AI timeouts or pathfinding.
- If the seed, settings, visible events, enemies, or movements differ significantly, do not treat speed alone as definitive: compare equivalent engagements.

## Limitations

The current environment has no Windows MENACE runtime or .NET SDK: this artifact contains source and Windows build/deploy, **not a compiled DLL**. Python offline tests check source contracts/geometry, not runtime Harmony behavior.

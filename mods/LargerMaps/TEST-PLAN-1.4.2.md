# Larger Maps 1.4.2 — experimental validation

**Do not publish this source build until compiled and run in MENACE.** Requires the same game build (`v0.7.15+21883`, Windows x64) with the matching verified `GameAssembly.dll` as 1.4.1. If there is a checksum mismatch, Larger Maps falls back to 64×64.

## Size selection

- In **Mod Options → Larger Maps**, set Tactical Map Size to **64 / 72 / 80 / 88 / 96**. Values between these sizes snap to the nearest multiple of 8 on the **next full game restart**. Default is 80.
- Do **not** switch a native backend while MENACE is running. Restart for each test. Log should say either `experimental 72x72/80x80/88x88/96x96 backend active`, or `native 64x64 pathfinding selected`.
- Test initial deployment, movement across coordinate 63, turn completion, map terrain, mission objectives, and reinforcements for **each selected size**.

## Spawn-area regressions (important)

1. **Stop Marauders — civilian base**: test the mission that previously failed six `civilian.worker` spawns because its area was `(42,41,0,1)`; use the same mission seed if possible. Also verify rescue-target placement near a building and the absence of new misplaced/immobile NPCs.
2. **Other pirate / rescue missions**: verify police chief `(-1,-1)` and pirate squads in `(32,42,10,0)` areas are placed. Check the civilian/rescue objective still works: spawning a survivor on a reachable tile matters more than suppressing warnings.
3. **Rogue Army Delay**: verify enemies at opposite map edges and late reinforcements; try all sizes. Confirm **support landing zones and player deployment do not move away from their setpieces**.
4. **Mine Roads**: verify NPCs, mission objectives, and terrain setpieces on all sizes. Pay attention to special targets near 1x1 buildings and narrow roads.
5. Use `Player.log` to check for `Failed to find spawn position`, `Failed to spawn units`, and `TryCreateInitialAiUnit`. Use `Latest.log` to check for `initial AI spawn area safety active`, `spawn area corrected`, `extended ... backend active`, or hook-install warnings.
6. If a **spawn does still fail despite 5x5 safety**, the local zone may genuinely contain no passable/free tiles. Capture `Latest.log`, `Player.log`, the mission template/seed and target area so we can examine collision, passability, and mission-specific constraints rather than enlarging every zone indiscriminately.

**Note:** This version fixes degenerate rectangles at creation time. It does not increase enemy strength or spawn additional units, does not alter A*/AI scoring, and does not guarantee a free cell exists in a blocked building or fully occupied area. AI route diagnostics remains optional and OFF by default.

# Extended pathfinding native patch — v0.7.15+21883

This implementation is experimental and uses the native methods from the supplied MENACE GameAssembly / IL2CPP dump. It is not a generic patch for other builds.

| Method / RVA | Change |
|---|---|
| `MissionTemplate.GetBounds` / `0x5F61C0` | **NEW 1.4.7**: two `mov ...,42` immediates (RVA `0x5F61C2`, `0x5F61CF`) changed to selected map width and height; removes 42x42 clipping in `MissionAreaConfig.AddToList` |
| `PathfindingProcess..ctor` / `0x69EED0` | PriorityQueue 0x800 -> 0x4000, closed buffer allocation 0x1000 -> 0x4000, 2D node-grid 64 -> 128 on both axes; active node constructor loops 64 -> 72/80/88/96 |
| `PathfindingProcess.Init` / `0x69E210` | Both limits 64 -> actual map side (72/80/88/96) |
| `PathfindingManager.Init` / `0x69BE30` | Both limits 64 -> actual map side (72/80/88/96) |
| `PathfindingProcess.FindPath` / `0x69C6B0` | Closed bitmap 4096 -> 16384 bytes and **11** `shl ...,6` -> `shl ...,7` |
| `PathfindingProcess.ProcessNode` / `0x69E840` | **1** `shl ...,6` -> `shl ...,7` |
| `PathfindingProcess.Clear` / `0x69C300` | Closed bitmap 4096 -> 16384 byte clear |

The process finalizer frees `m_ClosedNodes` with the same native allocator family; no allocator substitution is made. Temporary lists still dynamically grow from initial capacity 64.

### Safety

All 26 instruction signatures must match live GameAssembly memory AND on-disk DLL must match SHA-256 before any change. Runtime patch uses a single VirtualProtect-protected span (expanded to include the mission-bounds instructions), rechecks the results, flushes instruction cache, and restores original page protection. If any write fails, it restores/validates all original instructions; unrecoverable rollback/page protection failures explicitly halt the process to avoid running a partly modified pathfinder. These failure branches have not been exercised in the Windows game.

`GameAssembly.dll` on disk is never modified. A game update (including minor patch) should cause fallback to stock 64x64 until the patch plan has been reviewed for that build. Native patch is applied once at plugin startup; changing map size while MENACE is running is unsupported.

The internal grid is 128x128 but the loops are 72/80/88/96. Signed imm8 compare operands cannot encode 128 as a positive bound; the current design never tries to populate 128 rows. Game UI / API compatibility and map generation above 64 are not proven by static inspection.

The `tests/pathfinding-sites.json` manifest contains every original instruction and replacement-byte offset. `tests/test_extended_pathfinding.py` validates the exact bytes against original `GameAssembly.dll` when `MENACE_GAMEASSEMBLY` is set. No production DLL is bundled.

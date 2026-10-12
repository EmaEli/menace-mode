# 0.4.4 Mortar highlight: renderer-component conflict

The 0.4.1 update removed the native `DelayedAoE3`/`DelayedEnemyAoE3` TileHighlighter use because it interfered with off-map support marker lifetimes. 0.4.3 introduced a dual world-space border (dark outer line + colored inner line) to restore visibility, but used `root.AddComponent<LineRenderer>()` for both lines on one GameObject. Unity restricts a GameObject to a single Renderer/LineRenderer component; attempting the second component fails, preventing a complete registered highlight. The exception was swallowed every frame when Debug was OFF.

Fix: create one parent root with **two child GameObjects**, each owning exactly one LineRenderer. Destroy a partially created root if either addition/configuration fails; retain a scene-scoped, Debug-only single warning on failure. Destroy the root on tile deletion, actor selection changes, scene changes, and disable as before, recursively removing both children. The native TileHighlighter and delayed AoE channels remain untouched.

This is a code-level root-cause candidate, not yet runtime-verified. Test in Unity/MENACE and confirm real game logs. No mortar gameplay/balance changes.

## Reference mod review: Highlight Smuggler Stash V1.0.0

The user-provided `Highlight Smuggler Stash` archive (by LiveBait; different from Nexus mod 265's ZSM standalone DLL) contains `src/SmugglerStashMarker.cs`. It calls `TileHighlighter.Instance().Set(tile, TileHighlight.Objective)` after checking `IsSet`, records local ownership in `ownsHighlight`, calls `Unset` only for its own marker when the stash pickup is removed, and discards references at scene changes. This is a useful illustration of explicit ownership and cleanup.

Do not copy its `TileHighlight.Objective` channel for WMR mortar ranging. It is shared with the base game's objective markers and has no WMR per-tile red-to-green color control; reusing it would risk marker collisions or unwanted persistence. Our independent parent/child LineRenderer instances preserve marker isolation. The reference mod is informative but does not directly repair our renderer component conflict.

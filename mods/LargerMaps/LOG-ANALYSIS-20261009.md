# Larger Maps 1.4.5 — 96x96 preview regression, evidence from user logs

Input logs: `Latest(20261009-034738).log`, `Player(20261009-034751).log`.

- MelonLoader loads Larger Maps 1.4.5 and reports `experimental 96x96 backend active` (Latest lines 436, 538).
- Exact patched map generator reports `BLUEPRINT REPLACED 42x42 -> 96x96` and `tiles=9216` (Latest line 1193).
- The mission is `mission.pirates_repel_counterattack_cop_large_military_base_01`, seed 7235667 (Player lines 800-815).
- The army preview includes 4 flamethrower trucks, 5 scavenger trucks, 18 outcast heavy trucks, 2 chaingun trucks = 29 separate units (Player lines 825-829).
- The mission template (supplied `ExtractedData(1).zip`) defines four opposition spawn strips: NorthMapBorder(5), EastMapBorder(3), SouthMapBorder(2), WestMapBorder(4). The same cardinal layout appears in reinforcement spawn templates. Their native long edges are functions of actual map size, not static vanilla authored rectangles.
- **No `64-reference objective areas active` confirmation** is present in this startup. It cannot prove whether target objective geometry was transformed: role may have been unrecognized, native callback may not have passed through hooked AddToList, change may be a no-op/failed write, or another code path may own the final preview rectangle.
- Latest and Player cover startup and Mission Preparation, **not tactical gameplay**. No pathfinding timings or runtime target-zone scoring available.

Confirmed source issue: 1.4.5 calls `TryScaleReferenceBorderRect` on native-resolved border rectangles inside `MissionAreaAddToListPostfix`. This acts on geometry that was **already calculated for the current 96x96 map** and can double-process the offsets/widths. Version 1.4.6 stops these post-transformations while preserving final emergency spawn normalization. This is a justified correction, but its effect on this exact mission's southwest marker concentration is **not proven** until runtime testing.

Unresolved objective issue: centered primary anchored area scaling may not run, or its results may be based on already transformed coordinates. Version 1.4.6 does not claim this is fixed; it logs native input/output rectangles with a separate bounded, opt-in `DiagnoseMissionAreas` option, including the 64x64 reference case. One diagnostic run at 64 and one at 96 with the same seed is necessary.

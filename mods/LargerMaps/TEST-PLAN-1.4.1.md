# Larger Maps 1.4.1 — AI route investigation test

1. Keep map size 80, deploy 1.4.1. Leave `Diagnose AI Routes` OFF on the first launch. Confirm the game behaves like 1.4 and no `AI ROUTE` lines appear in Latest.log.
2. Enable `Diagnose AI Routes` in Larger Maps settings, **restart MENACE**, load a save before mission selection, then start one of the affected missions.
3. Watch 2–3 enemy turns. Capture `Latest.log` and `Player.log` without needing a full 15-round playthrough.
4. Search Latest.log for `AI ROUTE AGENT`: each sample shows current tile and current operative zones (up to four). Empty zones suggest there is no active operational destination, not necessarily broken pathfinding.
5. Search for `AI ROUTE MOVE`: samples show chosen target/ultimate tile and path length. Check a unit outside X/Z=63. If a unit with an active zone repeatedly has no movement target, investigate AI tile scoring / selection. If the target is valid but path list is empty, investigate pathfinding; do not automatically infer a failure from a zero path length alone.
6. To separate intentional defensive AI behavior from regression, optionally compare an identical mission seed and both map sizes after restarting MENACE (64 vs 80), noting relative deployment positions may differ.
7. Disable the diagnostic toggle and restart when finished.

No gameplay changes or extra hooks are installed with diagnostics OFF. Tests are static only until compiled/played on the user's Windows installation.

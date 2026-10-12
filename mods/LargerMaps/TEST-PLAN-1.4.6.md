# Larger Maps 1.4.6: conservative border handling + geometry investigation

1. Quit MENACE. Back up your saved game. Extract authoring ZIP. Execute `build-and-deploy.bat`; check for `SUCCESS: Larger Maps 1.4.6`.
2. Under Mod Options → Larger Maps enable `DiagnoseMissionAreas`. It is OFF by default and reads only during mission generation. Restart MENACE.
3. At **64x64**, generate Repel Counterattack from the same campaign and seed `7235667` where possible. Take full-map screenshot and save `Latest.log` as `64-Latest.log` immediately after opening Mission Prep.
4. Change to **96x96** and restart MENACE. Reopen the same mission/seed. Screenshot and save `Latest.log` as `96-Latest.log`.
5. Compare `AREA DIAG entered` and `AREA DIAG result` lines by `type`, `scope` and `role`. If the number of records differs because the 160-line cap is reached, reduce unnecessary mods or use a smaller diagnostic cap with purpose-specific filtering on the next build.
6. Confirm whether the 96 DEFEND and cyan deployment rectangles have scaled while staying aligned with the same compound; check that native border spawns are now spread along legal map edges rather than a single corner. The generated setpiece is physically ~20x22 tiles and does NOT grow with map size, so some visual density decrease is expected even if zone geometry is correct.
7. Turn `DiagnoseMissionAreas` OFF and restart after collecting logs. Do not use the experimental branch for an important ironman save until gameplay boundaries and spawns are verified.

**Note:** Two screenshots with identical mission seed may differ in secondary placement due to different RNG consumption at different map dimensions; compare each zone to its own generated setpiece, not to the same absolute X/Z coordinates.

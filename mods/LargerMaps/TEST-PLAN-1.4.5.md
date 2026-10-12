# Larger Maps 1.4.5 — geometry reference test plan

**Source-only experimental build; do not test with valuable ironman saves.** Keep a copy of 1.3.12 and 1.4.4. Build and deploy with the game closed.

## Mandatory baseline: native 64×64

1. Install **1.4.5**, set `TacticalMapSize` to **64**, quit and restart MENACE. Do not reuse screenshots from 1.4.4 as the baseline; 1.4.4 changed the reference area itself.
2. Open **Counterinsurgency → Repel Counterattack** with the same mission seed. Screenshot the preview (preferably the full map). Note the cyan **deployment** rectangle and DEFEND marker/scoring area separately.
3. The 1.4.5 geometry pass must be a no-op at 64, except for compatibility fixes inherited from 1.3.12: the native 64×64 result is the authority.
4. Start the mission. Check unit deployment legality and confirm that the DEFEND area has the same gameplay boundaries as its preview.

## Scaling comparisons

5. Choose **80×80**, restart, regenerate the **same mission/seed**. Measure width and height of both regions in tile coordinates: ratios of native resolved sizes should be approximately 1.25 (integer rounding ±1 tile). Compare the relative position to the same compound, not absolute screenshot pixels.
6. Repeat at **96×96**, expected multiplier 1.5 from the 64 baseline (rounding ±1 tile). Deployment and DEFEND may differ in size, because they have different rules.
7. Finally sanity-check **72×72** (1.125×) and **88×88** (1.375×), restarting between size changes. Check clamping at all four edges.

## Other mission families

8. Test CAPTURE, EVACUATE, RESCUE, SABOTAGE, RECOVER, INVESTIGATE and scripted trigger objectives. Verify native primary-chunk and standalone-building anchors are not relocated away from their buildings.
9. Test north/south/east/west/corner insertion zones and enemy reinforcements. Native border lengths must track the live map; their *thickness* must scale relative to 64. Check AI does not spawn outside the map.
10. Test a map with two separate primary compounds. Each zone must follow its own local anchor, without unioning the two buildings.
11. Use a test mission with civilians or single rescue targets; ensure invalid/zero-size spawn areas no longer create repeat spawn failures. Valid narrow spawn areas must remain valid and not artificially become 5×5.
12. Play several enemy turns. Confirm no AI timeouts, hangs, pathfinding errors, `Map.AddHole` exceptions, broken territory scoring, or persistent marker issues.

## Logs

Send `Latest.log` and `Player.log` if preview and tactical boundaries disagree. Normal build logs only the first successfully scaled objective per process; no per-turn/per-actor geometry diagnostics are added. A line `64-reference objective areas active` confirms the new transform ran on a map >64, *not* that it produces correct tactical semantics.

## Source verification

With Python 3 installed, and optionally the supplied game files, run:

```
set MENACE_GAMEASSEMBLY=M:\SteamLibrary\steamapps\common\Menace\GameAssembly.dll
set MENACE_EXTRACTED=M:\SteamLibrary\steamapps\common\Menace\UserData\ExtractedData.zip
python -m unittest discover -s tests -p "test_*.py" -v
```

These unit tests exercise the mathematical model and fingerprint of the supplied game binary but do not run or compile the C# hooks.

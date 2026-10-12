# Why DEFEND became 0x0 at 96x96

## Observed at runtime (Larger Maps 1.4.6)

The same mission type (`mission.pirates_repel_counterattack_cop_large_military_base_01`) was opened with `DiagnoseMissionAreas`. 64x64 `Latest(20261009-041050).log` contains:

- `AREA DIAG native-only type=0, role=ControlOrExtraction, preview=64x64 ... rect=(28,27,9,11)` (DEFEND).
- `AREA DIAG ... deploy:True ... before=(25,24,15,17), after=(25,24,15,17)`.
- `AREA DIAG ... type=5 ... before=(0,38,42,3), after=(0,58,64,5)` (north border).

96x96 `Latest(20261009-040755).log` showed:

- `AREA DIAG ... role=ControlOrExtraction ... (42,42,0,0)` for DEFEND.
- `AREA DIAG ... deployment ... (41,40,1,2) -> (37,36,2,3)`.
- Native border `(0,38,42,3)` still on a 96x96 preview.

## Native code path verified in supplied GameAssembly.dll

`MissionTemplate.GetBounds()` RVA `0x5F61C0` constructs a `RectInt` at origin with both dimensions equal to the literal `42` (`0x2A`):

- RVA `0x5F61C2`, instruction `C7 41 08 2A 00 00 00`: width 42.
- RVA `0x5F61CF`, instruction `C7 41 0C 2A 00 00 00`: height 42.

`Mission.GetBounds()` calls that template method. The `MissionAreaConfig.AddToList()` native implementation clamps generated areas to `Mission.GetBounds()`. An objective beginning at coordinate 42 is therefore clipped into a zero-size `RectInt`. Restoring it in a Postfix from width=0 is unsound.

## 1.4.7 intervention

Atomically amend the two native `imm32` values from 42 to the selected active N, as part of the existing pathfinder patch transaction. Only maps >64 are patched, and only with a full exact-byte/hash match. Keep 64 behavior identical to 1.4.6. Reconcile short border-strip thickness to the 64x64 reference after the native now correctly supplies N-wide border bands.

## Remaining risk

All sites match the supplied native assembly and offline unit tests pass, but the in-game effects of changing the global mission bounds are not verified. Some additional logic may depend on the vanilla 42 rectangle. In-game previews and tactical objective checks must both be tested, and the fallback must be observed on incompatible builds.

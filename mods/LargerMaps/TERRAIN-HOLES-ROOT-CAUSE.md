# Native `Map.AddHole` array-indexing bug (MENACE v0.7.15+21883)

## Reproduction and provenance

The user provided `Player(20261008-082004).log`, which records an `IndexOutOfRangeException` in `Menace.Tactical.Map.AddHole` called from `Menace.Tactical.TerrainHole.Start`. The mission continued and completed 17 rounds, so the error was nonfatal in that test.

## Root cause (verified through supplied GameAssembly.dll)

From `dump.cs`, `Map.AddHole(Vector3,float)` lives at RVA `0x67F640`; the first callee obtains the terrain, then its `TerrainData`. Disassembly of `GameAssembly.dll` at `0x18067F6BB`–`0x18067F8A4` shows:

- Compute a sample scale `(holesResolution - 1) / TerrainBounds.width`.
- Compute clamped start/end in X/Z using Unity-style `RoundToInt`.
- Call `TerrainData.GetHoles(startX,startZ,width,height)`.
- Loop local X from `0..width-1` and local Z from `0..height-1`.
- **Bug:** address `holes[X,Z]` even though the returned buffer has dimensions `[height,width]` and Unity requires `holes[Z,X]` (official Unity 6000.0 GetHoles / SetHoles documentation).

In the native body, the incorrect bounds checks are visible at `0x18067F849` (compares X against first dimension, height) and `0x18067F85A` (compares Z against second dimension, width). The write at `0x18067F86F` uses the similarly swapped flat offset. Non-square sample rectangles (e.g. near clipped map borders) can therefore throw.

## Narrow patch

`Map.AddHole` is prefixed only, with these guarantees:

1. Obtain the same terrain, TerrainData, sample coordinates, clipping and circular mask as vanilla.
2. `GetHoles` returns an `Il2CppObjectBase` pointer to IL2CPP `bool[,]`; generated UnityEngine.TerrainModule metadata confirms both GetHoles and SetHoles use that base wrapper.
3. Validate native x64 IL2CPP array header: bounds pointer at `+0x10`, element count at `+0x18`, data at `+0x20`; validate first and second dimensions match height and width respectively.
4. Copy the existing hole mask to a managed `byte[]`, set samples to zero only inside the original circle using `values[z*width+x]`, copy it back and call `TerrainData.SetHoles` with the original native object.
5. Suppress only the buggy original method if the replacement finished; otherwise fall back to vanilla and warn once. No blanket exception suppression, no modifications to unrelated map systems.

## Constraints

- Existing 64x64 geometry and 1.3.11 AI lifetime safety must remain unchanged.
- No runtime logging per hole with Debug OFF.
- The patch is **source-verified, not compiled nor game-tested** in this environment. Because it relies on the supplied game's IL2CPP x64 layout, validate the layout against future game updates.
- Visual test: confirm that hole objects still cut the terrain correctly and line up with scenery. Verify no `Map.AddHole` / `TerrainHole.Start` exception in Player.log. Keep this a development build until validated.

# MENACE Third Person Camera 0.3.4

**MEN-006 integrated distribution:** Third Person Camera 0.3.4 is now also built and installed by Gameplay Suite's `tools/Install-IntegratedSuite.ps1`. Its main DLL remains a MelonLoader `MelonMod` for camera lifecycle events, but it is included in the same downloadable installer/package as Gameplay Suite 0.32.3. The Mod Settings companion is copied into the Gameplay Suite modpack's `dlls` directory, so the old separate `Mods/ThirdPersonCamera` modpack is not required. Standalone camera authoring/build files remain available for developers. Integrated installation remains to be verified in game.

**0.3.4 compatibility API:** A read-only static `MenaceThirdPersonCamera.ThirdPersonCameraMod.IsCinematicViewActive` boolean now indicates whether the third-person camera is active. The state is common to F6 manual and automatic movement/fire modes, and is reset on camera exit. Other mods can consume this API using a cached managed delegate without referencing the camera DLL at build time. This fixes the inter-mod signal, without changing camera controls, behavior, gameplay or logging. See [MEN-006](https://github.com/EmaEli/menace-mode/issues/7). The new behavior still requires in-game verification.

A **camera-only** MelonLoader mod. Watch infantry and vehicles from a chase/orbit camera while MENACE still handles orders, movement, combat, AI and action points. The camera does not change combat outcomes or reveal currently hidden enemies.

## Controls

| Control | Action |
|---|---|
| **F6** | Enter/exit third person for the selected friendly infantry squad **or vehicle** |
| **F7** | Toggle automatic cinematics for this session (OFF by default) |
| **Hold middle mouse + move mouse** | Orbit freely around the observed unit and adjust camera elevation |
| **R** | Reset orbit to the unit's forward direction |

Bare F6/F7 do not intercept the **Ctrl+F6 / Ctrl+F7** combinations used by DevHotkeys. Optional `OrbitMouseButton=Right` is available but can conflict with tactical commands.

**The tactical HUD/UI automatically disappears in third person and reappears on exit.** MENACE's own `UITactical.SetUIVisibility` helper is used, including tactical indicators and unit HUDs. UI visibility is restored on F6 exit, automatic camera timeout, interrupted selection, turn/scene exit, and camera failures. Opening a different game screen exits third person rather than hiding a menu.

## Automatic camera

Automatic mode is **OFF by default**. Activate with **F7**, `AutomaticMode=true` in `UserData/MelonPreferences.cfg`, or **Mod Settings > Third Person Camera** when the accompanying `ThirdPersonCameraSettings` modpack is installed.

| Event | Default | Behavior |
|---|---:|---|
| `MovementChance` | 80% | Player infantry, player vehicles, and **currently visible enemy infantry/vehicles** use the **same** chance |
| `FiringChance` | 100% | Player infantry and player vehicles firing (the shooting unit, never a friendly victim) |
| `FollowVisibleEnemyMovement` | true | Enables/disables following visible enemy moves without adding another probability slider |

**Incoming fire no longer starts a camera shot.** Hits, misses, suppression, and attacks directed *at* friendly units do not trigger a victim-camera cut. This supersedes the old `UnderFireChance` preference from 0.2.x, which is ignored in 0.3 and no longer listed in the menu.

Enemy movement is eligible only if the enemy is **currently** discovered, not hidden, and its 3D model is visible to the player at the start of movement. A remembered/last-known enemy position is not enough. If the enemy becomes hidden during a cinematic, the mod immediately restores the tactical view. The camera does not reveal previously unknown locations and does not change the visibility system.

Each eligible movement/attack gets one independent probability roll, not one roll per frame or bullet. Camera random numbers do not advance MENACE's gameplay RNG. Short cooldowns reduce cuts between simultaneous events.

## Camera settings

The infantry camera retains the original preferences: `Distance=3.2`, `Height=1.75`, `ShoulderOffset=0.55`, `FieldOfView=66` and `OrbitSensitivity=0.35`.

Vehicles have their own defaults: `VehicleDistance=8.5` and `VehicleHeight=3.8`, with zero shoulder offset for a centered follow camera. Both can be adjusted in MelonPreferences or the in-game menu. This is a first approximation for tanks, transporters and tall walkers; the camera does not yet automatically measure each model's bounds.

Other preferences: `ToggleKey=F6`, `AutoToggleKey=F7`, `OrbitMouseButton=Middle`, `Debug=false`. All settings belong to the MelonPreferences category `MenaceThirdPersonCamera`. The companion modpack implements the same `IModpackPlugin` interface as Larger Maps (including the required scene callback) and registers these settings with `ModSettings.Register`. When installed, its settings are re-read at most twice per second and take precedence over corresponding MelonPreferences values. F7 is a temporary per-session override.


**0.3.3 settings fix:** The ModpackLoader plugin imports `Menace.SDK` so it can call `ModSettings.Register`. The camera reads `Menace.SDK.ModSettings.Get` by reflection at runtime (with a legacy namespace fallback); the standalone DLL still works when ModpackLoader is absent. In-game changes are polled at most twice per second. The settings plugin must be built and deployed successfully; it is not enough for the camera DLL alone to compile.

## Build and deploy (Windows)

1. Extract the entire `ThirdPersonCamera-0.3.3-authoring` folder into a **new directory**.
2. Close MENACE and run `build-and-deploy.bat`.
3. The script compiles using the game-generated IL2CPP assemblies under `MelonLoader/Il2CppAssemblies` when available, installs `MenaceThirdPersonCamera.dll` into `MENACE/Mods/`, and (if `Menace.ModpackLoader.dll` is present) also compiles and installs `Mods/ThirdPersonCamera/dlls/ThirdPersonCameraSettings.dll` and the `modpack.json` manifest.
4. Launch MENACE, open **Mod Settings**, select **Third Person Camera**. You should see sliders for Movement chance (0-100%) and Firing chance (0-100%). Check `MelonLoader/Latest.log` for `[ModSettings] Registered 7 settings for 'Third Person Camera'`.

Requires MelonLoader IL2CPP, the .NET SDK 6 or later (supporting `net6.0`) and the game's generated interop assemblies. **GitHub source checkout:** Game and MelonLoader DLLs are intentionally excluded from this public repository. Build with a local MENACE installation that has generated `MelonLoader/Il2CppAssemblies` and the MelonLoader runtime; the build script uses those local references. The original authoring ZIP also included fallback **compile-time** IL2CPP reference DLLs, which are not redistributed here. Do not copy any `refs` directory into the game.

Logs: `artifacts/build-and-deploy.log` for compiler issues; `MelonLoader/Latest.log` for runtime failures. The **first** event-callback error is logged in full once, but debug/per-frame logging stays OFF.

## Known limitations

- This is observational; it does not permit direct WASD driving/infantry control.
- The camera can clip through walls, vehicles, trees and terrain (no collision avoidance yet).
- Very tall walkers or very large vehicles may need custom distance/height preferences.
- Enemy movement cameras depend on the game's `TacticalManager.InvokeOnMovement` event being available; if that hook is unavailable, the fallback detects movement only for the selected **friendly** unit.
- Mod Settings requires the installed Menace Modpack Loader and the companion bridge modpack, both deployed by `build-and-deploy.bat`. Without them, use `UserData/MelonPreferences.cfg` instead.
- When changing `AutomaticMode` or the percentages from the in-game menu, those values take precedence over legacy MelonPreferences values. F7 remains a temporary session override.
- A bad native event callback is disabled individually rather than shutting off all automatic cameras. If camera update itself fails, the mod exits and restores the UI.
- **The companion interface was checked against Larger Maps, but a Windows .NET compilation and runtime test remain necessary.** The bundled source/interop APIs and packaging are statically checked; please test with MENACE.

## Manual preference fallback (if Mod Settings is unavailable)

In `MENACE/UserData/MelonPreferences.cfg`, under `[MenaceThirdPersonCamera]`:

```toml
AutomaticMode = true
MovementChance = 80
FiringChance = 100
FollowVisibleEnemyMovement = true
```

Stop the game before editing and restart after saving.

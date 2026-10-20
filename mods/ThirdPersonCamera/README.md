# Cinematic Action Camera 1.2.2 — Windows test install; gameplay retest pending

## Per-unit controls and short-move filter (#60)

Open **Menace Mod Settings (End) > Cinematic Action Camera**. Friendly infantry squad leaders,
conventional vehicles and walkers keep their distance/height and movement/firing controls.
Visible enemy movement has separate percentages for infantry (including jetpack units),
vehicles and walkers; enemies reuse the existing framing for their category. Firing
cinematics still follow friendly shooters only. The legacy ModpackLoader companion is frozen;
use the Jiangyu companion for the new controls.

**Minimum move distance** defaults to **0 (disabled)** to preserve existing behavior.
Set it to **3** to skip one- and two-tile moves. This uses MENACE's start-to-destination
tile distance, not detour path length or total tiles moved during a turn. It affects only
automatic movement cinematics, never firing or manual F6. If the native movement hook is
unavailable, the polling fallback cannot know the endpoints: automatic movement shots are
suppressed while this filter is enabled rather than bypassing it.

Automatic follow observes MENACE's concrete Jetpack effect before its jump coroutine starts. It uses
the skill's destination tile and the same movement chance and minimum-distance setting;
other skills do not trigger a movement shot.

Existing movement/firing percentages seed all three types on upgrade. New enemy movement
percentages inherit the matching unit type's movement chance; explicit new values, including
0%, are preserved. Walkers initially inherit the saved vehicle framing; saved infantry
Distance/Height are exposed unchanged. Without the companion, new
per-type preferences use **-1** to inherit the historical shared values. Walker detection
uses vehicle template IDs containing `walker`, consistent with Squaddie Veterancy; custom
vehicle templates without that marker use conventional vehicle controls.

The 1.2.2 candidate was built and installed on MENACE 0.7.17+22362 on 19 Oct 2026. A
post-install launch log contains the camera and no camera startup/hook/UI errors or warnings.
User feedback was positive, but the specific allied Jetpack automatic-follow result has not
yet been explicitly recorded; keep #60 open until confirmed. Per-setting persistence and
the remaining visual cases are not all verified.

## Obstructed firing cinematics (#54)

Automatic firing cinematics are skipped when a shooter is garrisoned in a
building or bunker. The decision uses MENACE's container type, so watchtowers
remain eligible. Movement cinematics and manual camera use are unchanged. This
source candidate has not yet been visually verified in game.

## Automatic firing direction (#49)

The firing view now frames from the attacking squad toward the attacked tile, not
the leader's facing/back. Accepted repeated attacks refresh the target; manual and
movement cameras retain their existing behavior. 1.0.0 received positive user
feedback during the long 16 October mission, with no camera errors in its log.
**1.1.0 is locally built, not deployed or game-accepted.** Standalone/install/settings
acceptance remains separate; see `docs/MISSION_VALIDATION_2026-10-16.md`.

## Jiangyu-compatible distribution (#33 / #15)

The camera remains the **single** `MenaceThirdPersonCamera.dll` MelonLoader entry point.
Its menu controls have a Jiangyu companion in `mods/jiangyu/CinematicActionCamera`,
using MenaceModSettings 0.6.0 (End key). The settings companion does not start another camera or
patch combat. Keep MelonLoader and Jiangyu; do not install the legacy settings modpack too.

The historical assembly/type names, `MenaceThirdPersonCamera` MelonPreferences category,
advanced offsets/FOV/keybindings and the confirmed MEN-006 HUD compatibility accessor are
preserved. Preference priority: existing modern JSON, recognized legacy group values,
then the camera's existing MelonPreferences. Migration never rewrites old preference files.
Only the known group-to-scalar JSON schema is imported; other legacy formats remain intact
and need explicit inspection, not guessed conversion.

The 1.2.2 camera and companion DLLs compiled and were installed on Windows against the
MENACE 0.7.17+22362 proxies; a post-install launch was checked. Keep #15/#33 open for F6/F7,
infantry/vehicles, visibility, orbit, HUD restore and settings persistence checks. Packaging
uses the shared installer under `mods/GameplaySuite/tools/Install-IntegratedSuite.ps1`.

The following sections describe the historical 0.3.3 behavior and controls; the new shared
installer supersedes the old manual deployment steps.

A **camera-only** MelonLoader mod. Watch infantry and vehicles from a chase/orbit camera while MENACE still handles orders, movement, combat, AI and action points. The camera does not change combat outcomes or reveal currently hidden enemies.

## Controls

| Control | Action |
|---|---|
| **F6** | Enter/exit third person for the selected friendly infantry squad (including jetpack infantry) **or vehicle** |
| **F7** | Toggle automatic cinematics for this session (OFF by default) |
| **Hold middle mouse + move mouse** | Orbit freely around the observed unit and adjust camera elevation |
| **R** | Reset orbit to the unit's forward direction |

Bare F6/F7 do not intercept the **Ctrl+F6 / Ctrl+F7** combinations used by DevHotkeys. Optional `OrbitMouseButton=Right` is available but can conflict with tactical commands.

**The tactical HUD/UI automatically disappears in third person and reappears on exit.** MENACE's own `UITactical.SetUIVisibility` helper is used, including tactical indicators and unit HUDs. UI visibility is restored on F6 exit, automatic camera timeout, interrupted selection, turn/scene exit, and camera failures. Opening a different game screen exits third person rather than hiding a menu.

## Automatic camera

Automatic mode is **OFF by default**. Activate with **F7**, `AutomaticMode=true` in `UserData/MelonPreferences.cfg`, or **Mod Settings > Third Person Camera** when the accompanying `ThirdPersonCameraSettings` modpack is installed.

| Event | Default | Behavior |
|---|---:|---|
| `MovementChance` | 80% | Friendly infantry movement; historical key retained |
| `FiringChance` | 100% | Friendly infantry firing; historical key retained |
| `VehicleMovementChance` / `VehicleFiringChance` | Inherited | Conventional vehicles; independent movement/firing percentages |
| `WalkerMovementChance` / `WalkerFiringChance` | Inherited | Walkers; independent movement/firing percentages |
| `EnemyInfantryMovementChance` | Inherited | Visible enemy infantry movement, including jetpack units |
| `EnemyVehicleMovementChance` | Inherited | Visible enemy vehicle movement |
| `EnemyWalkerMovementChance` | Inherited | Visible enemy walker movement |
| `MinimumMovementTiles` | 0 | Minimum ordered move distance; 3 skips 1-2 tiles |
| `FollowVisibleEnemyMovement` | true | Enables/disables visible enemy movement follow, in addition to the category percentages |

**Incoming fire no longer starts a camera shot.** Hits, misses, suppression, and attacks directed *at* friendly units do not trigger a victim-camera cut. This supersedes the old `UnderFireChance` preference from 0.2.x, which is ignored in 0.3 and no longer listed in the menu.

Enemy movement is eligible only if the enemy is **currently** discovered, not hidden, and its 3D model is visible to the player at the start of movement. A remembered/last-known enemy position is not enough. If the enemy becomes hidden during a cinematic, the mod immediately restores the tactical view. The camera does not reveal previously unknown locations and does not change the visibility system.

Each eligible movement/attack gets one independent probability roll, not one roll per frame or bullet. Camera random numbers do not advance MENACE's gameplay RNG. Short cooldowns reduce cuts between simultaneous events.

## Camera settings

The infantry camera retains the original preferences: `Distance=3.2`, `Height=1.75`, `ShoulderOffset=0.55`, `FieldOfView=66` and `OrbitSensitivity=0.35`. Distance and height are now exposed in the Jiangyu menu too.

Vehicles have their own defaults: `VehicleDistance=8.5` and `VehicleHeight=3.8`, with zero shoulder offset for a centered follow camera. Walkers use separate `WalkerDistance` and `WalkerHeight` settings, initially inherited from vehicles. All can be adjusted in MelonPreferences or the Jiangyu menu. The camera does not automatically measure model bounds.

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

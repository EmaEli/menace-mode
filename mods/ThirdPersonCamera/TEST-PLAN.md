# Cinematic Action Camera 1.0 — release acceptance checklist

- [ ] Run the single-click release installer with MENACE closed, verify two DLLs have AssemblyVersion `1.0.0.0` and the modpack manifest reads 1.0.
- [ ] Confirm startup log shows `Cinematic Action Camera v1.0` and Mod Settings shows `Cinematic Action Camera` with 7 options.
- [ ] With Gameplay Suite 0.32.3+ and Coordinated Fire/Overwatch active, verify F7 movement/firing and manual F6 hide their buttons and restore them afterward. The compatibility fix was tested with the prior camera version; regressions in this rebrand remain to be checked.
- [ ] Test F6 infantry and vehicle follow/orbit, F7 auto default OFF, HUD restoration, interruptions, and missing optional mods.
- [ ] Verify installer backup/rollback and no duplicate settings modpack when the Suite already embeds the camera companion.
- [ ] Confirm no repeated runtime logs with Debug OFF.

Historical test plans are retained below for reference.

---

# Third Person Camera 0.3.4 — MEN-006 compatibility smoke test (unverified)

Build Camera 0.3.4 and the matching Mod Settings companion. Install alongside Gameplay Suite 0.32.3 (Overwatch 0.32.3.3 and Coordinated Fire 1.6.9).

- With F7 automatic cinematics enabled, move an infantry squad, then a vehicle. Verify the native HUD and both custom buttons disappear during third person and return once overhead view resumes.
- Repeat with automatic firing and with manual F6. F6 and automatic share the public `IsCinematicViewActive` state.
- Interrupt via scene/menu change, change selected unit, disable automatic mode and exit F6. No button should remain stuck hidden.
- Test camera without the two combat mods and combat mods without camera; the camera state API creates **no mandatory loading dependency**.
- Verify no new repeated logs with Debug OFF. Do not mark MEN-006 resolved until actual game tests pass.

This patch changes only the read-only camera state exposure; movement, firing selection, camera controls, UI restoration and gameplay remain unchanged.

---

# Third Person Camera 0.3.3 - smoke-test plan

Use a tactical mission with player infantry, at least one player vehicle and some enemy infantry/vehicles.

1. **Build/install:** Run `build-and-deploy.bat`; confirm zero compiler errors, deployment to `Mods/MenaceThirdPersonCamera.dll` and MelonLoader startup showing v0.3.3.
2. **Manual infantry:** Select a friendly infantry squad, press F6. Confirm the view follows an individual soldier, the *entire tactical HUD disappears*, middle-mouse orbit works, R resets, and F6 restores the original camera **and complete HUD**.
3. **Manual vehicle:** Select a friendly tank/carrier/walker and press F6. Confirm camera placement behind the vehicle (not inside it), orbit and F6 restoration. If too near/far, adjust `VehicleDistance`/`VehicleHeight`.
4. **Automatic toggle:** Press F7. Confirm master mode ON, then OFF when pressed again. Auto mode defaults OFF after a restart unless preferences say otherwise.
5. **Friendly movement:** Set `MovementChance=100`, `FiringChance=0`. Order a movement from overhead for infantry and vehicle. Confirm third-person camera, hidden HUD, auto return after completion, and restored HUD.
6. **Friendly firing:** Set `MovementChance=0`, `FiringChance=100`. Fire from infantry and vehicles. Confirm the shooting unit gets the camera and no HUD remains on screen until the shot ends.
7. **Under fire (regression):** Set both chances to 0, enable auto mode and end the turn. Enemy attacks that target, hit, miss or suppress friendly units **must never** trigger a victim-camera cut. The legacy `UnderFireChance` setting should have no effect.
8. **Visible enemy movement:** Set `MovementChance=100`, `FiringChance=0`, `FollowVisibleEnemyMovement=true`. End the turn with one or more **currently visible** enemy units. As they start moving, the camera may follow them using the **same movement chance**. HUD hides and reappears afterward.
9. **Fog-of-war protection:** Repeat while enemies move outside vision or only last-known markers are present. Those enemies must **not** be followed. If a tracked enemy becomes hidden, the camera should exit immediately and restore UI.
10. **Probability:** At `MovementChance=0`, neither friendly nor enemy movements trigger; at `100`, eligible movement events should be accepted unless another cinematic/cooldown blocks a switch. Try 50% across several separate moves.
11. **Interruption:** During manual third person select a different friendly unit, end the turn, or open an in-game menu. The camera and HUD must recover. During an automatic sequence use F6 to leave; verify the 2-second re-entry mute.
12. **Mod Settings and shortcuts:** Check the two percentage sliders under Mod Settings > Third Person Camera (companion installed). Without the companion modify `UserData/MelonPreferences.cfg`. Confirm Ctrl+F6/Ctrl+F7 still work with DevHotkeys without toggling the camera.
13. **Logging/performance:** With `Debug=false`, there should be no continuous `Third Person Camera` log output. After an exception, only one full exception for the first event hook should be logged.

If a step fails, record the step number, the action, a screenshot for camera geometry/UI issues, and `MelonLoader/Latest.log`. The camera should never leave you with an invisible tactical HUD.

## Mod Settings integration regression tests (0.3.3)

1. Run `build-and-deploy.bat` with Menace.ModpackLoader installed. Confirm two DLL builds succeed, including the Mod Settings companion. Previously the companion failed with CS0118/CS0535; neither error should recur.
2. Confirm `MENACE/Mods/ThirdPersonCamera/modpack.json` and `MENACE/Mods/ThirdPersonCamera/dlls/ThirdPersonCameraSettings.dll`.
3. Launch game and verify `MelonLoader/Latest.log` says `Registered 7 settings for 'Third Person Camera'`.
4. In Mod Settings > Third Person Camera, set AutomaticMode=true, MovementChance=0, FiringChance=0. Verify no event-driven cut, while manual F6 still works.
5. Change MovementChance=100, move a friendly unit: camera follows. Change FiringChance=100: camera follows a friendly shooter.
6. Set FollowVisibleEnemyMovement=false and verify enemy movement does not trigger camera.
7. Close/reopen MENACE: mod settings persist. Confirm no duplicate registration warning.
8. With ModpackLoader absent, camera still works in manual mode and probabilities are loaded from MelonPreferences.

## v0.3.3 settings smoke test

1. Build/deploy from a clean extracted directory. Both projects must say zero errors: MenaceThirdPersonCamera.dll and ThirdPersonCameraSettings.dll.
2. In `MelonLoader/Latest.log` check for `[ModSettings] Registered 7 settings for 'Third Person Camera'` and `Initialized plugin` for the Third Person Camera modpack.
3. Open Mod Settings -> Third Person Camera; confirm Movement chance and Firing chance sliders exist.
4. Enable automatic mode; set both chances to 0%. F7 should not be needed: there should be no automatic cinematic cuts during movement/fire.
5. Set Movement chance to 100% and Firing chance to 0%. New movements (including visible enemies) should trigger but friendly firing should not. Then invert percentages and verify behavior.
6. Confirm F6 manual mode, middle-mouse orbit, hidden HUD while following, and HUD restoration on exit are unchanged.
7. If menu values have no effect, temporarily enable Debug in MelonPreferences and inspect `Latest.log` for settings reader attachment.

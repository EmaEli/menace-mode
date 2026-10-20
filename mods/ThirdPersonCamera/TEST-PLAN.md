# 1.2.2 candidate acceptance (#60)

1.2.1 failed the allied Jetpack test despite automatic mode ON, 100% movement chance and
minimum tiles 0. The 1.2.2 candidate observes JetPackHandler.OnApply, not IsMovementSkill.
When Debug is enabled, check `Hook active: OnApply` and `Jetpack effect actor=... distance=...`.
Windows test install passed 19 Oct 2026 (MENACE 0.7.17+22362); backup is
`_MENACE_Backups/20261019-170837-d007eddc0bac4a7082be2917cf49e260`. Existing settings
were preserved. A post-install launch log has been reviewed with no camera-specific startup,
hook, UI or depth-of-field errors/warnings. The user gave positive feedback; explicit
confirmation that allied Jetpack automatic follow worked is still pending.

- Upgrade with customized shared chances and vehicle framing: all types initially keep
  the old percentages; walkers keep old vehicle framing; infantry keeps Distance/Height.
- Set distinct distance/height values for infantry, vehicles and walkers; test F6,
  movement and firing for each. Changes should apply live without affecting other types.
- For each type, test movement/firing at 0% and 100% independently. Confirm currently
  visible enemy movement uses its type's chance, with no enemy firing/victim cuts.
- Set minimum tiles to 3: moves of 1 and 2 tiles must not start or extend a movement
  shot, and moves of 3+ tiles remain eligible. Test diagonals, repeated short orders and
  different unit types. Check 0 disables the filter; firing/manual F6 remain unaffected.
- With automatic mode on and friendly MovementChance=100, verify both ordinary movement
  and the actual Jetpack movement skill follow an allied JETPACK-tagged unit. At 0% neither
  should trigger; non-movement skills must not trigger. Repeat with a 3-tile minimum and
  check that the Jetpack skill destination obeys the same threshold without duplicate cuts.
  Repeat with a multi-element squad; rejected/invalid jumps must not trigger a cut.
- Restart: all separate settings, especially explicit 0%, must persist. Recheck attack
  target framing, building/tower distinction, orbit and HUD restoration after exit.
- With depth of field enabled in Graphics settings, test manual F6 and automatic camera
  cuts: depth of field should be off during the shot and the original quality restored on
  exit. Confirm the saved graphics setting is unchanged after restarting the game.
- Offline checks/compilation are not proof of this visual acceptance.

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

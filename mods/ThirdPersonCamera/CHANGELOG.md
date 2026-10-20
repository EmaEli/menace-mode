# 1.2.2 - Observe the native Jetpack effect (#60)

- Replaced the generic skill-use filter with an observer prefix on `JetPackHandler.OnApply`, before its jump coroutine starts. MENACE 0.7.17's JetPackHandler does not implement IMovementSkill, so 1.2.1 incorrectly rejected the actual jump.
- Use the effect's original source/destination tiles and native destination-validity check; preserve movement chance, minimum distance, visibility and cooldown rules. The concrete effect identifies Jetpack use without requiring an actor tag.
- Temporarily disable depth of field while third-person camera is active, then restore the previous quality on exit or failure. Preserve a newer graphics-setting change made while the camera is active.
- 1.2.1 loaded and manual F6 worked, but the user confirmed automatic Jetpack follow still failed with automatic mode enabled, 100% movement chance and minimum tiles 0.
- Canonical standalone BuildOnly and installation succeeded on MENACE 0.7.17+22362 (19 Oct 2026). Installed DLLs/manifests verify as 1.2.2; existing user settings are unchanged; no legacy camera duplicates remain. Backup: `_MENACE_Backups/20261019-170837-d007eddc0bac4a7082be2917cf49e260` (9 files).
- A post-install launch log was reviewed: the camera loaded with no camera-specific startup/hook/UI/depth-of-field errors or warnings. User feedback was positive, but the allied Jetpack auto-follow result was not explicit; keep #60 open until confirmed.

# 1.2.1 - Follow Jetpack movement skills (patch, #60)

- Added an automatic-camera hook for MENACE movement skills used by JETPACK-tagged units, covering Jetpack movement that does not emit the ordinary movement event.
- Reuses the existing movement chance, visibility/turn eligibility, cooldown and minimum-tile filter; non-movement skills do not trigger it.
- Installed on Windows; user gameplay retest failed. Superseded by the 1.2.2 candidate.

# 1.2.0 - Per-unit camera controls (local candidate, #60)

- Expose infantry squad leader distance/height in the Jiangyu settings menu.
- Separate movement/firing percentages and framing for infantry, vehicles and walkers.
- Add separate visible-enemy movement percentages by category and include JETPACK-tagged units as follow targets.
- Add a minimum ordered move distance in tiles (0 disables; 3 skips 1-2 tile moves).
- Preserve existing saved percentages/framing when splitting settings; new enemy chances inherit each category's movement chance and explicit 0% wins.
- Preserve vehicle follow behavior; firing-target framing, visibility and enclosed-building rules are unchanged.
- Not deployed or visually verified in game; release acceptance remains pending.

# 1.1.1 - Skip obstructed firing cinematics (patch)

- Automatic firing cinematics are skipped when the shooter is inside a building or bunker, using MENACE's native container type.
- Towers remain eligible; movement cinematics, manual camera use, and gameplay behavior are unchanged.
- User reports the current build works in game and turns progress normally; individual camera
  trigger cases were not logged separately.

# 0.3.3 - Mod Settings namespace and live-value reading (patch)

- Fixed `CS0103` in the settings companion: imported `Menace.SDK`, which exports `ModSettings`, following the working Larger Maps implementation.
- Fixed the standalone camera's reflection bridge to resolve `Menace.SDK.ModSettings` first. Previously it searched only `Menace.ModpackLoader.ModSettings`, so in-game slider changes could be silently ignored even if the companion compiled.
- Added pre-build guards for both namespaces and updated static regression tests to cover the actual UI-to-camera settings path.
- Camera geometry, automatic movement/firing events, enemy visibility rules, F6/F7 controls, UI hiding and gameplay are unchanged. Minimal logging; debug OFF.
- Build and in-game initialization must still be verified against the installed game assemblies.

# 0.3.2 - Mod Settings build compatibility (patch)

- Fixed `CS0118`: the companion plugin now explicitly uses `HarmonyLib.Harmony` in `IModpackPlugin.OnInitialize`, exactly as in the working Larger Maps plugin.
- Fixed `CS0535`: added required `IModpackPlugin.OnSceneLoaded(int, string)` implementation (no-op because the companion only registers settings).
- Added source and regression checks for both interface signatures before building the companion DLL.
- No changes to camera gameplay, controls, automatic camera probabilities, UI visibility, or logging (debug remains OFF).

# 0.3.1 - Mod Settings menu fix

- Replaced unreliable silent reflection-based menu registration with a ModpackLoader v2 companion plugin.
- The companion registers the existing `AutomaticMode`, `MovementChance`, `FiringChance`, `FollowVisibleEnemyMovement`, vehicle offsets, and orbit sensitivity via the same `ModSettings.Register` API used by Larger Maps.
- Camera DLL reads menu values dynamically but remains usable as a standalone MelonLoader mod.
- Build/deploy script installs a `ThirdPersonCamera` modpack beside the camera DLL when Menace.ModpackLoader is installed.
- No changes to camera triggers, probabilities, UI visibility, controls, or gameplay. Debug OFF.

# MENACE Third Person Camera changelog

## 0.3 - Vehicle views, visible enemies and clean cinematics

**New features** (minor version, not a patch):

- Manual **F6** and automatic movement/firing cameras now support player **vehicles** as well as infantry. Separate configurable vehicle distance/height, centered behind the hull or walker.
- Optional automatic third-person camera for **currently visible enemy infantry/vehicles** when they start moving. Reuses the **same `MovementChance`** percentage as friendly movement; `FollowVisibleEnemyMovement` defaults ON.
- Enforces current enemy visibility (discovered AND not hidden AND 3D visible). Exits the cinematic immediately when a tracked enemy disappears into fog of war.
- **Automatically hides MENACE's tactical UI/HUD** on third-person entry using native `UITactical.SetUIVisibility(false)` and restores it on manual/automatic exit, screen change, interrupted action, scene unload or camera errors.
- Camera update and entry require the tactical UI to be the frontmost screen so modals/menus are not unexpectedly hidden.

**Behavior correction:**

- Removed all incoming-fire auto-camera triggers and associated damage/miss/suppression hooks. Friendly units no longer steal the camera merely because somebody fires at them. Old `UnderFireChance` preferences are ignored and removed from the in-game menu.
- Automatic friendly firing follows the **shooter only**; player vehicle weapons use `FiringChance` as infantry weapons do.
- No gameplay/AI/visibility manipulation. Debug/per-frame logging remains OFF by default; event exceptions still isolated.

## 0.2.2 — Automatic camera resilience (bug-fix release)

- Fixed a native IL2CPP callback exception disabling **all** automatic cameras for the remainder of a session. Only the failing event hook is now isolated; other event types remain operational, and a failing movement hook falls back to lightweight movement polling.
- Logs the first complete callback exception only once per session to identify the actual failing native API, with at most one short warning for each other failed hook. Debug remains OFF by default; no per-frame logging.
- Guarded under-fire entity conversion: buildings and other non-actor damage targets are ignored before calling `ToActor()`.
- F7 can deliberately re-arm a global camera-update failure, whereas previously every subsequent F7 press misleadingly reported OFF.
- Bare F6/F7 no longer react to **Ctrl+F6/Ctrl+F7**, which are used by DevHotkeys.
- Made the optional Mod Settings reflection adapter tolerant of API overloads with optional parameters. The menu remains optional; `MelonPreferences.cfg` always works.
- No gameplay, pathfinding, damage, AI or camera geometry changes.

## 0.2.1 — Harmony compile fix

- Fixed `CS0118` build error where `Harmony` was resolved as a namespace rather than a type.
- Explicitly use `HarmonyLib.Harmony`, matching the reference mod pattern.
- Added a build-time source guard against the same regression. No behavior changes or additional logging.

## 0.2 — Automatic cinematics and free orbit

**New features** (minor version, not a patch):

- Optional automatic third-person camera for player infantry on **movement, firing, and incoming fire** (including being targeted, missed, damaged, or suppressed).
- Three independently configurable event probabilities (`MovementChance`, `FiringChance`, `UnderFireChance`) from 0 to 100 percent.
- Optional **in-game Mod Settings** registration when the Menace.ModpackLoader interface is available; MelonPreferences fallback always exists.
- **F7** toggles automatic camera for the session; starts **OFF**. **F6** manual camera remains available.
- Automatic shots persist through an action, then restore tactical view; player attacks may include overwatch/reactive fire; incoming fire can be shown during the AI turn.
- Free **middle-mouse drag** orbit around the followed soldier, with adjustable sensitivity and elevation. **R** resets orbit behind the soldier. Optional right-button orbit is configurable.
- Postfix-only Harmony event hooks, without changing tactical commands or game outcomes. Independent `System.Random` for cinematic chance rolls.
- Cooldowns/debouncing avoid rerolling on every projectile, rapid camera cuts, and repeatedly triggering immediately after manual exit.
- Minimal logging (debug OFF). Manual mode stays independent if cinematic hooks fail.

## 0.1.3 — Build reference fix

- Fixed build failures caused by omitted `Il2Cppmscorlib` reference; made references consistent with game-generated proxies.

## 0.1.2 — IL2CPP namespaces

- Switched imports from `Menace.*` to `Il2CppMenace.*` for generated assemblies.

## 0.1.1 — Reference search

- Adjusted build scripts to prefer known interop references; target x64.

## 0.1 — Initial manual camera

- F6 manual over-the-shoulder follow camera for a selected player infantry squad.

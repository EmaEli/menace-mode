# Menace Overwatch - 0.32.3 authoring package

## 0.32.3 MENACE API compatibility fix

- Fresh current-game dump confirms `Entity.IsPlayerControlled(bool)` was removed and replaced by `Entity.IsPlayer()` / `Entity.IsPlayerOrPlayerAI()`.
- Player ownership now resolves the new API first, with the old method retained only as a backwards-compatible fallback.
- Fresh dump also shows `Actor.HasLineOfSightTo(Entity, bool, Tile, Tile)` changed to `HasLineOfSightTo(Entity, Tile, Tile)`; Overwatch now resolves the current overload first with a legacy fallback.
- No gameplay/balance changes.

- Patch-only maintenance release: no gameplay behavior changes from alpha.32.
- Added **Debug logging** under Mod Settings -> Overwatch -> Diagnostics, default **OFF**.
- Routine gameplay logging is silent unless Debug logging is enabled: arming, AI reserve decisions, target rejection reasons, movement/attack triggers, reaction queue/firing, ammo reconciliation, HUD marker refreshes and selection restoration.
- Only rare warnings/errors that indicate a broken hook or failed reaction remain visible with Debug logging off.
- Versioning normalized to the project convention: the previous alpha.32 line maps to 0.32; this compatibility-only maintenance release is 0.32.3.
- Assembly, file, informational, manifest and package versions are aligned to `0.32.3`.
- Replaced direct `Entity.IsPlayerControlled(false)` calls with a runtime-compatible resolver supporting both the bool-argument and parameterless interop signatures.
- Prevents the MENACE 0.7.15 `MissingMethodException` from repeating in both the update loop and HUD path.
- Added one-shot fault suppression for runtime-update and HUD exceptions so a future compatibility failure cannot generate per-frame/per-OnGUI log floods.

## alpha.32 attack-trigger whitelist / transport safety

- Reaction provocation is now **whitelisted** instead of accepting every active non-movement skill.
- Overwatch can be provoked by only two event families: enemy movement completion and completed native **attack** skills (`Skill.IsAttack()`).
- Utility and state-transition skills no longer provoke Overwatch. This includes reloads, stance/setup actions, embark/disembark/eject-passenger actions and similar non-attack skills.
- `special.vehicle_eject_passengers_free` therefore cannot enter the queued reaction path while MENACE is mutating the passenger/vehicle state.
- The existing setting key `TriggerOnEnemyActions` is retained for user-setting compatibility, but its UI label is now **Trigger on enemy attacks** and it controls only the attack-skill trigger path. Movement triggers remain always active.
- A unit carrying Overwatch still loses its own carried watch when it commits any active skill or movement. The whitelist changes what **provokes someone else's** Overwatch, not what counts as committing the watching unit.
- Reaction-generated attacks remain excluded from recursively provoking Overwatch.
- alpha.31's `INDIRECT_FIRE` exclusion remains unchanged.

### alpha.32 test focus

1. Reproduce the Pirate/transport case that previously logged `special.vehicle_eject_passengers_free`; confirm no `Overwatch attack trigger completed` entry is produced for the eject action and the game does not hang.
2. Move an enemy through a valid Overwatch lane; confirm final-tile movement still provokes a reaction.
3. Fire a normal rifle/vehicle direct-fire attack in range; confirm the completed native attack still provokes Overwatch.
4. Use a non-attack action such as reload, stance/setup or passenger eject; confirm it does **not** provoke Overwatch.
5. Confirm an Overwatch reaction shot itself still does not recursively provoke another Overwatch reaction.
6. Confirm carried Overwatch still expires if the watching unit itself commits movement or any active skill.

## alpha.30 carried-watch renewal

- Carried Overwatch no longer blocks the unit from reserving Overwatch again on a later fresh activation.
- Selecting or Tab-cycling to a unit with carried Overwatch still does not cancel the existing watch.
- During the original committed activation, the button remains `OVERWATCH ARMED` and cannot be used again.
- Once MENACE gives that actor a fresh activation (`IsTurnDone == false` with AP restored), the panel becomes editable and the main action changes to `RE-ARM OVERWATCH - N REACTIONS - END TURN`.
- Re-arming validates the current weapon/mode/AP/ammo first, then atomically replaces the carried watch with a fresh reservation and ends the unit's turn. If validation fails, the old carried watch is preserved.
- The player may change INF/SPECIAL/BOTH/VEH/ALPHA selection before re-arming; merely changing the UI selection does not mutate the carried watch.
- Committing movement or another active skill still expires carried Overwatch exactly as in alpha.28/29.
- Added explicit `Overwatch renewed` logging with old remaining charges and new reaction count.

## alpha.29 indicator visibility refinement

- Removed `BrightOverwatchIndicators` from Mod Settings and runtime behavior. Overwatch no longer patches or writes `UnitHUD.SetOpacity`; MENACE remains fully in control of the normal spent/acted-unit fade.
- Added `HighContrastIndicators`, exposed as **High contrast Overwatch indicators**, default ON.
- High-contrast mode keeps the tested alpha.26/27 native healthbar position and 11px size, but uses a much stronger near-black silhouette with small faction-coloured accents. This is designed to remain readable when the entire native UnitHUD is dimmed.
- Turning high contrast OFF uses the lighter cyan/red reticle style.
- Reticle count remains one symbol per remaining reaction.
- alpha.28 commit-based Overwatch lifetime is retained unchanged: selecting/cycling does not cancel the watch; movement or an active skill does; reaction fire itself is excluded.

### alpha.29 test focus

1. Arm a unit and end its turn. Confirm MENACE dims the unit HUD normally; Overwatch must not force that HUD back to full opacity.
2. With high contrast ON, confirm the dark reticles remain readable on bright snow while the parent HUD is dimmed.
3. Turn high contrast OFF in Mod Settings and confirm the reticles switch back to the lighter cyan/red artwork without changing their position or the unit HUD opacity.
4. Confirm one versus two reactions still read as one versus two reticles.
5. Carry Overwatch into the unit's next turn, Tab through units, and confirm the watch remains until that unit commits movement or an active skill.
6. Confirm an Overwatch reaction shot does not cancel additional remaining charges.

## alpha.27 UI finalization

The native healthbar reticle row proven in alpha.26 is now the only Overwatch unit indicator.

Changes:
- removed the old world-space marker implementation and fallback;
- removed the experimental/native mode toggle: the native UnitHUD implementation is now standard;
- removed separate enemy-marker, reticle-size, and reticle-position settings;
- kept one setting only: `Show Overwatch indicators`;
- the setting deliberately keeps the legacy internal key `ShowWorldMarkers` so an existing user's on/off preference survives the upgrade;
- one small reticle equals one remaining reaction (`+`, `++`, `+++` visually as target reticles), up to eight shown;
- reticle size and placement are fixed to the alpha.26 layout that tested well in-game;
- friendly indicators use cyan; visible hostile indicators use red;
- hidden enemies never expose their Overwatch state through the UI;
- the tactical Overwatch panel still hides when no valid player unit is selected.

Implementation notes:
- the mod resolves the live MENACE `UnitHUD` for each watched actor from `UITacticalHUD.m_HUDList`;
- ownership is verified with `UnitHUD.GetActor()`;
- the reticle strip is inserted directly beside `m_HitpointsBar`, participating in MENACE's native overhead HUD layout;
- no custom screen-position math or `SimpleWorldSpaceIcon` fallback remains;
- public TacticalHUDCustomizer research by pylkij and public WOMENACE source by antistrategie were useful references for the current UnitHUD lifecycle and HUD lookup pattern;
- the reticle sprite remains generated procedurally by Overwatch; no external assets are copied.

- Replaced direct `Entity.IsPlayerControlled(false)` calls with a runtime-compatible resolver that supports both one-argument and parameterless interop signatures.
- Prevents the per-frame `MissingMethodException` / OnGUI error storm seen on MENACE 0.7.15.

## Build and deploy

Run `Build-and-Deploy-Dev.cmd` from this folder. The script builds the DLL against the configured MENACE installation and deploys the SourceVerified mod into the game's `Mods\Overwatch` directory.

Run `Build-Nexus-Package.cmd` after a successful build to create the Nexus package.

## Test focus

1. Arm one friendly unit with one reaction and another with two reactions; confirm one versus two tiny reticles beside their native health bars.
2. Pan and zoom the camera; the indicators should remain attached to the native overhead HUD without drifting.
3. Consume one charge from a two-reaction watch; confirm two reticles become one.
4. Let a visible enemy reserve Overwatch; confirm the same layout appears in red.
5. Break visibility on that enemy; confirm its indicator disappears without cancelling the internal watch.
6. Disable `Show Overwatch indicators`; confirm all friendly and enemy indicators disappear while Overwatch gameplay continues normally.
7. Deselect the current player unit; confirm the Overwatch action panel disappears.

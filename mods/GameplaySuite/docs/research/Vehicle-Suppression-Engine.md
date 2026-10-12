# Vehicle suppression engine notes — MENACE v0.7.14+20743

Static research used for Squaddie Veterancy 1.8.

## Confirmed native model

- `Menace.Tactical.Actor.ApplySuppression(float _value, bool _direct, Entity _suppressor, Skill _skill)` exists in the generated IL2CPP wrappers and native GameAssembly.
- Native `Actor.ApplySuppression` checks `EntityProperties.Flags` directly.
- `EntityFlags.ImmuneToSuppression = 32`.
- `EntityFlags.ImmuneToIndirectSuppression = 64`.
- Native `Actor.ApplySuppression` reads `EntityProperties.SuppressionImpactMult` directly rather than going through `EntityProperties.GetPropertyValue`.
- `EntityProperties.HasFlag(EntityFlags)` and `SetFlag(EntityFlags, bool)` exist in the generated callable wrappers.

## UI

- `Menace.UI.Tactical.UnitHUD` owns `m_SuppressionBar` and private `InitSuppressionBar(Actor)`.
- Native `UnitHUD.InitSuppressionBar` checks `ImmuneToSuppression`; when set, the suppression bar is hidden.
- `Menace.UI.Tactical.SelectedUnitPanel` also owns `m_SuppressionBar`.
- Native `SelectedUnitPanel.SetActor(Actor, bool)` performs the same `ImmuneToSuppression` check before configuring the selected-unit suppression display.

## Current templates

Typical player infantry uses:

- `SuppressionImpactMult = 1.0`
- no suppression-immunity flags

Human-crewed vehicle templates commonly use flags `96` (`32 + 64`). Their native suppression impact varies:

- many vehicles already use `SuppressionImpactMult = 0.5`
- several modular player vehicles and battle tanks use `0.0` because suppression is normally blocked by the immunity flags

## 1.8 implementation rule

Do not permanently strip vehicle immunity flags from templates/current properties.

For human-crewed vehicles only:

1. During native suppression application, temporarily clear `ImmuneToSuppression` and set/clear `ImmuneToIndirectSuppression` according to the mod option.
2. Temporarily resolve vehicle `SuppressionImpactMult` to the configured baseline (50% by default; preserve any already-lower external/native value).
3. Scale the incoming suppression value by crew rank.
4. Restore original impact and flags immediately after the native call.
5. During `UnitHUD.InitSuppressionBar` and `SelectedUnitPanel.SetActor`, temporarily clear the relevant immunity flag so MENACE initializes its own native bar, then restore the original flags.

Default rank modifier curve is the same as infantry:

- Green: -6% resistance
- Combat Tested: -4%
- Hardened: -2%
- Regular: 0%
- Veteran: +2%
- Crack: +4%
- Elite: +6%

With the 50% vehicle baseline, the resulting raw incoming suppression factors are approximately 0.53 / 0.52 / 0.51 / 0.50 / 0.49 / 0.48 / 0.47, so a vehicle is more resistant than infantry at the same rank by design.


## 1.8.1 runtime correction

The 1.8 approach of temporarily clearing suppression-immunity flags inside `UnitHUD.InitSuppressionBar` / `SelectedUnitPanel.SetActor` caused a native hard crash during Tactical loading. 1.8.1 removes those UI flag windows. Suppression remains implemented only around `Actor.ApplySuppression`; native bar forcing is deferred to a future UI-only implementation that does not mutate `EntityProperties` during Tactical UI construction.


## 1.8.3 confirmed safe design

The 1.8.2 runtime loaded the same Tactical mission successfully with all newly introduced suppression hooks removed. 1.8.3 therefore keeps the successful 1.8.2 hook set: it does not patch `Actor.ApplySuppression`, suppression UI methods, `TransientActor.GetCurrentProperties`, or a new TransientActor lifecycle method. Player vehicle properties are configured from the existing `UnitActor.OnMissionStarted` callback. Hostile vehicle properties are configured when the already-existing temporary vehicle-rank assignment path first resolves that vehicle. The actor's current `EntityProperties` are cloned, modified, and assigned back with `SetCurrentProperties`, allowing native `Actor.ApplySuppression` to read the configured `Flags` and `SuppressionImpactMult` directly.


## 1.8.4 HUD follow-up

Native disassembly confirms `Actor.ApplySuppression()` eventually calls `Actor.SetSuppression()`, and `SetSuppression()` calls `UnitHUD.UpdateSuppression()` when a HUD exists. The missing bar in 1.8.3 therefore did not require a new suppression implementation. `UnitHUD.InitSuppressionBar()` and `SelectedUnitPanel.SetActor()` hide their suppression bars when `ImmuneToSuppression` is present at UI initialization. 1.8.4 keeps the stable 1.8.3 property-clone design and uses the mod's existing periodic HUD refresh to set the already-created `UnitHUD.m_SuppressionBar` back to `DisplayStyle.Flex`; the selected-unit panel is refreshed through managed field access, without patching either UI initialization method.
## 1.9 runtime conclusion and replacement design

Runtime validation of 1.8.4 showed that clearing vehicle suppression-immunity flags and assigning a non-zero `SuppressionImpactMult` was not sufficient: the native vehicle actor remained at `GetSuppression() == 0`, `GetSuppressionPct() == 0`, `GetSuppressionState() == None` while receiving repeated committed attacks and crew-damaging defects. The visible bar was therefore only displaying a real native value of zero.

1.9 stops modifying native vehicle suppression entirely. Human-crewed vehicles instead use a battle-local `VehiclePressureState`. Pressure is sourced from MENACE's own weapon/attack suppression data on the committed skill and scaled by the configurable vehicle receive factor plus crew-rank resistance. The native suppression ProgressBar is retained only as a display widget for this custom value.

At the first threshold the crew becomes **Buttoned Up**, losing AP and Accuracy. At the second threshold the vehicle enters **Break Contact**: on its next activation the mod searches tiles farther from the last threat, validates them through MENACE movement rules, calls `Tactical.MovementSystem.GetPath(actor, tile, MoveBackwardsMode.Backwards, MovementFlags.None)`, and then starts movement with `Actor.MoveTo`. If no valid reverse path exists, no teleport/fallback movement is attempted. Native Morale remains independent and takes priority when already Fleeing.



## 1.9.1 Break Contact correction

The 1.9 runtime test proved Buttoned Up AP penalties and Break Contact state transitions worked, but one visually plausible retreat returned `no-safe-reverse-path`. The first implementation rejected candidate tiles geometrically before asking MENACE for a reverse path. 1.9.1 reverses that order: it requests real `MoveBackwardsMode.Backwards` paths first, then scores reachable destinations by distance gained from the last threat, with rearward alignment only a preference. The HUD also gains explicit `BUTTONED UP` and `BREAK CONTACT` labels so pressure effects are visible without reading logs.

## 1.9.2 confirmed-damage pressure shock

The 1.9.1 runtime log showed that weapon pressure increased correctly but actual vehicle damage/defects did not add any additional pressure. 1.9.2 hooks the native `TacticalManager.InvokeOnDamageReceived(Entity, Entity, Skill, DamageInfo)` dispatcher and only treats `DamageInfo.IsDamageInflicted && DamageInfo.Damage > 0` as confirmed damage. The default confirmed-damage shock is +40 pressure per attack action (deduplicated), with additional defect shocks of +10 Light / +20 Medium / +35 Heavy. Rank resistance applies to both.
## 1.9.3 Break Contact candidate-path correction

1.9.2 runtime diagnostics reached `Break Contact` correctly but logged `destinations=0 reversePaths=0` in situations where the player could visually see free space behind the vehicle. Source review showed the scan called `Tile.IsValidMovementDestination()` and `Tile.CanBeEnteredBy(actor)` before invoking MENACE pathfinding. For vehicle actors those helpers can reject all nearby tiles.

1.9.3 removes those two pre-filters. In-bounds valid tiles are sent directly to `MovementSystem.GetPath(actor, tile, MoveBackwardsMode.Backwards, MovementFlags.None)`. The returned native `MovementResult` is then filtered by success, non-empty path, AP cost, and positive distance gain from the remembered threat. This keeps retreat conservative while making the actual vehicle pathfinder the source of truth. No force/teleport/skip-pathfinding flags are introduced.



## 1.9.4 Walker locomotion and Rally bridge

The 1.9.3 runtime test confirmed that Break Contact pathfinding and `Actor.MoveTo` work, but modular walkers visibly slid during `MovementAction.Backwards`: actor translation occurred while their leg locomotion did not. Extracted animator data also distinguishes walker locomotion from conventional vehicles. 1.9.4 therefore detects vehicle templates whose ID contains `walker` and asks `MovementSystem.GetPath(..., MoveBackwardsMode.Forward, ...)` for their Break Contact route. The destination/scoring rules are unchanged: the path must succeed, fit available AP, and increase distance from the remembered threat. Non-walker vehicles still use `MoveBackwardsMode.Backwards`.

MENACE `perk.command_rally` is a targeted non-attack skill with a native `ChangeMorale` reset and `ChangeSuppression` of -80. Vehicle Crew Pressure is intentionally independent from those native systems, so before 1.9.4 Rally could reset native morale/suppression while leaving a custom `BreakContactPending` at 100 pressure. 1.9.4 handles this skill in the already-existing `TacticalEventHooks.OnSkillUsed` callback before the attack-only XP filter. By default it removes 80 Crew Pressure and cancels a pending Break Contact if the resulting pressure is below threshold and retreat movement has not started. It does not interrupt movement already in progress.

The reused suppression ProgressBar remains display-only. The floating label now explicitly says `CREW PRESSURE` and shows its value, while runtime logs include `nativeMorale` context so custom pressure and MENACE morale are not conflated.


## 1.9.5 Timed status callouts and persistent badges

The Crew Pressure ProgressBar remains the continuous pressure display. Full `BUTTONED UP` / `BREAK CONTACT` text is now treated as a transient state-change callout: it remains visible for 5 seconds after entering or changing state, then is removed. A separate compact UI Toolkit `Label` persists as a status badge while the threshold remains active (gold `B` for Buttoned Up, red left-arrow for Break Contact). This is UI-only and adds no Harmony hooks or gameplay changes.


## 1.9.5.6 runtime compatibility note

MENACE 0.7.15 was observed rewriting the floating UnitHUD suppression ProgressBar after Squaddie Veterancy initialized it to custom Crew Pressure. Runtime log showed `pressure=0/100 normalized=0` while the world-space bar visibly returned to about 50%. 1.9.5.6 therefore re-asserts custom pressure every existing HUD refresh rather than only on pressure changes.

The same patch adds a path-mode fallback ladder for Break Contact after a 0.7.15 runtime case produced `destinations=196 nativePaths=0` for explicit Backwards movement. Conventional vehicles prefer Backwards, then native Auto, then Forward; walkers prefer Forward then Auto. All passes still use `MovementSystem.GetPath(..., MovementFlags.None)`, require affordability and positive threat-distance gain, and never use Force/SkipPathfinding/teleport.


## 1.9.5.8 dedicated display conclusion

Repeated MENACE 0.7.15 tests showed the native floating vehicle suppression bar at roughly 50% while Squaddie Veterancy logged authoritative Crew Pressure `0/100 normalized=0`. Reasserting the native bar value every ~0.2 s did not make the display reliable. Therefore the native suppression ProgressBar is no longer treated as a trustworthy Crew Pressure surface. 1.9.5.8 hides it for pressure-enabled vehicles and renders a separate mod-owned meter from `VehiclePressureState.Pressure`. At zero pressure no custom meter is shown.

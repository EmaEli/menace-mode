# Overwatch 0.32.3.6

## Alpha 32.1 changes

- Logging cleanup only; gameplay behavior is unchanged from alpha.32.
- Added **Debug logging** under the Diagnostics section of Mod Settings, default OFF.
- Routine Overwatch event/AI/HUD/reaction diagnostics are now completely silent unless Debug logging is enabled.
- Only rare warnings/errors needed to diagnose a broken hook or failed reaction remain visible by default.

## Alpha 32 changes

- Overwatch reaction provocation is now whitelisted to **movement completion** and completed native **attack skills** only.
- Utility/state-transition actions no longer provoke Overwatch. Reloads, stance/setup actions, embark/disembark/passenger-eject actions and other non-attack active skills are ignored by the reaction-trigger path.
- This specifically prevents transport actions such as `special.vehicle_eject_passengers_free` from entering reaction evaluation while MENACE is changing passenger/vehicle tactical state.
- The existing settings key is kept for compatibility, but the UI option is now named **Trigger on enemy attacks**. It controls only attack-skill provocation; movement triggers remain active independently.
- The watching unit's own carried Overwatch still expires when that unit commits movement or any active skill. This safety change only narrows which enemy actions can provoke reaction fire.
- Indirect-fire weapon exclusion from alpha.31 remains unchanged.


Adds XCOM-style reaction fire to MENACE infantry and, optionally, vehicles.

## Core rules

- Overwatch can be provoked when an enemy **finishes movement** or **completes a native attack skill**.
- Movement reactions are evaluated on the mover's final tile.
- Attack reactions occur after the native attack resolves and evaluate the acting enemy on its current tile. Utility/state-transition skills such as reloads, stance/setup actions and embark/eject-passenger actions do not provoke Overwatch.
- Movement skills are not also treated as attack triggers, avoiding double reactions for the same movement.
- Overwatch reaction shots do **not** recursively trigger Overwatch.
- Arming Overwatch consumes all remaining AP and ends that actor's turn.
- Reaction charges are `floor(AP before arming / selected reaction cost)` and are also bounded by native uses/ammo. In BOTH mode, charges may continue while at least one selected weapon still has uses remaining.
- Unused reaction charges persist across enemy activations and into the watching unit's next turn. Merely selecting or cycling to that unit does not cancel Overwatch. The watch expires when that unit commits movement or another active skill, or when charges/ammo are exhausted or the unit becomes invalid.
- One watching unit spends at most one reaction on a given movement completion or completed attack event.
- Default player trigger threshold is 60% after the configurable Overwatch aim penalty (default -10 percentage points).
- Player Overwatch ignores Player AI, Civilians and Allied Local Forces.
- Weapons tagged by MENACE as **INDIRECT_FIRE** are never valid Overwatch attacks. Direct-fire rocket weapons remain eligible unless they also carry that tag.
- **Trigger on enemy attacks** is enabled by default in Mod Settings and can be disabled independently; movement triggers remain active.

## Infantry weapon choice

- **INF**: infantry/squad weapon.
- **SPECIAL**: squad-leader/special weapon.
- **BOTH**: available when Coordinated Fire is installed; a reaction triggers when either selected infantry weapon qualifies. Each weapon is checked independently, every qualifying weapon fires sequentially, and an ineligible weapon is skipped. The reaction cost remains the more expensive selected attack.

Coordinated Fire remains an infantry integration. Overwatch reaction calls use `Free | IgnoreUsabilityCheck`, so Coordinated Fire does not reinterpret them as normal salvos. Closely grouped native child attack uses from one composite attack are collapsed into one attack provocation.

## HUD selection memory

The active Overwatch choice is highlighted in green so the selected mode is easier to read at a glance.

Selection is remembered per player unit for the duration of the current tactical mission. If one squad is set to **BOTH**, another to **INF**, and a vehicle to a particular **VEH** attack, switching between them restores each unit's own last choice. Weapon-cycle positions are remembered as well.

Memory is intentionally mission-local and is cleared when the tactical mission resets or ends.

The Overwatch control panel always hides when no player infantry squad or vehicle is selected. **Show panel when selected unit cannot Overwatch** only controls whether the panel stays visible for a valid selected unit that temporarily cannot arm Overwatch.

## Overwatch unit indicators

Units holding Overwatch display compact reticles directly beside the native MENACE health bar. Friendly watches use cyan reticles and hostile watches use red reticles.

Each reticle represents one remaining reaction. One shot shows one reticle, two shots show two reticles, and so on, up to eight visible reticles. This keeps the indicator small while making the remaining reaction count immediately readable.

The indicator is integrated into MENACE's native overhead `UnitHUD` layout beside the hitpoint bar, so it follows the game's own camera scrolling, zooming, HUD pooling and unit positioning. The previous custom world-space and experimental screen-space marker modes have been removed.

Enemy indicators are information-safe: a hostile Overwatch indicator is only shown while that enemy actor/model is currently visible to the player. Hidden enemies never reveal their Overwatch state through the UI.

The marker settings are **Show Overwatch indicators** and **High contrast Overwatch indicators**. High contrast is enabled by default and changes only the reticle artwork; it never overrides MENACE's native HUD opacity. Position and size are intentionally fixed to the tested native layout.

## Vehicle Overwatch

Vehicle support is controlled by **Mod Settings -> Overwatch -> Enable vehicle Overwatch** and is enabled by default.

- **VEH** selects a native ranged vehicle attack. Click the selected VEH chip again to cycle through other supported vehicle attacks/firing actions.
- AP/reaction accounting uses the selected attack's native `GetActionPointCost()`.
- All remaining AP are committed when vehicle Overwatch is armed, just like infantry.
- **AI vehicles use Overwatch** is a separate setting and defaults off.

## Alpha Strike compatibility

Overwatch does not copy or modify Alpha Strike. If the installed Alpha Strike mod exposes its `Alpha Strike` action in the vehicle's native attack list, Overwatch detects it and displays a separate **ALPHA** choice.

When ALPHA is selected:
- Overwatch uses the Alpha Strike skill's own AP cost/range/usability;
- Overwatch invokes the Alpha Strike skill directly instead of firing the two vehicle weapons itself;
- Alpha Strike remains responsible for its child weapon selection, ammo, effects and special handling;
- Overwatch does not manually decrement the composite skill's ammo;
- the configured Overwatch aim penalty stays active for the composite resolution so child hit-chance calls are penalized too.

Composite native child skill events are coalesced so one Alpha Strike action does not intentionally consume several Overwatch charges merely because it uses multiple native child skills.

## AI Overwatch

AI Overwatch is reserved at the end of a hostile AI actor's activation using its leftover AP. An AI unit that spends all of its AP cannot enter Overwatch; an AI unit with enough leftover AP may reserve one or more reactions.

AI Overwatch can react to player movement completion **and**, when attack triggers are enabled, completed native player attack skills such as weapon fire. Non-attack utility/transport skills do not provoke it. The same range, line-of-sight, ammo and hit-threshold validation applies.

The dedicated end-of-activation validation path does not require player-turn usability after MENACE marks the AI actor turn-done/inactive. Civilians and other player-allied AI factions are excluded. AI vehicle Overwatch remains separately configurable and defaults off; hostile vehicle discovery accepts native itemless/hidden turret attacks while player-facing vehicle selection remains limited to visible actions.

## Timing and recursion safety

Queued movement reaction fire waits a few frames after movement completion before calling the native attack. Completed native attack skills are likewise allowed to unwind until MENACE's tactical skill system is idle before their Overwatch event is evaluated.

This avoids re-entering MENACE AI visibility/assessment bookkeeping while native movement or skill resolution is still mutating tactical state. Overwatch's own native reaction `Skill.Use` calls are marked as reaction-generated and never feed back into the attack-trigger system, preventing infinite counter-fire chains.

MENACE sets reacting actors to 0 AP during the opposing turn, so reaction attacks use the proven `Free | IgnoreUsabilityCheck` path. For ordinary limited-use attacks, Overwatch reconciles one use after a successful native shot only when MENACE did not consume it itself.

If attack triggers, vehicle selection, Alpha Strike, ammo or turn flow behave unexpectedly, send `MelonLoader/Latest.log`.

## Credits

The native Overwatch indicator uses MENACE's own overhead UnitHUD and hitpoint-bar layout. Public **TacticalHUDCustomizer** research by **pylkij** and public **WOMENACE** source by **antistrategie** were useful references for confirming the current UnitHUD lifecycle, HUD-list lookup and `UnitHUD.GetActor()` matching pattern. Overwatch uses its own marker state and artwork and includes no external assets.


## 0.32.3.6

- Fixed sibling fire modes of the same weapon (e.g. assault rifle normal / sustained fire) drifting apart by one round after a reaction shot. Reactions fire with `Free` usage so the game consumes nothing; the module then deducts one use with `Skill.SetUses`, which writes only that skill's counter. It now also calls `Skill.SynchronizeUses()`, the native per-item synchronization that `ConsumeUse` triggers, so both modes show the same ammo. GitHub issue #4.

## 0.32.3.2

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## 0.32.3.1

- Compatibility-only patch for the unified repository/suite distribution.
- Coordinated Fire and Alpha Strike presence detection now checks loaded assemblies as well as standalone mod folders, so optional integrations work when multiple feature DLLs are shipped inside one suite folder.
- No Overwatch gameplay balance changed.

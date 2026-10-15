## 1.6.8

- Fixed Berserk and other clamped AP gains losing part of their refund when a kill happens during Coordinated Fire. Native child attacks still use `NoApConsumed`; the combined AP fee is charged once at settlement, with only the clipped positive grants restored afterward (bounded by the fee).
- Applies to both sequential and synchronized player volleys. Multiple kills cannot grant more than normal cost-before-kill semantics; ordinary attacks, ammunition, AI handling, and partial-salvo safety remain unchanged.
- Normal logging remains silent by default. The new AP carry diagnostic is debug-only.
- **Source/model verified; Windows build and in-game gameplay verification pending.** GitHub issue #19 remains open.

## 1.6.7

- Fixed Coordinated Fire disabling itself when a long-running/setup child weapon (observed with `active.medium_mg_suppressive_fire_tripod`) asynchronously zeroes the actor's AP after `Skill.Use` already returned with `NoApConsumed`.
- The runtime now restores the launch AP only for this owned zero-AP side effect while the same live, unstunned actor still owns the same turn/round; genuine death, stun, rout, actor/round changes and other ambiguous AP changes remain fail-closed.
- The normal single max-cost salvo fee is still charged once after both attacks. Ammo/uses remain native.

# Changelog

## 1.6.6

- Fixed Coordinated Fire disabling itself after Lim's **Run & Gun / Full Send** changes a child weapon's AP cost between the first and second shot.
- Child attacks now use MENACE's native `UsageParameter.NoApConsumed`; Coordinated Fire still charges the single combined fee once after the volley, while native ammunition / limited-use consumption remains unchanged.
- The defensive AP-accounting fallback now reads each child's live AP cost at launch instead of relying on the potentially stale plan snapshot.
- No targeting, damage, ammo balance, AI selection, vehicle, synchronized-fire, or HUD changes.

## 1.6.5

- Updated player ownership detection for the current MENACE API: `Entity.IsPlayer()` / `Entity.IsPlayerOrPlayerAI()` replace removed `IsPlayerControlled(bool)`.
- Kept a legacy reflective fallback so the authoring source remains buildable against older generated interop assemblies.
- No gameplay, AP, ammo, targeting, or AI combined-fire balance changes.

## 1.6.4

- Fixed MENACE 0.7.15 compatibility by resolving `Entity.IsPlayerControlled` at runtime (supports both bool-argument and parameterless interop signatures).
- Prevents repeated tactical-update failures caused by `MissingMethodException`.
- No gameplay balance changes.

## 1.6.3

- Reduced runtime logging to effectively zero during normal play.
- Routine Coordinated Fire, synchronized-volley, AI decision, AP accounting and turn-flow messages are now emitted only when **Debug logging** is enabled in Mod Settings.
- Debug logging remains OFF by default.
- Critical runtime/binding/AP-accounting errors are still written to the log so genuine failures remain diagnosable.
- No combat, AI, vehicle, synchronized-fire or HUD behavior changes.

## 0.3.0-beta.1.6.2

- Fixed synchronized fire never reaching the second-shot attempt in live MENACE testing.
- The follow-up safety check now compares weapon-template identity to weapon-template identity instead of comparing a runtime item instance pointer with a stored template pointer.
- This also fixes the same false `The equipped weapon/fire mode changed` cancellation on the normal sequential follow-up path.
- Added explicit synchronized preflight pass/reject logging so live logs show whether the guarded overlap attempt is actually reached.
- No safety bypass was added: MENACE native usability/busy checks still decide whether the overlapping second `Use()` is accepted, with sequential fallback on a clean rejection.

## 0.3.0-beta.1.6.1

- Fixed the synchronized-volley safety drain exposed by the core regression suite.
- If the synchronized second shot becomes illegal after the first native attack has started, Coordinated Fire now waits for explicit first-shot completion before releasing AP/UI state, even on frames where MENACE is not reporting the actor as busy.
- A bounded timeout remains in place so a missing native completion event cannot stall the transaction forever.
- No combat-rule, AI, vehicle, HUD, or Alpha Strike compatibility changes from beta.1.6.

## 0.3.0-beta.1.6

- Added an **experimental, opt-in synchronized player volley** setting.
- Manual player Coordinated Fire can attempt the second native attack 0.10 seconds after the first attack reports native start.
- The synchronized attempt never bypasses MENACE usability/busy checks. If MENACE rejects it cleanly, the controller automatically falls back to the proven sequential follow-up.
- AI remains sequential and retains the beta.1.4 final-AP turn-safety guard.
- Synchronized completion waits for both native completion events and a non-busy game state before AP settlement / UI release.
- Added regression coverage for stagger timing, accepted overlap, clean fallback, legality failure and disabled-mode behavior.

## 0.3.0-beta.1.5

- Added optional vehicle Coordinated Fire. Vehicle support is OFF by default.
- Vehicle mode pairs attacks from two different equipped weapon templates and keeps the normal max-AP, native-ammo and native-targeting rules.
- Identical vehicle weapon types are not paired, leaving vanilla twin-link behavior alone.
- Added Alpha Strike detection/warning when vehicle Coordinated Fire is enabled at the same time.
- Vehicle attacks are completely ignored when the vehicle option is disabled, allowing Alpha Strike to remain the vehicle combined-fire provider.
- Added vehicle handling to AI conversion while retaining the beta.1.4 final-AP safety guard.
- Added regression tests for vehicle opt-in, distinct weapons and non-vehicle rejection.

## 0.3.0-beta.1.4

- Fixed an AI turn-stall seen when Coordinated Fire consumed the AI actor's final AP.
- AI attacks that would exhaust the actor's AP are now left completely vanilla so MENACE owns the native AI turn hand-off.
- Added regression tests for terminal AI action conversion.

## 0.3.0-beta.1.3

- Added configurable HUD placement for different resolutions, aspect ratios and UI layouts.
- Added Bottom Right and Top Center presets plus a center/custom-offset preset.
- Added horizontal and vertical HUD offsets.
- Expanded HUD scale range to 0.65x-1.5x with automatic fit protection on unusually small screens.
- Added Compact HUD mode (main action button only).
- Added a one-click Mod Settings toggle to reset HUD layout defaults.
- HUD placement is screen-relative and clamped so it cannot be moved completely off-screen.
- No Coordinated Fire combat-rule changes from 0.3.0-beta.1.2.

## 0.3.0-beta.1.2

- Completed an English-only pass over all user-facing HUD, settings, status, diagnostic, documentation, and build-script text.
- Build scripts now print a fixed English keypress prompt instead of displaying the operating system's localized `pause` message.
- No gameplay behavior changes from 0.3.0-beta.1.1.

## 0.3.0-beta.1.1

- Fixed the Modpack Loader plugin build failing because `Harmony` resolved as a namespace instead of `HarmonyLib.Harmony`.
- Fully qualified the `IModpackPlugin.OnInitialize` Harmony parameter and stored Harmony instance.
- No gameplay or UI behavior changes from 0.3.0-beta.1.

## 0.3.0-beta.1

- Converted Coordinated Fire from a standalone MelonLoader DLL to a **Menace Modpack Loader v2 plugin**.
- Nexus/end-user distribution is now a normal modpack folder: `modpack.json` + `dlls/CoordinatedFire.dll`.
- Removed the end-user build/install script requirement.
- Replaced the large always-on debug panel with a compact tactical control.
- Moved configuration to the Modpack Loader's `ModSettings` system.
- Moved diagnostic controls to the developer console instead of occupying tactical screen space.
- Preserved the tested combined-fire AP logic, setup/deployment gating, native ammo consumption, native turn flow and AI conversion logic from 0.2.0-alpha.4.1.3.

## 0.2.0-alpha.4.1.3

- Expanded AI interception to normal audio/instant-resolve usage flags and added rejection logging.
- Preserved native actor turn flow when AP remain and used MENACE's normal EndTurn path when the action budget is spent.
- Added explicit deployment/setup gating and corrected combined AP accounting.

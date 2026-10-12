Squaddie Veterancy 1.10

1.10 COMPATIBILITY API

- Adds a stable read-only API for other mods to query an individual squaddie by native MENACE squaddie ID.
- `SquaddieProbe.CompatibilityApi.GetSquaddieXp(int squaddieId)` returns current persistent XP, or `-1` when the veterancy runtime is unavailable.
- `SquaddieProbe.CompatibilityApi.GetSquaddieRank(int squaddieId)` returns the individual rank name, or an empty string when unavailable.
- No gameplay, XP, rank-threshold, persistence, or logging changes.


Squaddie Veterancy 1.9.6.1

1.9.6.1 VEHICLE BUTTONED-UP CONTROL FIX

- Crew Pressure is now authoritative for tracked vehicle combat stress on player, allied and enemy vehicles that participate in the pressure system.
- Native MENACE morale no longer turns a Crew-Pressure vehicle into generic Wavering/Fleeing panic behavior.
- Buttoned Up now means exactly the intended penalties: -20 AP and -20% Accuracy by default, while the vehicle remains otherwise usable.
- Break Contact remains a distinct high-pressure auto-retreat state.
- After Break Contact, pressure transitions to the configured post-break Buttoned value only after the retreat activation ends, avoiding a Buttoned label on an AP-zero retreating vehicle.
- No XP, rank, pressure thresholds or damage-shock balance changes.


1.9.6 HUD RANK MARKER POSITION

- Moves the floating veterancy rank marker from below the unit bars to the right side of the HP/suppression block.
- Position is calculated from live UI Toolkit geometry instead of a fixed horizontal offset, so pooled/rebuilt UnitHUD instances are handled correctly.
- Infantry markers use MENACE's HP + suppression geometry; vehicles use HP + the mod-owned Crew Pressure meter when present.
- The marker remains an absolute 18x18 overlay and does not participate in flex layout, preserving the black-rectangle fix from 1.9.5.10.
- No XP, rank, balance, save-format or Crew Pressure gameplay changes. Debug logging remains OFF by default.


1.9.5.10 HUD LAYOUT / LOGGING FIX

- Keeps the 1.9.5.8 hot-path logging suppression: repeated JAM CHECK, hitchance-apply, concealment-apply and XP-target-hint traces remain removed, with Debug logging OFF by default and rate-limited when enabled.
- Fixed the black rectangle behind floating veterancy markers. The marker is now an absolute overlay instead of a normal flex-layout child, so it can no longer enlarge MENACE's HP-bar background.
- Restored the proper drawn service-bar and chevron artwork instead of the temporary text glyphs. Regular/Crack use clean double chevrons and Elite uses triple chevrons.
- Rank colors/meaning are unchanged.
- No gameplay, XP, rank, balance, Crew Pressure or save-format changes.


1.9.5.6 VEHICLE CREW PRESSURE UI / BREAK CONTACT FIX

- Fixed the floating vehicle Crew Pressure bar being overwritten by MENACE after mission start. It is now synchronized continuously with the mod's actual Crew Pressure value.
- Break Contact still prefers reverse movement for conventional vehicles and forward movement for walkers. If MENACE 0.7.15 cannot produce a valid path in that preferred mode, the mod now tries safe native Auto/Forward fallbacks rather than immediately giving up.
- All fallback movement still uses MENACE pathfinding, must be affordable and must move farther from the remembered threat. No teleport, forced movement, or skipped pathfinding is used.
- Native Morale remains separate and authoritative; recovered morale is not artificially latched as Fleeing.
- No balance, XP, rank, Crew Pressure threshold, Rally or damage-shock changes.

1.9.5.5 MENACE 0.7.15 ROSTER PROXY COMPATIBILITY

- Fixed player veterancy roster loading on MENACE 0.7.15 by reading inherited Count through an explicit IL2CPP IReadOnlyCollection<T> proxy over the same native pointer.
- Updated the same collection handling for alive squaddies, hired leaders and squad-member ID lists.
- Updated CreateElement hook discovery for the current MENACE signature while keeping the old signature as fallback.
- Added a persistence safety guard: save snapshots are not written while the authoritative squaddie roster is unavailable, preventing empty XP sidecars after an incompatible game patch.
- Debug logging remains OFF by default. No balance changes.

1.9.5 CREW PRESSURE STATUS BADGES

- BUTTONED UP / BREAK CONTACT full HUD messages now fade after 5 seconds.
- Persistent compact status badges remain while the state is active: B for Buttoned Up and a red retreat arrow for Break Contact.
- State changes restart the 5-second message. Rally clears the badge when pressure falls below the active threshold.
- No gameplay or balance changes from 1.9.4.

1.9.4 WALKER RETREAT / RALLY / CREW PRESSURE UI

- Walkers now Break Contact by turning and moving forward away from the threat, so MENACE can use their normal leg locomotion instead of visually sliding backward. Wheeled/tracked vehicles continue to reverse.
- Command: Rally now removes 80 Vehicle Crew Pressure by default (configurable), matching Rally's native -80 Suppression effect. If the retreat has not started yet, dropping below the Break Contact threshold cancels the pending auto-retreat.
- Rally does not abruptly stop a vehicle already moving; the retreat completes, but its final pressure can no longer be raised back up by the post-retreat reset.
- Vehicle HUD feedback now explicitly identifies the custom meter as `CREW PRESSURE`, including the current value. Buttoned Up and Break Contact remain clearly labelled.
- Native Morale and native Suppression remain separate MENACE systems; pressure logs now include morale context to make interactions diagnosable.
- No new Harmony hook was added for Rally.

1.9.3 BREAK CONTACT REVERSE-PATH FIX

- Fixed Break Contact searches reporting `destinations=0` even when the vehicle visibly had room to reverse.
- Vehicle retreat candidates are no longer rejected by `Tile.IsValidMovementDestination()` / `Tile.CanBeEnteredBy(actor)` before pathfinding.
- Every valid in-bounds candidate is now evaluated by MENACE's native `MovementSystem.GetPath(..., MoveBackwardsMode.Backwards, ...)`.
- A path must still be successful, affordable with current AP, and increase distance from the last threat.
- No forced movement, skipped pathfinding, or teleport fallback is used.
- Existing damage/defect pressure shocks, Buttoned Up penalties, Break Contact HUD feedback, and pressure balance are unchanged.

1.9.2 VEHICLE DAMAGE SHOCK / PRESSURE FEEDBACK

Confirmed vehicle damage now causes a large pressure shock in addition to normal incoming-fire pressure. Defaults: +40 for a damaging hit, then +10/+20/+35 for Light/Medium/Heavy defects. All values are configurable and crew-rank resistance still applies.

- Break Contact now evaluates real MENACE Backwards paths before rejecting retreat destinations, allowing diagonal and partially lateral reverse escapes when they genuinely increase distance from the threat.
- Added stronger vehicle pressure feedback above the unit HUD:
  - BUTTONED UP shows the active AP and accuracy penalties.
  - BREAK CONTACT shows AUTO-RETREAT while the retreat state is armed.
- Added detailed retreat-search logging for easier diagnosis if no valid reverse path can be found.
- No new combat Harmony hooks were added.

1.9 VEHICLE CREW PRESSURE / BREAK CONTACT

- Vehicle suppression is now a custom **Vehicle Crew Pressure** mechanic because MENACE does not accumulate native suppression on vehicles even when immunity flags are removed. Native vehicle suppression is left untouched.
- Incoming committed attacks add pressure from MENACE's own weapon/attack suppression values, weapon repetitions, a configurable vehicle receive factor (50% default), and the vehicle crew's veterancy resistance.
- **Normal**: below the Buttoned threshold.
- **Buttoned Up**: at 50+ pressure by default; the vehicle loses 20 AP once per activation and suffers -20% Accuracy.
- **Break Contact**: at 100 pressure by default; on its next activation the vehicle attempts a real reverse retreat away from the last threat using MENACE pathfinding. No teleport is used.
- A successful Break Contact spends the rest of the activation. Pressure remains in Break Contact until that activation ends, then falls to 60 by default so the next activation begins Buttoned Up and usable.
- If no safe retreat path is available, the vehicle does not force invalid movement; it loses the activation, remains visibly in Break Contact for that activation, and falls back to the configured post-break pressure when the activation ends.
- Native morale remains stored internally, but Crew Pressure is authoritative for pressure-managed vehicle control: generic Wavering/Fleeing behavior is masked so Normal/Buttoned vehicles stay usable and Break Contact is the only forced-retreat state.
- The existing vehicle suppression bar is retained only as a display for the custom pressure meter.
- Pressure normally recovers by 20 after a completed activation. Thresholds, AP/Accuracy penalties, pressure receive factor, recovery, post-break pressure, reverse-search radius and rank modifiers are configurable.
- Player vehicles use persistent active-crew rank; hostile human-crewed vehicles use their battle-local vehicle rank. Constructs and Wildlife remain excluded.

1.8.4 VEHICLE SUPPRESSION HUD FIX

- Vehicle suppression remains implemented through safe per-actor runtime properties; no Actor.ApplySuppression or suppression UI Harmony patches are installed.
- Re-shows MENACE's existing native suppression bar for configured human-crewed vehicles after HUD initialization.
- Keeps the selected-unit suppression bar synchronized for the currently selected vehicle.
- Adds lightweight runtime diagnostics whenever a vehicle's real suppression value changes.


1.8.3 SAFE VEHICLE SUPPRESSION REWORK
- Re-enables vehicle suppression after the 1.8.2 crash-isolation build.
- No `Actor.ApplySuppression` patch and no suppression UI patch are used.
- Each human-crewed vehicle receives a cloned runtime property set with native suppression immunity removed and its suppression-impact multiplier configured. Shared templates are not modified.
- MENACE then performs suppression and suppression-bar handling natively.
- Regular vehicle crews receive about 50% of the raw suppression of equivalent infantry. Defaults by rank are approximately: Green 53%, Combat Tested 52%, Hardened 51%, Regular 50%, Veteran 49%, Crack 48%, Elite 47%.
- Indirect vehicle suppression is enabled by default and can be disabled in Mod Options.
- Player vehicles use persistent crew rank; hostile human-crewed vehicles use battle-local AI vehicle rank. Constructs and Wildlife remain excluded.

1.8 VEHICLE CREW SUPPRESSION
- Adds vehicle suppression as a crew effect while keeping vehicles substantially harder to suppress than infantry at equal veterancy.
- Uses MENACE's native suppression state/physics and native HUD rather than a custom suppression system.
- Existing saves and vehicle crew sidecars require no migration.

1.7.1 SQUAD-MAPPING SAFETY FIX

- Fixed a transient tactical `squad=0` case that could use one squaddie's XP as if it were the whole squad average.
- The mod now reuses the last valid roster mapping for the current campaign during temporary Tactical/Mission Preparation mapping gaps.
- If a trustworthy squad mapping still cannot be resolved, squad modifiers and HUD rank are skipped rather than inventing a rank.
- Squad XP awards and deployed-squad tracking also refuse the old singleton fallback.
- No balance, rank, AI-profile, vehicle-crew, persistence-schema, or save-path changes.

SEVEN-RANK PROGRESSION

- Green: 0-249 XP, army-green service bar.
- Combat Tested: 250-499 XP, one brass chevron.
- Hardened: 500-999 XP, one silver chevron.
- Regular: 1000-1999 XP, two silver chevrons.
- Veteran: 2000-3999 XP, one gold chevron.
- Crack: 4000-7999 XP, two gold chevrons.
- Elite: 8000+ XP, three gold chevrons.
- The thresholds now double cleanly from 250 onward: 250 -> 500 -> 1000 -> 2000 -> 4000 -> 8000.
- Default Accuracy, Defense and Suppression Resistance are symmetric around Regular: Green -6%, Combat Tested -4%, Hardened -2%, Regular 0%, Veteran +2%, Crack +4%, Elite +6%.
- Existing persistent XP is preserved; no sidecar migration is needed for the new rank ladder.

AI / ENEMY VEHICLE VETERANCY

- Temporary human NPC infantry now uses the same seven-rank vocabulary. Ranks remain battle-local and are never written into player save sidecars.
- Exact EntityTemplate profiles distribute ranks by faction and unit quality: basic Pirates skew Green/Combat Tested, trained Rogue Army units skew Hardened/Regular, and command/elite formations are where Veteran/Crack/Elite become common.
- Human-crewed hostile vehicles now receive one temporary crew/actor rank from vehicle-specific Pirate/Rogue profiles.
- Temporary AI rank affects Accuracy, Defense and Suppression Resistance with the same -6/-4/-2/0/+2/+4/+6 curve.
- Enemy vehicle crews do not use infantry no-fire rolls; no-fire remains an individual infantry reliability mechanic.
- Constructs and Wildlife remain intentionally outside veterancy even when the tactical entity is vehicle-shaped.

EXTENDED VETERAN RANKS

- Veteran: 2000-3999 XP, one gold chevron.
- Crack: 4000-7999 XP, two gold chevrons.
- Elite: 8000+ XP, three gold chevrons.
- Existing XP is preserved and squads/vehicle crews are re-ranked automatically.

PERSISTENT VEHICLE CREW POOL

- Vehicle XP/status belongs to persistent crew people, not to weapon roles.
- Native MENACE Pilots/leaders remain outside vehicle crew XP and casualty rolls.
- Current mounted weapons only define active jobs: base crewman 1, Light +1 gunner, Medium +1 gunner/+1 loader, Heavy +1 gunner/+2 loaders.
- Changing loadout reassigns the same active people whenever possible, preserving all XP/status.
- If crew demand shrinks, surplus personnel move to reserve without losing progression.
- If crew demand grows, experienced reserve personnel return before any new Green crewman is recruited.
- Example: a four-person Heavy crew can switch to Light + Medium and remain the same four people; only their job labels change.
- Vehicle rank uses average XP of active crew only.
- Only active crew gains XP and participates in vehicle-destruction/critical casualty rolls.
- Sidecar schema v8 migrates older role-keyed records into stable personnel-pool identities without discarding progression.

MOUNTED-WEAPON-DERIVED CREW REQUIREMENT

- Empty weapon mounts add no crew.
- Crew demand follows the mounted weapon class, not the chassis mount size. A Medium weapon in a Heavy mount still counts as Medium crew.
- Base crewman / driver: 1
- Mounted Light weapon: +1 gunner
- Mounted Medium weapon: +1 gunner +1 loader
- Mounted Heavy weapon: +1 gunner +2 loaders

VEHICLE CREW EXPERIENCE

Default XP per active crew member:
- Attack action: +1
- Directly targeted by a hostile attack: +2
- Enemy infantry element killed: +5
- Enemy vehicle destroyed: +50
- Successful mission participated: +10
- Survived successful mission: +10 additional
- Primary objective: +25
- Secondary objective: +10

VEHICLE-KILL XP

- Infantry squads receive +50 XP for destroying an enemy vehicle.
- Vehicle crews receive the same +50 XP.
- Vehicle kills do not also receive the normal +5 infantry-element award.

VEHICLE CASUALTIES / CREW PRESSURE

- Destroyed vehicle: every active abstract crew member independently rolls the configured survival chance (50% default).
- Survivor: Wounded and loses 25% XP by default.
- Death: that crew member is replaced by a 0-XP replacement who becomes fully active next mission.
- Light/Medium defects can wound one active crew member; Heavy defects can kill/wound one active crew member.
- Human-crewed vehicles use the custom Vehicle Crew Pressure system described above; native MENACE vehicle suppression remains untouched.
- Default vehicle pressure received is 50% of the attack's equivalent infantry suppression before crew-rank scaling.
- Crew-average rank modifies pressure resistance with the seven-rank -6/-4/-2/0/+2/+4/+6% curve.
- Buttoned Up reduces AP and Accuracy; Break Contact attempts a real reverse retreat on the vehicle's next activation.

ALPHA.15 LOAD TRANSITION XP SAFETY

- Fixed XP being reset when loading a save from another campaign while the previous/new-campaign roster was still resident in memory.
- Loaded sidecar XP is now protected until MENACE finishes replacing/refreshing the Strategy roster for the selected save.
- Identity mismatch protection still exists for genuinely stale sidecars, but it no longer runs against the pre-load campaign roster.
- Added load-guard diagnostics for troubleshooting.

Squaddie Veterancy 1.1.0-alpha.15

ALPHA.14 RESCUE DETECTION FIX

- Fixed rescued-squaddie XP loss after missions that return to Mission Preparation rather than directly to Strategy.
- Mission Preparation now uses the refreshed post-mission roster to determine which tactical casualties were rescued.
- The configured rescue XP percentage is applied only to casualty IDs that reappear alive.

Squaddie Veterancy adds persistent experience, veterancy ranks, reliability and squad-level combat modifiers to MENACE infantry squads.

RESCUED SQUADDIE XP LOSS IN ALPHA.13

- Squaddies who go down in Tactical and are recovered by MENACE after the mission can now lose veterancy XP.
- Enabled by default with a 25% individual XP loss.
- Both the feature and loss percentage are configurable in Mod Options.
- Permanently lost squaddies receive no XP penalty.
- A rescue can naturally lower the individual squaddie's rank and the squad-average rank if the new average crosses a threshold.
- Save sidecar format is unchanged.

SALVO TARGETED-XP FIX IN ALPHA.12

- Real committed salvo attacks now count as incoming attacks for the +10 targeted XP rule.
- XP attack classification is separate from reliability filtering, so NPC reliability behavior is unchanged.
- Existing per-action dedup prevents multi-shot/parent-child skill dispatch from awarding duplicate XP.

INCOMING-FIRE XP FIX IN ALPHA.11

- Fixed +10 targeted XP for hostile attacks that use MENACE's override-target entity path instead of attaching the defender directly to the target tile.
- Target identity is cached during hitchance evaluation but XP is only granted when the native attack action is actually committed.
- Missed attacks still count, as intended, while AI target previews do not award XP.

PERSISTENCE HARDENING IN ALPHA.10

- Pending XP snapshots are flushed synchronously when the mod unloads/game shuts down.
- A shutdown timestamp scan catches MENACE save writes that may have missed or outpaced FileSystemWatcher delivery.
- Unsaved XP is intentionally not written into an older MENACE save.
- Save/load logs now include the exact expected/written sidecar path for easier diagnosis.

PLAYER VETERANCY

Individual squaddies keep persistent XP per MENACE save. The tactical HUD shows the rounded average veterancy rank of the squad.

Ranks:
- Green: 0-249 XP
- Combat Tested: 250-499 XP
- Hardened: 500-999 XP
- Regular: 1000-1999 XP
- Veteran: 2000-3999 XP
- Crack: 4000-7999 XP
- Elite: 8000+ XP

Default XP awards:
- Perform a squad attack: +1 XP
- Kill an enemy element: +5 XP
- Be directly targeted by an enemy attack: +10 XP
- Complete a primary objective: +25 XP
- Complete a secondary objective: +10 XP

There is no movement XP.

Combat XP is awarded to living persistent squaddies in the participating squad. Mission-objective XP is awarded to every deployed player squad after a successful mission. Objective completion is cached during the tactical mission so the payout survives MENACE clearing objective objects during mission finalization.

DEFAULT VETERANCY EFFECTS

Green:
- HP +0 per element
- Accuracy -6%
- Defense -6%
- Suppression resistance -6%
- Concealment -2
- 10% individual chance to fail to fire

Combat Tested:
- HP +1 per element
- Accuracy -4%
- Defense -4%
- Suppression resistance -4%
- Concealment -1
- 5% individual chance to fail to fire

Hardened:
- HP +1 per element
- Accuracy -2%
- Defense -2%
- Suppression resistance -2%
- Concealment -1
- 3% individual chance to fail to fire

Regular:
- HP +2 per element
- Accuracy 0%
- Defense 0%
- Suppression resistance 0%
- Concealment 0
- 2% individual chance to fail to fire

Veteran:
- HP +3 per element
- Accuracy +2%
- Defense +2%
- Suppression resistance +2%
- Concealment +1
- 0% individual chance to fail to fire

Crack:
- HP +3 per element
- Accuracy +4%
- Defense +4%
- Suppression resistance +4%
- Concealment +1
- 0% individual chance to fail to fire

Elite:
- HP +3 per element
- Accuracy +6%
- Defense +6%
- Suppression resistance +6%
- Concealment +1
- 0% individual chance to fail to fire

The percentage combat curve is intentionally symmetric around Regular: Green -6%, Combat Tested -4%, Hardened -2%, Regular 0%, Veteran +2%, Crack +4%, Elite +6%. HP and concealment remain capped at the Veteran values.

Suppressed squaddies receive an additional +5 percentage points to their no-fire chance by default. Pinned behavior is left to MENACE itself.

If every eligible squaddie would fail to fire, the mod retains one valid shooter so MENACE is never handed an invalid empty attack.

NPC VETERANCY

Human hostile NPC infantry and explicitly recognized allied NPC infantry receive temporary battle-local ranks. Human-crewed hostile vehicles receive one temporary crew/actor rank. None of this temporary AI state is persisted.

Detailed profiles are enabled by default. Resolution uses exact EntityTemplate ID first, then a template-family/faction fallback. The seven temporary ranks are Green / Combat Tested / Hardened / Regular / Veteran / Crack / Elite.

Examples of infantry distributions (percent):
- Pirate Outcasts: 75 / 20 / 5 / 0 / 0 / 0 / 0
- Pirate Scavengers: 55 / 30 / 12 / 3 / 0 / 0 / 0
- Pirate Veteran Scavengers: 2 / 8 / 20 / 30 / 25 / 12 / 3
- Pirate Boarding elite: 0 / 3 / 7 / 20 / 35 / 25 / 10
- Rogue Conscripts: 40 / 35 / 20 / 5 / 0 / 0 / 0
- Rogue line infantry: 10 / 25 / 30 / 25 / 8 / 2 / 0
- Rogue Officer: 0 / 2 / 8 / 20 / 35 / 25 / 10
- Rogue Storm Troopers: 0 / 0 / 5 / 15 / 30 / 35 / 15
- Civilian Militia: 60 / 25 / 10 / 5 / 0 / 0 / 0
- Local Forces infantry: 15 / 30 / 30 / 20 / 5 / 0 / 0

Enemy vehicle crew examples:
- Pirate Outcast trucks: 45 / 30 / 20 / 5 / 0 / 0 / 0
- Pirate Boarding Commando truck: 0 / 5 / 10 / 25 / 35 / 20 / 5
- Rogue laser walker: 5 / 20 / 35 / 30 / 8 / 2 / 0
- Rogue battle tank: 0 / 5 / 20 / 35 / 25 / 12 / 3
- Rogue heavy tank: 0 / 3 / 15 / 32 / 30 / 15 / 5
- Rogue mercenary medium walker: 0 / 2 / 10 / 28 / 35 / 20 / 5

Temporary AI rank affects Accuracy, Defense and Suppression Resistance using the same symmetric percentage curve as player infantry. Infantry also uses the individual no-fire reliability system. Enemy vehicles intentionally do not roll individual no-fire: their rank represents the vehicle crew as a whole.

The tactical marker for an NPC infantry squad represents the rounded average of its temporary element ranks. A human-crewed enemy vehicle displays its single temporary crew rank when the vehicle is visible.

Allied NPC veterancy remains independently configurable. Civilian workers, pilots, POWs and other non-combat mission NPCs stay excluded. Constructs and Wildlife are deliberately excluded from veterancy/rank markers/reliability while still counting as hostile targets for combat and player XP.


VETERANCY ROSTER

A player veterancy roster is available in both Tactical and Mission Planning.

- Ctrl+V opens or closes the roster in either scene.
- Tactical shows only squads deployed in the current battle.
- Mission Planning shows all living player squads when the Veterancy button or Ctrl+V is used.
- A small Veterancy button remains available when the roster is closed.
- Each squad shows average XP, squad rank, progress toward the next rank, and every squaddie with individual XP, rank and XP remaining to the next rank.
- The roster is draggable and scrollable.
- The roster can be disabled entirely in Mod Options.

HUD MARKERS

- Green: army-green service bar (#5D6532)
- Combat Tested: brass single chevron
- Hardened: silver single chevron
- Regular: silver double chevron
- Veteran: gold single chevron
- Crack: gold double chevron
- Elite: gold triple chevron

CONFIGURATION

The MENACE Mod Options menu exposes configurable XP rewards, HP modifiers, accuracy, defense, infantry suppression resistance, vehicle suppression baseline/indirect-fire behavior/rank scaling, concealment, reliability chances, the Suppressed reliability penalty, eligible hostile-human NPC reliability, allied NPC reliability, detailed NPC profiles, HUD markers and diagnostics.

COMPATIBILITY

Designed to coexist with:
- Coordinated Fire
- Overwatch
- Visibility Overhaul
- Squad Size Modifiers

Overwatch is optional and Squaddie Veterancy has no hard dependency on it.

Vehicle suppression is configured on each human-crewed vehicle's cloned runtime `EntityProperties`; MENACE then reads the resulting `Flags` and `SuppressionImpactMult` through its native suppression path. No `Actor.ApplySuppression` or suppression-UI hook is installed. Infantry suppression-resistance compatibility remains on the existing property-getter path. Concealment remains additive to the resolved MENACE value, allowing Visibility Overhaul to remain independent.

SAVE DATA

The mod does not modify MENACE save files directly. Persistent XP is stored in a sidecar under:

Menace/UserData/SquaddieProbe/Saves/

The internal SquaddieProbe data path and mod ID are intentionally retained for compatibility with development builds and existing sidecar data.

REQUIREMENTS

- MENACE
- Menace Modpack Loader
- MelonLoader as required by the Modpack Loader setup

Validated during development with:
- MENACE v0.7.14+20743
- Menace Modpack Loader v38.0.5
- MelonLoader v0.7.4-ci.2581

INSTALLATION

Extract the SquaddieVeterancy folder into the MENACE Mods folder using the standard Menace Modpack Loader installation layout.

VERSION 1.1.0-alpha.13

Rescued squaddie consequences
- Added optional individual XP loss when a downed squaddie is recovered after a mission.
- Default loss is 25%, configurable from 0-100%.
- Uses the real tactical casualty ID and a fresh post-mission Strategy roster to distinguish rescued squaddies from permanent losses.
- Pending rescue state is cleared on save load to prevent cross-timeline penalties.
- Existing persistence format, rank thresholds, NPC profiles and combat balance are unchanged.

VERSION 1.1.0-alpha.12

Roster leader identification
- Each squad block now explicitly shows its Squad Leader in both Mission Planning and Tactical rosters.
- Squad leaders remain separate from individual squaddie XP and veterancy ranks.

Roster readability
- All roster text now uses one neutral text color.
- Rank-colored text was removed; colored progress bars remain.
- The veterancy roster no longer opens automatically in Mission Preparation.
- Added the Veterancy launcher button to Tactical at the exact same top-right position used in Mission Preparation.
- The roster now opens only when the player clicks Veterancy or presses Ctrl+V in either scene.
- Removed the Mission Planning auto-open option; legacy saved values are intentionally ignored.

VERSION 1.1.0-alpha.6
- Reworked the Tactical and Mission Planning roster to use only IL2CPP-safe raw GUI calls.
- Removed all GUILayout/GUILayoutUtility and GUI.Window dependencies that can trigger Method unstripping failed in MENACE.
- Added manual mouse-wheel scrolling plus Up/Down controls.
- Mission Planning now shows only real assigned squads and no longer turns unassigned replacement squaddies into one-man pseudo-squads.
- Keeps close button, Ctrl+V toggle, reopen button and manual dragging.
- No veterancy balance changes.

VERSION 1.1.0-alpha.2

- Restricts temporary NPC veterancy to human combatants: Pirates, Rogue Army, hostile Local Forces and explicitly recognized allied NPC squads.
- Constructs and Wildlife are deliberately excluded from veterancy, reliability rolls and HUD rank markers while remaining hostile for combat and XP logic.
- Keeps faction + unit-specific profiles for Pirates, Rogue Army and allied NPC combatants.
- Keeps civilians and non-combat mission NPCs excluded.
- Keeps vehicles outside the individual squaddie model.

VERSION 1.1.0-alpha.1

- Added runtime EntityTemplate-based NPC classification and the first faction + unit NPC profile pass.

VERSION 1.0.0

Initial public release.

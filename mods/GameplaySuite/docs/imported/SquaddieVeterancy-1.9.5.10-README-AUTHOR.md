# Squaddie Veterancy 1.9.5.10 authoring package


## 1.9.5.10 HUD layout fix + quiet logging

This is a UI/stability patch only.

- Keeps the 1.9.5.8 hot-path logging suppression and `DebugLoggingV2` safeguards. Runtime verification dropped the test session from ~522k lines to ~1.3k lines, with zero repeated `JAM CHECK`, hitchance-apply, concealment-apply or XP-target-hint traces.
- Fixed the actual source of the floating HUD black rectangles: the veterancy marker was a normal flex child of MENACE's HP-bar host, so the host expanded and exposed its dark background.
- Veterancy markers are now absolute-positioned overlays and therefore cannot resize the native UnitHUD.
- Restored the compact drawn service-bar/chevron artwork instead of the temporary `^`/`^^` text glyphs. Regular/Crack again use proper double chevrons, Elite triple chevrons.
- No gameplay, XP, rank, reliability probabilities, Crew Pressure, save schema, or balance changes.

Build fingerprint: `SOURCE BUILD: 1.9.5.10-centered-absolute-sprite-rank-markers`.

## 1.9.5.6 Crew Pressure HUD ownership + Break Contact fallback

- Fixed the floating vehicle Crew Pressure bar being overwritten by MENACE after mission-start. The bar is now re-asserted from custom Crew Pressure on every existing HUD refresh (~0.2 s), while HUD logging remains change-only. A vehicle at 0 Crew Pressure therefore stays visually empty instead of drifting back to MENACE's native suppression value (observed as ~50%).
- Selected-unit panel behavior is unchanged; it was already refreshed continuously.
- Break Contact keeps chassis-appropriate native movement as its first choice: wheeled/tracked vehicles prefer `Backwards`, walkers prefer `Forward`.
- Added safe MENACE 0.7.15 pathfinding fallbacks when the preferred mode returns no valid distancing path. Conventional vehicles try `Backwards -> Auto -> Forward`; walkers try `Forward -> Auto`. Every candidate still uses `MovementSystem.GetPath`, must be affordable, and must increase distance from the remembered threat.
- No `Force`, `SkipPathfinding`, teleport, or direct reposition fallback is introduced.
- Native morale remains authoritative and separate: if MENACE recovers a vehicle from `Fleeing` to `Neutral`, Squaddie Veterancy does not latch or re-impose the old morale state.
- No XP, rank, Crew Pressure thresholds, AP/accuracy penalties, Rally amount, damage shock, or recovery balance changed.

Build fingerprint: `SOURCE BUILD: 1.9.5.6-vehicle-pressure-hud-path-fallback`.

## 1.9.5.5 MENACE 0.7.15 IReadOnlyCollection proxy fix

- 1.9.5.4 still failed at runtime because Il2CppInterop exposes `IReadOnlyList<T>` and inherited `IReadOnlyCollection<T>` as separate CLR proxy classes; `GetInterfaces()` therefore cannot discover `Count`.
- Collection counting now re-wraps the same IL2CPP native pointer as `Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>` and reads `Count` from that proxy. Item lookup remains on the `IReadOnlyList<T>` proxy.
- The bridge is generic and covers `Squaddies.GetAllAlive()`, `Roster.GetHiredLeaders()`, and `BaseUnitLeader.GetSquaddieIds()`.
- No XP/rank/balance/pressure/UI behavior changed.

- Fixed the actual 0.7.15 veterancy regression observed with Debug logging enabled: `Squaddies.GetAllAlive()` now returns an IL2CPP `IReadOnlyList<Squaddie>` whose `Count` member is inherited from `IReadOnlyCollection<T>` and is not exposed by the old direct reflection lookup.
- Collection reflection now searches inherited interfaces for `Count`/`get_Count` and `Item`/`get_Item`. The same adapter is used for `Roster.GetHiredLeaders()` and `BaseUnitLeader.GetSquaddieIds()`, which also return `IReadOnlyList` in 0.7.15.
- `Entity.CreateElement` hook resolution now prefers the current `(UnitLeaderTemplate, int, int, Vector2)` signature and keeps the former `(GameObject, int, int, Vector2)` signature as a compatibility fallback.
- Save-sidecar writes are suspended until the strategy identity cache has produced an authoritative roster snapshot. This prevents a future game API break from overwriting valid squaddie XP with an empty `squaddies=0` snapshot.
- `Squaddies.Kill` was removed by MENACE 0.7.15 (`SetKilled` exists instead); Squaddie Veterancy does not depend on that SDK event and continues to derive alive/dead state from the authoritative alive roster.
- No XP thresholds, rank modifiers, reliability, vehicle crew, Crew Pressure, Rally, retreat, or HUD balance changed.

Build fingerprint: `SOURCE BUILD: 1.9.5.5-il2cpp-readonlycollection-proxy-fix`.

## 1.9.5 Timed Crew Pressure status callouts

- `BUTTONED UP` and `BREAK CONTACT` full HUD callouts now remain visible for 5 seconds after entering or changing state, then disappear automatically.
- A compact persistent badge remains while the state is active: gold `B` for Buttoned Up, red left-arrow for Break Contact.
- Changing from Buttoned Up to Break Contact (or back after a retreat) restarts the 5-second callout. Rally/removal of the pressure state removes the badge when the vehicle drops below Buttoned Up.
- Gameplay logic, thresholds, Rally integration, walker locomotion and pathfinding are unchanged from 1.9.4.
- No new Harmony hook is added.

Build fingerprint: `SOURCE BUILD: 1.9.5-pressure-status-badges`.

## 1.9.4 Walker retreat animation, Rally integration, and Crew Pressure clarity

- **Walkers no longer use backward locomotion for Break Contact.** Runtime testing showed the actor moved correctly but walker legs did not animate when `MovementAction.Backwards` was forced. Walker EntityTemplate IDs are detected by the `walker` token and their retreat search now uses MENACE `MoveBackwardsMode.Forward`; they turn and walk toward the best reachable tile that increases distance from the remembered threat. Wheeled/tracked vehicles continue to use native Backwards paths. No teleport/force/skip-pathfinding fallback is used.
- **Command: Rally now affects Vehicle Crew Pressure.** The existing stable `TacticalEventHooks.OnSkillUsed` callback recognizes `perk.command_rally` before the attack-only XP filter. Rally removes 80 Crew Pressure by default (configurable), matching the native skill's `ChangeSuppression -80` intent. If pressure falls below the Break Contact threshold before retreat starts, the pending auto-retreat is cancelled. A retreat already in motion is allowed to finish.
- Break Contact finalization no longer raises pressure if Rally reduced it below the configured post-retreat value while movement was already in progress.
- The floating vehicle feedback explicitly says `CREW PRESSURE x/100`; positive pressure below Buttoned Up now also gets a small label. This makes the custom meter visibly distinct from MENACE native Morale and native Suppression.
- Pressure, damage-shock, recovery, Rally, and Break Contact logs now include native morale context where useful. The two systems remain mechanically separate except for Rally's explicit bridge.
- No new Harmony hook is added for Rally; 1.9.4 reuses the already-attached tactical skill event.

Build fingerprint: `SOURCE BUILD: 1.9.4-rally-walker-pressure-ui`.

## 1.9.3 Native reverse-path candidate fix

Runtime diagnostics from 1.9.2 showed `BREAK CONTACT SEARCH ... destinations=0`, so the failure occurred before MENACE reverse pathfinding was even attempted. The retreat scan was pre-filtering vehicle tiles with `Tile.IsValidMovementDestination()` and `Tile.CanBeEnteredBy(actor)`, which can reject every nearby tile for a vehicle.

1.9.3 removes those two vehicle destination pre-filters. Every in-bounds, valid tile in the configured retreat radius is now passed directly to `MovementSystem.GetPath(actor, tile, MoveBackwardsMode.Backwards, MovementFlags.None)`. MENACE's native pathfinder is therefore the authority on whether the vehicle can actually reverse to a tile. Successful paths are still required to be affordable and to increase distance from the recorded threat; no `Force`, `SkipPathfinding`, or teleport fallback is used.

The existing `VEHICLE BREAK CONTACT SEARCH` diagnostics remain in place. A healthy search should now show `destinations > 0`; `reversePaths`, `affordable`, and `distancing` then reveal exactly where any remaining rejection occurs. Vehicle Pressure, confirmed-damage shock, defect shock, Buttoned Up penalties, HUD labels, morale precedence, and post-retreat pressure are unchanged from 1.9.2.

Build fingerprint: `SOURCE BUILD: 1.9.3-native-reverse-path-candidates`.

## 1.9.2 Vehicle damage shock

Vehicle Crew Pressure now distinguishes between merely being under fire and a hit that actually damages the vehicle. MENACE's native `TacticalManager.InvokeOnDamageReceived(Entity, Entity, Skill, DamageInfo)` dispatcher is used to detect confirmed damage. A confirmed damaging hit adds a large configurable pressure shock (default **+40**) once per attack action. Defects add a second configurable shock on top: **Light +10**, **Medium +20**, **Heavy +35** by default. Crew-rank pressure resistance still scales these shocks. Destroyed vehicles are ignored.

This makes a penetrating/damaging hit much more likely to force `BUTTONED UP`, while a damaging hit plus a serious defect can drive the crew into `BREAK CONTACT`. New diagnostic lines use `VEHICLE PRESSURE SHOCK ... source=confirmed-damage` and `source=defect-*`.

Build fingerprint: `SOURCE BUILD: 1.9.2-damage-shock-pressure`.

## 1.9.1 Break Contact pathing + stronger pressure feedback

- Keeps the 1.9 custom vehicle crew pressure system and all existing combat hooks.
- Makes Break Contact retreat search less brittle: MENACE Backwards paths are evaluated first, then scored by actual distance gained from the last threat. Rearward alignment is now a preference rather than a hard pre-path filter, so diagonal/lateral reverse escape routes can qualify.
- Adds retreat-search diagnostics: destination candidates, reverse paths, affordable paths, distancing paths, chosen cost and distance gain.
- Adds a strong floating vehicle HUD state label while pressure is active:
  - `BUTTONED UP` with the configured AP and accuracy penalties.
  - `BREAK CONTACT` with `AUTO-RETREAT`.
- Cleans pressure labels from pooled/rebound or concealed enemy HUDs so the feedback cannot leak stale state or hidden units.

Build fingerprint: `SOURCE BUILD: 1.9.1-break-contact-pathing-hud`.

## 1.9 vehicle crew pressure / Buttoned Up / Break Contact

- Replaces the unsuccessful attempt to make MENACE vehicles accumulate native suppression with a custom **Vehicle Crew Pressure** system. Native vehicle suppression flags/state remain untouched.
- Pressure is awarded only from committed attacks, using MENACE weapon / attack-handler suppression values and skill repetitions as the source scale.
- Default vehicle receive factor is 50% of infantry suppression, then crew rank applies the familiar -6/-4/-2/0/+2/+4/+6 resistance curve.
- **Buttoned Up** starts at 50 pressure by default: -20 AP once per activation and -20% Accuracy while buttoned.
- **Break Contact** starts at 100 pressure by default: on the vehicle's next activation it searches for a valid destination away from the last threat and asks MENACE for a real `MoveBackwardsMode.Backwards` path. No teleport or forced invalid path is used.
- Successful Break Contact spends the rest of the activation and resets pressure to 60 by default. If no valid reverse path exists, the vehicle loses the activation and also falls back to the configured post-break pressure.
- Native Morale remains separate. If MENACE already has the vehicle in `Fleeing`, native morale behavior takes priority over Break Contact.
- The native suppression ProgressBar is reused only as a display for custom pressure. Concealed hostile vehicles do not get their pressure bar re-shown before reveal.
- Pressure recovers by 20 after a normal completed activation by default. Thresholds, penalties, recovery, post-break value, receive factor, reverse-search radius and rank modifiers are configurable.
- No new combat Harmony hook is introduced: the system reuses the existing committed-skill, hitchance, mission-start, NPC-rank and periodic HUD paths already exercised by earlier versions.

Build fingerprint: `SOURCE BUILD: 1.9-vehicle-pressure-break-contact`.

### First runtime test

1. Enter a Tactical mission with Petra or another human-crewed vehicle. The bar should appear empty and the log should contain `VEHICLE PRESSURE READY` / `VEHICLE PRESSURE HUD READY`.
2. Let hostile weapons target the vehicle. Each committed attack with non-zero MENACE suppression data should emit `VEHICLE PRESSURE APPLY`.
3. At the first threshold expect `VEHICLE BUTTONED UP`; on the vehicle activation expect `VEHICLE BUTTONED AP`. Accuracy is reduced through the existing hitchance mutation path.
4. At the second threshold expect `VEHICLE BREAK CONTACT ARMED`. On the next activation expect either `VEHICLE BREAK CONTACT START` followed by `... COMPLETE`, or `... FAILED reason=no-safe-reverse-path`.
5. If the vehicle is already native `Fleeing`, expect `VEHICLE BREAK CONTACT DEFERRED` and MENACE morale behavior to remain in control.

## 1.8.4 native suppression HUD refresh

- Keeps the stable 1.8.3 per-actor suppression property design and adds **no new Harmony hooks**.
- MENACE `Actor.SetSuppression()` already drives `UnitHUD.UpdateSuppression()` natively; dump/native disassembly confirmed this path.
- The missing vehicle bar was a UI initialization timing issue: `UnitHUD.InitSuppressionBar()` hid the bar while the vehicle still carried `ImmuneToSuppression`, and later property reconfiguration did not re-show it.
- The existing 0.2 s veterancy HUD refresh now restores the already-created native vehicle suppression bar with `DisplayStyle.Flex` and performs one zero-duration native value sync.
- The selected-unit panel bar is refreshed without patching `SelectedUnitPanel.SetActor`.
- Runtime diagnostics now log `VEHICLE SUPPRESSION HUD READY` and `VEHICLE SUPPRESSION STATE` when actual actor suppression changes.

Build fingerprint: `SOURCE BUILD: 1.8.4-native-suppression-hud-refresh`.


## 1.8.3 safe vehicle-suppression rework

- The 1.8/1.8.1 suppression bridge caused a native hard crash during Tactical loading. The 1.8.2 bisect loaded the same mission after restoring the stable 1.7.1 tactical hook path, isolating the crash to the new suppression bridge.
- 1.8.3 does **not** patch `Actor.ApplySuppression`, `UnitHUD.InitSuppressionBar`, `SelectedUnitPanel.SetActor`, or `TransientActor.GetCurrentProperties`. It adds no new Tactical lifecycle hook compared with the successful 1.8.2 baseline.
- Player vehicle suppression is configured from the already-existing stable `UnitActor.OnMissionStarted` callback. Hostile human-crewed vehicles are configured when the already-existing temporary vehicle-rank path assigns their rank.
- The mod clones the actor's fully resolved runtime `EntityProperties`, clears `ImmuneToSuppression`, optionally clears `ImmuneToIndirectSuppression`, writes the effective `SuppressionImpactMult`, and assigns that clone back to the actor. Shared `EntityTemplate` data is never modified.
- MENACE then handles suppression completely natively. Because the actor properties are already non-immune before normal HUD setup, the native suppression bar should be created without any UI patch.
- Default base vehicle suppression received is 50% of infantry. The crew-rank curve is multiplicative: Green ~53%, Combat Tested ~52%, Hardened ~51%, Regular 50%, Veteran ~49%, Crack ~48%, Elite ~47%. If a vehicle already has a lower native/external suppression-impact value, that lower value is preserved as the baseline.
- Direct suppression is enabled; indirect suppression is enabled by default and remains configurable. Constructs, Wildlife and non-human vehicle-shaped actors remain excluded.
- No XP thresholds, XP awards, crew persistence, casualty rules, AI rank distributions, save schema, squad-mapping guard, or infantry balance were changed.

Build fingerprint: `SOURCE BUILD: 1.8.3-safe-vehicle-suppression-properties-r2`.

## 1.8 vehicle crew suppression

- Human-crewed vehicles use MENACE's native suppression mechanics.
- Vehicle suppression is enabled by default with a **50% base suppression-impact multiplier relative to infantry**, so a vehicle is always substantially harder to suppress than infantry at the same rank.
- Crew veterancy uses the seven-rank resistance curve Green -6%, Combat Tested -4%, Hardened -2%, Regular 0%, Veteran +2%, Crack +4%, Elite +6%. Applied around the 50% vehicle baseline, this yields approximately 53 / 52 / 51 / 50 / 49 / 48 / 47% incoming suppression.
- Player vehicles use persistent active-crew rank. Hostile human-crewed vehicles use their battle-local vehicle rank.
- Mod Options expose the vehicle suppression toggle, base suppression percentage, indirect suppression toggle, rank scaling toggle, and seven per-rank modifiers.
- 1.8.3 implements this through per-actor cloned runtime properties rather than suppression-call or UI hooks.

## 1.7.1 squad-mapping guard

- Fixes the transient `squad=0` rank-resolution bug without changing XP balance or AI profiles.
- Tactical/MissionPreparation rank calculations now require a real positive squad mapping.
- The identity cache retains the last valid roster mapping for the current StrategyState and can recover it during transient tactical roster gaps.
- If no trustworthy mapping exists, squad combat modifiers/HUD rank are skipped instead of treating one squaddie's XP as the squad average.
- Squad XP awards and deployed-squad tracking use the same reliable mapping guard, preventing singleton fallback awards.
- Added `SQUAD MAPPING RECOVERED`, `SQUAD RANK SKIP`, and `SQUAD XP SKIP` diagnostics.
- No changes to rank thresholds, modifiers, AI distributions, vehicle crew progression, persistence schema, or save paths.


## 1.7 seven-rank ladder + temporary AI vehicle veterancy

- Adds **Hardened** between Combat Tested and Regular, giving one continuous doubling ladder: Green 0-249, Combat Tested 250-499, Hardened 500-999, Regular 1000-1999, Veteran 2000-3999, Crack 4000-7999, Elite 8000+.
- Default squad-average Accuracy, Defense and Suppression Resistance now form a symmetric two-point curve around Regular: **-6 / -4 / -2 / 0 / +2 / +4 / +6%**.
- HUD language is now: Green service bar, Combat Tested brass `^`, Hardened silver `^`, Regular silver `^^`, Veteran gold `^`, Crack gold `^^`, Elite gold `^^^`.
- Hardened defaults to +1 HP, -1 concealment and 3% no-fire; HP/concealment still cap at the Veteran values for Crack/Elite.
- Temporary human NPC infantry can now roll all seven ranks. Detailed faction + EntityTemplate profiles keep Pirates weighted toward lower ranks, Rogue Army toward trained middle/high ranks, and elite/command formations as the main source of Crack/Elite results.
- Human-crewed hostile vehicles now receive one **battle-local crew/actor rank** from vehicle-specific Pirate/Rogue profiles. Their rank affects Accuracy, Defense and Suppression Resistance and is shown on the tactical HUD when the vehicle is visible.
- Enemy vehicles deliberately do **not** use infantry no-fire filtering: their rank represents the crew as a whole, while no-fire remains an individual infantry reliability mechanic.
- Constructs and Wildlife remain excluded from veterancy/rank markers even when they are vehicle-shaped; they are still hostile for combat and XP logic.
- Persistent player infantry XP and persistent player vehicle-crew personnel require no migration; existing XP is simply re-ranked through the new thresholds.

Build fingerprint: `SOURCE BUILD: 1.7.1-squad-mapping-guard`.

## 1.6.1 extended-rank combat bonuses

- Veteran / Crack / Elite no longer share one capped combat modifier.
- Default squad-average **Accuracy, Defense, and Suppression Resistance** now scale symmetrically above Regular: Veteran **+2%**, Crack **+4%**, Elite **+6%**.
- This mirrors the negative side of the curve: Green **-6%**, Combat Tested **-3%**, Regular **0%**.
- HP and concealment remain capped at the Veteran values; only the percentage combat modifiers use the new +2/+4/+6 progression.
- New extended-rank Mod Settings keys are used so an existing saved Veteran +3% value from older builds cannot silently override the new defaults.
- Rank XP thresholds remain unchanged: 2000 / 4000 / 8000.

Build fingerprint: `SOURCE BUILD: 1.6.1-extended-rank-bonuses`.

## 1.6 extended veteran ranks

- Adds two long-term progression ranks above Veteran: **Crack** and **Elite**.
- Thresholds double from Veteran upward: Veteran 2000-3999 XP, Crack 4000-7999 XP, Elite 8000+ XP.
- HUD markers now use one/two/three gold upward chevrons for Veteran/Crack/Elite respectively.
- Green remains a service bar, Combat Tested a brass single chevron, and Regular a silver single chevron.
- Roster progress bars and XP-to-next-rank text continue through Crack and Elite.
- In 1.6 this initial implementation capped combat modifiers at Veteran; 1.6.1 supersedes that balance with +2/+4/+6 percentage modifiers for Veteran/Crack/Elite.
- Temporary NPC profile rolls still cap at Veteran; Crack/Elite are persistent XP ranks for player infantry and persistent vehicle crews.
- No stored XP migration is required; existing squads are re-ranked automatically from their current XP.

Build fingerprint: `SOURCE BUILD: 1.6-extended-ranks`.

## 1.5.7 persistent personnel pool

- Vehicle XP/status now belongs to persistent **crew people**, not to Light/Medium/Heavy role keys.
- Weapon loadout only determines how many crew are active and which jobs they are currently assigned.
- Changing weapon class with the same crew requirement keeps the same active people and all of their XP/status, then simply reassigns their jobs.
- If a smaller loadout is fitted, surplus people move to reserve with progression untouched.
- If a larger loadout is fitted, reserve personnel are brought back first; a new Green crewman is created only when the persistent pool is still too small.
- Example: Heavy (Driver + Heavy Gunner + 2 Heavy Loaders) -> Light + Medium keeps the same four people and reassigns them as Driver + Light Gunner + Medium Gunner + Medium Loader.
- Native MENACE Pilot/leader remains outside crew XP/casualty handling.
- Sidecar schema is v8 / crew-layout v4. Older role-keyed crew records migrate to stable personnel IDs without discarding XP/status.
- No XP award values, suppression scaling, casualty probabilities, rank thresholds, infantry persistence, or load guard behavior changed.

## 1.5.5 mounted-weapon crew classification

Vehicle veterancy now models only the abstract crew working under a native MENACE Pilot/leader. The leader (Bog, Petra, Murdock, etc.) is not a crew member, earns no veterancy XP from this system, and is never included in crew casualty rolls. Native leader attributes/perks remain MENACE's responsibility.

### Active crew formula

Only actually mounted modular weapons add active crew:

- Base crewman / driver: 1
- Mounted Light weapon: +1 gunner
- Mounted Medium weapon: +1 gunner +1 loader
- Mounted Heavy weapon: +1 gunner +2 loaders

The mod reads `Pilot.GetItems() -> ItemContainer.GetModularVehicle() -> Slots`, counts only slots with a real `MountedWeapon`, then classifies that weapon through `MountedWeapon.GetTemplate().SlotType`. Crew requirement therefore follows the weapon itself, not the larger chassis mount that may host it. Example: an MK20A3 Medium weapon installed in a Heavy mount still requires a Medium weapon crew.

Examples:

- base only: 1 active abstract crew member
- base + Light: 2
- base + Medium: 3
- base + Light + Medium: 4
- base + Heavy: 4

### Persistent crew pool

- Historical 1.5.5 note: crew demand became loadout-derived here; 1.5.7 supersedes the old role-keyed persistence model with persistent personnel.
- Only active crew earns XP, contributes to vehicle rank/suppression, or participates in defect/destruction casualty rolls.
- Rank is the average XP of active crew only.
- Roster shows active role names such as Driver / Crewman, Light Gunner, Medium Gunner, Medium Loader, Heavy Gunner, Heavy Loader 1/2 plus reserve count.

### Persistence migration

Historical note: 1.5.5/1.5.6 used role-keyed schemas. 1.5.7 migrates all of them to schema v8 personnel-pool persistence and preserves crew progression during reassignment.

### Existing 1.5.x behavior retained

- Incoming hostile attack: +2 XP per active crew member by default.
- Attack: +1; infantry element kill: +5; enemy vehicle destroyed: +50 total.
- Infantry also receives +50 for destroying an enemy vehicle.
- Vehicle destruction and Light/Medium/Heavy defect casualty logic now chooses active abstract crew only.
- Vehicle suppression uses active crew-average rank.
- Player vehicle veterancy HUD marker fix from 1.5.3 remains in place.
- alpha.15 cross-campaign load guard and 1.3 dedup-prune hardening are preserved.

Build fingerprint: `SOURCE BUILD: 1.5.7-personnel-pool`.

## 1.3 conservative dedup-prune safety

- Rebased directly on the verified 1.1.0-alpha.15 authoring source.
- No reliability, shooter-list, XP, rank, NPC-profile, save-format or combat behavior changes.
- `PruneXpDedup()` catches only `InvalidOperationException` from a collection changing during cleanup, deferring that cleanup pass so the rest of the frame update can continue.

## 1.1.0-alpha.15 load-transition XP safety

- Fixed a destructive load-order bug when loading another campaign/save while a different campaign roster is still resident in memory.
- `ExecLoad` now arms a roster reconciliation guard before the selected XP sidecar is loaded.
- While MENACE is replacing `StrategyState`, the mod preserves the loaded sidecar records and refuses to compare them against the pre-load campaign roster.
- Normal identity reconciliation resumes only after MENACE exposes a refreshed roster from a changed StrategyState or a roster whose identities match the loaded sidecar.
- Added `XP LOAD ROSTER GUARD armed/waiting/released` diagnostics.
- A 15-second safety timeout remains for genuinely stale/corrupt sidecars after multiple successful roster refreshes.
- No XP balance, rescue-loss percentage, rank threshold, reliability, or UI changes.

# Squaddie Veterancy 1.1.0-alpha.15 authoring package

## 1.1.0-alpha.14 rescued-squaddie MissionPreparation fix

- Fixed rescued-squaddie XP loss being skipped after completed missions that return from `Tactical` to `MissionPreparation` instead of `Strategy`.
- `Strategy` and `MissionPreparation` are now both treated as valid post-mission roster scenes.
- Rescue tracking is no longer discarded on the normal `Tactical -> MissionPreparation` transition.
- Added the destination scene to the `RESCUE XP pending` diagnostic log.

## 1.1.0-alpha.13 rescued-squaddie XP loss

- Added optional XP loss for squaddies who go down in Tactical and are recovered by MENACE after the mission.
- Enabled by default at 25% of that individual squaddie's current XP.
- Added `RescueXpLoss` toggle and `RescueXpLossPct` 0-100% setting.
- Casualties are tracked from the real player `Element` death, then resolved only after a fresh Strategy roster refresh.
- A casualty that reappears alive is treated as rescued and loses the configured percentage.
- Permanently lost casualties are not penalized.
- Loading another save clears pending casualty tracking so rescue penalties cannot leak across save timelines.
- The XP sidecar format is unchanged.

## 1.1.0-alpha.12 salvo targeted-XP fix

- Fixed committed salvo attacks such as `active.pirate_infantry_chaingun_salvo` being excluded from XP attack detection.
- XP attack classification is now separate from reliability attack classification.
- Reliability still ignores salvo/wrapper skills exactly as before.
- Existing actor+target action dedup prevents parent/child or multi-projectile dispatch from awarding XP more than once.
- No persistence, balance, roster UI, rank threshold or NPC profile changes.

## 1.1.0-alpha.11 incoming-fire XP fix

- Fixed +10 XP not being awarded when a player squad is directly targeted by hostile attacks that use MENACE's `_overrideTargetEntity` path.
- `Skill.GetHitchance` now records a short-lived hostile attacker + target-tile hint for the actual player defender.
- XP is still awarded only from the committed native `OnSkillUsed` event, never from AI target evaluation or hitchance previews.
- The existing `targetTile.GetEntity()` path remains the fast path; the new hint is only a fallback.
- Added `XP TARGET RESOLVED via=...` diagnostics for verification.
- No balance, rank threshold, persistence format, roster UI or NPC veterancy changes.

## 1.1.0-alpha.10 persistence hardening

- Added `IModpackPlugin.OnUnload()` shutdown handling.
- Pending debounced MENACE save events are synchronously flushed before plugin unload.
- Added a shutdown disk-timestamp scan to catch save writes whose FileSystemWatcher event was delayed or missed.
- Shutdown deliberately does not attach merely-unsaved XP to the previously loaded save.
- Missing snapshot diagnostics now log the exact expected sidecar path.
- Snapshot-write diagnostics now include the sidecar path and shutdown reason.

## 1.1.0-alpha.9 roster leader change

- Mission Planning and Tactical veterancy rosters now show the squad leader explicitly in each squad block.
- Leader identity is resolved dynamically from the strategy roster with several safe reflection fallbacks; a `SQUAD LEADER` diagnostic line is emitted when squad mapping refreshes.
- Squad leaders remain separate from squaddie XP/ranks.


## 1.1.0-alpha.8 readability change

- All roster text now uses the same neutral/default text color for readability.
- Rank names are no longer green/brass/silver/gold in the roster.
- Rank-colored progress bars remain as non-text visual cues.

Runtime UI compatibility release: the veterancy roster now uses only raw `GUI.*` calls with explicit rectangles. MENACE Modkit documents `GUILayout.*` as unavailable under IL2CPP method unstripping, so all `GUILayout`, `GUILayoutUtility`, `GUI.Window`, group, area, and layout-scroll dependencies have been removed. Scrolling is implemented manually.


## 1.1.0-alpha.7 UX change

- MissionPreparation no longer auto-opens the veterancy roster.
- Tactical and MissionPreparation both show the same `Veterancy [Ctrl+V]` launcher at the same top-right position.
- The roster opens only on button click or Ctrl+V.
- The old `OpenPlanningRosterByDefault` setting is no longer registered or read, so stale saved values cannot force the roster open.

## 1.1.0-alpha.6 hotfix

- Restored the missing `HandleRosterDrag()` implementation referenced by the raw-GUI roster.
- Keeps the IL2CPP-safe `GUI.*`-only roster introduced in alpha.5.
- No veterancy, XP, reliability, or roster-data behavior changes.

## Tactical and Mission Planning veterancy roster

This build adds the first player-facing roster UI without modifying MENACE native Mission Preparation cards.

- Ctrl+V toggles the roster in Tactical and MissionPreparation.
- Tactical lists only squads confirmed as deployed through the existing player element spawn tracking.
- MissionPreparation lists all real assigned player squads from the existing StrategyState roster/squad mapping; unassigned replacement squaddies are not shown as pseudo-squads.
- MissionPreparation and Tactical both start with the roster closed; use the identical top-right Veterancy button or Ctrl+V to open it.
- The same `VeterancySquadView` model feeds both views.
- Each squad shows average XP, average rank, rank progress, and individual squaddie XP/rank/progress.
- No changes to XP awards, combat modifiers, NPC profiles, reliability, save sidecars, or native squad cards.

Focused runtime test:
1. Build/deploy and enter Mission Preparation. Confirm the roster starts closed and the top-right Veterancy button is visible.
2. Close it and confirm the top-right Veterancy button reopens it. Ctrl+V should toggle it as well.
3. Enter Tactical. Confirm the same Veterancy button appears in the same top-right position. Open it by button or Ctrl+V and confirm only deployed squads are listed.
4. Gain XP during the battle and reopen/refresh the roster; values should reflect the current in-memory XP immediately.
5. Confirm mouse-wheel scrolling and the Up/Down page controls work when the roster exceeds the visible area, and that the window can be dragged without affecting gameplay logic.


## Human faction + unit NPC veterancy build

The template-ID diagnostic succeeded on MENACE v0.7.14+20743: live tactical actors resolve canonical `EntityTemplate.m_ID` values through `Entity.GetTemplate()`. This build turns that validated path into the detailed human NPC veterancy pass. Constructs and Wildlife are intentionally outside the veterancy system.

Changes:
- Eligible hostile human NPC infantry resolves veterancy by exact `EntityTemplate` first, then template family, then faction fallback.
- Pirate profiles distinguish Outcasts, Scavengers, weapon teams, specialists, Saboteurs, Veteran Scavengers, Boarding Commandos and command units.
- Rogue Army profiles distinguish Conscripts, line infantry, weapon teams, advanced weapon teams, Heavy Infantry, Officers, Jaegers and Storm Troopers.
- Allied NPC combatants now receive battle-local individual ranks and reliability.
- Civilian workers, pilots, POWs and police-chief mission NPCs remain excluded.
- Vehicles remain excluded from individual squaddie veterancy because they are single-element tactical entities.
- Constructs and Wildlife receive no veterancy ranks, reliability rolls or HUD rank markers. They remain hostile for combat and XP logic.
- New Mod Options: `DetailedNpcProfiles` and `AlliedNpcReliability`.
- Existing player XP/save sidecars and player combat modifiers are unchanged.

See `research/NPC-Veterancy-Profiles.md` for the full distribution matrix.

Focused runtime test:
1. Run `Build-and-Deploy-Dev.cmd`.
2. Start any Pirate mission.
3. Confirm `ENEMY OWNER DETECTED` lines include a named profile such as `pirate.outcasts:85/13/2/0`, `pirate.scavengers:70/25/5/0`, or `pirate.weapon_team:55/30/12/3`.
4. Confirm `NPC RANK ASSIGN relation=hostile ... profile=<named profile>`.
5. If the mission contains temporary allied infantry, confirm `ALLIED NPC OWNER DETECTED` and `NPC RANK ASSIGN relation=allied`.
6. Confirm `civilian.worker` remains outside NPC veterancy.
7. If Constructs or Wildlife appear in any later test, confirm they receive no `NPC RANK ASSIGN` and no veterancy HUD marker.
8. Confirm no `RELIABILITY LEAK`, crash, or player-XP regression.

Public release name: **Squaddie Veterancy**.

The internal assembly/namespace, stable mod ID (`luca.squaddieprobe`), ModSettings group, and `UserData/SquaddieProbe` sidecar path intentionally remain `SquaddieProbe` for compatibility with existing saves and settings.

## 1.0.0 release baseline (runtime-validated from alpha.10.17.2)


- Fixes mission-objective XP not being paid because MENACE clears `ObjectiveManager` before/inside `TacticalManager.Finish()`.
- Completed objectives are now captured from `TacticalEventHooks.OnObjectiveStateChanged` while their runtime objects are still alive.
- `ObjectiveState == 2` is cached as completed; failed objectives (`state == 3`) are removed from the completion cache.
- Primary/Secondary classification is retained at completion time from MENACE's real runtime `m_Type` semantics. The tested build reported `Primary` and `Secondary` directly, so mission-end XP no longer needs an objective-list ordering fallback.
- Completion records are deduplicated by mission-local objective pointer.
- Mission-end payout still requires at least one completed primary objective, preventing secondary-only retreat/failure farming.
- The cache is cleared on scene/save changes, so objective completion cannot leak between missions.
- No balance changes from alpha.10.17.

Suggested 10.17.2 runtime check
1. Complete one primary and one or more secondary objectives.
2. Look for `OBJECTIVE XP CAPTURE ... kind=Primary/Secondary ... completed=1`.
3. At mission end verify `OBJECTIVE XP RESULT ... source=cached-state`.
4. Verify one `MISSION OBJECTIVE XP primary=... secondary=... total=...` line and one `source=mission-objectives` squad award for every deployed player squad.
5. Confirm no `MISSION OBJECTIVE XP no objectives` line appears.

## alpha.10.17.1 compile fix

Fixes the 10.17 C# definite-assignment error in mission objective classification (`source` could be considered unassigned by the compiler). No gameplay or balance changes from 10.17.

## alpha.10.17 squad XP economy + military Green marker

- Removes movement XP entirely.
- Replaces per-element firing XP with one squad-level award per real attack action: +1 XP by default.
- Adds +5 XP for each hostile element killed by the squad; attribution uses MENACE `InvokeOnElementDeath` attacker Entity, so squad weapons and squad-leader/special weapons feed the same squad.
- Adds +10 XP when a player squad is directly targeted by a hostile attack, awarded once per attack action rather than per projectile/hit.
- Adds mission-end objective XP to every deployed player squad: +25 per completed primary and +10 per completed secondary objective. A completed primary is required before any mission-objective XP is paid.
- Objective role detection uses MENACE runtime Primary/Secondary semantics; the 10.17.2 finalization fix caches completed objective roles before MENACE clears the objective list.
- XP is applied equally to the living persistent squaddie IDs in the squad, preserving the existing per-squaddie sidecar and transfer/death behavior while making progression squad-driven.
- Rank requirements are multiplied x10: Combat Tested 250, Regular 1000, Veteran 2000. Existing stored XP is not silently migrated or multiplied.
- Green HUD bar remains enabled and is recolored to army green `#5D6532` (`0.365, 0.396, 0.196`).
- All five XP awards have Mod Options sliders and can individually be set to zero.
- No hard dependency on Overwatch, Visibility Overhaul, Coordinated Fire, or Squad Size Modifiers.

## alpha.10.16 squad suppression resistance + concealment

- Adds player squad-average suppression resistance modifiers: Green -6%, Combat Tested -3%, Regular 0%, Veteran +3% by default.
- Historical alpha.10.16 implementation adjusted `EntityProperties.GetPropertyValue`. Engine research for 1.8 showed native `Actor.ApplySuppression` reads `SuppressionImpactMult` directly, so 1.8 supersedes this path by scaling the actual native suppression input while retaining the property postfix for secondary consumers. No hard dependency on Squad Size Modifiers.
- Adds player squad-average concealment modifiers: Green -2, Combat Tested -1, Regular 0, Veteran +1 by default. Values are additive concealment points/tiles, not percentages.
- Concealment is applied in a `GetConcealment` postfix at `Priority.Last`, so an installed Squad Size Modifiers concealment adjustment remains intact and veterancy is applied afterward.
- Both features have independent toggles and per-rank settings.
- Property-owner mapping is learned from player `GetCurrentProperties`/`GetProperties` calls and is cleared on scene/save changes. Enemy temporary ranks are not given these squad-average stat modifiers yet.

Rogue Army veterancy profile in alpha.10.15

- Rogue Army now uses a dedicated temporary per-element rank distribution: 20% Green, 30% Combat Tested, 30% Regular, 20% Veteran.
- Other currently-hostile factions keep the existing generic 70% Green, 25% Combat Tested, 5% Regular, 0% Veteran profile.
- Squad HUD rank is still the rounded average of its element ranks; this change does not assign a rank directly to the whole squad.
- Enemy XP remains battle-local only; no enemy rank data is persisted to the save sidecar.
- Profile resolution is centralized so the next refinement can branch by faction plus unit/template type without changing reliability/HUD consumers.
- Expected logs: Rogue Army `temporaryProfile=20/30/30/20` and `ENEMY RANK ASSIGN ... profile=20/30/30/20`.
- No changes to player XP, HP, Accuracy, Defense, no-fire percentages, marker visuals, or Overwatch/Coordinated Fire compatibility.

Green marker initialization fix in alpha.10.14.4

- Fixes a UI initialization bug introduced in 10.14.3: a newly-created marker started with Rank=Green, so a truly Green squad skipped the first sprite assignment and appeared to have no marker.
- Fresh/re-attached veterancy markers now always receive one visual refresh, so Green gets its horizontal bar immediately.
- No changes to veterancy rolls, enemy profiles, XP, reliability, HP, Accuracy, Defense, colors, marker size, or enemy visibility rules.
- Diagnostic expectation: visible Green player/enemy squads now log `VETERANCY HUD marker ... rank=Green glyph=bar`.


Green HUD baseline marker introduced in alpha.10.14.3
------------------------------------------
- Green squads now show a short bright-green horizontal service bar below the HP bar.
- Combat Tested remains a brass single chevron, Regular a silver single chevron, Veteran a gold double chevron.
- The Green bar is intentionally useful as a diagnostic: if an enemy is visible but no Green bar appears, the problem is HUD attachment/visibility rather than rank selection.
- No veterancy balance, XP, HP, Accuracy, Defense, or reliability values changed.

Enemy HUD lifecycle fix introduced in alpha.10.14.2
- Rogue Army rank assignment was already correct, but non-Green chevrons could be attached to a concealed/pooled enemy UnitHUD before reveal and then silently detached when MENACE rebuilt the HUD.
- Hostile veterancy markers are now created only while the actor/model is actually visible to the player, matching the existing Overwatch anti-information-leak behavior.
- Existing markers are validated against the current hitpoint-bar host every refresh; if MENACE reuses the same UnitHUD pointer with a rebuilt hierarchy, the marker is recreated and logs VETERANCY HUD reattach.
- No changes to XP, rank distribution, reliability, HP, accuracy, defense, colors, glyphs, or player marker layout.

Suggested focused test
1. Enter a mission with Rogue Army initially concealed.
2. Reveal several Rogue Army squads.
3. Any squad whose temporary average rank is Combat Tested or higher should show the same chevron semantics as player squads.
4. The marker must never reveal a concealed enemy.
5. Look for VETERANCY HUD marker side=enemy after reveal; VETERANCY HUD reattach is expected if MENACE rebuilt a pooled HUD.

Visual hotfix retained from alpha.10.14.1
- Enlarged veterancy chevrons from 12x11 to 18x15 UI pixels.
- Centered the insignia under the native HP bar instead of left-aligning it.
- Increased chevron steepness/thickness and strengthened the near-black outline.
- Raised brass/silver/gold luminance so ranks remain visible on bright desert terrain and when UnitHUD opacity is reduced.
- No changes to XP, ranks, reliability, HP, Accuracy or Defense logic.


Veterancy marker design retained from alpha.10.14.1
- Adds persistent tactical veterancy feedback to MENACE native UnitHUDs, independent of Overwatch.
- Green: no marker.
- Combat Tested: brass single upward chevron (︿ style).
- Regular: silver single upward chevron (︿ style).
- Veteran: gold double upward chevron (︽ style).
- Icons are drawn procedurally into sprites instead of relying on Unicode font glyph coverage.
- Player marker rank is the existing squad-average XP rank.
- Enemy marker rank is the rounded average of battle-local temporary element ranks; it is only shown while EnemyReliability is enabled.
- The veterancy marker uses its own row next to the native HP bar, so it can coexist with Overwatch's reticle row.
- Added ShowVeterancyMarkers Mod Option, enabled by default.
- Existing HP, Accuracy, Defense, reliability, save sidecar, Coordinated Fire and optional Overwatch compatibility logic are unchanged.

Suggested alpha.10.15 test
1. Enter a tactical mission containing Rogue Army infantry.
2. Reveal several Rogue Army squads and confirm all four HUD semantics remain valid: Green bar, brass Combat Tested chevron, silver Regular chevron, gold double Veteran chevron.
3. Check Latest.log for `ENEMY OWNER DETECTED faction=Rogue Army ... temporaryProfile=20/30/30/20`.
4. Check several `ENEMY RANK ASSIGN faction=Rogue Army` lines and confirm `profile=20/30/30/20`.
5. Generic hostile factions should continue logging `profile=70/25/5/0`.
6. Confirm hidden enemies never leak a veterancy marker before reveal.
7. No other gameplay regression test is required specifically for this profile-only change.

Previous alpha.10.13.1 notes

Focus of alpha.10.13.2.1

Compile fix in alpha.10.13.2.1
- MENACE 0.7.14's generated HitChanceResult wrapper does not expose the reconstructed Accuracy/FinalValue/AlwaysHits fields used by alpha.10.13.
- Accuracy/Defense modifiers now temporarily adjust the EntityProperties arguments passed into Skill.GetHitchance and restore them immediately in the postfix.
- Member access is resolved reflectively (AccuracyMult / DefenseMult and common IL2CPP backing-field names), avoiding compile-time dependence on reconstructed HitChanceResult members.

- Moves on from enemy reliability to the first broader veterancy gameplay pass.
- Keeps the alpha.10.12 late-commit enemy reliability fix unchanged and treats it as pending general regression verification.
- Reworks the experimental HP bonus at the correct spawn lifecycle point: Entity.CreateElement(prefab, squaddieId, hitpoints, scaleRange).
- Player squaddie elements are identified only when the parent is a UnitActor and the squaddieId belongs to the persistent player roster. Enemy element IDs therefore cannot alias a player squaddie for HP purposes.
- The incoming CreateElement hitpoints value is modified before Element initialization. A postfix records the vanilla base, applied bonus, owner and final element so later squad-rank changes can still reapply HP safely.
- Removes the old SetHitpointsMax capture hook from the active hook set. The previous tests showed that route ran at the wrong point in initialization.
- Adds squad-average Accuracy and Defense modifiers centered on Regular.
- Accuracy/Defense are applied in a Skill.GetHitchance postfix, not by permanently mutating EntityProperties. This avoids stacking or contaminating MENACE perk/equipment/status calculations.
- Adds a Suppressed-only no-fire penalty. Pinned is deliberately ignored because MENACE already prevents normal firing while pinned.
- All new gameplay values are configurable in Mod Options.

Default squad-average profile
- Green: HP +0, Accuracy -6%, Defense -6%
- Combat Tested: HP +1, Accuracy -3%, Defense -3%
- Regular: HP +2, Accuracy 0%, Defense 0%
- Veteran: HP +3, Accuracy +3%, Defense +3%

Default individual reliability
- Green: 10% no-fire
- Combat Tested: 5%
- Regular: 2%
- Veteran: 0%
- Suppressed: +5 percentage points to the rank chance
- Pinned: no Squaddie Veterancy modifier

Enemy profile remains unchanged
- Green: 70%
- Combat Tested: 25%
- Regular: 5%
- Veteran: 0%
- Enemy ranks remain tactical-only and never gain persistent XP.

Important implementation notes
1. HP
   - Hook target expected in MENACE 0.7.14: Entity.CreateElement(GameObject, int squaddieId, int hitpoints, Vector2 scaleRange).
   - Expected startup hook line includes Entity.CreateElement.
   - When a non-Green squad spawns, expect HP SPAWN APPLY followed by HP SPAWN RESULT.
   - HP SPAWN RESULT prints base, bonus, expected max and actual current/max HP, so this test finally tells us whether CreateElement is the correct live HP path.

2. Accuracy / Defense
   - Hook target expected: Skill.GetHitchance with seven arguments.
   - The postfix adjusts HitChanceResult only for player squads, based on their current squad-average rank.
   - Accuracy is a relative modifier to the computed accuracy component.
   - Defense adjusts MENACE's already-flipped defense multiplier: positive Defense lowers incoming hit chance, negative Defense raises it.
   - One-shot logs: SQUAD ACCURACY MOD and SQUAD DEFENSE MOD.
   - With DebugLoggingV2 enabled, HITCHANCE VETERANCY shows the before/after calculation.

3. Suppressed reliability
   - Uses Actor.GetSuppressionState through the native wrapper/reflection path.
   - Only the Suppressed state receives the configured extra no-fire percentage.
   - Pinned / PinnedDown is explicitly not treated as Suppressed.
   - One-shot log: RELIABILITY SUPPRESSION MOD owner=... state=Suppressed penalty=+N%.
   - Individual RELIABILITY ROLL lines now include baseChance, suppressed, suppressionPenalty and final chance.

Suggested alpha.10.13.2.1 test
1. Build/deploy normally and confirm there are no HOOK FAILED lines for CreateElement or GetHitchance.
2. Use one Green squad first. Its hit chance should be worse than vanilla by the configured Accuracy/Defense profile; HP should remain unchanged at +0.
3. Temporarily force/prepare a squad average into Combat Tested or higher so HP is non-zero. On mission spawn inspect HP SPAWN APPLY / HP SPAWN RESULT and confirm the actual max HP matches expectedMax.
4. Compare displayed hit chance with Accuracy/Defense toggles ON vs OFF. Regular should be neutral; Green should be worse; Veteran should be better.
5. Get a squad Suppressed but not Pinned. Confirm RELIABILITY SUPPRESSION MOD and that RELIABILITY ROLL shows the extra configured penalty.
6. Let the squad become Pinned if convenient and confirm Squaddie Veterancy does not add a Pinned reliability modifier.
7. Also let at least one enemy squad fire as a regression check for alpha.10.12 late-commit / failure notice behavior.
8. Confirm no RELIABILITY LEAK, AccessViolation, or persistent enemy XP.

Save behavior
- Player XP sidecars remain under MENACE/UserData/SquaddieProbe/Saves/.
- No save schema change in alpha.10.13.2.1.
- Accuracy, Defense and Suppressed modifiers are derived at runtime from current settings and squad XP; nothing new is persisted.

Build
Run Build-and-Deploy-Dev.cmd. It expects MENACE at M:\SteamLibrary\steamapps\common\Menace by default.

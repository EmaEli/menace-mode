## 0.5.6

- Fix for a tactical crash (`0xc0000005` / `ExecutionEngineException` inside the .NET runtime's JIT, dump `Menace.exe.24232.dmp`): MENACE evaluates AI criteria on IL2CPP thread-pool threads, and a Harmony hook of this mod being compiled lazily on such a thread crashed the process. Hook bodies are now compiled up front on the main thread, and every hook falls through to vanilla behaviour when invoked off the main thread. AI Assault is unaffected: it is decided in `Agent.Execute` on the main thread.
- No gameplay, AP, or logging-default changes.

## 0.5.5

- The `Assault` button no longer shows the vehicle Charge icon inherited from the `player.close_combat_charge` clone source; it now uses the vanilla running-figure skill sprites `skill_32` / `skill_32_inactive`, assigned at runtime by sprite name in both the legacy and the Jiangyu build.
- No gameplay, AI, AP, or logging-default changes.

## 0.5.4

- MENACE v0.7.16 compatibility: the game removed `Skill.GetDescription()`, which made the Harmony binder throw and abandon every Assault patch (symptom: `Failed to apply patches: System.MissingMethodException: Harmony target method was not resolved.`). The `GetDescription` postfix is removed; the Assault tooltip is served by the manifest-defined `SkillTemplate.Description`.
- The binder now reports the handler name of an unresolved target.
- No gameplay, AI, AP, or logging-default changes.

## 0.5.3

- MEN-005: repair stale cached native executing-element indices before Infantry Assault mouse-hover aiming, preventing `Actor.AimAt(Vector3)` out-of-range spam after squad casualties.
- Rebuild indices through MENACE `Skill.GetExecutingElements` only after a changed squad size or invalid cached index; keep the same native cached list instance and preserve normal preview/cursor behavior.
- When safe validation is unavailable, skip only that Assault hover-preview callback; never suppress native exceptions globally or touch other skills, Mount Up, CQB balance, AI, AP, or ammo.
- One optional debug-only warning per tactical scene on guard failure; default diagnostics remain OFF. Windows compilation and in-game verification pending.

## 0.5.2

- Fixed runtime Assault skill discovery through `BaseSkill` wrappers, avoiding false missing-skill diagnostics and duplicate injection attempts.
- Cache recognized native skill pointers rather than managed wrapper hash codes; re-check both live and queued skills after native inherent-skill refresh failures.
- No combat-rule, AI-policy, AP-cost or normal logging changes.

## 0.5.1

- Fixed a stale player Assault `SkillAction` that could continue receiving `HandleMouseMoveOnTile` after a CQB volley changed the attacking squad's element count.
- The committed Assault targeting action is now guarded from native mouse-move aiming, preventing repeated `Actor.AimAt` `ArgumentOutOfRangeException` errors after a failed/repulsed assault with casualties.
- The existing immediate and deferred TacticalState cleanup remains in place as a second safety layer.
- No AP cost, CQB accuracy, cover, suppression, AI selection, ammo, or victory-advance behavior changed. No new routine logging was added.

## 0.5

- Assault can no longer be initiated by infantry that are deployed/kneeling, Suppressed, or Pinned Down. The restriction applies to player usability, targeting/range validation, direct skill execution, and AI assault selection.
- Preserved 0.4.2 defensive-fire behavior: Suppressed defenders cannot react; Pinned Down defenders can react with half CQB accuracy and half cover-reduction bonus.
- Preserved 0.4.1 deferred targeting cleanup and the symmetric CQB lethality introduced in 0.4.
- No change to the flat 40 AP cost, weapon eligibility, victory advance, or default logging.

## 0.4.2

- Suppressed defenders remain unable to perform defensive fire.
- Pinned Down defenders can now perform defensive fire with half CQB bonuses.
- The Pinned Down penalty halves both the configured CQB accuracy bonus and the CQB cover-reduction advantage.
- Ready defenders and assaulters retain full configured CQB bonuses.
- No AP, weapon eligibility, AI gate, targeting-cleanup, or logging behavior changed.

## 0.4.1

- Suppressed and Pinned Down defenders can no longer perform Assault defensive fire.
- Added deferred post-click targeting cleanup so an Assault that causes squad casualties cannot leave a stale `SkillAction` behind and spam `Actor.AimAt` / `HandleMouseMoveOnTile` index errors.
- No CQB accuracy, cover, AP, ammo, AI assault-selection or victory-advance balance values changed from 0.4.

## 0.4

- Defensive fire now receives the same configurable CQB hit-chance bonus as the assaulter.
- The configured Assault Cover percentage now applies symmetrically: both the attacker's starting cover during defensive fire and the defender's cover during assault fire are reduced by the same rule.
- At a user setting of +50 percentage points, both sides receive +50pp during their respective CQB volley.
- Assault weapon eligibility, 40 AP cost, AI suppression gate, ammo consumption, and victory advance rules are unchanged.
- Debug logging remains OFF by default.

## 0.3.2

- Added Debug-only `[ASSAULT_DEBUG]` defensive-fire diagnostics.
- Logs defender/attacker/target tiles, tile occupant, resolved target identity, HP, armor durability, suppression and morale before/after the native reaction shot.
- No gameplay behavior changed; Debug logging remains OFF by default.

# Changelog

## 0.3.1

- Fix loaded-save/runtime Assault synchronization by preferring MENACE's native inherent-skill refresh path.
- Recognize skills queued for addition so synchronization does not create duplicates.
- No Assault balance changes.

## 0.3

- AI now considers Assault only against adjacent infantry whose native MENACE `SuppressionState` is `Suppressed` or `PinnedDown`.
- Unsuppressed targets fall through to normal MENACE AI behavior.
- Player-controlled Assault targeting is unchanged.
- No changes to the 40 AP cost, defensive fire, CQB bonus, cover reduction, victory advance, or weapon eligibility rules.

## 0.2.4

- Restricted Assault/defensive-fire weapon selection to direct-fire small arms.
- Excluded tripod/AT/ATGM/cannon/autocannon/crew-served MG/launcher/mortar/suppressive heavy-weapon modes from CQB.
- Fixed misleading runtime synchronization warnings after a successful skill add when only the optional UI refresh failed.

## 0.2.3

- Fixed defensive-fire weapon discovery for transient/dev-spawned infantry by merging native `GetAllAttacks()` with genuine attack skills from the actor `SkillContainer`; item-backed small arms remain preferred.
- Added configurable CQB hit-chance bonus for the assaulting squad, default +20 percentage points.
- CQB accuracy is applied only during the assault shot and restored afterward, including coroutine cleanup paths.
- Debug logging remains OFF by default.

## 0.2.2

- Fixed unified-repository compilation by importing `System.Runtime.CompilerServices` for `RuntimeHelpers.GetHashCode`.
- No gameplay, balance, AI, targeting, or logging changes from 0.2.1.

## 0.2.1

- Fixed stale `SkillAction` state after player Assault target confirmation by clearing the intercepted action from `TacticalState` after local aim cleanup.
- Fix targets the repeated `SkillAction.HandleMouseMoveOnTile` null-reference exceptions seen immediately after an Assault in 0.2.
- Restricted runtime fallback Assault-skill synchronization to infantry templates that actually declare `mod.infantry_assault`, preventing injection attempts on xeno/pseudo-infantry actors.
- No assault balance changes. Debug logging remains OFF by default.

## 0.2

- Changed Assault to a flat 40 AP cost for both player and AI.
- Fixed assault weapon discovery for MENACE v0.7.15 by using native `Actor.GetAllAttacks()` instead of enumerating mixed `BaseSkill` / `ItemSkill` entries from `SkillContainer.GetAllSkills()`.
- Fixed the repeated `Object does not match target type` failure that caused valid adjacent assaults to be rejected.
- Added a current-patch runtime synchronization fallback on `TacticalManager.InvokeOnEntitySpawned` so player/AI infantry that did not materialize the manifest skill still receive Assault and refresh their skill UI.
- Made concrete weapon `Use`, `GetTemplate`, `GetID`, range and AP-cost reflection runtime-type safe.
- Post-victory movement/entry AP is granted only temporarily for native `MoveTo` execution and is not charged on top of the 40 AP assault cost.
- Debug logging remains OFF by default.

## 0.1.8
- Updated runtime bindings for the latest MENACE patch using the newly generated dump/IL2CPP assemblies.
- Fixed player Assault confirmation by intercepting `SkillAction.HandleLeftClickOnTile` instead of relying on native Charge target execution.
- Added an Assault-specific `Skill.OnVerifyTarget` override for adjacent enemy infantry, including contained infantry.
- Fixed reaction/assault weapon execution after MENACE changed `UsageParameter` flag values; flags are now resolved by enum name and use `IgnoreUsabilityCheck | NoApConsumed` while still consuming ammunition/uses.
- Added `GetAPConsumer()` as a fallback when resolving the actor that owns an Assault skill.
- Refreshed the EntityTemplate patch list from the new extracted data and added the new armed `enemy.construct_gunslinger` infantry template; non-combat/civilian/xeno pseudo-infantry entries remain excluded.
- Debug logging remains OFF by default; routine diagnostics remain debug-only.

## 0.1.7 - Logging cleanup

- Moved all normal informational and warning logging behind the Debug logging option.
- Debug logging remains disabled by default.
- Default runtime output is silent apart from fatal initialization errors that prevent the mod from functioning.
- No gameplay or assault-rule changes.

## 0.1.6

- Fix: replaced post-spawn runtime `SkillContainer.Add` injection with the same template-grant pattern already proven by Field Logistics.
- Fix: `mod.infantry_assault` is now cloned and configured by `modpack.json` before Tactical actors are spawned.
- Fix: all current MENACE `infantry` EntityTemplates receive Assault through an incremental `Skills.$append`, preserving vanilla skills and skills added by other mods such as Field Logistics.
- Compatibility: load order moved to 190 so Infantry Assault appends after Field Logistics (180), whose existing manifest replaces player squad skill arrays.
- Fix: removed the failing runtime injection path that produced `Object does not match target type` warnings in 0.1.4.
- Diagnostics: Tactical setup now reports infantry skill presence/missing counts instead of attempting to mutate live SkillContainers.
- No assault-rule or balance changes.


## 0.1.4

- Fix: Assault injection now resolves `UnitActor.GetSkills()` and `TransientActor.GetSkills()` directly instead of relying on inherited `Entity.GetSkills()` reflection dispatch.
- Fix: added a post-spawn injection hook on `TacticalManager.InvokeOnEntitySpawned(Entity)` so the SkillContainer is populated only after the actor is fully spawned.
- Diagnostics: successful Assault additions are now logged even with verbose logging off; failed CreateSkill/Add paths report their exact stage.
- No assault-rule or balance changes.

## 0.1.3
- Fix: runtime Assault skills are explicitly marked visible and enabled after insertion.
- Fix: the owning actor is assigned as AP consumer/owner for the runtime Assault skill.
- Fix: the SkillContainer is scheduled for update and `UiNeedsUpdate` is set so the tactical skill bar refreshes.
- Fix: Assault `Skill.IsVisible()`/`IsHidden()` are forced to player-facing values because the native Charge visibility handler is intentionally removed.
- No assault rules, balance, or AI decision changes.

## 0.1.2
- Fix: removed the unsafe/inherited `Entity.GetSkills()` Harmony hook that produced an Il2Cpp warning in Tactical.
- Fix: Assault is now injected from concrete `UnitActor.Create` and `TransientActor.Create` postfixes, while the existing-actor scan remains as a fallback.
- No gameplay-rule changes.

## 0.1.1
- Fixed C# build failure in the post-victory advance coroutine (`CS1626`).
- No gameplay or feature changes from 0.1.

## 0.1
- Added adjacent infantry-vs-infantry Assault action.
- Added defensive fire before the assaulting squad fires.
- Assaulting squad uses the cover of its origin tile during defensive fire.
- Defender cover protection is reduced by 50% during assault fire.
- Successful assaults advance into the defeated unit's tile or container.
- Failed assaults leave the attacker on its original tile.
- Added support for assaulting infantry inside buildings, bunkers, and towers.
- Added AI assault usage with conservative target selection.
- Added Mod Settings for gameplay, AI, cover reduction, and diagnostics.

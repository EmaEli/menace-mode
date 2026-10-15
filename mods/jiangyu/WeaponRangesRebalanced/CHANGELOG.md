## 2.9.2

- Fixed weapon ranges staying vanilla under Jiangyu: the range pass found zero `WeaponTemplate` / `ModularVehicleWeaponTemplate` objects on every scene load (`Weapon templates are not loaded yet` in the debug log) and never wrote a `MaxRange`. The Jiangyu host resolved template types only in `Il2Cpp*` proxy assemblies, but the game's own proxy keeps its original name (`Assembly-CSharp.dll`), so no game template type could be resolved. The host now also searches `Assembly-CSharp` and reads templates through `DataTemplateLoader.GetAll<T>()` / `TryGet<T>()`, the per-type cache the game itself uses. No balance changes.

## 2.9.1

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## 2.9.0

- Jiangyu port. The module now builds as a Jiangyu package (`mods/jiangyu/WeaponRangesRebalanced`) sharing its gameplay source with the legacy modpack build; the host binding is split into `Plugin.Modpack.cs` (legacy) and `WeaponRangesRebalancedSystem` (Jiangyu).
- Jiangyu build: `weapon-overrides.json` is read from `UserData/WeaponRangesRebalanced/` instead of the mod folder, which is replaced on every install. An existing file in `Mods/WeaponRangesRebalanced/` is copied there once.
- No weapon-range or other balance changes.


## 2.8.1

- Generalized the verified `InfluencesPositioning=false` large-map AI performance policy from only `active.launch_medium_mortar_frag` to all current long-range no-Line-of-Fire mortar/artillery/rocket-organ/indirect-fire attack skills.
- Current coverage includes light/medium/120 mm mortar fire, pirate/modular rocket-organ barrages, `active.fire_artillery`, and modular/twin/sentry/tripod ATGM indirect-fire modes.
- Direct-fire artillery remains untouched.
- Replaced per-skill success spam with one Debug-only policy summary; failures still warn.
- No weapon-range or other balance changes.

## 2.7

- Replaces the single repeat-target bonus with cumulative per-tile mortar ranging.
- Each successful prior shot at a tile gives later shots from the same physical mortar **-10 percentage points ScatterChance** by default.
- The bonus accumulates per tile and is capped at **-50 percentage points** by default; both step and cap are configurable.
- A mortar remembers multiple ranged tiles at once instead of forgetting the previous tile when it changes target.
- HE and smoke fired by the same physical mortar share the same firing-solution memory.
- Infantry mortar ranging memory is cleared when the crew leaves the deployed stance. Vehicle-mounted 120 mm mortar memory is cleared when the platform moves.
- Mortar ranges, damage, suppression, AP/ammo economy, cover policy and medium-mortar AI fix are unchanged from 2.6.

## 2.6

- Doubles default WRR mortar maximum ranges: light 17 -> 34, medium 21 -> 42, heavy 120 mm 24 -> 48.
- Keeps smoke rounds at the same doubled reach through their parent weapons; synchronizes range-overriding heavy/twin-fire 120 mm HE skills to 48.
- Future/unlisted mortar auto-balancing now uses the same 34/42/48 light/medium/heavy targets.
- Adds repeat-target ranging fire to current HE and smoke mortars: targeting the exact same tile as the previous shot reduces `ScatterChance` by 25 percentage points by default.
- Adds `Repeat-target aim bonus (scatter chance pp)` to Mod Settings (0-50, default 25). The template value is modified only while resolving the actual target and restored immediately afterwards.
- Does not change mortar damage, suppression, AP, ammunition, cover-destruction policy, or the medium-mortar AI positioning fix.

## 2.5

- Rebalanced Assault Rifle `Sustained Fire` and Battle Rifle `Spray` to 40 AP, matching their normal primary fire modes.
- High-volume alternate fire consumes 2 shared ammunition uses per successful activation instead of 1.
- Added a runtime usability guard so Sustained Fire/Spray require at least 2 ammo remaining; AI and forced/bypassed uses follow the same rule.
- `Aimed Fire` remains one-ammo-per-shot. Current 40 AP aimed-fire skills remain unchanged; aimed-fire AP is capped at 50 if a variant is ever above that value.
- Covers all current 5.56/7.62/suppressed/RMC/EM Sustained Fire skills and both current Battle Rifle Spray skills.
- No mortar/range-table changes from 2.4.

## 2.4

- Applies the mortar action-economy rebalance to smoke rounds too: smoke-mortar firing AP cost is halved and limited uses are doubled.
- Covers `active.launch_mortar_smoke`, `active.mod.launch_mortar_smoke_autoloader`, and the twin-fire smoke variant; smoke dispersion, VFX and tile effects stay native.
- No change to the existing HE suppression-only outer ring or HE damage scaling.

## 2.3

- Doubles limited HE mortar ammunition to match the 50% firing AP cost and preserve overall endurance.
- Adds a suppression-only outer ring one tile beyond the native mortar blast AoE at 50% suppression.
- Keeps physical and armor-durability damage radius unchanged; the extra ring calls native `Actor.ApplySuppression` so normal resistance/modifiers still apply.
- Smoke mortar ammunition is unchanged.

## 2.2.2

- Halves mortar AP cost per firing action.
- Halves physical HP damage and armor-durability damage while leaving suppression/morale output unscaled.
- Increases scatter to 3/4/5 tiles for light/medium/heavy mortar attacks.
- Keeps single-shot/native twin-fire execution, broader suppression falloff, half-cover immunity compatibility and the medium-mortar AI performance fix.

## 2.2.1

- Rolled back artificial mortar salvos created by increasing `SkillTemplate.Repetitions`; MENACE does not render those repetitions as separate full aim/fire cycles.
- Restored native mortar repetition counts and native per-shot damage/suppression multipliers. Native twin-fire remains native.
- Kept the accepted increased scatter, wider suppression falloff, heavy-mortar accuracy reduction, secondary-skill range synchronization and medium-mortar large-map AI performance fix.

## 2.2

- Rebalanced mortar HE into area salvos: light 4 rounds, medium 3, heavy 2 per mortar (4 in twin-fire mode).
- Increased mortar scatter and removed the heavy mortar's vanilla +50 accuracy bonus so point fire is less precise.
- Divided damage, armor-durability damage and suppression across each salvo so extra projectiles do not simply multiply aggregate output; reduced suppression AoE falloff to broaden the suppression footprint.
- Kept the verified `active.launch_medium_mortar_frag.AIConfig.InfluencesPositioning=false` large-map AI performance fix and all accepted mortar ranges.
- Audited current v0.7.15 range-overriding weapon skills and added proportional range scaling for 11 previously missed secondary attack skills (12 parent-skill associations), including underbarrel grenades, battle-rifle alternate fire and SMG/PDW tap fire.
- Secondary modes preserve their relative role: their max range scales by the parent weapon's WRR ratio rather than being forced equal to primary range.
- Normal gameplay logging remains silent unless Debug is enabled.

## 2.1.4.4

- Removed the temporary `[MORTAR_AI_PROBE]` / `Attack.OnCollect` diagnostic instrumentation after the enlarged-map stress test confirmed the mortar AI fix.
- Kept the verified `AIConfig.InfluencesPositioning=false` runtime write/read-back for `active.launch_medium_mortar_frag`.
- Weapon ranges, targeting rules and accepted balance values are unchanged.
- Debug logging remains OFF by default; only concise policy verification remains available when Debug is enabled.

## 2.1.4.3

- Fixed the medium-mortar `AIConfig.InfluencesPositioning=false` policy so it actually writes through the IL2CPP runtime wrapper (field or property) and verifies the value by reading it back.
- Keeps the medium mortar's realistic range unchanged; this fix targets only MENACE's expensive move-to-fire origin search.
- Removed the experimental per-agent `Agent.Evaluate()` re-entrancy guard from 2.1.4.2 after stress testing showed it did not materially reduce turn time.
- Debug mode now emits one `[MORTAR_AI_POLICY]` verification line showing current, desired and read-back values.
- The read-only `[MORTAR_AI_PROBE]` remains available for stress testing and remains silent with Debug logging OFF.

## 2.1.4.2

- Fixed the diagnostic `Attack.OnCollect` hook lookup for the current IL2CPP runtime namespace (`Il2CppMenace...`).
- No weapon ranges, AI positioning policy, target selection, collections, watchdog behavior, or gameplay values changed.

## 2.1.4

- Diagnostic-only release for the enemy-turn stall seen with long-range medium mortars on enlarged maps.
- Keeps all weapon and mortar ranges unchanged; `active.launch_medium_mortar_frag.AIConfig.InfluencesPositioning=false` remains the only gameplay-affecting mortar policy from 2.1.3.
- Adds a read-only Harmony probe around native `Attack.OnCollect()` for the medium mortar only.
- With Debug logging ON, `[MORTAR_AI_PROBE]` records elapsed time, concurrent mortar evaluations, Agent task count, tile/candidate collection counts before/after, thread/round/actor and native exception type.
- The probe never edits arguments or collections, never filters targets, never changes AI state, never cancels tasks and returns native exceptions unchanged.
- With Debug logging OFF the probe exits immediately and produces no log output.

## 2.1.3

- Keeps all realistic ranges unchanged, including the long-range medium mortar.
- Disables only `active.launch_medium_mortar_frag.AIConfig.InfluencesPositioning` while WRR is active, preventing MENACE from doing the expensive attack-specific move-to-fire origin search for that mortar.
- The mortar remains usable from valid positions reached by normal AI movement.
- No `Attack.OnCollect`, `AIFaction.GetOpponents`, opponent-list, watchdog, or forced-turn hooks are used.
- Restores the original AI positioning flag if WRR is disabled.

## 2.1.2

- Emergency stability rollback: removed the experimental `Attack.OnCollect` / `AIFaction.GetOpponents` mortar AI hooks introduced in 2.1.
- Removed fire-mission caching/target filtering and mortar AI positioning overrides.
- Restored the known pre-optimizer WRR runtime path while keeping all accepted realistic weapon ranges and VMAT synchronization unchanged.
- This directly addresses reproduced mortar AI timeouts followed by `Collection was modified` failures inside native `AI.Behaviors.Attack.OnCollect()`.

# Changelog

## 2.8

- Narrowed WRR to weapon range changes plus the verified medium-mortar `AIConfig.InfluencesPositioning=false` large-map turn-performance fix.
- Moved mortar AP/ammo economy, damage/armor-durability scaling, scatter/suppression behavior and cumulative ranging fire to Weapon Mechanics Rebalanced 0.1.
- Moved Sustained Fire / Spray AP+ammo economy and the Aimed Fire AP ceiling to Weapon Mechanics Rebalanced 0.1.
- Retained realistic mortar ranges (34/42/48), proportional secondary-skill range scaling, family strength settings and JSON exact range overrides.

## 2.1.1

- Build-only fix: qualified filesystem `Path` calls as `System.IO.Path` after the AI optimizer introduced `Il2CppMenace.Tactical.Path` into scope.
- No gameplay, range, or mortar optimizer behavior changes from 2.1.

## 2.1

- Added an AI-only fire-mission optimizer for long-range mortars; no player weapon range is reduced.
- The 80 mm mortar returns to the normal WRR realistic target (21); the temporary diagnostic clamp to 14 is not included.
- During mortar `Attack.OnCollect()`, MENACE receives a stable copy containing only the assigned fire-mission target. The real `AIFaction` opponent list is never edited, avoiding collection mutation while native AI enumerates it.
- Fire missions persist for 2 rounds by default and retarget immediately if the target dies or moves outside the mortar's live min/max range.
- New targets prefer clustered opponents, then already-suppressed or damaged units; native MENACE still performs the final attack scoring and execution.
- Added settings to enable/disable the optimizer, set target lock to 1-4 rounds, and disable expensive mortar attack-position searches.
- Optimizer diagnostics use the existing Debug logging toggle and remain silent by default.

## 2.0.1.2

- Fixed the vehicle-mounted VMAT accessory being left at its vanilla `active.launch_vmat_missile` max range of 9.
- VMAT is now synchronized directly at the range-overriding SkillTemplate level, with a realistic max-range target of 18 and the Launcher/Overall strength controls applied.
- Min range 3 and ideal range 6 remain native.
- No mortar targets or mortar logic changed in this release.

## 2.0.1.1

- Compatibility-only patch for MENACE Gameplay Suite integration.
- `weapon-overrides.json` now resolves from either the standalone `luca.weaponrangesrebalanced` modpack or the unified `luca.menacegameplaysuite` modpack.
- No range targets, classification rules, linked skills, settings defaults, or other gameplay behavior changed from 2.0.1.

## 2.0.1

- Reduced runtime logging to the minimum by default.
- Removed routine initialization, range-pass and successful JSON-load messages from normal logging.
- Replaced the old diagnostic setting keys with fresh `Debug logging` / full-table debug settings, both OFF by default, so previously enabled test logging does not carry over after updating.
- Unmatched weapon diagnostics are now emitted only with Debug logging enabled.
- Full weapon/skill table logging remains opt-in and no longer repeats when the table is unchanged.
- Real range write failures are still reported, but duplicate failure warnings are suppressed until the failure set changes.
- JSON parse/read/create errors and unexpected apply exceptions remain always visible.

## 2.0.0-alpha.5

- Fixed weapons whose attack `SkillTemplate` overrides `WeaponTemplate.MaxRange`, which could make the tooltip show the WRR range while the actual targeting distance stayed at vanilla values.
- Linked 14 current v0.7.14 primary range-overriding attack skills to their parent weapons.
- ATGM direct and indirect fire now follow the configured WRR ATGM range (24 by default for the modular vehicle launcher).
- 120 mm modular mortar attacks now follow the configured mortar range.
- Fixed the pirate truck HMG, vehicle flamers, tripod ATGM, and alien acid spitter where the primary attack skill could otherwise ignore WRR range changes.
- Linked skill ranges respect Overall/Family strength sliders and exact JSON weapon overrides; disabling WRR or setting strength to 0 restores the captured vanilla skill ranges.
- `MinRange` and `IdealRange` remain untouched. Only `MaxRange` is synchronized.
- Added `[WRR_SKILL]` diagnostic logging for linked attack skills when full weapon table logging is enabled.

## 2.0.0-alpha.4

- Replaced the Exact Overrides Mod Settings textbox with an external `weapon-overrides.json` file.
- Prevents long override lists from expanding the shared Mod Settings menu width for WRR and other mods.
- Added automatic hot reload of `weapon-overrides.json` while MENACE is running.
- Exact JSON overrides still bypass the overall and family strength sliders.
- Added safe JSON validation: invalid edits keep the last valid override set active and log a warning.
- Added an `Enable weapon-overrides.json` Mod Settings toggle.
- Added a packaged `weapon-overrides.example.json`; the active `weapon-overrides.json` is created only when missing so updates do not overwrite user configuration.

## 2.0.0-alpha.3

- Consolidated WRR around one realism-oriented balance table; there is no separate Classic vs Realistic preset.
- Kept the alpha.2 infantry and special-weapon targets unchanged.
- Reworked vehicle/heavy direct-fire ranges on MENACE's compressed tactical scale so HMGs no longer outrange cannons and autocannons.
- Revised representative hierarchy to HMG 19, chaingun 18, autocannon 20, anti-tank gun 21, long cannon 22, ATGM 24.
- Increased vehicle rocket launchers / airburst launchers to 15 and rocket artillery to 26 while leaving radar at 30.
- Rebalanced fixed vehicle turrets as well as modular vehicle weapons, including the heavy-tank 15 cm weapon, pirate truck HMG, chaingun, energy weapons and rocket systems.
- Updated future/new vehicle auto-classification to follow the same realistic hierarchy.
- Renamed diagnostics/source labeling from `Classic` to `Realistic` to reflect the single active table.
- Current v0.7.14 expected diagnostics remain: realistic=125, auto=0, unmatched ranged=0, write failures=0.

## 2.0.0-alpha.2

- Verified the live weapon set against MENACE v0.7.14+20743.
- Updated the explicit baseline to all 125 current unique weapons: 101 non-modular weapons and 24 modular vehicle weapons.
- Added explicit targets for current modular weapons missing from the original March table.
- Removed four no-longer-present v0.7.14 IDs from the current baseline.
- Fixed modular vehicle weapons being enumerated again through the base `WeaponTemplate` query.
- Vehicle weapons now capture their true pre-WRR range once, obey the vehicle toggle and strength cleanly, and no longer generate false unmatched warnings.

## 2.0.0-alpha.1

- Rewritten from a static stats patch into a current Menace SDK / MelonLoader modpack plugin.
- Added runtime weapon discovery, Mod Settings, automatic future-weapon classification, exact overrides and diagnostics.
- Continues to modify `MaxRange` only.

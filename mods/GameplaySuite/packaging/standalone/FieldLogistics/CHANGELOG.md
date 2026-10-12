## 1.0.6 - Concrete Skill use-state fix

- Fixed vehicle resupply uses remaining at unreadable/uninitialized values (`max=-1`, `uses=-1`, `charges=-1`) even though the custom skill itself was present.
- Root cause: `SkillContainer.GetAllSkills()` returns `BaseSkill` wrappers; reflection on that wrapper cannot see concrete `Skill` use-state methods.
- Matching Field Logistics skills are now safely re-cast with `TryCast<Skill>()`, and vehicle resupply calls `SetMaxUses`, `SetUses`, `SynchronizeUses`, `GetMaxUses`, `GetUses`, and `GetCharges` directly through the generated interop API.
- Keeps the 1.0.5 runtime missing-skill recovery/Mount Up load-order hardening unchanged.
- No balance changes and no new normal logging.

# Changelog

## 1.0.5 - Runtime skill injection hardening

- Hardened `Resupply Infantry` against template/load-order conflicts with other vehicle mods and loaded-save actors.
- If the manifest-defined skill is missing from a live player vehicle, Field Logistics now first asks MENACE to rebuild inherent skills from the current `EntityTemplate`, then falls back to appending only the missing registered `SkillTemplate` instance to the live `SkillContainer`.
- Applied the same fallback to `Redistribute Ammo` on player infantry so both custom Field Logistics skills recover from the same missed `EntityTemplate.Skills` merge condition.
- The fallback is idempotent and never replaces another mod's skill list.
- No balance changes. Debug logging remains OFF by default.

## 1.0.4 - Hot-path logging cleanup

- Removed `Redistribute Ammo candidate` logging from weapon discovery.
- Removed `Redistribute Ammo cache` logging from normal weapon-use caching.
- Debug logging still retains real redistribution summaries, failures and recovery diagnostics.
- No gameplay or balance changes.

## 1.0.3 - Logging cleanup

- Disabled all Field Logistics runtime logging by default.
- Informational, warning, retry, verification, and diagnostic messages are now emitted only when `Debug logging` is enabled in Mod Settings.
- Debug logging remains OFF by default.

## 1.0.2 - Release cleanup

- Removed the non-functional `Module supply cost` Mod Option.
- The Field Ammunition Supply module now has a fixed deployment cost of 20 supplies, as defined by the modpack manifest.
- Removed the runtime write to `DeployCosts.m_Supplies`, eliminating the related warning.
- Updated release metadata and Nexus packaging to version 1.0.2.

## 1.0.1 - Build hotfix

- Fixed a compile error in the vehicle resupply initialization path caused by an out-of-scope `ReadSkillCharges` helper call.
- Vehicle resupply diagnostics now read `GetCharges()` through the shared reflection helper.
- Added explicit DLL version metadata matching the mod version.
- Updated the Nexus release packaging script to produce `FieldLogistics-1.0.1.zip`.

## 1.0.0 - Initial public release

### Vehicle logistics

- Added `Resupply Infantry` to player vehicles with one use by default.
- Added `DefaultVehicleResupply` setting, enabled by default.
- Added the `Field Ammunition Supply` vehicle module.
- The module adds configurable extra resupply uses instead of adding a duplicate skill.
- Default configuration gives 1 resupply use to a normal vehicle and 5 total uses with the module installed.
- Added configurable resupply AP cost.
- Added configurable module supply cost, rarity, Black Market quantity, and campaign progress requirement.
- Added test-loadout and debug options for development/testing.

### Experimental infantry ammo sharing

- Added optional `Redistribute Ammo` action for adjacent infantry.
- Feature remains experimental and disabled by default.
- Restricted sharing to regular squad weapons.
- Excluded grenades and special weapons.
- Restricted sharing to weapons in the same MENACE weapon category.
- Preserves total ammunition while balancing between compatible weapons and respecting capacities.
- Added runtime handling for temporarily unavailable (`-1`) inactive-weapon ammo states.
- Added pending ammo credits so deferred transfers do not create or lose ammunition.
- Added synchronization for alternate/sustained/spray fire modes of the same weapon.
- Added transactional rollback and post-refresh verification for failed writes.

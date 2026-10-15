# Field Logistics 1.0.11

Field Logistics adds battlefield ammunition support for MENACE vehicles and an optional experimental infantry ammo-sharing system.

Both actions use the cartridge sprites `skill_05` / `skill_05_inactive` in 1.0.11
(issue #38). This is an offline-built candidate; the new icons still need visual
verification in game.

1.0.7 restores compatibility with MENACE v0.7.16: the skill template both actions were cloned from was removed by the game, so the clones now derive from `active.rogue_officer_whistle` with the Refill Ammo behavior re-applied by the manifest, and squad/vehicle skill lists are appended to instead of replaced (see issue #41).

## Main feature: vehicle resupply

- Player vehicles receive one `Resupply Infantry` use by default.
- The action fully refills the regular ammunition of an adjacent infantry unit.
- `DefaultVehicleResupply` can disable the inherent vehicle use if desired.
- The `Field Ammunition Supply` vehicle module adds extra uses to the same resupply action.
- Default module bonus: +4 uses.
- Default result: vehicle without module = 1 use; vehicle with module = 5 uses.
- Resupply AP cost, rarity, Black Market quantity, campaign progress requirement, and extra module uses are configurable in Mod Options. The module supply cost is fixed at 20 supplies.

## Experimental infantry ammo sharing

Infantry ammo sharing remains optional, experimental, and OFF by default.

When enabled, adjacent infantry squads gain `Redistribute Ammo`:

- Range 1 only.
- Regular `squad_weapon` ammunition only.
- Grenades and special weapons are excluded.
- Only weapons from the same MENACE weapon category can exchange ammunition.
- Total ammunition is conserved.
- Distribution attempts to balance available ammunition while respecting weapon capacity.
- Alternate/sustained/spray fire modes are kept synchronized where MENACE exposes them.
- A mission-local pending-ammo ledger handles transient MENACE runtime states where inactive weapon ammo temporarily reports `-1`.

Because MENACE 0.7.14 can expose inactive weapon state inconsistently, this feature should still be treated as experimental.

## Jiangyu package and shared installer

This package uses `jiangyu.json`, compiled template data and `code/FieldLogistics.dll`;
it is not the legacy `modpack.json` / `dlls` package. Options use MenaceModSettings (F9).
Gameplay code is shared with `mods/GameplaySuite/src/FieldLogistics`.

Use the canonical [shared installer workflow](../../GameplaySuite/docs/INSTALLING.md).
The same installer supports the full Suite or `-Product Standalone -Modules FieldLogistics`;
do not install the legacy copy alongside this Jiangyu package. Unrelated mods and
ModpackLoader are preserved. The installer candidate still needs Windows/rollback tests.

For authoring, use `jiangyu compile --clean --release` after changing shared gameplay
sources: Jiangyu's code fingerprint does not include the linked GameplaySuite sources.
The compiled package must include `templates.json` and locales, not just its DLL.

## Requirements

- MENACE 0.7.16+22323 and existing MelonLoader with generated game proxies.
- Jiangyu 1.4.8 or later; the shared installer pins and hash-checks 1.4.8.
- MenaceModSettings >= 0.4.0 (the shared distribution includes 0.5.0).
- .NET 10 SDK for the canonical build workflow; Unity Editor is not needed to rebuild
  code in the bundled-source candidate. Authoring asset changes has separate tool requirements.

## Version 1.0.6

- Fixed `Resupply Infantry` use initialization on current MENACE/Il2CppInterop builds.
- MENACE exposes `SkillContainer.GetAllSkills()` as `List<BaseSkill>`; Field Logistics now safely casts a matching action back to concrete `Skill` before calling `SetMaxUses`, `SetUses`, `SynchronizeUses`, and the corresponding getters.
- Retains the 1.0.5 runtime skill-injection/load-order compatibility fix.
- No balance changes; Debug logging remains OFF by default.

## Version 1.0.5

- Hardened runtime skill injection for compatibility with vehicle/template mods and loaded saves.
- Missing `Resupply Infantry` / `Redistribute Ammo` actions are restored through MENACE's native inherent-skill refresh first, with a narrow direct `SkillContainer` fallback only when needed.
- Existing skills are never replaced or duplicated.
- No balance changes; Debug logging remains OFF by default.

## Version 1.0.4

- Removed hot-path per-candidate and per-cache diagnostic lines even when Debug logging is enabled.
- Debug retains operation summaries, failures and useful state transitions without weapon-use spam.
- No gameplay changes.

## Version 1.0.3

Release candidate with the invalid live module-cost setting removed; module cost remains fixed at 20 supplies.

See `CHANGELOG.md` for the release notes.

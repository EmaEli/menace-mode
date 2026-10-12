# Changelog

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

# Field Logistics 1.0.10

Field Logistics adds battlefield ammunition support for MENACE vehicles and an optional experimental infantry ammo-sharing system.

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

## Build and deploy for development

Run:

```text
build-and-deploy.bat
```

By default the script expects MENACE at:

```text
M:\SteamLibrary\steamapps\common\Menace
```

To use another installation, set `MENACE_GAME` before running the script.

The script builds the DLL and deploys the modpack to:

```text
MENACE\Mods\FieldLogistics
```

## Build a Nexus release ZIP

Run:

```text
build-nexus-release.bat
```

This script builds the release DLL but does **not** deploy anything into the game. It creates:

```text
artifacts\FieldLogistics-1.0.6.zip
```

The archive contains only the installable Nexus/modpack structure:

```text
FieldLogistics\
  modpack.json
  dlls\
    FieldLogistics.dll
```

The authoring ZIP is for development and source distribution; the ZIP produced by `build-nexus-release.bat` is the one intended for Nexus Mods.

## Requirements

- MENACE
- MelonLoader
- Menace Modpack Loader v2
- .NET 6 SDK for local compilation

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

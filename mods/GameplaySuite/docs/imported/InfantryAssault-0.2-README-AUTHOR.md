# Infantry Assault 0.2 - authoring package

This is the editable source package for the MENACE Infantry Assault mod.

## Build / deploy

Run `Build-and-Deploy-Dev.cmd` on the MENACE machine. It builds against the live MelonLoader IL2CPP assemblies and deploys to:

`MENACE/Mods/InfantryAssault`

The default game path matches the other Luca MENACE mods:

`M:\SteamLibrary\steamapps\common\Menace`

If compilation fails, send `artifacts/build-and-deploy.log`.
If compilation succeeds but the mod fails in game, send `MelonLoader/Latest.log`.

## Technical design

- Modpack Loader v2 / .NET 6 / Harmony.
- Reuses native `player.close_combat_charge` only as the clone source for icon/cursor/targeting presentation.
- `modpack.json` clones and patches `mod.infantry_assault` before actor creation; native Charge damage handlers are removed in the manifest.
- Current game infantry EntityTemplates receive the skill through `Skills.$append`, preserving existing skills from MENACE and other mods.
- Load order is 190 so this append runs after Field Logistics (180); this is required because the current Field Logistics manifest replaces player squad `Skills` arrays.
- Runtime synchronization is a fallback: spawned infantry missing the manifest skill receive a `SkillTemplate.CreateSkill()` instance through their live `SkillContainer`, followed by a native UI refresh.
- Player Assault use is intercepted and resolved by the mod rather than native Charge behavior.
- AI hooks `Menace.Tactical.AI.Agent.Execute()` and can invoke the exact same assault resolver.
- Assault has a flat 40 AP cost. Defensive and assault fire reuse the squad's actual item-backed weapon skill, so normal damage, ammo/uses, veterancy, perks, and other weapon modifiers remain in the normal game pipeline.
- During assault fire, a last-priority postfix modifies the final `Skill.GetCoverMult()` result. This is intended to remain compatible with StrongerCover loaded earlier.
- The attacker is not physically moved into the defender's occupied tile while combat is unresolved. It advances only after the defender is destroyed.

## Versioning

Use the project convention:

- functional changes/features: `0.1` -> `0.2` -> `0.3` ...
- bug/build/packaging-only fixes: `0.1` -> `0.1.1` -> `0.1.2` etc.
- first stable release: `1.0`.

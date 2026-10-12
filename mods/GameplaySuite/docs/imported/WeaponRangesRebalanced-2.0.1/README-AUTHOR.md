# Weapon Ranges Rebalanced 2.0.1 authoring package

This package follows the current StrongerCover / Overwatch-style project layout:

- .NET 6 project under `src/WeaponRangesRebalanced`;
- builds against MENACE MelonLoader IL2CPP assemblies and `Menace.ModpackLoader.dll`;
- deploys to `Mods/WeaponRangesRebalanced/dlls/WeaponRangesRebalanced.dll`;
- includes source in deployed and Nexus packages;
- uses `ModSettings` and current `Menace.SDK.Templates` APIs;
- delays scene range passes by 10 frames before touching IL2CPP-backed templates.

## Balance model

Alpha.4 moved exact per-weapon overrides out of Mod Settings into a hot-reloaded `weapon-overrides.json`, preventing long override strings from expanding the shared menu. Release packages ship only `weapon-overrides.example.json`; the active file is created at runtime when missing so user overrides survive normal updates. Alpha.5 additionally synchronizes known primary range-overriding attack SkillTemplates with their parent weapon MaxRange so gameplay range cannot remain stuck at vanilla while the item tooltip shows the WRR value.

There is one active balance model: the realism-oriented WRR table. Infantry and special-weapon values remain from the prior update; vehicle/heavy direct-fire targets were revised in alpha.3 so the compressed MENACE scale has a more plausible hierarchy.

Historical v1 JSON files remain under `docs/` only as provenance. They are not selectable presets and are not loaded at runtime.

## Build

Run `Build-and-Deploy-Dev.cmd` with MENACE closed.

If the build fails, send `artifacts/build-and-deploy.log`.

If it builds but behaves incorrectly, enable **Debug logging** and **Log full weapon table (debug)**, restart MENACE, enter a mission once, and send `MelonLoader/Latest.log`.

## Runtime-source test package

`runtime-source/WeaponRangesRebalanced` is a ready-to-copy source-compiled Modpack Loader package. For a final Nexus release, prefer the prebuilt-DLL package produced by `Build-Nexus-Package.cmd`.

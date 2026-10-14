# Cover Destruction Rebalance — Jiangyu port (Issue #31)

Data-only port of the legacy standalone `mods/GameplaySuite/packaging/standalone/CoverDestructionRebalance` (Modpack Loader 0.3.1) to Jiangyu.Loader. No C# code.

## What it does

Same rule as the legacy mod, applied to the `Attack` event handler of each `SkillTemplate` (MENACE 0.7.15 data, 62 skills):

| Vanilla `DestroyHalfCover` | Patched |
| --- | --- |
| `None`, `Light` | unchanged |
| `Medium` | `Light` |
| `Heavy` | `Medium` |
| `active.laser_rifle_gunslinger` (`Heavy`) | `Light` |
| `active.launch_light_mortar_frag`, `active.launch_medium_mortar_frag` | `None` |

KDL grammar: handlers are addressed through their parent skill with a child-block descent (`set "EventHandlers" index=N { set "DestroyHalfCover" enum="HalfCoverClass" "..." }`); bracket indexers (`EventHandlers[0].X`) are rejected by the 1.4.8 compiler.

## Differences vs legacy

- The legacy mod computed the rule at runtime over every current `SkillTemplate`, so it followed skills added by later game patches. This port is a frozen list generated from the 0.7.15 extracted data: regenerate `templates/skill-destroy-half-cover.kdl` after a game update that adds or changes attack handlers.
- The legacy 0.3.1 `AttackHandler.OnTileHit` Harmony guard (re-forcing the mortar exception if handlers get rebuilt) is not ported. Jiangyu applies template patches before gameplay; whether the game rebuilds handler objects later is something to watch for in testing (mortar HE destroying sandbags would be the symptom).
- No in-game toggle or debug logging (Jiangyu has no per-mod settings surface). Toggle = install/remove the mod folder.

## Build

`jiangyu compile --release` (CLI 1.4.8, validated against the installed game's `Assembly-CSharp.dll` + `globalgamemanagers`): 0 errors, `Parsed 62 template patch(es)`. `jiangyu package` output is committed at `dist/CoverDestructionRebalance-0.4.0.zip` (contents `CoverDestructionRebalance/jiangyu.json` + `CoverDestructionRebalance/templates.json`). Extract it into `<MENACE>\Mods\`. The loader reads only `templates.json`; copying this source folder yields `no content`.

## Status

Compiled, **not yet tested in game**. Do not run alongside the legacy CoverDestructionRebalance standalone or the GameplaySuite module with it enabled (both write the same fields).

Expected `Latest.log` line: `[Jiangyu] [CoverDestructionRebalance] 0.4.0: 62 patch ops` followed by a template self-check of 62 matched ops. In-game check: light/medium mortar HE no longer removes sandbags; heavy weapons that used to remove heavy half-cover now only remove medium.

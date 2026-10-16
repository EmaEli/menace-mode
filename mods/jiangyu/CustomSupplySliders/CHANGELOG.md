# Custom Supply Sliders

## 0.2.1

- The `-`/`+` buttons of the four percentage settings move by 100 instead of 1. Requires MenaceModSettings 0.6.0 (`AddNumber` step).

## 0.2.0

- Ported from the legacy Modpack Loader (`modpack.json` + `dlls\`) to a Jiangyu package. Settings now live in the MenaceModSettings window (End key) under "Custom Supply Sliders"; the keys (`PlayerMin`, `PlayerMax`, `EnemyMin`, `EnemyMax`, `DebugLogging`) and defaults are unchanged.
- Hooks now bind to the typed `NewGameWindow` / `LabeledIntSlider` proxies instead of scanning loaded assemblies by name.
- Release logging is silent; the one-time range rewrite messages of 0.1.5 are now emitted only with Debug logging ON.
- No changes to the slider bounds logic.

## 0.1.5

- Fixed the runtime range hook: replaced the IL2CPP `object[] __args` mutation path with explicit by-reference Harmony arguments.
- Added an independent `LabeledIntSlider.Init` fallback hook matching the confirmed vanilla supply ranges (67-150 player, 67-200 enemy).
- Added only minimal one-time diagnostics: hook installation and the first successful player/enemy range rewrite.

## 0.1.4

- Fixed the Modpack Loader trust flag (`securityStatus: SourceVerified`); removed the ineffective `forceLoad` workaround.

## 0.1.3

- Added the exact source tree to the runtime/Nexus package; clarified the Mod Settings labels.

## 0.1.2

- Added the nullable compiler metadata shim used by the MENACE Gameplay Suite (CS0656 against the IL2CPP reference assemblies).

## 0.1.1

- Fixed build/deploy diagnostics and added Nexus package creation.

## 0.1

- Initial implementation.

# Starting Options 0.1

Adds two **Mod Options** settings for the **New Game / Customize Difficulty** screen.

| Mod Option | Default | Range | Behaviour |
|---|---:|---:|---|
| Maximum selectable starting leaders | 12 | 6-12 | Extends the vanilla Initial Leaders slider maximum of 6; reopen the New Game window after changing. |
| Starting Spare Parts (Black Market Headstart) | 36 | 0-1000 | Changes the number of Spare Parts given when the game's **Black Market Headstart** checkbox is selected. Each retains its native 5 trade value. |
| DebugLogging | OFF | on/off | Only startup/new-campaign diagnostics; no per-turn logging. |

The leader cap is also constrained to the number of pickable leader templates available in the game. All other new-game settings remain unchanged.

**Important:** These options take effect when creating a **new campaign**; they do not retroactively alter saves. The game's *Black Market Headstart* checkbox is still required: if unchecked, no free Spare Parts are added.

## Build / installation

Jiangyu package. Build with `jiangyu compile --release --clean` from this directory and copy `compiled/` into `Mods\StartingOptions\`. Requires MelonLoader, the Jiangyu loader and MenaceModSettings (options are shown in the F9 window).

Remove any legacy Modpack Loader copy of Starting Options (`modpack.json` + `dlls\`) so the module is loaded only once.

## Notes

- Native integration: Harmony Prefix on `NewGameWindow.AddCustomDifficultySlider` and Postfix on `NewGameWindow.CreateGlobalDifficultyObject`.
- Only initial extra-item entries identified as `commodity.spare_parts` are changed. Other starting rewards are not touched.
- No runtime/tactical hooks and no logging by default.
- Game integration has to be validated in an actual MENACE installation after compiling.

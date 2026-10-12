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

This is a **source/authoring template**. From the top-level unified repo run `Build-Nexus-Packages.cmd` to build the installable standalone `StartingOptions-0.1.zip` and `MenaceGameplaySuite-0.32.zip`. Requires a working MENACE installation with MelonLoader, Menace Modpack Loader v2, and .NET 6 SDK. Or run `Build-and-Deploy-Suite.cmd` to build and deploy the suite directly.

Do not install both the unified suite and individual copies of Starting Options.

## Notes

- Native integration: Harmony Prefix on `NewGameWindow.AddCustomDifficultySlider` and Postfix on `NewGameWindow.CreateGlobalDifficultyObject`.
- Only initial extra-item entries identified as `commodity.spare_parts` are changed. Other starting rewards are not touched.
- No runtime/tactical hooks and no logging by default.
- Game integration has to be validated in an actual MENACE installation after compiling.

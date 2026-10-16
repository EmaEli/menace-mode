# Custom Supply Sliders 0.2.0

Expands the two percentage sliders under **New Game -> Customize Difficulty**:

- **Player Supply Points** (vanilla range 67%-150%)
- **Enemy Supply Points** (vanilla range 67%-200%)

Normal/Hard/Expert difficulty templates are not modified. Only the Custom Difficulty slider ranges are changed.

## Settings (MenaceModSettings window, End key, group "Custom Supply Sliders")

| Setting | Default | Range | Step |
|---|---:|---:|---:|
| Player Supply Points minimum (%) | 0 | 0-2000 | 100 |
| Player Supply Points maximum (%) | 2000 | 150-10000 | 100 |
| Enemy Supply Points minimum (%) | 0 | 0-2000 | 100 |
| Enemy Supply Points maximum (%) | 2000 | 200-10000 | 100 |
| Debug logging | OFF | on/off | |

Changes apply the next time the New Game window is opened.

## Build / installation

Jiangyu package. Build with `jiangyu compile --release --clean` from this directory and copy `compiled/` into `Mods\CustomSupplySliders\`. Requires MelonLoader, the Jiangyu loader and MenaceModSettings.

Remove any legacy Modpack Loader copy of Custom Supply Sliders (`modpack.json` + `dlls\`) so the mod is loaded only once.

## Notes

- Native integration: Harmony Prefix on `NewGameWindow.AddCustomDifficultySlider` (by-reference ints) plus an independent fallback Prefix on `LabeledIntSlider.Init(int, int, bool)` that matches only the vanilla 67-150 / 67-200 pairs.
- No tactical hooks and no logging by default.

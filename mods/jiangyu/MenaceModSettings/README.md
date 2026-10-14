# MenaceModSettings (Jiangyu companion)

Shared settings registry and in-game settings window for mods running on `Jiangyu.Loader`.
It replaces the `ModSettings` API that the legacy `Menace.ModpackLoader` provided and that
Jiangyu has no equivalent for (tracked in #37; unblocks the code modules in #38).

**Status:** compiles and packages with jiangyu 1.4.8; registry/persistence logic covered by an
offline smoke test (19 checks). 0.1.0 loaded in game and the overlay opened, but plain labels
rendered invisible (no USS class, Unity default black text) and F10 collided with
SquaddieFieldPromotions; 0.2.0 fixes both and is **pending in-game re-test**.

## What it does

- `ModSettings.Register(group, builder)` declares a group of settings: headers, toggles,
  sliders (float), numbers (int) and dropdowns. Same shape as the legacy builder, so a module
  ports by swapping `using`.
- `ModSettings.Get<T>(group, key)` / `Get` / `TryGet` / `Set` read and write values.
  `Set` clamps and coerces to the definition, persists and raises `ModSettings.Changed`.
- Values persist in **one file for the whole install**:
  `UserData/MenaceModSettings/settings.json` (`{ "Group": { "Key": value } }`).
  They are not tied to a save slot, which matches the legacy behaviour.
- **F9** opens the window on any screen: one tab per group, one row per setting, `RESET GROUP`,
  `CLOSE`, or click the dimmed backdrop. Every control is button-driven (`ON/OFF`, `-`/`+`,
  `<`/`>`), built from the SDK's native `TextButton` and `Flyout`, so no Unity bundle is needed.
- The companion registers its own `MenaceModSettings` group with the toggle key
  (F8–F12, Insert, Home, End; F10 is used by SquaddieFieldPromotions), so the window is never
  empty and the key can be changed in game.
- Plain text must get an inline colour (`SettingRows.Text`): injected elements only inherit the
  game's stylesheet through USS classes, and a bare `Label` renders black on the dark frame.

## Using it from another Jiangyu mod

`jiangyu.json`:

```json
"depends": ["Jiangyu >= 1.4.8", "MenaceModSettings >= 0.2.0"]
```

`code/<Mod>.csproj` (the loader loads every mod DLL into the default context, in dependency order,
so a plain reference resolves at runtime):

```xml
<ItemGroup>
  <Reference Include="MenaceModSettings">
    <HintPath>../../MenaceModSettings/compiled/code/MenaceModSettings.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

```csharp
using MenaceModSettings;

public sealed class StrongerCoverSystem : JiangyuSystem
{
    public override void OnInit()
    {
        ModSettings.Register("StrongerCover", s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("EnableGameplay", "Enable StrongerCover", true);
            s.AddSlider("LightCover", "Light cover hit chance (%)", 10f, 100f, 80f);
        });
    }

    bool Enabled => ModSettings.TryGet<bool>("StrongerCover", "EnableGameplay", out var v) && v;
}
```

## Layout

- `jiangyu.json` – manifest.
- `code/` – the C# project (`ModSettings`, `SettingsStore`, `SettingsWindowSystem`, `SettingRows`).
- `locales/de.po` – German strings for the window. Also what makes the 1.4.8 compiler build this
  mod: `jiangyu compile` skips a project with no templates, assets or locales, so a code-only mod
  needs at least one of them.
- `dist/` – packaged zip from `jiangyu package`.

## Build

```sh
jiangyu code sync          # once per machine: writes code/local.props from your global config
jiangyu compile --release
jiangyu package --output dist
```

## Install (manual test)

Unzip `dist/MenaceModSettings-<version>.zip` into `Mods/` (gives `Mods/MenaceModSettings/`),
start the game, press **F9**. With no consumer mod installed the window shows a single
`MenaceModSettings` tab with the toggle-key dropdown. `Latest.log` should contain a
`[MenaceModSettings]` load line and no errors.

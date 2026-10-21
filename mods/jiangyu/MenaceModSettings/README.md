# MenaceModSettings (Jiangyu companion)

Shared settings registry and in-game settings window for mods running on `Jiangyu.Loader`.
It replaces the `ModSettings` API that the legacy `Menace.ModpackLoader` provided and that
Jiangyu has no equivalent for (tracked in #37; unblocks the code modules in #38).

**Status:** compiles and packages with jiangyu 1.4.8; registry/persistence logic covered by an
offline smoke test (19 checks). 0.1.0 loaded in game and the overlay opened, but plain labels
rendered invisible (no USS class, Unity default black text) and F10 collided with
SquaddieFieldPromotions; 0.2.0 fixed both and rendered every element in game. 0.2.1 fixed the
settings list laying out horizontally with an unstyled horizontal scrollbar (no default Unity
theme on the game's panel), verified in game. 0.2.2 clamps the window to 85% of the screen
when a group has many rows (Larger Maps ran off screen with a percent max-height alone) so the
settings list scrolls; verified in game on MENACE 0.7.16. 0.3.0 adds `ModSettings.OnSettingChanged`
as an alias of `Changed` (the legacy Modpack Loader name), so GameplaySuite sources shared between
both hosts subscribe without `#if`. 0.3.1 wraps long row labels instead of letting them push the
controls past the window edge (Starting Options' stepper was clipped), verified in game. 0.3.2 pins
`AssemblyVersion` to 1.0.0.0 (0.3.1 briefly shipped 0.3.1.0 and every consumer failed to bind) and
adds a near-opaque backing under the window frame. 0.4.0 adds `ModGui.Add/Remove(Action)`: an IMGUI
frame hook driven by one injected `[JiangyuType]` MonoBehaviour, for mods that draw a GUI/GUILayout
HUD (Jiangyu systems have no OnGUI callback); 0.3.2 and 0.4.0 not yet verified in game. 0.5.1
makes End the default toggle key (F9 is the game's bug-report key). 0.6.0 scrolls the tab column
when more groups are registered than fit the window and adds an optional `step` to `AddNumber`
(Custom Supply Sliders moves its percentages by 100). 0.6.1 restores the 5-argument `AddNumber`
overload: 0.6.0 replaced it with an optional parameter, which is bound at compile time, so every
consumer built against 0.5.x (Field Logistics, Starting Options) failed `OnInit` with
`MissingMethodException`. Never change an existing public signature; add an overload.

## What it does

- **0.7.0 — debug inventory (#65):** on a campaign screen, press **End → MenaceModSettings →
  DEBUG: STOCK 10 OF EVERY ITEM**, then press **CONFIRM**. Tops up to ten **unequipped reserve**
  copies per obtainable item: market equipment (including K-PAC with Launcher), reward-table
  items, blueprint results/materials, starting unlocks and commodities. Ignores campaign
  progress/market randomness. No automatic grants; repeated clicks only replace missing stock.
  Preserves equipped items and stock above ten; uses native AddItem so vehicle records are
  created normally. Excludes FutureContent/Dev and non-obtainable enemy equipment.
  Dossiers, tokens, vouchers and recruits are excluded: native AddItem redeems them immediately,
  rather than storing them. This is not a roster/ship-upgrade unlocker.
  **Use a separate test save:** changes persist when saved and mark the campaign as cheated.
  Cancel, closing the window, changing tabs or scenes clears confirmation. Reopen Armory/Market
  afterward to refresh the list. Reports partial failures without pretending to roll back;
  retrying fills only remaining shortages.
- **0.8.0 — inventory failure diagnostics:** reports the item ID, processing stage and exception
  type/message for failures, or explicitly says when `AddItem` returns no item. Shows up to five
  failures and counts any additional ones. Catalog-wide errors identify the phase that failed.
  Installed on the MENACE test PC on 21 October 2026; user reports zero additions and all items
  failing. The failing stage/exception is not yet known; the inventory action is not verified.
- **0.8.1 — opt-in inventory log diagnostics:** after the first stock-button click, choose
  **CONFIRM + LOG DIAGNOSTICS TO Latest.log** instead of normal CONFIRM. Performs the same
  guarded top-up, but also logs the summary and up to twenty item IDs/stages/error details;
  catalog-wide failures include the full exception. Normal confirmation and normal gameplay
  remain silent. Diagnostic logger failures cannot interrupt or repeat additions. This is
  evidence collection for #65, not a claimed fix for native item creation.
- 0.4.1 adds a shared tactical HUD input guard (#43). IMGUI consumers register a live
  rectangle provider with `HudInputGuard.Register(owner, provider)` (GUI coordinates, null
  when hidden) and call `Unregister(owner)` on unload. One companion-owned Harmony guard
  blocks native map/camera mouse paths and captures HUD-origin presses through release.
  Keyboard input is untouched. Offline build/tests passed; in-game interception is pending.
- `ModSettings.Register(group, builder)` declares a group of settings: headers, toggles,
  sliders (float), numbers (int) and dropdowns. Same shape as the legacy builder, so a module
  ports by swapping `using`.
- `ModSettings.Get<T>(group, key)` / `Get` / `TryGet` / `Set` read and write values.
  `Set` clamps and coerces to the definition, persists and raises `ModSettings.Changed`
  (also exposed as `OnSettingChanged`, the legacy name).
- Values persist in **one file for the whole install**:
  `UserData/MenaceModSettings/settings.json` (`{ "Group": { "Key": value } }`).
  They are not tied to a save slot, which matches the legacy behaviour.
- **End** opens the window on any screen (configurable; F9 is avoided because it is the game's
  bug-report key): one tab per group, one row per setting, `RESET GROUP`,
  `CLOSE`, or click the dimmed backdrop. Every control is button-driven (`ON/OFF`, `-`/`+`,
  `<`/`>`), built from the SDK's native `TextButton` and `Flyout`, so no Unity bundle is needed.
  Both the tab column and the settings list scroll vertically when they overflow.
- `AddNumber(key, label, min, max, default, step = 1)`: `-`/`+` move the value by `step`.
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
start the game, press **End**. With no consumer mod installed the window shows a single
`MenaceModSettings` tab with the toggle-key dropdown. `Latest.log` should contain a
`[MenaceModSettings]` load line and no errors.

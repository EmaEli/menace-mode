# StrongerCover 2.2.3

StrongerCover makes MENACE cover more meaningful and configurable. This build is updated for the October 2026 MENACE IL2CPP API used by the latest uploaded game patch.

## Directional cover

Default attacker hit-chance multipliers:

- Light cover: 80%
- Medium cover: 60%
- Heavy cover: 40%

## Inside cover

Default hit-chance multipliers:

- Bunker: 20%
- Forest / vegetation: 55%
- Open vehicle: 60%
- Heavy watchtower: 30%
- Garrisonable building: 30% when the compatible `cover_inside_building` profile is present

The Garrisonable Buildings integration only overrides the building `AccuracyMult`. Its base damage, suppression and concealment values remain owned by Garrisonable Buildings.

Without Garrisonable Buildings installed, the building setting simply has no target and does nothing.

## Indirect fire

Indirect-fire cover is enabled by default. For cover-ignoring skills whose IDs identify mortars, howitzers, artillery or indirect fire, StrongerCover can:

- average cover around all 8 directions of the target tile;
- combine that with inside-cover protection using the more protective value;
- leave grenades, grenade launchers, RPGs, rockets and flamethrowers on vanilla behavior.

Indirect-fire cover intentionally does not apply squad-size scaling.

## Squad size

Infantry squad size can additionally reduce hit chance:

- 1-3 soldiers: 70%
- 4-5 soldiers: 80%
- 6-7 soldiers: 90%
- 8+ soldiers: no additional squad-size modifier

Wildlife and Constructs are excluded by default.

## Mod Settings

Open MENACE Mod Settings and select **StrongerCover**.

The panel includes directional cover, inside-cover profiles, the Garrisoned Building hit-chance setting, indirect-fire toggles, squad-size scaling and diagnostics. Runtime logging is silent by default; enable **Debug logging** only when diagnostic logs are needed. Debug mode is intentionally compact: per-query squad-size and indirect-cover calculations are not written to the log because those hot paths can be evaluated thousands of times. The DevConsole diagnostics still show the latest indirect-cover calculation.

## Garrisonable Buildings compatibility

Garrisonable Buildings 0.8 creates a dedicated `cover_inside_building` profile with its own base values. StrongerCover recognizes that profile and, while enabled, applies **Garrisoned building hit chance (%)** to its `AccuracyMult`. Default: 30%.

Disabling or unloading StrongerCover restores the base building accuracy captured from Garrisonable Buildings.

If anything behaves unexpectedly, send `MelonLoader/Latest.log`.

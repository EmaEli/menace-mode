# Smoke mechanics notes (MENACE v0.7.15)

## Native structures used by Smoke Overhaul

`Menace.Tactical.TileEffects.TileEffectTemplate` contains the native `Type` and `BlockLineOfSight` fields. `TileEffectType.Smoke` is flag value `1`.

`Tile` keeps active `TileEffectHandler` instances and exposes `GetEffects()`. `TileEffectHandler.GetTemplate()` provides the underlying tile-effect template.

`Skill.GetHitchance(Tile from, Tile target, EntityProperties attacker, EntityProperties defender, bool includeDropoff, Entity overrideTargetEntity, bool forImmediateUse)` returns the shared `HitChance` struct used by gameplay calculations. Smoke Overhaul adjusts only `HitChance.FinalValue` after the native calculation.

## Exported smoke data

The current extracted game data shows smoke delivery skills referencing `tile_effect.smoke_grenade`, with carrier smoke also using `tile_effect.carrier_smoke_screen`. Units inside smoke receive the vanilla `effect.smoke` status. The mod deliberately does not replace or rewrite that status effect and does not alter smoke VFX.

## Design rule

The added fire-through penalty counts only smoke tiles strictly between shooter and target. Endpoint tiles are excluded so native inside-smoke modifiers remain authoritative and are not double-counted.

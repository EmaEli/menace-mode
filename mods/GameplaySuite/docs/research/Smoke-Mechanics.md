# Smoke mechanics notes (MENACE v0.7.16 extracted data)

## Native structures used by Smoke Overhaul

`Menace.Tactical.TileEffects.TileEffectTemplate` contains the native `Type` and `BlockLineOfSight` fields. `TileEffectType.Smoke` is flag value `1`.

`Tile` keeps active `TileEffectHandler` instances and exposes `GetEffects()`. `TileEffectHandler.GetTemplate()` provides the underlying tile-effect template.

`Skill.GetHitchance(Tile from, Tile target, EntityProperties attacker, EntityProperties defender, bool includeDropoff, Entity overrideTargetEntity, bool forImmediateUse)` returns the shared `HitChance` struct used by gameplay calculations. Smoke Overhaul adjusts only `HitChance.FinalValue` after the native calculation.

## Exported smoke data

The extracted game data shows smoke delivery skills referencing `tile_effect.smoke_grenade`, with carrier smoke also using `tile_effect.carrier_smoke_screen`. Units inside smoke receive the vanilla `effect.smoke` status. Historical exports include concealment, accuracy, suppression, damage sustained, defense and vision changes. Smoke Overhaul 0.8.3 disables every stat change in that status, preserving the status handlers, tile effects and smoke VFX. Native `TryCast<ChangeProperty>()` is necessary because IL2CPP collections expose base wrappers. Current 0.7.18 proxy metadata exposes `ChangeProperty.Properties` as `Il2CppStructArray<PropertyChange>` instead of the older scalar fields. The mod swaps only this stat array for an empty array and restores the exact original array on disable/unload; legacy scalar fields are neutralized to 0/1 with their value provider disabled.

## Design rule

The custom hitchance penalty counts intervening smoke tiles as full exposure and smoke on the shooter/target tiles as half exposure each. With the native `effect.smoke` AccuracyMult neutralized, this weighted rule is the sole smoke accuracy penalty while Smoke Overhaul is enabled.

## 0.7.18 cloud investigation (#85, 5 November 2026)

The fresh 10:41–10:43 PC log still loaded SmokeOverhaul 0.8.2. A player smoke throw
at 10:42:35 reached `TileEffectHandler.AssignToTile` at 10:42:37, where the mod logged
`tile_effect.smoke_grenade` and duration 2 -> 4 rounds. This is evidence of assignment
being entered, not of completion, visible VFX, or a persistent tile effect.

For native hash `494c9e21…`, disassembly separates visual creation from status stats:
`SpawnTileEffectHandler.Apply` (RVA 0x7B4B80) calls `Tile.AddEffect`; `AssignToTile`
(0x6F36D0) schedules the prefab callback (0x6F81A0) using the handler delay. That callback
instantiates the existing `SpawnObjectOrEffect` and stores `m_WorldObject`; it does not
read `effect.smoke` stat modifiers or use `BlockLineOfSight` as a visual-spawn gate.

0.8.4 therefore preserves that native path and adds debug-only observations of tile
membership, lifetime and visual-object presence/activity after the delay. No speculative
prefab replacement, forced extra smoke spawn or footprint change was introduced.

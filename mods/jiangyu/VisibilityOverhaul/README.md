# Visibility Overhaul 3.1.9

Visibility Overhaul increases tactical vision and adds movement-based infantry concealment.

## Runtime design

3.1.9 moves the flat vision bonus into MENACE's native `EntityProperties` rebuild path. Only two Harmony hooks are used:

1. `SkillContainer.Update` - resets the per-property-cycle guard.
2. `Skill.OnUpdate(EntityProperties)` - applies the flat `Vision` bonus and, for infantry, the movement-based concealment modifier once per property rebuild.

`EntityProperties.GetVision` is no longer patched. There is also no `Map.UpdateVisibilityForActor` patch, movement-finished hook, or manual/global visibility rebuild.

This mirrors how vanilla `ChangeProperty` effects modify Vision. MENACE's own `SkillContainer.Update` compares the old and rebuilt `GetVision()` values and marks `PropertyDirtyFlag.Vision` when the value changes, so the normal fog-of-war/visibility lifecycle remains fully game-owned.

## Default settings

- Visibility Overhaul: On
- Flat vision bonus: +10
- Movement-based infantry concealment: On
- Stationary concealment bonus: +10
- Concealment lost per tile moved: 1
- Maximum concealment penalty below vanilla: -10
- Debug logging: Off

## Notes

The configured vision bonus is additive to the raw rebuilt `EntityProperties.Vision` value, before the game's normal `VisionMult`/`GetVision()` handling. Existing vanilla bonuses and penalties therefore continue to compose normally.

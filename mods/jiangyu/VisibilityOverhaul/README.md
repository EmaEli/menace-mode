# Visibility Overhaul 3.2.0

Visibility Overhaul increases tactical vision and adds movement-based infantry concealment.

## Runtime design

The gameplay path moves the flat vision bonus into MENACE's native `EntityProperties` rebuild path and uses two Harmony hooks:

1. `SkillContainer.Update` - resets the per-property-cycle guard.
2. `Skill.OnUpdate(EntityProperties)` - applies the flat `Vision` bonus and, for infantry, the movement-based concealment modifier once per property rebuild.

`EntityProperties.GetVision` is no longer patched. There is also no `Map.UpdateVisibilityForActor` patch, movement-finished hook, or manual/global visibility rebuild.

The optional pirate diagnostics add two observer-only postfix hooks for `Actor.HasLineOfSightTo` and `Actor.IsHiddenToPlayerAtTile`; they are gated by both diagnostics settings and do not invoke native visibility queries.

This mirrors how vanilla `ChangeProperty` effects modify Vision. MENACE's own `SkillContainer.Update` compares the old and rebuilt `GetVision()` values and marks `PropertyDirtyFlag.Vision` when the value changes, so the normal fog-of-war/visibility lifecycle remains fully game-owned.

## Default settings

- Visibility Overhaul: On
- Flat vision bonus: +10
- Movement-based infantry concealment: On
- Stationary concealment bonus: +10
- Concealment lost per tile moved: 1
- Maximum concealment penalty below vanilla: -10
- Debug logging: Off
- Pirate visibility diagnostics: Off (requires Debug logging)

## Notes

The configured vision bonus is additive to the raw rebuilt `EntityProperties.Vision` value, before the game's normal `VisionMult`/`GetVision()` handling. Existing vanilla bonuses and penalties therefore continue to compose normally.

## Pirate visibility diagnostics

The optional **Pirate visibility diagnostics** setting requires **Debug logging** and is off by default. It does not change visibility or invoke native visibility tests. It samples at most three pirate infantry actors and the allied observer with the smallest observed coordinate tile-span. It logs only initial states, a native `Actor.HasLineOfSightTo` result transition, a transition in `Actor.IsHiddenToPlayerAtTile`, or a sampled coordinate-tile-span transition from 2+ to 1; it stops after 100 events per scene. With diagnostics off, the hooks perform only their setting guards; they make no extra game calls and emit no routine logs.

Snapshots include raw/effective Vision, Detection and Concealment before/after VO, property/owner pointers, VO mutation/evaluation counts, update-call ID, movement count, coordinates, native predicate results and final hidden/visible state where that hook is called. Exact distance, geometric LOS separately from spotting, terrain/cover contributions, turn/stance, mission seed and active-mod list are reported as unavailable when not exposed by these hooks. The coordinate tile-span is not claimed to be the game's distance metric. The open-field range is an estimate based on the formula recorded in the issue, not proof of the v0.7.17 implementation. This logging is evidence collection only: it does not alter the +10 Vision / stationary infantry +10 Concealment design or apply a fixed spotting range.

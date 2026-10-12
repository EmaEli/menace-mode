# MENACE native Vision property pipeline

Verified against the current project `dump.cs`, `GameAssembly.dll`, and generated `Il2CppAssemblies` supplied with the 2026-10-01 game dump.

## Relevant game API

- `EntityProperties.Vision` is the raw additive vision stat.
- `EntityProperties.VisionMult` is the normal multiplier stage.
- `EntityProperties.GetVision()` (RVA `0x677530`) returns effective vision.
- `EntityPropertyType.Vision = 0` and `EntityPropertyType.VisionMult = 20` are supported by the generic property-change system.
- `PropertyDirtyFlag.Vision = 1` is the native dirty flag used to trigger visibility-dependent refresh work.
- `Map.UpdateVisibilityForActor(Actor)` (RVA `0x682CD0`) and `Map.UpdateVisibility(Tile,int,int,Actor)` (RVA `0x682D90`) consume the actor's rebuilt vision state for fog-of-war visibility.

## Critical SkillContainer.Update behavior

`SkillContainer.Update()` (RVA `0x760BC0`) performs the relevant lifecycle in this order:

1. Reads the owner's old effective `GetVision()` value.
2. Rebuilds `EntityProperties` and runs skill `OnUpdate(EntityProperties)` handlers.
3. Reads the rebuilt effective `GetVision()` value.
4. Compares old vs. new vision.
5. If changed, calls `Actor.SetPropertyDirty(PropertyDirtyFlag.Vision)` (RVA `0x611670`) through the native game path.

This means a modded additive `Vision` change made during `Skill.OnUpdate(EntityProperties)` participates in the same dirty-flag lifecycle as vanilla property-changing skills. A separate `GetVision` postfix or direct map/FOW rebuild is not required.

## Visibility Overhaul 3.1.9 design consequence

Visibility Overhaul now adds its configured flat vision bonus once to the rebuilt `EntityProperties.Vision` value during `Skill.OnUpdate(EntityProperties)`. The existing per-property-cycle guard prevents one bonus per skill. Movement-based infantry concealment remains on the same rebuild hook.

No patches are used on:

- `EntityProperties.GetVision`
- `Map.UpdateVisibilityForActor`
- `Map.UpdateVisibility`
- movement-finished callbacks
- selection/UI refresh callbacks

This is intentionally the narrowest integration point currently known to preserve MENACE's own visibility lifecycle.

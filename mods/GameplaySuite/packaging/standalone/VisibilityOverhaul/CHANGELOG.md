## 3.1.9

- Reworked the flat vision bonus to use MENACE's native `EntityProperties` rebuild path instead of a postfix on `EntityProperties.GetVision()`.
- `Skill.OnUpdate(EntityProperties)` now adds the configured bonus directly to the rebuilt raw `Vision` stat once per property cycle, matching the same stage used by vanilla `ChangeProperty` effects.
- Removed the `EntityProperties.GetVision` Harmony patch entirely; runtime hook count drops from three to two.
- MENACE's own `SkillContainer.Update()` now sees the changed effective vision during its old/new comparison and can set `PropertyDirtyFlag.Vision` through the normal game path.
- No `Map.UpdateVisibilityForActor`, movement-finished, selection, per-frame, or manual/global visibility rebuild hooks were added.
- Movement-based infantry concealment behavior is unchanged and remains on the same property rebuild hook.
- Debug logging remains OFF by default.

## 3.1.8

- Rolled runtime integration back to the stable 3.1.4 design.
- Removed the `Map.UpdateVisibilityForActor(Actor)` hook and temporary raw `Vision` / `VisionMult` overrides introduced by 3.1.7.
- Visibility Overhaul again uses only three hooks: `SkillContainer.Update`, `Skill.OnUpdate(EntityProperties)`, and `EntityProperties.GetVision`.
- No automatic full-map or movement-triggered visibility rebuild is performed.
- Flat vision bonus and movement-based infantry concealment are unchanged.
- This intentionally restores the old stability tradeoff: visual fog refresh may wait for MENACE's normal selection/state refresh.

## 3.1.7

- Fixed the early hard/native crash introduced by 3.1.6 while keeping the corrected extended LoS behavior.
- Removed the second `Map.UpdateVisibility(...)` call from the `Map.UpdateVisibilityForActor()` postfix.
- The native visibility method now runs exactly once. A scoped prefix temporarily exposes the bonus-aware effective vision through the raw `Vision`/`VisionMult` fields, and the postfix restores the original values immediately afterward.
- Prevents double application of the +vision bonus during the scoped native pass.
- No global visibility rebuilds and no movement/turn hooks.

## 3.1.6

- Fixed the vision bonus collapsing back to vanilla after player movement.
- Removed the 3.1.5 manual `TacticalState.OnMapReady` / `TacticalManager.InvokeOnMovementFinished` visibility rebuild path and all deferred `RecalculateVisibilityBasedOnVision()` state.
- Added one narrow postfix on `Map.UpdateVisibilityForActor(Actor)`. MENACE still performs its native visibility update first; the mod then extends that same actor/faction visibility pass using bonus-aware `EntityProperties.GetVision()`.
- No global visibility clear/rebuild is initiated by Visibility Overhaul. This also avoids disturbing unrelated tactical UI/highlight state during movement.
- Debug counters now report native visibility extensions/failures rather than manual rebuilds.

# Changelog

## 3.1.5

- Fixed extended-vision terrain fog-of-war lag where enemy icons could be visible beyond the revealed map area.
- Added one deferred visibility recalculation after tactical map-ready and after player/player-AI movement finishes.
- Requests are coalesced; AI movement does not trigger recalculation and there is no per-frame/full-map refresh loop.
- Added diagnostics counters for targeted rebuilds and failed rebuild attempts.
- Debug logging remains OFF by default.

## 3.1.4

- Reduced runtime logging to zero by default.
- All Visibility Overhaul informational, patch-status, settings, scene and patch-error messages are now emitted only when Debug logging is enabled in Mod Options.
- Debug logging remains Off by default.
- No gameplay behavior changed from 3.1.3.
- Cleaned the manifest description so it no longer refers to the withdrawn seamless-visibility refresh experiment.

## 3.1.3

- Stability rollback after the 3.1.x seamless-visibility experiment.
- Removed all automatic full-map visibility recalculation calls.
- Removed the tactical movement-finished visibility refresh hook.
- Removed the multi-pass mission bootstrap refresh.
- Removed the Seamless extended vision refresh Mod Option because it did not eliminate MENACE's selection-driven fog-of-war refresh and introduced unnecessary risk in a hot tactical subsystem.
- Core configurable vision bonus and movement-based infantry concealment are unchanged.
- Vision changes still apply through EntityProperties.GetVision. MENACE may not visually rebuild all fog-of-war until its normal selection/state refresh occurs; this is now documented as a known limitation rather than forced by the mod.
- Runtime hook count reduced from four to three.

## 3.1.2

- Experimental seamless-vision bootstrap and movement refresh. Withdrawn in 3.1.3 for stability.

## 3.1.1

- Fixed Harmony namespace/type collision by explicitly using global::HarmonyLib.Harmony.

## 3.1.0

- Added configurable vision and concealment Mod Options.
- Modernized project/build/release structure.
- Added diagnostics and independent Harmony patch binding.
- Added bounded movement concealment penalties.

## 3.0.0

- Initial modernization of the legacy Visibility Overhaul mod.

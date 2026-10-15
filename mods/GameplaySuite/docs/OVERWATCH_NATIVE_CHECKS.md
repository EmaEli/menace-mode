# Overwatch native firing-sector check (#23)

Inspected locally: MENACE 0.7.16 `GameAssembly.dll` and matching Il2CppDumper output.
Addresses below are RVAs, evidence for this binary only, not hardcoded patch addresses.

- `Skill.IsApplicableTo(Tile, Entity)`, RVA `0x75BAB0`: iterates skill event handlers and
  invokes applicability; the dispatcher itself has no range/facing test.
- `Skill.IsInRange(Tile, Tile, bool)`, RVA `0x75BDF0`: when the skill is ranged and the
  boolean is true, a `RangeShape.Cone` template compares the horizontal target direction
  against the owning entity's facing and the template's `ConeAngle`. It then checks native
  min/max distance. The integer-distance overload alone does not cover the cone.
- `Skill.IsUsableOn(Tile, Tile, TargetUsageParams)`, RVA `0x75C240`: contains the same cone
  comparison when `IncludeDeploymentAndAngle` is enabled. Calling full usability is not
  appropriate for reserved Overwatch shots: their actor intentionally has no normal AP.
- `RangeShape` is `Circle=0`, `Cone=1`. The angle check observes native ownership/facing;
  the mod should not replace it with a universal 90/180-degree hull/turret rule.

## Integration

Vehicle `Native.CanReact` explicitly calls `skill.IsInRange(targetTile, sourceTile, true)`
and rejects exceptions. Existing `EvaluateSkill` invokes `CanReact` before enqueueing for
each weapon; `ProcessReaction` invokes it again before ammo reconciliation or native `Use`.
`FinishReaction` already clears pending and charges a reaction only if a shot was accepted.
No extra rotation, ammo/AP debit or changes to queue/turn scheduling were added.

## What this does not prove

This restores the game's native cone gate that Overwatch previously bypassed. It is not
proof that every visual turret stop is represented by `RangeShape/ConeAngle`, nor a live
reproduction of the captured heavy-tank report. Circle-range weapons have no native cone
in this API. Model-specific traverse restrictions and hull/turret animations remain an
in-game acceptance question; do not invent additional angles from model appearance.

Offline source invariants and release compilation cover wiring, not execution of this
IL2CPP function or actual front/side/rear firing behavior. Keep #23 open pending those tests.
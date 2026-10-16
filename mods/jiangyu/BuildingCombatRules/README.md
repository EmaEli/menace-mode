# Building Combat Rules 0.2.0

A focused MENACE combat-structure rules module.

## Small-arms structure immunity

Direct-fire small arms can still hit infantry garrisoned inside a building, but their collateral `DamageInfo` against the building itself is suppressed. Pistols, SMGs/PDWs, carbines, rifles, assault/battle/sniper/marksman rifles, shotguns and light machine guns are treated as small arms.

The same immunity now covers native **vegetation structures (trees and forest)**,
including the attack's half-cover destruction path. The separate **Small arms cannot
damage trees and forest** option is ON by default; the existing building option/key
is unchanged. Native structure classification is used, not translated object names.
Forest movement, visibility and cover values are unchanged. Local 0.7.17 build and
managed guard tests passed; new tree behavior is not yet accepted in game (#48).

Explosives, launchers, mortars, rockets/RPGs, grenades, flamethrowers, tripod/crew-served weapons, medium/heavy machine guns, anti-materiel rifles, ATGM/anti-tank weapons, autocannons/cannons and similar structural weapons are not changed and can still damage buildings.

Those exclusions also apply to trees. Explosive/heavy skill identifiers take priority
over an item's rifle tag, including rifle-grenade modes. Ordinary props are unchanged.

## Safe garrison collapse

For the exact multi-tile civilian structures made enterable by Garrisonable Buildings, an occupied lethal collapse is deferred instead of destroying the structure in the same call stack as occupant ejection. The module uses MENACE's native ejection path when needed, waits until the occupant is detached and its movement has finished, then resumes the original native `Structure.Die()` call.

After the occupant exit has completed, or immediately for an empty structure, only those exact multi-tile templates temporarily restore the two vanilla template fields changed by Garrisonable Buildings (`ContainerType=None` and no inside-cover profile) while `Structure.Die()` executes. The garrison template is restored immediately afterward. This specifically targets the reproduced `building_civilian_shelter_1x2_01` hard crash without changing vanilla building-container behavior globally.

## Compatibility

This does not replace Cover Destruction Rebalance. CDR can continue to rebalance normal cover destruction separately; Building Combat Rules only guards `Structure` damage/collapse behavior.

Debug logging is OFF by default.


### Garrisonable Buildings compatibility

0.1.2 added a narrow exception guard for `Actor.GetCurrentProperties()` calls originating from Garrisonable Buildings. If MENACE invalidates an actor between the mod's reflection probe and invocation, the failed read is treated as unavailable (`null`) instead of propagating a `TargetInvocationException`. Calls from the base game and other mods retain normal exception behavior.

# Building Combat Rules 0.3.0

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

## Compatibility

This does not replace Cover Destruction Rebalance. CDR can continue to rebalance normal cover destruction separately; Building Combat Rules only guards `Structure` damage behavior.

Debug logging is OFF by default.

### Garrisonable Buildings

Versions 0.1.2 to 0.2.0 also contained the safe-collapse logic for occupied multi-tile garrisons (deferred `Structure.Die()`, native occupant ejection, template normalization) and the `Actor.GetCurrentProperties()` exception guard. Since 0.3.0 that logic lives in Garrisonable Buildings 1.1.0 (GitHub #50), where the crash it prevents originates. Building Combat Rules no longer patches `Structure.Die`, so it can be installed with or without Garrisonable Buildings.

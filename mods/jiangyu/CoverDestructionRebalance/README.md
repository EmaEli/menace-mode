# Cover Destruction Rebalance 0.4.1

Makes MENACE half-cover harder to erase and protects field fortifications from light/medium infantry mortar HE.

## Rules

- Vanilla `DestroyHalfCover=1` stays `1`.
- Vanilla `2` becomes `1`.
- Vanilla `3` becomes `2`.
- Exception: `active.laser_rifle_gunslinger` becomes `1`.
- `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` are forced to `DestroyHalfCover=0`, so sandbags/low walls represented as half-cover are not destroyed by light or medium infantry mortar impacts.
- 120 mm mortar attacks are not part of the mortar-immunity exception and follow the normal tier rebalance.

Only the half-cover destruction class changes here. Mortar accuracy/dispersion and suppression are handled by Weapon Mechanics Rebalanced 0.1; mortar ranges and the long-range indirect-fire AI positioning fix remains in Weapon Ranges Rebalanced 2.8.1. Artificial repetition salvos are not used.

## Current-patch compatibility

The Jiangyu package applies authored KDL patches to the supported skills; unlike the legacy mod, it does not dynamically scan every skill for the general tier rule. Its mortar immunity is intentionally scoped to the light and medium infantry HE mortar attack skills listed above.

Version 0.4.1 restores the runtime `AttackHandler.OnTileHit` guard from legacy 0.3.1, which was omitted in Jiangyu 0.4.0. The guard reasserts `DestroyHalfCover=None` on the actual handler before every native impact, including area-of-effect tiles and handlers replaced after template application. It does not skip normal impact handling or change damage, suppression or 120 mm attacks.

This is a candidate for [issue #58](https://github.com/EmaEli/menace-mode/issues/58), not an in-game verified fix. It protects the half-cover destruction path; it does not make all structures immune to direct damage.

## Logging

Successful template application is silent. Only real failures are logged; there is no per-skill or per-frame logging.

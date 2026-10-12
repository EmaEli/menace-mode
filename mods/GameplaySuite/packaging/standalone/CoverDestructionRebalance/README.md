# Cover Destruction Rebalance 0.3.1

Makes MENACE half-cover harder to erase and protects field fortifications from light/medium infantry mortar HE.

## Rules

- Vanilla `DestroyHalfCover=1` stays `1`.
- Vanilla `2` becomes `1`.
- Vanilla `3` becomes `2`.
- Exception: `active.laser_rifle_gunslinger` becomes `1`.
- `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` are forced to `DestroyHalfCover=0`, so sandbags/low walls represented as half-cover are not destroyed by light or medium infantry mortar impacts.
- 120 mm mortar attacks are no longer part of the mortar-immunity exception and follow the normal dynamic tier rebalance.

Only the half-cover destruction class changes here. Mortar accuracy/dispersion and suppression are handled by Weapon Mechanics Rebalanced 0.1; mortar ranges and the long-range indirect-fire AI positioning fix remains in Weapon Ranges Rebalanced 2.8.1. Artificial repetition salvos are not used.

## Current-patch compatibility

The mod dynamically scans the current `SkillTemplate` collection and edits handlers exposing `DestroyHalfCover`, so the general tier rebalance does not depend on a stale hardcoded attack list. The mortar immunity is intentionally scoped to the current light and medium infantry HE mortar attack skills.

Version 0.3.1 also installs a runtime `AttackHandler.OnTileHit` guard for those two mortar skills. This closes the load-order/template-rebuild hole where MENACE could later replace the startup-patched handler and restore cover destruction. The guard runs before native impact handling and forces that mortar handler's `DestroyHalfCover` to `None` before any sandbag/low-wall destruction can occur.

## Logging

Successful template application is silent. Only real failures are logged; there is no per-skill or per-frame logging.

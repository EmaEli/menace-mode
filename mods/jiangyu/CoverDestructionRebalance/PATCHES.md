# Cover Destruction Rebalance v0.4.1 - Jiangyu rules

The general rebalance is authored in `templates/half-cover-destruction.kdl` for the supported skills.

The authored handler values implement the following vanilla-to-rebalanced tiers:

- `None (0)` -> unchanged
- `Light (1)` -> unchanged
- `Medium (2)` -> `Light (1)`
- `Heavy (3)` -> `Medium (2)`
- Exception: `active.laser_rifle_gunslinger` `Heavy (3)` -> `Light (1)`

Unlike the legacy startup scan, new skills and handler-index changes require reviewing the KDL patches after game updates.

## Mortar exception

After the general rule, `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` are forced to `DestroyHalfCover=None (0)`. 120 mm mortar skills are not exempt and use the general tier rule above.


## Runtime mortar guard (0.4.1; legacy 0.3.1 behavior restored)

The KDL template pass is retained, but light/medium mortar protection is also enforced immediately before native `AttackHandler.OnTileHit(Skill, Tile, Tile, Vector3)` through Jiangyu's mod-owned prefix. It changes the actual handler's attack template, not just a startup lookup. It never skips the original method, restores an obsolete destruction value, or alters 120 mm attacks. The guard is silent in normal play and warns once if execution fails. Jiangyu owns patch-registration diagnostics and unload cleanup.

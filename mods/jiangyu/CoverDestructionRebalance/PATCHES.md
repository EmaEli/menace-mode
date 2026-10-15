# Cover Destruction Rebalance v0.3.1 - dynamic patch rule

The mod no longer keeps a fixed skill-by-skill list.

At startup it obtains every current `SkillTemplate` through `DataTemplateLoader.GetAll<T>()`, then inspects each event handler. If a handler exposes `DestroyHalfCover`, the following rule is applied:

- `None (0)` -> unchanged
- `Light (1)` -> unchanged
- `Medium (2)` -> `Light (1)`
- `Heavy (3)` -> `Medium (2)`
- Exception: `active.laser_rifle_gunslinger` `Heavy (3)` -> `Light (1)`

This keeps the original rebalance while automatically following weapon/skill additions in later MENACE patches.

## Mortar exception

After the general rule, `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` are forced to `DestroyHalfCover=None (0)`. 120 mm mortar skills are not exempt and use the general tier rule above.


## Runtime mortar guard (0.3.1)

The startup template pass is retained, but light/medium mortar protection is also enforced immediately before native `AttackHandler.OnTileHit`. This prevents later template/handler replacement from restoring `DestroyHalfCover` for those two skills. The guard is silent in normal play and only logs if installing or executing the guard itself fails.

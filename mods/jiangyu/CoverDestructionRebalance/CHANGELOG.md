0.4.1 (candidate; #58 gameplay verification pending)
- Restores the light/medium infantry mortar impact-time guard omitted during the Jiangyu migration to 0.4.0.
- Uses Jiangyu's mod-owned AttackHandler.OnTileHit prefix; checks the live handler at every impact, including replacement handlers and area-of-effect tiles.
- Keeps normal damage/effects and all 120 mm mortar/general tier rules unchanged. Successful impacts are silent; guard failures warn once per initialization.

0.4.0
- Jiangyu migration: authored KDL template patches replaced the legacy dynamic startup scan. The legacy runtime mortar guard was omitted.

0.3.1
- Fixes light/medium mortar sandbag protection when MENACE or Modpack Loader rebuilds skill handlers after the startup template pass.
- Adds a silent runtime `AttackHandler.OnTileHit` guard that forces `DestroyHalfCover=None` for `active.launch_light_mortar_frag` and `active.launch_medium_mortar_frag` immediately before native impact handling.
- 120 mm mortar and the general cover-destruction tier rules are unchanged.

0.3
- Narrows mortar half-cover immunity to the light and medium infantry HE mortar skills.
- Those two skills stay at `DestroyHalfCover=0`, protecting sandbags/low-wall half-cover.
- 120 mm mortar skills now follow the normal dynamic cover-destruction tier rebalance.

0.2
- Mortar HE no longer destroys half-cover objects such as sandbags and low walls (`DestroyHalfCover=0` for the current light, medium and 120 mm HE mortar skills).
- Preserved the accepted general cover-destruction rules for non-mortar attacks.
- Successful startup/template application is now silent; only real failures are logged.

0.1.4
- Compatibility fix: resolve SkillTemplate.EventHandlers and Attack.DestroyHalfCover as interop fields or properties instead of assuming raw fields.
- No balance changes.

Cover Destruction Rebalance 0.1.4
- Updated for the current MENACE API verified from the fresh dump.
- Replaced the hardcoded skill/handler manifest with a dynamic scan of all current SkillTemplate entries.
- Newly added attacks are now covered automatically when they expose DestroyHalfCover.
- Removed per-skill missing/unexpected warnings from normal operation; startup logging is now one summary plus real errors.
- Gameplay rules are unchanged: 1 stays 1, 2 -> 1, 3 -> 2, with active.laser_rifle_gunslinger 3 -> 1.

Cover Destruction Rebalance 0.1.2
- Fixed runtime type resolution for MelonLoader/Il2CppInterop: MENACE wrappers use the Il2CppMenace.* namespace.
- Added fallback lookup across all loaded assemblies and legacy Menace.* names.
- Added clearer retry diagnostics when template types are not ready yet.
- No gameplay rebalance values changed from 0.1.1.

Cover Destruction Rebalance 0.1.2
---------------------------------
- Fixed the build/deploy script so the MENACE game path is detected automatically or requested when needed.
- No gameplay features or rebalance values changed from 0.1.

Cover Destruction Rebalance 0.1
-------------------------------
- Initial release.
- Reduced DestroyHalfCover 2 -> 1 for selected attacks.
- Reduced DestroyHalfCover 3 -> 2 for other heavy attacks.
- Kept Construct Gunslinger Laser Rifle as a specific 3 -> 1 exception.

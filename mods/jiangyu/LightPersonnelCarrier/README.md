# Light Personnel Carrier 0.1 (authoring prototype)

**Not compiled, not installable, and not in-game verified.** See issue #76.

- New deployable 1-squad troop carrier cloned from MENACE's `player_vehicle.modular_light_troop_carrier`. Original stats: 250 HP, 125 frontal armor, 100 AP.
- No weapon hardpoints: separate `ModularVehicleTemplate` clone with empty `Slots`.
- Uses vanilla `carrier_light_static` Tank Chassis scenery prefab, which visually has a tarp covering the rear; the scenery prefab may be incompatible with the live vehicle's animator.
- New independent `VehicleItemTemplate` item, `vehicle.chassis_light_personnel_carrier`, costs 150 supplies to deploy instead of the original carrier's 220.
- Does not change existing vehicles or static map props; no logging.

## Build/test on Windows

Use the project in a configured Jiangyu authoring environment with game assets indexed; run `jiangyu compile --clean --release` from this module directory. Inspect compiler output and test on a backup save before distributing. Nothing in this branch is ready to ship.

Confirm: all three clones compile, prefab exists in asset registry; custom item is obtainable/equippable, 150 supply cost applies, no weapon slots appear, native embark/disembark one squad, movement/tracks/animator function, death/damage, save/reload and coexistence with other mods.

The scenery prefab is the highest-risk detail: `carrier_light_static` may not have the bones/components of `el.carrier_open_transport`. For the first diagnostic pass switch the prefab line to `append "Prefabs" asset="el.carrier_open_transport"` and recompile. This verifies the unarmed gameplay variant but *not* the tarp. If it succeeds, use `jiangyu unity import-prefab` on both vanilla assets to build a dedicated mobile prefab in Unity, preserving the mobile carrier rig and transplanting tarp visuals.

Template registration may not automatically place a newly cloned item in shops or the player's inventory; its acquisition route also requires verification.

No 2-squad mechanics. Keep logs disabled by default. Do not close issue #76 without in-game validation.

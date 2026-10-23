# Light Infantry Carrier (Utility) 0.1.2

Experimental, data-only Jiangyu package for issue #76. It adds an independently identified
single-squad utility derivative of the Light Infantry Carrier, with no vehicle deployment
supply cost. It is excluded from the default Suite. Version 0.1.1 made the item common,
allowed up to ten copies in the Black Market, gave it one light weapon slot but no accessory
slots, and reduced directional armor by about 10%. Version 0.1.2 changes only the visible
name and description; internal package and template IDs remain stable.
The 0.1.1 install was loaded successfully and the user confirmed that the vehicle works.
Version 0.1.2 passed authoring compilation; its canonical install and in-game name check are
tracked in the repository-wide migration status.

## Design

- Clones `player_vehicle.modular_light_troop_carrier` to retain native movement, cargo,
  damage, and one-squad transport behavior; armor is set to 113/99/81 versus 125/110/90.
- Names the visible variant `Light Infantry Carrier (Utility)` to distinguish its reduced
  armor and equipment capacity from the standard Light Infantry Carrier.
- Clones `modular_vehicle_carrier` and keeps only its light slot at `chassis_mount_02`.
- Clones `vehicle.chassis_carrier` as a separate item with zero deployment supplies, no
  accessory slots, rarity 0, and a market quantity cap of 10.
- Uses the scenery prefab `carrier_light_static` for the tarp-covered appearance.

## Verification still required

`carrier_light_static` is a static scenery prefab, not a mobile-vehicle donor. Its rig,
renderer and animator compatibility must be checked in game. If it fails, first test with
`el.carrier_open_transport`; a tarp-bearing mobile prefab may require Unity authoring.
Also verify that the cloned item is obtainable/equippable, a light weapon mounts correctly,
no accessory slots appear, and embark/disembark, movement,
damage, death, save/reload and mod coexistence work. The source is not ready for release
or issue closure until those checks pass.

The 0.1.0 prototype compiled with `jiangyu compile --clean --release`; the 0.1.1 changes
passed authoring compilation and canonical Windows BuildOnly/install. The installer consumes
the resulting precompiled package; it does not author template data or require Unity on the
game PC. Version 0.1.2 changes only the display name/description and passed authoring
compilation. Its canonical installation and in-game name check are tracked in
the repository-wide migration status. The user confirmed the 0.1.1 vehicle works; the
`carrier_light_static` scenery prefab's mobile rig/animator compatibility remains a known area
for further testing.
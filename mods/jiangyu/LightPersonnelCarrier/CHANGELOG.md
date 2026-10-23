## 0.1.2 - utility display name (2026-10-23)

- Rename the visible vehicle and chassis to Light Infantry Carrier (Utility); internal IDs stay unchanged.
- Clarify its stripped-down role: reduced armor, no accessory slots, and one light weapon slot.
- Recompiled with the pinned Jiangyu 1.4.8 / Unity 6000.0.82f1 authoring toolchain.

## 0.1.1 - common low-armour test candidate (2026-10-23)

- Set item rarity to 0 and Black Market maximum quantity to 10.
- Remove accessory slots and set armor to 113/99/81 (about 10% below the donor's 125/110/90).
- Set vehicle deployment cost to 0 supplies and retain one light weapon slot at `chassis_mount_02`.
- Canonical Windows BuildOnly/install passed; user confirmed the vehicle works and the fresh log records version 0.1.1 loaded.

## 0.1.0 - experimental authoring prototype (2026-10-23)

- Add independent one-squad unarmed transport templates with a 150-supply deployment cost.
- Use the vanilla static light-carrier tarp prefab as an unverified appearance donor.
- Jiangyu 1.4.8 / Unity 6000.0.82f1 authoring compilation succeeded.
- Canonical Windows standalone BuildOnly/install passed; installed manifest, templates and loader hash verified, with backup retained.
- Windows build used SDK 10.0.113 and had zero errors; the shared settings build emitted one JIA005 warning.
- Item availability, mobile rig compatibility and in-game behavior remain unverified.
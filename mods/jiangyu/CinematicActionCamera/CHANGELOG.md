# Changelog

## 1.2.3 — 5 November 2026

- Fix camera entry/exit after MENACE 0.7.18 moved TacticalCameraController from States to Tactical.
- Resolve the current or legacy IL2CPP proxy without a hard reference to either controller type; preserve and restore the existing SkipAutoFocus value.
- Add executable autofocus regression tests and a metadata-only camera build gate for native hooks and binary type references.
- Keep settings identities, controls, visibility restrictions and framing unchanged.
- Validation: local 0.7.17/0.7.18 builds and 65 camera checks per proxy set passed; the gate rejects the old 1.2.2 binary. Canonical Windows BuildOnly/install and installed 1.2.3 metadata/hashes passed on 0.7.18. All 1,397 protected Mods/UserData files were unchanged; backups retained outside Mods.
- Startup/gameplay retest remains pending. This does not address the separate Player.log PathMover exception.
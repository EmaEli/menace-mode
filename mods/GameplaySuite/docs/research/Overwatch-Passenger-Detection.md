# Open-top passenger Overwatch detection (#25)

## Evidence — 3 November 2026

The Overwatch 0.35.0.0 session showed 15 hostile AI passenger reservations, markers,
zero queued reactions and zero native reaction shots. Most candidates were outside
weapon range; the in-range rifle/RPG passenger failed actor visibility both at movement
finish and 165 ms later, and again after the player fired.

Pinned MENACE 0.7.17+22362 GameAssembly SHA256:
`871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`.

`Actor.HasLineOfSightTo(Entity, Tile, Tile)`, RVA `0x637400`:

- `0x637462`: query `Entity.IsContainedWithinAnotherEntity` on the observer.
- `0x63746b–0x63748a`: invoke the container's virtual slot 45 (`Entity.IsActor`).
  If true, branch straight to false at `0x63770e`.
- The rejection happens before source/target tile, tile LOS, vision or concealment.
  Supplying explicit tiles or waiting for visibility bookkeeping cannot fix it.

## Narrow 0.35.1.0 extension

Only Overwatch's own `Native.HasActorLineOfSight` dispatches actor-contained observers
to `PassengerOverwatch.HasLineOfSight`. It revalidates live allied native open-top
membership; closed/custom/stale containers fail closed. No Harmony visibility patch,
template change, temporary disembark or vehicle-property substitution is used.

The calculation follows the remaining native method:

1. Preserve tile LOS (`0x6374ea`), source-to-target distance and the passenger's effective
   `EntityProperties.GetVision()`.
2. A revealed Actor uses distance <= vision (`0x637576–0x637585`). Otherwise retain
   target concealment, observer detection and the native minimum detection range of 1.
3. A contained target at its actual tile uses the tile occupant's inside-cover
   concealment, with no tile concealment (`0x63758a–0x637640`).
4. Otherwise use directional `Tile.GetCover(direction, target, null, true)` and
   `Config.COVER_CONCEALMENT`, plus `Tile.GetConcealment()` (including smoke).
   Native cover concealment is ignored for adjacent tiles.
5. Detection range = max(1, vision - max(0, cover + target concealment + tile concealment
   - observer detection)), matching `0x6376ba–0x637706`.

Current properties still use the existing non-optimizing/non-inlining concrete actor
resolver from #44. Ordinary uncontained/building observers still call native visibility.
Range, target applicability, hit threshold, ammo, reservation and reaction execution are
unchanged. Tests execute production detection and `Native.CanReact` against stand-ins;
they do not prove native projectile execution. A fresh game test remains required.
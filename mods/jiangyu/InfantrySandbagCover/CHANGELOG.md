## 0.3.0 - Configurable stationary construction

- Add Construction turns (1–10, default 2) to MenaceModSettings; 80 AP per work action,
  at most once per tactical round. New setting values apply to new construction sites.
- Require the same stationary infantry squad and tile. Movement (including out-and-back),
  embarking, death and scene changes cancel unfinished work; stationary skipped rounds pause it.
- Add an amber world-space tile border and progress counter using the mortar marker pattern.
- Run native cover/environment effects only on completion; refund failed final placement
  without discarding earlier paid work. Preserve native skill checks/AP charging.
- Add managed production-hook/marker checks and metadata-only native contracts, run by the
  canonical installer before staging. Windows/gameplay acceptance is not implied by these tests.

## 0.2.0 - Immediate self-tile emplacement

- Remove the off-map crate projectile and target selection; deploy native half cover
  immediately on the acting squad's tile.
- Keep the fixed 80 AP cost, native cover/environment handlers and player-infantry scope.

## 0.1.0 - Initial experimental action (#89)

- Clone the native sandbag-cover skill as an ordinary active skill with an 80 AP cost.
- Preserve the native empty-tile target, three-tile selection range, crate effect and both
  native placement/environment handlers.
- Add the action only to player squads and player local-forces infantry.
- Canonical BuildOnly/install passed on 2026-11-05 against MENACE 0.7.18+22828; in-game
  AP/placement verification remains pending.
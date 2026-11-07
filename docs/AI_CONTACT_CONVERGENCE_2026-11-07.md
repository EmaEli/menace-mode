# Contact memory and unassigned approaches — 7 November 2026

Feature: [#17](https://github.com/EmaEli/menace-mode/issues/17).
First-stage implementation: **AIPerformance 1.3.0**, experimental option OFF by default.
This is not a fix or acceptance claim for the sparse-fire problem in #2.

## Contract

- Snapshot coordinates only on native `Actor.SetDetectedByFaction(int)` events,
  after the faction's detection bit is set and checked. Never poll remembered Actor/TTL
  lists for positions. Do not read a hidden target's position or death state.
- Only hostile pirate/Rogue factions collect player/player-AI/allied-local-force contacts.
  Civilians and other factions cannot become targets of this approach behavior.
- Store managed coordinates/identity/round only. Maximum 32 contacts; a lost contact
  expires after two rounds. The suspected area grows from 3 to 7 tiles.
- At most three responders per faction, with separate approach sectors and stable
  contact assignments. Arrival, two stationary rounds or three response rounds stop
  an attempt. A fresh sighting can authorize another; absent responders release slots.
- Only infantry without any faction mission zone, combat candidate/positive useful
  action or existing positive tile utility qualifies. Sleep, deployment, suppression,
  morale, retreat roles and movement restrictions are preserved. Support teams,
  indirect-fire assets, machines, xenos, drones and vehicles are excluded.
- Before native `Move.HasUtility`, otherwise zero-utility, no-less-safe destinations
  strictly closer to the approach area receive native movement utility threshold.
  Native Move still chooses destinations and calculates reachability, path and AP.
  No shared zone/order changes, forced activation, additional path search or Update loop.
- The independent option requires restart. Diagnostics default OFF, maximum 50 lines
  per mission. Mission initialization and preparation/strategy/title/unload clear state.
- Hash locked to reviewed MENACE 0.7.18+22828; no new 0.7.17 compatibility claim.

## Native integration review

Reviewed exact GameAssembly SHA256
`494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
The local dump and annotated disassembly show `SetDetectedByFaction` at RVA `0x644EC0`
setting the faction detection mask; `IsDetectedByFaction` at `0x63D8B0` reads that bit.
`AIFaction.OnOpponentSighted` at `0x7B9640` updates Actor/TTL entries and assessment,
not a last-known coordinate snapshot, so this implementation does **not** poll it.
`Move.HasUtility` at `0x7DC2C0` compares raw TileScore utility against the native
threshold. Existing native movement/execution remains in control after seeding.

## Automated/preflight evidence

- Fresh PC native binary matches the reviewed hash; all 119 PC proxy DLL hashes match
  local 0.7.18 references. Initial PC preflight found the game closed and AI 1.2.2 installed.
- Local Release build: zero warnings/errors; manifest/assembly/file 1.3.0/1.3.0.0/1.3.0.0.
- 199 Python regression tests, 18,000 scoped shared C# checks, 29 planning/task checks,
  12,562 recovery/contact checks and 105 native metadata contracts pass.
- Contact fixtures exercise hidden movement/death without position reads, reacquisition,
  observed-death invalidation, expiry, mission reset, responder cap under concurrency,
  separate sectors, no-progress stopping, order/combat/support/actor guards, native
  score/path/AP preservation, failed-write rollback and diagnostic-sink failure.
- The existing canonical builder includes these production-source tests in its
  AIPerformance bundle and runs them before Windows staging. No new installer is needed.

These checks do not establish native dispatch, effective in-game approach movement,
load/save behavior or 64x64/96x96 turn-time impact. Canonical deployment evidence is
recorded below once performed. The source intentionally remains OFF by default.

## Next stage: indirect fire (not implemented)

Last-known mortar fire, preparatory objective bombardment and off-map support remain
separate work. Verify each eligible native skill's unseen/empty-ground validation,
scoring and execution before adding candidates; do not infer feasibility from a
tile-based signature. Preserve range/AP/ammo, friendly/neutral blast safety and bounded
barrage budgets, and never score hidden actual occupancy as evidence.
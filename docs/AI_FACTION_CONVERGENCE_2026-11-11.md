# All-hostile faction mobilization — AIPerformance 1.12.0

Tracking: [#17](https://github.com/EmaEli/menace-mode/issues/17),
[#13](https://github.com/EmaEli/menace-mode/issues/13),
[#2](https://github.com/EmaEli/menace-mode/issues/2).

## Why this candidate exists

The player's 1.11.0 initiative/coordination feedback was negative. The fresh pirate mission
had 45 initial hostiles against eight player units, no native mission zones, no coordinator
contacts in 19 rounds, no published support roster/group evidence and 23 of 37 survivors
still asleep at round 19. Movement and late attacks alone were not accepted coordination.
See [the rejected gameplay evidence](AI_PIRATE_MOBILIZATION_FEEDBACK_2026-11-11.md).

The follow-up request covers all hostile factions, not only human crewed units. Eligible
units should converge on reported player sectors without needing a pre-existing mission
advance order. This does not authorize clairvoyance or replacement of explicit defense.

## Confirmed native sighting gap

Reviewed exact game 0.7.18+22828, native SHA256
`494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`:

- `Map.UpdateVisibility` at RVA `0x6A25E0` calls virtual `Actor.OnDiscovered` and then
  virtual `AIFaction.OnOpponentSighted` for the observing faction.
- `Actor.OnDiscovered` at RVA `0x640600` sets the faction's detection bit directly
  (`bts` and write to actor field `0x148`), rather than calling `SetDetectedByFaction`.
- `AIFaction.OnOpponentSighted` at RVA `0x7B9640` adds/refreshes its native opponent record.
- The previous mod hooked only `SetDetectedByFaction`, and its once-per-turn fallback ran
  before native turn-start visibility. Normal new sightings could therefore be missed.

This is a confirmed integration gap, not proof that every 1.11.0 idle choice has that cause.
A failed-first production-hook fixture reproduces the empty-refresh/missed-setter sequence;
it fails unchanged 1.11.0, then passes with the new native sighting hook.

## Behavior and boundaries

- Hook the native faction sighting event and refresh after native turn-start visibility.
  Both event and fallback retain native detection before position, weapon, faction or death
  reads. A callback alone is not permission to inspect an undetected enemy.
- Apply faction alert/convergence to hostile faction types 5–9, excluding player allies.
  Wildlife, bugs/aliens, constructs/drones and unrecognized machine vehicles are no longer
  excluded solely by human class/template tags. Creatures/machines do not require human
  morale; recognized human-crewed vehicles retain that guard.
- Give nearby units a shared contact-sector objective, held for two rounds to avoid
  evaluation-by-evaluation retargeting. Each faction knows only its own reported contacts.
  Forgetting a legitimately observed dead target releases its assignment immediately.
- Wake eligible sleeping units and favor existing local forward movement candidates while
  no useful native action exists. Rifle/direct-fire eligibility is no longer required.
  Movement also works when simplified skill-positioning profiles are disabled.
- Human combined-arms class guards remain narrow. Pirate/Rogue infantry without native
  orders can join existing optional local groups using managed contact objectives. These
  scalar snapshots are never written into native mission zones or actor order lists.
- Keep explicit defensive/mixed orders, deployment, movement-disabled/avoidant roles,
  containment, stun, human morale/suppression and native useful/emergency actions.
  Native Move owns pathfinding and actual AP cost; native Attack owns legal execution.
- Store at most 32 contacts per faction/512 total and 4,096 scalar unit assignments.
  Reports expire after 12 rounds; repeated same-turn/same-tile reports skip weapon scans.
  There is no new global path search, all-pairs unit scan or custom combat scheduler.
- Keep all diagnostic options/defaults unchanged. Existing explicit diagnostics add native
  sighting and faction-turn actor/assignment/awakening counts within the current line cap.
  No settings, saves, equipment, skills, ammunition or AP are granted or rewritten.

## Local checks

- Release build against real 0.7.18 proxies and pinned Jiangyu 1.4.10: zero warnings/errors.
- 19,680 linked recovery/contact/group checks; 303 settings/planning behavior checks.
- 223 real proxy/binary metadata contracts, including faction values and sighting signatures.
- 156 LargerMaps Python tests passed, with eight expected skips.
- 210 GameplaySuite Python regression tests passed in the shared worktree.
- New tests exercise 45 unordered units per hostile faction, simultaneous five-faction
  contact budgets, cached stable objectives, melee/non-rifle movement, machine morale,
  native attack priority, missing detection, worker callbacks, lost-contact coordinates,
  capacity/expiry/map reset, simplification OFF and real group publication/live refresh.
- 45 unordered human infantry form 12 bounded local groups without changing native zones.

These execute managed stand-ins or inspect native metadata; they do not execute IL2CPP
detours, demonstrate visible movement, prove real turn times or establish gameplay acceptance.

## Packaging, Windows and gameplay status

Canonical package/clean-source/Windows gates are pending at this point in the record.
Do not infer installation or gameplay success from the local checks above. #17, #13 and
#2 remain open. After a verified installation, try the unordered pirate encounter and a
bugs/constructs encounter: one legitimate sighting should alert more than the local squad,
and eligible enemies should keep closing on the reported sector rather than patrolling
the map edge. Native usable attacks must still win, without tracking hidden player moves.
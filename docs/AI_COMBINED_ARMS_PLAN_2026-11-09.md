# Combined-arms AI support — proposal, 9 November 2026

Tracking: [#13 tactical coordination](https://github.com/EmaEli/menace-mode/issues/13),
[#17 assignments](https://github.com/EmaEli/menace-mode/issues/17),
[#2 initiative](https://github.com/EmaEli/menace-mode/issues/2).
**Planning only: no new gameplay code, build, settings change or deployment.**

## Feedback and scope

User reports some improvement with AI Performance 1.5.0, but vehicles remain timid.
User endorses caution around spotted anti-tank weapons and requests infantry-led
vehicle/heavy-weapon support, not a general reduction of danger avoidance.
This is partial positive feedback, not acceptance of every #2/#17 requirement.
No fresh log retrieved: menace-pc was shut down at the user's request.

Initial scope: hostile pirate/Rogue infantry, their direct-fire weapon teams and
explicitly identified crewed combat vehicles/walkers. This extends #13's old vehicle
exclusion for this feature only; it does not enable robots, drones, xenos, player
units, scripted sleepers, transports without useful weapons or all MACHINE actors.
Keep the existing mission/sleep/retreat/defence constraints. No new units or abilities.

## 1. Infantry publishes a short-lived support request

- Record the squad's legitimate advance direction, next intended native movement
  area, known threat/contact area, urgency and request expiry. Mission direction
  can justify covering empty ground without inventing an enemy there.
- A provisional Move selection is intent, not completed movement. Cancel or refresh
  requests when native choices, orders, movement or combat state invalidate them.
- Requests mean "cover this advance/lane" or "help against this detected threat";
  they do not directly execute another actor's skill or grant extra activations.
  These are internal AI requests, not a new player button or radio/voice feature.
- Consider awake, mobile, advancing infantry only. Retreating, pinned or defending
  squads cannot drag supporting assets away from their responsibilities.
- Infantry without support keeps its ordinary legal plan. A request never becomes
  an indefinite wait or a requirement that every squad receive support.

## 2. Assign nearby support, with stable ownership

- For each available asset, prefer the closest feasible advancing infantry request
  in the same operational area. Use a local spatial index, not all-pairs pathfinding.
- Feasible means useful equipped weapon, compatible mission, plausible range/route,
  resources and acceptable known danger. Geometric proximity alone is insufficient.
- Initially allow one vehicle and one direct-fire heavy team per request; one
  primary request per asset. Other infantry may incidentally share its covering fire.
- Keep a valid assignment for roughly two rounds; switch early for danger, death,
  inability to support, changed orders or a clearly better urgent request. This is
  hysteresis: avoid changing partners for every small distance difference.
- Release stalled/expired requests and unsupported assignments. No global dogpile,
  abandonment of critical defences, or permanently reserved unavailable assets.

## 3. Vehicles support from behind, not merely nearby

- Desired separation: **3–10 native tile-distance units** from the supported squad;
  initial preferred centre 5–7, to tune in play. Verify the native distance metric
  before implementation; document diagonal behaviour rather than assume Manhattan.
- Determine "behind" from the squad's advance direction, not map north or a simple
  radius. Prefer rear/offset positions that can fire into the squad's approach area.
- Rank existing native movement candidates for useful line of fire, weapon range,
  cover/danger, rear spacing, route clearance and mission consistency. Vehicle
  footprint, infantry passage and friendly-fire safety remain authoritative.
- Hold and shoot if already providing useful support; do not move merely to attain
  an ideal distance. Catch up when infantry advances beyond useful support range.
- Spotted anti-tank danger, retreat/fire/defect states and native safety take priority
  over spacing: offset, hold, withdraw or release the assignment rather than charge.
  Do not infer an unseen weapon's loadout/location or make stale contacts track it.
- The 3–10 band is a tactical preference, not a teleport, path guarantee or hard
  constraint. Terrain/range/safety can justify exceptions; being safe behind a wall
  with no way to cover the infantry does not count as successful support.

## 4. Heavy weapons establish a useful base of fire

- Identify direct-fire support by actual weapon/skill capabilities, not just a tag.
  Mortars/indirect fire are a separate policy, not given a direct-fire LOS requirement.
- Inspect a small set of native candidate positions for visibility/line of fire
  into the infantry's intended approach and a few lane/likely-contact points.
  A geometric line alone does not establish a legal attack or weapon firing arc.
- Prefer a covered position with useful range/arc, not a hidden tile that sees none
  of the advance. Hold an existing useful deployed position rather than churn setup.
- Plan legal pack/move/deploy-or-setup steps, including their real AP/ammo costs;
  reserve setup and a shot where affordable. Multi-activation setup is permitted,
  but infantry must not wait indefinitely for it or treat it as already ready.
- Ready means setup complete, weapon usable, resources and an effective lane, not
  merely assigned or deployed. Revalidate each step and abort unsafe/illegal plans.
- Fire at detected enemies threatening the covered advance when a worthwhile legal
  attack exists; useful damage/suppression should beat pointless repositioning.
- When the advance leaves its lane, pack and establish the next position. Prefer
  one support asset holding while another relocates when available; no rigid
  requirement for two teams and no activation-order rewrite in the first version.

## 5. Covering fire is not free reaction fire

On the asset's activation, prefer real native attacks on detected threats. Automatic
fire during enemy movement is a distinct Overwatch integration, tracked in
[#55](https://github.com/EmaEli/menace-mode/issues/55). Verify hostile reservation,
AP ownership, trigger, native shot and completion before promising that behaviour.
If Overwatch is absent/disabled, support still works during ordinary activations.
The full requested "opens fire when spotted" reaction behaviour remains incomplete
until this path is verified for the relevant vehicles and deployed heavy weapons.
Do not add a second reaction engine, grant free AP/ammunition, shoot unseen actors,
or change suppression/damage mechanics. Coordinated Fire remains weapon execution,
not a competing owner of squad assignments.

## Implementation boundaries and unresolved contracts

Prefer the existing AIPerformance companion, one shared assignment owner and small
score adjustments; no new DLL/director, global pathfinder or native profile re-enable.
Current 1.5.0 `OffensivePlanningRuntime.Support` is only a proximity bonus. Its
projection excludes setup/deployment skills, and eligibility excludes MACHINE actors.
Do not simply relax these guards: add explicitly verified support eligibility and
setup-aware scoring without affecting existing organic infantry or robotic factions.

Existing `Agent.PostProcessTileScores`/`PickBehavior` integration and native movement
can be reused only after checking timing/interaction with recovery and attack scores.
Actor.IsVehicle/IsDeployed and LOS metadata, plus existing Overwatch requirement
checks, show relevant interfaces exist, not that native deployment hooks are proven.
First verify role classification, AT threat semantics, native pack/setup/deployment,
future-position arcs, intent publication timing and behaviour selection/execution.
No shared RoleData edits, forced deployment flags or direct skill execution bypasses.

Snapshot managed data on safe lifecycle hooks; workers read immutable snapshots and
cached IDs only. Guard map/round/actor generations and invalidate on mission unload.
Proposed bounds: four local requests per asset, eight existing tile candidates,
three lane samples each, and no extra path search. Charge **all** support/attack
native probes against a common hard budget, with per-asset and faction-round caps
fixed before coding. Repeated evaluations/events cannot refresh the spent budget.
Time checks are soft (cannot cancel a native call); exhausted budget skips new work.
Refresh assignments per round/relevant event, not per-frame. Debug OFF by default.

## Delivery order and acceptance

1. Verify contracts; implement/test request ownership and nearest-feasible assignment.
2. Prototype vehicle rear support, preserving known AT avoidance and useful attacks.
3. Add direct-fire heavy-team lane selection and legal deployment/fire progression.
4. Verify deliberate Overwatch reservation/reactions separately; add only if safe.
5. Compare short representative engagements and AI timing with the 1.5.0 baseline.

Cover-before-advance is best-effort under native activation order: count only
actually ready support. If that order cannot establish coverage first, do not claim
synchronised movement; evaluate safe scheduling separately without stalling infantry.

Test blocked routes, no support, multiple squads competing, partner death, changed
orders, lost visibility, deployment/AP limits, friendly lanes, spotted AT, withdrawal,
mission resets and repeated/concurrent evaluations. Cover both 64×64 and 96×96 timing.
Acceptance: closest feasible infantry supported without oscillation; vehicles usually
behind at 3–10 tiles when safe/useful; heavy weapons cover the intended lane and fire
when legal; infantry never deadlocks; no hidden information or material turn slowdown.
First prototype opt-in and reversible. #1 smoke and #2 remain prerequisites for the
broader integrated tactics release; this plan neither claims their acceptance nor
requires smoke for basic support. #13/#17 stay open pending implementation/gameplay.
# AI Performance 1.12.0

## Faction mobilization and shared contact objectives (0.7.18 test candidate)

The existing default-ON `OffensiveCombatPlanning` now covers all five hostile AI factions,
including wildlife, bugs/aliens and constructs. It remains independent of simplified combat
positioning; no new setting or player configuration change is required.

- Observe native `AIFaction.OnOpponentSighted`, not just `Actor.SetDetectedByFaction`:
  normal map visibility calls `Actor.OnDiscovered`, which writes the detection bit inline.
  A sighting still needs native faction detection before enemy position/weapon reads.
- Refresh after native faction-turn visibility, alert eligible sleeping units and assign
  stable two-round objectives toward reported contact sectors. Nearby units share a sector;
  this is not knowledge-sharing between different hostile factions or hidden tracking.
- Movement no longer requires human infantry, a crewed template, a rifle or a pre-existing
  mission advance order. Creatures/machines do not need a human morale percentage. Native useful
  actions, emergency behavior, deployment, explicit defense and movement-disabled roles win.
- Pirate/Rogue infantry without orders can join the existing opt-in combined-arms groups
  using managed contact objectives. No native mission zone is created or rewritten.
- Contact storage is bounded per faction; lost reports expire after 12 rounds. Assignment
  lookups are cached and bounded, repeated reports avoid repeated weapon scans, and no new
  pathfinder, faction-wide per-unit scan, attack scheduler, AP, ammunition or skill is added.

Managed regressions include 45 unordered units per hostile faction, simultaneous faction
memory, missed native sighting, stable assignments, hidden-read guards, native attack
priority and actual production group publication/refresh. Gameplay acceptance is pending.
See [scope and evidence](../../../docs/AI_FACTION_CONVERGENCE_2026-11-11.md); #17 remains open.

## Shared local advance windows (0.7.18 test candidate)

With the existing opt-in `CombinedArmsSupport` and default-ON `OffensiveCombatPlanning`:

- Nearby compatible advance-ordered infantry and support share small groups (up to four
  infantry/two support units), a known contact, cohesion limits and one crossing deadline.
  Publication is once per faction turn, with a bounded spatial lookup; no global pathfinding.
- Exposed lead infantry can stage for support; vehicles retain an infantry screen instead
  of independently rushing ahead. Actual accepted native fire at the contact sector opens
  a shared one-turn/next-turn advance window. Proximity or projected utility is not firing.
- Equipped smoke requests require affordable native movement afterward. Accepted forward
  smoke is shared, but must actually materialize before authorizing exposed crossing.
  Real smoked forward tiles get movement preference. No repeated smoke at the same place
  until the group's infantry has progressed three tiles. No mortar smoke skill/ammo is
  invented; equipped hostile mortar-smoke availability remains unverified.
- Waiting expires after two rounds; leader loss, regrouping or changing contacts do not
  renew spent waiting time. Native attacks/emergencies and defensive orders still win.
- Native zone scopes now work on AI workers, with thread/faction isolation, stale-generation
  invalidation and exception cleanup. A geometric lane with no native shot cannot park a
  unit forever. Pure movement clears the native 1024 post-utility AP sentinel without
  changing the real path cost or granting AP.

Both diagnostic options remain OFF by default. The existing explicit combined-arms logging
opt-in now includes contact/group traces, so no additional settings edit is needed for that
test setup. The 256-line contact budget reserves half for group fire/smoke/choice evidence;
repeated group choices are sampled to avoid consuming it in the opening rounds.

Managed checks and native metadata checks are not gameplay acceptance. #17 remains open.

## Contact-led alert, advance and firing cover (0.7.18)

The existing **Contact-led alert, advance and firing positions (pirate/Rogue)** option
(`OffensiveCombatPlanning`, default ON, restart required) now supplies the main policy.
No new gameplay setting or equipment is needed. This deliberately changes the rejected passive
1.8.1 behavior; the sections below describe older fallbacks, not an obligation to hold.

- Native mission advance zones (`AdvanceTowards`) keep pulling eligible units forward after
  contact. MENACE drops them for the whole faction once any squad has a known opponent,
  which left the 1.9.0 test's reserves in cover. Only inside the native zone scoring, for
  ready advance-ordered units that have no useful attack or suppression shot, the faction
  flag reads as "no known opponent"; engaged units and every other reader stay native.
- A legitimate faction sighting alerts eligible unassigned or advance-ordered infantry,
  direct-fire teams and recognized crewed vehicles. This is not limited to three scouts.
  Explicit defensive/mixed orders, deployment and non-human/drone/artillery roles remain native.
- Keep closing toward the observed area across turns, with five modest lateral approach
  preferences. A hidden opponent is only a last-known area: no hidden position/weapon
  refresh, no automatic fire at remembered ground. Stop searching on arrival; expire an
  unrefreshed area after 12 rounds. This is not an exact sector/formation director.
- Favor covered origins within actual own range and sampled line of sight; account for
  real deployment/setup/fire AP. When no useful firing lane is available, cover-only
  movement utility cannot end the advance. Native safety values, reachability, clipped
  paths, attacks, resources and emergency actions remain owned by MENACE.
- Already-positive native damage **or suppression-fire** candidates win over Move/Idle.
  No fabricated attack candidates or attack utility. Fast planning remains enabled.
- With installed **Smoke Overhaul 0.9.0**, exposed infantry can request an equipped,
  affordable, legal smoke throw inside observed enemy weapon range while lacking its
  own firing lane. Existing defensive smoke remains available. No smoke merely because
  the destination is distant; no free grenades/AP or replacement grenade executor.
- Scan only existing native local candidates (at most 4,096, within ten tiles); at most
  two direct-fire profiles and 16 range/LOS lane checks per evaluation, with a soft 10 ms
  probe-start limit. Damage/usability samples share 24/48/512 evaluation/actor/faction
  token limits; cheap geometric staging survives exhausted damage budgets. These are
  bounded checks, not a guaranteed frame-time bound or a new global pathfinder.

The combined-arms exposed-route hold is now limited to two rounds per unit per mission.
If exposure persists and no safe native route or smoke/fire response appears, the native
Move choice is allowed again on round three. Enable **Log bounded contact/advance hook
decisions** (`DiagnoseContactAdvance`, OFF by default) and restart to record contact
observation results, native-zone scope/thread eligibility, whether the native
`HasKnownOpponent` check was reached, and guard expiry. The trace is capped at 256
deduplicated lines per mission and does not change gameplay policy.

Local regression/build checks do not establish native AI dispatch, actual smoke throws,
successful shooting or turn-time performance. **Windows installation/gameplay pending.**

## Experimental threat-driven support (0.7.18)

Enable **Threat-driven vehicle, fire and smoke support (0.7.18)** and restart the
game (`CombinedArmsSupport`, OFF by default). Fast planning must remain enabled.

**1.8.1 correction:** fresh native score collections no longer lose safe vehicle staging
when the target-query budget runs out before Execute. Last-known geometric approaches
do not spend live-target probe tokens; when those tokens are exhausted, only freshly
no-more-dangerous, affordable staging is re-seeded. Real shot checks retain their existing
caps. No cached native scores/attacks, new aggression multiplier, smoke or terrain changes.
Changed AP, weapon availability, orders, readiness and expired threats still cancel staging.

The 1.8.0 replay showed inadequate support despite verified installation; see
[feedback evidence](../../../docs/AI_THREAT_RESPONSE_FEEDBACK_2026-11-09.md).
Canonical 1.8.1 Windows BuildOnly/install and affected-only backup are verified; all
1,450 protected Mods/UserData files are unchanged. Startup/gameplay acceptance is pending.
See [regression and installation evidence](../../../docs/AI_VEHICLE_STAGING_2026-11-09.md).

**1.8.0 response:** a legitimately observed ranged threat can direct nearby crewed
vehicles ahead of infantry, including after the infantry anchor dies. Up to four existing
local origins and eight setup-aware weapon probes share the existing faction/actor budget
(soft 10 ms start limit). Real detected shots compete against unchanged native danger;
uncertain-position staging cannot increase it. Native Move still chooses paths and spends AP.

Already-positive native immediate attacks, including artillery, receive 2x priority at the
observed threat tile while currently detected. This is not a new artillery executor: native
spotting, blast/friendly-fire evaluation, range, deployment and firing remain mandatory.
Forward infantry choices into remembered firing lanes are held unless native danger is
reduced or the route waypoints have cover/smoke. Inspection is bounded to 16 native waypoints,
32 LOS probes and a soft 5 ms; this is not proof of an exposure-free interpolated route.

With **Smoke Overhaul 0.9.0**, ordinary rifle infantry can request one equipped, affordable,
native-legal smoke throw toward the threat, preferring the adjacent forward tile. Existing
successful smoke can enable a step forward; smoke is concealment, not invulnerability.
The bridge grants no grenade or ammo; Smoke Overhaul's separate free-grenade setting can.
Without that optional companion, cover/fire remain available
but an unsafe advance is not authorized by imaginary smoke. Useful attacks/emergencies win.

The older nearby-infantry support fallback remains below when no threat response is seeded:

- Nearby infantry with exclusively advancing mission orders supplies the direction.
  Vehicles/heavy teams keep their own compatible advance order, or have no order;
  defense, retreat, arrival and emergency restrictions are not replaced.
- Recognized human-crewed Rogue walkers/tanks and pirate guntrucks can support despite
  their MACHINE tag. Constructs, alien machines, drones, artillery and indirect/AoE
  attacks do not receive a broad MACHINE exemption. A direct-fire weapons team can
  support infantry while accounting for its native deployment/setup skills.
- Hold near infantry; never move backward merely to restore a six-tile gap. Forward/lateral
  catch-up and useful firing positions can receive support utility; genuine native danger
  reduction is retained. Nearby distinct advance zones qualify when their directions agree.
  Require actual weapon range and a clear sampled lane, or a
  useful native damage evaluation against a currently detected opponent. Empty ground
  receives positioning utility only, never fake attack candidates or reaction fire.
- Native threat costs, including AT threats, remain unchanged. Added support utility
  is restricted to existing nearby candidates no more dangerous than the current tile.
  Native Move owns actual reachability, paths, movement/packing costs and execution;
  reserve stance/setup/fire AP and recheck its real clipped cost before choosing movement.
  Prefer a ready position over needless setup churn.
- A main-thread faction snapshot and small spatial index find at most four partners.
  Each squad has one vehicle and one heavy-team slot. Short leases expire after two
  rounds; no advance, changed orders, arrival, movement/death callbacks and mission reset
  release or refresh evidence. Threat-free infantry is not held waiting for support.
- At most eight local origins, two weapons and four detected targets. Support shares
  the existing 24-token evaluation / 48-token actor-round budget; all planning also
  shares 512 tokens per faction-round. Lane samples cost two tokens and setup-aware
  damage samples four. Stop starting probes after a soft 20 ms; not a frame-time guarantee.
- Record only detected armed enemies with range/line of sight to advancing infantry.
  Keep managed last-seen coordinates for two rounds, with a 3/5/7-tile uncertainty radius;
  no hidden-position or hidden-weapon refresh. At most 32 observations per round and 32
  retained threats, isolated by faction/mission. A local flank uses at most 16 shared probes
  and a soft 5 ms start limit, preserves native safety and never creates attack utility.
  Four flank seeds at most; no global route search or guaranteed squad-level maneuver.
- Threat observation is independent of the potential victim's suppression/readiness, but
  still requires an advance-ordered infantry target and a detected armed enemy with native
  applicability, range and line of sight. Failed observations get at most one retry per
  target/round; successful sightings are not polled again that round.
- If a remembered threat remains in a possible firing lane and no safe local flank is
  available, suppress only the generic objective-advance score for that AI pass. Native
  direct-fire scoring, emergency choices and no-threat advance behavior are retained.

Indirect fire at hidden/uncertain contacts is **not injected**. Only existing positive
native attacks against a currently detected observed threat receive the priority bonus.

This is a first-stage positioning/setup-aware firing policy, **not** proven gameplay
coordination. Stationary pre-deployment toward empty ground, explicit facing and
Overwatch integration are deferred: no verified native action is
fabricated to claim them. Native setup still executes only when native Move/Attack
selects it. See [scope and evidence](../../../docs/AI_COMBINED_ARMS_2026-11-09.md).

## Bounded diagnostics (1.6.1)

For a diagnostic run, enable **Log bounded combined-arms planning diagnostics** together with
**Infantry-led vehicle and heavy-weapon support (0.7.18)**, then restart. `CA-DIAG` lines
in `Latest.log` report snapshot rejects, request matching/occupied slots, support
candidate filters and score/probe outcomes, assignment claims/releases, and the native
versus final selected behavior. At most 256 unique phase records are logged per mission;
the diagnostic option is OFF by default. These traces do not establish gameplay success.

## Bounded offensive planning (0.7.18)

Default-ON `OffensiveCombatPlanning` addresses the missing firing-position evaluation
under fast planning, not merely stalled/Idle units. Scope: non-allied, organic pirate
and Rogue forces; alien/machine/drone AI and severe suppression/morale emergencies
retain their previous behavior.

- Prefer an already-positive native InflictDamage action over Move/Idle. This does not
  execute attacks directly or change weapon uses, AP, friendly-fire or targeting rules.
- Before native score normalization, inspect at most eight existing local origins,
  four currently detected targets and two unlimited-use, targeted, direct-fire weapons.
  Native usability, applicability and expected damage decide whether a shot is useful.
  Setup/deployment weapons, AoE, indirect and limited-use attacks keep native handling.
- Hard limits: 24 native probes per evaluation, 48 per actor/round, 256 actor records;
  stop starting probes after a soft 20 ms budget. An individual native call cannot be
  interrupted, so this is **not** a guaranteed 20 ms frame-time bound. No map-wide
  firing search, additional path search, task or skill execution is introduced.
- One/two ready companions within 12 tiles add 25/50% projected offensive utility
  before native scaling. This rewards local support, not omniscient enemy counts.
- With no native attack/support utility, already-assigned exclusively AdvanceTowards
  orders get immediate directional progress utility, including when cover is worse.
  Preserve native multi-order allocation, arrival, defense orders, safety and AP/path
  processing. This is separate from the old two-idle-round recovery fallback.
- Visible ProtectUnit/RescueUnit/Collateral mission targets can influence positioning;
  hidden actors never provide coordinates. Native immediate civilian priority is retained.

Disable `OffensiveCombatPlanning` and restart to compare with 1.4-style behavior.
Automated checks do not establish improved aggressiveness or turn time in MENACE;
the new policy needs gameplay acceptance (#2/#17).
Canonical 1.5.0 Windows BuildOnly/install and on-disk versions/hashes are verified;
see [policy/install evidence](../../../docs/AI_OFFENSIVE_PLANNING_2026-11-08.md).

Independent AI DLL; Larger Maps is optional and no longer installs it automatically.
Requires Jiangyu and MenaceModSettings. The Suite explicitly includes both modules.

## Continued mission advances and civilian objectives (#2/#17)

- After recovery starts, actual progress toward the same complete advance order allows
  recovery next round without another two-idle-round wait. Selection alone is not progress;
  no progress, changed orders, gaps, arrival or useful other actions stop continuation.
- Safe closer native candidates receive 1–2x native utility threshold, proportional to
  progress. Native safety/distance scores, pathfinding, clipping and AP are unchanged.
- `PrioritizeMissionCivilians` defaults ON, restart required, reviewed 0.7.18 only.
  It doubles an already-positive native immediate damage value for a detected non-allied
  civilian/local escort explicitly targeted by ongoing ProtectUnit/RescueUnit or matching
  native Collateral conditions. Hidden, unrelated, allied, dead or contained targets,
  incidental splash and invalid/zero-value attacks are unchanged. At most 32 objectives
  are inspected during an existing native evaluation; no new attack/path search or polling.
- This does not discover unseen civilians, make all civilians enemies or guarantee a shot.
  Other mission types retain native targeting. Fast planning remains ON; optional contact
  convergence and its default remain unchanged. Gameplay impact still needs a fresh test.

## Experimental recent-contact approaches (#17)

- `ConvergeOnLastKnownContacts` is OFF by default and requires a game restart.
  Enable **AI Performance / Unassigned infantry approach recent contacts (0.7.18)**
  before launching a mission. Independent of simplified planning and advance recovery.
- Only pirate/Rogue infantry with **no faction mission zones**, no useful combat
  action/candidates and no existing tile utility qualify. Does not override defense,
  attack, retreat, sleep/deployment, suppression, morale or movement restrictions.
  Vehicles, machine/xeno/drone units, indirect-fire assets and weapons teams are excluded.
- Stores coordinates only on native faction-detection events, never by polling hidden
  actors or remembered-contact TTL lists. Contacts expire after two unseen rounds;
  approach uncertainty grows from 3 to 7 tiles. No hidden movement/death tracking.
- At most three responders per faction approach slightly different areas. Arrival,
  two stationary rounds or three response rounds stop the attempt; fresh detection
  can permit another. Absent responders release their slots. Native Move chooses
  reachable destinations, cover, paths and AP; only no-less-safe progress tiles receive
  its utility threshold. No new path search or shared mission-order mutation.
- Reviewed 0.7.18 native hash only; works on both 64x64 and enlarged maps.
  Unknown builds retain native behavior. `DiagnoseContactConvergence` is OFF by
  default and capped at 50 lines per mission. No per-frame work.
- First-stage prototype: **no last-known mortar fire, preparatory bombardment or
  off-map artillery changes yet**. Existing combat/planning behavior is unchanged
  when the new option is OFF. Gameplay effectiveness and turn-time impact need testing.

## Existing performance and advance-recovery behavior

- Tracks the actual AI worker task instead of a wrapper that detaches after 12 seconds.
  Does not cancel work, skip units, suppress exceptions or block the game thread.
- Simplified attack/buff planning defaults ON, including native 64x64 and enlarged maps.
  Disables hypothetical future firing-position evaluation;
  this is a deliberate AI positioning tradeoff, not behavior-identical acceleration.
- Applies to every eligible shared attack/buff skill template, with no faction or
  infantry-profile exception. The old PreserveBasicInfantryFirePositioning option is
  removed; any saved value is ignored. Native profiles already set to false remain unchanged.
- Optional nearby destination limits default OFF (20 destinations / 12 tiles when ON).
- Restores modified template flags and candidate limits on mission preparation,
  strategy/title transitions and unload. Native pathfinding/player movement untouched.
- Settings are under **AI Performance**. Disable simplified planning there for an OFF
  comparison, then restart MENACE.
- Conditional advance recovery now lives here, not in LargerMaps.dll. It is ON by
  default on actual maps above 64x64 and independently hash-locked to reviewed
  0.7.17+22362 / 0.7.18+22828 binaries. Unknown builds retain native behavior.
- Requires two idle rounds at the same position/order, one to eight exclusively hostile
  AdvanceTowards zones, full morale, no suppression, available AP, no valid attack candidates/positive
  other actions or tile utility. Sleep, defense and arrival are preserved. Once native
  Move has produced an AP-valid destination toward the order, recovery selects Move
  instead of Idle; native Move still owns destination/path/safety calculation.
- Handles the null-destination case before native Move.HasUtility: safe tiles strictly
  closer to the advance order receive bounded native-threshold progress utility, only after
  the existing idle-history and no-shot guards pass. Current/farther/less-safe tiles
  receive no incentive; existing positive utility is preserved. Native Move still
  chooses targets and computes reachable paths/AP costs; no additional path search.
- Multiple advance orders use the native Entity.ID modulo advance-count allocation,
  not the first/nearest zone. Multi-zone distance weights are left untouched; only safe
  utility toward the allocated zone is recovered. Arrival stops recovery; mixed/defensive
  orders are excluded. The full ordered list of areas/priorities is revalidated before
  utility/choice, so even an unselected zone's change invalidates stale eligibility.
- No contact or attack candidate is fabricated. This applies before or after contact.
- A legal recovered Move selection retains eligibility during same-round re-evaluation;
  provisional selection alone is not proof of movement. Actual movement prevents another
  recovered move that round; observed progress can continue recovery next round. Order changes,
  useful non-movement actions and all existing combat guards cancel recovery normally.
  History resets between missions. Old Larger Maps recovery settings are not migrated;
  use **AI Performance / RecoverStalledAdvance** for OFF comparisons.
- No update loop or default per-unit logging. Optional `DiagnoseAdvanceRecovery`
  defaults OFF and is capped at 200 lines per mission. When enabled, it traces only
  hostile advance-order candidates, deduplicates unchanged rejection stages, and reports
  whether recovery was armed/applied/forced plus seeded utility and subsequent Move/Idle scores. Larger Maps
  retains optional general AI diagnostics but no recovery hooks.

Managed fixture checks exercise production task/planning/restore code. Metadata-only
checks inspect current game proxies before canonical staging; they do not execute IL2CPP.
Windows installation, native dispatch and real battle timing acceptance are separate.
Issues: #2 (stalled advance), #93 (extraction), #57 (late-round slowdown).
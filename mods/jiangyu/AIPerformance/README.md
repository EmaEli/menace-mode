# AI Performance 1.8.0

## Experimental threat-driven support (0.7.18)

Enable **Threat-driven vehicle, fire and smoke support (0.7.18)** and restart the
game (`CombinedArmsSupport`, OFF by default). Fast planning must remain enabled.

Canonical 1.8.0 Windows BuildOnly/install and affected-only backups are verified;
1,445 unrelated Mods/UserData files unchanged. Gameplay acceptance is still pending.
See [test evidence](../../../docs/AI_THREAT_RESPONSE_2026-11-09.md).

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
No grenade or ammo is granted. Without that optional companion, cover/fire remain available
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
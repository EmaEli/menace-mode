# Objective-led enemy battle plans — proposal, 9 November 2026

Tracking: [#17 operational coordination](https://github.com/EmaEli/menace-mode/issues/17),
[#13 local tactics](https://github.com/EmaEli/menace-mode/issues/13),
[#2 individual initiative](https://github.com/EmaEli/menace-mode/issues/2).

**Proposal only. No gameplay source, settings, builds or installed files changed.**
The broad gameplay direction is user-requested; the mechanics below are proposed,
not an accepted implementation or a claim that current AI already does this.

**Priority update:** the user requests fixing individual cover-and-fire behavior first.
The [latest-mission investigation](AI_INDIVIDUAL_ENGAGEMENT_FEEDBACK_2026-11-09.md)
finds sleeping/order coverage gaps and awake non-engagement. This larger prototype is
deferred behind that work; formation alone will not restore missing firing decisions.

## Gameplay problem

The user finds the game too easy: enemies mostly hide and appear individually,
creating a whack-a-mole encounter rather than a battle. Desired behavior is a front
that advances or holds according to the objective, with supporting units, concentrated
firepower and opportunistic flanking. More movement or a general aggression multiplier
is not sufficient. Previous ineffective support remains unresolved, not accepted.

## Current gap, checked against 1.8.1 source

- `OffensivePlanningRuntime` evaluates bounded individual firing positions, prefers
  positive native damage actions and adds objective-progress utility. Its nearby-friend
  multiplier is proximity, not a group battle plan.
- `CombinedArmsState` owns short-lived infantry/support assignments. Existing response
  and flank hooks react to observed threats but do not establish shared frontline
  sectors, group advance/hold posture or a reserve.
- Native mission zones remain authoritative. Support requests currently require
  advancing infantry; this is not a verified active-defense implementation.
- `AI_VEHICLE_STAGING_2026-11-09.md` records the installed 1.8.1 staging fix, not
  gameplay acceptance or completion of #13/#17. This feedback does not establish
  the installed version or a new log-derived root cause.

## Proposed behavior: one plan, different jobs

### 1. Objective determines posture

- Attacking groups advance toward their legitimate assigned objective in short bounds.
- Defenders establish useful firing positions covering the objective and approaches,
  reinforce threatened local sectors and counterattack only within their mission remit.
- An advantageous firing line holds and fights; it does not keep moving for its own sake.
- With no known enemy, positioning follows mission areas, not hidden player locations.
- Unknown, mixed, scripted or retreat orders keep native handling until their exact
  semantics are verified. Do not infer enemy posture from the player's objective name.

### 2. A front means mutually supporting sectors, not a straight row

- Assign nearby compatible squads adjacent sectors along an objective/contact-facing
  front. Terrain and weapon reach shape it; no rigid formation or global unit blob.
- Avoid isolated squads advancing far beyond effective friendly support. Others catch
  up or cover their movement; no squad waits indefinitely for a missing partner.
- Stable assignments survive repeated native evaluations. Refresh for changed orders,
  meaningful contacts, casualties, blocked routes or genuine completed progress.
- An assignment is intent, not proof that a unit reached its sector or fired.

### 3. Roles follow available weapons and opportunities

- **Line/manoeuvre:** occupy useful cover and advance by bounds when appropriate.
- **Fire support:** ready MG/heavy teams and crewed combat vehicles cover that advance
  with real firing lanes, legal setup and worthwhile native attacks.
- **Flank:** a small mobile element changes the attack angle only when the route and
  resulting position are better than frontal movement. Sideways motion alone is not
  a flank; do not send every rifle squad around the same side.
- **Reserve:** when sufficient forces exist, retain an element for a threatened sector,
  casualties or an opening. A reserve must have a bounded response task, not endless Idle.
- Small or unsuitable groups use fewer roles; never invent equipment or require a
  fixed composition. Existing Rogue/pirate doctrine scope remains the starting point.

### 4. Concentrate effects, not bodies

- Prefer the dangerous detected enemy blocking the group's mission: for example, a
  firing MG covering the approach. Use suitable available weapons, not every weapon
  against the same target regardless of effect, exposure or overkill.
- Suppression supports manoeuvre only when real suppression is established; assignment
  or a provisional shot choice is not readiness. Respect native activation order.
- Support already delivering worthwhile fire stays useful instead of chasing an ideal
  spacing. Concealment behind a wall with no useful firing lane is not successful support.
- Preserve genuine AT caution, emergencies, AP/ammunition, native targeting, deployment,
  paths, friendly-fire rules and the option to reorganize or withdraw.
- Smoke remains the existing optional executor; no new grenade system. Overwatch and
  uncertain-contact bombardment remain separately verified work, not implied features.

## Implementation sequence and boundaries

1. Verify native advance/defense order semantics and current support ownership. Extend
   the existing AIPerformance assignment owner, not another competing director/scheduler.
2. First opt-in milestone: shared local sectors and advance/hold posture, preserving
   useful attacks and mission constraints. Confirm actual pressure and objective progress
   before layering on additional roles.
3. Attach setup-aware support and one opportunistic flank to that same plan; add reserve
   and coordinated target priorities only after their native execution contracts are checked.
4. Validate distinct attacking/defending encounters and turn times before default enablement.

Keep snapshots managed and immutable for workers, with mission/faction/actor generation
checks and no retained native score/attack objects. Re-evaluation must not erase valid
intent or refill spent probe budgets. Reuse bounded native candidates; no all-pairs
pathfinding, full-map firing search, per-frame planning or shared RoleData mutation.
Prove the operation caps before enabling a prototype. Diagnostics remain optional/OFF.
Existing #1/#2 prerequisites for the broader integrated tactics release remain open;
neither lack of smoke nor incomplete later roles should artificially stop native combat.

## What success looks like

- The player faces a connected fighting force, not isolated brief appearances.
- Attackers gain useful ground together; defenders actively contest their objective.
- Support establishes real lanes and fires when legal; flanks create a useful angle.
- A blocked group chooses counterfire, another approach, reorganization or bounded
  fallback instead of oscillating between exposed movement and indefinite hiding.
- Native emergency behavior and useful attacks remain available. No deadlocks, hidden
  position reads, spacing-only retreats or material AI-turn slowdown on 64x64/96x96.

Tests must include fresh-score repeated evaluations, partner death, objective changes,
blocked/absent support, lost detection, low AP/ammo, setup costs and small groups.
Managed tests/builds cannot establish this gameplay result; keep issues open until
the user accepts the relevant behavior. No identical long replay is required by this plan.

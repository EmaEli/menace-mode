# SFP: vehicle crew promotions

Status: **implementation in progress; identity/snapshot foundation only** (23 October 2026).
Tracked in [vehicle promotions #66](https://github.com/EmaEli/menace-mode/issues/66).
Related: [promotion flow #27](https://github.com/EmaEli/menace-mode/issues/27),
[service history #53](https://github.com/EmaEli/menace-mode/issues/53).

## Intended player experience

Promote a generic veteran vehicle crew member into a new **vehicle commander**,
using the same dossier -> Hire -> deployment flow as infantry promotions.
Do not promote or replace an existing named commander such as Bog or Petra.
Scope confirmed in the issue: promote generic crew serving under existing hired pilots,
not named commanders such as Bog or Petra; create a new vehicle commander. Start with
the balaclava pilot artwork and use the same Veterancy-band/source-learned-perk approach
as squaddie promotions.

Recommend a separate Vehicle Field Promotion Dossier, with the same pricing rules,
so buying an infantry dossier cannot unexpectedly consume a vehicle crew member.
Keep the existing infantry selection and promotion behavior unchanged.

## What the current source actually supports

- `mods/jiangyu/SquaddieFieldPromotions/code/NativeCandidates.cs` reads only native
  living squaddies. It cannot currently select vehicle crew.
- `mods/GameplaySuite/src/SquaddieVeterancy/ExperienceStore.cs` stores individual
  `CrewMemberRecord` entries inside each native commander's `PilotRecord`. Each now has
  a generated `PersonId`, separate from its persistent pool/slot key. Migration repairs
  missing/duplicate IDs without changing XP; death/replacement gives the new person a
  new ID, while wounds and loadout reassignment retain it.
- `CompatibilityApi` now exposes a versioned, copied crew snapshot for living hired
  commanders. Its reader uses cached data and does not normalize or mutate the store.
  There is still no explicit crew-promotion transaction.
- `mods/jiangyu/SquaddieFieldPromotions/code/PromotionRecord.cs` requires a native
  squaddie source ID. Its current ledger cannot represent crew sources correctly.
- `mods/jiangyu/SquaddieFieldPromotions/code/LeaderForge.cs` clones Sachin's leader
  template and uses infantry donors. Reusing this unchanged would create infantry.
- MENACE 0.7.17 metadata has native `Pilot : BaseUnitLeader`,
  `UnitLeaderTemplate.UnitActorType`, and a roster leader factory. The correct
  vehicle template/deployment path still needs focused native/runtime validation.

## 1. Give the person an identity before generating names

Let Veterancy own a persistent `PersonId` on each crew record, separate from the pool
key used for assignment compatibility. The source now generates a GUID identity when
migrating a record with no valid ID, without changing XP. Death/replacement creates a
new PersonId; wounds and loadout/active-reserve reassignment preserve it. Remaining
checks must prove this through sidecar reload and runtime crew mutations. Never
reconstruct earlier identities, missions or deaths from missing data.

Store generated identity with the person: forename, surname, callsign, native
gender/skin values, portrait/voice choice and generator/catalog version. Persist the
actual choices, not just a seed, so a catalog update cannot rename existing people.
Names are cosmetic; appearance/name choices must not determine abilities.

Use local curated name sets and callsigns, not online AI or an external service.
Use coherent naming sets, not arbitrary fragments or borrowed hero identities.
A versioned stable hash/PRNG seeded by PersonId can make initial generation repeatable;
do not use process-randomized string.GetHashCode or depend on runtime Random behavior.
Avoid duplicate full-name/callsign combinations in the current save where possible,
using a bounded deterministic disambiguation policy. Native IDs remain the authority.

Recommended timing: identity is assigned when the person enters the crew pool, and
shown in Veterancy's crew UI before promotion, rather than appearing only in Hire.
Example authored identity: **Mara "Rivet" Voss**, formerly a gunner under Petra.
This is an illustration, not an imported character or a claimed live crew member.

## 2. Preserve real experience; invent only missing characterization

- Rank using **individual** crew XP, never the average shown for a vehicle crew.
  Ties use stable PersonId ordering, not mutable slot/role order.
- Start conservatively with healthy living members, active or reserve under a
  currently hired living player commander; exclude Replacement, Wounded, already
  promoted and unavailable crew. Wound transfer is not implemented by current SFP.
- An absent/old/ambiguous Veterancy provider disables vehicle promotions only;
  infantry retains its current optional-provider/native-missions behavior.
- Reuse the rank-aligned bonus ladder, but verify an appropriate vehicle-commander
  baseline rather than copying infantry attributes or donor perks wholesale.
- Biography uses real XP/rank, last role, vehicle and commander when available.
  Track future missions/wounds per PersonId; say earlier service is unavailable.
  Do not invent combat kills, mission totals, decorations or a homeworld as facts.
- A short authored background can provide flavor, explicitly separate from the
  recorded service history. No gameplay perks are implied by that background.

## 3. Create a vehicle commander, not an infantry leader in disguise

Extend the SFP record with an explicit source kind and crew PersonId/source context;
old records migrate as Infantry with their saved attributes/text unchanged.
Persist separate forename/surname for generated crew identities, while preserving
the current full-name handling of existing squaddie promotions.

Add a vehicle-specific forge path from a validated native vehicle-commander donor.
Validate actor type, allowed equipment, attribute layout, supply costs, perk trees,
vehicle assignment and native Pilot creation. Never grant a free vehicle implicitly.
Reuse generic gendered portraits/badges where appropriate; audit John/Jane routes for
vehicle use and retain a validated native fallback if a route is unsuitable.
Rehydrate the saved leader/speaker templates before native roster deserialization,
including when Veterancy is absent: existing promoted commanders must still load.

## 4. One person out, one commander in

Expose a versioned compatibility snapshot from Veterancy, plus a separate explicit
promotion transaction; do not turn the existing read-only API into hidden mutation.
SFP must not edit Veterancy JSON directly or use reflection to mutate private pools.

At redemption, revalidate PersonId, XP, status, pool membership and save context.
Prepare the leader, reserve the exact source person, add/verify Hire, commit the
promotion and retire that person from the crew pool. Fill the vacated job with a
**new identity at 0 XP** according to explicit crew replacement rules.
The original commander, vehicle and other crew keep their equipment/XP/status.

SFP and Veterancy currently persist separately. Design a shared transaction ID and
save-scoped recovery/reconciliation protocol before enabling paid redemption; two
successful in-memory edits do not prove atomic save persistence. Missing/mismatched
transaction state must block further crew promotions rather than silently duplicate
or delete people. An old save must restore that save's source/leader state together.
Native dossier purchase/refund behavior must be tested separately from pool rollback.

## Implementation order and focused checks

1. Complete PersonId migration and the read-only crew snapshot in Veterancy. Test
   sidecar reload, stable IDs, death versus wound, reassignment, distinct saves,
   malformed IDs and replacements. Promotion remains disabled until these checks pass.
2. Add source-kind records and vehicle forge. Verify a generated leader is a native
   Pilot in Hire/Armory/deployment, with correct text, equipment and save rehydration.
3. Add coordinated consume/rollback/recovery and the vehicle dossier. Test exact-source
   retirement, failure injection between stages, repeated redemption, replacements,
   save/restart/load, divergent saves and missing/disabled provider behavior.
4. Run targeted automated checks, canonical Windows build/install with backup, then
   user gameplay acceptance. Install only with MENACE closed; never interrupt a run.

Only the Veterancy identity/snapshot source foundation is being changed at this stage.
The vehicle dossier, forge, promotion transaction and recovery are not implemented;
no installed mods or save files have been changed.
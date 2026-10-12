# NPC veterancy profiles - 1.7

Percentages are ordered **Green / Combat Tested / Hardened / Regular / Veteran / Crack / Elite** and always total 100.

Temporary NPC ranks are tactical/battle-local only. Player infantry XP and player vehicle-crew XP remain persistent.

## Rank effects

For temporary human AI, the squad/vehicle rank applies the same default Accuracy / Defense / Suppression Resistance curve as player infantry:

| Rank | Accuracy | Defense | Suppression resistance |
|---|---:|---:|---:|
| Green | -6% | -6% | -6% |
| Combat Tested | -4% | -4% | -4% |
| Hardened | -2% | -2% | -2% |
| Regular | 0% | 0% | 0% |
| Veteran | +2% | +2% | +2% |
| Crack | +4% | +4% | +4% |
| Elite | +6% | +6% | +6% |

Infantry temporary ranks also participate in individual no-fire reliability. Human-crewed enemy vehicles receive a single crew/actor rank and **do not** use infantry no-fire filtering.

## Scope

Temporary infantry veterancy applies to Pirates, Rogue Army, hostile Enemy Local Forces, and explicitly listed allied human combatants. Temporary vehicle veterancy applies to recognized human-crewed hostile Pirate/Rogue vehicles. Constructs and Wildlife remain excluded from veterancy even when the entity is vehicle-shaped. Civilians and non-combat mission NPCs are also excluded.

Resolver order is exact `EntityTemplate.m_ID`, then family/faction fallback.

## Pirate infantry

| Unit group | G | CT | H | R | V | C | E |
|---|---:|---:|---:|---:|---:|---:|---:|
| Tutorial weak | 95 | 5 | 0 | 0 | 0 | 0 | 0 |
| Outcasts | 75 | 20 | 5 | 0 | 0 | 0 | 0 |
| Scavengers | 55 | 30 | 12 | 3 | 0 | 0 | 0 |
| Weapon teams | 35 | 35 | 20 | 8 | 2 | 0 | 0 |
| Specialists | 20 | 30 | 25 | 15 | 8 | 2 | 0 |
| Saboteurs | 5 | 15 | 25 | 25 | 20 | 8 | 2 |
| Veteran Scavengers | 2 | 8 | 20 | 30 | 25 | 12 | 3 |
| Boarding elite | 0 | 3 | 7 | 20 | 35 | 25 | 10 |
| Captain / command | 0 | 0 | 5 | 15 | 35 | 30 | 15 |
| DEV heavy | 10 | 20 | 25 | 25 | 15 | 5 | 0 |
| Pirate fallback | 55 | 30 | 12 | 3 | 0 | 0 | 0 |

## Rogue Army infantry

| Unit group | G | CT | H | R | V | C | E |
|---|---:|---:|---:|---:|---:|---:|---:|
| Conscripts | 40 | 35 | 20 | 5 | 0 | 0 | 0 |
| Line infantry | 10 | 25 | 30 | 25 | 8 | 2 | 0 |
| Weapon teams | 5 | 20 | 30 | 30 | 12 | 3 | 0 |
| Advanced weapon teams | 2 | 10 | 25 | 35 | 20 | 7 | 1 |
| Heavy infantry | 2 | 8 | 20 | 35 | 25 | 8 | 2 |
| Officer | 0 | 2 | 8 | 20 | 35 | 25 | 10 |
| Jaeger | 0 | 2 | 8 | 20 | 30 | 30 | 10 |
| Storm Troopers | 0 | 0 | 5 | 15 | 30 | 35 | 15 |
| Ammo runner / support | 15 | 30 | 30 | 20 | 5 | 0 | 0 |
| Rogue fallback | 10 | 20 | 30 | 25 | 10 | 4 | 1 |

## Allied NPC infantry

| Unit group | G | CT | H | R | V | C | E |
|---|---:|---:|---:|---:|---:|---:|---:|
| Civilian Militia | 60 | 25 | 10 | 5 | 0 | 0 | 0 |
| Civilian Hunters | 30 | 35 | 20 | 10 | 5 | 0 | 0 |
| Local Forces Conscripts | 50 | 30 | 15 | 5 | 0 | 0 | 0 |
| Local Forces Infantry | 15 | 30 | 30 | 20 | 5 | 0 | 0 |
| Local weapon teams | 10 | 25 | 30 | 25 | 8 | 2 | 0 |
| Allied Pirate Scavengers | 55 | 30 | 12 | 3 | 0 | 0 | 0 |

Excluded allied mission NPCs include `civilian.worker`, `allied_local_forces.pilots`, `allied_local_forces.police_chief`, and `allied_local_forces.pows`.

## Hostile human vehicle crews

| Vehicle group | G | CT | H | R | V | C | E |
|---|---:|---:|---:|---:|---:|---:|---:|
| Pirate Outcast trucks | 45 | 30 | 20 | 5 | 0 | 0 | 0 |
| Pirate Scavenger truck | 35 | 35 | 20 | 8 | 2 | 0 | 0 |
| Pirate weapon-team trucks | 25 | 35 | 22 | 13 | 5 | 0 | 0 |
| Pirate Boarding Commando truck | 0 | 5 | 10 | 25 | 35 | 20 | 5 |
| Pirate Veteran Scavenger truck | 2 | 8 | 20 | 30 | 25 | 12 | 3 |
| Pirate chaingun guntruck | 20 | 35 | 25 | 15 | 5 | 0 | 0 |
| Pirate laser/rocket guntruck | 10 | 25 | 30 | 25 | 8 | 2 | 0 |
| Pirate flamethrower heavy truck | 10 | 25 | 30 | 25 | 8 | 2 | 0 |
| Pirate HMG heavy truck | 5 | 20 | 30 | 30 | 12 | 3 | 0 |
| Rogue laser walker | 5 | 20 | 35 | 30 | 8 | 2 | 0 |
| Rogue autocannon walker | 2 | 15 | 30 | 35 | 15 | 3 | 0 |
| Rogue light carrier | 2 | 12 | 28 | 35 | 18 | 5 | 0 |
| Rogue battle tank | 0 | 5 | 20 | 35 | 25 | 12 | 3 |
| Rogue heavy tank | 0 | 3 | 15 | 32 | 30 | 15 | 5 |
| Rogue mercenary medium walker | 0 | 2 | 10 | 28 | 35 | 20 | 5 |

## Faction fallbacks

| Faction/fallback | G | CT | H | R | V | C | E |
|---|---:|---:|---:|---:|---:|---:|---:|
| Pirate infantry | 55 | 30 | 12 | 3 | 0 | 0 | 0 |
| Rogue infantry | 10 | 20 | 30 | 25 | 10 | 4 | 1 |
| Enemy Local Forces infantry | 25 | 30 | 25 | 15 | 5 | 0 | 0 |
| Pirate vehicles | 20 | 35 | 25 | 15 | 5 | 0 | 0 |
| Rogue vehicles | 2 | 13 | 30 | 35 | 15 | 5 | 0 |
| Enemy Local Forces vehicles | 20 | 30 | 30 | 15 | 5 | 0 | 0 |

Construct and Wildlife vehicles are intentionally not assigned a human crew rank.

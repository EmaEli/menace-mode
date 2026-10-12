# Smoke Overhaul 0.5.2: native AI smoke selection performance fix

## Evidence and causal hypothesis

On the user's 80x80 mission with Larger Maps 1.6.1, round 11 AI activation
#192 spent 89.4 seconds wall time, with 82.6 seconds of cumulative Collect
work, dominated by `CreateLOSBlocker.Collect` for mortar squads. Native
pathfinding accounted for only 284 ms. The player observed delayed mortar
shots and no smoke being fired. This does NOT mean rendering a mortar round
lasted 82 seconds; the delay is in AI planning.

Smoke Overhaul 0.5.1 had enabled `CreateLOSBlocker` for mortar smoke shells
by sharing the normal infantry smoke-grenade AIConfig. Mortar squads also
receive the infantry smoke grenade via the optional free-infantry-grenade
feature. Consequently an optimization affecting only mortar smoke shells
would miss grenade-based `CreateLOSBlocker` scans performed by mortar squads.

MENACE 0.7.15 GameAssembly.dll native `Assist.OnCollect` is at RVA 0x7AE830.
After retrieving `SkillTemplate.AIConfig` at offset 0x2D8, it checks
`InfluencesPositioning` (offset 0x14 in `AI.Data.SkillBehavior`) and returns
early when that flag is false (branch observed near RVA 0x7AEA69). The game
itself documents that disabling this flag prevents the AI from seeking new
positions *for this ability*, but still permits the ability when the actor
can already use it. `Assist.OnEvaluate` does not have the corresponding
early return based on this flag.

## Implementation

- `FastAISmokeSelection` defaults to ON, is independent from
  `EnableAISmokeUsage`, and requires Smoke Overhaul `EnableGameplay`.
- Only the existing five known smoke skill templates are considered.
- Non-null native `AIConfig` pointers are deduplicated, and the original
  `InfluencesPositioning` value is saved before writing `false`.
- No Attack, Map, Movement, Actor, or FindPath hooks were added.
- No smoke AoE, AP cost, accuracy, range, cooldown or animation fields changed.
- The flag is restored when the option is disabled, gameplay is disabled,
  or the mod is unloaded. Pointer-identity dedup prevents restoring a shared
  smoke profile to the wrong value.
- One normal initialization log confirms application; diagnostic logging
  remains OFF by default, with no per-call or per-turn log additions.

## Limitations / test plan

This is a targeted optimization of **speculative movement-for-smoke**. It does
not replace MENACE's native smoke target-scoring algorithm, and some work may
remain in `Assist.OnEvaluate` when the unit is already in firing position.
Because the optimization also applies to infantry smoke, their AI will no
longer move solely to get a smoke-throwing angle. It does not remove their
ability to select and throw smoke after moving for other reasons.

On Windows, build with `Build-and-Deploy-Suite.cmd`. Keep Larger Maps 1.6.1
unchanged and verify a single:

`[SmokeOverhaul] Fast AI smoke selection active: N native profile(s)`

`N` should be positive with the current 0.7.15 templates. Test the original
mission at round 10-11, where mortar/AI smoke-related slowdowns manifested.
Compare AI profiling `CreateLOSBlocker.Collect` and total actor activation
wall times. Confirm that mortars still fire HE and that smoke abilities
remain accessible/usable. If smoke use becomes too rare, switch OFF
`FastAISmokeSelection` and restart. Turn off deep AI profiling for normal
play after collecting evidence, because profiling adds overhead.

Offline static tests and native disassembly do not replace an in-game test.

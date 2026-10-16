# Larger Maps 2.2.0 — offline Jiangyu candidate

The current host compiles the shared `mods/LargerMaps/src/LargerMaps` sources.
Use the canonical Suite installer in `mods/GameplaySuite`; do not revive the old
PR #18 one-click installer or install a second legacy Modpack Loader copy.

## Selective infantry positioning (#2)

Ports PR #18 commit `3f50705dffe21fa729813001ac2fe5378d30b33f`, not merged.
`PreserveBasicInfantryFirePositioning` is **OFF by default**, restart required.
With `SimplifyCombatAIPlanning=ON` and map size **above 64x64**, it preserves only
already-enabled native positioning profiles resolved from these five skills:

- `active.fire_pirate_assault_rifle`
- `active.fire_pirate_pdw`
- `active.fire_pirate_outcast_pipe_gun`
- `active.fire_assault_rifle_tier1_556`
- `active.fire_battle_rifle_tier_1`

Profiles must be attacks requiring line of fire, with nonzero native pointers.
Selection precedes all mutations and deduplicates pointers, not managed wrappers.
Aliases sharing a preserved profile also retain it; five IDs are not necessarily
five distinct profiles. Disabled/missing/incompatible candidates are never forced
ON. Other attacks and recognized buffs keep the existing simplification behavior.
Scene exit/unload restores owned changes and clears preserved identities.

No new agent/turn hooks, wake-ups, orders or custom pathfinder. Preserving native
move-to-fire scoring **can increase native CPU work**; it is not a proven speedup,
cover-and-fire controller or implementation of #13/#17.

## Required A/B acceptance (not performed)

1. Validate #1 smoke first. For this test hold all Smoke Overhaul / Infantry Assault
   settings identical; preferably keep proactive smoke OFF to isolate positioning.
2. Use the same save/seed, mission, mod set and 2.2.0 binary. Restart between OFF/ON
   runs. `SimplifyCombatAIPlanning=ON`, `UseLocalTacticalRouting=OFF`; all three deep
   AI diagnostic flags OFF. Saved settings may override new-install defaults.
3. Test 64x64 as an inert control, then 96x96; offensive and sleeping/defensive missions
   separately. Do not compare different missions or assume sleep is a positioning bug.
4. Record ~3 AI turns per run: actual shots, useful repositioning, idle/sleep states,
   defended objectives, visible turn durations and any oscillation/suicidal advance.
5. Separately inspect the setup log: retained profiles must be 0 with option OFF,
   and at most 5 when ON above 64. No activation line is expected at 64. Missing-ID
   fallback must not silently be called a successful initiative test.
6. Check return to strategy, next mission and unload. If behavior/CPU regresses, leave
   the option OFF. Native 0.7.16 use, timing and Windows installation remain unverified.
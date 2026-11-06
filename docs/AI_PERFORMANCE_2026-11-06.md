# AI Performance extraction — 6 November 2026 (#93 / #57)

## Observed slowdown (old installation, not candidate evidence)

The captured MENACE 0.7.18+22828 session loaded LargerMaps 2.5.0 and fell back to
64x64: the native 80/96 backend does not support this game hash. Installed settings
had simplified planning and local routing OFF; route/task/path diagnostics were ON.

- One round-6 enemy task took 184,819 ms. Two completed units took 69,116 / 60,478 ms.
  Attack evaluation accounted for 67,301 / 60,465 ms respectively, with 162 / 166
  attack evaluations. Another faction task took 58,516 ms with 26,552 ms in attack
  evaluations and 22,788 ms in buff evaluations.
- Native `FindPath` samples were mostly about 1–13 ms. The sampled expensive work
  is therefore combat candidate evaluation, not evidence for a pathfinder rewrite.
- Advancing while combat is slower is consistent with these timings. They do not
  prove every mod's contribution or establish a universal cause for every mission.

## Candidate

LargerMaps **2.6.0** declares mandatory **AIPerformance >= 1.0.0**. The canonical
standalone/Suite selection includes both in one build/stage/backup transaction.
Installed-settings mode validates and preserves MenaceModSettings and Jiangyu.Loader.

Only AIPerformance owns the worker task lifetime hook, reversible skill planning flags
and optional native AI candidate limits. Larger Maps retains map generation, advance
recovery and opt-in diagnostics. No duplicate task hooks or new native pathfinding edits.

Simplified planning defaults ON, including 64x64. It avoids hypothetical future firing
positions: this can change AI positioning and must not be described as behavior-identical.
The old Larger Maps OFF setting is not migrated; the new **AI Performance** menu has
its own restart-required toggle. Local candidate limits remain OFF. No update polling
or per-unit performance logging is introduced. Existing Larger Maps diagnostic settings
are preserved, not silently disabled.

The scoped 0.7.18 allowance is code-only. Requests above 64 continue to fail the existing
native hash guard and fall back to 64; larger native maps are not newly authorized.
The prior #92 forest-height correction is retained, not a separate acceptance claim.

## Checks

- 26 managed production behavior checks: actual worker task identity, fault propagation,
  no inline worker execution, null delegate fallback, 64x64 planning, OFF behavior,
  original-false/shared-profile restoration, candidate limits and scene cleanup.
- 22 metadata-only native contracts against the PC's current proxy: timeout hook,
  skill flags/config, writable positioning/candidate fields and template ID lookup.
- 143 Larger Maps Python tests passed (8 explicit native/reference skips).
- 197 GameplaySuite Python tests passed; selected package/shared C# checks: 18,121.
- Release builds of both DLLs and clean extracted-source BuildKit verification passed.
  AIPerformance had no build warnings; Larger Maps retains one unused-field warning.

Initial candidate ZIP SHA256: `19ea1864061e138fdecbe757c87edc78d3da7a7624ca8a4741a4a0d1309949e4`
(118 inventoried files, two packages; private local artifact, not a public release).
Windows canonical BuildOnly passed on 6 November using private SDK 10.0.113, including
the 22 native metadata and 26 behavior checks. A complete repeat verified that all live
Mods hashes remained unchanged; both DLL versions passed staging. No installation or
gameplay speedup is claimed here: MENACE was running at the preflight.
The production compatibility gate also passed 41 Windows fixture checks, including
the allowed pair, missing companion allowance, unknown hash and mixed/Suite rejection.

## In-game acceptance still needed

After the canonical backed-up installation, restart MENACE and load the slow mission.
Compare the same save/turn with simplified planning ON versus OFF (restart between
runs). Keep other settings fixed; for clean timings turn optional diagnostics OFF.
Check attack choices, movement/cover use, complete unit turns, return to strategy,
another mission and a loaded save. Reject stuck turns, missing attacks or unintended
movement changes. Retrieve the fresh log after the user reports the result.
## 2.3.2 - Defer destination AP validation to native Move (MEN-002)

- Fix the recovery's premature destination-cost check: at ConsiderZones.PostProcess,
  native TileScore.APCost is still uncomputed (zero). Require an existing candidate
  geometrically closer to the assigned zone, then let normal Move calculate paths,
  clip them to affordable AP and select/reject movement. No extra path search or
  cost/path mutation; actor AP and every other recovery guard remain unchanged.
- Reproduce the zero-cost failure in the managed harness and pin native Reset /
  Move path-cost call sites to MENACE 0.7.17. Managed/native-byte checks, local
  clean-source build and canonical Windows BuildOnly/install pass. The previous
  2.3.1 is backed up outside Mods; settings/unrelated mods are unchanged. Fresh
  gameplay acceptance remains pending; no movement improvement is claimed yet.
- 2.3.1 game evidence: zero recovery applications; 29/31 hostiles still stationary
  in rounds 4–10. History reaches two idle rounds; rounds 6–9 show 20 actors rejected
  at Progress and nine at TileUtility. The latter guard is deliberately not relaxed.

---

## 2.3.1 - Explain rejected advance recovery (MEN-002)

- With `DiagnoseAIRoutes` enabled, record the first recovery rejection stage and
  observed idle-round history, plus whether each final choice qualifies as idle.
  Reuse per-actor/per-round route budgets; diagnostics remain OFF by default.
- Do not relax any eligibility guard or change native action selection. Diagnostic
  failures are isolated from recovery itself. This is not an inactivity fix.
- The first 2.3.0 game run enabled recovery without errors but recorded no recovery
  applications; 29/31 enemies remained stationary in rounds 4–10. 2.3.1 passed the
  canonical Windows BuildOnly/install checks on 20 October; focused gameplay and
  diagnostic-log analysis remain pending.

---

## 2.3.0 - Conditional advance recovery candidate (MEN-002)

- Add `RecoverStalledAdvance` (ON by default; restart required), restricted
  to reviewed MENACE 0.7.17 and actual maps above 64x64. Hostile factions 5–9 may
  recover their existing single `AdvanceTowards` order after two observed idle
  rounds without a position change. Useful native choices reset this history.
- Restore the native zone distance/inside-utility contributions on existing tile
  scores only when there are no attack candidates or positive combat/general tile
  utilities. Normal Move evaluation, action competition, safety, AP, stance and
  path execution remain native; no additional attack/path search is requested.
- Exclude allies/civilians, mixed/defensive orders, arrival, garrisons, suppression,
  morale loss, deployment, sleeping, spent AP and already-moving/moved actors.
  Keep contacts and enemy positions untouched. No centre target or patrol added.
- Windows BuildOnly and canonical installation passed. The first gameplay run
  did not apply recovery; acceptance remains open (see 2.3.1).

---

## 2.2.2 - Observe native Move utility decisions (MEN-002)

- Isolate the `Behavior.Execute` Harmony diagnostic state in its own callback type,
  allowing it to coexist with the deep profiler's `__state` on the same method.
- Add opt-in observations of `Move.OnEvaluate`, `OnNewTurn` and `HasUtility`, plus
  movement counters/flags and stored target-score components when `Idle` wins.
- No gameplay decisions, settings, pathfinding or native results are changed. The
  additional probes remain behind `DiagnoseAIRoutes`, OFF by default.
- Windows BuildOnly and canonical install passed. A game run reported v3 hooks=15/15;
  31 enemies chose Idle in rounds 4–9 while Move evaluation returned 0. The cause is
  still unknown; no gameplay behavior was changed.

---

## 2.2.1 - Keep AI investigation visible through the mission (MEN-002)

- Replaces the session-wide 120-agent / 200-move logging cutoff with independent
  per-actor, per-round event budgets, repeated-poll suppression and explicit limit
  warnings/count summaries. Civilians cannot exhaust another actor's budget.
- Existing opt-in `DiagnoseAIRoutes` now records round-start/end positions, AP,
  sleep/turn state, zone distance, known-contact identities/TTL, native action
  scores, attack candidates, movement state, actual movement completion, skill-use
  acceptance and effect processing. A selected destination is not called arrival;
  accepted skill use/effect processing is not proof of a hit or kill.
- No gameplay settings/decisions, pathfinder, orders or native scores changed.
  Diagnostics remain OFF by default, independent of deep pathfinding profiling.
- Added executable managed budget/distance checks and source/native-contract
  regression guards. Windows startup and mission coverage require a fresh game run.

---

## 2.2.0 - Terrain stamp footprint kept at vanilla world size (MEN-008)

- MEN-008 (#14): `ScatterStamps` writes each landform stamp texture 1:1 into the fixed 513x513 heightmap / 1024x1024 alphamaps, so on enlarged maps every basin, hill and plateau grew with the world size (about 1.3x at 64x64, 1.7x at 96x96) while the stamp prefab (water plane, rocks) kept its authored size. A lake therefore rendered as a flat slab hanging inside a much larger basin, with a visible step down to the lake floor at its edge.
- New `PreserveTerrainStampFootprint` toggle (default **ON**, restart required): for the duration of `SpawnStampsFromList` the stamp `HeightMap`, `BlendMask` and `AlphaMaps` are swapped for area-averaged copies scaled by the vanilla/enlarged world-unit ratio, then restored. Placement radius, rotated bounds, grid blockage and terrain points follow the smaller textures automatically. Scaled copies are cached per template; ScriptableObject assets are never modified permanently.
- Scale is derived from the live `MapBlueprint` (width, total and border world units); vanilla-width maps are untouched. All-or-nothing per stamp: if any texture cannot be scaled the template is left vanilla. Normal logging is one line per session; failures warn once.
- Offline build and source guards only in this revision; in-game visual confirmation (lake shoreline, 64 vs 96) still required before closing #14.

---

## 2.1.0 - Selective infantry positioning (offline candidate)

- Ports the OFF-by-default PR #18 selective profile preservation to the Jiangyu host (maps above 64x64 with simplified combat planning). See `mods/jiangyu/LargerMaps/README.md` for the A/B acceptance steps; #2 remains open.

---

## 1.8 - Adjustable high terrain stamp density (experimental)

- MEN-003: expand optional terrain-stamp **attempt** multiplier on enlarged maps from 1.0-1.5x to **1.0-5.0x**; new slider default 1.5x, feature toggle remains OFF for new settings.
- Raise the internal safety cap from 40 to 200 attempts per stamp group; never lower native counts. At 96x96, a native 4/10/0 large/medium/small budget now becomes 12/30/0 at 3x or 20/50/0 at 5x (before native placement rejections).
- Preserve the gradual 64x64-to-96x96 ramp, unchanged 64x64 maps, unchanged scene objects/per-tile grass and foliage, mission setpieces, AI/pathfinding, and logging policy (diagnostics OFF by default, capped when enabled).
- Elevated density may increase map generation time or crowd routes/compounds. Compare same-seed OFF, 1.5x, 3x and 5x and inspect visuals, FPS, vehicle access, objectives and AI turns before considering release.
- Code/static checks only in this revision; Windows build and live tests still required.

---

## 1.7.1 - Fix Harmony namespace compilation error (patch)

- Fix CS0118 in `TerrainStampDensity.cs`: fully qualify the Harmony patch argument as `HarmonyLib.Harmony` to avoid namespace/type ambiguity.
- No change to stamp scaling, generation logic, settings, AI, pathfinding, or logging. Prior 1.7 behavior remains experimental.
- Build and live testing on the user's Windows install still required; do not merge PR #10 until verified.

---

## 1.7 - Optional terrain landmark density (experimental)

- MEN-003: opt-in scaled *attempt* counts for terrain landform stamps on maps above 64x64, capped at 1.5x for 96x96. Default is OFF; 64x64 stays unchanged. Scales the integer argument to `ScatterStamps.SpawnStampsFromList` rather than modifying shared `StampCollection` assets.
- Does not multiply per-tile environmental vegetation/props, grass or mission-authored setpieces. Overlap/placement validation remains native, so the number actually placed may be lower than the scaled attempt budget.
- Optional capped generation-count diagnostic, OFF by default. No per-frame logging or AI/pathfinder changes.
- Build/runtime still require Windows game installation, MENACE v0.7.15+21883; validate visuals, pathing and FPS in game before enabling broadly.

---

## 1.6.1 - Fix inactive simplified combat planning (patch)

- Fix the 1.6 bug confirmed in Latest(20261011-175248).log: zero eligible skill templates were modified, leaving completely unchanged vanilla combat AI. The first attempt uses the original full template collection; when it updates zero skills, fall back to the supported `DataTemplateLoader.Get<SkillTemplate>(ID,false)` lookup for known attack/buff IDs from the game data export.
- The provided v0.7.15 SkillTemplate export lists 244 attack abilities with active AI positioning and 4 explicitly recognized support skills; availability and flags are rechecked at runtime. Missing IDs are quietly ignored, supporting updates without startup crashes.
- A single startup summary includes adjusted attack/buff counts and lookup-hit count. If the summary still has no positive number, stop the experiment; don't play an entire mission. No hot-path logging, extra native patches, or FindPath hooks.
- The routing and combat-planning settings from 1.6 remain unchanged, including 64x64 exclusion and restoration on leaving Tactical. This is a bugfix, not a new AI feature. Runtime performance benefit remains unverified.

---

## 1.6 - Simplified Combat AI Planning (experimental)

- **New map-size-scoped feature:** `SimplifyCombatAIPlanning` (default ON above 64x64, OFF by construction for 64x64). MENACE has a native `SkillTemplate.AIConfig.InfluencesPositioning` flag. When it is false, the game's `Attack.OnCollect` and `Assist.OnCollect` skip their speculative positioning enumeration; neither pathfinder nor player movement is modified.
- Turn off active *position-seeking* for offensive AI skills (`SkillTemplate.IsAttack`) and for verified support/buff configurations. The native AI retains its movement choices, target validity, weapon ranges, shooting from already-viable positions, and normal skill use. **Tradeoff:** AI will be less inclined to relocate specially for an ideal shot or support ability. This must be checked in actual combat.
- Changes are applied once on tactical-scene entry, verified as writable, and restored when leaving tactical gameplay / unloading the mod. No `FindPath` hooks, native buffer resizing, collection-patch detours, hot-path logging, or modification of `Attack.OnCollect` body.
- The previous 1.5 `UseLocalTacticalRouting` setting is still available but **defaults OFF** here: reducing `NearTileLimit` to 20 and distance to 12 halved pathfinding work in the real test yet did not improve AI turn time. If you explicitly enabled that option in 1.5, turn it OFF to isolate this new feature.
- `GameAssembly.dll` v0.7.15+21883 inspected: `Attack.OnCollect` at RVA 0x7B2342 and `Assist.OnCollect` at RVA 0x7AEA69 directly branch on `AIConfig.InfluencesPositioning` at offset +0x14. The previous 26 native 128x128-backed map patch sites are untouched.
- One startup summary (counts of changed skill configs) or one warning on failure. No detailed logging except explicitly enabled pre-existing diagnostics.

**Status:** static source and native inspection only. Not compiled or tested in MENACE. No confirmed performance improvement or guarantee of tactical behavior; see `TEST-PLAN-1.6.md` before use.

---

## 1.5 - Local Tactical Routing (experimental)

- **New large-map-only AI option:** `UseLocalTacticalRouting` (default ON for maps >64; restart required). On tactical-scene entry, adjust the game's own `AIConfig.WEIGHTS` rather than patching `FindPath` or movement. Default `NearTileLimit` is reduced from 64 to 20; `CullTilesDistances` from 20 to 12. These affect AI tactical candidate selection but do not confine its A* paths to a hard rectangle. The full route solver remains available for obstacles and long routes.
- `LocalNearTileLimit` slider (12..64) and `LocalCullTileDistance` slider (10..20) allow selective comparison/tuning. Settings NEVER modify player pathfinding or reference 64x64 mode. No byte edits beyond the known 1.4.13 enlarged-map native backend.
- Reads the existing values, applies both only when resolved safely, and restores originals on leaving tactical gameplay or plugin unload. On startup failure it keeps the native AI unmodified.
- **No Harmony hook on `FindPath`**; removes the possibility of the 1.4.14 invalid IL/immobile-units failure from the new feature. No cache or compact-buffer changes.
- AI combat behaviors, visibility, objective areas, spawn handling and task lifetime safety remain untouched.
- Single startup summary; no per-tile/per-turn logs unless the existing optional AI profiler is enabled.

**Limitations:** this is the first stage of local tactical routing using native candidate culling. It is NOT a new sector-grid A* implementation. The gameplay speedup and preservation of sensible aggression/cover use require a live 80x80 test. Do not use as a confirmed fix until validated.

---

# 1.4.13 - Conservative area regression candidate

- Preserve 1.4.12's exact 1.3.11 area handlers and original scope coverage at 64x64.
- For 72/80/88/96 replace the expansive 1.4.10 objective/area-role hooks with an intentionally conservative mission-area implementation using only the 1.3.11 scope coverage. Do not widen arbitrary AI or objective control areas.
- On large maps, native-generated setpiece areas stay anchored; native border bands keep their map-sized long axis while only their short thickness is adjusted; authored Rect spawn/deployment/evac areas retain 42-to-N scaling. Invalid actor-spawn rectangles are still repaired without changing valid areas.
- No AI behavior evaluation changes, no pathfinding changes, no native patch changes, no timeout or AI lifetime changes, and no additional logging outside Debug=ON.
- **Experimental**: uses the positive 64x64 area-regression test as a design clue. It has not been verified on any extended map, and objective/scoring zones may differ from 1.4.10.

---

# 1.4.12 - DIAGNOSTIC: 64x64 mission-area regression isolation

- 64x64 only: install the **exact 1.3.11 `MissionAreaConfig.AddToList` handlers and scaler**, including the original authored-rectangle transformations. Keep 1.3.11's original mission-area hook coverage; do not install the 14 newer objective-Init scope hooks, the two added `CreateInitialAiUnits`/`RescueUnit` scopes, or the later initial/tactical spawn safety hooks. Other 1.4.10 hooks remain.
- 72/80/88/96: retain **all 1.4.10** mission-area hooks, generator logic, native pathfinding and geometry. No experimental new A* changes or changes in AI decisions.
- Both: **retain** the non-detaching AI `RunWithTimeout` lifetime safety, terrain-hole indexing, 1.4.10 optional profiling (OFF by default), and all unrelated fixes. No extra hot-path diagnostics or logging.
- Purpose: A/B compare against 1.3.11 fast and 1.4.10 slow on the **same 64x64 mission seed 5320037**, with otherwise identical mods and settings. The cause is *not* proven; changing area generation alone may not affect combat-time computation.
- Source/static tests only in this environment. Compile and runtime verification require MENACE/.NET on Windows. This is **not** a release or performance fix.

---

# 1.4.10 - Identify slow AI behaviors using EXISTING deep diagnostic settings

- **Build-only correction:** resolve Windows .NET 6 SDK `CS0656` at `AIProfiling.cs:356` by replacing nested LINQ anonymous-type projection with an explicit, bounded ranking of behavior samples. Identical metrics, ordering and output; no new settings, hooks or gameplay changes. Keep version 1.4.10 because the first package failed to build.
- On the existing `DiagnoseAIPerformance=ON` + `DiagnoseAIPathfinding=ON` path, identify each native `Behavior.Collect(Actor)` sample with `Behavior.GetID()` (fallback to GetName/type if unavailable). Example IDs: `InflictDamage`, `Move`, `InflictSuppression`.
- Emit at most eight `AI PERF behavior turn=#... actor=... template=... id=... collect=n=...,sum=...ms,max=...ms` rows per completed activation, ranked by cumulative duration. Ignore samples without an individual Collect call of at least 50 ms. Slow `BehaviorCollect` samples also carry `id=` when available.
- No additional settings, no changed default values, no extra Harmony hooks, no extra debug logs when the existing two diagnostic toggles are OFF. Existing saved settings are read on launch, so already-ON flags require only the normal launch after deployment, not an extra settings/restart cycle.
- New measurements exclude profiler label-reflection time from the measured Collect interval; these nested/concurrent sums are diagnostic only, not additive wall-clock durations. Collection uses bounded thread-safe aggregates and does not log from AI worker threads.
- No changes to scoring, decisions, pathfinding, game geometry, 26 native patch sites or AI task lifetime safety. This is NOT yet a performance optimization; Windows compile and in-game run still required.

---

# 1.4.9 - Correct AI profiler activation boundaries (diagnostic bugfix)

- Fixed the v1.4.8 profiler resetting all timing counters on every `AIFaction.OnTurnStart` as if it were a whole faction round. MENACE calls this method repeatedly for actor activations. Previous metrics were discarded and the real slow activations emitted false "missing Finish" warnings, while short activations misleadingly printed 0 ms.
- Close and report every completed actor activation on the next activation or normal `Finish`. Hold a bounded diagnostic snapshot until its already-running task completes; no additional waits, cancellation, timeouts, or AI scoring changes. Include `scope=actor-activation`, `actor`, `template`, `closedBy` on the summary line, identifying the scheduled worker actor whenever present (otherwise the active TacticalManager actor).
- Preserve every existing 1.4.8 gameplay patch and all 26 native instruction patches unchanged. The protective 1.3.11 `RunWithTimeout` prefix remains byte-for-byte unchanged.
- Profiling settings remain OFF by default. No new spam in normal play; verbose output exists only during intentionally enabled diagnostic sessions.
- 2026-10-09 runtime clue: in the 80x80 test, slow activation #7 lasted over 20 seconds and activation #15 over 30 seconds. By order of actor events in Player.log, both appear to correspond to `enemy.vehicle_rogue_army_heavy_tank`; this correlation must be confirmed by the new explicit actor label.
- Build, native runtime profiling and performance optimization still require testing in the real MENACE process; this update does not claim the slowdown is fixed.

---

# 1.4.8 - AI performance diagnostics only (not an algorithm fix)

- **Authoring correction:** fix Windows build errors `CS0118` in `AIProfiling.cs` by using the fully-qualified `HarmonyLib.Harmony` type in both patch installers. No version increment, behavior change, or native edit.
- Introduces optional `DiagnoseAIPerformance` (OFF): bounded per-faction-turn and per-unit wall-clock timings, real AI Task completion observation without a timeout or background waiting.
- Introduces separately optional `DiagnoseAIPathfinding` (OFF): detailed per-operation metrics for behavior evaluation, native path finding and movement, enabled only for a second diagnostic pass.
- Limits output to summaries, top 5 units, 12 slow samples and 6 heartbeats per turn; maintains zero AI-performance logging by default.
- No changes to existing 1.4.7 gameplay algorithms, 26 native byte patch sites, mission area geometry, or the orphan-task prevention introduced in 1.3.11.
- Includes reproducible investigation procedure, offline tests and optional external CPU sampling.
- **No verified performance fix yet.** Build and gameplay testing on Windows still required.

---

# Larger Maps changelog

## 1.4.7 — native mission-bounds correction (experimental)

- Correct the upstream 42x42 `MissionTemplate.GetBounds()` that clipped DEFEND into 0x0 and prevented correct deployment/spawn areas beyond the vanilla boundary.
- Add two SHA-256-locked native immediate edits to the existing atomic 24-edit transaction (26 verified sites total), only on 72/80/88/96 maps. 64x64 remains on the original code.
- Rebuild native enemy border spawn bands at the full selected length, scaling only their narrow thickness relative to the 64x64 reference.
- Keep DEFEND smaller than deployment as defined by the original mission, not force both to fill the entire compound.
- Preserve AI lifetime, pathfinding, terrain and objective bug fixes. Diagnostic logging remains opt-in and bounded.
- Still requires Windows compilation and in-game validation; see `ROOT-CAUSE-1.4.7.md`.

## 1.4.6 — diagnostic baseline

- **Bug fix (candidate):** on enlarged maps, leave MENACE-generated north/east/south/west/corner border rectangles unchanged. In 1.4.5 these were proportionally modified *after* native calculation, which could shrink/shift/clip enemy spawn and reinforcement bands. Native border rectangles already use current map dimensions; don't double-transform them.
- **Diagnostic:** new `DiagnoseMissionAreas` mod option (OFF by default, restart required). Logs at most 160 brief `AREA DIAG` records *per MissionPreview* for MissionAreaConfig.AddToList: area type, role, scoped call, native input rectangle, and output rectangle. Includes unscoped calls so role/hook mismatches can be identified; not per turn/frame.
- `64x64` reference remains unchanged relative to 1.4.5. Anchored DEFEND/deployment geometry is not yet confirmed correct on 96x96; treat this version as experimental and inspect the new diagnostic traces before further geometric changes.
- No changes to native pathfinding backend, AI lifetime safety, terrain holes or mission combat balance. Debug diagnostics remain opt-in.

---

# Larger Maps 1.4.5 — restore 64-reference proportions for ALL mission zones

- Fixed the 1.4.4 regression: the whole primary compound is **no longer an enforced minimum** for DEFEND or player deployment. The objective scoring area and the blue deployment area intentionally remain separate.
- On maps **64x64**, the newly-added 1.4.x objective/deployment enlargement is disabled. Existing 1.3.12 corrections remain; 64x64 is the runtime reference geometry rather than an assumed 20x22 compound or a modified 1.4.4 screenshot.
- At **72 / 80 / 88 / 96**, proportionally scale the native resolved rectangle size by map_size/64, respecting signed/inclusive scoring regions and half-open spawn/deployment regions. Keep local chunk/setpiece anchors and any intended offset; never relocate to a guessed compound center.
- All 11 `MissionAreaType` values are covered according to semantics: `PrimaryChunks`, `Chunk`, authored `Rect`, and all four borders and four corners. Map-relative native border lengths are not scaled twice; only border thickness/insets are proportioned.
- Preserve existing authored 42->N spawn / AI-zone transforms, 1.3.12 special capture/evac safety and 1.4.2 invalid-spawn safeguards. Valid small spawn rectangles are not enlarged arbitrarily. No AI, pathfinding, weapon, terrain or unit-count changes.
- Added `TEST-PLAN-1.4.5.md` and new geometry/identity tests. Debug remains OFF by default and no per-event/per-turn diagnostic logging is added.

**Validation:** 41 tests passed offline against C# source contracts, known native patch bytes and game data; this is a source-only authoring build. Needs Windows compilation and real-game verification before release. Geometry of mission-specific script-only areas that bypass `MissionAreaConfig.AddToList` cannot be guaranteed by this common hook.

# Larger Maps 1.4.4 — DEFEND and player deployment anchored to primary compounds

- Fixed undersized, off-center areas on large military outposts (notably **Repel Counterattack**). The previous objective fallback grew a small marker but did not consult the placed setpiece footprint.
- `DefendArea` and primary-compound `CaptureArea` now derive their region from the nearest actual placed primary chunk (`MapBlueprint.Chunks`, `ChunkBlueprint.Primary`, `GetBounds()`), then scale the footprint from a **64x64 baseline** to 64/72/80/88/96.
- Fixed the **player deployment rectangle** separately. Missions using `PrimaryChunks` (including `BaseExpand=-3`) no longer leave a valid but small blue deployment rectangle untouched; the new area is centered on the placed compound and uses half-open RectInt dimensions.
- Kept separate compounds separate by matching each rectangle to its nearest primary chunk. Rectangles are clipped inside the selected map. Triggers, single-target sabotage, evacuation, rescue and enemy spawn rules retain their earlier specialized behavior.
- Safe fallback to the 1.4.3 generic scaling or spawn safety when native chunk bounds cannot be identified; at most one useful message per path by default. No per-turn logging, no new AI changes, no map size restrictions added.

**Validation:** Offline source/math tests and game-template/native fingerprint checks only. Test in MENACE before publishing; no Windows game compilation performed in this environment.

# Larger Maps 1.4.3 — proportional mission objective zones

- Automatically discovers and hooks native `Objective.Init` implementations, instead of keeping a mission-name allowlist. Every `MissionAreaConfig.AddToList` call originating in those objective initializers can now be classified by its actual configuration field.
- Resizes final mission control, defend, capture, investigate, sabotage, evacuation, rescue/extraction, recovery drop-off, landing and event-trigger rectangles consistently for 64 / 72 / 80 / 88 / 96.
- Authored `Rect` areas scale position and footprint from vanilla's 42x42 coordinates. Resolved `PrimaryChunks` and `Chunk` areas instead grow **around their actual generated center**: compounds and buildings keep their anchors. Border areas remain attached to map edges, scaling only authored widths and leaving map-relative percentage spans alone.
- Objective `RectInt` geometry preserves signed/inclusive endpoint conventions and is clamped to the playable tile range; scoring/mission-failure code and mission-prep preview read the same final lists. Scoring/extraction zones receive a size-proportional vanilla 5-tile minimum, while trigger zones have no artificial minimum to prevent accidental early activation.
- Preserves the existing AIZones authored-Rect scaling without applying it twice; leaves separate AI/special actor spawn normalization (1.4.2) and 128x128 pathfinding (1.4) unchanged. No AI combat behavior, enemy budget or number of units changed.
- Logging remains minimal: at most one example of a rescaled objective rectangle per process, no per-turn/per-frame output. New source tests cover all 11 `MissionAreaType` values and all five supported sizes.

**Validation:** offline contract/geometry and native signature checks only; the DLL has not been compiled or tested in-game in this environment. Especially test objective preview size, score boundaries and setpiece alignment across mission classes before release. Some mission-specific scripted areas may bypass the common native `MissionAreaConfig.AddToList` path and require follow-up if observed.

# Larger Maps 1.4.2 — selectable map sizes and spawn-area safety

- Map size option now snaps to **64 / 72 / 80 / 88 / 96** tiles; restart MENACE after changes. The native patch uses its exact build fingerprint and 24 original byte checks for each extended size. Fallback stays 64x64.
- Repairs `MissionAreaConfig` generated zones with zero/negative width or height, and prevents out-of-bounds rectangles. Explicit authored areas scale to actual map dimensions; chunk/setpiece-anchored rectangles keep their actual generated coordinates.
- Adds final guards at `Mission.TryCreateInitialAiUnit` for civilians, special/rescue targets, and regular enemy spawn areas. Also guards the two rectangle-taking `TacticalManager.TrySpawnUnit` overloads used for tactical spawns/reinforcements. Very small/invalid areas grow **locally** to a 5x5 minimum (not relocated to arbitrary map borders) and are clamped within the actual map size.
- Retains 1.4.1 AI route diagnostics (OFF by default), 1.3.11 AI task lifetime fix, and 1.3.12 terrain hole fix. No changes to AI goals, movement costs, or enemy unit counts.
- Logs only the first corrected spawn rectangle per mission by default; detailed diagnostics remain disabled.

**Status:** Source-only test build, *not* compiled or run in MENACE here. Spawn fixes are grounded in `Player.log` reports of 0/-1-width or height regions. A spawn can still fail when a whole 5x5 neighborhood is obstructed, and native patches remain version-locked. Verify 64,72,80,88,96 modes and sample mission categories before release.

# Larger Maps 1.4.1 (diagnostic patch)

- Adds an **optional** `Diagnose AI Routes` setting (OFF by default; restart required) to distinguish missing AI operational zones from missing path destinations.
- When enabled, logs once per AI agent turn (max 120 entries) the operative zones, current tile, sleeping state, and zone/distance role weights. On AI Move execution, reports chosen target/ultimate tile, path length and candidate count (max 200 entries).
- Does **not** change AI behavior, movement heuristics, pathfinding, generated map size, or native patch bytes. The 1.4 native backend remains unchanged.
- Ordinary gameplay retains zero added diagnostic hooks and zero added per-turn logs.

**Status:** Investigation build, not a proven fix for objective behavior. Compare mission behavior on 64 and 80 with the optional diagnostics before changing tactics.

# Changelog

## 1.4 — experimental extended pathfinding (not yet game tested)

- First opt-in native 80x80/96x96 pathfinding backend; 128-wide internal nodes/closed bitmap, 12 stride corrections, resized priority queue and map-bounded initialization loops.
- Version-locked 24 instruction patch sites; all checked before any in-memory write; 64x64 fallback for unsupported binaries.
- TacticalMapSize option (64/80/96, restart required); default 80 for controlled testing.
- Keeps AI task lifetime, terrain holes, foliage, elevation, objectives, deploy areas, and zoom fixes from 1.3.x.
- Source/authoring prototype only: game behavior and Windows runtime installation have **not** been validated.


## 1.3.12 — Unity terrain holes indexing safety (bugfix)

- Fixed `IndexOutOfRangeException` from `Menace.Tactical.Map.AddHole -> TerrainHole.Start` on rectangular terrain-hole regions, especially near map borders.
- Verified the native MENACE v0.7.15 `Map.AddHole` implementation obtains `TerrainData.GetHoles(x,z,width,height)` (which returns `[height,width]`) but incorrectly indexes the returned buffer as `[x,z]`. The result can access a non-existent row/column for non-square regions.
- Safely replaces only `Map.AddHole`'s hole-sampling loop with the original clipped/rounded coordinates and circular mask, using Unity's correct `[z,x]` indexing. Array dimensions and element count are checked against the game's native IL2CPP x64 layout before writing. If the layout differs, vanilla is used as a fallback with one warning.
- No terrain hole, 64x64 map tile, objective, reinforcement, cover, AI, or other game-mechanics change intended. Preserves the tested 1.3.11 AI task lifetime fix without modification.
- No per-hole logging unless source-level Debug is ON; one startup confirmation and meaningful failures only.
- **Not yet tested in running MENACE**; validate actual holes, terrain rendering, and both logs before distribution.

## 1.3.11 — AI task lifetime safety (bugfix, 64x64 unchanged)

- Fixed the underlying **detached AI task** failure mode demonstrated by MENACE's native `AIFaction.RunWithTimeout`: it starts an inner task for the AI action, waits at most **12,000 ms**, then returns even when that inner task is still running. Later AI passes can collide with the unfinished calculation, matching the reproduced `Agent.Evaluate()` concurrent dictionary errors and frozen enemy turn.
- Harmony-prefixes only `AIFaction.RunWithTimeout(Action, Actor)` to return the **actual AI work task** via native `Il2CppSystem.Threading.Tasks.Task.Run(action)`. The engine therefore sees completion only when that work really ends. This avoids silently abandoning an unfinished evaluation or corrupting the same AI state with overlapping passes.
- Preserves the game's own asynchronous task machinery; eliminates one needless wrapper task per call. Does **not** freeze the main thread, force synchronous calculations, skip units, widen timeouts by an arbitrary amount, swallow exceptions, alter AI scores, or change combat mechanics.
- Keeps tactical battlefields **64x64**. All map-generation, mission placement, elevation, foliage, cover and camera code is otherwise unchanged.
- Normal gameplay logging remains limited to one installation confirmation (or an actionable installation error); no per-task/per-frame log output. Existing development diagnostics remain OFF.
- **Validation required in game:** the change prevents the timeout wrapper from abandoning unfinished tasks; it cannot guarantee short enemy turns or rule out separate tile/LoS defects. Because the timeout wrapper itself is replaced, absence of `AI task timed out` alone does not prove performance — observe actual enemy-turn completion times and check the full Player.log for dictionary errors.


## 1.3.10

- Fixed a concrete 42x42/64x64 elevation-buffer mismatch on enlarged tactical maps. MENACE's `MapElevationTexture.Resize` correctly expects a 2048x2048 target for a 64x64 map, but the shared `TacticalConfig.MapElevationTexture` remains authored at the vanilla 1344x1344 size and vanilla only logs the mismatch instead of resizing it.
- Larger Maps now resizes that existing shared RenderTexture in place before vanilla elevation setup (`Release -> width/height -> Create`), preserving the same object/reference while matching the actual map dimensions. The same hook also restores the vanilla 1344x1344 size if a 42x42 map is used later.
- This specifically targets the runtime errors `map_elevation_render_texture width/height should be 2048 but is 1344` seen immediately before an otherwise stackless/native-looking crash.
- Retains the 1.3.8 minimum **5x5 CaptureArea** fix: finalized capture rectangles smaller than 5x5 are expanded around their existing center/anchor; already-large areas remain unchanged; signed/inclusive RectInt semantics are preserved.
- Retains the 1.3.9 **ObjectiveConfig.AIZones** fix: authored map-space `Rect` AI zones are proportionally mapped from 42x42 coordinates to the active enlarged map, while chunk/setpiece and border zones retain their native anchoring/behavior.
- Detailed per-area/per-zone diagnostics remain debug-only. Normal gameplay adds only the bounded elevation-resize confirmation plus warnings for real failures.

## 1.3.9

- Proportionally mapped authored `ObjectiveConfig.AIZones[].Area` rectangles from vanilla 42x42 map coordinates into the 64x64 battlefield.
- Only explicit map-space `Rect` zones are transformed. Chunk/setpiece-relative zones and border zone types are left to MENACE's native resolution so their anchors are not displaced.
- Per-zone logging is debug-only.

## 1.3.8

- Added a minimum **5x5** finalized footprint for `CaptureArea` objectives on enlarged maps, matching the existing evacuation safety rule.
- Capture rectangles that are already 5x5 or larger are unchanged. Smaller areas expand around the same approximate center/anchor and are clamped to the live map bounds.
- Handles MENACE's signed/inclusive `RectInt` deltas correctly.
- Per-area diagnostics are debug-only.

## 1.3.7

- Fixed floating decorative terrain props, especially small/medium/big rocks, on enlarged 64x64 maps.
- Root cause: the managed `TerrainPointSpawner.ProcessTerrainPoints` replacement that performs corrected world-to-heightmap reprojection for Decorative and Ground TerrainPoints was accidentally installed only inside the source-debug diagnostics block. Since stable builds use `Debug() == false`, normal 1.3.6 gameplay fell back to the vanilla 1:1 height-sampling assumption.
- The functional TerrainPoint spawner replacement is now installed unconditionally as a core Larger Maps compatibility fix.
- Verbose GroundCover/TerrainPoint diagnostics remain debug-only and OFF by default; normal gameplay adds no per-point or per-frame logging.
- The existing gameplay-point reprojection, heightmap safety clamp, 64x64 map generation, spawn/deployment/objective scaling, tile highlighter, timed-mission pacing, and camera behavior are unchanged.

## 1.3.6

- Fixed Recover Object **DROP-OFF** zones being left in vanilla 42x42 map coordinates while EVACUATE was correctly moved into 64x64 coordinates.
- `RecoverObject.Init` now scopes only the `RecoverObjectsConfig.PlayerDropOffAreas` `MissionAreaConfig` entries for enlarged-map scaling. Object spawn areas and unrelated objective areas are not changed.
- Chunk/setpiece-relative drop-off areas remain anchor-preserved; only true map-space drop-off rectangles are proportionally mapped, matching the existing evacuation placement rules.
- Keeps logging quiet: the new drop-off scaling path only emits per-rectangle diagnostics when debug is enabled.
- Bugfix only; no new gameplay feature or balance change.

## 1.3.5

- Fix build failure on older compiler reference set by removing the LINQ `SelectMany` diagnostic lookup that required `NullableAttribute`.
- No gameplay or balance changes.

## 1.3.4
- Updated against the fresh current MENACE dump.
- Removed the obsolete `MissionPreview.SetPreview` hook; that method no longer exists in the current game.
- Updated the debug-only Mission Prep diagnostic hook from `LaunchMission` to the current `StartMission` method.
- Diagnostic-only Harmony hooks are no longer installed at all when debug is OFF, keeping targeting/AOE/map-generation hot paths clean in normal play.
- Made the debug AOE comparer probe resilient to compiler-generated display-class renumbering.
- No map-size, mission-scaling, foliage, camera, deployment, or balance behavior changed.

## 1.3.3
- Added targeted diagnostics for the reproduced smoke/AI slowdown path without changing gameplay behavior or suppressing native exceptions.
- Hooks `Skill.IsUsableOn` and `Tactical.LineOfSight.HasLineOfSight` only to report slow calls or exceptions, including skill/actor identity, origin/destination pointers and coordinates, LoS flags, 64x64 map/blueprint dimensions, and TacticalManager/map binding.
- Adds one-shot `Map.UpdateBounds` lifecycle markers showing the blueprint dimensions used to resize the runtime tile map and the resulting runtime tile count.
- On LoS/targeting failures, re-fetches the same coordinates from the live runtime `Map` and reports whether the returned tile has the same native pointer. This directly tests whether failures correlate with coordinates beyond vanilla 0-41 or with stale/dead tile wrappers.
- Expanded AOE diagnostics with pre/post list snapshots, explicit null/dead-pointer counts, nearby entries around bad indices, and the comparer target tile.
- Added `GetRandomAoeTile` null-production diagnostics because native `QueryTargetTiles` can add that return value to the output list without a null check for random-AOE modes.
- The older broad AI/pathfinding probes are not installed while their hard-disabled diagnostic switch is off, keeping this investigation focused and reducing hot-path Harmony overhead.
- No null filtering, forced false LoS result, actor skipping, pathfinding changes, or other workaround is applied. This is a diagnostic bugfix build for the existing 1.3 branch.

## 1.3.2
- Fixed the remaining 5x5 evacuation-zone bug found in missions whose runtime `RectInt` uses negative width/height deltas.
- MENACE treats these signed deltas as inclusive endpoints (`-1` means a 2-tile span), so 1.3.1 incorrectly rejected valid 2x2 evacuation rectangles as invalid.
- The runtime and anchor-aware evacuation minimum now measure the real tile span as `abs(delta) + 1`, preserve the original axis direction, and expand undersized zones to a true 5x5 footprint (`+/-4` raw delta).
- No new gameplay feature; this is a bugfix for the 1.3 evacuation-area feature.

## 1.3.1
- Fixed the 1.3 minimum evacuation footprint not taking effect on some missions.
- The 5x5 safety minimum is now enforced directly on the finalized `EvacuateToArea.m_Areas` runtime rectangles after vanilla `Init`, rather than relying only on the scoped `MissionAreaConfig.AddToList` path.
- Added an `EVAC RUNTIME AREA` log marker showing the actual runtime rectangle before and after expansion.
- No new gameplay feature; this is a bugfix for the 1.3 evacuation-area feature.

## 1.3
- Evacuation objective areas now have a minimum footprint of **5x5 tiles**, preventing missions with larger forces from becoming impossible because the extraction zone is too small.
- Chunk/setpiece-anchored evac zones keep their generated position and expand around the same anchor; true map-space evac zones still scale proportionally and are then clamped to at least 5x5.

## 1.2
- Added focused diagnostics for the reproduced AI-turn slowdown in `Skill.QueryTargetTiles` / `Skill.GetAoeTiles`.
- Logs the exact skill, actor, origin/target tiles, query mode, map dimensions, and output tile list when targeting throws.
- Logs null AOE sort operands before MENACE's native comparer throws, including the captured AOE target tile.
- Distinguishes tiles inside vanilla 42x42 space, valid enlarged-map tiles beyond coordinate 41, and coordinates outside the 64x64 map.
- Renamed the misleading `StaticTiles=42x42` diagnostic to `VanillaConstants=42x42`; the runtime tile map remains 64x64.
- No null filtering, actor skipping, pathfinding changes, or other speculative gameplay fix is applied in this diagnostic build.

## 1.1
- Fixed evacuation/extraction setpieces staying too close to the center of 64x64 maps.
- All current authored evac setpieces now scale their center-relative offset from 42x42 to 64x64, preserving the intended direction and travel distance.
- Evac setpiece variance is scaled as well, preserving the vanilla randomized placement range on the larger battlefield.
- Escort evacuation destinations now use the same general evac placement logic instead of a mission-specific special case.
- `EvacuateToArea` objective rectangles still scale when they are true map-space areas, keeping vehicle extraction zones appropriately positioned and sized.
- Other authored objective areas remain unchanged.

## 1.0
- First stable release, promoted from the validated 0.29 build.
- Includes the complete AI-safe 64x64 map pipeline: enlarged blueprint/map generation, Mission Prep extent correction, tile-highlighter correction, enemy/reinforcement spawn scaling, timed-mission pacing, and extended tactical zoom.
- Includes the finalized terrain/foliage height fixes from 0.28.
- Includes the 0.29 mission-layout fixes: chunk/setpiece deployment areas remain anchored to their generated structures, and `KillUnit` objective target spawn areas scale into enlarged-map space.
- Objective areas themselves remain vanilla to avoid detaching authored objectives from their POIs/setpieces.
- Simplified Mod Options for the stable release: only `MissionRoundMultiplier`, `ExtendedZoomOut`, and `ZoomOutMultiplier` are user-facing.
- Core 64x64 compatibility fixes are now always enabled and cannot be accidentally disabled through stale or user-edited settings.
- Development-only debug and AI diagnostic switches are no longer exposed in Mod Options.
- No gameplay/map-layout changes from the validated 0.29 behavior.

## 0.29
- Fixed chunk/setpiece-relative player deployment zones being incorrectly origin-scaled after vanilla had already resolved them against the generated 64x64 blueprint. `PrimaryChunks` and `Chunk` rectangles are now preserved in place; this targets the off-center Secure Repair Depot deployment zone.
- Added a dedicated `KillUnit` target-spawn scaling scope around both `KillUnit.Init` and `KillUnit.OnCreateInitialAiUnits`. This covers special destroy/kill targets such as the Pirate First Mate, which bypass the ordinary `GenericMissionTemplate.OnCreateInitialAiUnits` enemy-spawn scope.
- Regular border/rect enemy spawn and player deployment areas continue to scale proportionally from 42x42 to 64x64.
- Retained the 0.28 managed TerrainPointSpawner / gameplay-point foliage-height fixes unchanged.
- Added explicit diagnostics for anchor-preserved deployment rectangles and objective-target spawn rectangles.

## 0.28
- 0.27 diagnostics proved `GroundCoverScatter` is working: the test generated ~162k Ground points (grass/tall grass/flowers/ferns), and they reached `TerrainPointSpawner.ProcessTerrainPoints`.
- Found the real 0.26/0.27 failure: the Harmony prefix on native `TerrainUtilities.TryResolveTerrainPoint(ref TerrainPoint, ...)` received a zeroed `TerrainPoint` bridge (`AssetTypeID=0`, `Flags=0`) even though the blueprint list contained the correct values. Skipping the native body therefore left the real points unresolved vertically.
- Removed the 0.26/0.27 `TryResolveTerrainPoint` replacement from the active hook path.
- Replaced `TerrainPointSpawner.ProcessTerrainPoints` only on enlarged maps with a managed equivalent that reads the real `MapBlueprint.TerrainPoints` list, preserves vanilla slope/infill rules, converts 64x64 world coordinates to the fixed 513x513 heightmap domain, and feeds resolved Decorative/Ground points to the original GPU renderer.
- Added a direct patch for `PointPropGenerator.ReprojectGameplayPoint` so ForestDense/ForestSparse/ForestSingle gameplay points use the same corrected world-to-heightmap conversion.
- Kept the final `GetWorldHeightBilinear` bounds clamp as crash protection and retained the 0.27 foliage diagnostics for validation.
- Expected result: grass should return at terrain height and floating forest/decorative vegetation should be substantially reduced or eliminated.

## 0.27
- Kept the 0.26 terrain-height resolver behavior unchanged; this build is diagnostic rather than another speculative visual fix.
- Added targeted `GroundCoverScatter.ModifyBlueprint` probes showing TerrainPoint counts, raw asset IDs, group distribution, flags and X/Z bounds before and after ground-cover generation.
- Added `TerrainPointSpawner.Initialize` and private `ProcessTerrainPoints` probes to show exactly which Ground/Gameplay/Decorative points reach the spawner and how many GPU points/renderers it creates.
- Added raw resolver `AssetTypeID` and flag histograms to the final terrain-resolve summary.
- Added generation-pass list/runtime-type diagnostics and per-`SpawnMap` TerrainData snapshots (heightmap, alphamap, detail prototypes, tree prototypes/instances, terrain layers and size).
- Purpose: determine whether missing grass originates in `GroundCoverScatter`, point routing/culling, or a separate rendering path, and whether floating trees bypass the resolver through `GameplayTerrainPoints`.
- All existing 64x64 map, Mission Prep/deployment, enemy spawn scaling, highlighter, zoom, timed-round and AI behavior is otherwise unchanged.

## 0.26
- Replaced the ineffective 0.25 nested height-sampling scope after logs proved `TryResolveTerrainPoint` ran 173k+ times while the nested heightmap mapping hook ran zero times.
- Added a managed replacement for `TerrainUtilities.TryResolveTerrainPoint` on enlarged blueprints only.
- Reproduces vanilla slope filtering and infill validation, then samples the fixed 513x513 cached heightmap using corrected 64x64 world-to-heightmap coordinates.
- Handles Decorative, Gameplay (including forest/tree terrain assets), and Ground (grass/fern/etc.) TerrainPoints without deleting them.
- Leaves vanilla 42x42 resolution behavior untouched.
- Added per-group resolver diagnostics and failure counters so the next test can verify that grass/tree points are actually passing through the corrected path.
- Kept the 2.0x default mission round multiplier and all 64x64 map, deployment, enemy spawn, highlighter, zoom and other diagnostics behavior.

## 0.25
- Replaced 0.24 terrain-point border culling with a true world-to-heightmap coordinate conversion inside `TerrainUtilities.TryResolveTerrainPoint` only.
- Correct conversion uses the 512 intervals of the 513x513 heightmap: `sample * (HeightMapRes - 1) / TotalWorldUnits` (512/688 on the current 64x64 terrain).
- TerrainPoints are no longer discarded just because their border-shifted world coordinate is above 512.
- This should restore grass/detail TerrainPoints and align reprojectable tree/vegetation height with the enlarged Unity terrain.
- Removed the broader `PointPropGenerator.SpawnPrefabs` reprojection scope used experimentally in 0.23; point props are intentionally left untouched until separately proven necessary.
- Added terrain resolve summary diagnostics and retained the final bounds clamp only as crash protection.
- Kept the 2.0x default mission round multiplier and all 64x64 map, deployment, enemy spawn, highlighter, zoom and diagnostics behavior.

## 0.24
- Reverted the broad terrain-height reprojection introduced in 0.23 after testing showed ordinary grass/detail instances could disappear below the visible terrain.
- Added a targeted `TerrainUtilities.TryResolveTerrainPoint` guard: decorative terrain points are skipped only when their vanilla border-shifted height sample would fall outside the fixed 513x513 cached heightmap.
- Valid `0..512` terrain-height samples are now left exactly as vanilla generated them.
- Retained the `GetWorldHeightBilinear` clamp only as a final crash-safety fallback for unexpected callers.
- Kept the 2.0x default mission round multiplier and all 64x64 map, deployment, enemy spawn, highlighter, zoom and diagnostics behavior from 0.23.

## 0.23
- Fixed enlarged-map terrain decorations/grass sampling the wrong vertical height in the far portion of the map.
- Identified the underlying vanilla assumption: `TerrainUtilities.TryResolveTerrainPoint` and `PointPropGenerator.SpawnPrefabs` pass border-shifted world coordinates directly to a 513x513 heightmap because vanilla total terrain width is 512 world units.
- Added narrow call scopes that reproject only those world-space samples by `HeightMapRes / TotalWorldUnits` before `GetWorldHeightBilinear`; already-normalized callers are not rescaled.
- Retained a final heightmap bounds clamp only as a safety fallback.
- Added configurable `MissionRoundMultiplier` (1.0x-3.0x, default 2.0x) for all currently round-limited mission objective configs.
- Timed attack objectives get more travel time; defense objectives last proportionally longer so distant enemy spawns do not make them trivial.
- Round limits are adjusted on the objective config before creation so gameplay logic and displayed limits stay consistent.
- Kept 64x64 map generation, Mission Prep/deployment scaling, enemy spawn scaling, highlighter, zoom and diagnostics unchanged.

## 0.22
- Fixed player deployment zones remaining in the vanilla 42x42 coordinate space while Mission Prep and Tactical use 64x64.
- Added a scoped `MissionTemplate.CreateDeploymentZones(...)` hook so only player deployment rectangles generated through `MissionAreaConfig.AddToList(...)` are proportionally mapped into the actual enlarged map dimensions.
- Player deployment-zone scaling is part of the 64x64 map fix and is not exposed as an optional setting.
- Objective areas remain vanilla; simple proportional objective scaling is intentionally not restored because it can detach objective zones from their authored POIs/structures.
- Kept 0.21 terrain-height boundary protection, enemy spawn scaling, Mission Prep extent correction, highlighter fix, zoom, and diagnostics unchanged for final pre-1.0 testing.

## 0.21
- Fixed the terrain-generation boundary failure exposed by 0.20 diagnostics.
- Added a narrow safety prefix for `MapBlueprint.GetWorldHeightBilinear(float,float)` that clamps only out-of-range heightmap coordinates to the valid cached-heightmap domain.
- This targets the observed `IndexOutOfRangeException` from `TerrainPointSpawner.ProcessTerrainPoints -> TerrainUtilities.TryResolveTerrainPoint -> MapBlueprint.GetWorldHeightBilinear`.
- Kept SpawnMap diagnostics temporarily so the next test can verify that terrain spawning now completes normally instead of aborting through the diagnostic trampoline.
- No changes to 64x64 map size, Mission Prep extent, tile highlighter, enemy spawn scaling, zoom, AI behavior, or objective-area handling.

## 0.20
- Added targeted diagnostics for `TerrainMapGenerator.SpawnMap`, the exact nested coroutine where the 0.19 loading trace stops.
- Logs SpawnMap coroutine state transitions, yielded objects, pass count/current pass when discoverable, biome/terrain presence, and step timing.
- Startup probes now include both parent `Map.GenerateMap` and child `SpawnMap` progress.
- No gameplay behavior changed from 0.19.
- Mission Prep remains 64x64, tactical map remains AI-safe 64x64, enemy spawn scaling and extended zoom remain unchanged.
- Objective-area scaling remains removed.

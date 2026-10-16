# Long tactical mission — 16 October 2026

## Preserved evidence, not a new release

The author reports approximately 90 minutes of play and broadly positive results.
The retrieved log spans **10:48:53.392–13:08:03.053**; that is elapsed logging time,
not a measurement of active play. Tactical scene entry is 10:50:11.590, followed by
a return to Strategy at 13:07:24.700. Do not infer the mission outcome or save-load
provenance from those scene transitions alone.

- MENACE **v0.7.17+22362**.
- Direct PC retrieval of `MelonLoader/Latest.log`: **5,674 lines / 1,315,673 bytes**.
- SHA256: `e620dc36bae8ecc5c3eeb04e7eed6900af0e803ed6811c5366f0064a805d4279`.
- Remote metadata independently matches the archived size/hash; last write
  **2026-10-16T11:08:03.0526148Z**. Game confirmed closed; no new launch requested.
- Private, gitignored archive:
  `game_files/v0.7.17/validation/2026-10-16-long-mission/Latest.log` (owner-only access).
  The first retrieved copy is also retained in `.tmp/mission-2026-10-16/`.
- Reproducible aggregate analysis: run `python3 mods/GameplaySuite/tools/analyze_mission_log.py`
  with the archived log path. It emits allowlisted identifiers/counts, not raw log
  messages, actor names, save paths or settings files. Local output: `analysis.json`
  beside the first retrieved copy. Raw logs and game binaries are not published.

## What this mission establishes

- **459 consecutive AI actor-activation summaries**, #1–459, across **rounds 1–18**.
  These are not 459 faction turns. Every summary has `pending=0`, `droppedTasks=0`.
  Closure: 445 `finish`, 14 `next-activation`. The first-round crash/stall did not
  recur during this session; this is not a controlled AI-performance comparison.
- **25 Assault starts and 25 attacker-fire records**; 17 victory advances, 8 repulsed.
  All 17 defensive-fire records report `used=True`; **12 report `pinned=True`**.
  This directly corroborates the author's reversed defensive-suppression report.
- The 17 defensive post-shot samples show HP/armor/morale changes in 4 cases and
  suppression changes in all 17. Do not misread the before→after values as only the
  first number. `intendedMatch=False` is not proof of a wrong victim: the diagnostic
  compares managed wrapper identity (`RuntimeHelpers.GetHashCode`), not native entity
  pointers. Combat retargeting was not changed on that evidence.
- **222 MEN-004 ammo-ledger records**; the 12 sampled `ConsumeUse` operations each
  decrement exactly one use. This does not close #4: separate `SetUses` records and
  incomplete action coverage cannot prove all alternate-fire ammunition behavior.
- Visibility diagnostics captured **100 events** from 10:50:17.048 to 13:03:46.363
  and reached the configured event cap. In the 173 recorded property snapshots,
  Vision application count is always 1; Concealment application count is 1 in 167
  and 0 in 6, never >1. This supports the captured rebuild behavior, not exhaustive
  visibility correctness for every actor/frame. No +10/+10 gameplay rule was removed.
- **Two ERROR lines, both the known external Custom Leader failure** on
  `BlackMarket.FillUp`. No camera callback/disable/hook-unavailable errors found.
  There are **13 warnings**: five legacy scene diagnostics; existing legacy save
  and squaddie-hook probes; Starting Options field fallback; interop StrategyConfig;
  Custom Leader; external extractor; two unclassified startup warnings. All 13
  warning messages match messages already present in earlier local test logs after
  timestamp/address normalization. This is not an error-free log or a fix to those
  external/legacy warnings.

## User acceptance and remaining scope

- **#46 closed** on the author's explicit request after gameplay with VO 3.2.0.
- **#43 closed** on the author's explicit HUD click-through acceptance. Individual
  gestures are user-observed, not proved by a quiet input log.
- **#45:** substantial long-session/AI evidence now exists. Keep feature-specific
  gates honest: no WRR `Applied range pass` / `write failures=0` records in this log;
  no unambiguous logged CF/OW/Smoke activation or actual Field Logistics resupply.
  Configuration/skill presence is not the action. Save-load provenance and installer
  matrix are also not established. Do not request another long mission for the AI
  evidence already obtained, and do not advance the release/verified-build marker.
- **#33:** author says the camera seems fine; no camera errors found. Record positive
  integrated runtime acceptance, without claiming separate F6/F7, vehicle, preference
  round-trip, clean/upgrade/standalone checks that were not individually reported.
- **#35 / #27:** dynamic leaders are unpublished and still being developed. Keep
  that work open; it is not a blocker for a stable distribution excluding that feature.

## Focused PC acceptance after deployment — 16 October 2026

After the long-session log above, Infantry Assault **0.5.9**, Building Combat Rules
**0.2.0** and Cinematic Action Camera **1.1.0** were installed on the PC following a
dated backup at `_MENACE_Backups/20261016-133207`. The author confirms all three
focused checks passed: Suppressed defenders react while Pinned defenders do not;
personal-weapon bullets leave trees intact while the heavy/explosive control remains
allowed; and automatic firing view points from the attacking squad toward its target.
This is the author's in-game acceptance, not an independently observed video test.

The post-deployment `Latest.log` was retrieved privately: 1,268 lines, 168,894 bytes,
SHA256 `ac3962fd0355da2a74bba07d78a0b8f6c53084817abb27713fad8429eda33626`, covering
13:33:36.040–13:40:29.600. It identifies MENACE **v0.7.17+22362** and the three
installed versions above. Four Assault starts and two defensive-fire records appear;
no camera failure records appear. Two unrelated known Custom Leader errors remain.
The concise log does not independently prove the tree/weapon outcomes or camera
framing; those are recorded from the author's direct confirmation. Issues #47–#49
were closed on that acceptance and can be reopened if a regression appears.

## Installed versions observed in this log

These are the tested binaries, not the subsequently edited source inventory.

| Package | Observed version |
| --- | --- |
| MenaceModSettings | 0.6.1 |
| BuildingCombatRules | 0.1.5 |
| BuildingsBlockLineOfSight | 0.2.3 |
| CinematicActionCamera | 1.0.0 |
| CoordinatedFire | 1.6.10 |
| CoverDestructionRebalance | 0.4.0 |
| FieldLogistics | 1.0.11 |
| ForestTraversal | 0.2.0 |
| GarrisonableBuildings | 1.0.5 |
| InfantryAssault | 0.5.8 |
| LargerMaps | 2.2.0 |
| Overwatch | 0.32.3.6 |
| SmokeOverhaul | 0.6.4 |
| SquaddieVeterancy | 1.10.2 |
| StartingOptions | 0.1.1 |
| StrongerCover | 3.0.1 |
| VisibilityOverhaul | 3.2.0 |
| WeaponMechanicsRebalanced | 0.4.9 |
| WeaponRangesRebalanced | 2.9.2 |

## Deployed follow-up versions and acceptance scope

- **#47 / InfantryAssault 0.5.9:** Suppressed defenders can react with half CQB
  bonuses; Pinned Down (suppression or stance) cannot. Real stuns, dead/dying
  defenders and weapon restrictions still veto fire. Attacker eligibility, AI
  target selection, fixed 40 AP fee and ammunition code are unchanged.
- **#48 / BuildingCombatRules 0.2.0:** native vegetation structures (trees/forest)
  receive the existing small-arms guards in both structural damage and half-cover
  destruction paths. Separate default-ON tree toggle; existing building setting key
  is preserved. Explosive/heavy IDs take priority over a rifle item's native tag,
  so a rifle-grenade mode is not protected as a bullet. Collapse safety stays
  building-only; no forest traversal/vision/cover templates changed.
- **#49 / CinematicActionCamera 1.1.0:** automatic firing view uses attacking squad
  position → attacked tile, not leader facing. Target snapshots update on accepted
  repeated attacks/retargets and clear on exit. Manual orbit, movement views,
  probabilities/debounce, HUD restoration and camera-only gameplay contract remain.

All four affected DLLs (three Jiangyu packages plus camera runtime) compiled locally
against the cached **0.7.17 proxies**, with zero warnings/errors in those builds.
Actual assembly/file metadata matches source manifests/projects. **115 Python tests**
and **15,492 offline C# checks** passed. The Python suite additionally executes the
actual extracted structure guards (86 checks) and camera geometry (99 checks) with
managed stand-ins. They do not execute Harmony/IL2CPP or simulate Unity rendering
and smoothing. These are separate from native gameplay acceptance.
The test deployment used the versioned `compiled/` payloads, plus the camera runtime
DLL, and verified their on-PC hashes against the local payloads. A canonical one-click
release installer/Nexus distribution has **not** been built or published. The backup
is retained outside `Mods`; this deployment did not change settings. No merge or public
release was performed. Reopen #47–#49 if later play reveals a regression.
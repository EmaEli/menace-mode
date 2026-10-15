# Regression follow-up — 15 October 2026

Continues the migration checkpoint in MIGRATION_STATUS.md. These test-only additions
do not change the production code or the verified migration ZIP. Work is local and
uncommitted. No PC access, installation, real user settings/saves or game tests.

## #19 — Berserk / Coordinated Fire

- Existing CF 1.6.10 still contains the merged 1.6.8 capped-grant fix. It was not rewritten.
- Linked the **actual production C# Rules/Fee helpers** into the offline runner. All
  2,072 combinations of AP caps, fees, AP already spent and zero/single/multiple grants
  pass, with carry bounded to the one fee. The original Python model remains separate.
- Added actual C# eligibility checks for the second weapon: usability, native cost
  affordability, ammo, direct fire, deployment/setup, identity and overlapping ranges.
  Source guards retain live native follow-up checks and NoApConsumed rather than Free.
- The C# runner now passes **4,545 checks**, including all earlier settings and actual
  package/DLL metadata checks. This does not execute native AP writes or Berserk events.
- Runtime gate: same save/gear, synchronized ON/OFF, AP spent 0/5/15/30+, no kill, first
  vs second-child kill, multiple kills, interrupted follow-up and native second-weapon
  affordability. For the reported 95 AP / 30 fee / one 30 grant: expected final 95, not 70.
- Debug stays OFF by default. #19 remains open until that native event/timing test passes.

## #4 — WMR ammo diagnostics, not a balance fix

- Current WMR 0.4.7 retains the existing opt-in `[MEN-004 AMMO]` snapshots and actual
  DLL 0.4.7.0 was clean-build/metadata checked in the migration checkpoint.
- New source guards check OFF defaults, eligible skill filtering, one trace call site,
  before/native/final uses/charges/consumed-per-use, rejected-action guards and bounded
  extra-call requests. A Python request-count model does **not** execute the native API.
- `ConsumeUse()` is not guaranteed to subtract one: `GetUsesConsumed()` can change.
  The present code requests at most two extra CALLS, not a proven maximum of two ammo
  UNITS. Do not certify conservation or fix the reported 16-to-0 event from these tests.
- Runtime experiment: same save/weapon, count exact normal/sustained activations; record
  UI and `[MEN-004 AMMO]` before/native/final `uses`, `charges`, `consumedPerUse`,
  `flags`, `success`, `nativeDelta`, `extraRequested`, `extraCalls`. Check reload and mode
  switching. Isolate FL sharing, CF and Overwatch after the WMR-only baseline.
- WMR gameplay OFF also skips these traces; its comparison baseline needs manual UI
  records (absence of a trace is expected, not evidence the ability failed). Turn Debug
  OFF afterward. No code/balance change, reproduction or attribution; #4 remains open.

## #14 — Water elevation: narrowed observation plan, no speculative offset

- Screenshot evidence is from game 0.7.15 / LargerMaps 1.8, 96x96, stamp density ON
  (53 placed). No recorded seed or identified water prefab. Current code is 2.0.1;
  its 64/80/96 startup/mission evidence is **not** a water-rendering acceptance test.
- Current source paths are `TerrainMapGenerator` blueprint replacement,
  `TerrainStampDensity.cs` (changes only stamp-attempt budgets), shared elevation
  render-texture resizing, `GameplayPointReprojectionPrefix` and the managed
  `TerrainPointSpawnerProcessPrefix` / `TryResolveTerrainPointManaged` path.
- Both reprojection paths convert world X/Z to cached heightmap coordinates via
  `(resolution-1)/TotalWorldUnits`, with border offset and clamping. Managed point Y
  changes only for `CanBeReprojected` / `IsInfill`; flags/asset IDs are preserved.
- 0.7.16 dump confirms `TerrainPoint.Flags`, those enum values, the blueprint height
  sampler and gameplay reprojection signatures. No dedicated Water/Lake/Pond class
  was found by that name search; it does not identify or exclude an asset/shader path.
- Four new portable source/coordinate-model guards cover these existing contracts.
  They do not sample actual terrain, find the water prefab, or prove correct world Y.
- Discriminating test: record mission, seed, biome, exact config and prefab/asset ID;
  capture water local/world transform, parent scale/Y, terrain Y at center and banks,
  blueprint sample coordinates/flags and slope from the **same positions**. Compare
  64 vs 96 and density OFF vs ON with equivalent generation where possible. Log any
  seed/config mismatch; don't call non-identical maps a controlled A/B.
- Existing capped terrain/stamp diagnostics can aid that test; keep AI route/deep AI
  profiling OFF. They do not currently capture water identity/transform. Do not add a
  global scene scan or blanket Y offset before locating the actual water object/path.
- No LargerMaps production change or verified root cause. #14 remains open.

## #20 — First Aid, rescue chance and Reinforcements are distinct

- Cached `v0.7.16/ExtractedData/SkillTemplate.json` contains two non-attacks:
  First Aid has Regeneration/ChangeMorale **and a passive ChangeSquaddieRescue**;
  Reinforcements has RefillSquaddies. First Aid's passive rescue chance must not be
  confused with the active ability that draws fresh replacements from manpower.
- The cache's pristine-vanilla/live provenance is not established; keep current tooltip
  and real living-vs-dead tests as release gates. A minimal hashed fixture preserves
  these observed fields without redistributing the full game export.
- Source guards retain attack-only reliability filtering and known-squaddie identity
  checks before element HP creation. Store records use integer IDs, start new IDs at
  zero XP and reset XP when a reused ID has a different generated name. None proves
  that native Reinforcements supplies fresh IDs or that every modded heal bypasses SV.
- Required A/B: SV-only vs no SV; living injured First Aid, dead-element First Aid and
  native Reinforcements separately. Record exact skill/tooltip, target, alive count,
  HP/morale/AP/charges/manpower; compare replacement IDs/XP, survivor XP, squad-average
  HP bonus, mission exit and reload. Use only disposable campaigns/backups.
- The other reported leader/"revive" ability is still unidentified. WorkshopUnlock API
  errors and ItemContainer/NextRound mission-spawn stacks are not an identified
  First Aid/RefillSquaddies call stack. No resurrection or persistence fix; #20 open.

## #21 — Original third-party repair package audited read-only

- Found the original Field Repair Kit 1.0.0 archive locally, read without extraction,
  modification or installation. Source hash is recorded in the minimal test fixture.
- Confirmed all four `active.frk_*` skills clone cached non-attack `cut_fence`, clear
  native EventHandlers, and use two `Skill.Use` postfix classes plus target resolution.
  The hull path has a 75% cap and temporarily zeroes armor before negative Combat.Damage.
  Source presence is not proof of live Harmony registration or successful healing.
- Confirmed logger file append precedes `Log.Info` inside an empty catch: an unavailable
  hard-coded developer directory can silence both. Do not interpret no log as no hook.
  Third-party code was not silently patched or redistributed.
- Added own-source guard for SV's non-positive-damage pressure exclusion and cached
  clone-name/non-attack contracts. These do not prove compatibility or invoke repairs.
- Repro matrix: same vehicle with **HP AND armor below 75%**, FRK-only then FRK+SV;
  infantry-carried and vehicle-carried kits separately. Record AP/charges, HP/armor and
  exact target. Reload between isolated SV reliability/HP-bonus/crew-pressure toggles.
- Request/enable portable opt-in third-party diagnostics for patch registration,
  `__result`, target resolution, negative-damage return and HP/armor before/after.
  Logging must survive a file-write failure; temporary armor must restore on exceptions.
- No repair implementation change, attributed cause or runtime verification. #21 open.

## Regression-batch checkpoint (before the subsequent AI follow-up)

- Suite: **85 Python tests / 4,545 C# checks passed** (existing nullable warnings remain).
- LargerMaps: **4 water-coordinate/source + 6 existing stamp-density tests passed**.
- `git diff --check` passed. All **276 distributable payload files** still byte-match
  `.tmp/MENACE-Suite-migration-followup.zip`; final ZIP inventory passed again.
- These regression-batch additions change tests, fixtures and review documentation only. The existing
  candidate and its production source/binary payload remain unchanged. No Windows,
  PowerShell transaction, real healing, ammo, water-rendering or save test performed.
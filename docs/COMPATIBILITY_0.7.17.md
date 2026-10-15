# MENACE 0.7.17 — candidate compatibility evidence (#45)

**Not gameplay-verified. Not a release.** The earlier 0.7.16 acceptance does not
carry over. No README/CHANGELOG verified-on line has been advanced.

## Actual inputs

- User launch: 15 October 2026; log last write 16:34:17 UTC.
- Exact version line: `[18:33:33.698] Game Version: v0.7.17+22362`.
- Main menu: `[18:33:56.037] [Menace_Data_Extractor] Main menu loaded`.
- Steam buildid observed: 25958176; **not yet an accepted/verified build marker**.
- GameAssembly SHA256: `871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111`.
- Assembly-CSharp proxy SHA256: `fe8eea7fc2775db28fe48455685e3434057bdf84028c3ecc300351cc8c0646d9`.
- The 121 proxies were already regenerated at 16:19:44 UTC; the last launch reused
  them. Generator last/current SHA512 matches the independently hashed new binary.
- Ignored cache: `game_files/v0.7.17/`; `reference-manifest.json` records native,
  metadata, all proxies and runtime DLL hashes. Manifest SHA256:
  `a0e8d58483079c84ddd90b02ab97fd8c82aa6931eb7cd0ec9ed061319ae1e77c`.
- Local builds explicitly use this versioned cache, not the old `/tmp/jy/game` refs.
  The older cache and original dump were not overwritten.

## Restricted contract findings

Compared complete dumps from both original binary/metadata pairs, each containing
15,940 types. Inventory covers 83 Suite/shared/camera C# files, 8 KDL files and 187
source-named Menace types, including `MenaceSdk.cs` TacticalEventHooks. Checked all
existing declarations/field offsets as a superset to cover dynamic/string access.

No existing Harmony method signature, shim field/property type or KDL set-path
contract changed or disappeared. No field-layout changes. Only declaration deltas:

- Added private static `StrategyState.ShowLeaderPerResetDialog(BaseUnitLeader, int)`.
- `SaveState.SAVE_STATE_VERSION`: 125 → 126.
- Added `SaveState.LIM_PERK_RESET_VERSION = 126`.

Native pathfinding methods moved +0x3A0; `MissionTemplate.GetBounds` stayed at
0x60F630. Exact new RVAs are recorded in `pathfinding-sites.json`. All seven methods
were matched by declaration in both dumps; **26/26 original instruction patterns**
were checked against actual 0.7.17 PE bytes. Only the build map/fingerprint changed;
patch descriptors, algorithm and old build maps are untouched. Unknown builds
still fall back to 64x64. This is not evidence of successful live native patching.

## Checks completed locally

- 17 code packages and camera runtime rebuilt against the new proxy cache.
- Both data packages rebuilt with Jiangyu CLI; unchanged KDL contracts above.
- Actual DLL assembly/file versions, manifests and dependencies checked for all
  19 packages and camera; MenaceModSettings assembly identity remains 1.0.0.0.
- 96 GameplaySuite Python tests passed.
- 116 LargerMaps Python tests passed, 4 optional checks skipped, with real 0.7.17
  GameAssembly/dump inputs. Native simulation changes exactly 26 bytes.
- 15,681 C# offline checks passed. These do not execute Harmony/IL2CPP gameplay.

## Initial installed-mod findings and remaining gates

The initial launch used the **old installed builds**, not this candidate. Two ERROR
lines describe one external Custom Leader Harmony failure on `BlackMarket.FillUp`.
LargerMaps correctly fell back to 64x64. StartingOptions' property-first lookup also
probes an unused field fallback and emits a warning; no new native field removal
was found. External mods and CustomSupplySliders are not modified by this work.

Windows canonical build/install/rollback acceptance and step-5 gameplay remain
pending. Minimum: clean Suite title startup, load save, end turn/AI turn 1 (#44), WRR
debug range pass with zero write failures then debug OFF, OW/CF/Smoke activations,
and Field Logistics resupply. Only afterward record a verified buildid and implement
the mismatch warning/explicit-confirmation installer gate (not a blanket hard block).
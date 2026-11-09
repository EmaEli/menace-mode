## 0.9.0

- Answer optional AIPerformance 1.8.0 observed-threat smoke requests for ordinary
  advance-ordered rifle infantry, not just flamethrower carriers. Prefer adjacent forward
  smoke and use the existing main-thread executor/one-attempt-per-turn guard.
- Equipped grenade, remaining uses, AP, native target legality and successful Use are
  required. No grants or defensive-direction changes; emergency smoke retains its odds.
- Expose real smoke presence for guarded forward movement; disabling its aim penalty
  cannot authorize smoke-protected advance. Smoke is not invulnerability.
- Add linked-runtime request/throw/legality/failure tests. Canonical standalone mode can
  preserve installed settings/loader; gameplay acceptance remains separate.

## 0.8.4

- Carries forward 0.8.3's all-stat neutralization for a scoped MENACE 0.7.18 test candidate; no whole-suite compatibility claim.
- Fixes native casts for mortar smoke spawn templates and AI-only cooldown discovery; non-smoke tile-effect templates are excluded.
- Adds bounded, debug-only observations of smoke assignment, early removal, tile membership and visual-object presence/activity after the native spawn delay. Does not create, remove or replace clouds. Debug remains OFF by default.
- Fresh pre-install 0.8.2 log confirms smoke assignment and duration 2 -> 4 rounds, not successful rendering. Cloud disappearance remains under investigation; this is not a confirmed visual fix.
- Validated 2026-11-05: 185 Python tests, 19 executed lifecycle assertions, 17,913 candidate offline checks, clean-source build, 49 current native contracts and 24 Windows compatibility checks. Canonical Windows BuildOnly/install passed; installed manifest/assembly/file versions are 0.8.4/0.8.4.0/0.8.4.0. Smoke build: zero warnings; settings companion: existing JIA005 warning.
- Backup outside Mods: `20261105-105607-e7e25cf7ec6d4b8986680d36c55ba00a`. All 1,394 protected unrelated Mods/UserData files unchanged; MenaceModSettings remains 0.8.2.
- Gameplay follow-up: user reports “seems to have worked.” Fresh 0.7.18 log confirms six native `effect.smoke` modifiers neutralized and 12 observed smoke handlers remained attached with active world objects at the +8-second sample. This diagnostic proves object activity, not pixel visibility; user feedback is the acceptance. No failure reported.

## 0.8.3

- Disables every native `effect.smoke` stat change, including concealment, accuracy, defense, damage, suppression and vision; retains the custom 20-point/10-point shot-chance rule.
- Resolves concrete `ChangeProperty` templates through native casts instead of managed wrapper names. Handles both legacy scalar members and MENACE 0.7.18's `Properties` arrays; snapshots originals for transactional rollback and disable/unload restoration.
- Does not remove smoke tile effects, event handlers or visual prefabs, or change spawning/lifetime. The reported disappearing cloud is still unconfirmed and is not claimed fixed.
- Windows deployment/gameplay pending; the existing 0.7.18 support gate is not bypassed.
- Local builds against both 0.7.17 and 0.7.18 proxies pass with zero warnings; manifest/assembly/file version is 0.8.3. Shared-worktree validation: 183 Python tests and 17,883 offline C# checks passed. This is not Windows/gameplay verification.

## 0.8.2

- Fixes native smoke AccuracyMult discovery: MENACE exposes the handler as `ChangePropertyHandler`, which 0.8.1's narrower type-name filter missed. The fresh 0.8.1 log confirmed the native -70% Accuracy modifier was not neutralized.
- Other native smoke status properties remain unchanged; this fix only replaces the native accuracy multiplier with the configured weighted smoke penalty.

## 0.8.1

- Neutralizes MENACE's native `effect.smoke` AccuracyMult (0.3 / -70%) while Smoke Overhaul is enabled, leaving the configured weighted penalty as the only smoke accuracy modifier.
- The custom penalty remains 20 percentage points per intervening smoke tile and 10 per smoke-occupied shooter/target tile by default; other native smoke status properties are unchanged.
- Restores the original status handler when gameplay is disabled or the module unloads.

## 0.8.0

- Smoke fully blocks LOS by default when it lies between units; the existing soft-LOS setting remains available as an opt-in.
- Smoke on the shooter or target tile contributes half a tile to the configurable hitchance penalty; intervening smoke tiles still count as full tiles and the configured cap is unchanged.
- 181 Python tests and 17,894 offline C# checks passed. Canonical MENACE 0.7.17 BuildOnly/install passed 2026-11-04; in-game acceptance remains pending. Saved settings were left untouched.

## 0.7.0

- Free smoke and Tier 2 frag grenade grants for infantry are now opt-in and OFF by default; both settings remain independently configurable.
- Existing AI smoke-use probabilities and decision rules are unchanged.

## 0.5.3

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## Smoke Overhaul 0.5.2 — Fast AI smoke selection fix

- Optimizes MENACE's existing `CreateLOSBlocker` AI using its native `InfluencesPositioning` guard: AI evaluates smoke from the current position instead of expanding future-position searches for smoke deployment.
- Handles smoke grenades **and** all three mortar smoke skills, including shared AI profile instances; no duplicated native behavior and no new hot-method Harmony patches.
- Keeps player targeting, smoke spawning patterns, two-round mortar AI cooldown, grenades, accuracy rules, and explosive ammunition unchanged.
- Restores previous native AI flags on setting change and unload. One startup confirmation line, no per-turn logging. `FastAISmokeSelection` is ON by default and can be turned OFF to restore the former AI search.
- Fix is source-level and needs in-game compilation/testing before performance or use frequency can be guaranteed.

## 0.5.1

- Fixed a runtime interaction with other mortar systems by removing the unnecessary periodic reapplication of smoke-delivery template/AI mutations every 0.5 seconds during tactical play.
- Smoke delivery profiles are now applied only at initialization, scene load, or when their settings actually change.
- No smoke footprint, duration, LOS, AI behavior, AP cost, grenade grant, or normal logging balance changed from 0.5.

## 0.5

- Added caliber-based smoke footprints: infantry smoke and light-mortar smoke are 1 tile; the 80 mm medium mortar uses a five-tile cross; heavy/120 mm mortar smoke keeps the vanilla 3x3 footprint.
- Added the existing vanilla mortar-smoke skill to the 80 mm mortar, which currently has no smoke skill in MENACE data.
- Footprint filtering is applied to `Skill.GetAoeTiles` / `Skill.GetUnsortedAoeTiles` for preview/AI evaluation and enforced again at `SpawnTileEffectHandler.OnApply` for the actual tile effect.
- Retained light/80 mm smoke tiles are deterministic; heavy/120 mm smoke behavior remains vanilla.
- Enabled MENACE's native `CreateLOSBlocker` AI behavior for mortar smoke skills that previously had no AI profile. Infantry smoke keeps its existing native AI behavior. Mortar smoke also reuses the infantry smoke skill's native two-round AI-only cooldown to prevent consecutive smoke spam.
- Added debug-only `AI SMOKE USE` diagnostics. Normal logging remains silent.
- Existing soft-LOS, accuracy, duration, grenade-AP and free smoke/frag behavior remains unchanged.

## 0.4

- Reduced all hand-thrown `active.throw_*` grenade/bomb skills from 40 AP to 20 AP by default. Grenade launchers, mortars, off-map bombs and deployable charges are intentionally excluded.
- Timed smoke tile effects now last 2x their vanilla round duration by default; the multiplier is configurable from 1x to 4x.
- The longer duration applies to smoke from all sources using a `TileEffectType.Smoke` timed tile effect, while preserving vanilla VFX.
- Kept the safer template-level lifetime approach. Staggered one-tile-at-a-time dissipation is not forced through MENACE's private `TileEffectHandler.m_Lifetime` / `StartRound` bookkeeping.
- Existing soft-LOS, fire-through accuracy penalties, free three-use smoke/frag grants and duplicate-skill guard remain unchanged.

## 0.3.1

- Fix duplicate smoke/frag skill-bar buttons when the same grenade skill is added through both inherent and item/runtime skill passes.
- Prefer the vanilla three-use smoke skill over the limited variant when both are presented to the same SkillContainer.
- No smoke LOS, accuracy, grenade count, AI or VFX balance changes.

# Changelog

## 0.3

- Added an enabled-by-default Tier 2 fragmentation-grenade grant using vanilla `active.throw_frag_grenade_tier2`.
- Keeps the native 3 uses, 40 AP cost, range 1-3, cover-ignoring behavior and AI configuration.
- Uses the same normal humanoid infantry scope as the working smoke-grenade grant.
- Added a separate Mod Settings toggle.
- Avoids a duplicate frag button when the template or its default equipment already grants Tier 2 frag.
- Existing smoke mechanics and smoke-grenade grant are unchanged.

## 0.2.3

- Fixed free smoke grenades not appearing on infantry at runtime. The grant is now re-validated against the live `EntityTemplate.Skills` list and injected immediately before MENACE builds inherent skills, so later template-list replacement cannot silently erase it.
- Scope is now normal humanoid combat infantry (`infantry_default`) rather than every `ActorType.Infantry` actor.

# Changelog

## 0.2.2

- Build-only compatibility fix; gameplay is unchanged from 0.2.
- Qualified `System.Math` calls to avoid the MENACE tools namespace collision.
- Switched infantry template discovery to `Resources.FindObjectsOfTypeAll<EntityTemplate>()` because the generated IL2CPP `IReadOnlyCollection<T>` wrapper is not directly enumerable by C#.

## 0.2.1

- Build-only nullable metadata compatibility shim for MENACE/Unity IL2CPP assemblies.

## 0.2

- Added an enabled-by-default option that grants every infantry template one vanilla one-use smoke grenade (`active.throw_smoke_grenade_limited`).
- Uses `EntityTemplate.IsInfantry()` so vehicles and structures are not affected.
- Avoids adding a second direct smoke-grenade skill when the template already has the normal or limited vanilla smoke-grenade skill.
- The grant is shared by player and AI infantry and uses the vanilla AP cost, range, targeting and AI behavior.
- Debug remains OFF by default and the grant produces no normal-gameplay log spam.

## 0.1

- Initial release.
- Smoke tile effects no longer fully block line of sight by default.
- Added configurable progressive hit-chance penalty for smoke tiles between shooter and target: 20 percentage points per tile, capped at 60 by default.
- Attacker and defender tiles are excluded from the added line penalty so vanilla `effect.smoke` modifiers remain authoritative for units standing inside smoke.
- Uses the shared hit-chance path so AI and player calculations are affected consistently.
- Leaves smoke VFX untouched.
- Debug logging is optional and OFF by default; no per-hit logging is emitted.

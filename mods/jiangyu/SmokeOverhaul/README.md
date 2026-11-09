# Smoke Overhaul 0.9.0 — coordinated smoke test candidate

Smoke Overhaul makes smoke fully obstruct line of sight when it lies between units, while a unit standing in smoke receives half-tile aim exposure. It also extends smoke duration and gives smoke-delivery systems distinct tactical footprints.

**0.9.0:** optional AIPerformance 1.8.0 support requests let ordinary advance-ordered rifle
infantry use an equipped grenade toward an observed threat, even without a flamethrower.
The existing main-thread smoke executor validates ammo, AP and target legality; it attempts
at most once per actor turn. No equipment is granted. Successful smoke can allow a guarded
forward step; this is concealment, not invulnerability. Native shots and emergency actions
are not replaced by the request. Without AIPerformance, existing behavior is unchanged.

**0.8.4:** carries forward the fix disabling all native `effect.smoke` stat changes, not only Accuracy: no concealment, accuracy, defense, damage, suppression or vision buffs/debuffs. Uses native casts and supports the legacy scalar and 0.7.18 property-array layouts. Smoke tile effects and VFX are preserved. Only the configured shot-chance rule remains: -20 percentage points per intervening smoke tile and -10 per smoke-occupied shooter/target tile by default. Mortar spawn/cooldown discovery now also uses native casts. This is a scoped 0.7.18 test candidate, not whole-suite or gameplay acceptance.

The disappearing-cloud report is **not yet confirmed fixed**. With Debug logging enabled,
the first 12 smoke assignments per scene report their native lifetime and prefab presence,
then tile membership and visual-object presence/activity 2 and 8 seconds after the native
spawn delay. Early removal is reported within that observation window. These checks are
read-only, bounded and cleared on scene changes/unload; they do not repair or replace VFX.

**Windows test install:** canonical BuildOnly/install passed on MENACE 0.7.18+22828
on 5 November 2026. Installed manifest/assembly/file versions verified as 0.8.4 / 0.8.4.0 /
0.8.4.0; all 1,394 protected unrelated Mods/UserData files unchanged. Backup retained
outside Mods. The user subsequently reports the smoke test “seems to have worked.” The
fresh log records six neutralized native smoke stat modifiers and active visual objects
eight seconds after assignment. This is diagnostic corroboration, not direct pixel proof.

**0.7.0:** free infantry smoke and Tier 2 frag grants are now OFF by default; their settings
remain independently configurable. Existing AI smoke-use probabilities are unchanged. Not
verified in game: native execution, ammo/AP/cooldown, AI coexistence and 64/96-map timing remain
acceptance gates. The earlier migration ZIP remains an unchanged historical candidate.

**0.8.0 (#85):** intervening smoke fully blocks LOS by default; the existing setting can opt
back into soft LOS. Smoke on the shooter's or target's tile contributes 0.5 of one configured
aim-penalty tile, while each intervening smoke tile contributes one. Existing saved LOS choices
are preserved; OFF is the default when no saved preference exists. Canonical MENACE 0.7.17
BuildOnly/install passed 2026-11-04; in-game acceptance is pending.

**0.5.2 AI performance fix:** MENACE's native `CreateLOSBlocker.Collect` was spending excessive time evaluating hypothetical positions for smoke on 80x80 maps. By default, `FastAISmokeSelection` changes only the existing AI profile flag `InfluencesPositioning` to false for smoke grenades and mortar smoke shells. The AI can still choose valid smoke targets from its current position; it no longer looks for better positions *specifically to use smoke*. Full native smoke decision making, smoke footprints, AI smoke use, and the two-round mortar AI cooldown remain in place. A single log line verifies activation, with no per-turn logging. Turn this setting OFF to restore vanilla speculative smoke positioning.

**0.5.1 runtime fix:** smoke-delivery template/AI configuration is no longer re-applied every 0.5 seconds during tactical play; it is applied only when lifecycle/settings changes require it. Gameplay values are unchanged from 0.5.

## Default behavior

- Smoke tile effects fully block line of sight when smoke is between units by default. Enable **Allow line of sight through smoke** to restore soft LOS.
- The configurable aim penalty counts each intervening smoke tile as **1.0 tile** and smoke on the shooter or target tile as **0.5 tile each**. Defaults are **-20 percentage points per full tile**, capped at **-60 percentage points**.
- All native `effect.smoke` stat changes are disabled while Smoke Overhaul is enabled; entering smoke must not change unit-panel stats. Weighted exposure affects calculated shot chance only. Disabling the mod restores the original stat changes.
- Timed smoke lasts **2x vanilla duration** by default (configurable 1x-4x).
- All hand-thrown `active.throw_*` grenade/bomb skills cost **20 AP** by default. Grenade launchers, mortars, off-map bombs and deployable charges keep their own AP economy.
- Free infantry smoke and Tier 2 frag grenade grants are **off by default**; enable either grant in Mod Settings to give normal humanoid infantry the vanilla three-use grenade. Existing equivalent skills/equipment are detected to avoid duplicate buttons.

## Caliber-based smoke footprints

Enabled by default:

- **Infantry smoke grenade:** center tile only (1 tile).
- **Light mortar smoke:** center tile only (1 tile).
- **80 mm medium mortar smoke:** five-tile cross centered on the target (`+` shape). Smoke Overhaul grants the vanilla mortar-smoke skill to the 80 mm mortar because current MENACE data does not grant it by default.
- **120 mm/heavy mortar smoke:** unchanged vanilla 3x3 footprint.

For the shared light/80 mm mortar smoke skill, retained footprint tiles are deterministic. The footprint is also filtered through `Skill.GetAoeTiles`, so targeting/AI evaluation sees the same shape as the actual spawned smoke. A second guard on `SpawnTileEffectHandler.OnApply` prevents smoke from leaking outside the configured footprint.

## AI smoke use

Enabled by default. Infantry smoke keeps MENACE's native **CreateLOSBlocker** AI behavior. Mortar smoke skills that currently have no AI profile inherit that same native behavior, so the AI can deliberately place smoke to interrupt useful lines of sight instead of treating smoke as a player-only utility. They also inherit the infantry smoke skill's native **2-round AI-only cooldown**, preventing repeated smoke spam while leaving player use unchanged.

With `FastAISmokeSelection` ON, native AI smoke selection no longer uses future movement locations solely to position smoke. It can still fire smoke using its currently available valid targets. The native AI evaluates the skill's current AoE tiles, so the 1-tile, cross and 3x3 footprints participate in its target selection. Smoke Overhaul does not add per-frame custom AI scanning. Debug logging can report actual AI smoke uses, but is OFF by default.

`SimpleAISmokeRules` additionally makes one proactive hand-thrown smoke decision per
eligible actor/turn: **60% Pinned Down, 45% Suppressed, 30% exposed flamethrower**. The
roll happens only after native skill affordability/usability and target checks. These
are proactive probabilities, not total smoke-use percentages: native AI may still act.
Only known, currently detected opponents supply coordinates (at most 24 checked);
at most six nearby tiles are considered, with no custom pathfinding or global scan.
Earlier Infantry Assault overrides take precedence; failed decisions return to native
AI. The guard is cleared at the actor's turn start and on scene changes.

`AllowSmokeWhilePinnedDown` permits only the two infantry smoke-grenade skills while
Pinned Down; their queried AP cost becomes zero **only in that state**, including
player use. Other grenades and normal/Suppressed AP costs are unchanged. Native ammo,
cooldown and target checks remain in force. Disabling restores owned permissions.

## Mod Settings

Open MENACE Mod Settings and select **Smoke Overhaul**. You can configure:

- Enable Smoke Overhaul.
- Whether line of sight is allowed through intervening smoke (OFF by default).
- Accuracy penalty per full smoke tile, including half-weight shooter/target tiles, and maximum accumulated penalty.
- Smoke duration multiplier.
- AP cost for hand-thrown grenades.
- Free infantry smoke/frag grants.
- Caliber-based smoke footprints.
- Native AI smoke use.
- Fast AI smoke selection (ON by default; disable for original speculative smoke positioning).
- Proactive AI smoke rules (ON; native AI remains the fallback).
- Smoke permission / zero AP while Pinned Down only (ON, independent toggle).
- Debug logging.

Debug logging is OFF by default and hot paths do not write routine logs.

### Runtime setting note

The line-of-sight setting is safest when changed outside a tactical battle. If changed while smoke already exists, the original LOS template state is restored on the next non-tactical scene so MENACE's LOS bookkeeping cannot be unbalanced by an already-active effect.

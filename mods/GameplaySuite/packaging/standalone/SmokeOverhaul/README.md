# Smoke Overhaul 0.6.1

Smoke Overhaul changes MENACE smoke from a hard line-of-sight wall into a longer-lasting soft obstruction and gives smoke-delivery systems distinct tactical footprints.

**0.6.1 native smoke AI action (unverified):** The first in-game test observed ZERO enemy smoke grenades with 0.6. Native scoring alone was insufficient. The new version hooks the same Agent.Execute transition as Infantry Assault and makes one bounded, native-validated smoke attempt for eligible suppressed/pinned squads and exposed flamethrowers. At most 6 adjacent tiles, or 2 forward tiles for flamethrowers, are inspected. Native skill affordability, range, cooldown and ammo are honored; no pathfinding or global map search. Debug logging is OFF; an aggregate diagnostic is available in Dev Console. The fix still needs a Windows build and in-game acceptance test.

**0.6 tactical AI smoke:** Allows pinned infantry to use smoke at 0 AP only while Pinned Down; Suppressed throws retain normal cost. Native AI scores immediate adjacent smoke higher under pinning/suppression, and exposed flamethrower infantry may prefer smoke ahead if an enemy is known. No forced movement, new path search or default logging. Settings: `AllowSmokeWhilePinnedDown` and `SimpleAISmokeRules` (both ON). These preferences need game testing.\n\n**0.5.2 AI performance fix:** MENACE's native `CreateLOSBlocker.Collect` was spending excessive time evaluating hypothetical positions for smoke on 80x80 maps. By default, `FastAISmokeSelection` changes only the existing AI profile flag `InfluencesPositioning` to false for smoke grenades and mortar smoke shells. The AI can still choose valid smoke targets from its current position; it no longer looks for better positions *specifically to use smoke*. Full native smoke decision making, smoke footprints, AI smoke use, and the two-round mortar AI cooldown remain in place. A single log line verifies activation, with no per-turn logging. Turn this setting OFF to restore vanilla speculative smoke positioning.

**0.5.1 runtime fix:** smoke-delivery template/AI configuration is no longer re-applied every 0.5 seconds during tactical play; it is applied only when lifecycle/settings changes require it. Gameplay values are unchanged from 0.5.

## Default behavior

- Smoke tile effects no longer fully block line of sight.
- Shooting through smoke applies **-20 percentage points of hit chance per intervening smoke tile**, capped at **-60 percentage points**.
- Shooter and target tiles are excluded from this extra line penalty; vanilla `effect.smoke` modifiers on units standing inside smoke remain authoritative.
- Timed smoke lasts **2x vanilla duration** by default (configurable 1x-4x).
- All hand-thrown `active.throw_*` grenade/bomb skills cost **20 AP** by default. Grenade launchers, mortars, off-map bombs and deployable charges keep their own AP economy.
- Normal humanoid infantry receives the vanilla three-use smoke grenade and Tier 2 frag grenade by default. Existing equivalent skills/equipment are detected to avoid duplicate buttons.

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

## Mod Settings

Open MENACE Mod Settings and select **Smoke Overhaul**. You can configure:

- Enable Smoke Overhaul.
- Whether smoke fully blocks line of sight.
- Accuracy penalty per intervening smoke tile and maximum accumulated penalty.
- Smoke duration multiplier.
- AP cost for hand-thrown grenades.
- Free infantry smoke/frag grants.
- Caliber-based smoke footprints.
- Native AI smoke use.
- Fast AI smoke selection (ON by default; disable for original speculative smoke positioning).
- Debug logging.

Debug logging is OFF by default and hot paths do not write routine logs.

### Runtime setting note

The line-of-sight setting is safest when changed outside a tactical battle. If disabled while smoke already exists, the original LOS template state is restored on the next non-tactical scene so MENACE's LOS bookkeeping cannot be unbalanced by an already-active effect.

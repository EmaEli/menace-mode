## Smoke Overhaul 0.6.4 — #1 offline follow-up, acceptance pending

Ports PR #12 commit `0426b4484581e355cbd5b831f85fb40c483f114c` to the current
Jiangyu host. Compilation and pure geometry tests do **not** verify native AI use.
Keep the existing migration ZIP as its own historical candidate; do not deploy it
expecting these new smoke rules. Use only one Smoke Overhaul copy.

1. Verify both new toggles (`SimpleAISmokeRules`, `AllowSmokeWhilePinnedDown`) default
   ON and Debug OFF. Keep fast selection ON; no speculative positioning is needed.
2. With known equipped infantry and a detected threat, exercise Pinned Down (60%),
   Suppressed (45%) and exposed flamethrower (30%) opportunities separately. Roll
   once per eligible actor/turn **after** native skill/target legality. These are
   proactive odds, not overall-use odds: native AI can still choose smoke afterward.
3. Compare valid threats in every direction, edges, occupied/smoked/blocked target
   tiles and hidden/recently lost enemies. No throws from facing alone or use of
   hidden coordinates; at most six local candidates and 24 opponent checks.
4. Confirm native ammo/cooldown, no reroll after a rejected/failed proactive use,
   turn-start reset, repeated scene changes, and fallback when no legal target exists.
5. Pinned player and AI infantry may use **only the two hand-thrown smoke skills** at
   zero queried AP cost. Suppressed/normal smoke and every other grenade retain their
   configured costs. Independently toggle permission OFF/ON and verify restoration;
   disabling proactive rules must leave native smoke behavior available.
6. Run with Infantry Assault enabled: an earlier override must win; smoke must not
   start another action. Check main-thread execution/reset and off-thread fallback.
7. Separate debug-only diagnostic runs from performance runs. Proactive coordinate
   logs cap at 24 per mission; mission counters distinguish eligible/skill/target/
   chance-rejected/successful use. The older `AI SMOKE USE` prefix logs an attempt,
   not proof of a successful throw. Debug OFF must remain quiet.
8. Compare the same save/seed/settings on 64x64 and 96x96, proactive rules OFF vs ON,
   with all deep AI profilers OFF. Record actual throws, repeated opportunities,
   native actions/AP/ammo and ~3 AI-turn times; do not infer improvement from builds.

## Larger Maps 2.1.0 — #2 selective positioning, acceptance pending

- Keep `PreserveBasicInfantryFirePositioning` OFF normally. It preserves at most five
  already-enabled native profiles, only above 64x64 with simplified planning ON.
  Other attacks/buffs retain baseline simplification; no waking or new orders.
- Same 2.1.0 binary, save/seed and mission: OFF/ON runs with restarts, local routing OFF,
  all deep AI diagnostics OFF and Smoke/Assault settings identical. Prefer proactive
  smoke OFF to isolate #2 after completing #1's separate acceptance.
- Use 64x64 as an inert control, then 96x96. Separate offensive vs sleeping/defensive
  missions. Record ~3 AI-turn times, actual shots/repositions, cover/objective retention
  and oscillation. Historical 1.9 observations on a different mission are not an A/B.
- A separate setup-log run must show 0 preserved profiles when OFF, at most 5 when ON
  above 64, and no activation at 64. Sharing may mean fewer than five distinct profiles.
- Check lost/missing/disabled profiles, next mission and unload. In-memory doubles test
  production setup/restore paths, not IL2CPP validity, native shots or CPU performance.
  Leave OFF if timing/tactical behavior regresses. No new per-issue installer.

## Migration checkpoint — #38 / #31 follow-up

Field Logistics 1.0.11 and Infantry Assault 0.5.8 are follow-ups to the
historical batches below. MenaceModSettings is now 0.5.0 (ABI remains 1.0.0.0).
The canonical installer exists as an **unexecuted PowerShell candidate**; do not read
older notes saying "packaging pending" as an instruction to create another installer.

### Infantry Assault 0.5.8 (#31)

Verify the Assault button uses `skill_41` / `skill_41_inactive` in active and disabled
states, including after loading a save and another tactical scene. Then exercise the
existing hover-after-casualties, 40 AP, defensive-fire, garrison and AI regressions below.

### Field Logistics 1.0.11 (#38)

- Verify both Resupply Infantry and Redistribute Ammo show `skill_05`, and their
  disabled states use `skill_05_inactive`; no exchange-arrow or whistle icon remains.
- Default sharing OFF: no redistribution or pending transfer; vehicle resupply remains
  one use, with four extra uses from the module. Check 60 AP resupply / 40 AP sharing.
- Turn sharing ON: adjacent compatible squad weapons conserve total ammunition;
  grenades/special weapons and incompatible categories do not exchange ammunition.
- Exercise inactive/unreadable `-1` ammo, delayed credits, alternate fire modes, skill
  refresh and repeated use. Turn sharing OFF while pending; no delayed transfer later.
- Repeat with vehicle module absent/present, default vehicle resupply ON/OFF, loaded save,
  actor spawn, mission exit and next mission. Debug/test-loadout options remain OFF.

### StrongerCover / Garrisonable Buildings / WRR (#38)

- With StrongerCover absent/disabled, buildings use Garrisonable Buildings' own 40%
  default (or saved custom value); enabled cover uses its BuildingCover 30% default.
- Change both settings live, in either module/load order; disable/re-enable StrongerCover
  and verify the current garrison base, not a stale/default value, is restored.
- WRR: compare 2.9.2 ranges and linked skill ranges with gameplay ON/OFF and each type
  toggle; only WRR-owned values should restore. Check indirect-fire AI positioning too.
- Put different synthetic override values in the modern UserData file and old Mods file
  in a disposable test install. Modern wins; when absent, legacy is copied once. Test
  hot reload and malformed JSON without replacing real user files.

Offline coverage executes actual CoverSettings and ModSettings code with an in-memory
store; checks template-lookup/restore/override wiring; inspects actual binary references,
dependency minimums and DLL versions. It does not prove IL2CPP template writes, ammo
conservation, on-disk persistence, live unload or Windows installation/rollback.

# Offline batch 1 — final candidate, not deployed

| Module | Manifest / file version | Assembly identity |
| --- | --- | --- |
| MenaceModSettings | 0.4.1 / 0.4.1.0 | 1.0.0.0 (stable) |
| Coordinated Fire | 1.6.10 / 1.6.10.0 | 1.6.10.0 |
| Overwatch | 0.32.3.5 / 0.32.3.5 | 0.32.3.5 |
| Squaddie Veterancy | 1.10.2 / 1.10.2.0 | 1.10.2.0 |

Local Jiangyu release builds only; no Windows build, deployment, startup or gameplay test.
Overwatch .3/.4 below are intermediate implementation steps included in final .5, not
separate packages to install. Use one coordinated distribution with the settings companion;
the canonical installer/packaging work is still pending under #34. Test commands and the
metadata-only artifact verifier are in [tests/README.md](../tests/README.md).

# #23 — Native vehicle firing sector (in-game verification pending)

Overwatch 0.32.3.5 combines #43/#24 with an explicit native `IsInRange(tile, source, true)`
check for vehicle reactions, both before queueing and before firing. Binary evidence and
the limitations of this API are recorded in [OVERWATCH_NATIVE_CHECKS.md](OVERWATCH_NATIVE_CHECKS.md).

Use the captured heavy tank/short-barrel gun, a hull-fixed weapon and a rotating turret;
compare normal shots with Overwatch at front/side/rear and just inside/outside native limits.
Repeat for hostile AI, movement and attack provocations, and Both/Alpha Strike modes. If
facing changes while a reaction is queued, revalidation must reject an illegal shot without
extra ammo/AP/reaction charges or a stuck pending watch. No free hull turn should be added.
Native circle-range guns and visual-only turret stops require explicit observation; no
additional guessed angle restrictions were introduced. Debug reasons already use the
existing opt-in logging path; leave Debug OFF normally. Keep #23 open until live validation.

# #24 — K-PAC tag classification, partial fix (in-game verification pending)

Overwatch 0.32.3.4 carries PR #28's skill-scoped indirect-fire fix together with #43's HUD
guard. The available asset export confirms K-PAC grants direct normal/sustained rifle modes
and an indirect underbarrel frag launcher; fixture/model and source-wiring tests cover that
distinction. Direct weapon indirect tags still exclude attacks; metadata exceptions fail closed.

Test discovering, reserving and actually firing each mode for player and AI. The two rifle
modes should be eligible; the launcher and genuine indirect-fire/indirect ATGM attacks must
remain excluded. Verify direct ATGM and native built-in vehicle attacks, range/LOS, ammo/AP
and Squad/Leader/Both modes. Distinguish using a weapon for a reaction from provoking enemy
Overwatch by firing it. ARC Range and PPP CQB remain undiagnosed: no speculative SourceSkill
or role fallback was added. Keep #24 open, and PR #28 draft, until live validation.

# #35 — Leader display names (in-game verification pending)

Squaddie Veterancy 1.10.2 changes display resolution only. Compare vanilla leaders and SFP
dynamic leaders in the deployment/roster overlay, then after deployment and another scene
change. The native localized nickname should win, followed by speaker full name/title.
GUIDs and dynamic template IDs must never appear as names; missing data shows `Unknown Squad
Leader`. Verify punctuation/casing/accents, squad numbers, member IDs, existing XP and save
contents remain unchanged. No SFP promotion/save integration is included in this fix.

The offline C# runner tests the actual resolver with managed stand-ins for localized fields,
missing/throwing members, dynamic IDs, fallback sources and an untouched identity getter.
This does not exercise real IL2CPP localized proxies; keep #35 open until tested in game.

# #43 — Shared tactical HUD input guard (in-game verification pending)

Offline build: MenaceModSettings 0.4.1, Coordinated Fire 1.6.10, Overwatch 0.32.3.3.
The companion retains assembly identity 1.0.0.0; both HUD mods require companion >=0.4.1.

1. In a tactical player turn, select an infantry squad. Click/double-click each panel's main
   button and weapon-mode chips, including disabled controls and panel background. No move,
   native attack, map selection or camera drag should result from these HUD clicks.
2. Press in either panel, drag onto the map and release. The release must not act on the map;
   the next independent click outside must work. Repeat left/right/middle buttons.
3. Repeat with both mods together, each alone, compact/normal CF HUD, all anchors, scale/offset
   changes and different resolutions. Input geometry must match visible panels immediately.
4. Hide HUDs, disable gameplay, pause, enter an enemy turn, change scenes and unload modules.
   Hidden panels must not leave blocked rectangles; release from an already captured press is
   still swallowed. Normal keyboard/camera keys and CF Escape cancellation must remain native.
5. Verify each HUD mod with no valid selection/plan, and CF during its locked volley. The shared
   guard has no runtime-bound or unlocked requirement; only genuinely visible HUDs block input.

Offline checks execute the actual C# capture policy and inspect shared-registry wiring. The
0.7.16 native mouse dispatcher/camera/double-click path was inspected. None of that proves
successful Harmony registration or click interception in a running IL2CPP game. Keep #43 open.

# MEN-019 — Coordinated Fire 1.6.8 / Gameplay Suite 0.32.3 (in-game verification pending)

1. Deploy **one** copy of Coordinated Fire 1.6.8, either standalone or within Gameplay Suite 0.32.3. Do not load two copies. Leave Debug logging OFF initially.
2. Equip a player squad leader with the **Berserk** perk and two coordinated-fire-compatible weapons. Record maximum AP and combined salvo fee.
3. Starting close to full AP (e.g. dismount for 5 AP), make a kill with Coordinated Fire's **first child attack**. With a 30-AP fee and 30-AP Berserk grant, the expected AP are the same as immediately before shooting, not 25 AP lower. Repeat with 0, 15 and 30+ AP already spent.
4. Repeat with **Synchronized player volley ON** and **OFF**. Repeat for a kill caused by the **second child attack**, and (if possible) multiple kills in one volley.
5. Control: an ordinary attack with Berserk must keep its native refund. An attack without Berserk must still charge the maximum child AP fee only once. Both native attacks should still consume their own ammo.
6. Edge cases: first attack kills target so second cannot fire; second native launch is rejected; canceled/abandoned targeting; setup/deploy weapon; actor turn changes. No unexpected AP gain or turn stall should occur. Verify AI behavior remains unchanged.
7. If AP differs from expectation, temporarily enable Coordinated Fire **Debug logging**, repeat once, and inspect the single fee-settlement entry (`preserved clipped grants`) plus nearby native events. Return Debug logging to OFF; no repeating logs should be necessary.
8. Keep issue [#19](https://github.com/EmaEli/menace-mode/issues/19) OPEN until gameplay results confirm the fix.

**Coverage so far:** game binary AP cap and Berserk kill handler inspected; 2,072 model arithmetic scenarios passed. This does not constitute a Windows/.NET build or an in-game test.

---

# MEN-005 — Infantry Assault 0.5.3 / Gameplay Suite 0.32.1 (in-game verification pending)

1. Build and deploy the 0.32.1 suite once using `Build-and-Deploy-Suite.cmd` (or deploy standalone Infantry Assault 0.5.3). Avoid duplicate standalone + suite DLLs. Default Debug logging OFF.
2. Use a mission with a player infantry squad and pirate infantry. Select Assault; move the mouse over several adjacent enemy tiles for several seconds **before clicking**. Cursor, targeting visuals and ordinary selection should remain usable; `Actor.AimAt(Vector3)` must not throw.
3. Cause casualties to the attacking squad (e.g. let a defender fire during CQB). **With remaining squad members**, select Assault again and hover before clicking. Repeat with partially depleted squads and after target or unit changes. The exact three historical 33/39/38-error bursts must not recur.
4. Validate target confirmation, defensive fire, assault shot, ammo consumption, suppression, failed/repulsed Assault, successful advance into a cleared tile/building/garrison, and the normal 40 AP fee. AI Assault against suppressed/pinned targets must remain usable.
5. Test ordinary firearm aiming, movement and Mount Up 2.5.1 (if installed) as separate regressions; these must not be affected by the Assault-only guard. Also test cancel/right-click while hovering an Assault target.
6. Collect full `MelonLoader/Latest.log`. Expected: **zero** `System.ArgumentOutOfRangeException` in `Menace.Tactical.Actor.AimAt` during the exercises. Normal-release output must remain free of new per-frame/per-hover logging.
7. Temporarily enable Infantry Assault Debug logging only if the preview stops responding. At most one `Assault aim-cache guard failed` warning per tactical scene is expected. Restore Debug OFF. If guard warnings occur, attach the entire build log and runtime log.

Root cause based on IL2CPP inspection, not yet proven by a controlled A/B runtime test. Do not close [MEN-005](https://github.com/EmaEli/menace-mode/issues/5) until Windows build and live regression pass.

# Mortar renderer Unity component hotfix 0.4.4 / Suite 0.31.6

1. Confirm Suite 0.31.6 and Weapon Mechanics Rebalanced 0.4.4 are loaded once (not alongside standalone duplicates); keep normal Debug OFF.
2. Deploy Pike's light mortar or Corlain's 80 mm mortar and fire once at a fixed tile. Reselect the mortar; the tile should show both the dark and colored square borders.
3. Fire repeatedly at that same tile without moving/undeploying, verifying the displayed Scatter changes (light 3->2, medium 4->3->2, direct-hit solved 1) and color progression.
4. Change selection away and back: the square should disappear and return accordingly. Actually undeploy/move the mortar: the stored solution should clear.
5. Complete a Supply Drop/off-map targeting effect: its vanilla marker must expire. Reselecting mortars must not revive that marker.
6. With WMR Debug ON (temporary) look for exactly one `MORTAR HIGHLIGHT renderer=dual-LineRenderer separate-children` diagnostic on the first successful world border, and no repeating render failures. Turn Debug OFF afterward.
7. Check `Player.log` and `Latest.log` for Unity `Can't add component 'LineRenderer'` warnings/errors, and for any `Mortar world-space tile highlight failed` error.
8. Regression: Infantry Assault 0.5.2 and Larger Maps 1.3.12 are not changed by this ZIP.

# Mortar highlight visibility 0.4.3

1. Confirm **Weapon Mechanics Rebalanced 0.4.3** loads with Debug logging OFF by default.
2. Fire a mortar once at a tile, then reselect that mortar: the remembered tile must show a clearly visible square border even over bright/rough terrain.
3. Confirm the square has a dark outer under-stroke and a red-to-green inner stroke; solved Scatter 1 remains the strongest green.
4. Trigger/complete a Supply Drop or another delayed off-map marker. Its marker must disappear normally and must not reappear when selecting/deselecting the mortar.
5. Confirm the 0.31.1 ranging reset rules are unchanged: real undeploy/movement clears the solution; unrelated stance calls do not.
6. Confirm Smoke Overhaul 0.5.1 behavior is unchanged: infantry/light=1 tile, 80 mm=cross, 120 mm=3x3, native AI smoke logic retained.
7. With WMR Debug ON, the first created border may emit one `MORTAR HIGHLIGHT renderer=...` line. Debug OFF must remain silent.

# Mortar highlight / supply-drop compatibility 0.4.1

## 0.31.1 mortar highlight regression

- Fire a mortar at one tile and confirm the red/amber/green ranging border remains visible when the mortar unit is reselected.
- Wait several seconds in tactical mode with Smoke Overhaul enabled; the remembered mortar tile must remain highlighted.
- Select/use smoke skills and confirm they do not erase the HE mortar's remembered firing solution for the same physical weapon.
- Actually undeploy the infantry mortar: the remembered solution/highlight must clear.
- Move a vehicle-mounted mortar: the remembered solution/highlight must clear.
- Confirm 0.31 smoke footprints remain unchanged: infantry/light=1 tile, 80 mm=cross, 120 mm=3x3.
- With WMR Debug ON, real resets may emit one `MORTAR RESET` line; normal Debug OFF gameplay remains silent.


1. Launch a tactical mission with a delayed off-map ability/supply drop and Weapon Mechanics Rebalanced enabled.
2. Allow the delayed off-map marker to appear and complete/expire normally; confirm its marker disappears without selecting a mortar.
3. Fire a mortar at a tile, then reselect the mortar. Confirm the ranged tile uses an independent red/orange/yellow/green world-space border.
4. Confirm selecting/deselecting the mortar does not make an expired supply-drop marker reappear.
5. Confirm mortar ranging behavior is unchanged: misses tighten Scatter only to 2; only an exact impact unlocks Scatter 1; ScatterChance is unchanged.
6. With `DebugLogging` OFF, confirm no per-frame/per-shot diagnostic spam is emitted.

# Mortar ranging 0.4 regression

1. Confirm **Weapon Mechanics Rebalanced 0.4.3** loads with Debug logging OFF by default.
2. Select a light mortar, deploy it and fire repeatedly at one fixed tile without moving or undeploying. First shot uses Scatter 3; after a miss the next shot must use Scatter 2. Further misses must remain at Scatter 2.
3. Repeat with the medium mortar: expected miss-only progression is Scatter 4 -> 3 -> 2, then it remains at 2.
4. Repeat with a 120 mm mortar: expected miss-only progression is Scatter 5 -> 4 -> 3 -> 2, then it remains at 2.
5. At any stage, if the resolved impact is exactly the intended tile, the firing solution must become solved and the **next** shot must use Scatter 1. Scatter 1 must never be reached from misses alone.
6. Confirm `ScatterChance` never changes during ranging (light 85%, medium 80%, current 120 mm 85%).
7. World-space tile feedback should move from red through warmer/greener tones as Scatter tightens; full/strong green is reserved for a solved Scatter 1 solution. Optional center text may show `S3`, `S2`, `S1`.
8. With **Debug logging ON only for diagnosis**, every mortar shot should emit `MORTAR APPLY` and `MORTAR RESULT`: verify target, impact, `directHit`, `shotScatter`, `nextScatter`, unchanged `scatterChance`, shot count and solution state.
9. Switch away from the mortar and back: the marker must persist. Undeploy an infantry mortar or move a vehicle mortar: its saved solutions must clear.
10. Confirm HE and smoke from the same physical mortar share the same per-tile solution memory.

## 0.25 Squaddie Veterancy compatibility API / packaging test

1. Confirm **Squaddie Veterancy 1.10** builds with no warnings/errors and exposes `SquaddieProbe.CompatibilityApi`.
2. Confirm `GetSquaddieXp(int)` returns persistent individual XP for a valid MENACE squaddie ID and `-1` when the runtime is unavailable.
3. Confirm `GetSquaddieRank(int)` returns the individual rank display name and an empty string when unavailable.
4. Place an older `SquaddieVeterancy-*.zip` in `artifacts`, build standalone/all, and confirm only the current Squaddie Veterancy ZIP remains.
5. Repeat for another standalone feature and for `MenaceGameplaySuite-*.zip` to confirm stale package cleanup is generic.
6. Confirm gameplay, save data, rank thresholds and normal logging are unchanged.

## 0.24.2 Coordinated Fire / Run & Gun regression test

1. Confirm **Coordinated Fire 1.6.7** loads and Debug logging is OFF by default.
2. Use Lim's **Run & Gun / Full Send** so the next eligible weapon attack receives its temporary AP discount.
3. Arm Coordinated Fire with two compatible attacks, including the previously reported shotgun + flamethrower case if available.
4. Commit the volley with enough AP to cover the combined displayed fee. Both child attacks should resolve and consume their normal ammunition/limited uses.
5. Confirm Coordinated Fire charges the combined fee exactly once and does not emit `AP accounting uncertain after child attack` or disable new salvos.
6. Repeat with a different compatible loadout while Run & Gun is active, then repeat without Run & Gun to confirm normal AP behavior is unchanged.
7. If Debug logging is enabled for diagnosis, child `Use` lines should show `NoApConsumed` in the usage flags while ammunition still decreases normally.

## 0.24.1 indirect-fire AI positioning regression test

1. Confirm **Weapon Ranges Rebalanced 2.8.1** and **Weapon Mechanics Rebalanced 0.1** both appear as separate Mod Settings groups/modules.
2. Disable Weapon Mechanics Rebalanced while leaving WRR enabled: realistic ranges (including 34/42/48 mortar reach) and the generalized long-range indirect-fire `InfluencesPositioning=false` fix must remain, while mortar AP/ammo/damage/scatter/ranging and alternate-fire AP/ammo behavior return to vanilla.
3. Disable WRR while leaving Weapon Mechanics Rebalanced enabled: weapon ranges must return to captured vanilla values, while mortar mechanics and Sustained Fire/Spray economy remain active.
4. With both enabled, behavior must match Suite 0.23 gameplay: range changes from WRR plus mechanics from WMR, without duplicate ammo consumption or duplicate ranging bonuses.
5. Keep both Debug options OFF and confirm normal gameplay produces no routine logging spam.

## 0.23 Dense Forest traversal test

1. Confirm **Forest Traversal 0.1.2** appears in Mod Settings and Debug logging is OFF by default.
2. Find a vanilla **Dense Forest** tile (`environment_backbone_forestdense`). Infantry movement preview must be able to route through it instead of treating it as impassable.
3. Confirm crossing Dense Forest costs more movement/AP than comparable open ground; the mod does not replace MENACE's native Forest/structure movement costs.
4. Confirm a vehicle still cannot route through the same Dense Forest tile.
5. Confirm Dense Forest still blocks line of sight and keeps its existing cover/destruction behavior.
6. Toggle **Infantry can traverse Dense Forest** OFF and start/reload Tactical; Dense Forest should return to vanilla infantry-blocking behavior.

## 0.22 cumulative mortar-ranging test

1. Confirm the suite reports WRR 2.7 and Debug logging remains OFF by default.
2. Deploy an infantry mortar and fire at tile A repeatedly: first shot has no ranging bonus; later shots apply -10 / -20 / -30 / -40 / -50 percentage points ScatterChance, then stay capped.
3. Fire at tile B, then return to tile A: tile A must retain its accumulated solution and tile B must keep its own independent count.
4. Undeploy and redeploy the infantry mortar: all remembered tile solutions must be cleared.
5. For a vehicle 120 mm mortar, verify solutions survive while stationary and reset after the vehicle moves.
6. Verify HE and smoke from the same mortar share the same tile solution.

## 0.21 mortar range / repeat-target ranging-fire smoke test

1. Confirm WRR reports version 2.6 and Debug logging remains OFF by default.
2. At 100% WRR/Artillery strength, confirm light mortar maximum range is **34**, medium 80 mm is **42**, and heavy 120 mm is **48**.
3. Confirm infantry and vehicle smoke-mortar skills can target out to the same parent-weapon maximum range.
4. Confirm the 120 mm HE and twin-fire HE skills, which override range natively, also target out to **48**.
5. Fire a mortar at tile A once; the first shot must use the normal configured scatter chance.
6. Fire the same mortar skill at tile A again. With the default setting, its scatter chance for target resolution should be temporarily reduced by **25 percentage points**.
7. Change to tile B and confirm the ranging bonus does not carry over. Return to tile A only after firing at B and confirm it is treated as a fresh target.
8. Repeat the same-target test with a smoke-mortar skill and, if available, the 120 mm mortar.
9. Change `Repeat-target aim bonus (scatter chance pp)` in Mod Settings and confirm 0 disables the bonus while higher values tighten repeated shots without permanently altering the base template.
10. Keep Debug logging OFF and confirm repeated targeting/firing adds no routine log spam.

# Suite 0.25 test checklist

## 0.20 alternate rifle fire-mode smoke test

1. Equip a standard Assault Rifle with `Salvo` + `Sustained Fire`. Confirm both attacks show **40 AP**.
2. With at least 4 ammo remaining, fire Sustained Fire once. Confirm the shared weapon-ammo counter drops by **2**, and the normal Salvo button immediately reflects the same remaining ammo.
3. Repeat with a Battle Rifle that has `Shoot` + `Spray`: both should cost **40 AP**, and Spray should consume **2** shared ammo.
4. Leave exactly 1 ammo use in either weapon. Confirm Sustained Fire/Spray is unusable while the normal one-ammo shot remains usable.
5. Test an AI unit carrying a supported Assault/Battle Rifle and confirm it cannot select the high-volume mode with only 1 ammo remaining.
6. Equip a marksman/HAMER Battle Rifle with `Aimed Fire`. Confirm Aimed Fire consumes **1** ammo. Current extracted v0.7.15 aimed-fire skills should remain **40 AP**.
7. Cancel/fail targeting before a shot resolves and confirm no extra ammo is consumed.
8. Keep WRR Debug logging OFF and confirm there is no routine fire-mode log spam.


## Vehicle Crew Pressure regression

- Put a player, allied or enemy pressure-managed vehicle into Buttoned Up (50-99 pressure by default). It must remain normally controllable/AI-usable, with only the configured AP and accuracy penalties.
- A Buttoned vehicle must not enter MENACE generic panic/flee movement even if native morale internally falls to Wavering/Fleeing.
- At Break Contact threshold (100 by default), only the mod's controlled retreat should occur.
- During the forced-retreat activation the HUD/state must remain Break Contact; only after that activation ends should pressure fall to the configured post-break Buttoned value.
- On the following activation, the Buttoned vehicle must receive the AP penalty once and otherwise remain usable.

## 0.19.2 mortar smoke economy / field cover

1. Equip/use the medium 80 mm mortar and confirm HE uses are doubled from vanilla 10 to 20 while the existing halved firing AP cost remains.
2. If available, verify light HE mortar uses 16 instead of 8 and 120 mm HE uses 16 instead of 8.
3. Verify infantry mortar smoke uses are doubled from 4 to 8 and AP cost is halved from 40 to 20.
4. If available, verify 120 mm autoloader smoke and twin-fire smoke use 16 instead of 8 and cost 30 AP instead of 60.
5. Confirm the base smoke pattern, scatter, VFX and tile effects are unchanged; only cumulative per-tile ranging may temporarily reduce scatter chance.
6. Fire beside (not directly on) a unit positioned exactly one tile outside the highlighted/native HE blast AoE; it should receive suppression but no new physical damage from the WRR outer ring.
7. Fire light and medium HE mortars directly at sandbag/low-wall half-cover and confirm the cover survives, including after loading into Tactical where skill handlers may have been rebuilt.
8. Verify 120 mm mortar cover destruction follows the normal Cover Destruction Rebalance tier rule rather than the light/medium immunity exception.
9. With WRR Debug logging OFF, repeated mortar fire should add no routine log spam.

## 0.16 Smoke Overhaul / infantry grenades

1. Confirm `SmokeOverhaul.dll` loads once and Smoke Overhaul appears in Mod Settings with Debug OFF.
2. Start a tactical mission and confirm normal player infantry and human AI infantry receive one smoke-grenade ability with 3 uses and one Tier 2 fragmentation-grenade ability with 3 uses; aliens/constructs/decoys must not receive either grant.
3. Confirm the frag ability is `active.throw_frag_grenade_tier2` and retains vanilla 40 AP cost and range 1-3.
4. Confirm infantry whose template or default equipment already grants Tier 2 frag does not show a duplicate frag button.
5. Verify vanilla smoke no longer blocks LOS completely and one/two/three intervening smoke tiles apply -20/-40/-60 percentage-point hit-chance penalties.
6. Verify attacker/defender tiles are excluded from the extra line penalty and vanilla inside-smoke effects still apply.
7. Confirm both player and AI infantry receive the grenade grants, while vehicles and structures do not.
8. Confirm normal gameplay remains quiet with Debug OFF.


## 0.14.3 Infantry Assault defensive-fire diagnostic

1. Enable **Infantry Assault -> Debug logging** only for the test.
2. Perform one infantry assault in open ground if possible.
3. Capture `Latest.log` and `Player.log`.
4. Expect one `[ASSAULT_DEBUG] defensive-pre` and one `[ASSAULT_DEBUG] defensive-post` line.
5. Verify `attackerTile` equals `targetTile`, `resolvedTarget` identifies the attacker, and `intendedMatch=True`.
6. Compare `hp=before->after`, `armor=before->after`, `suppression=before->after` and `morale=before->after` to determine whether the defensive shot missed, hit, or targeted the wrong entity.
7. Disable Debug again after the test.


## 0.14 veterancy HUD marker smoke test

1. Start a tactical mission with `Show veterancy markers` enabled and Debug logging OFF.
2. Confirm infantry rank markers render immediately to the right of the HP/suppression block and never below the bars.
3. Select/reveal multiple friendly and hostile infantry units to exercise pooled/rebuilt UnitHUDs; marker position must remain attached to the correct unit HUD.
4. For a vehicle with Crew Pressure active, confirm the rank marker sits to the right of the HP/Crew Pressure block rather than using the hidden native vehicle suppression bar.
5. Confirm the marker does not enlarge the HUD background or recreate the historical black rectangle.

## 0.14.3 mortar / secondary-range regression

1. Confirm the suite loads WRR 2.7 and Cover Destruction Rebalance 0.3.1 once, with WRR Debug OFF by default.
2. On a 64x64 map, stress-test long-range indirect fire from multiple factions: Rogue/local-force artillery, Rogue mortar teams, the pirate rocket guntruck, and any available modular 120 mm mortar/rocket-organ vehicle. Confirm AI turns remain responsive and no `AI task timed out` / `Collection was modified` regression appears.
3. Fire light, medium and single heavy mortar HE and confirm each uses its native single firing cycle; native twin-fire remains native.
4. Confirm live mortar AP costs are 20 light / 25 medium / 30 heavy (50% of v0.7.15 native costs).
5. Confirm mortar impacts visibly disperse more than vanilla: scatter radius 3 light / 4 medium / 5 heavy, and the heavy mortar no longer behaves as if it has the vanilla +50 point-accuracy bonus.
6. Compare equivalent hits against the same target and confirm physical HP/armor-durability damage is approximately halved while suppression/morale output is not halved.
7. Compare units around impacts and confirm suppression is distributed across a wider beaten zone without extra artificial projectiles.
8. Fire light/medium mortar HE at sandbags/low walls represented as half-cover and confirm the cover object survives. Verify other non-mortar cover destruction still follows CDR's accepted 1->1, 2->1, 3->2 rules.
9. Verify secondary explicit-range attacks follow their WRR parent proportionally: underbarrel grenade; battle-rifle spray/aimed modes; heavy-SMG/PDW/SMG tap-fire modes.
10. With WRR strength changed from 100%, confirm those secondary ranges scale with the parent rather than jumping directly to the primary max.
11. Disable WRR and confirm captured mortar dispersion/template values and secondary skill max ranges restore without errors.

## 0.12.9.4 medium-mortar AI positioning policy verification

- With WRR Debug logging enabled, confirm `[MORTAR_AI_POLICY]` reports `desired=False`, `final=False`, and `write=ok`.
- On a 64x64 Larger Maps mission, stress with several Rogue Army medium mortar teams and confirm AI turns remain responsive through multiple rounds.
- Confirm there are no `AI task timed out for actor 'enemy.rogue_army_mortar_weapon_team'` messages and no `Collection was modified` exceptions.
- Confirm medium mortar teams still fire `active.launch_medium_mortar_frag` at the accepted realistic range.
- Turn WRR Debug logging OFF for normal play; the temporary `Attack.OnCollect` probe has been removed.

## 0.12.8 native Vision property integration

- Confirm Visibility Overhaul reports version 3.1.9 in Mod Settings/debug UI.
- With the default +10 vision bonus, select a normal player infantry squad whose vanilla Vision is 9 and confirm the effective tactical vision behaves as 19 before other modifiers.
- Move the unit several tiles on a 64x64 Larger Maps mission and confirm newly visible terrain/fog remains extended instead of collapsing back to the vanilla radius.
- Cycle/select another unit and confirm there is no visibility flicker or sudden radius reset.
- Test a vehicle as well as infantry; the flat Vision bonus should still apply to both, while movement-based concealment remains infantry-only.
- Equip/use a vanilla Vision modifier such as binoculars/high-ground/scout/vehicle sensors where available and confirm it composes with the mod bonus rather than replacing it.
- Toggle the configured Vision bonus and force a normal property refresh (selection/state/skill update); confirm MENACE updates visibility without any mod-triggered global map rebuild.
- Keep Debug logging OFF and confirm there is no routine per-frame/per-movement visibility spam.

## 0.12.7 occupied multi-tile collapse fix (retained)

- Garrison player infantry inside `building_civilian_shelter_1x2_01`.
- Destroy the occupied shelter with an allowed structural attack (for example the alien bug blaster, RPG, cannon, or other non-small-arm source).
- Confirm the occupant starts and completes the native `Leave` movement before the structure collapses.
- Confirm the game does not terminate during `Structure.Die`, and the unit remains valid after the collapse.
- Repeat on an empty 1x2 shelter to confirm the already-working empty-collapse path is unchanged.


## 0.11.3 focused regression checks (retained)

- LoS: verify both objects titled `Cargo Containers` block sight, while `obstacle_pirate_container_01_1x1` (`Cargo Container`) does not.
- Visibility: on a 64x64 Larger Maps mission, move a player unit and confirm newly visible terrain/fog updates with the +vision bonus without selecting/cycling another unit.
- VMAT: confirm `active.launch_vmat_missile` reports max range 18 at default WRR strengths, while min 3 / ideal 6 remain unchanged.
- Mortar regression: confirm WRR mortar target constants and behavior are unchanged from 0.11.2.
- Medium mortar AI: 0.12.3 keeps the long WRR range but disables only the skill's attack-specific `InfluencesPositioning` search. No runtime AI collection hooks are present.

## Build/deploy

1. Close MENACE.
2. In each changed package run `jiangyu compile --release --clean` from `mods/jiangyu/<Module>/`.
3. Copy the package's `compiled/` output into `Mods\<Module>\` on the game PC. Remove any legacy copy of the same module (`modpack.json` + `dlls\` folder, or a root-level DLL) so the module is loaded only once.

## Loader smoke test

On launch, inspect `MelonLoader/Latest.log` and confirm:

- one modpack named `MENACE Gameplay Suite` is loaded;
- all fifteen suite DLLs are loaded once each: `SquaddieProbe.dll`, `Overwatch.dll`, `CoordinatedFire.dll`, `FieldLogistics.dll`, `InfantryAssault.dll`, `StrongerCover.dll`, `BuildingCombatRules.dll`, `ForestTraversal.dll`, `CoverDestructionRebalance.dll`, `WeaponRangesRebalanced.dll`, `WeaponMechanicsRebalanced.dll`, `GarrisonableBuildings.dll`, `BuildingsBlockLineOfSight.dll`, `SmokeOverhaul.dll`, and `VisibilityOverhaul.dll`;
- fifteen suite `Plugin` implementations are discovered/initialized;
- there are no duplicate-patch or plugin-initialization errors.

## Settings smoke test

Confirm these existing groups still appear:

- SquaddieProbe / Squaddie Veterancy settings;
- Overwatch;
- Coordinated Fire.
- Field Logistics.
- Infantry Assault;
- StrongerCover.
- Building Combat Rules.
- Forest Traversal.
- Weapon Ranges Rebalanced;
- Weapon Mechanics Rebalanced.

The setting keys/groups were intentionally not renamed, so existing `UserData/ModSettings.json` preferences should carry across.

## Gameplay smoke test

1. Load a save that already has Squaddie Veterancy data and check roster/ranks.
2. Enter Tactical and verify Veterancy HUD feedback.
3. Arm Overwatch with one infantry unit; move/fire with an enemy and confirm reaction behavior.
4. Use Coordinated Fire with a compatible squad + leader pair.
5. Enable the Overwatch `Both` path and confirm Coordinated Fire is detected from the suite install (this specifically tests the new shared presence resolver).
6. End several turns with AI enabled for both features.
7. Check `Latest.log` for excessive logging. Debug options should remain OFF.

## Disabling a module

Each module is an independent Jiangyu package; remove or rename its `Mods\<Module>\` folder to disable it. There is no longer a combined suite package.


## Infantry Assault 0.5.1 smoke test

State-gate checks:

1. Default/ready attacker: Assault may be used normally against a valid adjacent target.
2. Deployed/kneeling attacker: Assault must be unavailable.
3. Suppressed attacker: Assault must be unavailable.
4. Pinned Down attacker: Assault must be unavailable.
5. Pinned Down defender: defensive fire is skipped (suppression or pinned stance).
6. Suppressed defender: defensive fire is allowed with half CQB bonuses, unless genuinely stunned.


1. Select infantry with at least 40 AP adjacent to enemy infantry.
2. Activate Assault and click the enemy tile.
3. Confirm exactly 40 AP is charged.
4. With a normal armed defender, confirm defensive fire happens before the attacker shot.
5. Repeat against a DevHotkeys/transient spawned armed infantry target; defensive fire should also occur if it has a valid 1-tile attack.
6. Confirm the attacker assault shot receives the configured CQB hit-chance bonus (default +20 percentage points).
7. After the assault, confirm normal shots no longer receive the CQB bonus.
8. Confirm the defender keeps only the configured percentage of cover protection during the assault shot (default 50%).
9. If the defender survives, attacker stays in place.
10. If the defender dies, attacker advances into the target tile/container.
11. Move the mouse and select/cycle units immediately afterward: there must be no repeating `SkillAction.HandleMouseMoveOnTile` / `Actor.AimAt` index errors, including after defensive fire or assault fire removes squad members.
12. Spawn or encounter xeno/pseudo-infantry and confirm there is no runtime Assault synchronization warning for actors whose template does not declare Assault.

## StrongerCover 2.2.3 smoke test

1. Confirm the suite loads `StrongerCover.dll` once and no standalone StrongerCover copy is present.
2. Confirm the StrongerCover Mod Settings group is present and Debug logging is OFF by default.
3. Compare an exposed target against light/medium/heavy directional cover and confirm hit chance changes according to the configured multipliers.
4. Verify bunker/forest/open-vehicle/watchtower inside-cover values still apply.
5. Verify indirect-fire cover and squad-size scaling behave as in standalone 2.2.3.
6. With Garrisonable Buildings installed separately, verify the `cover_inside_building` compatibility path still works.


## Building Combat Rules 0.1.4 smoke test

1. Confirm **Small arms cannot damage buildings** is enabled and Debug logging is OFF.
2. Fire a pistol/carbine/assault rifle/SMG/shotgun/LMG at a building and confirm the building does not lose HP/armor or collapse.
3. Fire the same small arm at infantry garrisoned inside a building and confirm the infantry can still take damage while the building remains intact.
4. Fire an RPG/rocket/grenade/mortar/flamethrower/anti-tank or cannon-class weapon at a building and confirm structural damage is still allowed.
5. Destroy an **empty** `building_civilian_shelter_1x2_01` with an RPG/rocket or other allowed structural weapon. The structure must collapse without terminating the process.
6. Repeat with player infantry currently garrisoned inside. Confirm the occupant begins its native exit, finishes leaving the building, and only then the structure collapses without terminating the process.
7. Repeat on at least one other multi-tile Garrisonable template (1x2/1x3/2x2 or larger) and confirm native collapse completes.
8. After one building collapses, verify another surviving building of the same template is still enterable and still uses `cover_inside_building`; this confirms the temporary template normalization was restored.


## Field Logistics 1.0.6 smoke test

1. Confirm `FieldLogistics.dll` loads exactly once from the Suite and no standalone FieldLogistics folder remains.
2. Confirm the Field Logistics Mod Settings group is present; Debug logging is OFF and infantry ammo sharing is OFF by default.
3. Confirm a player vehicle has `Resupply Infantry` with one use by default and can refill an adjacent infantry unit.
4. Equip the Field Ammunition Supply module and confirm extra resupply uses are added.
5. Temporarily enable infantry ammo sharing and confirm eligible player infantry show `Redistribute Ammo`.
6. Confirm the same infantry also still show `Assault`; this validates the merged Suite manifest skill grants.
7. Exercise ammo redistribution between compatible adjacent squads and confirm total ammunition is conserved.
8. With another vehicle/template mod such as Mount Up enabled, start a mission and confirm `Resupply Infantry` still appears on player vehicles.
9. Load a save or mission where an actor could have been materialized before the template patch pass; confirm a missing Field Logistics action is recovered without duplicate skill buttons.
10. With Field Logistics Debug logging temporarily enabled, spawn a player vehicle and confirm the resupply diagnostic reports readable native use state (for defaults, `desired=1`, `max=1`, `uses=1`) instead of `max=-1`, `uses=-1` and missing `SetUses`/`SetMaxUses` methods. Turn Debug logging back OFF after the check.


## 0.6.1 regression checks

1. Play several turns without using Redistribute Ammo; confirm normal weapon fire does not emit `Redistribute Ammo candidate` or `Redistribute Ammo cache` lines, including with Field Logistics Debug logging enabled.
2. Exercise movement/garrison transitions that previously produced `Actor.GetCurrentProperties() failed`; confirm Garrisonable Buildings no longer floods reflected stale-actor warnings.
3. Re-test Assault, Coordinated Fire, Overwatch, vehicle resupply and Building Combat Rules for unchanged behavior.

## 0.12.3 medium-mortar AI positioning regression

- Confirm `specialweapon.tripod_medium_mortar_80mm` keeps its normal WRR long range; there is no diagnostic max-range clamp.
- Run a 64x64 mission with Rogue Army mortar teams for several rounds.
- Confirm mortar teams can still deploy and fire `active.launch_medium_mortar_frag` when a target is valid from their current position.
- Normal AI movement may still reposition mortar teams; they should no longer search specifically for alternate move-to-fire origins for the mortar attack.
- Check that `AI task timed out for actor 'enemy.rogue_army_mortar_weapon_team'` does not recur under the previously problematic conditions.
- Check that `Collection was modified` inside `AI.Behaviors.Attack.OnCollect()` does not recur.
- Confirm runtime startup contains no WRR hooks for `Attack.OnCollect` or `AIFaction.GetOpponents`.
- Disable WRR from Mod Settings, reapply settings/scene as normally required, and confirm the vanilla `InfluencesPositioning` behavior is restored without errors.

## Smoke Overhaul 0.5.1

- Start a fresh tactical mission and confirm each normal humanoid infantry unit still has exactly one smoke-grenade button and one Tier 2 frag-grenade button, each with 3 uses.
- Confirm hand-thrown grenades still use the configured 20 AP default while mortar smoke retains mortar AP economy.
- Throw an infantry smoke grenade and verify **only the selected tile** receives smoke.
- Fire light-mortar smoke and verify **only the selected tile** receives smoke.
- Equip/use the 80 mm medium mortar and verify it now has a smoke-shell skill. Fire it and verify smoke appears in an exact five-tile `+` footprint: center, north, south, east and west; diagonals remain clear.
- Fire heavy/120 mm mortar smoke and verify its vanilla 3x3 footprint is unchanged.
- Check targeting previews for infantry/light/80 mm smoke and confirm previewed AoE matches the actual footprint.
- Run AI infantry with smoke available and confirm it can deliberately use smoke in tactically relevant LOS situations.
- Run AI mortar teams/platforms with smoke available and confirm mortar smoke can be selected by native AI; verify it is not spammed every frame/decision loop.
- With Smoke Overhaul Debug Logging enabled, confirm AI smoke execution emits concise `AI SMOKE USE` lines. With debug disabled, confirm no per-use/per-frame smoke diagnostics are written.
- Confirm smoke still does not fully block LOS, still applies the configured per-tile hit-chance penalty, and lasts according to the configured 1x-4x duration multiplier.
- Disable `Caliber-based smoke footprints` and confirm the 80 mm smoke grant is removed and shared light-mortar smoke spawn chance is restored without errors.
- Disable `AI smoke usage` and confirm mortar AIConfig is restored without affecting infantry's vanilla smoke behavior.
- Cycle repeatedly between squads after using grenades; the skill bar must not duplicate grenade buttons or throw `SkillBar.UpdateSkills` index errors.

## Infantry Assault 0.5.2 native skill recognition regression

1. Start with Gameplay Suite 0.31.5 and keep only one copy of Infantry Assault loaded (the suite copy).
2. Revisit a saved Rogue Army mission with all six player infantry and mixed enemy infantry/weapon teams. Enable Infantry Assault Debug *temporarily*.
3. Verify the two diagnostic scans complete. After actor initialization, expect the manifest-declared eligible infantry to show `missing=0` with no `Assault runtime synchronization failed` warnings; an early pre-spawn scan with `eligibleInfantry=0` is expected.
4. Verify one, not two, Assault buttons on each eligible player squad. Confirm adjacent enemy infantry can be targeted and 40 AP is spent.
5. Verify enemy infantry can still initiate Assault only against adjacent Suppressed/Pinned Down opponents; existing deployment/stance restrictions still apply.
6. Test reload/save to ensure no duplicates, no UI refresh exceptions, and no `Agent.Evaluate` exceptions or turn stalls.
7. Turn Infantry Assault Debug OFF again. The game should stay silent during ordinary tactical play.

If `missing` remains nonzero, capture the specific actor template IDs and the `MelonLoader/Latest.log`; do not disable the manifest gate or inject skills into unspecified units.

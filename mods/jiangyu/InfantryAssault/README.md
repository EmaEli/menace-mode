# Infantry Assault 0.5.5

## 0.5.4 MENACE 0.7.16 compatibility (#41)

- MENACE v0.7.16 removed `Skill.GetDescription()`. Version 0.5.3 failed to bind that method and silently dropped all its Harmony patches, so Assault could not be used. The patch is removed; the tooltip text comes from the mod's own `SkillTemplate.Description`.
- An unresolved Harmony target is now reported with its handler name in `MelonLoader/Latest.log`.

## 0.5.3 targeting stability fix (MEN-005)

- During Assault targeting, MENACE can reuse an outdated cached list of executing soldier indices after squad casualties. The mod now checks that list and regenerates it through the game's own selection logic when outdated.
- Native targeting previews remain active; other skills and Mount Up are not patched. AP/CQB/AI rules are unchanged.
- If validation cannot be performed safely, only the current Assault hover preview is skipped. Debug is OFF by default (an error can be logged once per scene if enabled).
- In-game verification: retry Assault previews before and after losses and inspect `MelonLoader/Latest.log` for `Actor.AimAt` errors.

## 0.5.2 skill detection fix

- Recognizes Assault on `BaseSkill` wrappers returned by `SkillContainer.GetAllSkills()` or the add queue. Native-pointer identity and live/queued re-checks prevent duplicate recovery attempts.
- All Assault combat and AI rules are unchanged. Debug diagnostics are optional and off by default.


Adds a new adjacent infantry assault action.

## Rules in 0.5.1

- Infantry can assault adjacent enemy infantry only while in the normal mobile stance. **Deployed/kneeling**, **Suppressed**, and **Pinned Down** attackers cannot initiate Assault. The same gate applies to player and AI usage.
- Works against infantry in the open and infantry contained in structures such as bunkers, towers, and garrisonable buildings.
- Assault costs a flat 40 AP. Weapon AP and movement/entry AP do not add to that cost.
- If the defender can fire with eligible direct-fire small arms, it performs defensive fire first. Ready defenders receive the full configured CQB bonuses. **Pinned Down** defenders may still return fire, but receive half of both the configured CQB accuracy bonus and the CQB cover-reduction benefit. **Suppressed** defenders cannot return fire.
- Surviving assaulters then fire with eligible direct-fire small arms under the full CQB hit-chance bonus and cover-reduction rule. Defaults remain +20 percentage points and 50% retained cover protection; if CQB bonus is set to +50pp, a ready defender and the assaulter receive +50pp while a Pinned Down defender receives +25pp.
- Tripod/AT/ATGM/cannon/launcher/mortar and other crew-served heavy modes remain excluded as CQB weapons.
- If the defender is destroyed, the attacker advances into the target tile/container. If the defender survives, the attacker remains where it started.
- AI infantry still considers Assault only against adjacent enemies whose native MENACE suppression state is `Suppressed` or `PinnedDown`, and now also obeys the attacker's own stance/suppression gate.
- Player targeting cleanup now also blocks native mouse-move aiming on the exact committed Assault `SkillAction`, closing the remaining stale-action path after CQB casualties.

## 0.5.1 test focus

1. Ready/default-stance attacker vs ready defender: Assault is usable and both get full CQB bonuses.
2. Deployed/kneeling attacker: Assault is unavailable.
3. Suppressed attacker: Assault is unavailable.
4. Pinned Down attacker: Assault is unavailable.
5. Ready defender: defensive fire uses full configured CQB bonuses.
6. Pinned Down defender: defensive fire still occurs but uses half accuracy and half cover-reduction bonus.
7. Suppressed defender: no defensive fire.
8. Failed assault: defender survives, attacker remains on origin tile, and mouse movement does not produce stale `Actor.AimAt` errors.
9. Successful assault: attacker advances into the target tile/container.
10. AI adjacent to an eligible suppressed/pinned target can Assault only when the AI attacker itself is not deployed, Suppressed, or Pinned Down.

If the mod fails to build, send `artifacts/build-and-deploy.log`.
If it builds but fails in game, send `MelonLoader/Latest.log`.


## 0.3 AI suppression gate

- AI target selection now requires MENACE native `SuppressionState` to be `Suppressed` or `PinnedDown`.
- Unsuppressed adjacent infantry are ignored by the Assault AI and handled by normal MENACE AI behavior.
- Player-controlled Assault remains unrestricted by suppression state.
- Existing 40 AP cost, defensive fire, CQB accuracy bonus, reduced defender cover, victory advance, and small-arms restrictions are unchanged.

## 0.2.4 cleanup

- Assault and defensive fire are restricted to direct-fire small arms; tripod AT guns, ATGM, cannons/autocannons, crew-served MGs, launchers, mortars and suppressive/sustained-fire modes are excluded from CQB.
- Runtime skill synchronization no longer reports a false failure when the Assault skill was added successfully but MENACE rejected the optional UI refresh on a loaded-save actor.
- Debug logging remains OFF by default.

## 0.2.3 combat fix

- Defensive fire now accepts genuine 1-tile attack skills even when a transient/dev-spawned infantry actor does not expose them as item-backed weapons. Conventional equipped weapons are still preferred.
- Added configurable `CQB hit chance bonus`, default +20 percentage points, applied only to the attacker's assault shot and restored immediately afterward.
- Debug logging remains OFF by default.

## 0.2.2 build fix

- Fixed the unified-repository build failure caused by a missing `System.Runtime.CompilerServices` import for `RuntimeHelpers.GetHashCode`.
- No gameplay, balance, targeting, AI, or logging behavior changes from 0.2.1.

## 0.2.1 fix

- Player Assault now fully clears the intercepted `SkillAction` from `TacticalState` after target confirmation. This prevents the stale targeting action from throwing `SkillAction.HandleMouseMoveOnTile` null-reference exceptions every frame after an assault.
- Runtime fallback injection is restricted to infantry templates that actually declare the Assault skill, avoiding accidental attempts to add it to xeno/pseudo-infantry actors.

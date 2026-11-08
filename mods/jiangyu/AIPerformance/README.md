# AI Performance 1.3.1

Independent AI DLL; Larger Maps is optional and no longer installs it automatically.
Requires Jiangyu and MenaceModSettings. The Suite explicitly includes both modules.

## Experimental recent-contact approaches (#17)

- `ConvergeOnLastKnownContacts` is OFF by default and requires a game restart.
  Enable **AI Performance / Unassigned infantry approach recent contacts (0.7.18)**
  before launching a mission. Independent of simplified planning and advance recovery.
- Only pirate/Rogue infantry with **no faction mission zones**, no useful combat
  action/candidates and no existing tile utility qualify. Does not override defense,
  attack, retreat, sleep/deployment, suppression, morale or movement restrictions.
  Vehicles, machine/xeno/drone units, indirect-fire assets and weapons teams are excluded.
- Stores coordinates only on native faction-detection events, never by polling hidden
  actors or remembered-contact TTL lists. Contacts expire after two unseen rounds;
  approach uncertainty grows from 3 to 7 tiles. No hidden movement/death tracking.
- At most three responders per faction approach slightly different areas. Arrival,
  two stationary rounds or three response rounds stop the attempt; fresh detection
  can permit another. Absent responders release their slots. Native Move chooses
  reachable destinations, cover, paths and AP; only no-less-safe progress tiles receive
  its utility threshold. No new path search or shared mission-order mutation.
- Reviewed 0.7.18 native hash only; works on both 64x64 and enlarged maps.
  Unknown builds retain native behavior. `DiagnoseContactConvergence` is OFF by
  default and capped at 50 lines per mission. No per-frame work.
- First-stage prototype: **no last-known mortar fire, preparatory bombardment or
  off-map artillery changes yet**. Existing combat/planning behavior is unchanged
  when the new option is OFF. Gameplay effectiveness and turn-time impact need testing.

## Existing performance and advance-recovery behavior

- Tracks the actual AI worker task instead of a wrapper that detaches after 12 seconds.
  Does not cancel work, skip units, suppress exceptions or block the game thread.
- Simplified attack/buff planning defaults ON, including native 64x64 and enlarged maps.
  Disables hypothetical future firing-position evaluation;
  this is a deliberate AI positioning tradeoff, not behavior-identical acceleration.
- Applies to every eligible shared attack/buff skill template, with no faction or
  infantry-profile exception. The old PreserveBasicInfantryFirePositioning option is
  removed; any saved value is ignored. Native profiles already set to false remain unchanged.
- Optional nearby destination limits default OFF (20 destinations / 12 tiles when ON).
- Restores modified template flags and candidate limits on mission preparation,
  strategy/title transitions and unload. Native pathfinding/player movement untouched.
- Settings are under **AI Performance**. Disable simplified planning there for an OFF
  comparison, then restart MENACE.
- Conditional advance recovery now lives here, not in LargerMaps.dll. It is ON by
  default on actual maps above 64x64 and independently hash-locked to reviewed
  0.7.17+22362 / 0.7.18+22828 binaries. Unknown builds retain native behavior.
- Requires two idle rounds at the same position/order, one to eight exclusively hostile
  AdvanceTowards zones, full morale, no suppression, available AP, no valid attack candidates/positive
  other actions or tile utility. Sleep, defense and arrival are preserved. Once native
  Move has produced an AP-valid destination toward the order, recovery selects Move
  instead of Idle; native Move still owns destination/path/safety calculation.
- Handles the null-destination case before native Move.HasUtility: safe tiles strictly
  closer to the advance order receive the native movement utility threshold, only after
  the existing idle-history and no-shot guards pass. Current/farther/less-safe tiles
  receive no incentive; existing positive utility is preserved. Native Move still
  chooses targets and computes reachable paths/AP costs; no additional path search.
- Multiple advance orders use the native Entity.ID modulo advance-count allocation,
  not the first/nearest zone. Multi-zone distance weights are left untouched; only safe
  utility toward the allocated zone is recovered. Arrival stops recovery; mixed/defensive
  orders are excluded. The full ordered list of areas/priorities is revalidated before
  utility/choice, so even an unselected zone's change invalidates stale eligibility.
- No contact or attack candidate is fabricated. This applies before or after contact.
- A legal recovered Move selection retains eligibility during same-round re-evaluation;
  provisional selection alone is not proof of movement. Actual position/order changes,
  completed movement, useful non-movement actions and all existing combat guards cancel
  recovery normally. This fixes 1.2.1 losing its target between planning and execution.
  History resets between missions. Old Larger Maps recovery settings are not migrated;
  use **AI Performance / RecoverStalledAdvance** for OFF comparisons.
- No update loop or default per-unit logging. Optional `DiagnoseAdvanceRecovery`
  defaults OFF and is capped at 200 lines per mission. When enabled, it traces only
  hostile advance-order candidates, deduplicates unchanged rejection stages, and reports
  whether recovery was armed/applied/forced plus seeded utility and subsequent Move/Idle scores. Larger Maps
  retains optional general AI diagnostics but no recovery hooks.

Managed fixture checks exercise production task/planning/restore code. Metadata-only
checks inspect current game proxies before canonical staging; they do not execute IL2CPP.
Windows installation, native dispatch and real battle timing acceptance are separate.
Issues: #2 (stalled advance), #93 (extraction), #57 (late-round slowdown).
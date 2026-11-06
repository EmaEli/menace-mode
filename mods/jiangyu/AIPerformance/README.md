# AI Performance 1.1.0

Independent AI DLL; Larger Maps is optional and no longer installs it automatically.
Requires Jiangyu and MenaceModSettings. The Suite explicitly includes both modules.

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
- Requires two idle rounds at the same position/order, one hostile AdvanceTowards
  zone, known contact, full morale, no suppression, available AP, no valid attack
  candidates/positive other actions or tile utility. Sleep, defense and arrival are
  preserved. Only missing native distance/inside-zone utility contributions are
  restored; native Move still decides paths, AP, safety and action competition.
- No contact override or promise to fix pre-contact stalls. No extra AI/path search.
  History resets between missions. Old Larger Maps recovery settings are not migrated;
  use **AI Performance / RecoverStalledAdvance** for OFF comparisons.
- No update loop or default per-unit logging. Optional `DiagnoseAdvanceRecovery`
  defaults OFF and is capped at 200 lines per mission. Larger Maps retains optional
  general AI diagnostics but no recovery hooks.

Managed fixture checks exercise production task/planning/restore code. Metadata-only
checks inspect current game proxies before canonical staging; they do not execute IL2CPP.
Windows installation, native dispatch and real battle timing acceptance are separate.
Issues: #2 (stalled advance), #93 (extraction), #57 (late-round slowdown).
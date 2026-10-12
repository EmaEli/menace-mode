# MEN-001 smoke test plan (not yet verified in-game)

Build standalone SmokeOverhaul 0.6 or Gameplay Suite 0.33 on Windows using the repository's standard Build.ps1 with MENACE game references. Do NOT combine duplicate suite and standalone SmokeOverhaul DLLs.

1. With Mod Settings enabled, check FastAISmokeSelection / EnableAISmokeUsage / AllowSmokeWhilePinnedDown / SimpleAISmokeRules ON; DebugLogging OFF.
2. Regular smoke throw has configured grenade AP cost (default 20). Suppressed throw still costs 20.
3. Pinned Down infantry at 0 AP can target a valid empty tile and throw smoke at 0 AP, consuming an actual smoke use. Regular frag, weapons and other actions must not become free. Check native cooldown and 0 uses.
4. Enemy AI under fire with smoke: compare pinned vs suppressed use on empty adjacent squares; repeat with blocked/already-smoked tiles.
5. Enemy flamethrower in exposed open area, aware of player: check preference for smoke 1-2 tiles in front. Native AI alone decides whether to advance into smoke later; no scripted move.
6. Compare median and worst AI turn times over five turns in identical 64x64 and 96x96 missions with v0.5.2; reject major regression, loop or log spam.
7. Toggle AllowSmokeWhilePinnedDown OFF between missions and verify original permission restored. Toggle SimpleAISmokeRules OFF and verify normal native smoke selection.
8. Regression-test mortars, smoke footprints, AI cooldown, regular grenade economy and with third-person camera mod omitted.

This is a draft. Require clean Windows build and observed in-game validation before merging or closing #1.

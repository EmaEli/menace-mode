# AI Performance 1.0.0

Companion DLL bundled automatically with Larger Maps 2.6.0 by the canonical installer.
Requires Jiangyu and MenaceModSettings. Do not install a legacy Larger Maps copy too.

- Tracks the actual AI worker task instead of a wrapper that detaches after 12 seconds.
  Does not cancel work, skip units, suppress exceptions or block the game thread.
- Simplified attack/buff planning defaults ON on maps of at least 64x64, including
  Larger Maps' safe fallback. Disables hypothetical future firing-position evaluation;
  this is a deliberate AI positioning tradeoff, not behavior-identical acceleration.
- Optional nearby destination limits default OFF (20 destinations / 12 tiles when ON).
- Restores modified template flags and candidate limits on mission preparation,
  strategy/title transitions and unload. Native pathfinding/player movement untouched.
- Settings are under **AI Performance**. Old Larger Maps AI toggles are not imported;
  explicitly disable the new toggle there for an OFF comparison, then restart MENACE.
- No update loop or per-unit logging. Larger Maps still owns map generation, conditional
  advance recovery and optional diagnostic profiling.

Managed fixture checks exercise production task/planning/restore code. Metadata-only
checks inspect current game proxies before canonical staging; they do not execute IL2CPP.
Windows installation, native dispatch and real battle timing acceptance are separate.
Issues: #93 (extraction), #57 (late-round slowdown).
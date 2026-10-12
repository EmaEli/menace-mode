# MEN-001 Smoke Overhaul 0.6.1 — targeted smoke in-game verification

Status: source patch only. No Windows build or gameplay verification yet.

1. Install with the single MEN-001 one-click CMD while MENACE is closed; confirm compiled DLL AssemblyVersion 0.6.1.0 and rollback/backup paths.
2. Keep EnableAISmokeUsage, SimpleAISmokeRules, AllowSmokeWhilePinnedDown enabled. DebugLogging OFF for normal play.
3. Test pirate infantry visibly Suppressed and Pinned Down with smoke equipment on free adjacent tiles. Watch actual smoke throw, remaining grenade charges and two-round AI cooldown.
4. Test exposed AI flamethrower with known opponents: smoke 1–2 tiles ahead; subsequent movement remains vanilla.
5. Ensure no forced smoke for asleep AI, units without smoke, already smoked tiles, covered flamethrowers or occupied targets.
6. Test normal and pinned player grenade AP; frag and weapons must not become free.
7. Compare AI turns against 0.5.2 on 64x64 and 96x96; no major regression or per-event logging.
8. If no enemy smoke: enable Smoke Overhaul Debug for ONE battle, read aggregate counters in Dev Console and Latest.log. Debug OFF afterward.
9. Do not close issue until real smoke launch and performance verified.
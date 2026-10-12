# MEN-001 Smoke Overhaul 0.6.2 — targeted smoke in-game verification

Status: source patch only. No Windows build or gameplay verification yet.

1. Install with the single MEN-001 one-click CMD while MENACE is closed; confirm compiled DLL AssemblyVersion 0.6.2.0 and rollback/backup paths.
2. Keep EnableAISmokeUsage, SimpleAISmokeRules, AllowSmokeWhilePinnedDown enabled. DebugLogging OFF for normal play.
3. Position enemies at N/E/S/W and diagonals, including units facing AWAY from the player: every forced smoke landing tile should be closer to its chosen known enemy than the throwing unit. Verify recent detected attackers receive reasonable priority, but an unseen opponent is never targeted from hidden position.
4. Test pirate infantry visibly Suppressed and Pinned Down with smoke equipment on free adjacent tiles. Watch actual smoke throw, remaining grenade charges and two-round AI cooldown.
5. Test exposed AI flamethrower with known opponents: smoke 1–2 tiles toward a currently detected opponent; subsequent movement remains vanilla.
6. Ensure no forced smoke for asleep AI, units without smoke, already smoked tiles, covered flamethrowers or occupied targets.
7. Test normal and pinned player grenade AP; frag and weapons must not become free.
8. Compare AI turns against 0.5.2 on 64x64 and 96x96; no major regression or per-event logging.
9. If no enemy smoke: enable Smoke Overhaul Debug for ONE battle, read aggregate counters in Dev Console and Latest.log. Debug OFF afterward.
10. Do not close issue until real smoke launch and performance verified.
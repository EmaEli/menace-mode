# 1.6.1 Verification: template lookup fix

1. Build/deploy from this authoring archive (Windows, game version v0.7.15+21883) and restart MENACE. Set `TacticalMapSize=80`, `SimplifyCombatAIPlanning=ON`, `UseLocalTacticalRouting=OFF`.
2. **Startup-only gate:** check `MelonLoader/Latest.log` for `simplified large-map combat planning: N attack / M buff skills adjusted` with N > 0. If absent or a warning reports zero eligible skills, **STOP**: there is no reason to run the AI benchmark; share that startup log.
3. If enabled, check normal player move/attack, then observe one or two enemy activations with AI profiling OFF initially to reduce overhead. The tradeoff is fewer AI searches for future firing spots; attacks are still possible from valid current positions.
4. If the AI seems passive, set `SimplifyCombatAIPlanning=OFF` and restart, or restore v1.4.13. No cached paths and no changes to native `FindPath` are included.
5. If performance remains slow with successful startup, turn on `DiagnoseAIPerformance` and `DiagnoseAIPathfinding` for **one brief controlled comparison** using the same seed and units. Both diagnostic toggles are OFF by default.

The only expected behavior difference versus v1.6 is that its previously inactive simplified combat AI feature now actually activates. None of the checks above constitutes a runtime performance guarantee.

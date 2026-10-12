# Larger Maps 1.4.4 — verify territory and deployment zone alignment

**Experimental, source-only until built and tested in MENACE.** Before testing, keep the 1.4.3 binaries and backup the save. Check MENACE game version v0.7.15+21883.

1. Close MENACE, extract this ZIP, run `build-and-deploy.bat`, then start MENACE. In Mod Options keep your desired map size; restart after changing it.
2. Open *Counterinsurgency / Repel Counterattack*. Its primary COP outpost setpiece is 20x22 tiles. The previous 1.4.3 cyan rectangle was small and offset. In 1.4.4, the **cyan player deployment area** should cover the compound and be centered on it; the **DEFEND objective scoring area** should likewise cover the right part of the outpost, not only one side.
3. Check `Latest.log`: expect `primary-compound deployment scaling active` and `primary-compound territory scaling active` after a relevant mission is prepared. If either logs an unavailable-primary warning, do not assume the new zone is correct; share the log.
4. Deploy units near both sides of the blue region; ensure the UI accepts valid positions. Start the mission. Confirm DEFEND preview/ticket scoring uses the same highlighted boundary as the mission prep. Check enemy reactions and the round counter.
5. Test 80x80 first, then 96x96. Afterward try 64,72,88 (restart every time). For a 20x22 primary setpiece, target areas (before map-edge clamp): 20x22, 22x25, 25x28, 28x30 and 30x33 respectively.
6. Test a different mission with **two separate primary compounds**, and check that each region stays anchored to the correct building group, rather than drawing a single huge rectangle through the empty space between them.
7. Verify evacuation, sabotage, rescue, scripted triggers, enemy initial spawns and reinforcements on a representative mission. They should behave as 1.4.3, not automatically cover the whole base.
8. Check `Player.log` and `Latest.log` for repeated spawn failures, `IndexOutOfRangeException`, AI timeouts and native pathfinding corruption. The 1.4 branch remains experimental.

**Known limitation:** This depends on Unity IL2CPP exposing `MapBlueprint.Chunks`, `ChunkBlueprint.Primary` and `GetBounds()` correctly through the provided interop wrappers. Source-only tests validate shapes and calculations, not that runtime lookup. On failure, the original 1.4.3 behavior is deliberately preserved and the log warns once.

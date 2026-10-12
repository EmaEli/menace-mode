# Next migration steps

The unified repository now contains eight isolated feature modules. Suite 0.8 adds Cover Destruction Rebalance 0.2 without merging its gameplay logic into Building Combat Rules.

## Current validation target

- Build all eight standalone packages from this tree.
- Build and deploy MENACE Gameplay Suite 0.8.
- Confirm the log shows all eight plugin DLLs discovered exactly once.
- Confirm Cover Destruction Rebalance emits one successful dynamic-scan summary and no per-skill spam.
- Confirm Assault, Overwatch, Coordinated Fire, Field Logistics, Stronger Cover and Veterancy retain their prior behavior.
- Confirm Building Combat Rules still prevents light weapons from damaging structures.
- Check that Garrisonable Buildings no longer floods stale `Actor.GetCurrentProperties()` warnings and that occupied-building destruction does not crash.

## Next feature migration

**Weapon Ranges Rebalanced 2.0.1** is the preferred next low-risk import after Suite 0.8 runtime validation. It is already mature, has no manifest overlap with Assault/Field Logistics, and can remain an isolated DLL with its JSON override file preserved. Before importing it, keep its current verbose range table behind Debug and OFF by default.

**Garrisonable Buildings** remains the next high-value integration after its stale-actor/collapse path is proven clean in runtime. It is more tightly coupled to containment/pathfinding, so it should not be folded into the suite merely to reduce the standalone mod count.

Keep **Larger Maps** outside the suite for now because its map-generation/pathfinding footprint is much broader than the gameplay modules above.

## Safe shared code

Move only well-proven duplication into `src/Shared`, starting with IL2CPP object identity and player-ownership compatibility. Build and test after each extraction.

## Common tactical event surface

After the low-level helpers are stable, inventory which MENACE methods are patched by more than one of our mods. Where semantics truly overlap, introduce shared event dispatchers so one Harmony hook can fan out to multiple feature subscribers.

Do this gradually. Shared tactical hooks are valuable, but a bad common hook can break multiple mods at once.

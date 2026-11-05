# Infantry Sandbag Cover 0.3.0

Experimental, opt-in package for [issue #89](https://github.com/EmaEli/menace-mode/issues/89). The player's named infantry
squads and player local-forces infantry receive **Deploy Sandbags**, an 80 AP active action.
Use it on **different tactical rounds**, without moving, to construct MENACE's native sandbag
half cover on the squad's occupied tile. **Construction turns** in **Infantry Sandbag Cover**
in MenaceModSettings selects 1–10 work rounds (**default 2**). Each action costs **80 AP**;
the default therefore costs 160 AP over two rounds. Settings changes affect new sites.

An amber tile border and **Sandbags 1/2** label show unfinished work. The border uses the same
independent dual-LineRenderer pattern as mortar ranging, without taking native marker channels.
Movement cancels progress, including moving away and back. A squad that already moved this
round cannot build. Embarking, death and mission/scene changes clear unfinished work. Work is
owned by its builder, not transferable to another squad. Skipping a work round while remaining
stationary pauses progress. Like mortar ranging, unfinished progress is session-local, not saved.

Marker implementation reference: `code/ConstructionOverlay.cs` mirrors the mortar overlay in
`../../GameplaySuite/src/WeaponMechanicsRebalanced/Plugin.cs`: non-generic IL2CPP component
creation, one renderer per child GameObject, a dark outer border plus colored inner border,
terrain-relative world coordinates and explicit cleanup. Do not reuse TileHighlighter's
shared delayed-AoE channels; doing so can hide native Supply Drop markers.

No cover or native environment-clearing effects occur until the final action. Native placement
handlers are retained; failed final placement refunds that action's AP and keeps earlier work.
There is no target selection or crate projectile. Enemy infantry and vehicles receive no action.

Requires Jiangyu 1.4.10 and MenaceModSettings >= 0.4.1. The canonical standalone installer can
validate/reuse the installed companions without replacing either. Excluded from the default Suite.
Managed runtime/marker tests and metadata-only native checks do not prove IL2CPP dispatch or
visible in-game rendering; Windows install and in-game acceptance are separate checks.
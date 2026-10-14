# Forest Traversal — Jiangyu PoC (Issue #30)

Data-only port of the legacy standalone `mods/GameplaySuite/packaging/standalone/ForestTraversal` (Modpack Loader 0.1.2) to Jiangyu.Loader. No C# code: the legacy Harmony `Structure.Create` prefix and the `OnUpdate` retry loop only re-applied the same two template fields at runtime, which Jiangyu's loader applies itself before `OnTemplatesApplied`.

Status: **unverified in game**. The legacy releases are untouched; do not ship this alongside the legacy ForestTraversal modpack (both write the same two fields).

## Pinned versions

| Component | Version |
| --- | --- |
| MENACE | 0.7.15 |
| MelonLoader | 0.7.3 (Jiangyu requirement) |
| Jiangyu Loader / CLI | v1.4.8 |
| .NET | 6 runtime (loader), 10 SDK (CLI) |

## Known functional gap vs legacy

The legacy mod exposed two in-game options (`EnableDenseForestTraversal`, `DebugLogging`). Jiangyu has no per-mod settings surface; the PoC is always-on and silent. Toggle = install/remove the mod folder.

## Windows verification (to run)

```powershell
# 1. Prerequisites: MelonLoader 0.7.3, Jiangyu.Loader.dll in <game>\Mods\, jiangyu CLI on PATH
jiangyu assets index
jiangyu templates index
# 2. From this folder
jiangyu compile --release
jiangyu templates inspect --type EntityTemplate --name environment_backbone_forestdense --output text --with-mod .
jiangyu deploy
# 3. Launch MENACE, then check MelonLoader\Latest.log for the "Template self-check" line and no
#    "Override template patch" / "waiting on" warnings for environment_backbone_forestdense.
# 4. In a tactical mission, order infantry into Dense Forest (expect 30 AP enter / 30 AP leave, vehicles blocked).
```

Record installed versions, `Latest.log` excerpts and the in-game result in Issue #30 before closing it.

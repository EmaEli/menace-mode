# Forest Traversal — Jiangyu PoC (Issue #30)

Data-only port of the legacy standalone `mods/GameplaySuite/packaging/standalone/ForestTraversal` (Modpack Loader 0.1.2) to Jiangyu.Loader. No C# code: the legacy Harmony `Structure.Create` prefix and the `OnUpdate` retry loop only re-applied the same two template fields at runtime, which Jiangyu's loader applies itself before `OnTemplatesApplied`.

Status: **verified in game (2026-10-14)** on MENACE v0.7.15+21883, MelonLoader 0.7.4-ci.2607, Jiangyu.Loader 1.4.8, alongside Menace Modpack Loader v38.0.5: loader report `[ForestTraversal] 0.2.0: 2 patch ops`, self-check `all 2 patch op(s) matched the current game`, infantry entered Dense Forest (see Issue #30, run 3). Vehicle-blocked check and mod-removed counter-proof not run. Note: the loader reads only `templates.json` next to `jiangyu.json`; copying this source folder into `Mods\` yields `no content`. `jiangyu compile --release` (CLI 1.4.8 built from source on macOS, validated against the installed game's `Assembly-CSharp.dll` + `globalgamemanagers`) passed with 0 errors. The packaged build is committed at `dist/ForestTraversal-0.2.0.zip` (sha256 `60edf02e6fd16a18a0b8cc80807b1d8261729b5fa7f3ee9e9ebfc2bc748e48ec`; contents `ForestTraversal/jiangyu.json` + `ForestTraversal/templates.json`). Extract it into `<MENACE>\Mods\` so you get `Mods\ForestTraversal\`. Build intermediates (`.jiangyu/`, `compiled/`) are gitignored. The legacy releases are untouched; do not ship this alongside the legacy ForestTraversal modpack (both write the same two fields).

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

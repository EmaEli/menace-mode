# MENACE Mods

Source snapshots of MENACE mods maintained for issue tracking and ongoing development.

## Included projects

| Directory | Imported version | Files | Notes |
|---|---|---:|---|
| [`mods/GameplaySuite`](mods/GameplaySuite) | **0.32.1** | 145 | Unified authoring repository with C# sources, build/deploy scripts, packaging, tests and documentation |
| [`mods/LargerMaps`](mods/LargerMaps) | **1.6.1** | 57 | Map scaling, AI planning/performance code, build/deploy scripts, regression tests and technical notes |
| [`mods/ThirdPersonCamera`](mods/ThirdPersonCamera) | **0.3.3** | 14 | Source-only camera mod with automatic and manual cinematic views, Mod Settings bridge, Windows build scripts and static regression tests |

**Baseline imported:** October 12, 2026, from the latest source ZIP archives provided in this ChatGPT MENACE project. These are source snapshots, not independently verified upstream releases.

## Windows installer policy (mandatory)

All MENACE installers must follow the [one-click installer standard](docs/INSTALLER_STANDARD.md). For every new chat or contributor, [AGENTS.md](AGENTS.md) requires reusing the existing launcher and build/deploy workflow rather than inventing another kit. A normal installation should be one double-click with pinned source, automatic build and deployment, safe backup/rollback, a persistent console, and a saved log that opens automatically on failure. Build-only is an explicit optional mode, **not** the default.

## Build / testing

Follow each project's own instructions:

- Gameplay Suite: [README](mods/GameplaySuite/README.md) (shared sources; each module is built as a Jiangyu package from [`mods/jiangyu/<Module>`](mods/jiangyu)), [Testing](mods/GameplaySuite/docs/TESTING.md).
- Larger Maps: [README](mods/LargerMaps/README.md) (sources; built as the Jiangyu package [`mods/jiangyu/LargerMaps`](mods/jiangyu/LargerMaps)), [Tests](mods/LargerMaps/run-tests.bat).
- Third Person Camera: [README](mods/ThirdPersonCamera/README.md), [Build script](mods/ThirdPersonCamera/build-and-deploy.bat), [Test plan](mods/ThirdPersonCamera/TEST-PLAN.md).

Compiling requires local game dependencies and the intended MENACE/MelonLoader environment; game assemblies and extracted game assets are **not** included in this repository.

## Tracking

Use [GitHub Issues](../../issues) as the source of truth. Keep issue evidence and test status up to date, and close issues only after verification. Refer to [AGENTS.md](AGENTS.md) for the project workflow, logging policy and versioning rules.

## Source control

Track source, scripts, mod manifests, tests and documentation here. Keep compiled DLLs, game files, local logs, save files and transient build outputs out of Git. Use Git history for successive changes rather than storing a new authoring ZIP for each revision.

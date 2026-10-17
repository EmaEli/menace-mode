# Offline regression checks

From the repository root:

- Python source-wiring and data-model checks: `python3 -m unittest discover -s mods/GameplaySuite/tests -v`.
- Execute actual HUD pointer-capture, leader-name resolver and settings/cover-contract C# sources:
  `dotnet run --project mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj -c Release`.
- The runner also executes the production optional cinematic-camera bridge with a fake clock
  and a dynamically loaded managed camera contract: absent/delayed camera, repeated entry/exit,
  immediate state changes, throwing getter and recovery (15 assertions). This is not a game test.
- After compiling the catalogued Jiangyu packages and camera, append `-- .` to the C# command to also
  validate source/project/compiled manifests, actual DLL file/assembly versions, dependency
  minimums, pinned payload SDK and binary references. MenaceModSettings must keep assembly
  identity 1.0.0.0; Jiangyu DLLs must not retain binary ModpackLoader references.
- The same runner executes production Coordinated Fire fee/rule helpers across 2,072
  positive-grant/cap scenarios, plus weapon-eligibility guards. This tests the actual
  arithmetic, not native AP hooks, overlap timing or live Berserk events.
- BuildKit ZIP/clean-source checks: see [INSTALLING.md](../docs/INSTALLING.md). The Python
  distribution tests exercise actual ZIP generation/integrity and inspect PowerShell wiring;
  they do **not** execute PowerShell, installer filesystem transactions or Windows rollback.
- Mission-feedback checks execute the actual Assault defensive policy (100 cases
  inside the offline runner), plus extracted production building/tree guards (86)
  and camera framing methods (99) against managed stand-ins. The latter two use the
  installed .NET 10 SDK and skip explicitly if absent; no external packages needed.
  They do not test native damage dispatch or Unity rendering/smoothing.
- Private log aggregates: `python3 mods/GameplaySuite/tools/analyze_mission_log.py`
  followed by the private log path. It emits allowlisted counts/identifiers only,
  not raw messages, settings or actor/save names. Quiet modules are not action tests.

The runner targets .NET 10 (also used by the local Jiangyu CLI), has no external packages
and does not load game assemblies. Name tests use managed stand-ins for native localized
fields. The K-PAC fixture contains only relevant fields from the available asset export.
Settings/cover checks use production classes with an in-memory persistence stand-in;
they do not write user preferences or execute native template restoration. Migration
source-wiring checks are explicitly distinct from actual Windows/game/save tests.

These checks do not execute native IL2CPP methods, install Harmony patches, prove click
interception or validate live reaction fire. Follow [the in-game checklist](../docs/TESTING.md)
separately. Build outputs are not an installer or evidence of Windows deployment.
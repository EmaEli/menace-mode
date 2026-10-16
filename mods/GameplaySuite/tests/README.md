# Offline regression checks

From the repository root:

- Python source-wiring and data-model checks: `python3 -m unittest discover -s mods/GameplaySuite/tests -v`.
- Execute the actual HUD pointer-capture and leader-name resolver C# sources:
  `dotnet run --project mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj -c Release`.
- After compiling the catalogued Jiangyu packages and camera, append `-- .` to the C# command to also
  validate source/project/compiled manifests, actual DLL file/assembly versions and guard
  dependencies. MenaceModSettings must keep assembly identity 1.0.0.0.
- BuildKit ZIP/clean-source checks: see [INSTALLING.md](../docs/INSTALLING.md). The Python
  distribution tests exercise actual ZIP generation/integrity and inspect PowerShell wiring;
  they do **not** execute PowerShell, installer filesystem transactions or Windows rollback.

The runner targets .NET 10 (also used by the local Jiangyu CLI), has no external packages
and does not load game assemblies. Name tests use managed stand-ins for native localized
fields. The K-PAC fixture contains only relevant fields from the available asset export.

These checks do not execute native IL2CPP methods, install Harmony patches, prove click
interception or validate live reaction fire. Follow [the in-game checklist](../docs/TESTING.md)
separately. Build outputs are not an installer or evidence of Windows deployment.
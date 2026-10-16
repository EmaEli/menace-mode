# MENACE Windows installer standard

**Status:** Mandatory for every new or updated MENACE mod installer and for the Gameplay Suite.
**Audience:** Developers and AI assistants working on this repository, including in new chats.
**Rule:** Read this document and the root [AGENTS.md](../AGENTS.md) **before** creating or modifying an installer. Reuse the established implementation; do not invent another workflow or ask the player to perform avoidable manual steps.

## Expected user experience

The default deliverable is a **single Windows double-click launcher** (`INSTALL-<ModName>.cmd` or `INSTALL-SUITE.cmd`) that performs the complete sequence:

1. Locate the MENACE game and verify MelonLoader, ModpackLoader and local IL2CPP references as appropriate. Prompt for the game location only if discovery fails. Check that the game is closed before deploying. Bootstrap the pinned private build SDK automatically; do not require a manual SDK pre-install.
2. Download public source from a **pinned Git commit**, without Git authentication, or use explicitly bundled source. For online bootstrap scripts, verify the pinned installer with SHA-256 before execution. Avoid silently fetching floating `main`.
3. Compile **all required components**, including camera/settings companions or other dependencies belonging to one logical mod/suite, without modifying live `Mods` yet.
4. Validate **actual build artifacts**: DLL presence, DLL assembly version, modpack manifest version, required runtime/settings dependencies, and intended directory structure. C# projects must emit the requested `AssemblyVersion` (`GenerateAssemblyInfo=true` or an explicit assembly attribute). A successful `dotnet build` alone is not enough.
5. Stage **one logical installable distribution**, even if a MelonLoader entry-point must technically remain a DLL in `Mods` while its settings plugin belongs to a Suite modpack. Do not require the player to find, assemble or install two ZIPs manually.
6. Make a **timestamped backup outside `Mods`** of the precise versions/files that will be replaced. Remove or relocate only known conflicting duplicates. Never delete other unrelated mods, user settings, saves or backups.
7. Install the staged files automatically. Verify files and versions on disk; on failure, attempt rollback and report any restoration errors without claiming the game is fixed.
8. Print an unambiguous **SUCCESS** or **FAILED** summary, the installed versions, and where logs/backups are stored. In-game acceptance testing remains a separate step; do not say a fix works until tested in MENACE.

If the user explicitly requests build-only, offer that as an opt-in switch; **do not default to build-only** or leave installation as a hidden manual step.

## Non-negotiable diagnostics and console behavior

- A double-click must **not lose the error when the process exits**. Prefer a persistent wrapper (`cmd.exe /k`) so the console stays visible after success **and** failure, until explicitly closed. Do not rely on `pause`: pressing a key can close the window.
- From the first bootstrap/download step, write a complete diagnostic log to a **stable, predictable location** (for example `%TEMP%\MENACE-<ModName>-install.log`), including downloads, all build output, validation and deployment/rollback errors. Keep the path visible in the window.
- On any failure, **automatically open the log in Notepad**. No scavenger hunt in random temp folders, screenshots of vanishing windows, or requirement to rerun just to capture an error.
- Log errors and build warnings to the *installer file*, not to gameplay per-frame logs. Runtime mod logging remains silent/minimal by default, with optional Debug OFF.
- Print actionable error messages, not only an exit code. On installation failure, state whether rollback succeeded.

## Reuse rather than reinvent

Before writing new installer code:

1. Read `AGENTS.md` and this standard.
2. Inspect the existing **one-click MEN-006 implementation** and **reuse its launcher/pinned-download/backup/staging/error-reporting pattern**, correcting defects in the shared approach rather than creating a different installer for each chat:
   - Launcher: [`mods/GameplaySuite/INSTALL-MEN006-ONECLICK.cmd`](https://github.com/EmaEli/menace-mode/blob/fix/men-006-cinematic-hud/mods/GameplaySuite/INSTALL-MEN006-ONECLICK.cmd).
   - PowerShell build/deploy: [`mods/GameplaySuite/tools/Install-IntegratedSuite.ps1`](https://github.com/EmaEli/menace-mode/blob/fix/men-006-cinematic-hud/mods/GameplaySuite/tools/Install-IntegratedSuite.ps1).
   - These links point to the **draft PR #8** implementation until merged. It is a reference implementation, **not yet confirmed Windows/in-game stable**. Validate and fix it before treating it as a tested template.
3. Prefer extending an existing `Build.ps1` and shared launcher pattern over producing one-off `v2/v3/v4` kits, separate build scripts or manual post-build copy instructions.
4. Keep configurable values (repo, pinned commit, manifest/DLL versions, mod directories) centralized and update the launcher and bootstrap hash when the PowerShell source changes. Verify the published GitHub commit, hash and packaged/downloadable launcher match.
5. When a bug is fixed in the installer workflow, **update this standard and the canonical example**, so later sessions inherit the correction.

## Private .NET SDK bootstrap (#34, 15 October 2026)

The canonical installer must install its build SDK **as its first build step**, after
source/helper integrity checks and before any build or live mod move. The user does
not install an SDK manually. This toolchain is separate from MelonLoader's game runtime;
package code remains `net6.0` and the installed machine-wide SDK 6 remains untouched.

- Current pin: **10.0.113**, the highest stable `10.0.1xx` returned by Microsoft's
  `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json` when checked.
  Exact pins live in `tools/distribution.json` and are copied into `kit.json.dotnetSdk`;
  the installer rejects a disagreement between those two records.
- Windows x64 SDK archive SHA512:
  `4db6d0a3d91dedaaf2ce9bb3d4648e833bb8966648c1fb271c10d344a86f05ec8b811cca120049fa06cf1397b03ade17285b4f0600d2d484a3a1e1dae14cc2dc`.
- Microsoft `dotnet-install.ps1` is pinned to install-scripts commit
  `e5cf1dd2d1540ed05ac84f8eb8c5cdec2807621e`, SHA256
  `3bb07bc8025211836c1e4f9d3f6a044e55b1fb6eec518a6c78851d04e210442b`.
- Install under `<kit>\tools\dotnet`, with `-Channel 10.0 -Version <pinned> -NoPath`.
  Verify the script SHA256 and archive SHA512 **before execution/extraction**. The
  Microsoft script normally downloads without that SHA512 gate: supply its supported
  filesystem `-AzureFeed` containing only the already verified archive, not an online feed.
- Check disk space before downloading (currently 2 GiB allowance for the installed SDK,
  archive and extraction copy). Reuse the matching private SDK without downloading again.
  Run that private `dotnet --version`, log it, and fail if it is not the pin.
- Set `DOTNET_ROOT`, `DOTNET_ROOT_X64`, prepended `PATH`,
  `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`,
  `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` and `DOTNET_MULTILEVEL_LOOKUP=0` for the
  **installer process only**. Keep CLI home/NuGet caches private too, restore the
  original process environment in `finally`, and build from fresh bundled sources.
- Never use an MSI, write `C:\Program Files\dotnet`, change User/Machine environment
  variables or create `global.json` in the game folder. Download/hash/extraction messages
  belong in the advertised persistent installer log from the first bootstrap operation.
- Manual fallback **only after an automatic-bootstrap failure**: the same pinned,
  verified Microsoft script into `%LOCALAPPDATA%\Microsoft\dotnet` with `-NoPath`;
  never the MSI. Do not replace normal automatic installation with these manual steps.

`mods/GameplaySuite/tests/Test-SdkBootstrap.ps1` executes the production shared helper
for hash rejection, disk guard, cleanup and optional real Windows install/idempotence
checks. It is a test harness, not a second installer. Full Windows deployment, rollback
and gameplay acceptance remain separate gates.
On the game PC, PowerShell 5.1 passed 36 behavior checks, including real pinned download,
SHA256/SHA512 checks, extraction/version, repeat-with-network-blocked idempotence and
unchanged User/Machine environment and machine-wide SDK list. This validates **only the
SDK bootstrap**, not the complete installer or game compatibility.

## Version-preflight regression guard (MEN-001, 12 October 2026)

A smoke installer rejected valid Smoke Overhaul 0.6.2 **before compilation** because an old literal `<Version>0.6.1</Version>` remained in `Install-Component.ps1`. The standalone manifest and real project both declared 0.6.2. This is an **installer-only** error, and no live game files should have changed.

For **every** installer, validate source C# project versions by parsing the `.csproj` XML and comparing the **actual value** against the requested/manifest version. Do not embed a second hard-coded project version in a PowerShell regex: it can be left behind on release bumps. For example, following the updated shared component installer:

```powershell
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode) { throw "Missing project Version: $project" }
$actualProjectVersion = $versionNode.InnerText.Trim()
if ($actualProjectVersion -ne $ExpectedVersion) {
    throw "Project version mismatch: $actualProjectVersion != $ExpectedVersion"
}
```

The assembly check must separately verify real compiled DLL metadata, not project text. Derive the expected assembly version from a centrally defined expected version (or explicitly scoped metadata convention) instead of duplicating a stale release literal. **Run regression assertions that parse actual manifests and project files and inspect the installer preflight before pinning a release.** The reusable reference now includes `mods/GameplaySuite/tools/Install-Component.ps1` and a regression test (`mods/GameplaySuite/tests/test_installer_versions.py`) in the MEN-001 branch. Preserve the MEN-006 example for integrated Suite+Camera packaging.

## Acceptance checklist (required before delivery)

### Current shared Jiangyu candidate (#34)

`mods/GameplaySuite/INSTALL-SUITE.cmd` and the canonical
`tools/Install-IntegratedSuite.ps1` now implement the MEN-006 pattern with explicitly
bundled, SHA256-inventoried sources. All products use the same installer and independently
versioned packages; no floating Git download or second camera installer is needed.
`distribution.json` centralizes the supported native game hash and Jiangyu 1.4.8 asset pins.
The Jiangyu SDK comes from the hash-verified CLI archive; code is built directly with .NET against
installed game proxies. Authored templates/bundles are precompiled for the pinned game,
so the player does **not** need Unity Editor or a global Jiangyu configuration change.

Regression guards: pad three-part versions before asking `.NET Version.ToString(4)`;
reject renamed/ambiguous duplicates rather than moving unrelated folders; preserve the
legacy WRR override inside staging for its existing runtime migration; journal partial
copies and retain an in-memory journal for automatic rollback even if writing the journal
fails. Rollback moves rejected files outside Mods and retains original backups, verifies
hashes, and refuses manual rollback over changed live files. No live recursive deletion.
Require an explicit, unique inventory entry for each installer/helper before executing it;
an empty hash list must not bypass bootstrap validation. Reject extra uninventoried source
files (especially MSBuild props/targets), and recheck that MENACE is closed at deployment
and rollback, not only before a potentially lengthy build.

The scripts are a **candidate**, not Windows-verified. Console/log behavior, real filesystem
failure recovery, Windows builds and gameplay remain explicit acceptance gates.

- [ ] Launcher is one double-click action, not a sequence of ZIPs and commands.
- [ ] Download/bootstrap errors, compilation errors and version mismatches leave a readable, persisted log; failure opens it automatically.
- [ ] Console stays open if the user clicks or presses a key.
- [ ] Builds do not change live files until **all** modules pass validation.
- [ ] Generated assembly versions match manifest versions, including the camera/companion when bundled.
- [ ] Installation backs up only targeted files outside `Mods` and attempts rollback on failure.
- [ ] No duplicate standalone + Suite plugins are left enabled; unrelated mods and user data remain untouched.
- [ ] Installed on-disk versions and paths are checked; the startup log should be verified in a later gameplay test.
- [ ] ZIP/package integrity and script behavior are checked. If there was **no actual Windows build or gameplay test**, say so explicitly.

## Historical failure to avoid

MEN-006 initially supplied two separate build ZIPs without deploying either one: the game still loaded old camera/Suite versions. A later integrated installer correctly built the DLLs but stopped on `Third Person Camera DLL version mismatch: 0.0.0.0, expected 0.3.4.0` because the C# project disabled assembly metadata generation. Finally, the old `pause` launcher let its window disappear on keypress. These are installer regressions; do not repeat them.

This policy applies to **all MENACE mods and future conversations**, not just issue #7.

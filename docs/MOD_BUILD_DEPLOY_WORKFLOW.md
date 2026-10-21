# MENACE mod build, PC test and Nexus hand-off

**Read this before every MENACE build, PC deployment or Nexus package/metadata update.**
Last checked: 20 October 2026. Paths, game versions and release status can change; verify
them again before acting. This is a workflow, not permission to deploy or publish.

## Branches and practical acceptance

- Only `main` and `dev` are active development branches. Work and commit on `dev`;
  promote the complete user-accepted snapshot to `main`. No per-issue branches or kits.
- Normal gameplay feedback from the user is sufficient for the scope they accept.
  Agents run relevant automated checks; log analysis is for a specific investigation,
  not a task the user must request after every play session. No universal QA matrix.
- Record known bugs and unperformed checks honestly. They do not automatically block
  unrelated accepted source from integration. The installer/public Nexus cutover in
  #34 remains separate from acceptance of the personal playable baseline.
- See [the consolidation record](CONSOLIDATION_2026-10-20.md) for the initial snapshot
  and the distinction between source versions and independently verified PC files.

## 1. Start with issues and the working tree

1. Read [`AGENTS.md`](../AGENTS.md), [`INSTALLER_STANDARD.md`](INSTALLER_STANDARD.md),
   and the current [installer candidate instructions](../mods/GameplaySuite/docs/INSTALLING.md).
   For tests, see the [offline-check guide](../mods/GameplaySuite/tests/README.md).
2. Review open issues and search by mod/symptom before editing. MENACE's canonical tracker
   is [EmaEli/menace-mode](https://github.com/EmaEli/menace-mode). Always read #34 and its
   latest comments before packaging; also read the relevant feature issue and linked parent.

   ```text
   gh issue list --repo EmaEli/menace-mode --state open
   gh issue list --repo EmaEli/menace-mode --search "<mod or symptom>"
   gh issue view 34 --repo EmaEli/menace-mode --comments
   ```

   As of this check #34 is still **open**. Its 16 October note records focused testing of
   three modules, not completion of the public installer/release work. Do not
   reuse this status snapshot without rereading the issue.
3. Check `git status --short` and `git branch --show-current` before changing branches,
   building or packaging. Preserve all staged, unstaged and untracked work. Follow
   `AGENTS.md` to synchronize `dev` with current `main` before substantial work; never use
   `reset --hard`, `clean`, force-push or branch switching to discard someone else's work.
4. Confirm whether the target is a current Jiangyu package or a legacy ModpackLoader module.
   `mods/GameplaySuite/tools/distribution.json` is the catalog: stable `modules` and
   `experimentalModules` are deliberately separate. Check the manifest, `.csproj` version,
   compiled output, dependencies and migration notes; never infer version/status from an
   old ZIP or old section in `mods/GameplaySuite/docs/TESTING.md`.

## 2. Build and verify a candidate

- Use the pinned game/toolchain information in `distribution.json` and the current installer
  standard. A game patch means re-check the PC's `GameAssembly.dll` hash and fresh
  `MelonLoader/Il2CppAssemblies` before building. Do not silently build against stale refs.
- Update the module's source, manifest and project version consistently. Run the existing
  Python regression checks from the repository root. When a version changes, rebuild or
  refresh the matching `compiled/` manifest/assets and update the package inventory in
  `docs/MIGRATION_STATUS.md`; both are checked against the source version. On this Mac,
  `dotnet` is not on `PATH`, so use the existing user-level SDK as shown below:

  ```text
  python3 -m unittest discover -s mods/GameplaySuite/tests -v
  "$HOME/.dotnet/dotnet" run --project mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj -c Release -- .
  ```

  The second command's `-- .` asks the runner to validate package manifests, actual DLL
  metadata, dependencies and binary references; compile the catalogued packages first.
  Offline checks do not prove Windows installation or in-game behavior.
- Build an inventoried **installer candidate** with a new output name (the builder refuses
  to overwrite an existing file). For the full suite:

  ```text
  python3 mods/GameplaySuite/tools/build_kit.py --output .tmp/MENACE-Suite-candidate-YYYYMMDD.zip
  python3 mods/GameplaySuite/tools/verify_kit.py .tmp/MENACE-Suite-candidate-YYYYMMDD.zip
  ```

  For one stable module, add `--module <ModuleName>` before `--output`. This makes the
  canonical standalone installer, including MenaceModSettings as required. The SFP preview
  is experimental and requires both `--module SquaddieFieldPromotions` and
  `--use-installed-settings`; follow its special checks in `INSTALLING.md`.
- `build_kit.py` packages the current source and compiled payload and runs offline checks;
  it does **not** itself compile the game DLLs or create a Nexus upload ZIP. Compile/rebuild
  package outputs with the current pinned Jiangyu/.NET workflow before creating the kit.
  Optionally use `verify_kit.py <zip> --game-dir <fresh game refs> --sdk-dir <Jiangyu SDK>`
  for a clean extracted-source rebuild in a temporary directory. That is still a local
  check, not Windows PowerShell or gameplay acceptance.

## 3. Windows PC build, deployment and gameplay check

1. Copy the candidate to a **new, clearly named staging folder**, not directly into `Mods`.
   The Windows host is `menace-pc`; use the PowerShell-file workflow in the personal
   `menace-game-pc` rule for multi-step commands. Don't inline PowerShell through `cmd.exe`.
2. Check the current game version/hash and confirm `Menace.exe` is closed. First run the
   canonical installer in explicit `-BuildOnly` mode and inspect its build/version checks.
   For a one-module test, the bundled script invocation is:

   ```text
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<bundle>\source\mods\GameplaySuite\tools\Install-IntegratedSuite.ps1" -BundleRoot "<bundle>" -Product Standalone -Modules <ModuleName> -BuildOnly
   ```

   Run it from the extracted verified kit root, using the current PC's real path. Keep the
   game closed for consistency. Build-only writes no live mod files.
3. Deploy only after the user has requested that test install and the build gate passed.
   Use the canonical launcher/default installer, not a manual partial copy: it stages all
   outputs, validates real DLL and manifest versions, backs up affected files outside
   `Mods`, guards duplicates and attempts rollback. Confirm the game is still closed.
4. Verify the install summary, `%TEMP%\MENACE-install.log`, dated
   `Menace\_MENACE_Backups\...` directory, on-disk versions and hashes. Do not infer a
   working fix from a successful build/install. Ask the user to launch the game, then pull
   the fresh `MelonLoader\Latest.log` when needed for the specific investigation.
   Report precisely what was and was not tested; update the relevant GitHub issue.
   The user's gameplay acceptance can close that scope without a comprehensive test
   plan. Do not require explicit per-feature logging after every ordinary session.

## 4. Prepare Nexus files without overwriting the old release

- A Nexus **mod ZIP** is a different artifact from the installer BuildKit. It must contain
  the correct single mod folder and be made from the exact PC-tested, hash-verified files.
  Do not upload a BuildKit ZIP as a mod ZIP.
- Each module's release folder follows the existing layout:
  `<Mod>/<Mod>-<version>.zip`, `<Mod>/description.txt`, and
  `<Mod>/CHANGELOG.txt`; the release root has `INDEX.txt`. Keep descriptions in English
  BBCode, with accurate features, requirements, install/upgrade steps and compatibility.
  Changelogs state the previous Nexus version, changes and migration notes, plus a truthful
  test-status section. Derive claims from the issue and observed test evidence, not intent.
- Stage a **new dated release directory** locally, review the ZIP contents and hashes, and
  preserve the previous release unchanged. On 19 October the known local and PC historical
  directory is `MENACE-Nexus-2026-10-16`; the PC copy is under `D:\Downloads`. Recheck the
  current Downloads path before copying. Mac `$HOME/Downloads` and Windows `D:\Downloads`
  are different locations.
- Important: `.tmp/nexus_pack.py` is a scratch script, not the canonical installer builder.
  It is hard-coded to the 16 October output and removes matching mod ZIPs in that folder
  before writing. Do not run it unchanged for a new release or over the historical bundle.
  Review its staging inputs and output target, choose a fresh destination, and verify every
  package before handoff.
- After confirming the new remote destination does not already exist, copy the completed
  directory (not a guessed individual file) from the Mac to the PC:

  ```text
  scp -q -r "$HOME/Downloads/$REL" "menace-pc:D:/Downloads/"
  ```

  Set `REL` to the new directory name. If the destination already exists, stop: verify its
  contents and make a dated backup or choose a new name before copying. Then verify the
  remote file list and SHA256 hashes against the local files with a PowerShell script.
  Do not publish to Nexus unless the user explicitly asks.

## 5. Report the outcome

Record the issue number, source revision, mod/version, game version/hash, candidate ZIP hash,
test commands/results, PC backup/log paths, installed hashes and exactly which gameplay
checks the user completed. Keep offline, Windows build, deployment, startup and gameplay
evidence distinct. A public installer/Nexus release still needs its relevant packaging
checks; #34 is not a universal gate on integrating the user-accepted playable baseline.
"""Verify a BuildKit; optionally compile its clean extracted sources locally, without installing."""
import argparse
import json
import shutil
import stat
import subprocess
import tempfile
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from build_kit import ROOT, TOOLS, digest, safe_path


def read_archive(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len({n.casefold() for n in names}):
            raise ValueError("Duplicate/case-colliding ZIP paths")
        # Private SFP audio is legitimately larger than the ordinary source kit.
        # Keep the old limit for everything else and separately bound the two bundles.
        voice_root = "source/mods/jiangyu/SquaddieFieldPromotions/runtime_assets/voice/"
        voice_names = {voice_root + name for name in
                       ("sfp_john_jane_voice.sfpbundle", "sfp_xcom_voicepacks.sfpbundle")}
        voice_bytes = sum(i.file_size for i in archive.infolist() if i.filename in voice_names)
        other_bytes = sum(i.file_size for i in archive.infolist() if i.filename not in voice_names)
        if voice_bytes > 320 * 1024 * 1024 or other_bytes > 100 * 1024 * 1024:
            raise ValueError("Unexpectedly large candidate payload")
        for entry in archive.infolist():
            if not safe_path(entry.filename) or stat.S_ISLNK(entry.external_attr >> 16):
                raise ValueError("Unsafe archive entry")
        kit = json.loads(archive.read("kit.json"))
        if kit["format"] != 1:
            raise ValueError("Unsupported inventory format")
        expected = {e["path"]: e["sha256"] for e in kit["files"]}
        if len(expected) != len(kit["files"]) or set(names) != set(expected) | {"kit.json"}:
            raise ValueError("Inventory does not match ZIP contents")
        files = {name: archive.read(name) for name in expected}
        for name, data in files.items():
            if digest(data) != expected[name]:
                raise ValueError("SHA256 mismatch: " + name)
        snapshot = digest(json.dumps(kit["files"], sort_keys=True, separators=(",", ":")).encode())
        if snapshot != kit["snapshotSha256"]:
            raise ValueError("Snapshot digest mismatch")
        return files, kit


def compile_clean(files, game, sdk, dotnet):
    # Only this fresh temporary tree is written. Game files/Mods are read-only inputs.
    with tempfile.TemporaryDirectory(prefix="MENACE-clean-build-") as folder:
        work = Path(folder).resolve()
        for name, data in files.items():
            path = work / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        source = work / "source"
        catalog = json.loads((source / TOOLS / "distribution.json").read_text())
        refs, runtime = game / "MelonLoader/Il2CppAssemblies", game / "MelonLoader/net6"
        present = {p.parent.name for p in (source / "mods/jiangyu").glob("*/jiangyu.json")}
        modules = [name for name in catalog["modules"] + catalog.get("experimentalModules", []) if name in present]
        if present != set(modules):
            raise ValueError("Unknown module in clean source tree")
        for module in modules:
            root = source / "mods/jiangyu" / module
            project = root / "code" / (module + ".csproj")
            if not project.exists():
                continue
            build_args = [dotnet, "build", str(project), "-c", "Release", "--nologo", "-v", "quiet",
                          f"-p:GameDir={game}", f"-p:GameAssembliesDir={refs}", f"-p:MelonLoaderDir={runtime}",
                          f"-p:JiangyuSdkDir={sdk}", "-p:JiangyuDev=false"]
            if module in {"SquaddieFieldPromotions", "SquaddieVeterancy", "CoordinatedFire"} and "MenaceModSettings" not in modules:
                settings_dll = game / "Mods/MenaceModSettings/code/MenaceModSettings.dll"
                if not settings_dll.is_file():
                    settings_dll = ROOT / "mods/jiangyu/MenaceModSettings/compiled/code/MenaceModSettings.dll"
                if not settings_dll.is_file():
                    raise ValueError("Installed-settings candidate has no MenaceModSettings build reference")
                build_args.append(f"-p:MenaceModSettingsPath={settings_dll}")
            subprocess.run(build_args, check=True, cwd=source)
            assembly = ET.parse(project).findtext("PropertyGroup/AssemblyName", module)
            shutil.copy2(root / "code/bin/Release" / (assembly + ".dll"), root / "compiled/code" / (assembly + ".dll"))
        if "CinematicActionCamera" in modules:
            camera = source / "mods/ThirdPersonCamera/ThirdPersonCamera.csproj"
            subprocess.run([dotnet, "build", str(camera), "-c", "Release", "--nologo", "-v", "quiet",
                            f"-p:MenaceInteropDir={refs}", f"-p:MenaceRuntimeDir={runtime}",
                            "-p:DebugType=none", "-p:DebugSymbols=false"], check=True, cwd=source)
            contracts = source / "mods/jiangyu/CinematicActionCamera/tests/NativeContractChecks/NativeContractChecks.csproj"
            subprocess.run([dotnet, "run", "--project", str(contracts), "-c", "Release", "--",
                            str(refs / "Assembly-CSharp.dll"),
                            str(camera.parent / "bin/Release/MenaceThirdPersonCamera.dll")], check=True, cwd=source)
        check_modules = list(modules)
        if "MenaceModSettings" not in check_modules:
            depends_on_settings = any(
                any(requirement.startswith("MenaceModSettings >= ") for requirement in
                    json.loads((source / "mods/jiangyu" / module / "jiangyu.json").read_text())["depends"])
                for module in check_modules
            )
            if depends_on_settings:
                settings_source = ROOT / "mods/jiangyu/MenaceModSettings"
                settings_target = source / "mods/jiangyu/MenaceModSettings"
                # ArtifactChecks distinguishes code packages from data-only packages
                # by the project file. Copy this metadata witness too, after builds;
                # it is inspected only, never built or installed as a companion.
                for relative in ("jiangyu.json", "compiled/jiangyu.json", "compiled/code/MenaceModSettings.dll",
                                 "code/MenaceModSettings.csproj"):
                    target = settings_target / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(settings_source / relative, target)
                check_modules.insert(0, "MenaceModSettings")

        runner = ROOT / "mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj"
        check_args = [dotnet, "run", "--project", str(runner), "-c", "Release", "--", str(source)]
        if modules == ["SquaddieFieldPromotions"]:
            runner = source / "mods/jiangyu/SquaddieFieldPromotions/tests/OfflineChecks/OfflineChecks.csproj"
            check_args[3] = str(runner)
        else:
            check_args.extend(["--modules", ",".join(check_modules)])
        subprocess.run(check_args, check=True, cwd=ROOT)
        print("Clean extracted-source build and real DLL metadata checks passed (local host, NOT Windows).")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--game-dir", type=Path)
    parser.add_argument("--sdk-dir", type=Path)
    args = parser.parse_args()
    files, kit = read_archive(args.archive)
    print(f"ZIP integrity verified: {len(files)} files, {len(kit['modules'])} packages.")
    if bool(args.game_dir) != bool(args.sdk_dir):
        parser.error("Supply both --game-dir and --sdk-dir for the clean local build")
    if args.game_dir:
        dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
        compile_clean(files, args.game_dir.resolve(), args.sdk_dir.resolve(), dotnet)


if __name__ == "__main__":
    main()
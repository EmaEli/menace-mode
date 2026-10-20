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
        if sum(i.file_size for i in archive.infolist()) > 100 * 1024 * 1024:
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
            if module == "SquaddieFieldPromotions":
                build_args.append(f"-p:MenaceModSettingsPath={game / 'Mods/MenaceModSettings/code/MenaceModSettings.dll'}")
            subprocess.run(build_args, check=True, cwd=source)
            assembly = ET.parse(project).findtext("PropertyGroup/AssemblyName", module)
            shutil.copy2(root / "code/bin/Release" / (assembly + ".dll"), root / "compiled/code" / (assembly + ".dll"))
        if "CinematicActionCamera" in modules:
            camera = source / "mods/ThirdPersonCamera/ThirdPersonCamera.csproj"
            subprocess.run([dotnet, "build", str(camera), "-c", "Release", "--nologo", "-v", "quiet",
                            f"-p:MenaceInteropDir={refs}", f"-p:MenaceRuntimeDir={runtime}",
                            "-p:DebugType=none", "-p:DebugSymbols=false"], check=True, cwd=source)
        runner = ROOT / "mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj"
        check_args = [dotnet, "run", "--project", str(runner), "-c", "Release", "--", str(source)]
        if modules == ["SquaddieFieldPromotions"]:
            runner = source / "mods/jiangyu/SquaddieFieldPromotions/tests/OfflineChecks/OfflineChecks.csproj"
            check_args[3] = str(runner)
        else:
            check_args.extend(["--modules", ",".join(modules)])
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
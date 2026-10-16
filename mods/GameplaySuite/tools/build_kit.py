"""Assemble the canonical bundled-source candidate. Does not install or contact Windows."""
import argparse
import hashlib
import io
import json
import re
import shutil
import subprocess
import zipfile
from pathlib import Path, PurePosixPath
from xml.etree import ElementTree as ET

TOOLS = Path("mods/GameplaySuite/tools")
ROOT = Path(__file__).resolve().parents[3]
IGNORED = {"bin", "obj", ".jiangyu", ".git", "refs", "__pycache__"}
CODE_SUFFIXES = {".cs", ".csproj", ".props", ".targets"}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(name):
    parts = PurePosixPath(name).parts
    return bool(parts) and not name.startswith("/") and "\\" not in name and ":" not in name and ".." not in parts


def collect(root):
    root = Path(root).resolve()
    catalog = json.loads((root / TOOLS / "distribution.json").read_text())
    modules = catalog["modules"]
    if len(set(modules)) != len(modules) or any(not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", n) for n in modules):
        raise ValueError("Invalid/duplicate module names")
    actual = {p.parent.name for p in (root / "mods/jiangyu").glob("*/jiangyu.json")}
    if actual != set(modules):
        raise ValueError("Distribution catalog does not cover the current modules exactly")
    payload = {}

    def add(path):
        path = Path(path)
        if path.is_symlink() or any(p.is_symlink() for p in path.parents if p != root.parent):
            raise ValueError("Symlinks are not allowed in bundled sources")
        name = "source/" + path.relative_to(root).as_posix()
        if not safe_path(name):
            raise ValueError("Unsafe bundled path")
        payload[name] = path.read_bytes()

    def tree(folder, suffixes=None):
        if not folder.exists():
            return
        for path in sorted(folder.rglob("*")):
            if path.is_symlink():
                raise ValueError("Symlink in source/payload tree")
            if not path.is_file() or IGNORED.intersection(path.relative_to(folder).parts):
                continue
            if path.name == "local.props" or (suffixes is not None and path.suffix not in suffixes):
                continue
            if path.suffix in {".pdb", ".mdb"}:
                continue
            add(path)

    versions = {}
    for module in modules:
        folder = root / "mods/jiangyu" / module
        manifest = json.loads((folder / "jiangyu.json").read_text())
        compiled = json.loads((folder / "compiled/jiangyu.json").read_text())
        if manifest["name"] != module or compiled["name"] != module or manifest["version"] != compiled["version"]:
            raise ValueError("Manifest mismatch: " + module)
        versions[module] = manifest["version"]
        project = folder / "code" / (module + ".csproj")
        if project.exists():
            xml = ET.parse(project)
            if xml.findtext("PropertyGroup/Version") != manifest["version"]:
                raise ValueError("Project/manifest mismatch: " + module)
            assembly = xml.findtext("PropertyGroup/AssemblyName", module)
            dlls = {p.name for p in (folder / "compiled/code").glob("*.dll")}
            if dlls != {assembly + ".dll"}:
                raise ValueError("Missing or unexpected compiled DLLs: " + module)
        if list((folder / "templates").glob("*.kdl")) and not (folder / "compiled/templates.json").is_file():
            raise ValueError("Missing compiled template payload: " + module)
        add(folder / "jiangyu.json")
        for filename in ("README.md", "CHANGELOG.md", "LICENSE"):
            if (folder / filename).is_file():
                add(folder / filename)
        tree(folder / "code", CODE_SUFFIXES)
        tree(folder / "templates", {".kdl"})
        tree(folder / "locales", {".po", ".pot"})
        tree(folder / "compiled")
    tree(root / "mods/GameplaySuite/src", {".cs"})
    tree(root / "mods/LargerMaps/src/LargerMaps", {".cs"})
    for filename in ("ThirdPersonCamera.csproj", "src/ThirdPersonCameraMod.cs", "README.md"):
        add(root / "mods/ThirdPersonCamera" / filename)
    for filename in ("Install-IntegratedSuite.ps1", "Install.Common.ps1", "Install.Deployment.ps1", "distribution.json"):
        add(root / TOOLS / filename)
    add(root / "mods/GameplaySuite/docs/INSTALLING.md")
    payload["INSTALL-SUITE.cmd"] = (root / "mods/GameplaySuite/INSTALL-SUITE.cmd").read_bytes().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    payload["README.txt"] = (
        "MENACE Suite - OFFLINE CANDIDATE, not Windows/game-verified.\r\n"
        "INSTALL-SUITE.cmd builds and INSTALLS all components, including the camera.\r\n"
        "Requires the pinned MENACE build, existing MelonLoader/proxies and .NET 10 SDK.\r\n"
        "Jiangyu 1.4.8 SDK/loader are downloaded with SHA256 checks; no Unity Editor needed.\r\n"
        "Known conflicting copies are backed up outside Mods; unrelated mods, loaders,\r\n"
        "UserData and saves are not removed. Failure triggers rollback.\r\n"
        "Persistent log: %TEMP%\\MENACE-install.log. See source installer documentation.\r\n"
    ).encode("ascii")
    return payload, versions


def make_archive(payload, versions, base_revision):
    files = [{"path": name, "sha256": digest(data)} for name, data in sorted(payload.items())]
    tree_hash = digest(json.dumps(files, sort_keys=True, separators=(",", ":")).encode())
    kit = {"format": 1, "status": "offline-candidate", "gitBaseRevision": base_revision,
           "snapshotSha256": tree_hash, "modules": versions, "files": files}
    content = dict(payload)
    content["kit.json"] = json.dumps(kit, indent=2).encode() + b"\n"
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in sorted(content.items()):
            if not safe_path(name):
                raise ValueError("Unsafe ZIP path")
            entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)
    return output.getvalue(), kit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output already exists; refusing to overwrite")
    dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
    subprocess.run([dotnet, "run", "--project", str(ROOT / "mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj"),
                    "-c", "Release", "--", str(ROOT)], cwd=ROOT, check=True)
    payload, versions = collect(ROOT)
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    archive, kit = make_archive(payload, versions, revision)
    with args.output.open("xb") as target:
        target.write(archive)
    print(f"Offline candidate: {args.output}; {len(versions)} packages; {len(kit['files'])} inventoried files")
    print("ZIP SHA256: " + digest(archive))
    print("Windows/PowerShell execution, installation and in-game acceptance NOT performed.")


if __name__ == "__main__":
    main()
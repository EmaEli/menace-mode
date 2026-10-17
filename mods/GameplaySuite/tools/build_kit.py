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


def collect(root, module=None, use_installed_settings=False):
    root = Path(root).resolve()
    catalog = json.loads((root / TOOLS / "distribution.json").read_text())
    available = catalog["modules"] + catalog.get("experimentalModules", [])
    if len(set(available)) != len(available) or any(not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", n) for n in available):
        raise ValueError("Invalid/duplicate module names")
    actual = {p.parent.name for p in (root / "mods/jiangyu").glob("*/jiangyu.json")}
    if actual != set(available):
        raise ValueError("Distribution catalog does not cover the current modules exactly")
    if module is not None and module not in available:
        raise ValueError("Unknown requested module")
    if use_installed_settings and module != "SquaddieFieldPromotions":
        raise ValueError("Installed-settings mode is supported only by SquaddieFieldPromotions")
    if module == "SquaddieFieldPromotions" and not use_installed_settings:
        raise ValueError("Experimental preview requires --use-installed-settings; do not replace an existing companion")
    modules = catalog["modules"] if module is None else list(dict.fromkeys(
        ([] if use_installed_settings else ["MenaceModSettings"]) + [module]))
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
    for current_module in modules:
        folder = root / "mods/jiangyu" / current_module
        manifest = json.loads((folder / "jiangyu.json").read_text())
        compiled = json.loads((folder / "compiled/jiangyu.json").read_text())
        if manifest["name"] != current_module or compiled["name"] != current_module or manifest["version"] != compiled["version"]:
            raise ValueError("Manifest mismatch: " + current_module)
        versions[current_module] = manifest["version"]
        project = folder / "code" / (current_module + ".csproj")
        if project.exists():
            xml = ET.parse(project)
            if xml.findtext("PropertyGroup/Version") != manifest["version"]:
                raise ValueError("Project/manifest mismatch: " + current_module)
            assembly = xml.findtext("PropertyGroup/AssemblyName", current_module)
            dlls = {p.name for p in (folder / "compiled/code").glob("*.dll")}
            if dlls != {assembly + ".dll"}:
                raise ValueError("Missing or unexpected compiled DLLs: " + current_module)
        if list((folder / "templates").glob("*.kdl")) and not (folder / "compiled/templates.json").is_file():
            raise ValueError("Missing compiled template payload: " + current_module)
        add(folder / "jiangyu.json")
        for filename in ("README.md", "CHANGELOG.md", "LICENSE"):
            if (folder / filename).is_file():
                add(folder / filename)
        tree(folder / "code", CODE_SUFFIXES)
        tree(folder / "templates", {".kdl"})
        tree(folder / "locales", {".po", ".pot"})
        tree(folder / "tests", {".cs", ".csproj", ".py", ".ps1"})
        tree(folder / "compiled")
        tree(folder / "runtime_assets")
    tree(root / "mods/GameplaySuite/src", {".cs"})
    tree(root / "mods/LargerMaps/src/LargerMaps", {".cs"})
    for filename in ("ThirdPersonCamera.csproj", "src/ThirdPersonCameraMod.cs", "README.md"):
        add(root / "mods/ThirdPersonCamera" / filename)
    for filename in ("Install-IntegratedSuite.ps1", "Install.Common.ps1", "Install.Deployment.ps1", "distribution.json"):
        add(root / TOOLS / filename)
    add(root / "mods/GameplaySuite/docs/INSTALLING.md")
    add(root / "docs/MIGRATION_STATUS.md")
    launcher = (root / "mods/GameplaySuite/INSTALL-SUITE.cmd").read_bytes().replace(b"\r\n", b"\n")
    launcher_name = "INSTALL-SUITE.cmd"
    if module is not None:
        marker = b'-BundleRoot "%~dp0."'
        if launcher.count(marker) != 1:
            raise ValueError("Canonical launcher invocation changed")
        extra = " -Product Standalone -Modules " + module
        if use_installed_settings:
            extra += " -UseInstalledSettings"
        launcher = launcher.replace(marker, marker + extra.encode("ascii"))
        launcher_name = "INSTALL-" + module + ".cmd"
    payload[launcher_name] = launcher.replace(b"\n", b"\r\n")
    payload["README.txt"] = (
        f"MENACE {module or 'Suite'} - CANDIDATE, not game-verified.\r\n"
        f"Double-click {launcher_name} to build, validate, back up and INSTALL.\r\n"
        + ("Requires existing MenaceModSettings; does NOT replace it or Jiangyu.Loader.\r\n" if use_installed_settings else "") +
        "Requires the pinned MENACE build and existing MelonLoader/proxies.\r\n"
        "Downloads a hash-verified pinned .NET 10 SDK privately under tools/dotnet.\r\n"
        "Jiangyu 1.4.8 SDK/loader are downloaded with SHA256 checks; no Unity Editor needed.\r\n"
        "Known conflicting copies are backed up outside Mods; unrelated mods, loaders,\r\n"
        "UserData and saves are not removed. Failure triggers rollback.\r\n"
        "Persistent log: %TEMP%\\MENACE-install.log. See source installer documentation.\r\n"
    ).encode("ascii")
    return payload, versions


def make_archive(payload, versions, base_revision, dotnet_sdk=None):
    files = [{"path": name, "sha256": digest(data)} for name, data in sorted(payload.items())]
    tree_hash = digest(json.dumps(files, sort_keys=True, separators=(",", ":")).encode())
    kit = {"format": 1, "status": "offline-candidate", "gitBaseRevision": base_revision,
           "snapshotSha256": tree_hash, "modules": versions, "files": files}
    if dotnet_sdk is not None:
        kit["dotnetSdk"] = dotnet_sdk
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
    parser.add_argument("--module", help="Build one explicitly selected module instead of the default Suite")
    parser.add_argument("--use-installed-settings", action="store_true", help="SFP preview: validate/reuse the installed companion and loader")
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output already exists; refusing to overwrite")
    dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
    payload, versions = collect(ROOT, args.module, args.use_installed_settings)
    runner = ROOT / "mods/GameplaySuite/tests/OfflineChecks/OfflineChecks.csproj"
    if args.module == "SquaddieFieldPromotions":
        runner = ROOT / "mods/jiangyu/SquaddieFieldPromotions/tests/OfflineChecks/OfflineChecks.csproj"
    subprocess.run([dotnet, "run", "--project", str(runner), "-c", "Release", "--", str(ROOT)], cwd=ROOT, check=True)
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    sdk = json.loads((ROOT / TOOLS / "distribution.json").read_text())["dotnetSdk"]
    archive, kit = make_archive(payload, versions, revision, sdk)
    with args.output.open("xb") as target:
        target.write(archive)
    print(f"Offline candidate: {args.output}; {len(versions)} packages; {len(kit['files'])} inventoried files")
    print("ZIP SHA256: " + digest(archive))
    print("Windows/PowerShell execution, installation and in-game acceptance NOT performed.")


if __name__ == "__main__":
    main()
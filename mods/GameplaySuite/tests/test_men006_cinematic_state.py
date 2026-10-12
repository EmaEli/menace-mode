"""MEN-006 static integration checks for camera/Suite source compatibility.

This tests the shared source contract and guards against regressions in the
build inputs. It is not a substitute for dotnet compilation or an in-game test.
Run: python tests/test_men006_cinematic_state.py
"""
from pathlib import Path
import json
import re

mods = Path(__file__).resolve().parents[2]
suite = mods / "GameplaySuite"
camera = mods / "ThirdPersonCamera"
read = lambda path: path.read_text(encoding="utf-8")

cam = read(camera / "src/ThirdPersonCameraMod.cs")
project = read(camera / "ThirdPersonCamera.csproj")
ui = read(suite / "src/Shared/TacticalHudVisibility.cs")
bridge = read(suite / "src/Shared/ThirdPersonCameraBridge.cs")
overwatch = read(suite / "src/Overwatch/Plugin.cs")
fire = read(suite / "src/CoordinatedFire/Plugin.cs")

manifest = lambda path: json.loads(read(path))["version"]
checks = {
    "camera 0.3.4": '"MENACE Third Person Camera", "0.3.4"' in cam and
        "<Version>0.3.4</Version>" in project,
    "camera explicit read-only static state":
        "public static bool IsCinematicViewActive => s_instance != null && s_instance._active;" in cam,
    "manual and auto entries use common state":
        re.search(r"_active\s*=\s*true;\s*HideTacticalUi\(\);", cam) is not None and
        "TryEnterThirdPerson(actor, false, kind)" in cam and
        "TryEnterThirdPerson(manager.GetActiveActor(), true)" in cam,
    "camera exit resets state":
        re.search(r"private void ExitThirdPerson\(.*?_active\s*=\s*false;", cam, re.S) is not None,
    "native HUD calls remain": "UITactical.SetUIVisibility(false)" in cam and
        "UITactical.SetUIVisibility(true)" in cam,
    "optional managed bridge finds camera by type name":
        'MenaceThirdPersonCamera.ThirdPersonCameraMod' in bridge and
        'IsCinematicViewActive' in bridge and
        "GetAssemblies()" in bridge,
    "no per-frame reflection invocations":
        "Delegate.CreateDelegate" in bridge and "return reader();" in bridge and
        "GetProperty(ActiveProperty" in bridge and
        "reader.DynamicInvoke" not in bridge and
        "MethodInfo.Invoke" not in bridge,
    "rate-limited initial binding":
        "_nextProbeAt = now + 5f;" in bridge and
        "if (now < _nextProbeAt) return;" in bridge,
    "dual guard (native + cinematic state)":
        "_nativeHudVisible && !ThirdPersonCameraBridge.IsActive" in ui,
    "Overwatch GUI guards visibility":
        "if (!TacticalHudVisibility.IsVisible || _runtime == null" in overwatch,
    "Coordinated Fire GUI guards visibility":
        "if (!TacticalHudVisibility.IsVisible || _runtime == null" in fire,
    "Coordinated Fire invisible hitbox disabled":
        "PointerOverPanel = () => TacticalHudVisibility.IsVisible &&" in fire,
    "standalone and suite compile shared source":
        '../Shared/*.cs' in read(suite / "src/Overwatch/Overwatch.csproj") and
        '../Shared/*.cs' in read(suite / "src/CoordinatedFire/CoordinatedFire.csproj"),
    "version consistency":
        manifest(camera / "modpack/ThirdPersonCamera/modpack.json") == "0.3.4" and
        manifest(suite / "packaging/suite/MenaceGameplaySuite/modpack.json") == "0.32.3" and
        manifest(suite / "packaging/standalone/Overwatch/modpack.json") == "0.32.3.3" and
        manifest(suite / "packaging/standalone/CoordinatedFire/modpack.json") == "1.6.9",
    "no default per-frame debug":
        "MelonLogger.Msg(" not in bridge and "MelonLogger.Msg(" not in ui and
        "DebugLog(" not in bridge,
}

for name, result in checks.items():
    print(("PASS" if result else "FAIL") + " " + name)
if not all(checks.values()):
    raise SystemExit("MEN-006 static integration checks failed.")
print(f"{len(checks)} MEN-006 integration checks passed")

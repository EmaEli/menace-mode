"""Execute the production camera-only input prefixes with managed stand-ins."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
HARNESS = r'''
using System;
class Camera {
    public int MouseCalls, KeyCalls;
    public bool Inside;
    public void HandleMouseInput(bool inside) { MouseCalls++; Inside = inside; }
    public void HandleKeyInput() { KeyCalls++; }
}
class TacticalManager {
    public static TacticalManager Current = new();
    public Camera Camera = new();
    public static TacticalManager Get() => Current;
    public Camera GetCamera() => Camera;
}
namespace Il2CppMenace.UI {
    class UIManager {
        public static UIManager Current = new();
        public bool Outside;
        public static UIManager Get() => Current;
        public bool IsCursorOutsideScreen() => Outside;
    }
}
static class Application { public static bool isFocused = true; }
enum KeyCode { Escape }
static class Input { public static bool Escape; public static bool GetKeyDown(KeyCode key) => Escape; }
static class Native { public static bool Valid(object value) => value != null; }
partial class Runtime {
    public static Runtime Instance;
    public bool Active, Settling, Armed;
    public bool Locked => Active || Settling;
    public int Cancelled;
    public void CancelTargeting(string reason) { Cancelled++; Armed = false; }
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Check " + checks); }
    public static void Main() {
        var manager = TacticalManager.Current;
        var camera = manager.Camera;
        var ui = Il2CppMenace.UI.UIManager.Current;
        foreach (var runtime in new Runtime[] { null, new(), new() { Armed = true } }) {
            Instance = runtime;
            Check(Hooks.Mouse() && Hooks.Keyboard());
            Check(camera.MouseCalls == 0 && camera.KeyCalls == 0);
        }
        // Player/AI volleys share Locked; settlement must preserve the same restrictions.
        foreach (bool settling in new[] { false, true }) {
            var r = Instance = new() { Active = !settling, Settling = settling };
            foreach (bool focused in new[] { false, true })
            foreach (bool outside in new[] { false, true }) {
                Application.isFocused = focused; ui.Outside = outside;
                int mouse = camera.MouseCalls, keys = camera.KeyCalls;
                Check(!Hooks.Mouse() && !Hooks.Keyboard());
                Check(camera.MouseCalls == mouse + 1 && camera.KeyCalls == keys + 1);
                Check(camera.Inside == (focused && !outside));
                Check(r.Cancelled == 0 && r.Locked);
            }
            // Escape still goes through the native pause path, not camera dispatch twice.
            Input.Escape = true;
            int before = camera.KeyCalls;
            Check(Hooks.Keyboard() && camera.KeyCalls == before && r.Locked);
            Input.Escape = false;
            manager.Camera = null;
            Check(!Hooks.Mouse() && !Hooks.Keyboard());
            manager.Camera = camera;
            TacticalManager.Current = null;
            Check(!Hooks.Mouse() && !Hooks.Keyboard());
            TacticalManager.Current = manager;
            Il2CppMenace.UI.UIManager.Current = null;
            Check(!Hooks.Mouse() && !camera.Inside);
            Il2CppMenace.UI.UIManager.Current = ui;
            r.Active = r.Settling = false;
            before = camera.KeyCalls;
            Check(Hooks.Mouse() && Hooks.Keyboard() && camera.KeyCalls == before);
        }
        var armed = Instance = new() { Armed = true };
        Input.Escape = true;
        Check(!Hooks.Keyboard() && !armed.Armed && armed.Cancelled == 1);
        Check(Hooks.Keyboard() && armed.Cancelled == 1);
        Console.WriteLine($"Coordinated Fire camera input: {checks} passed");
    }
}
'''


class CoordinatedFireCameraInputTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_camera_dispatch_preserves_combat_lock_and_escape(self):
        source = (ROOT / "src/CoordinatedFire/Runtime.cs").read_text()
        hooks = source[source.index("        public static bool Mouse("):].rsplit("    }", 1)[0]
        with tempfile.TemporaryDirectory(prefix="menace-cf-camera-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (root / "Program.cs").write_text(
                HARNESS + "\npartial class Runtime { public static class Hooks {\n" + hooks + "\n}}")
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Coordinated Fire camera input: 50 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
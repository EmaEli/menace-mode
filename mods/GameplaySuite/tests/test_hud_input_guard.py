"""Input guard wiring checks. The C# OfflineChecks runner executes the actual policy."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = ROOT.parent / "jiangyu"
GUARD = (PACKAGES / "MenaceModSettings/code/HudInputGuard.cs").read_text()


class HudInputGuardTests(unittest.TestCase):
    def test_one_shared_registry_and_mouse_only_patches(self):
        self.assertIn('new global::HarmonyLib.Harmony("menacemodsettings.hudinput")', GUARD)
        self.assertIn('typeof(TacticalState), "HandleMouseInput", Type.EmptyTypes', GUARD)
        self.assertIn('typeof(TacticalCamera), "HandleMouseInput", new[] { typeof(bool) }', GUARD)
        self.assertIn('typeof(NoneAction), "HandleLeftClickOnTile"', GUARD)
        self.assertEqual(GUARD.count("harmony.Patch("), 3)
        self.assertIn("priority = Priority.First", GUARD)
        self.assertIn("RuntimeHelpers.PrepareMethod(prefix.MethodHandle)", GUARD)

    def test_guard_is_independent_of_runtime_and_imgui(self):
        self.assertNotIn("Runtime.Instance", GUARD)
        self.assertNotIn("Event.current", GUARD)
        self.assertIn("Screen.height - mouse.y", GUARD)
        self.assertLess(GUARD.index("Environment.CurrentManagedThreadId != _mainThread"),
                        GUARD.index("Application.isFocused"))
        self.assertIn("Capture.Reset()", GUARD)

    def test_consumers_share_visible_geometry_and_unregister(self):
        for module, provider in (("CoordinatedFire", "GetInputRect"), ("Overwatch", "GetHudRect")):
            with self.subTest(module=module):
                source = (ROOT / f"src/{module}/Plugin.cs").read_text()
                self.assertIn(f"HudInputGuard.Register(this, {provider})", source)
                self.assertIn("HudInputGuard.Unregister(this)", source)
                self.assertIn('!Setting("EnableGameplay", true)', source)
                self.assertIn('!Setting("ShowHUD", true)', source)
                self.assertIn("manager.IsPaused()) return null", source)
                manifest = json.loads((PACKAGES / module / "jiangyu.json").read_text())
                self.assertIn("MenaceModSettings >= 0.4.1", manifest["depends"])
        runtime = (ROOT / "src/CoordinatedFire/Runtime.cs").read_text()
        self.assertNotIn("PointerOverPanel", runtime)
        self.assertIn("Instance?.Locked != true", runtime)
        self.assertIn("Input.GetKeyDown(KeyCode.Escape)", runtime)

    def test_companion_lifecycle_and_identity(self):
        system = (PACKAGES / "MenaceModSettings/code/SettingsWindowSystem.cs").read_text()
        for call in ("HudInputGuard.Install()", "HudInputGuard.SceneChanged()", "HudInputGuard.Unload()"):
            self.assertIn(call, system)
        project = (PACKAGES / "MenaceModSettings/code/MenaceModSettings.csproj").read_text()
        self.assertIn("<AssemblyVersion>1.0.0.0</AssemblyVersion>", project)


if __name__ == "__main__":
    unittest.main()
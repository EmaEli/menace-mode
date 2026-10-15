"""MEN-004 source guards/request-count model, NOT native ammo-consumption tests."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()
PREFIX = SOURCE.split("private static bool HighVolumeFireUsePrefix(", 1)[1].split("private static void HighVolumeFireUsePostfix(", 1)[0]
POSTFIX = SOURCE.split("private static void HighVolumeFireUsePostfix(", 1)[1].split("private static void HighVolumeFireIsUsablePostfix(", 1)[0]
CAPTURE = SOURCE.split("private static void CaptureHighVolumeAmmoBefore(", 1)[1].split("private static bool HighVolumeFireUsePrefix(", 1)[0]


class AmmoDiagnosticTests(unittest.TestCase):
    def test_debug_is_opt_in_and_snapshot_capture_is_gated(self):
        self.assertIn('AddToggle("DebugLogging", "Debug logging", false)', SOURCE)
        self.assertIn("if (_debugLogging)\n                CaptureHighVolumeAmmoBefore", PREFIX)
        self.assertIn("if (!_enabled || __instance == null) return true;", PREFIX)
        self.assertIn("if (!_enabled || __instance == null || __state.BeforeUses < 0) return;", POSTFIX)
        self.assertIn("if (_debugLogging)\n            {\n                int afterMod", POSTFIX)

    def test_trace_has_one_call_site_and_all_three_snapshots(self):
        self.assertEqual(SOURCE.count("[MEN-004 AMMO]"), 1)
        for field in ("actor=", "actorTemplate=", "actorPtr=", "item=", "itemPtr=", "skill=", "flags=",
                      "success=", "blocked=", "uses=", "maxUses=", "charges=", "consumedPerUse=",
                      "chargesPerUse=", "nativeDelta=", "extraRequested=", "extraCalls="):
            self.assertIn(field, POSTFIX)
        self.assertIn("{__state.BeforeUses}->{afterNative}->{afterMod}", POSTFIX)
        self.assertIn("{__state.BeforeUsesConsumed}->{afterNativeUsesConsumed}->{afterModUsesConsumed}", POSTFIX)
        for method in ("GetMaxUses()", "GetUsesConsumed()", "GetCharges()", "GetChargesPerUse()"):
            self.assertIn(method, CAPTURE)

    def test_failed_and_ineligible_calls_do_not_request_extra_consumption(self):
        self.assertIn("if (!HighVolumeFireSkillIds.Contains(skillId)) return true;", PREFIX)
        self.assertIn("if (!__instance.HasLimitedUses()) return true;", PREFIX)
        self.assertIn("if (before >= 2) return true;", PREFIX)
        self.assertIn("__state.BlockedByGuard = true;", PREFIX)
        self.assertIn("__result ? Math.Max(0, 2 - Math.Max(0, nativeDelta)) : 0", POSTFIX)
        self.assertIn("if (__result)\n            {\n                for", POSTFIX)
        self.assertIn("i < extraToConsume && __instance.GetUses() > 0", POSTFIX)
        self.assertIn("__instance.ConsumeUse();\n                    extraCalls++;", POSTFIX)
        self.assertIn("__instance.SynchronizeUses();", POSTFIX)

    def test_request_model_is_bounded_not_a_proof_of_ammo_units(self):
        # This is only the request-count expression in the postfix. ConsumeUse can
        # subtract GetUsesConsumed(), so two calls do not imply two ammunition units.
        for native_delta in range(-20, 33):
            for success in (False, True):
                requested = max(0, 2 - max(0, native_delta)) if success else 0
                self.assertTrue(0 <= requested <= 2)
                if not success or native_delta >= 2:
                    self.assertEqual(requested, 0)
        self.assertEqual(max(0, 2 - 1), 1)
        # Hypothetical mutable native consumption: one extra call at 3 uses/call
        # would consume 4 total after a native delta of 1, NOT 2. Requires live data.
        self.assertEqual(1 + max(0, 2 - 1) * 3, 4)

    def test_current_package_versions_remain_aligned(self):
        package = ROOT.parent / "jiangyu/WeaponMechanicsRebalanced"
        version = json.loads((package / "jiangyu.json").read_text())["version"]
        self.assertIn(f'AssemblyVersion("{version}.0")', SOURCE)
        self.assertIn(f'AssemblyFileVersion("{version}.0")', SOURCE)
        self.assertIn(f"<Version>{version}</Version>", (package / "code/WeaponMechanicsRebalanced.csproj").read_text())


if __name__ == "__main__":
    unittest.main()
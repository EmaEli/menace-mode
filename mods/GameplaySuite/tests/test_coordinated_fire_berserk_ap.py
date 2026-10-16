"""MEN-019 offline regression checks for Coordinated Fire 1.6.8.

These validate the cost-before-grant AP arithmetic and patch wiring in source.
They do NOT compile the C# code or replace live MENACE/IL2CPP testing.
"""
import json
import unittest
from itertools import product
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = (ROOT / "src/CoordinatedFire/Runtime.cs").read_text(encoding="utf-8")
RULES = (ROOT / "src/CoordinatedFire/Core/Rules.cs").read_text(encoding="utf-8")
PLUGIN = (ROOT / "src/CoordinatedFire/Plugin.cs").read_text(encoding="utf-8")
PACKAGE = ROOT.parent / "jiangyu/CoordinatedFire"
MANIFEST = json.loads((PACKAGE / "jiangyu.json").read_text())
CSPROJ = (PACKAGE / "code/CoordinatedFire.csproj").read_text(encoding="utf-8")


def expected_native_order(start, maximum, cost, gains):
    """The normal attack pays its AP fee before on-kill AP grants."""
    ap = start - cost
    for gain in gains:
        ap = min(maximum, ap + gain)
    return ap


def deferred_fee_with_carry(start, maximum, cost, gains):
    """Model of Fee.ClippedGain / AccumulateClippedGain / AfterPreservingClippedGains."""
    ap = start
    carried = 0
    for gain in gains:
        requested = ap + gain
        if requested > ap and ap <= maximum:
            clipped = max(0, requested - maximum)
            carried += min(clipped, max(0, cost - carried))
        ap = min(maximum, requested)
    return max(0, ap - cost) + min(cost, carried), carried


class BerserkApRefundTests(unittest.TestCase):
    def test_reported_five_ap_example(self):
        final, carried = deferred_fee_with_carry(95, 100, 30, (30,))
        self.assertEqual(carried, 25)
        self.assertEqual(final, 95)
        self.assertNotEqual(final, 70)  # Previous, wrongly deferred-fee result.

    def test_exhaustive_positive_grants_and_multiple_kills(self):
        checked = 0
        for maximum, cost, spent, gains in product(
            (40, 60, 80, 100, 120),
            (0, 5, 15, 20, 30, 40, 50),
            (0, 1, 5, 10, 15, 29, 30, 40, 70),
            ((), (0,), (5,), (30,), (30, 30), (12, 30, 20), (60,), (0, 50)),
        ):
            start = maximum - spent
            if start < cost or start < 0:
                continue
            actual, carried = deferred_fee_with_carry(start, maximum, cost, gains)
            expected = expected_native_order(start, maximum, cost, gains)
            with self.subTest(maximum=maximum, cost=cost, spent=spent, gains=gains):
                self.assertEqual(actual, expected)
                self.assertGreaterEqual(carried, 0)
                self.assertLessEqual(carried, cost)
            checked += 1
        self.assertGreaterEqual(checked, 2000)

    def test_source_tracks_only_clamped_positive_native_grants(self):
        section = RUNTIME.split("private void InterceptActionPointWrite(", 1)[1].split(
            "private void ReconcileNativeAP()", 1
        )[0]
        self.assertIn("requested > current", section)
        self.assertIn("actor.GetCurrentProperties()", section)
        self.assertIn("Fee.ClippedGain(current, requested, properties.GetActionPoints())", section)
        self.assertIn("Fee.AccumulateClippedGain(_clampedApGain, clipped, _transaction.Cost)", section)
        # Free first child can still trigger a grant before a paid second child.
        self.assertLess(section.index("Fee.ClippedGain("), section.index("int cost = CurrentNativeCost()"))
        hooks = RUNTIME.split("public static class Hooks", 1)[1]
        self.assertIn("nativeClamps: true", hooks)
        self.assertIn("nativeClamps: false", hooks)
        self.assertEqual(hooks.count("nativeClamps: true"), 1)
        self.assertEqual(hooks.count("nativeClamps: false"), 1)

    def test_carry_is_settled_once_only_if_fee_charged(self):
        section = RUNTIME.split("public void Finish(", 1)[1].split("private bool BeforeRoundCheck(", 1)[0]
        guard = "if (charge && !suppressed && Native.Valid(actor))"
        self.assertIn(guard, section)
        after = "Fee.AfterPreservingClippedGains(before, cost, _clampedApGain)"
        self.assertIn(after, section)
        self.assertLess(section.index(guard), section.index(after))
        self.assertIn("actor.SetActionPoints(after, true)", section)
        self.assertIn("private void InterceptActionPointWrite", RUNTIME)
        self.assertIn("if (!IsActive || _settling || !Native.Same(actor, _firingActor)) return;", RUNTIME)
        self.assertEqual(RUNTIME.count("_launchAp = _launchCost = _clampedApGain = 0;"), 3)

    def test_pure_helpers_keep_refund_bounded_by_fee(self):
        self.assertIn("public static int ClippedGain(", RULES)
        self.assertIn("public static int AccumulateClippedGain(", RULES)
        self.assertIn("int remaining = Math.Max(0, cost - carried);", RULES)
        self.assertIn("public static int AfterPreservingClippedGains(", RULES)
        self.assertIn("return After(current, cost) + Math.Min(cost, carried);", RULES)

    def test_versions_and_debug_default(self):
        version = MANIFEST["version"]
        self.assertIn(f'AssemblyVersion("{version}.0")', PLUGIN)
        self.assertIn(f'private const string Version = "{version}";', PLUGIN)
        self.assertIn(f"<Version>{version}</Version>", CSPROJ)
        self.assertIn('AddToggle("VerboseLogging", "Debug logging", false)', PLUGIN)


if __name__ == "__main__":
    unittest.main()

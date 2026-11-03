"""Guard the AI infantry Overwatch path that reserves a coordinated weapon pair."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = (ROOT / "src/Overwatch/Runtime.cs").read_text()
PLANNER = (ROOT / "src/Overwatch/AIOverwatchWeaponPlanner.cs").read_text()
RESERVE = RUNTIME.split("private void TryArmAI", 1)[1].split("private bool RemoveWatch", 1)[0]


class OverwatchAICoordinatedFireTests(unittest.TestCase):
    def test_only_coordinated_infantry_reserves_both_weapons(self):
        self.assertIn("allowPair: infantry && _coordinatedFireInstalled", RESERVE)
        self.assertIn("OverwatchMode.Both", RESERVE)
        self.assertIn("Arm(actor, skill, secondary, mode", RESERVE)
        self.assertIn(".Where(s => Native.CanReserveAIWeapon(actor, s, out _))", RESERVE)

    def test_pair_uses_maximum_ap_and_ammo_bounds(self):
        self.assertIn("Math.Max(actionPointCost(squad), actionPointCost(leader))", PLANNER)
        self.assertIn("Math.Max(Native.AmmoBound(skill), Native.AmmoBound(secondary))", RESERVE)
        self.assertIn("AIReactionBudget.Calculate(ap, cost, ammoBound", RESERVE)

    def test_single_weapon_affordability_fallback_is_preserved(self):
        self.assertIn("selection is { Secondary: null } && !diagnosticBonus", RESERVE)
        self.assertIn("allowPair: false", RESERVE)


if __name__ == "__main__":
    unittest.main()
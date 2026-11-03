"""Ensure the user-facing choice scales only the AI fractional AP reservation roll."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / "src/Overwatch/Plugin.cs").read_text()
RUNTIME = (ROOT / "src/Overwatch/Runtime.cs").read_text()


class OverwatchAIChanceMultiplierTests(unittest.TestCase):
    def test_dropdown_defaults_to_normal_and_wires_all_four_choices(self):
        self.assertIn('s.AddDropdown("AIOverwatchChanceMultiplier", "AI fractional Overwatch chance",', PLUGIN)
        for choice in ("Normal (1x)", "Double (2x)", "Triple (3x)", "Quadruple (4x)"):
            self.assertIn(f'"{choice}"', PLUGIN)
        self.assertIn('"AIOverwatchChanceMultiplier", "Normal (1x)"', PLUGIN)
        for value, multiplier in (("Double (2x)", 2), ("Triple (3x)", 3), ("Quadruple (4x)", 4)):
            self.assertIn(f'"{value}" => {multiplier}', PLUGIN)
        self.assertIn("AIChanceMultiplier()", PLUGIN)

    def test_multiplier_scales_only_fractional_ai_reservation_and_is_reported(self):
        reserve = RUNTIME.split("private void TryArmAI", 1)[1].split("private bool RemoveWatch", 1)[0]
        self.assertIn("AIReactionBudget.FractionalDrawThreshold(apRemainder, cost, _aiChanceMultiplier)", reserve)
        self.assertIn("AIReactionBudget.Calculate(ap, cost, ammoBound, randomDraw, _aiChanceMultiplier)", reserve)
        self.assertIn("fractionalChance={fractionalThreshold}/{cost} chanceMultiplier={_aiChanceMultiplier}", reserve)
        self.assertNotIn("SetActionPoints", reserve)
        self.assertIn("_aiChanceMultiplier = Math.Clamp(aiChanceMultiplier, 1, 4);", RUNTIME)
        arm = RUNTIME.split("private bool Arm(", 1)[1].split("private void OnActorTurnStarted", 1)[0]
        self.assertNotIn("_aiChanceMultiplier", arm)


if __name__ == "__main__":
    unittest.main()
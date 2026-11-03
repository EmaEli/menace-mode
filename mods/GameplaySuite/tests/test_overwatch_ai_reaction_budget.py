"""Execute the production AI reaction-budget helper with deterministic random draws."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
HELPER = ROOT / "src/Overwatch/AIReactionBudget.cs"
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")

HARNESS = r'''
internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new System.Exception(description);
    }

    private static void Main()
    {
        int successes = 0;
        for (int draw = 0; draw < 40; draw++)
            if (AIReactionBudget.Calculate(30, 40, 5, draw) == 1) successes++;
        Check(successes == 30, "30/40 AP must allocate exactly 30 of 40 draws");

        for (int multiplier = 1; multiplier <= 4; multiplier++)
        {
            successes = 0;
            for (int draw = 0; draw < 40; draw++)
                if (AIReactionBudget.Calculate(10, 40, 5, draw, multiplier) == 1) successes++;
            Check(successes == 10 * multiplier, $"10/40 AP at {multiplier}x must use the exact scaled probability");
        }

        for (int multiplier = 1; multiplier <= 4; multiplier++)
        {
            successes = 0;
            for (int draw = 0; draw < 40; draw++)
                if (AIReactionBudget.Calculate(50, 40, 5, draw, multiplier) == 2) successes++;
            Check(successes == 10 * multiplier, $"50 AP at {multiplier}x must preserve its guaranteed reaction and scale only the remainder");
        }

        Check(AIReactionBudget.Calculate(80, 40, 5, -1) == 2, "whole AP costs are guaranteed without a random draw");
        Check(AIReactionBudget.Calculate(90, 40, 2, 0) == 2, "ammo caps guaranteed and fractional reactions");
        Check(AIReactionBudget.Calculate(90, 40, 2, -1, 4) == 2, "ammo cap still applies when the scaled fractional chance reaches 100%");
        Check(AIReactionBudget.Calculate(90, 40, 3, 9) == 3, "fractional draw can add a third reaction");
        Check(AIReactionBudget.Calculate(10, 40, 5, -1, 4) == 1, "quadrupled 10/40 chance reaches 100% without a draw");
        Check(AIReactionBudget.Calculate(30, 20, 5, 9) == 2, "fraction uses the selected weapon cost");
        Check(AIReactionBudget.Calculate(30, 40, 5, -1) == 0, "missing draw does not grant a fractional reaction");
        Check(AIReactionBudget.Calculate(10, 40, 5, 11, 0) == 0, "invalid low multiplier clamps to normal");
        Check(AIReactionBudget.Calculate(10, 40, 5, -1, 5) == 1, "invalid high multiplier clamps to quadruple");
        Check(AIReactionBudget.Calculate(0, 40, 5, 0) == 0, "zero AP gets no normal reaction");
        Check(AIReactionBudget.Calculate(-1, 40, 5, 0) == 0, "negative AP is rejected");
        Check(AIReactionBudget.Calculate(40, 0, 5, 0) == 0, "zero-cost skills are rejected");
        Check(AIReactionBudget.Calculate(40, 40, 0, 0) == 0, "empty ammo bound rejects reactions");
        System.Console.WriteLine($"Overwatch AP budget checks: {_checks} passed");
    }
}
'''


class OverwatchAIReactionBudgetTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_helper_with_exact_fractional_probabilities_and_caps(self):
        with tempfile.TemporaryDirectory(prefix="menace-overwatch-budget-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>')
            (root / "Program.cs").write_text(HELPER.read_text() + "\n" + HARNESS)
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Overwatch AP budget checks: 22 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()

"""Run the production garrison-accuracy rules against managed test cases."""
from html import escape
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "mods/GameplaySuite/src/GarrisonableBuildings/GarrisonAccuracyRules.cs"
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")

HARNESS = r'''
using System;
using System.Collections.Generic;
using GarrisonableBuildings;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Main()
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "building_civilian_shelter_01"
        };
        Check(GarrisonAccuracyRules.ShouldApply("BUILDING_CIVILIAN_SHELTER_01", enabled, true, 0, 1), "enabled civilian garrison attack qualifies");
        Check(!GarrisonAccuracyRules.ShouldApply("building_military_1x1_bunker", enabled, true, 0, 1), "native bunker is outside the mod whitelist");
        Check(!GarrisonAccuracyRules.ShouldApply("building_civilian_shelter_01", enabled, false, 0, 1), "non-attack skill does not qualify");
        Check(!GarrisonAccuracyRules.ShouldApply("building_civilian_shelter_01", enabled, true, 1, 1), "deployed stance does not stack");
        Check(!GarrisonAccuracyRules.ShouldApply("building_civilian_shelter_01", enabled, true, -1, 1), "unknown stance fails closed");
        Check(!GarrisonAccuracyRules.ShouldApply(null, enabled, true, 0, 1), "missing container does not qualify");

        Check(Math.Abs(GarrisonAccuracyRules.Apply(0.8f, GarrisonAccuracyRules.AccuracyMultiplierPropertyType, 0, 1.15f) - 0.92f) < 0.0001f, "native AccuracyMult compounds with existing modifiers");
        Check(GarrisonAccuracyRules.Apply(40f, GarrisonAccuracyRules.AccuracyPropertyType, 15, 1f) == 55f, "native flat Accuracy amount is added");
        Check(GarrisonAccuracyRules.Apply(0.8f, GarrisonAccuracyRules.AccuracyMultiplierPropertyType, 0, 1f) == 0.8f, "neutral native multiplier has no effect");
        Check(GarrisonAccuracyRules.Apply(0.8f, GarrisonAccuracyRules.AccuracyMultiplierPropertyType, 0, float.NaN) == 0.8f, "invalid native multiplier is ignored");
        Check(GarrisonAccuracyRules.Apply(0.8f, 99, 15, 1.15f) == 0.8f, "unrelated native property is ignored");
        object flatValue = GarrisonAccuracyRules.ToPropertyValue(55f, GarrisonAccuracyRules.AccuracyPropertyType);
        Check(flatValue is int && (int)flatValue == 55, "flat Accuracy writes the native integer type");
        object multiplierValue = GarrisonAccuracyRules.ToPropertyValue(1.15f, GarrisonAccuracyRules.AccuracyMultiplierPropertyType);
        Check(multiplierValue is float && Math.Abs((float)multiplierValue - 1.15f) < 0.0001f, "AccuracyMult writes the native float type");
        Check(GarrisonAccuracyRules.ToPropertyValue(float.NaN, GarrisonAccuracyRules.AccuracyMultiplierPropertyType) == null, "non-finite property values are rejected");
        Check(GarrisonAccuracyRules.ToPropertyValue(5f, 99) == null, "unrelated property types cannot be written");
        Console.WriteLine($"Garrison accuracy rule checks: {checks} passed.");
    }
}
'''


class GarrisonAccuracyRuleTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_rules(self):
        project = f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include="{escape(str(SOURCE), quote=True)}" /></ItemGroup></Project>'''
        with tempfile.TemporaryDirectory(prefix="menace-garrison-accuracy-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(project)
            (root / "Program.cs").write_text(HARNESS)
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True,
                text=True,
                timeout=60,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Garrison accuracy rule checks: 15 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
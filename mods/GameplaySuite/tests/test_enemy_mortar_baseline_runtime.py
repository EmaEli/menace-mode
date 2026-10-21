"""Execute WMR's production baseline/restore methods with managed template stand-ins."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
SOURCE = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()

HARNESS = r'''
using System;
using System.Collections.Generic;
class GameObj {
    public Dictionary<string, object> Fields = new();
    public bool IsNull;
}
static class Templates {
    public static GameObj Current = new();
    public static int Writes;
    public static bool RejectWrite;
    public static GameObj Find(string type, string id) {
        if (type != "SkillTemplate" || id != "active.enemy_only_launch_medium_mortar_frag")
            throw new Exception("Wrong template selected");
        return Current;
    }
    public static object? ReadField(GameObj template, string field) => template.Fields.GetValueOrDefault(field);
    public static bool WriteField(GameObj template, string field, object value) {
        if (RejectWrite) return false;
        Writes++; template.Fields[field] = value; return true;
    }
}
class Plugin {
    static bool _enabled = true;
    static bool _debugLogging;
    static int warnings, diagnostics, checks;
    static readonly Dictionary<string, object> OriginalMortarTemplateValues = new();
    static void DebugLog(string message) { if (_debugLogging) diagnostics++; }
    static void Warn(string message) { warnings++; }
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Enemy baseline check " + checks); }
    // PRODUCTION_METHODS
    static void Main() {
        Templates.Current.Fields = new() {
            // NATIVE_FIELDS
            ["DamageMult"] = 1f, ["SuppressionDropoffAOE"] = -5f
        };
        var fields = Templates.Current.Fields;
        void Ballistics(int scatter, int chance) {
            Check((int)fields["Scatter"] == scatter && (int)fields["ScatterChance"] == chance);
        }
        void Untouched() {
            Check((int)fields["ActionPointCost"] == 50 && (int)fields["Uses"] == 10);
            Check((int)fields["Repetitions"] == 1 && (float)fields["DamageMult"] == 1f);
            Check((float)fields["SuppressionDropoffAOE"] == -5f);
        }
        ApplyEnemyMortarBallisticsPolicy(); Ballistics(4, 80); Untouched();
        Check(Templates.Writes == 2 && diagnostics == 0 && warnings == 0);
        ApplyEnemyMortarBallisticsPolicy(); Ballistics(4, 80); Untouched();
        Check(Templates.Writes == 2 && OriginalMortarTemplateValues.Count == 2);
        _enabled = false;
        ApplyEnemyMortarBallisticsPolicy(); Ballistics(2, 50); Untouched();
        _enabled = true; _debugLogging = true;
        ApplyEnemyMortarBallisticsPolicy(); Ballistics(4, 80); Untouched();
        Check(diagnostics == 1 && warnings == 0);
        Templates.Current.IsNull = true;
        int writes = Templates.Writes;
        ApplyEnemyMortarBallisticsPolicy();
        Check(Templates.Writes == writes && diagnostics == 2 && warnings == 0);
        Templates.Current.IsNull = false; _enabled = false; Templates.RejectWrite = true;
        ApplyEnemyMortarBallisticsPolicy();
        Check(warnings == 2); Ballistics(4, 80); Untouched();
        Console.WriteLine($"Enemy mortar baseline managed checks: {checks} passed");
    }
}
'''


class EnemyMortarBaselineTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_baseline_idempotence_restore_and_scope(self):
        methods = []
        for name in ("void ApplyEnemyMortarBallisticsPolicy()",
                     "void ApplyMortarTemplateValue(", "bool NumericEquivalent("):
            start = SOURCE.index("    private static " + name)
            end = SOURCE.index("\n    private static ", start + 1)
            methods.append(SOURCE[start:end])
        fixture = json.loads((ROOT / "tests/fixtures/enemy_medium_mortar.json").read_text())
        native = "\n".join(f'            ["{key}"] = {value},'
                           for key, value in fixture["cached_native"].items())
        program = HARNESS.replace("// PRODUCTION_METHODS", "\n".join(methods))
        program = program.replace("// NATIVE_FIELDS", native)
        with tempfile.TemporaryDirectory(prefix="menace-enemy-mortar-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (root / "Program.cs").write_text(program)
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"),
                                     "-c", "Release", "--nologo", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Enemy mortar baseline managed checks:", result.stdout)


if __name__ == "__main__":
    unittest.main()
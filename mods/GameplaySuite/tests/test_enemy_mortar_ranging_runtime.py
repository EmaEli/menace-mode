"""Execute production enemy target lookup/reset with managed actor stand-ins, not IL2CPP."""
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
using System.Linq;
using MenaceWeaponMechanicsRebalanced;
namespace Il2CppMenace.Tactical {
    class Actor { public long Key; public Actor(long key) { Key = key; } }
}
class Plugin {
    static bool _debugLogging;
    static int checks;
    class MortarRangingState { }
    class MortarApplyState { public (long Actor, long Weapon) RangingKey; }
    static readonly Dictionary<(long Actor, long Weapon), Dictionary<(int X, int Z), MortarRangingState>> MortarRangingSolutions = new();
    static readonly Dictionary<long, Queue<MortarApplyState>> PendingMortarImpacts = new();
    // ENEMY_DICTIONARY
    static long NativeKey(Il2CppMenace.Tactical.Actor actor) => actor.Key;
    static void DebugLog(string message) { }
    static void Warn(string message) => throw new Exception(message);
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Ranging check " + checks); }
    // PRODUCTION_METHODS
    static void Main() {
        var actor = new Il2CppMenace.Tactical.Actor(1);
        var first = GetEnemyMortarTurnState((1, 10), (3, 4));
        var second = GetEnemyMortarTurnState((1, 10), (8, 9));
        var otherWeapon = GetEnemyMortarTurnState((1, 20), (3, 4));
        var otherActor = GetEnemyMortarTurnState((2, 10), (3, 4));
        Check(ReferenceEquals(first, GetEnemyMortarTurnState((1, 10), (3, 4))));
        Check(first.EffectiveScatter(4) == 4);
        first.RecordShot(false); Check(first.EffectiveScatter(4) == 3);
        first.RecordShot(false); Check(first.EffectiveScatter(4) == 2);
        first.RecordShot(false); Check(first.EffectiveScatter(4) == 2);
        first.RecordShot(true); Check(first.EffectiveScatter(4) == 1);
        Check(second.EffectiveScatter(4) == 4 && !second.SolutionFound);
        Check(otherWeapon.EffectiveScatter(4) == 4 && otherActor.EffectiveScatter(4) == 4);
        second.RecordShot(false); otherWeapon.RecordShot(true); otherActor.RecordShot(true);
        MortarRangingActorTurnStartPostfix(null!);
        MortarRangingActorTurnStartPostfix(new Il2CppMenace.Tactical.Actor(0));
        MortarRangingActorTurnStartPostfix(new Il2CppMenace.Tactical.Actor(3));
        Check(first.SolutionFound && second.Shots == 1 && otherWeapon.SolutionFound);
        MortarRangingSolutions[(1, 10)] = new();
        var playerHistory = MortarRangingSolutions[(1, 10)];
        MortarRangingActorTurnStartPostfix(actor);
        foreach (var state in new[] { first, second, otherWeapon })
            Check(state.Shots == 0 && !state.SolutionFound && state.EffectiveScatter(4) == 4);
        Check(otherActor.SolutionFound && otherActor.EffectiveScatter(4) == 1);
        Check(ReferenceEquals(playerHistory, MortarRangingSolutions[(1, 10)]));
        first.RecordShot(false); Check(first.EffectiveScatter(4) == 3);
        first.RecordShot(true);
        ClearMortarRangingForActor(actor);
        Check(GetEnemyMortarTurnState((1, 10), (3, 4)).EffectiveScatter(4) == 4);
        Check(GetEnemyMortarTurnState((1, 10), (8, 9)).EffectiveScatter(4) == 4);
        Check(otherActor.SolutionFound && !MortarRangingSolutions.ContainsKey((1, 10)));
        Console.WriteLine($"Enemy mortar ranging managed checks: {checks} passed");
    }
}
'''


class EnemyMortarRangingTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_target_weapon_actor_isolation_and_actual_turn_reset(self):
        methods = []
        for signature in ("MortarTurnScatterState GetEnemyMortarTurnState(",
                          "void MortarRangingActorTurnStartPostfix(",
                          "void ClearMortarRangingForActor("):
            start = SOURCE.index("    private static " + signature)
            end = SOURCE.index("\n    private static ", start + 1)
            methods.append(SOURCE[start:end])
        declaration = next(line for line in SOURCE.splitlines()
                           if " EnemyMortarTurnRanging = new();" in line)
        program = HARNESS.replace("// PRODUCTION_METHODS", "\n".join(methods))
        program = program.replace("// ENEMY_DICTIONARY", declaration)
        rules = ROOT / "src/WeaponMechanicsRebalanced/MortarScatterRules.cs"
        with tempfile.TemporaryDirectory(prefix="menace-enemy-ranging-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>'
                f'<Compile Include="{rules}" /></ItemGroup></Project>')
            (root / "Program.cs").write_text(program)
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"),
                                     "-c", "Release", "--nologo", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Enemy mortar ranging managed checks:", result.stdout)
"""Execute production mortar payload/suppression code with native-wrapper stand-ins."""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")

HARNESS = r'''
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
namespace Il2CppInterop.Runtime.InteropTypes {
    class Il2CppObjectBase {
        public object? Native;
        public T? TryCast<T>() where T : class => Native as T;
    }
}
namespace Il2CppMenace.Tactical.Skills.Effects {
    class Attack {
        public float DamageMult = 1.4f, DamageToArmorDurabilityMult = 0.8f;
        public float SuppressionDropoffAOE = -5f, Suppression = 20f, SuppressionDealtMult = 1.2f;
        public float AccuracyBonus = 10f, AccuracyMult = 1f;
    }
}
namespace Il2CppMenace.Tactical {
    enum Direction { N, NE, E, SE, S, SW, W, NW }
    class Actor {
        public List<float> Applied = new();
        public void ApplySuppression(float value, bool direct, Actor source, Skills.Skill skill) => Applied.Add(value);
    }
    class Tile {
        public int X, Z; public Actor Actor = new();
        public static Dictionary<(int,int), Tile> Map = new();
        public int GetX() => X; public int GetZ() => Z;
        public Actor GetActor() => Actor;
        public Tile? GetNextTile(Direction d) {
            var offsets = new[] { (0,1),(1,1),(1,0),(1,-1),(0,-1),(-1,-1),(-1,0),(-1,1) };
            var (x,z) = offsets[(int)d]; return Map.GetValueOrDefault((X+x,Z+z));
        }
    }
}
namespace Il2CppMenace.Tactical.Skills {
    class Skill {
        public string Id = "active.enemy_only_launch_medium_mortar_frag";
        public List<Tile> Affected = new(); public Actor Actor = new();
        public string GetID() => Id;
        public List<Tile> GetAffectedTiles(Tile origin, Tile target) => Affected;
        public Actor GetActor() => Actor;
    }
}
class GameObj {
    public Dictionary<string, object> Fields = new(); public bool IsNull;
}
static class Templates {
    public static Dictionary<string, GameObj> All = new(); public static int Writes;
    public static GameObj Find(string type, string id) => All.GetValueOrDefault(id) ?? new() { IsNull = true };
    public static object? ReadField(GameObj t, string field) => t.Fields.GetValueOrDefault(field);
    public static bool WriteField(GameObj t, string field, object value) { Writes++; t.Fields[field] = value; return true; }
}
class Plugin {
    static bool _enabled = true, _debugLogging;
    static int checks;
    static readonly Dictionary<string, object> OriginalMortarTemplateValues = new(), OriginalMortarHandlerValues = new();
    static void DebugLog(string s) { }
    static void Warn(string s) => throw new Exception(s);
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Mortar payload check " + checks); }
    // PRODUCTION
    static void Main() {
        const string enemy = "active.enemy_only_launch_medium_mortar_frag";
        var attacks = new Dictionary<string, Il2CppMenace.Tactical.Skills.Effects.Attack>();
        foreach (string id in MortarSkillSpecs.Keys) {
            var attack = new Il2CppMenace.Tactical.Skills.Effects.Attack(); attacks[id] = attack;
            Templates.All[id] = new() { Fields = new() {
                ["Scatter"] = 2, ["ScatterChance"] = 50, ["ActionPointCost"] = 50, ["Uses"] = 10, ["Repetitions"] = 1,
                ["MarkAsActed"] = true,
                ["EventHandlers"] = new object[] { new Il2CppObjectBase(), new Il2CppObjectBase { Native = attack } }
            } };
        }
        ApplyMortarBallisticsPolicy();
        foreach (var (id, attack) in attacks) {
            var fields = Templates.All[id].Fields;
            Check(Math.Abs(attack.DamageMult - 0.7f) < 0.0001f);
            Check(Math.Abs(attack.DamageToArmorDurabilityMult - 0.4f) < 0.0001f);
            Check(attack.Suppression == 20f && attack.SuppressionDealtMult == 1.2f);
            Check(attack.SuppressionDropoffAOE == MortarSkillSpecs[id].SuppressionDropoffAOE);
            Check((int)fields["ActionPointCost"] == (id == enemy ? 33 : 25));
            Check((bool)fields["MarkAsActed"] == (id != enemy));
            Check((int)fields["Uses"] == (id == enemy ? 10 : 20) && (int)fields["Repetitions"] == 1);
        }
        Check((int)Templates.All[enemy].Fields["ScatterChance"] == 80);
        int writes = Templates.Writes;
        ApplyMortarBallisticsPolicy(); Check(writes == Templates.Writes);
        Check(Math.Abs(attacks[enemy].DamageMult - 0.7f) < 0.0001f);
        for (int z = -2; z <= 2; z++) for (int x = -2; x <= 2; x++)
            Il2CppMenace.Tactical.Tile.Map[(x,z)] = new() { X = x, Z = z };
        var center = Il2CppMenace.Tactical.Tile.Map[(0,0)];
        var skill = new Il2CppMenace.Tactical.Skills.Skill { Affected = new() { center, center } };
        MortarSuppressionRadiusPostfix(skill, new object[] { center, center, 20f, false, null! });
        foreach (var (coord, tile) in Il2CppMenace.Tactical.Tile.Map) {
            bool ring = Math.Max(Math.Abs(coord.Item1), Math.Abs(coord.Item2)) == 1;
            Check(tile.Actor.Applied.Count == (ring ? 1 : 0));
            if (ring) Check(tile.Actor.Applied[0] == 10f);
        }
        int applied = Il2CppMenace.Tactical.Tile.Map[(1,0)].Actor.Applied.Count;
        MortarSuppressionRadiusPostfix(skill, new object[] { center, center, 20f, true, null! });
        skill.Id = "unrelated";
        MortarSuppressionRadiusPostfix(skill, new object[] { center, center, 20f, false, null! });
        Check(Il2CppMenace.Tactical.Tile.Map[(1,0)].Actor.Applied.Count == applied);
        _enabled = false; ApplyMortarBallisticsPolicy();
        foreach (var (id, attack) in attacks) {
            Check(attack.DamageMult == 1.4f && attack.DamageToArmorDurabilityMult == 0.8f);
            Check(attack.SuppressionDropoffAOE == -5f && attack.AccuracyBonus == 10f && attack.AccuracyMult == 1f);
            var f = Templates.All[id].Fields;
            Check((int)f["Scatter"] == 2 && (int)f["ScatterChance"] == 50);
            Check((int)f["ActionPointCost"] == 50 && (int)f["Uses"] == 10);
            Check((bool)f["MarkAsActed"]);
        }
        _enabled = true; ApplyMortarBallisticsPolicy();
        Check(Math.Abs(attacks[enemy].DamageMult - 0.7f) < 0.0001f);
        Console.WriteLine($"Mortar payload checks: {checks} passed");
    }
}
'''


class MortarPayloadTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_native_base_wrappers_damage_suppression_economy_restore(self):
        spec = SOURCE[SOURCE.index("    private sealed class MortarSkillSpec"):SOURCE.index("    private static readonly HashSet<string> MortarRangingSkillIds")]
        methods = []
        for name in ("ApplyMortarBallisticsPolicy", "ApplyEnemyMortarBallisticsPolicy", "ApplyMortarTemplateValue",
                     "ApplyMortarTemplateScaledValue", "ApplyMortarHandlerScaledValue", "ApplyMortarHandlerValue",
                     "EnumerateInteropItems", "ReadMemberValue", "TryWriteNumericMember", "NumericEquivalent",
                     "FindMemberRecursive", "MortarSuppressionRadiusPostfix", "MortarTileKey"):
            start = re.search(r"    private static [^\n]+ " + name + r"\(", SOURCE).start()
            end = SOURCE.find("\n    private static ", start + 1)
            methods.append(SOURCE[start:end if end >= 0 else SOURCE.rfind("}")])
        program = HARNESS.replace("// PRODUCTION", spec + "\n".join(methods))
        with tempfile.TemporaryDirectory(prefix="menace-mortar-payload-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (root / "Program.cs").write_text(program)
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Mortar payload checks:", result.stdout)
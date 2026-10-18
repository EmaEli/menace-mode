"""Execute the actual #48 C# guards against managed stand-ins, not IL2CPP/Harmony."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = (ROOT / "mods/GameplaySuite/src/BuildingCombatRules/Plugin.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


def part(start, end):
    return start + SOURCE.split(start, 1)[1].split(end, 1)[0]


HARNESS = r'''
using System;
using System.Linq;
using System.Collections.Generic;
enum HalfCoverClass { None, Light, Medium, Heavy }
enum TagType { ANTI_STRUCTURE, VEHICLE_WEAPON, INDIRECT_FIRE, GRENADE, ROCKET, MINE, SMG, ASSAULT_RIFLE, BATTLE_RIFLE, SHOTGUN, SNIPER }
class Il2CppObjectBase { public IntPtr Pointer = (IntPtr)1; }
class Entity : Il2CppObjectBase {
    public bool Building, Vegetation, Structure;
    public bool IsStructure() => Structure;
    public bool IsBuilding() => Building;
    public bool IsVegetation() => Vegetation;
}
class Structure : Entity { public Structure() { base.Structure = true; } }
class BaseItemTemplate {
    public HashSet<TagType> Tags = new(); public bool HasTag(TagType tag) => Tags.Contains(tag);
}
class Item {
    public string Id = "item.assault_rifle"; public BaseItemTemplate Template = new();
    public string GetID() => Id; public BaseItemTemplate GetBaseItemTemplate() => Template;
}
class Skill {
    public string Id = "active.fire_assault_rifle"; public Item? Item;
    public Item? GetItem() => Item;
}
class DamageInfo {
    public int Damage = 20, ArmorDamage = 5, ArmorPenetration = 10;
    public bool IsDamageInflicted = true, IsElementDestroyed = true, IsTargetDestroyed = true, IsContainerDestroyed = true;
}
class Attack : Il2CppObjectBase { public HalfCoverClass DestroyHalfCover = HalfCoverClass.Heavy; }
class AttackHandler { public Attack Template = new(); public Attack GetTemplate() => Template; }
class Tile : Il2CppObjectBase { public Entity? Entity; public Entity? GetEntity() => Entity; }
partial class Guard {
    static bool _protectBuildings = true, _protectVegetation = true;
    static void DebugLog(string _) { } static void Error(string _) { }
    static string Short(Exception _) => "stand-in";
    static string EntityId(Entity _) => "stand-in";
    static string SkillId(Skill skill) => skill.Id;
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Guard check " + checks + " failed"); }
    public static void Main() {
        foreach (bool buildings in new[] { false, true })
        foreach (bool trees in new[] { false, true })
        foreach (int category in new[] { 0, 1, 2 }) {
            _protectBuildings = buildings; _protectVegetation = trees;
            var entity = new Structure { Building = category == 0, Vegetation = category == 1 };
            bool expected = category == 0 ? buildings : category == 1 && trees;
            Check(IsProtectedStructure(entity) == expected);
            var skill = new Skill(); var damage = new DamageInfo();
            OnStructureDamage(entity, skill, damage);
            Check(damage.Damage == (expected ? 0 : 20));
            Check(damage.ArmorDamage == (expected ? 0 : 5));
            if (expected) Check(!damage.IsDamageInflicted && !damage.IsElementDestroyed && !damage.IsTargetDestroyed && !damage.IsContainerDestroyed);
            var handler = new AttackHandler();
            var state = BeforeAttackTileHit(handler, skill, new Tile { Entity = entity });
            Check(handler.Template.DestroyHalfCover == (expected ? HalfCoverClass.None : HalfCoverClass.Heavy));
            AfterAttackTileHit(state);
            Check(handler.Template.DestroyHalfCover == HalfCoverClass.Heavy);
        }
        _protectBuildings = _protectVegetation = true;
        var tree = new Structure { Vegetation = true };
        foreach (TagType excluded in new[] { TagType.ANTI_STRUCTURE, TagType.VEHICLE_WEAPON, TagType.INDIRECT_FIRE, TagType.GRENADE, TagType.ROCKET, TagType.MINE }) {
            var item = new Item(); item.Template.Tags.UnionWith(new[] { excluded, TagType.ASSAULT_RIFLE });
            var skill = new Skill { Item = item }; var damage = new DamageInfo();
            OnStructureDamage(tree, skill, damage); Check(damage.Damage == 20);
            var handler = new AttackHandler(); BeforeAttackTileHit(handler, skill, new Tile { Entity = tree });
            Check(handler.Template.DestroyHalfCover == HalfCoverClass.Heavy);
        }
        foreach (string id in new[] { "active.fire_rifle_grenade", "active.fire_rocket_launcher", "active.fire_heavy_machinegun", "active.fire_anti_materiel_rifle" }) {
            var item = new Item(); item.Template.Tags.Add(TagType.ASSAULT_RIFLE);
            Check(!IsSmallArmsSkill(new Skill { Id = id, Item = item }));
        }
        var tagged = new Item(); tagged.Template.Tags.Add(TagType.ASSAULT_RIFLE);
        Check(IsSmallArmsSkill(new Skill { Id = "active.modded_shot", Item = tagged }));
        Check(IsSmallArmsSkill(new Skill()));
        Check(!IsProtectedStructure(new Entity { Vegetation = true }));
        Check(!IsProtectedStructure(null));
        Check(!IsProtectedStructure(new Structure { Vegetation = true, Pointer = IntPtr.Zero }));
        var empty = new AttackHandler(); BeforeAttackTileHit(empty, new Skill(), new Tile());
        Check(empty.Template.DestroyHalfCover == HalfCoverClass.Heavy);
        Console.WriteLine($"Building small-arms C# checks: {checks} passed");
    }
}
'''


class BuildingGuardRuntimeTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_actual_guard_methods(self):
        methods = [
            part("    private static readonly string[] SmallArmsTokens =", "    // Garrisonable Buildings"),
            part("    private static void OnStructureDamage", "    private static bool IsSmallArmsSkill"),
            part("    private static bool IsSmallArmsSkill", "    private static string SkillId"),
            part("    private static bool IsProtectedStructure", "    private static string Short"),
        ]
        with tempfile.TemporaryDirectory(prefix="menace-tree-guard-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
            (root / "Program.cs").write_text(HARNESS + "\npartial class Guard {\n" + "\n".join(methods) + "\n}")
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Building small-arms C# checks: 86 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
"""Execute the production #58 guard with managed stubs, not native IL2CPP/Harmony."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
MOD = ROOT / "mods/jiangyu/CoverDestructionRebalance"
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")

HARNESS = r'''
using System;
using System.Collections.Generic;
using CoverDestructionRebalance;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Jiangyu.Sdk;
namespace Il2CppInterop.Runtime.InteropTypes {
    public class Il2CppObjectBase {
        public IntPtr Pointer = (IntPtr)1;
        public object? Target;
        public T? TryCast<T>() where T : class => Target as T;
    }
}
namespace Il2CppMenace.Tactical { public enum HalfCoverClass { None, Light, Medium, Heavy } }
namespace Il2CppMenace.Tactical.Skills {
    public class Skill : Il2CppObjectBase {
        public string? Id; public bool Throw;
        public string? GetID() => Throw ? throw new InvalidOperationException() : Id;
    }
}
namespace Il2CppMenace.Tactical.Skills.Effects {
    public class Attack : Il2CppObjectBase { public HalfCoverClass DestroyHalfCover; public int Damage = 40; }
    public class AttackHandler : Il2CppObjectBase {
        public Attack? Template = new(); public bool Throw;
        public Attack? GetTemplate() => Throw ? throw new InvalidOperationException() : Template;
    }
}
namespace Jiangyu.Sdk {
    public class PatchInfo {
        public object? Instance; public IReadOnlyList<object?> Args; public bool Skip; public object? Result;
        public PatchInfo(object? instance, params object?[] args) { Instance = instance; Args = args; }
    }
    public class Patches {
        public Action<PatchInfo>? Callback; public string? Type, Method; public int Count;
        public void Prefix(string type, string method, int count, Action<PatchInfo> callback) {
            Type = type; Method = method; Count = count; Callback = callback;
        }
    }
    public class Log { public int Warnings; public void Warn(string _) => Warnings++; }
    public class ModContext { public Patches Patches = new(); public Log Log = new(); }
    public class JiangyuSystem {
        public ModContext Context = new(); public virtual void OnInit() { } public virtual void OnUnload() { }
    }
}
class Program {
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Mortar cover check " + checks); }
    static void Main() {
        var system = new MortarCoverSystem(); system.OnInit(); var patches = system.Context.Patches;
        Check(patches.Type == "Il2CppMenace.Tactical.Skills.Effects.AttackHandler");
        Check(patches.Method == "OnTileHit" && patches.Count == 4);
        void Hit(object? handler, object? skill) {
            var info = new PatchInfo(handler, skill, null, null, null); patches.Callback!(info);
            Check(!info.Skip && info.Result == null);
        }
        foreach (string id in new[] { "active.launch_light_mortar_frag", "active.launch_medium_mortar_frag" })
        foreach (HalfCoverClass tier in Enum.GetValues<HalfCoverClass>()) {
            var skill = new Skill { Id = id }; var handler = new AttackHandler();
            handler.Template!.DestroyHalfCover = tier;
            Hit(handler, skill); Check(handler.Template.DestroyHalfCover == HalfCoverClass.None);
            // Simulate a post-startup replacement, then repeated AoE impacts.
            handler.Template = new Attack { DestroyHalfCover = tier };
            Hit(handler, skill); Hit(handler, skill);
            Check(handler.Template.DestroyHalfCover == HalfCoverClass.None && handler.Template.Damage == 40);
            // SDK dispatch may box native objects through the base interop wrapper.
            handler = new AttackHandler { Template = new Attack { DestroyHalfCover = tier } };
            Hit(new Il2CppObjectBase { Target = handler }, new Il2CppObjectBase { Target = skill });
            Check(handler.Template.DestroyHalfCover == HalfCoverClass.None);
        }
        foreach (string? id in new[] { null, "", "active.fire_120mm_mortar", "active.mod.fire_120mm_mortar_autoloader",
            "active.mod.twinfire.fire_120mm_mortar_autoloader", "active.fire_rifle", "active.launch_rocket",
            "active.launch_light_mortar_smoke", "active.launch_light_mortar_frag_custom" })
        foreach (HalfCoverClass tier in Enum.GetValues<HalfCoverClass>()) {
            var handler = new AttackHandler { Template = new Attack { DestroyHalfCover = tier } };
            Hit(handler, new Skill { Id = id }); Check(handler.Template.DestroyHalfCover == tier);
        }
        var mortar = new Skill { Id = "active.launch_light_mortar_frag" };
        Hit(null, mortar); Hit(new object(), mortar); Hit(new AttackHandler(), null);
        Hit(new AttackHandler(), new object()); Hit(new AttackHandler { Template = null }, mortar);
        var invalid = new AttackHandler { Template = new Attack { DestroyHalfCover = HalfCoverClass.Heavy, Pointer = IntPtr.Zero } };
        Hit(invalid, mortar); Check(invalid.Template!.DestroyHalfCover == HalfCoverClass.Heavy);
        invalid.Template.Pointer = (IntPtr)1; invalid.Pointer = IntPtr.Zero;
        Hit(invalid, mortar); Check(invalid.Template.DestroyHalfCover == HalfCoverClass.Heavy);
        invalid.Pointer = (IntPtr)1; mortar.Pointer = IntPtr.Zero;
        Hit(invalid, mortar); Check(invalid.Template.DestroyHalfCover == HalfCoverClass.Heavy);
        patches.Callback!(new PatchInfo(null)); Check(system.Context.Log.Warnings == 0);
        mortar.Pointer = (IntPtr)1;
        Hit(new AttackHandler { Throw = true }, mortar);
        Hit(new AttackHandler(), new Skill { Throw = true }); Check(system.Context.Log.Warnings == 1);
        Hit(invalid, mortar); Check(invalid.Template.DestroyHalfCover == HalfCoverClass.None);
        Console.WriteLine($"Mortar cover C# checks: {checks} passed");
    }
}
'''


class MortarCoverTests(unittest.TestCase):
    def test_manifest_project_and_template_contract(self):
        version = json.loads((MOD / "jiangyu.json").read_text())["version"]
        project = ET.parse(MOD / "code/CoverDestructionRebalance.csproj")
        self.assertEqual(project.findtext("PropertyGroup/Version"), version)
        source = (MOD / "code/MortarCoverSystem.cs").read_text()
        self.assertLess(source.index("RuntimeHelpers.PrepareMethod"), source.index("Context.Patches.Prefix"))
        kdl = (MOD / "templates/half-cover-destruction.kdl").read_text()
        for size in ("light", "medium"):
            block = kdl.split(f'patch "SkillTemplate" "active.launch_{size}_mortar_frag"', 1)[1].split("\n}", 1)[0]
            self.assertIn('enum="HalfCoverClass" "None"', block)

    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_callback_and_registration(self):
        with tempfile.TemporaryDirectory(prefix="menace-mortar-cover-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
            (root / "MortarCoverSystem.cs").write_text((MOD / "code/MortarCoverSystem.cs").read_text())
            (root / "Program.cs").write_text(HARNESS)
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Mortar cover C# checks:", result.stdout)


if __name__ == "__main__":
    unittest.main()
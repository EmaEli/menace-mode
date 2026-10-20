"""Execute the production helper with managed doubles, not IL2CPP or MonoMod."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/Overwatch/Native.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
ATTRIBUTE = "    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]"

HARNESS = r'''
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
class EntityProperties { }
class Actor {
    public bool Live = true, CastThrows;
    public T? TryCast<T>() where T : class {
        if (CastThrows) throw new InvalidOperationException();
        return this as T;
    }
}
class UnitActor : Actor {
    public readonly EntityProperties Props = new();
    public bool Throws;
    public virtual EntityProperties GetCurrentProperties() {
        if (Throws) throw new InvalidOperationException();
        return Props;
    }
}
class DerivedUnit : UnitActor {
    public readonly EntityProperties OverrideProps = new();
    public override EntityProperties GetCurrentProperties() => OverrideProps;
}
class TransientActor : Actor {
    public readonly EntityProperties Props = new();
    public bool Throws;
    public virtual EntityProperties GetCurrentProperties() {
        if (Throws) throw new InvalidOperationException();
        return Props;
    }
}
class DerivedTransient : TransientActor {
    public readonly EntityProperties OverrideProps = new();
    public override EntityProperties GetCurrentProperties() => OverrideProps;
}
partial class Native {
    static bool Valid(Actor? actor) => actor?.Live == true;
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Check " + checks); }
    public static void Main() {
        var method = typeof(Native).GetMethod("CurrentProperties", BindingFlags.NonPublic | BindingFlags.Static)!;
        var flags = method.GetMethodImplementationFlags();
        Check((flags & MethodImplAttributes.NoInlining) != 0);
        Check((flags & MethodImplAttributes.NoOptimization) != 0);
        var unit = new UnitActor();
        Check(ReferenceEquals(CurrentProperties(unit, out var type), unit.Props));
        Check(type == "UnitActor");
        var derivedUnit = new DerivedUnit();
        Check(ReferenceEquals(CurrentProperties(derivedUnit, out type), derivedUnit.OverrideProps));
        var transient = new TransientActor();
        Check(ReferenceEquals(CurrentProperties(transient, out type), transient.Props));
        Check(type == "TransientActor");
        var derivedTransient = new DerivedTransient();
        Check(ReferenceEquals(CurrentProperties(derivedTransient, out type), derivedTransient.OverrideProps));
        Check(CurrentProperties(new Actor(), out type) == null);
        Check(type == "unknown");
        Check(CurrentProperties(null!, out type) == null);
        Check(CurrentProperties(new UnitActor { Live = false }, out type) == null);
        Check(CurrentProperties(new UnitActor { Throws = true }, out type) == null);
        Check(CurrentProperties(new TransientActor { Throws = true }, out type) == null);
        Check(CurrentProperties(new Actor { CastThrows = true }, out type) == null);
        Console.WriteLine($"Overwatch JIT guard: {checks} passed");
    }
}
'''


class OverwatchJitGuardTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_actual_helper_preserves_dispatch_and_failure_paths(self):
        self.assertEqual(SOURCE.count(ATTRIBUTE), 1)
        helper = ATTRIBUTE + SOURCE.split(ATTRIBUTE, 1)[1].split("    public static bool TryHitChance", 1)[0]
        with tempfile.TemporaryDirectory(prefix="menace-overwatch-jit-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>')
            (root / "Program.cs").write_text(HARNESS + "\npartial class Native {\n" + helper + "\n}")
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Overwatch JIT guard: 15 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
"""Execute the guarded AP helper with managed doubles, not IL2CPP/MonoMod."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
ATTRIBUTE = "    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]"
HARNESS = r'''
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CoordinatedFire.Core;
class Properties { public int Cap = 100; public int GetActionPoints() => Cap; }
class Actor {
    public int Key = 1, AP = 95, Reads;
    public bool ThrowProperties;
    public Properties? Props = new();
    public virtual int GetActionPoints() { Reads++; return AP; }
    public virtual Properties? GetCurrentProperties() {
        if (ThrowProperties) throw new InvalidOperationException("unavailable");
        return Props;
    }
}
class DerivedActor : Actor {
    public readonly Properties Buffed = new() { Cap = 110 };
    public override Properties GetCurrentProperties() => Buffed;
}
static class Native {
    public static bool Same(Actor a, Actor? b) => b != null && a.Key == b.Key;
    public static bool Valid(Properties? p) => p != null;
}
record Transaction(int Cost);
partial class Runtime {
    bool IsActive = true, _settling, _apDebitIntercepted;
    Actor? _firingActor;
    Transaction? _transaction = new(30);
    int _clampedApGain, Cost = 30, Logs;
    void _log(string message) { Logs++; }
    int CurrentNativeCost() => Cost;
    static int checks;
    static void Check(bool ok) { checks++; if (!ok) throw new Exception("Check " + checks); }
    static int Write(Runtime r, Actor actor, int requested, bool clamps = true) {
        r.InterceptActionPointWrite(actor, ref requested, clamps);
        return requested;
    }
    public static void Main() {
        var method = typeof(Runtime).GetMethod("InterceptActionPointWrite", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var flags = method.GetMethodImplementationFlags();
        Check((flags & MethodImplAttributes.NoInlining) != 0);
        Check((flags & MethodImplAttributes.NoOptimization) != 0);
        var a = new Actor();
        var r = new Runtime { _firingActor = a, IsActive = false };
        Check(Write(r, a, 85) == 85 && a.Reads == 0 && r.Logs == 0);
        r.IsActive = true; r._settling = true;
        Check(Write(r, a, 85) == 85 && a.Reads == 0);
        r._settling = false;
        var other = new Actor { Key = 2 };
        Check(Write(r, other, 65) == 65 && other.Reads == 0);
        Check(Write(r, a, 65) == 95 && r._apDebitIntercepted);
        r._apDebitIntercepted = false;
        Check(Write(r, a, 64) == 64 && !r._apDebitIntercepted);
        Check(Write(r, a, 125) == 125 && r._clampedApGain == 25);
        Write(r, a, 125);
        Check(r._clampedApGain == 30);
        r._clampedApGain = 0;
        Check(Write(r, a, 125, false) == 125 && r._clampedApGain == 0);
        var derived = new DerivedActor();
        r._firingActor = derived;
        Write(r, derived, 125);
        Check(r._clampedApGain == 15);
        r._clampedApGain = 0; r._firingActor = a; a.ThrowProperties = true;
        int logs = r.Logs;
        Check(Write(r, a, 125) == 125 && r._clampedApGain == 0 && r.Logs == logs + 1);
        a.ThrowProperties = false; a.Props = null;
        Check(Write(r, a, 125) == 125 && r._clampedApGain == 0);
        r._transaction = null;
        Check(Write(r, a, 125) == 125 && r._clampedApGain == 0);
        r.Cost = 0;
        Check(Write(r, a, 65) == 65);
        Console.WriteLine($"Coordinated Fire JIT guard: {checks} passed");
    }
}
'''


class CoordinatedFireJitGuardTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_actual_ap_helper_preserves_guards_debits_and_clipped_gains(self):
        source = (ROOT / "src/CoordinatedFire/Runtime.cs").read_text()
        self.assertEqual(source.count(ATTRIBUTE), 1)
        helper = ATTRIBUTE + source.split(ATTRIBUTE, 1)[1].split("    private void ReconcileNativeAP", 1)[0]
        with tempfile.TemporaryDirectory(prefix="menace-coordinated-jit-") as folder:
            root = Path(folder)
            rules = ROOT / "src/CoordinatedFire/Core/Rules.cs"
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '</PropertyGroup><ItemGroup><Compile Include="' + str(rules) + '" />'
                '</ItemGroup></Project>')
            (root / "Program.cs").write_text(HARNESS + "\npartial class Runtime {\n" + helper + "\n}")
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Coordinated Fire JIT guard: 15 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
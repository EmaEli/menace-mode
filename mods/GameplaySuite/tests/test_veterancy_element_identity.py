"""Execute the production hook/resolver with native-class and missing-parent stand-ins."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class VeterancyElementIdentityTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK unavailable")
    def test_shared_setter_receiver_and_unowned_element(self):
        source = (ROOT / "src/SquaddieVeterancy/ProbeRuntime.cs").read_text()
        methods = []
        for signature in ("private static void ElementSetSquaddieIdPostfix(",
                          "private long ResolveElementParentEntityPtr("):
            start = source.index("    " + signature)
            end = source.index("\n    private ", start + 1)
            methods.append(source[start:end])
        # No SDK/raw-memory stand-in is supplied: reintroducing that fallback fails compilation.
        fixture = r'''
using System;
using System.Collections.Generic;
class NativeObject { public IntPtr Pointer; }
class Element : NativeObject {
    public bool NativeElement = true, CastThrows, GetterThrows;
    public int Reads;
    public NativeObject? Parent;
    public T? TryCast<T>() where T : class {
        if (CastThrows) throw new Exception("class unavailable");
        return NativeElement ? this as T : null;
    }
    public int GetElementIndex() { Reads++; return 3; }
    public NativeObject? GetEntity() {
        Reads++;
        if (GetterThrows) throw new Exception("parent unavailable");
        return Parent;
    }
}
class ProbeRuntime {
    class LiveElementState {
        public Element? Element;
        public int SquaddieId, ElementIndex = -1;
        public long ParentEntityPtr;
        public bool PlayerSquaddie = false;
    }
    class Identities { public string Label(int id) => "test"; }
    static ProbeRuntime? Current;
    readonly Dictionary<long, LiveElementState> _liveElements = new();
    readonly Identities _identities = new();
    readonly Action<string> _log = _ => {};
    bool _verbose = false;
    static bool Valid(NativeObject? value) => value != null && value.Pointer != IntPtr.Zero;
    static long Key(NativeObject? value) => value?.Pointer.ToInt64() ?? 0;
    static int checks;
    static void Check(bool ok) { if (!ok) throw new Exception("Check " + checks); checks++; }
    public static void Main() {
        var c = new ProbeRuntime(); Current = c;
        var wrong = new Element { Pointer = new IntPtr(1), NativeElement = false };
        ElementSetSquaddieIdPostfix(wrong, 10);
        Check(wrong.Reads == 0 && c._liveElements.Count == 0);
        Check(c.ResolveElementParentEntityPtr(wrong) == 0 && wrong.Reads == 0);
        var missing = new Element { Pointer = new IntPtr(2) };
        ElementSetSquaddieIdPostfix(missing, 11);
        Check(c._liveElements[2].ParentEntityPtr == 0);
        Check(c._liveElements[2].SquaddieId == 11 && c._liveElements[2].ElementIndex == 3);
        var parent = new NativeObject { Pointer = new IntPtr(200) };
        missing.Parent = parent;
        Check(c.ResolveElementParentEntityPtr(missing) == 200);
        ElementSetSquaddieIdPostfix(missing, 12);
        Check(c._liveElements.Count == 1 && c._liveElements[2].ParentEntityPtr == 200);
        Check(c._liveElements[2].SquaddieId == 12 && c._liveElements[2].Element == missing);
        missing.GetterThrows = true;
        Check(c.ResolveElementParentEntityPtr(missing) == 0);
        missing.CastThrows = true; int before = missing.Reads;
        Check(c.ResolveElementParentEntityPtr(missing) == 0 && missing.Reads == before);
        ElementSetSquaddieIdPostfix(missing, 13);
        Check(c._liveElements[2].SquaddieId == 12 && missing.Reads == before);
        var zero = new Element();
        ElementSetSquaddieIdPostfix(zero, 14);
        Check(c.ResolveElementParentEntityPtr(zero) == 0 && zero.Reads == 0);
        Check(c.ResolveElementParentEntityPtr(null!) == 0);
        var ignored = new Element { Pointer = new IntPtr(4) };
        ElementSetSquaddieIdPostfix(ignored, 0);
        ElementSetSquaddieIdPostfix(ignored, -1);
        Check(ignored.Reads == 0 && c._liveElements.Count == 1);
        Current = null; ElementSetSquaddieIdPostfix(ignored, 1);
        Check(ignored.Reads == 0);
        Console.WriteLine($"PASS: {checks} production element identity checks");
    }
__METHODS__
}
'''
        with tempfile.TemporaryDirectory(prefix="menace-element-identity-") as folder:
            directory = Path(folder)
            (directory / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>')
            (directory / "Program.cs").write_text(fixture.replace("__METHODS__", "\n".join(methods)))
            result = subprocess.run([DOTNET, "run", "--project", str(directory / "Checks.csproj"),
                                     "-c", "Release", "--nologo", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("PASS: 14 production element identity checks", result.stdout)


if __name__ == "__main__":
    unittest.main()
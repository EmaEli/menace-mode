"""Run production delayed-movement and Break Contact logic with managed native stand-ins."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


class VehicleRetreatSafetyTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK unavailable")
    def test_production_retreat_lifecycle_and_native_skill_guards(self):
        source = (ROOT / "src/SquaddieVeterancy/ProbeRuntime.cs").read_text()
        methods = []
        for signature in ("private static void CancelDeadVehicleRetreat(",
                          "private void UpdateVehiclePressureSystem(",
                          "private void TryStartBreakContact(",
                          "private void FinalizeBreakContact("):
            start = source.index("    " + signature)
            end = source.index("\n    private ", start + 1)
            methods.append(source[start:end])
        fixtures = Path(__file__).parent / "VehicleRetreatChecks"
        helper = ROOT / "src/SquaddieVeterancy/VehicleRetreatSafety.cs"
        with tempfile.TemporaryDirectory(prefix="menace-retreat-") as folder:
            directory = Path(folder)
            (directory / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '<ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup>'
                f'<Compile Include="{helper}" /><Compile Include="{fixtures}/*.cs" />'
                '</ItemGroup></Project>')
            (directory / "Production.cs").write_text(
                'using Il2CppMenace.Tactical; namespace SquaddieProbe; partial class ProbeRuntime {\n'
                + "\n".join(methods) + "\n}")
            result = subprocess.run([DOTNET, "run", "--project", str(directory / "Checks.csproj"),
                                     "-c", "Release", "--nologo", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("production vehicle retreat checks", result.stdout)

    def test_delayed_native_entrypoint_is_guarded(self):
        source = (ROOT / "src/SquaddieVeterancy/ProbeRuntime.cs").read_text()
        self.assertIn('PatchPrefix(typeof(PathMover), "StartMovement", nameof(PathMoverStartMovementPrefix)', source)
        self.assertIn('VehicleRetreatSafety.CanStartMovement(__instance)', source)

    def test_installer_runs_native_contracts_before_staging(self):
        installer = (ROOT / "tools/Install-IntegratedSuite.ps1").read_text()
        start = installer.index("if ($name -eq 'SquaddieVeterancy')")
        end = installer.index("if ($name -eq 'SquaddieFieldPromotions')", start)
        self.assertIn("tests\\NativeContractChecks\\NativeContractChecks.csproj", installer[start:end])
        self.assertLess(end, installer.index("Copy-Item -LiteralPath $compiled -Destination"))


if __name__ == "__main__":
    unittest.main()
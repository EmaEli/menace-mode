"""Execute production passenger guards and extracted arming/selection with native stand-ins.

These are managed behavior checks, not native firing or Unity HUD acceptance tests.
"""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "src/Overwatch"
FIXTURE = ROOT / "tests/PassengerOverwatchChecks"
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


def between(source, start, end):
    return source[source.index(start):source.index(end, source.index(start))]


class PassengerOverwatchTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK is not available")
    def test_actual_passenger_guard_and_player_reservation_methods(self):
        native = (SOURCES / "Native.cs").read_text()
        runtime = (SOURCES / "Runtime.cs").read_text()
        using = "using System.Reflection;\nusing Il2CppTactical;\nusing Il2CppMenace.Tactical;\nusing Il2CppMenace.Tactical.Skills;\nusing Il2CppMenace.States;\nnamespace MenaceOverwatch;\n"
        with tempfile.TemporaryDirectory(prefix="menace-passenger-checks-") as directory:
            target = Path(directory)
            for name in ("Program.cs", "NativeStubs.cs", "RuntimeFixture.cs"):
                shutil.copyfile(FIXTURE / name, target / name)
            shutil.copyfile(SOURCES / "PassengerOverwatch.cs", target / "PassengerOverwatch.cs")
            guards = between(native, "    public static bool CanArm(", "    public static int AmmoBound(")
            guards += between(native, "    private static readonly MethodInfo? ActorLineOfSightMethod", "    public static long Key(")
            guards += between(native, "    public static bool CanReact(", "    // #44:")
            (target / "NativeMethods.cs").write_text(using + "internal static partial class Native {\n" + guards + "}\n")
            methods = between(runtime, "    private bool Arm(", "    private void OnActorTurnStarted(")
            methods += between(runtime, "    private Actor? ResolvePlayerActor(", "    private bool IsCurrentPlayerSelection(")
            methods += between(runtime, "    private bool RemoveWatch(", "    internal void OnMovementFinished(")
            (target / "RuntimeMethods.cs").write_text(using + "internal sealed partial class Runtime {\n" + methods + "}\n")
            (target / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
                '<Nullable>enable</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>')
            result = subprocess.run([DOTNET, "run", "--project", str(target / "Checks.csproj"), "-c", "Release"],
                                    text=True, capture_output=True, timeout=90)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Passenger Overwatch checks passed:", result.stdout)
            print(result.stdout.strip())

    def test_reaction_lifecycle_and_ui_wiring(self):
        runtime = (SOURCES / "Runtime.cs").read_text()
        native = (SOURCES / "Native.cs").read_text()
        plugin = (SOURCES / "Plugin.cs").read_text()
        react = between(native, "    public static bool CanReact(Actor shooter, Skill skill, Actor target, Tile tile, out string reason)", "    // #44:")
        self.assertIn("PassengerOverwatch.CanUseAttack(shooter, skill, out reason)", react)
        self.assertIn("shooter.IsAlliedWith(target)", react)
        cleanup = between(runtime, "    private void CleanupInvalidWatches(", "    private void ExpireWatchOnCommit(")
        self.assertIn("SamePositionContext(w.Shooter, w.ContainerKey)", cleanup)
        movement = between(runtime, "    internal void OnMovementCommitted(", "    internal void OnMovementFinished(")
        self.assertIn('ExpireWatchOnCommit(passenger!, "transport movement committed")', movement)
        process = between(runtime, "    private void ProcessReaction(", "    private void FinishReaction(")
        self.assertLess(process.index("SamePositionContext"), process.index("skill!.Use"))
        self.assertLess(process.index("ReferenceEquals(current, w)"), process.index("skill!.Use"))
        self.assertIn("UsageParameter.Free | UsageParameter.IgnoreUsabilityCheck", process)
        marker = between(runtime, "    private bool TryRefreshNativeHudMarker(", "    private UnitHUD? FindActorUnitHud(")
        self.assertIn("passenger ? actorHud.m_ContainedBadgeIcon : hitpointsBar", marker)
        self.assertIn('passenger ? "OverwatchPassengerReticles" : "OverwatchReticles"', marker)
        self.assertIn("passenger ? 8 : 11", marker)
        self.assertIn("Native.IsVisibleToPlayer(transport!)", marker)
        self.assertIn("existing.Passenger == passenger", marker)
        self.assertIn('"PASSENGERS", _runtime.SelectingPassenger', plugin)
        self.assertIn("ThirdPersonCameraBridge.IsActive", plugin)


if __name__ == "__main__":
    unittest.main()
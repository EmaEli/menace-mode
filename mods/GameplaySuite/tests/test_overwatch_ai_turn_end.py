"""Execute production turn-boundary callbacks with managed native stand-ins, not IL2CPP."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/Overwatch/Runtime.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
METHODS = SOURCE.split("    private void OnActorTurnStarted", 1)[1].split("    private void TryArmAI", 1)[0]
HOOKS = SOURCE.split("        public static void ActorTurnStarted", 1)[1].split("        public static void MovementStarted", 1)[0]

HARNESS = r'''
using System;
using System.Collections.Generic;
using MenaceOverwatch;
class Actor { public long Key; public int AP; public bool Player, Valid = true; }
static class Native {
    public static bool Valid(Actor actor) => actor != null && actor.Valid;
    public static bool IsPlayerControlled(Actor actor) => actor.Player;
    public static long Key(Actor actor) => actor.Key;
}
static class TacticalManager {
    public static bool Running = true;
    public static bool IsMissionRunning() => Running;
}
partial class Runtime {
    bool _disposed, _aiEnabled = true;
    readonly HashSet<long> _aiFinalizedActors = new();
    readonly Action<string> _errorLog = message => throw new Exception(message);
    int calls, budget, reactions;
    void TryArmAI(Actor actor, string trigger, int remainingAp) {
        if (actor.AP != 0) throw new Exception("Allocation must wait for native finalization");
        calls++; budget = remainingAp;
        reactions = AIReactionBudget.Calculate(remainingAp, 20, 5, 0);
    }
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void End(Actor actor) {
        Hooks.ActorTurnEnding(actor, out int state);
        actor.AP = 0; // Native OnTurnEnd clears remaining AP directly.
        Hooks.ActorTurnEnded(actor, state);
    }
    public static void Main() {
        var runtime = new Runtime(); Hooks.Owner = runtime;
        var actor = new Actor { Key = 1, AP = 30 };
        Hooks.ActorTurnStarted(actor);
        actor.AP -= 20; // A real attack leaves 10; never reuse the earlier 30.
        Hooks.ActorTurnEnding(actor, out int state);
        Check(state == 10 && runtime.calls == 0 && actor.AP == 10, "Capture only, without changing AP or arming early");
        actor.AP = 0;
        Hooks.ActorTurnEnded(actor, state);
        Check(runtime.calls == 1 && runtime.budget == 10 && runtime.reactions == 1, "Use pre-reset AP after native end");
        End(actor);
        Check(runtime.calls == 1, "Repeated end cannot reroll");
        Hooks.ActorTurnStarted(actor); actor.AP = 20; actor.AP -= 20;
        End(actor);
        Check(runtime.calls == 2 && runtime.budget == 0 && runtime.reactions == 0, "Spent AP is not resurrected");
        End(actor);
        Check(runtime.calls == 2, "Zero allocation also finalizes once");
        Hooks.ActorTurnStarted(actor); actor.AP = 50;
        End(actor);
        Check(runtime.calls == 3 && runtime.budget == 50 && runtime.reactions == 3, "Fresh native activation can reserve again");
        var other = new Actor { Key = 2, AP = 40 }; End(other);
        Check(runtime.calls == 4 && runtime.budget == 40, "Independent actor even without update polling");
        var player = new Actor { Key = 3, AP = 100, Player = true }; End(player);
        Check(runtime.calls == 4, "Player end never gets AI reservation");
        runtime._aiEnabled = false; Hooks.ActorTurnStarted(actor); actor.AP = 100; End(actor);
        Check(runtime.calls == 4, "Disabled feature does not reserve");
        runtime._aiEnabled = true; TacticalManager.Running = false; End(actor);
        Check(runtime.calls == 4, "No reservation outside a mission");
        TacticalManager.Running = true; actor.Valid = false; End(actor);
        Check(runtime.calls == 4, "Invalid actors ignored");
        actor.Valid = true; runtime._disposed = true; End(actor);
        Check(runtime.calls == 4, "Disposed runtime ignored");
        runtime._disposed = false; Hooks.ActorTurnStarted(actor); actor.AP = 10;
        Hooks.ActorTurnEnding(actor, out state); actor.AP = 0; runtime._aiEnabled = false;
        Hooks.ActorTurnEnded(actor, state);
        Check(runtime.calls == 4, "Recheck enabled state after native end");
        Console.WriteLine($"Overwatch turn-end checks: {checks} passed");
    }
}
'''


class OverwatchAITurnEndTests(unittest.TestCase):
    def test_native_hooks_capture_before_reset_and_remove_late_polling(self):
        self.assertIn("_harmony.Patch(actorTurnStart, postfix:", SOURCE)
        self.assertIn("prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ActorTurnEnding))", SOURCE)
        self.assertIn("postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ActorTurnEnded))", SOURCE)
        self.assertIn("_aiFinalizedActors.Clear();", SOURCE)
        self.assertNotIn("ObserveAI(", SOURCE)
        reserve = SOURCE.split("    private void TryArmAI", 1)[1].split("    private bool RemoveWatch", 1)[0]
        self.assertIn("int ap = Math.Max(0, remainingAp);", reserve)
        self.assertNotIn("actor.GetActionPoints()", reserve)

    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_callbacks_across_native_ap_reset(self):
        methods = "    private void OnActorTurnStarted" + METHODS
        hooks = "class Hooks { internal static Runtime? Owner; public static void ActorTurnStarted" + HOOKS + "}"
        harness = HARNESS.replace("class Actor {", "class Actor { public int GetActionPoints() => AP;")
        with tempfile.TemporaryDirectory(prefix="menace-overwatch-turn-end-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>')
            (root / "Program.cs").write_text(harness + "\npartial class Runtime {\n" + methods + hooks + "\n}")
            (root / "Budget.cs").write_text((ROOT / "src/Overwatch/AIReactionBudget.cs").read_text())
            result = subprocess.run(
                [DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Overwatch turn-end checks: 13 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
"""#23 source wiring/accounting checks, not a substitute for native vehicle tests."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = (ROOT / "src/Overwatch/Native.cs").read_text()
RUNTIME = (ROOT / "src/Overwatch/Runtime.cs").read_text()


class OverwatchVehicleSectorTests(unittest.TestCase):
    def test_native_check_uses_angle_and_fails_closed(self):
        gate = NATIVE.split("if (IsVehicleActor(shooter))", 1)[1].split("if (!source.HasLineOfSightTo", 1)[0]
        self.assertIn("if (!skill.IsInRange(tile, source, true))", gate)
        self.assertIn("outside native vehicle weapon range/firing sector", gate)
        self.assertIn("catch (Exception e)", gate)
        self.assertEqual(gate.count("return false;"), 2)
        self.assertNotIn("Quaternion", gate)
        self.assertNotIn("SetRotation", gate)

    def test_same_gate_is_used_before_queue_and_before_spending(self):
        evaluate = RUNTIME.split("private bool EvaluateSkill", 1)[1].split("private void ProcessReaction", 1)[0]
        self.assertIn("Native.CanReact(w.Shooter, skill, target, tile, out reason)", evaluate)
        queue = RUNTIME.split("private void QueueReactionsForTrigger", 1)[1].split("private bool EvaluateSkill", 1)[0]
        self.assertLess(queue.index("EvaluateSkill(w, w.Primary"), queue.index("_reactions.Enqueue"))
        self.assertLess(queue.index("EvaluateSkill(w, w.Secondary!"), queue.index("_reactions.Enqueue"))
        process = RUNTIME.split("private void ProcessReaction()", 1)[1].split("private void FinishReaction", 1)[0]
        gate = process.index("Native.CanReact(w.Shooter, skill!, r.Target, r.Tile")
        self.assertLess(gate, process.index("int beforeAmmo"))
        self.assertLess(gate, process.index("ActivateReactionActionContext"))
        self.assertLess(gate, process.index("skill!.Use(r.Tile, usage)"))
        self.assertIn("FinishReaction(r, r.AnyShotAccepted);", process[gate:])

    def test_rejected_shots_do_not_consume_reaction_count(self):
        finish = RUNTIME.split("private void FinishReaction", 1)[1].split("private void ActivateReactionActionContext", 1)[0]
        self.assertLess(finish.index("if (!consumed) return;"), finish.index("r.Watch.RemainingReactions - 1"))
        self.assertIn("r.Watch.Pending = false;", finish)

    def test_release_identity_and_debug_remain_consistent(self):
        package = ROOT.parent / "jiangyu/Overwatch"
        version = json.loads((package / "jiangyu.json").read_text())["version"]
        source = (ROOT / "src/Overwatch/Plugin.cs").read_text()
        for attribute in ("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion"):
            self.assertIn(f'{attribute}("{version}")', source)
        self.assertIn(f"<Version>{version}</Version>", (package / "code/Overwatch.csproj").read_text())
        self.assertIn('s.AddToggle("DebugLogging", "Debug logging", false)', source)


if __name__ == "__main__":
    unittest.main()
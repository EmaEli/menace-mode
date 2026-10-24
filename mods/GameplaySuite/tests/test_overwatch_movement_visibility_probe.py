"""Guard the opt-in, read-only same-tile movement visibility probe."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = (ROOT / "src/Overwatch/Runtime.cs").read_text()
NATIVE = (ROOT / "src/Overwatch/Native.cs").read_text()
PLUGIN = (ROOT / "src/Overwatch/Plugin.cs").read_text()


class OverwatchMovementVisibilityProbeTests(unittest.TestCase):
    def test_diagnostic_is_opt_in_and_does_not_require_general_debug_logging(self):
        self.assertIn('s.AddToggle("DebugMovementVisibility", "DEBUG: trace movement visibility timing", false)', PLUGIN)
        debug_log = PLUGIN.split("private void DebugLog", 1)[1].split("private void WarningLog", 1)[0]
        self.assertIn('Setting("DebugLogging", false) || Setting("DebugMovementVisibility", false)', debug_log)
        self.assertIn("bool debugMovementVisibility", RUNTIME)

    def test_only_movement_visibility_rejections_schedule_a_deferred_sample(self):
        queue = RUNTIME.split("private void QueueReactionsForTrigger", 1)[1].split("private void ScheduleMovementVisibilityProbe", 1)[0]
        self.assertIn("if (isMovement && _debugMovementVisibility)", queue)
        self.assertIn("primaryReason == Native.TargetNotVisibleReason", queue)
        self.assertIn("secondaryReason == Native.TargetNotVisibleReason", queue)
        self.assertIn("ScheduleMovementVisibilityProbe(w, w.Primary, target, tile)", queue)

    def test_deferred_probe_requires_same_tiles_and_only_checks_native_detection(self):
        sample = RUNTIME.split("private void ProcessMovementVisibilityProbes", 1)[1].split("private void LogMovementVisibilityProbe", 1)[0]
        self.assertIn("if (now < probe.SampleAt) continue;", sample)
        self.assertIn("Native.Same(probe.Shooter.GetTile(), probe.SourceTile)", sample)
        self.assertIn("Native.Same(probe.Target.GetTile(), probe.TargetTile)", sample)
        self.assertIn("Native.HasActorLineOfSight(", sample)
        self.assertIn("Native.CanReact(", sample)
        self.assertIn("Native.TryHitChance(", sample)
        for gameplay_action in ("QueueReactionsForTrigger(", "ProcessReaction(", "Skill.Use(", "RemainingReactions ="):
            self.assertNotIn(gameplay_action, sample)
        self.assertIn("MovementVisibilityProbeDelaySeconds = 0.15f", RUNTIME)
        self.assertIn("MovementVisibilityProbeMaxAgeSeconds = 0.5f", RUNTIME)
        self.assertIn("public static bool HasActorLineOfSight", NATIVE)

    def test_disabling_diagnostics_and_reset_clear_pending_probes(self):
        configure = RUNTIME.split("public void Configure", 1)[1].split("public void Update", 1)[0]
        reset = RUNTIME.split("private void Reset()", 1)[1].split("public void Dispose", 1)[0]
        self.assertIn("if (_debugMovementVisibility && !debugMovementVisibility) _movementVisibilityProbes.Clear();", configure)
        self.assertIn("_movementVisibilityProbes.Clear();", reset)


if __name__ == "__main__":
    unittest.main()
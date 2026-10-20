"""Read-only diagnostic wiring/contracts; not a native execution test."""
from pathlib import Path
import os
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/LargerMaps/AIRouteDiagnostics.cs").read_text()
PLUGIN = (ROOT / "src/LargerMaps/Plugin.cs").read_text()


class RouteDiagnosticsTests(unittest.TestCase):
    def test_opt_in_independent_of_deep_profiler(self):
        self.assertIn('if (!ReadBool("DiagnoseAIRoutes", false)) return;', SOURCE)
        self.assertRegex(PLUGIN, r'AddToggle\("DiagnoseAIRoutes",[^\n]*false\)')
        self.assertNotIn('ReadBool("DiagnoseAIPerformance"', SOURCE)
        self.assertNotIn('ReadBool("DiagnoseAIPathfinding"', SOURCE)
        self.assertNotIn('_routeMoveLines', PLUGIN)
        self.assertNotIn('_routeAgentLines', PLUGIN)

    def test_probes_observe_decisions_results_and_actual_arrival(self):
        for name in ('OnRoundStart', 'OnTurnStart', 'OnTurnEnd', 'PickBehavior',
                     'Execute', 'OnMovementFinished', 'Use', 'ApplyToTile', 'OnEvaluate'):
            self.assertIn('"' + name + '"', SOURCE)
        for label in ('CHOICE', 'ATTACK-EVAL', 'MOVE', 'ARRIVED', 'SKILL', 'APPLIED', 'COUNTS'):
            self.assertIn('"' + label + '"', SOURCE)
        self.assertIn('accepted={__result}', SOURCE)
        self.assertIn('not proof of hit/kill', SOURCE)
        self.assertIn('per-actor budgets', SOURCE)
        self.assertIn('AI ROUTE LIMIT', SOURCE)

    def test_observer_does_not_change_native_results_or_request_evaluation(self):
        for operation in ('ref bool __result', 'TrySetMember(', 'SetSleeping(', 'Task.Run(',
                          'FindPath(', 'IsUsableOn(', 'HasLineOfSight(', '.Wait(', 'SetOrder('):
            self.assertNotIn(operation, SOURCE)
        self.assertIn('PrepareMethod(', SOURCE)
        self.assertNotIn('InvokeNoArg(__instance, "PickBehavior")', SOURCE)
        self.assertNotIn('InvokeNoArg(__instance, "Evaluate")', SOURCE)

    def test_behavior_state_is_isolated_from_deep_profiler(self):
        self.assertIn('private static class AIRouteBehaviorProbe', SOURCE)
        self.assertIn('typeof(AIRouteBehaviorProbe));', SOURCE)
        self.assertIn('callbacks.GetMethod(prefix, flags)', SOURCE)
        self.assertIn('callbacks.GetMethod(postfix, flags)', SOURCE)
        self.assertIn('typeof(AIRouteBudget), typeof(AIRouteBehaviorProbe)', SOURCE)
        self.assertIn('ex.GetBaseException().Message', SOURCE)

    def test_move_state_is_observed_even_when_idle_wins(self):
        self.assertIn('moveState=[{AIRouteMoveState(move)}]', SOURCE)
        self.assertIn('move ??= AIRouteAsMove(behavior)', SOURCE)
        self.assertIn('m.Name == "TryCast"', SOURCE)
        for member in ('m_TurnsBelowUtilityThreshold', 'm_TurnsBelowUtilityThresholdLastTurn',
                       'm_IsMovementDone', 'm_HasMovedThisTurn', 'UtilityScore',
                       'UtilityByAttacksScore', 'DistanceScore'):
            self.assertIn('"' + member + '"', SOURCE)
        for label in ('MOVE-EVAL', 'MOVE-RESET', 'MOVE-UTILITY'):
            self.assertIn('"' + label + '"', SOURCE)
        self.assertIn('AI ROUTE v3 hooks={ok}/15', SOURCE)
        self.assertIn('stored target may be stale', SOURCE)
        self.assertNotIn('InvokeNoArg(__instance, "HasUtility")', SOURCE)

    def test_native_probe_signatures_when_dump_supplied(self):
        file = os.environ.get('MENACE_DUMP_CS')
        if not file:
            self.skipTest('Set MENACE_DUMP_CS for game contract checks')
        dump = Path(file).read_text(errors='replace')
        for signature in ('public void OnMovementFinished()', 'public void OnTurnEnd()',
                          'private Behavior PickBehavior()', 'public bool Execute(Actor _actor)',
                          'public bool Use(Tile _targetTile, UsageParameter _parameters = 0)',
                          'public void ApplyToTile(Tile _targetTile, UsageParameter _parameters)',
                          'protected override int OnEvaluate(Actor _actor)',
                          'public override void OnNewTurn()',
                          'protected bool HasUtility(Dictionary<Tile, TileScore> _tiles)',
                          'public override void OnRoundStart()'):
            self.assertIn(signature, dump)


if __name__ == '__main__':
    unittest.main()
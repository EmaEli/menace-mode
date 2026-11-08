"""Source/version wiring only; executable policy tests live in AdvanceRecoveryChecks."""
from pathlib import Path
import json
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
AI = ROOT.parent / 'jiangyu/AIPerformance'
PLUGIN = (AI / 'code/Plugin.cs').read_text()
HOOK = (AI / 'code/AdvanceRecovery.cs').read_text()
RUNTIME = (AI / 'code/AdvanceRecoveryRuntime.cs').read_text()


class AdvanceRecoveryWiringTests(unittest.TestCase):
    def test_candidate_is_on_by_default_version_locked_and_large_map_only(self):
        self.assertRegex(PLUGIN, r'AddToggle\("RecoverStalledAdvance",[^\n]*true\)')
        self.assertIn('!ReadBool("RecoverStalledAdvance", true)', HOOK)
        self.assertIn('!AdvanceRecoveryBuild.IsCurrentSupported()', HOOK)
        self.assertNotIn('ExtendedPathfinding', HOOK)
        self.assertIn('map.Width <= ZoneReferenceSize || map.Height <= ZoneReferenceSize', HOOK)

    def test_native_postprocess_and_choice_observer_are_wired(self):
        self.assertIn('PatchAdvanceRecovery(harmony);', PLUGIN)
        self.assertIn('typeof(ConsiderZones), "PostProcess"', HOOK)
        self.assertIn('typeof(Agent), "PickBehavior"', HOOK)
        self.assertIn('nameof(AdvanceRecoveryReset)', HOOK)
        self.assertIn('RuntimeHelpers.PrepareMethod', HOOK)
        self.assertIn('_advanceRecoveryEnabled = false;', HOOK)

    def test_no_extra_attack_path_search_or_contact_mutation(self):
        for forbidden in ('SetSleeping(', 'SetOrder(', 'FindPath(', 'IsUsableOn(',
                          'HasLineOfSight(', 'Task.Run(', '.Evaluate(', '.Execute(',
                          'ref bool __result', '.TTL =',
                          '.m_Score =', '.SafetyScore =', '.UtilityByAttacksScore ='):
            self.assertNotIn(forbidden, RUNTIME + HOOK)
        self.assertIn('ref Behavior __result', HOOK)
        self.assertIn('__result = advanceMove;', HOOK)
        self.assertIn('if (!faction.HasKnownOpponent())', RUNTIME)
        self.assertIn('state.MarkMoveFallbackEligible', RUNTIME)
        self.assertIn('HasMoveFallbackEligibility', HOOK)

    def test_force_move_reuses_native_legal_target_without_new_path_search(self):
        self.assertIn('TryGetAdvanceMove', RUNTIME)
        self.assertIn('GetTargetTile()', RUNTIME)
        self.assertIn('state.MarkMoveFallbackEligible', RUNTIME)
        self.assertIn('destination.APCost <= 0 || destination.APCost > ap', RUNTIME)
        for version in ('v0.7.17', 'v0.7.18'):
            dump = ROOT.parent.parent / 'game_files' / version / 'dump/dump.cs'
            with dump.open() as source:
                self.assertTrue(any('public TileScore GetTargetTile()' in line for line in source), version)

    def test_null_target_recovery_runs_before_native_utility_and_path_selection(self):
        self.assertIn('typeof(Move), "HasUtility"', HOOK)
        self.assertIn('prefix: new HarmonyMethod(typeof(Plugin), nameof(AdvanceRecoveryUtility))', HOOK)
        self.assertIn('TrySeedAdvanceUtility', RUNTIME)
        self.assertIn('float threshold = move.GetUtilityThreshold();', RUNTIME)
        self.assertIn('distance < currentDistance && score.SafetyScore >= currentScore.SafetyScore', RUNTIME)
        self.assertIn('threshold * (1f + (currentDistance - distance) / (float)currentDistance)', RUNTIME)
        self.assertIn('edit.Score.UtilityScore = edit.Recovered;', RUNTIME)
        self.assertIn('edit.Score.UtilityScore = edit.Utility;', RUNTIME)
        self.assertIn('ADVANCE-UTILITY round=', HOOK)

    def test_provisional_move_does_not_cancel_same_round_re_evaluation(self):
        self.assertIn('IsProvisionalAdvanceChoice(__instance, actor, __result)', HOOK)
        self.assertIn('idleOnly, provisionalAdvance, advanceActivity)', HOOK)
        self.assertIn('TryGetAdvanceDestination(agent, actor, out Move move) && chosen.Pointer == move.Pointer', RUNTIME)

    def test_rejection_diagnostics_are_opt_in_bounded_and_do_not_disable_gameplay(self):
        self.assertIn('out AdvanceRecoveryResult result', RUNTIME)
        self.assertIn('out bool diagnosticCandidate, out bool stalled', RUNTIME)
        self.assertIn('ShouldLogDiagnostic', HOOK)
        self.assertIn('ADVANCE-CHECK round=', HOOK)
        self.assertIn('ADVANCE-CHOICE round=', HOOK)
        self.assertIn('moveScore=', HOOK)
        self.assertIn('ReadBool("DiagnoseAdvanceRecovery", false)', HOOK)
        self.assertIn('Interlocked.Increment(ref _advanceRecoveryLogCount) <= 200', HOOK)
        self.assertIn('catch { }', HOOK)

    def test_larger_maps_no_longer_owns_recovery(self):
        self.assertNotIn('PatchAdvanceRecovery', (ROOT / 'src/LargerMaps/Plugin.cs').read_text())
        self.assertEqual(list((ROOT / 'src/LargerMaps').glob('AdvanceRecovery*.cs')), [])
        self.assertNotIn('ModSettings.Get("Larger Maps"', PLUGIN)

    def test_pre_path_recovery_does_not_read_uncomputed_ap_costs(self):
        self.assertNotIn('score.APCost', RUNTIME)
        self.assertIn('destination.APCost', RUNTIME)
        self.assertIn('progress |= distance < currentDistance;', RUNTIME)
        self.assertIn('ap <= 0', RUNTIME)
        self.assertIn('score.UtilityScore > 0', RUNTIME)

    def test_source_project_and_compiled_manifest_agree(self):
        package = ROOT.parent / 'jiangyu/LargerMaps'
        source = json.loads((package / 'jiangyu.json').read_text())
        compiled = json.loads((package / 'compiled/jiangyu.json').read_text())
        project = ET.parse(package / 'code/LargerMaps.csproj')
        self.assertEqual(source['version'], compiled['version'])
        self.assertEqual(source['version'], project.findtext('./PropertyGroup/Version'))


if __name__ == '__main__':
    unittest.main()
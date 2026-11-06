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
                          'ref bool __result', 'ref Behavior __result', '.TTL =',
                          '.m_Score =', '.SafetyScore =', '.UtilityByAttacksScore ='):
            self.assertNotIn(forbidden, RUNTIME + HOOK)
        self.assertIn('if (!faction.HasKnownOpponent()) return false;', RUNTIME)

    def test_rejection_diagnostics_are_opt_in_bounded_and_do_not_disable_gameplay(self):
        self.assertIn('out AdvanceRecoveryResult result', RUNTIME)
        self.assertIn('ADVANCE-CHECK round=', HOOK)
        self.assertIn('ADVANCE-CHOICE round=', HOOK)
        self.assertIn('ReadBool("DiagnoseAdvanceRecovery", false)', HOOK)
        self.assertIn('Interlocked.Increment(ref _advanceRecoveryLogCount) <= 200', HOOK)
        self.assertIn('catch { }', HOOK)

    def test_larger_maps_no_longer_owns_recovery(self):
        self.assertNotIn('PatchAdvanceRecovery', (ROOT / 'src/LargerMaps/Plugin.cs').read_text())
        self.assertEqual(list((ROOT / 'src/LargerMaps').glob('AdvanceRecovery*.cs')), [])
        self.assertNotIn('ModSettings.Get("Larger Maps"', PLUGIN)

    def test_pre_path_recovery_does_not_read_uncomputed_ap_costs(self):
        self.assertNotIn('score.APCost', RUNTIME)
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
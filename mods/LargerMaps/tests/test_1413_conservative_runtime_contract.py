"""1.5 source-path invariants; Unity/Harmony runtime behavior not simulated."""
import hashlib
import json
import math
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT/'src/LargerMaps/Plugin.cs').read_text()
CONSERVATIVE = (ROOT/'src/LargerMaps/ConservativeMissionAreas.cs').read_text()
REFERENCE = (ROOT/'src/LargerMaps/Reference64MissionAreas.cs').read_text()

class ConservativeRuntimeContractTest(unittest.TestCase):
    def test_reference_64_is_selected_and_not_rewritten(self):
        self.assertIn('string areaPrefix = nameof(Reference64MissionAreaAddToListPrefix);', PLUGIN)
        self.assertIn('useReference64Areas ? nameof(Reference64MissionAreaAddToListPostfix) : nameof(ConservativeLargeMissionAreaAddToListPostfix)',PLUGIN)
        self.assertIn('Reference64TryScaleMissionAreaRect',REFERENCE)
        self.assertIn('Version = "1.7.1"',PLUGIN)

    def test_no_broad_objective_init_hooks_installed(self):
        install=PLUGIN.split('private static void InstallHooks(',1)[1].split('private static void PatchMethod(',1)[0]
        self.assertNotIn('PatchAllObjectiveAreaScopes(harmony)', install)
        self.assertIn('PatchMissionAreaScaling(harmony)',install)
        self.assertIn('if (TargetSize > ZoneReferenceSize)\n                PatchFinalSpawnAreaSafety(harmony);',install)
        self.assertNotIn('PatchAllObjectiveAreaScopes(harmony);',PLUGIN)

    def test_legacy_scopes_only(self):
        code=PLUGIN.split('private static void PatchMissionAreaScaling(',1)[1].split('private static void PatchAllObjectiveAreaScopes(',1)[0]
        self.assertNotIn('PatchScopedMethod(harmony, _rescueUnitType',code)
        self.assertNotIn('PatchScopedMethod(harmony, _missionTemplateType, "CreateInitialAiUnits"',code)
        self.assertIn('_genericMissionTemplateType, "OnCreateInitialAiUnits"',code)
        self.assertIn('_reinforcementGroupType, "Init"',code)
        self.assertIn('_missionTemplateType, "CreateDeploymentZones"',code)
        self.assertIn('_killUnitType, "Init"',code)
        self.assertIn('_evacuateToAreaType, "Init"',code)
        self.assertIn('_recoverObjectType, "Init"',code)

    def test_only_scope_generated_rects_are_modified(self):
        self.assertIn('if (!enemyScope && !deploymentScope && !targetScope &&',CONSERVATIVE)
        self.assertIn('!evacuationScope && !recoverScope) return;',CONSERVATIVE)
        self.assertIn('if (areaType == 0 || areaType == 1)',CONSERVATIVE)
        self.assertIn('TryNormalizeSpawnRect(rect, mapW, mapH, 1,',CONSERVATIVE)
        self.assertIn('TryScaleReferenceBorderRect(rect, areaType, mapW,',CONSERVATIVE)
        self.assertIn('Reference64TryScaleMissionAreaRect(rect, areaType,',CONSERVATIVE)
        self.assertNotIn('GetObjectiveAreaRole(',CONSERVATIVE)
        self.assertNotIn('TryScaleResolvedAnchoredRect(',CONSERVATIVE)
        self.assertNotIn('TryScaleReferenceAuthoredRect(',CONSERVATIVE)

    def test_conservative_path_silent_without_debug(self):
        # All hot-path logs must explicitly require Debug().
        lines=[ln.strip() for ln in CONSERVATIVE.splitlines() if '_log.' in ln]
        self.assertEqual(len(lines),2)
        self.assertTrue(all('Debug()' in CONTEXT for CONTEXT in [CONSERVATIVE[CONSERVATIVE.rfind('if (Debug())',0,CONSERVATIVE.index(line)-5):CONSERVATIVE.index(line)] for line in lines]))
        self.assertNotIn('Task.Run(',CONSERVATIVE)
        self.assertNotIn('new HarmonyMethod(',CONSERVATIVE)

    def test_version_consistency(self):
        meta=json.loads((ROOT/'modpack/LargerMaps/modpack.json').read_text())
        self.assertEqual(meta['version'],'1.7.1')
        self.assertIn('<Version>1.7.1</Version>',(ROOT/'src/LargerMaps/LargerMaps.csproj').read_text())
        self.assertIn('Larger Maps 1.7.1',(ROOT/'build-and-deploy.bat').read_text())

    def test_pathfinder_and_profiler_not_modified(self):
        old=Path('/mnt/data/lm_work/v1412/LargerMaps-1.4.12-area-regression-diagnostic-authoring/src/LargerMaps')
        if old.exists():
            for name in ('ExtendedPathfinding.cs','AIProfiling.cs','Reference64MissionAreas.cs'):
                self.assertEqual(hashlib.sha256((old/name).read_bytes()).digest(),
                                 hashlib.sha256((ROOT/'src/LargerMaps'/name).read_bytes()).digest())
        self.assertIn('PatchAiTaskLifetime(harmony);',PLUGIN)
        self.assertIn('ExtendedPathfinding.TryInstall(requested',PLUGIN)

if __name__ == '__main__': unittest.main()

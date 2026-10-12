"""Source contract for 1.5 candidate; no IL2CPP runtime validation."""
import unittest
import hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
src=(root/'src/LargerMaps/Plugin.cs').read_text()
legacy=(root/'src/LargerMaps/Reference64MissionAreas.cs').read_text()

class ConservativeAreaReferenceTest(unittest.TestCase):
    def test_selector_installs_only_legacy_area_handlers_on_64(self):
        self.assertIn('bool useReference64Areas = TargetSize <= ZoneReferenceSize;',src)
        self.assertIn('string areaPrefix = nameof(Reference64MissionAreaAddToListPrefix);',src)
        self.assertIn('useReference64Areas ? nameof(Reference64MissionAreaAddToListPostfix) : nameof(ConservativeLargeMissionAreaAddToListPostfix)',src)
        self.assertNotIn('PatchScopedMethod(harmony, _missionTemplateType, "CreateInitialAiUnits"',src)
        self.assertNotIn('PatchScopedMethod(harmony, _rescueUnitType',src)
        self.assertIn('if (TargetSize > ZoneReferenceSize)\n                PatchFinalSpawnAreaSafety(harmony);',src)
    def test_reference_handlers_exist_and_are_separate(self):
        self.assertIn('void Reference64MissionAreaAddToListPrefix(',legacy)
        self.assertIn('void Reference64MissionAreaAddToListPostfix(',legacy)
        self.assertIn('bool Reference64TryScaleMissionAreaRect(',legacy)
        self.assertIn('TryEnsureMinimumAreaRect(',legacy)
        self.assertIn('Math.Round(x * scaleX)',legacy)
        self.assertIn('Math.Round(y * scaleY)',legacy)
        reference = legacy.split('    private static void Reference64MissionAreaAddToListPrefix(',1)[1].rsplit('\n}',1)[0]+'\n'
        self.assertEqual(hashlib.sha256(reference.encode('utf-8')).hexdigest(),
                         'da8cb24740212abc41c342d795d7091f56f06fc0416b4b9cb62b6b24f6095ccd')
    def test_safety_and_enlarged_mission_areas_still_exist(self):
        self.assertIn('PatchTerrainHoleIndexing(harmony);',src)
        self.assertIn('PatchAiTaskLifetime(harmony);',src)
        self.assertIn('PatchMissionAreaScaling(harmony);',src)
        self.assertNotIn('PatchAllObjectiveAreaScopes(harmony);',src)
        self.assertIn('Version = "1.6.1"',src)
if __name__=='__main__': unittest.main()

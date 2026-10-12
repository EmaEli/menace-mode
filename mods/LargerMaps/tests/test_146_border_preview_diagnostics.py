"""1.4.7 regression guards: don't double-transform MENACE native border zones.

Static checks only. The IL2CPP hook cannot be exercised without the game runtime.
"""
from pathlib import Path
import re
import json
import unittest

ROOT = Path(__file__).resolve().parents[1]
SRC = (ROOT/'src/LargerMaps/Plugin.cs').read_text()
MANIFEST = json.loads((ROOT/'modpack/LargerMaps/modpack.json').read_text())

class PreviewGeometry147Tests(unittest.TestCase):
    def test_version_alignment(self):
        self.assertEqual(MANIFEST['version'], '1.7.1')
        self.assertIn('Version = "1.7.1"', SRC)
        self.assertIn('<Version>1.7.1</Version>', (ROOT/'src/LargerMaps/LargerMaps.csproj').read_text())
        self.assertIn('Larger Maps 1.7.1', (ROOT/'build-and-deploy.bat').read_text())

    def test_only_native_border_short_axis_is_scaled_after_bounds_fix(self):
        body = (ROOT/'src/LargerMaps/ConservativeMissionAreas.cs').read_text()
        self.assertIn('TryScaleReferenceBorderRect(rect, areaType, mapW,', body)
        self.assertIn('MissionTemplate.GetBounds native patch has already extended', body)
        self.assertIn('Only adjust its short border thickness', body)
        self.assertIn('ScaleBorderThickness(spanW, mapWidth)', SRC)
        self.assertIn('ScaleBorderThickness(spanH, mapHeight)', SRC)
        self.assertIn('nativeThickness * (double)ZoneReferenceSize / VanillaSize', SRC)

    def test_64_reference_areas_are_logged_without_modification(self):
        self.assertIn('if (!enlarged && !inSpawnOrDeployment)', SRC)
        self.assertIn('unmodified 64-reference objective area', SRC)
        self.assertIn('LogUnmodifiedAreaResult(list, __state, mapW, mapH, areaType, role,', SRC)
        self.assertIn('no new rectangles', SRC)

    def test_role_unknown_and_unscoped_calls_are_visible(self):
        self.assertIn('unresolved objective role or unscoped area', SRC)
        self.assertIn('AREA DIAG entered', SRC)
        self.assertIn('AREA DIAG result', SRC)
        self.assertIn('AREA DIAG native-only', SRC)

    def test_no_verbose_logging_by_default_and_cap(self):
        self.assertIn('AddToggle("DiagnoseMissionAreas",',SRC)
        self.assertRegex(SRC, r'AddToggle\("DiagnoseMissionAreas",[^\n]+false\)')
        self.assertIn('private static bool AreaDiagnostics() => ReadBool("DiagnoseMissionAreas", false);', SRC)
        self.assertIn('MaxAreaGeometryDiagnosticLines = 160;',SRC)
        self.assertIn('_areaGeometryDiagnosticLines = 0;',SRC)

    def test_existing_ai_safety_is_preserved(self):
        self.assertIn('PatchAiTaskLifetime(harmony)',SRC)
        self.assertIn('ExtendedPathfinding.TryInstall(requested',SRC)
        self.assertIn('PatchTerrainHoleIndexing(harmony)',SRC)
        self.assertNotIn('PatchAllObjectiveAreaScopes(harmony)',SRC)

if __name__ == '__main__': unittest.main()

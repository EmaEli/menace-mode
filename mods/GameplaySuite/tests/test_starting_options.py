"""Static authoring checks for MENACE Starting Options.

Tests verify build/package wiring and native symbol grounding, but do not replace
building the C# module and testing it inside MENACE on Windows.
"""
from pathlib import Path
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


class StartingOptionsAuthoringTests(unittest.TestCase):
    def setUp(self):
        self.plugin = (ROOT / 'src/StartingOptions/Plugin.cs').read_text(encoding='utf8')
        self.script = (ROOT / 'tools/Build.ps1').read_text(encoding='utf8')
        self.manifest = json.loads((ROOT / 'packaging/standalone/StartingOptions/modpack.json').read_text(encoding='utf8'))
        self.suite = json.loads((ROOT / 'packaging/suite/MenaceGameplaySuite/modpack.json').read_text(encoding='utf8'))

    def test_new_module_packaging(self):
        self.assertEqual(self.suite['version'], '0.32')
        self.assertEqual(self.manifest['version'], '0.1')
        self.assertEqual(self.manifest['code']['prebuiltDlls'], ['StartingOptions.dll'])
        self.assertEqual(self.manifest['patches'], {})
        self.assertIn('Folder = "StartingOptions"', self.script)
        self.assertIn('Assembly = "StartingOptions.dll"', self.script)
        self.assertTrue((ROOT / 'src/StartingOptions/StartingOptions.csproj').is_file())

    def test_leaders_slider_scope(self):
        self.assertIn('nameof(StartingLeaderSliderPrefix)', self.plugin)
        self.assertIn('int __1, ref int __2', self.plugin)
        self.assertIn('__1 != 1 || __2 != VanillaLeaderMaximum', self.plugin)
        self.assertIn('MaximumSupportedLeaders = 12;', self.plugin)
        self.assertIn('InitialPickableUnitLeaders', self.plugin)

    def test_resources_checkbox_and_defaults(self):
        self.assertIn('VanillaHeadstartCount = 36;', self.plugin)
        self.assertIn('MaximumHeadstartCount = 1000;', self.plugin)
        self.assertIn('"HeadstartSparePartsCount"', self.plugin)
        self.assertIn('"m_BlackMarketHeadstartToggle"', self.plugin)
        self.assertIn('if (!headstartToggle.GetValue()) return;', self.plugin)
        self.assertIn('commodity.spare_parts', self.plugin)
        self.assertIn('initialItems.Add(sparePartsTemplate);', self.plugin)
        self.assertIn('initialItems.RemoveAt(i);', self.plugin)

    def test_no_tactical_hooks_or_log_spam(self):
        self.assertRegex(self.plugin, r'public void OnUpdate\(\) \{ \}')
        self.assertIn('"DebugLogging", "Enable diagnostic logging (OFF by default)", false', self.plugin)
        self.assertNotIn('AttackHandler', self.plugin)
        self.assertNotIn('OnRoundStart', self.plugin)

    def test_code_structure(self):
        self.assertEqual(self.plugin.count('{'), self.plugin.count('}'))
        self.assertEqual(self.plugin.count('('), self.plugin.count(')'))
        self.assertEqual(self.plugin.count('['), self.plugin.count(']'))


if __name__ == '__main__':
    unittest.main()

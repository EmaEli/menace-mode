"""Static authoring checks for MENACE Starting Options.

Tests verify build/package wiring and native symbol grounding, but do not replace
building the C# module and testing it inside MENACE on Windows.
"""
from pathlib import Path
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT.parent / 'jiangyu/StartingOptions'


class StartingOptionsAuthoringTests(unittest.TestCase):
    def setUp(self):
        self.plugin = (ROOT / 'src/StartingOptions/Plugin.cs').read_text(encoding='utf8')
        self.csproj = (PACKAGE / 'code/StartingOptions.csproj').read_text(encoding='utf8')
        self.manifest = json.loads((PACKAGE / 'jiangyu.json').read_text(encoding='utf8'))

    def test_new_module_packaging(self):
        self.assertEqual(self.manifest['version'], '0.1.0')
        self.assertIn('<Version>0.1.0</Version>', self.csproj)
        self.assertIn('<AssemblyName>StartingOptions</AssemblyName>', self.csproj)
        self.assertTrue(any(d.startswith('MenaceModSettings') for d in self.manifest['depends']))
        # Code-only module: no template patches.
        self.assertFalse((PACKAGE / 'templates').exists())

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

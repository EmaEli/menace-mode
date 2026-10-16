"""Source/packaging guards for #38; actual sprite resolution needs the running game."""
import json
import re
import unittest
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
PLUGIN = ROOT / "mods/GameplaySuite/src/FieldLogistics/Plugin.cs"
PACKAGE = ROOT / "mods/jiangyu/FieldLogistics"


class FieldLogisticsIconTests(unittest.TestCase):
    def test_both_actions_request_the_cartridge_icon(self):
        source = PLUGIN.read_text()
        self.assertIn('ApplySkillIcons(vehicleSkill, "skill_05")', source)
        self.assertIn('ApplySkillIcons(shareSkill, "skill_05")', source)
        self.assertNotIn('"skill_28"', source)

    def test_active_disabled_pair_and_retry_are_preserved(self):
        source = PLUGIN.read_text()
        helper = source.split("private static bool ApplySkillIcons", 1)[1].split("private static void TryGrantTestLoadout", 1)[0]
        self.assertIn('SpriteLookup.Find(spriteName + "_inactive")', helper)
        self.assertIn("icon == null || iconDisabled == null", helper)
        self.assertIn("return false;", helper)
        self.assertIn('Templates.WriteField(skill, "Icon", icon)', helper)
        self.assertIn('Templates.WriteField(skill, "IconDisabled", iconDisabled)', helper)
        self.assertIn("_skillIconsApplied = ok;", source)

    def test_version_surfaces_agree(self):
        version = json.loads((PACKAGE / "jiangyu.json").read_text())["version"]
        self.assertEqual(version, "1.0.11")
        self.assertEqual(ET.parse(PACKAGE / "code/FieldLogistics.csproj").findtext("PropertyGroup/Version"), version)
        self.assertEqual(re.search(r'private const string Version = "([^"]+)"', PLUGIN.read_text()).group(1), version)

    def test_experimental_and_diagnostic_defaults_remain_off(self):
        source = PLUGIN.read_text()
        for key in ("EnableAmmoSharing", "GrantTestLoadout", "DebugLogging"):
            self.assertRegex(source, r'settings\.AddToggle\("' + key + r'", [^\n]+, false\);')
        self.assertIn('settings.AddNumber("VehicleRefillAPCost", "Refill AP cost", 0, 100, 60)', source)
        self.assertIn('settings.AddNumber("VehicleRefillCharges", "Extra refill uses from Ammo Supply module", 1, 12, 4)', source)


if __name__ == "__main__":
    unittest.main()
"""MEN-024 static/source-data checks, NOT an in-game behavior test.

Run: python -m unittest discover -s mods/GameplaySuite/tests -p test_overwatch_indirect_fire.py
Optional game-export verification:
    MENACE_EXTRACTED_DATA=/path/to/ExtractedData python -m unittest ...
"""

import json
import os
import re
import unittest
from pathlib import Path


SUITE = Path(__file__).resolve().parents[1]
NATIVE = SUITE / "src" / "Overwatch" / "Native.cs"
PLUGIN = SUITE / "src" / "Overwatch" / "Plugin.cs"
MANIFEST = SUITE / "packaging" / "standalone" / "Overwatch" / "modpack.json"


def indirect_method():
    source = NATIVE.read_text(encoding="utf-8")
    start = source.index("public static bool IsIndirectFire(Skill skill)")
    end = source.index("private static bool SupportedBase(Skill skill)", start)
    return source[start:end]


class TestOverwatchIndirectFire(unittest.TestCase):
    def test_only_selected_attack_skill_and_direct_item_tags_are_checked(self):
        method = indirect_method()
        self.assertIn("skill.GetTemplate()", method)
        self.assertIn("attackTemplate.Tags", method)
        self.assertRegex(method, r"tag\.TagType\s*==\s*TagType\.INDIRECT_FIRE")
        self.assertRegex(
            method,
            r"itemTemplate\.HasTag\(TagType\.INDIRECT_FIRE,\s*false\)",
        )
        # The old one-argument call recursively checked the other skills on
        # a hybrid gun, wrongly treating its normal rifle attacks as indirect.
        self.assertNotRegex(method, r"\.HasTag\(TagType\.INDIRECT_FIRE\s*\)")

    def test_indirect_fire_exclusion_remains_in_all_eligibility_paths(self):
        native = NATIVE.read_text(encoding="utf-8")
        self.assertGreaterEqual(native.count("IsIndirectFire(skill)"), 4)
        self.assertIn("catch { return true; } // Fail closed", indirect_method())

    def test_patch_versions_match(self):
        plugin = PLUGIN.read_text(encoding="utf-8")
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(manifest["version"], "0.32.3.4")
        for marker in ("AssemblyVersion", "AssemblyFileVersion", "AssemblyInformationalVersion"):
            self.assertIn(f'[{marker}("0.32.3.4")]', plugin)
        self.assertIn('private const string Version = "0.32.3.4";', plugin)

    def test_extracted_weapon_variants_have_skill_scoped_indirect_tags(self):
        base = os.getenv("MENACE_EXTRACTED_DATA")
        if not base:
            self.skipTest("MENACE_EXTRACTED_DATA not set; optional game-data verification")
        root = Path(base)
        weapons = {x["m_ID"]: x for x in json.loads((root / "WeaponTemplate.json").read_text(encoding="utf-8"))}
        skills = {x["m_ID"]: x for x in json.loads((root / "SkillTemplate.json").read_text(encoding="utf-8"))}

        rifle = weapons["weapon.generic_assault_rifle_tier1_kpac_grenade_launcher"]
        self.assertEqual(rifle["SlotType"], 0)
        self.assertEqual(rifle["Tags"], [])
        for skill_id in (
            "active.fire_assault_rifle_tier1_556",
            "active.fire_assault_rifle_sustained_fire_tier1_556",
        ):
            self.assertIn(skill_id, rifle["SkillsGranted"])
            self.assertNotIn("indirect_fire", skills[skill_id]["Tags"])
        grenade_id = "active.launch_underbarrel_frag_grenade"
        self.assertIn(grenade_id, rifle["SkillsGranted"])
        self.assertIn("indirect_fire", skills[grenade_id]["Tags"])

        # ARC Range and PPP variants are direct-fire in this export. The
        # reported PPP CQB name is NOT present here; this is not a fix claim.
        for name in (
            "weapon.generic_assault_rifle_tier1_ARC_762_ACC",
            "weapon.generic_smg_tier1_ppp_HiCap",
        ):
            weapon = weapons[name]
            self.assertEqual(weapon["SlotType"], 0)
            self.assertTrue(weapon["SkillsGranted"])
            self.assertTrue(all("indirect_fire" not in skills[s]["Tags"] for s in weapon["SkillsGranted"]))

        # A mixed-mode vehicle ATGM is another regression case.
        atgm = weapons["mod_weapon.medium.atgm_launcher"]
        self.assertNotIn("indirect_fire", skills["active.mod.atgm.direct_fire"]["Tags"])
        self.assertIn("indirect_fire", skills["active.mod.atgm.indirect_fire"]["Tags"])
        self.assertIn("active.mod.atgm.direct_fire", atgm["SkillsGranted"])


if __name__ == "__main__":
    unittest.main()

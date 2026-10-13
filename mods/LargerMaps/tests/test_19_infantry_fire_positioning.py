"""MEN-002: cheap, OFF-by-default infantry firing-position A/B experiment."""
import json
import re
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMBAT = (ROOT / "src/LargerMaps/SimplifiedCombatPlanning.cs").read_text()
PLUGIN = (ROOT / "src/LargerMaps/Plugin.cs").read_text()
MANIFEST = json.loads((ROOT / "modpack/LargerMaps/modpack.json").read_text())
CSPROJ = (ROOT / "src/LargerMaps/LargerMaps.csproj").read_text()


class TestSelectiveInfantryFirePositioning(unittest.TestCase):
    def test_opt_in_large_maps_and_native_guard_remain(self):
        self.assertIn('TargetSize > ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true)', COMBAT)
        self.assertIn('ReadBool("PreserveBasicInfantryFirePositioning", false)', COMBAT)
        self.assertIn('AddToggle("PreserveBasicInfantryFirePositioning"', PLUGIN)
        self.assertIn('OFF; restart required)", false)', PLUGIN)
        self.assertIn('ApplySimplifiedCombatPlanning();', PLUGIN)
        self.assertIn('RestoreSimplifiedCombatPlanning();', PLUGIN)

    def test_small_static_budget_and_profile_identity(self):
        block = COMBAT.split('CandidateInfantryFireSkills =', 1)[1].split('};', 1)[0]
        chosen = re.findall(r'"(active\.[^"]+)"', block)
        self.assertEqual(len(chosen), 5)
        self.assertEqual(len(chosen), len(set(chosen)))
        self.assertIn("active.fire_pirate_assault_rifle", chosen)
        self.assertIn("active.fire_pirate_pdw", chosen)
        self.assertIn("active.fire_assault_rifle_tier1_556", chosen)
        self.assertIn('GetMember(profile, "Pointer")', COMBAT)
        self.assertIn('_preservedInfantryFireProfiles.Contains(NativeProfilePointer(config))', COMBAT)
        self.assertLess(COMBAT.index('CollectPreservedInfantryFireProfiles(getById)'),
                        COMBAT.index('foreach (object template in EnumerateSkillTemplates(templates))'))
        self.assertIn('_preservedInfantryFireProfiles.Clear();', COMBAT)
        self.assertIn('if (!isAttack && !isBuff) return;', COMBAT)

        archive = Path("/mnt/data/ExtractedData(1).zip")
        if archive.exists():
            with zipfile.ZipFile(archive) as z:
                skills = json.loads(z.read("ExtractedData/SkillTemplate.json"))
            by_id = {skill["name"]: skill for skill in skills}
            for identifier in chosen:
                with self.subTest(identifier=identifier):
                    skill = by_id[identifier]
                    self.assertTrue(skill.get("IsAttack"))
                    self.assertTrue(skill.get("IsLineOfFireNeeded"))
                    self.assertTrue((skill.get("AIConfig") or {}).get("InfluencesPositioning"))

    def test_no_new_hot_ai_hooks_or_routing(self):
        for forbidden in ("FindPath(", "Thread.Sleep", "Task.Run", "Harmony.Patch", "OnUpdate(",
                          "OnRoundStart(", "OnTurnStart(", "Move.OnEvaluate"):
            self.assertNotIn(forbidden, COMBAT)

    def test_versions(self):
        self.assertIn('private const string Version = "1.9";', PLUGIN)
        self.assertEqual(MANIFEST["version"], "1.9")
        self.assertIn("<Version>1.9</Version>", CSPROJ)
        self.assertIn("<AssemblyVersion>1.9.0.0</AssemblyVersion>", CSPROJ)
        self.assertIn("<FileVersion>1.9.0.0</FileVersion>", CSPROJ)


if __name__ == "__main__":
    unittest.main()

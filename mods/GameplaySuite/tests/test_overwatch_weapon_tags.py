"""Source invariants for #24. Does not execute native IL2CPP tag lookup or reaction fire."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/Overwatch/Native.cs").read_text()
INDIRECT = SOURCE.split("public static bool IsIndirectFire(Skill skill)", 1)[1].split(
    "private static bool SupportedBase", 1)[0]


class OverwatchWeaponTagTests(unittest.TestCase):
    def test_exported_kpac_data_reproduces_sibling_tag_contamination(self):
        # Data-model check, not a native HasTag invocation. Fixture contains only the
        # exact tag/granted-skill fields extracted from the available asset snapshot.
        fixture = json.loads((ROOT / "tests/fixtures/overwatch_kpac_tags.json").read_text())
        attacks = fixture["attacks"]
        old_recursive_item_result = any("indirect_fire" in s["tags"] for s in attacks)
        self.assertTrue(old_recursive_item_result)  # Previously rejects every rifle mode.
        excluded = ["indirect_fire" in s["tags"] or "indirect_fire" in fixture["direct_item_tags"] for s in attacks]
        self.assertEqual(excluded, [False, False, True])

    def test_checks_executed_skill_before_direct_item_tags(self):
        self.assertIn("var attackTemplate = skill.GetTemplate();", INDIRECT)
        self.assertIn("var tags = attackTemplate.Tags;", INDIRECT)
        self.assertIn("tag.TagType == TagType.INDIRECT_FIRE", INDIRECT)
        self.assertIn("itemTemplate.HasTag(TagType.INDIRECT_FIRE, false)", INDIRECT)
        self.assertNotIn("HasTag(TagType.INDIRECT_FIRE)", INDIRECT)
        self.assertLess(INDIRECT.index("attackTemplate.Tags"), INDIRECT.index("skill.GetItem()"))

    def test_metadata_errors_fail_closed_but_native_itemless_attacks_are_allowed(self):
        self.assertIn("if (!Valid(skill)) return true;", INDIRECT)
        self.assertIn("if (!Valid(attackTemplate)) return true;", INDIRECT)
        self.assertIn("if (!Valid(itemTemplate)) return true;", INDIRECT)
        self.assertIn("catch { return true; }", INDIRECT)
        self.assertIn("if (!Valid(item)) return false;", INDIRECT)

    def test_no_speculative_source_skill_or_role_fallback(self):
        self.assertNotIn("SourceSkill", SOURCE)
        supported = SOURCE.split("private static bool SupportedBase", 1)[1].split("public static List<Skill> Attacks", 1)[0]
        for gate in ("IsIndirectFire(skill)", "skill.IsAttack()", "skill.IsActive()", "skill.IsTargeted()",
                     "!skill.IsMovementSkill()", "!skill.IsBackgroundOnly()", "Role(skill) != WeaponRole.Unknown"):
            self.assertIn(gate, supported)


if __name__ == "__main__":
    unittest.main()
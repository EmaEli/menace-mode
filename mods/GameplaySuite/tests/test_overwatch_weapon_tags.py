"""Source invariants for #24. Does not execute native IL2CPP tag lookup or reaction fire."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/Overwatch/Native.cs").read_text()
CHAIN = (ROOT / "src/Overwatch/SkillSourceChain.cs").read_text()
INDIRECT = SOURCE.split("public static bool IsIndirectFire(Skill skill)", 1)[1].split(
    "private static bool SupportedBase", 1)[0]
ITEM_METADATA = SOURCE.split("private static bool HasIndirectFireMetadata", 1)[1].split(
    "private static bool TryFindWeaponItemSkill", 1)[0]


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

    def test_checks_every_source_skill_and_its_direct_item_tags(self):
        self.assertIn("var template = skill.GetTemplate();", ITEM_METADATA)
        self.assertIn("var tags = template.Tags;", ITEM_METADATA)
        self.assertIn("tag.TagType == TagType.INDIRECT_FIRE", ITEM_METADATA)
        self.assertIn("itemTemplate.HasTag(TagType.INDIRECT_FIRE, false)", ITEM_METADATA)
        self.assertNotIn("HasTag(TagType.INDIRECT_FIRE)", ITEM_METADATA)
        self.assertLess(ITEM_METADATA.index("template.Tags"), ITEM_METADATA.index("skill.GetItem()"))

    def test_metadata_errors_fail_closed_but_native_itemless_attacks_are_allowed(self):
        self.assertIn("if (!Valid(skill)) return true;", ITEM_METADATA)
        self.assertIn("if (!Valid(template)) return true;", ITEM_METADATA)
        self.assertIn("if (!Valid(itemTemplate)) return true;", ITEM_METADATA)
        self.assertIn("if (!Valid(item)) return false;", ITEM_METADATA)
        self.assertIn("return !TryFindWeaponItemSkill(skill, out _, out bool unsafeChain) || unsafeChain;", INDIRECT)

    def test_source_skill_fallback_is_bounded_cycle_guarded_and_role_limited(self):
        self.assertIn("current => current.SourceSkill", SOURCE)
        self.assertIn("MaximumDepth = 8", CHAIN)
        self.assertIn("Span<long> visited = stackalloc long[MaximumDepth]", CHAIN)
        self.assertIn("visited[i] != pointer", CHAIN)
        self.assertIn("unsafeChain || !Valid(itemSkill)", SOURCE)
        self.assertIn("return !TryFindWeaponItemSkill(skill, out _, out bool unsafeChain) || unsafeChain;", SOURCE)
        supported = SOURCE.split("private static bool SupportedBase", 1)[1].split("public static List<Skill> Attacks", 1)[0]
        for gate in ("IsIndirectFire(skill)", "skill.IsAttack()", "skill.IsActive()", "skill.IsTargeted()",
                     "!skill.IsMovementSkill()", "!skill.IsBackgroundOnly()", "Role(skill) != WeaponRole.Unknown"):
            self.assertIn(gate, supported)


if __name__ == "__main__":
    unittest.main()
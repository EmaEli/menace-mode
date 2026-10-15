"""MEN-005 / #41 / #31 authoring regression checks for Infantry Assault.

These tests verify source scoping, index validation, and packaging metadata.
They cannot replace .NET compilation or in-game testing on MENACE IL2CPP.
"""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT.parent / "jiangyu/InfantryAssault"
PLUGIN = (ROOT / "src/InfantryAssault/Plugin.cs").read_text(encoding="utf-8")
MANIFEST = json.loads((PACKAGE / "jiangyu.json").read_text())
CSPROJ = (PACKAGE / "code/InfantryAssault.csproj").read_text(encoding="utf-8")


def between(start, end):
    assert start in PLUGIN and end in PLUGIN, (start, end)
    return PLUGIN.split(start, 1)[1].split(end, 1)[0]


class AssaultAimCacheTests(unittest.TestCase):
    def test_patch_versions(self):
        version = MANIFEST["version"]
        self.assertIn(f'AssemblyVersion("{version}.0")', PLUGIN)
        self.assertIn(f'AssemblyFileVersion("{version}.0")', PLUGIN)
        self.assertIn(f'private const string Version = "{version}";', PLUGIN)
        self.assertIn(f"<Version>{version}</Version>", CSPROJ)

    def test_requested_icon_pair(self):
        self.assertIn('SpriteLookup.Find("skill_41")', PLUGIN)
        self.assertIn('SpriteLookup.Find("skill_41_inactive")', PLUGIN)
        self.assertNotIn('SpriteLookup.Find("skill_32', PLUGIN)
        self.assertIn('SetMember(_assaultTemplate, "Icon", icon)', PLUGIN)
        self.assertIn('SetMember(_assaultTemplate, "IconDisabled", iconDisabled)', PLUGIN)

    def test_no_patch_on_members_removed_in_menace_0_7_16(self):
        self.assertNotIn('"GetDescription"', PLUGIN)
        self.assertNotIn('"GetSkillType"', PLUGIN)
        self.assertNotIn("SkillGetDescriptionPostfix", PLUGIN)
        self.assertIn("was not resolved for {prefix ?? postfix}", PLUGIN)

    def test_only_assault_mouse_move_is_intercepted(self):
        prefix = between(
            "public static bool SkillActionHandleMouseMovePrefix(object __instance)",
            "private static bool PrepareAssaultAimElements(object skill)",
        )
        self.assertIn("if (!IsAssaultSkill(skill)) return true;", prefix)
        self.assertIn("Key(__instance) == _blockedPlayerAssaultActionKey.Value", prefix)
        self.assertIn("return PrepareAssaultAimElements(skill!);", prefix)
        self.assertNotIn("ActorAimAtVectorPrefix", PLUGIN)
        self.assertNotIn('Patch(Method(_actorType, "AimAt"', PLUGIN)

    def test_native_cache_only_checked_when_actor_changes(self):
        guard = between(
            "private static bool PrepareAssaultAimElements(object skill)",
            "public static bool SkillActionHandleLeftClickPrefix",
        )
        self.assertIn("ValidatedAssaultAimCounts.TryGetValue(skillPtr, out var validated)", guard)
        self.assertIn("validated.Actor == actorPtr && validated.Count == liveCount", guard)
        self.assertIn("GetMember(elements, \"Count\")", guard)
        self.assertIn("_skillGetExecutingElementsCached.Invoke(skill,", guard)
        self.assertIn("cacheCount != liveCount", guard)
        self.assertIn("if (index < 0 || index >= liveCount)", guard)
        self.assertEqual(PLUGIN.count("ValidatedAssaultAimCounts.Clear();"), 2)

    def test_refresh_uses_native_selection_and_same_list(self):
        guard = between(
            "private static bool PrepareAssaultAimElements(object skill)",
            "public static bool SkillActionHandleLeftClickPrefix",
        )
        self.assertIn("_skillGetExecutingElements.Invoke(skill,", guard)
        self.assertIn('Method(cached.GetType(), "Clear", 0)', guard)
        self.assertIn('Method(cached.GetType(), "Add", 1)', guard)
        self.assertIn("clear.Invoke(cached, null);", guard)
        self.assertIn("add.Invoke(cached, new object[] { index });", guard)
        self.assertLess(guard.index("replacement.Add(index);"), guard.index("clear.Invoke(cached, null);"))
        self.assertNotIn("new List<int>(liveCount)", guard)

    def test_fail_closed_and_debug_opt_in(self):
        guard = between(
            "private static bool PrepareAssaultAimElements(object skill)",
            "public static bool SkillActionHandleLeftClickPrefix",
        )
        self.assertIn("if (_verbose && !_assaultAimGuardWarned)", guard)
        self.assertEqual(guard.count("Warn("), 1)
        self.assertIn("return false;", guard)
        self.assertIn('s.AddToggle("VerboseLogging", "Debug logging", false);', PLUGIN)

    def test_no_combat_rule_or_ai_hook_changed(self):
        self.assertIn("private const int AssaultApCost = 40;", PLUGIN)
        self.assertIn("Patch(_agentExecute, prefix: nameof(AgentExecutePrefix));", PLUGIN)
        self.assertIn("Patch(Method(_skillActionType, \"HandleMouseMoveOnTile\", 4), prefix: nameof(SkillActionHandleMouseMovePrefix));", PLUGIN)


if __name__ == "__main__":
    unittest.main()

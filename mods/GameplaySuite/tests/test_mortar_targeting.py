"""Source-wiring guards only; native hooks and visible rendering require a game test."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()
OVERLAY = (ROOT / "src/WeaponMechanicsRebalanced/MortarTargetingOverlay.cs").read_text()


class MortarTargetingTests(unittest.TestCase):
    def test_shot_and_preview_use_same_target_lookup(self):
        self.assertIn("GetMortarTargetScatter(__instance, actor, __0, originalScatter)", PLUGIN)
        self.assertIn("GetMortarTargetScatter(skill, actor, target, template.Scatter)", OVERLAY)
        self.assertIn("MortarRangingKey(skill, actor)", OVERLAY)
        self.assertIn("solutions.TryGetValue((target.GetX(), target.GetZ()), out ranging)", OVERLAY)
        self.assertIn("return GetMortarEffectiveScatter(ranging, baseScatter)", OVERLAY)
        self.assertIn("MortarScatterRules.EffectiveScatter", PLUGIN)

    def test_preview_works_before_any_shots_without_writing_gameplay_state(self):
        self.assertNotIn("MortarRangingSolutions.Count", OVERLAY)
        self.assertNotIn(".Scatter =", OVERLAY)
        self.assertNotIn(".Shots++", OVERLAY)
        self.assertNotIn(".ScatterChance", OVERLAY)
        self.assertNotIn(".Scatter(", OVERLAY)
        self.assertIn("state.GetCurrentTile()", OVERLAY)
        self.assertIn("!skill.IsInRange(target, actor.GetTile())", OVERLAY)
        self.assertIn("template.IsDeploymentRequired && !actor.IsDeployed()", OVERLAY)

    def test_preview_geometry_and_label_are_distinct_from_blast_aoe(self):
        self.assertIn("MortarScatterRules.BorderEdges(dx, dz, scatter)", OVERLAY)
        self.assertIn('scatter, color, "Spread "', OVERLAY)
        for bit in (1, 2, 4, 8):
            self.assertIn(f"== null) edges |= {bit};", OVERLAY)
        self.assertNotIn("GetAffectedTiles", OVERLAY)
        self.assertNotIn("TileHighlighter", OVERLAY)
        self.assertNotIn("DelayedAoE", PLUGIN + OVERLAY)

    def test_gui_state_is_neutralized_and_restored(self):
        on_gui = PLUGIN.split("public void OnGUI()", 1)[1].split("private static IEnumerator", 1)[0]
        for prop, previous in (("color", "oldColor"), ("contentColor", "oldContentColor"),
                               ("backgroundColor", "oldBackgroundColor"),
                               ("matrix", "oldMatrix"), ("depth", "oldDepth"), ("enabled", "oldEnabled")):
            self.assertIn(f"GUI.{prop} = {previous};", on_gui.split("finally", 1)[1])
        self.assertIn("GUI.matrix = Matrix4x4.identity", on_gui)
        self.assertIn("GUI.color = Color.white", on_gui)
        self.assertIn("DrawMortarRangingOverlay();", on_gui)
        self.assertIn("DrawMortarScatterPreview();", on_gui)
        self.assertIn("Color(0f, 0f, 0f, 0.95f)", PLUGIN)

    def test_selected_weapon_cannot_fall_back_to_another_weapons_markers(self):
        remembered = PLUGIN.split("private static void DrawMortarRangingOverlay()", 1)[1]
        remembered = remembered.split("private static void EnsureMortarOverlayStyles()", 1)[0]
        self.assertIn("GetMortarDisplayActor(manager, selectedSkill)", remembered)
        self.assertIn("if (selectedSkill == null)", remembered)
        self.assertNotIn("if (solutionMaps.Count == 0)\n            {", remembered)
        self.assertIn("pair.Key != selectedKey", PLUGIN)
        self.assertIn("GetMortarHighlightBlend(ranging, selectedScatter)", remembered)

    def test_preview_error_resets_per_scene_and_logging_remains_opt_in(self):
        self.assertIn("_mortarPreviewErrorLogged = false;", PLUGIN)
        self.assertIn("_mortarPreviewErrorLogged = true;\n            if (_debugLogging)", OVERLAY)
        self.assertIn('AddToggle("DebugLogging", "Debug logging", false)', PLUGIN)

    def test_lines_avoid_unstripping_failed_draw_texture_chain(self):
        draw = PLUGIN.split("private static void DrawGuiLine(", 1)[1].split(
            "private static void InstallMortarSuppressionRadiusPatch", 1)[0]
        self.assertNotIn("GUI.DrawTexture(", draw)
        self.assertIn("_mortarLineStyle.normal.background = Texture2D.whiteTexture", draw)
        self.assertIn("GUIContent.none, _mortarLineStyle)", draw)
        self.assertIn("finally { GUI.matrix = oldMatrix; }", draw)

    def test_world_lines_avoid_stripped_generic_component_instantiation(self):
        self.assertNotIn(".AddComponent<LineRenderer>()", PLUGIN)
        self.assertIn("Il2CppInterop.Runtime.Il2CppType.Of<LineRenderer>()).Cast<LineRenderer>()", PLUGIN)
        self.assertIn("if (_mortarWorldRendererErrorLogged) return;", PLUGIN)


if __name__ == "__main__":
    unittest.main()
"""Source-wiring guards only; native hooks and visible rendering require a game test."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / "src/WeaponMechanicsRebalanced/Plugin.cs").read_text()
OVERLAY = (ROOT / "src/WeaponMechanicsRebalanced/MortarTargetingOverlay.cs").read_text()


class MortarTargetingTests(unittest.TestCase):
    def test_shot_and_preview_use_same_target_lookup(self):
        self.assertIn("GetMortarTargetScatter(__instance, actor, __0, originalScatter, useDisplayedState: false)", PLUGIN)
        self.assertIn("GetMortarTargetScatter(skill, actor, target, template.Scatter)", OVERLAY)
        self.assertIn("MortarRangingKey(skill, actor)", OVERLAY)
        self.assertIn("solutions.TryGetValue((target.GetX(), target.GetZ()), out ranging)", OVERLAY)
        self.assertIn("return useDisplayedState", OVERLAY)
        self.assertIn("GetMortarDisplayedEffectiveScatter(ranging, baseScatter)", OVERLAY)
        self.assertIn("MortarScatterRules.EffectiveScatter", PLUGIN)

    def test_hostile_mortars_range_per_activation_independent_of_target(self):
        self.assertIn("Dictionary<(long Actor, long Weapon), MortarTurnScatterState> EnemyMortarTurnRanging", PLUGIN)
        self.assertIn("GetEnemyMortarTurnState(key).EffectiveScatter(originalScatter)", PLUGIN)
        self.assertIn("IsHostileMortarActor(actor)", PLUGIN)
        self.assertIn("MortarRangingAgentTurnStartPostfix", PLUGIN)
        self.assertIn("pair.Value.Reset()", PLUGIN)
        self.assertIn("turnState.RecordShot(__state.DirectHit)", PLUGIN)
        self.assertIn("if (__state.IsHostileMortar)", PLUGIN)
        self.assertIn("GetMortarTargetScatter(__instance, actor, __0, originalScatter, useDisplayedState: false)", PLUGIN)

    def test_ranging_ui_waits_for_projectile_impact(self):
        self.assertIn("PendingMortarImpacts", PLUGIN)
        self.assertIn("AttackHandler.OnTileHit/4", PLUGIN)
        self.assertIn('"Il2CppMenace.Tactical.Skills.Skill"', PLUGIN)
        self.assertIn('"Il2CppMenace.Tactical.Tile"', PLUGIN)
        self.assertIn("typeof(Vector3).FullName", PLUGIN)
        self.assertIn("MortarRangingTileHitPostfix", PLUGIN)
        self.assertIn("ranging.DisplayShots <= 0", PLUGIN)
        self.assertIn("ranging?.DisplayShots ?? 0", PLUGIN)
        apply_postfix = PLUGIN.split("private static void MortarRangingApplyToTilePostfix(", 1)[1]
        apply_postfix = apply_postfix.split("private static MortarRangingState RecordMortarPlayerShot", 1)[0]
        self.assertIn("RecordMortarPlayerShot(__state)", apply_postfix)
        self.assertNotIn("DisplayShots =", apply_postfix)
        self.assertNotIn("UpdateMortarRangingDisplay", apply_postfix)
        self.assertIn("impact-gated ranging feedback will remain hidden", PLUGIN)
        impact_hook = PLUGIN.split("private static void MortarRangingTileHitPostfix", 1)[1]
        impact_hook = impact_hook.split("private static long NativeKey", 1)[0]
        self.assertIn("UpdateMortarRangingDisplay(ranging, shot)", impact_hook)
        display_update = PLUGIN.split("private static void UpdateMortarRangingDisplay", 1)[1]
        display_update = display_update.split("private static void EnqueuePendingMortarImpact", 1)[0]
        self.assertIn("ranging.DisplayShots = shot.ShotsAfterShot", display_update)

    def test_mortar_ranges_use_muted_scatter_based_colors(self):
        self.assertIn("Color.Lerp(low, high, blend.Progress)", PLUGIN)
        self.assertIn("new Color(0.48f, 0.15f, 0.14f, 0.92f)", PLUGIN)
        self.assertIn("new Color(0.30f, 0.59f, 0.35f, 0.92f)", PLUGIN)

    def test_enemy_only_medium_mortar_is_not_omitted_again(self):
        fixture = json.loads((ROOT / "tests/fixtures/enemy_medium_mortar.json").read_text())
        ranging_ids = PLUGIN.split("MortarRangingSkillIds =", 1)[1].split("};", 1)[0]
        self.assertIn('"' + fixture["id"] + '"', ranging_ids)
        baseline = PLUGIN.split("private static void ApplyEnemyMortarBallisticsPolicy()", 1)[1]
        baseline = baseline.split("private static void ", 1)[0]
        self.assertIn('const string skillId = "' + fixture["id"] + '";', baseline)
        for field, value in fixture["wmr_overrides"].items():
            self.assertIn(f'ApplyMortarTemplateValue(template, skillId, "{field}", {value});', baseline)
        self.assertNotIn("ApplyMortarTemplateScaledValue", baseline)
        self.assertNotIn("ApplyMortarHandler", baseline)
        for field in ("ActionPointCost", "Uses", "Repetitions", "DamageMult"):
            self.assertNotIn('"' + field + '"', baseline)

    def test_enemy_only_baseline_uses_existing_enable_and_restore_path(self):
        policy = PLUGIN.split("private static void ApplyMortarBallisticsPolicy()", 1)[1]
        policy = policy.split("private static void ", 1)[0]
        self.assertIn("ApplyEnemyMortarBallisticsPolicy();", policy)
        self.assertIn("object desired = _enabled ? enabledValue : OriginalMortarTemplateValues[key];", PLUGIN)
        self.assertIn("MORTAR BASELINE skill={skillId}", PLUGIN)

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
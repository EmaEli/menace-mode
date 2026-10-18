using UnityEngine;

namespace MenaceWeaponMechanicsRebalanced;

public sealed partial class Plugin
{
    private static bool _mortarPreviewErrorLogged;

    private static Il2CppMenace.Tactical.Skills.Skill? GetSelectedMortarSkill()
    {
        var state = Il2CppMenace.States.TacticalState.Get();
        var skill = state == null ? null : state.GetSelectedSkill();
        return skill != null && MortarRangingSkillIds.Contains(skill.GetID()) ? skill : null;
    }

    private static Il2CppMenace.Tactical.Actor? GetMortarDisplayActor(
        Il2CppMenace.Tactical.TacticalManager manager,
        Il2CppMenace.Tactical.Skills.Skill? selectedSkill)
        => selectedSkill != null ? selectedSkill.GetActor() : manager.GetActiveActor();

    private static Camera? GetMortarOverlayCamera(Il2CppMenace.Tactical.TacticalManager manager)
    {
        var tacticalCamera = manager.GetCamera();
        var camera = tacticalCamera == null ? null : tacticalCamera.GetCamera();
        return camera != null ? camera : Camera.main;
    }

    private static int GetMortarTargetScatter(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Actor actor,
        Il2CppMenace.Tactical.Tile target,
        int baseScatter)
    {
        MortarRangingState? ranging = null;
        if (MortarRangingSolutions.TryGetValue(MortarRangingKey(skill, actor), out var solutions))
            solutions.TryGetValue((target.GetX(), target.GetZ()), out ranging);
        return GetMortarEffectiveScatter(ranging, baseScatter);
    }

    private static void DrawMortarScatterPreview()
    {
        if (_mortarPreviewErrorLogged || !_enabled) return;
        try
        {
            var manager = Il2CppMenace.Tactical.TacticalManager.Get();
            var state = Il2CppMenace.States.TacticalState.Get();
            var skill = GetSelectedMortarSkill();
            if (manager == null || state == null || skill == null) return;
            var actor = skill.GetActor();
            var template = skill.GetTemplate();
            var target = state.GetCurrentTile();
            if (actor == null || template == null || target == null) return;
            if (template.IsDeploymentRequired && !actor.IsDeployed()) return;
            if (!skill.IsInRange(target, actor.GetTile())) return;
            var map = manager.GetMap();
            var camera = GetMortarOverlayCamera(manager);
            if (map == null || camera == null) return;

            int scatter = GetMortarTargetScatter(skill, actor, target, template.Scatter);
            Color color = new Color(1f, 0.78f, 0.12f, 1f);
            int x = target.GetX(), z = target.GetZ();
            for (int dz = -scatter; dz <= scatter; dz++)
            {
                for (int dx = -scatter; dx <= scatter; dx++)
                {
                    var tile = map.GetTile(x + dx, z + dz);
                    if (tile == null) continue;
                    int edges = MortarScatterRules.BorderEdges(dx, dz, scatter);
                    // Close the envelope against map boundaries as well.
                    if (map.GetTile(x + dx, z + dz - 1) == null) edges |= 1;
                    if (map.GetTile(x + dx + 1, z + dz) == null) edges |= 2;
                    if (map.GetTile(x + dx, z + dz + 1) == null) edges |= 4;
                    if (map.GetTile(x + dx - 1, z + dz) == null) edges |= 8;
                    if (edges != 0) DrawMortarTileBorder(camera, tile, 2.5f, color, edges);
                }
            }

            EnsureMortarOverlayStyles();
            DrawMortarTileScatter(camera, target, scatter, color, "Spread ");
        }
        catch (Exception ex)
        {
            _mortarPreviewErrorLogged = true;
            if (_debugLogging)
                Warn($"Mortar scatter preview disabled for this scene: {ex}");
        }
    }
}
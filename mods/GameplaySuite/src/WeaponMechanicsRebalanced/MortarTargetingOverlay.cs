using System;
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

    private static bool TryGetMortarBullseyeChance(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Actor actor,
        Il2CppMenace.Tactical.Tile target,
        out int bullseyeChance,
        out bool directSight)
    {
        bullseyeChance = -1;
        directSight = false;
        if (!_mortarScatterCenterFilterPatchInstalled) return false;

        Il2CppMenace.Tactical.Tile originTile = actor.GetTile();
        if (originTile == null) return false;

        int effectiveRange = Math.Max(0, skill.GetMaxRange());
        int targetDistance = originTile.GetDistanceTo(target);
        directSight = MortarCanSeeTarget(actor, originTile, target);
        bullseyeChance = MortarScatterRules.BullseyeChance(targetDistance, effectiveRange, directSight);
        return true;
    }

    private static Il2CppMenace.Tactical.Tile? GetCurrentMortarPreviewTarget(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Actor actor)
    {
        var state = Il2CppMenace.States.TacticalState.Get();
        var target = state == null ? null : state.GetCurrentTile();
        var template = skill.GetTemplate();
        Il2CppMenace.Tactical.Tile originTile = actor.GetTile();
        if (target == null || template == null || originTile == null) return null;
        if (template.IsDeploymentRequired && !actor.IsDeployed()) return null;
        if (!skill.IsInRange(target, originTile)) return null;
        return target;
    }

    private static int GetMortarTargetScatter(
        Il2CppMenace.Tactical.Skills.Skill skill,
        Il2CppMenace.Tactical.Actor actor,
        Il2CppMenace.Tactical.Tile target,
        int baseScatter,
        bool useDisplayedState = true)
    {
        MortarRangingState? ranging = null;
        if (MortarRangingSolutions.TryGetValue(MortarRangingKey(skill, actor), out var solutions))
            solutions.TryGetValue((target.GetX(), target.GetZ()), out ranging);
        return useDisplayedState
            ? GetMortarDisplayedEffectiveScatter(ranging, baseScatter)
            : GetMortarEffectiveScatter(ranging, baseScatter);
    }

    private static void DrawMortarScatterPreview()
    {
        if (_mortarPreviewErrorLogged || !_enabled) return;
        try
        {
            var manager = Il2CppMenace.Tactical.TacticalManager.Get();
            var skill = GetSelectedMortarSkill();
            if (manager == null || skill == null) return;
            var actor = skill.GetActor();
            var template = skill.GetTemplate();
            if (actor == null || template == null) return;
            var target = GetCurrentMortarPreviewTarget(skill, actor);
            if (target == null) return;
            var map = manager.GetMap();
            var camera = GetMortarOverlayCamera(manager);
            if (map == null || camera == null) return;

            int baseScatter = GetMortarBaseScatter(skill, skill.GetID(), template.Scatter);
            int scatter = GetMortarTargetScatter(skill, actor, target, baseScatter);
            int bullseyeChance = TryGetMortarBullseyeChance(skill, actor, target, out int chance, out _)
                ? chance
                : -1;
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
            DrawMortarTargetPreview(camera, target, scatter, bullseyeChance, color);
        }
        catch (Exception ex)
        {
            _mortarPreviewErrorLogged = true;
            if (_debugLogging)
                Warn($"Mortar scatter preview disabled for this scene: {ex}");
        }
    }

    private static void DrawMortarTargetPreview(
        Camera camera,
        Il2CppMenace.Tactical.Tile tile,
        int effectiveScatter,
        int bullseyeChance,
        Color color)
    {
        if (_mortarBonusStyle == null || _mortarBonusShadowStyle == null) return;

        Vector3 center = tile.GetPos();
        center.y += 0.18f;
        Vector3 p = camera.WorldToScreenPoint(center);
        if (p.z <= 0f) return;

        const float width = 140f;
        const float height = 40f;
        float x = p.x - width * 0.5f;
        float y = Screen.height - p.y - height * 0.5f;
        string text = bullseyeChance >= 0
            ? $"Spread {Math.Max(0, effectiveScatter)}\nBullseye {bullseyeChance}%"
            : $"Spread {Math.Max(0, effectiveScatter)}";

        Color old = _mortarBonusStyle.normal.textColor;
        try
        {
            _mortarBonusStyle.normal.textColor = color;
            GUI.Label(new Rect(x + 1f, y + 1f, width, height), text, _mortarBonusShadowStyle);
            GUI.Label(new Rect(x, y, width, height), text, _mortarBonusStyle);
        }
        finally
        {
            _mortarBonusStyle.normal.textColor = old;
        }
    }
}
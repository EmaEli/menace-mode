using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace StrongerCover;

internal static class Il2Cpp
{
    /// <summary>A boxed patch argument as the game type, or null (null reference or other type).</summary>
    public static T As<T>(object value) where T : Il2CppObjectBase =>
        value as T ?? (value as Il2CppObjectBase)?.TryCast<T>();
}

/// <summary>The replacement for Skill.GetCoverMult, same rules as the legacy module 2.2.3.</summary>
internal static class CoverRules
{
    /// <summary>
    /// False means "let vanilla decide" (missing tiles). Otherwise <paramref name="result"/> is
    /// the hit-chance multiplier the caller receives.
    /// </summary>
    public static bool TryResolve(Skill skill, Tile from, Tile targetTile, Entity targetEntity,
        EntityProperties defender, bool pretendInside, out float result)
    {
        if (SkillIgnoresCover(skill))
        {
            result = CoverSettings.IndirectFireCover && IsIndirectFireSkill(skill)
                ? ResolveIndirectFireCover(targetTile, targetEntity, defender, pretendInside)
                : 1.0f;
            return true;
        }

        if (from == null || targetTile == null)
        {
            result = 1.0f;
            return false;
        }

        int distance = from.GetDistanceTo(targetTile);
        if (distance < CoverSettings.MinimumCoverDistance)
        {
            result = ApplySquadSize(1.0f, targetEntity);
            return true;
        }

        Direction direction = targetTile.GetDirectionTo(from);
        float multiplier = DirectionalMultiplier(targetTile, direction, targetEntity, defender);
        result = Math.Clamp(ApplySquadSize(multiplier, targetEntity), 0.01f, 1.0f);
        return true;
    }

    private static float DirectionalMultiplier(Tile tile, Direction direction, Entity entity, EntityProperties defender)
    {
        int level = Math.Clamp((int)tile.GetCover(direction, entity, defender, true), 0, 3);
        float multiplier = CoverSettings.CoverMultipliers[level];
        return CoverSettings.RespectCoverUsage ? ApplyCoverUsage(multiplier, defender) : multiplier;
    }

    private static bool SkillIgnoresCover(Skill skill)
    {
        // SkillTemplate.IsIgnoringCover is the generic flag. IsIgnoringCoverInsideForTarget() is
        // deliberately not treated as a generic ignore: vanilla only uses it to decide whether the
        // special inside-container modifier applies.
        var template = skill?.GetTemplate();
        return template != null && template.IsIgnoringCover;
    }

    private static bool IsIndirectFireSkill(Skill skill)
    {
        string id = (skill?.GetID() ?? string.Empty).ToLowerInvariant();
        if (id.Contains("grenade") || id.Contains("rpg") || id.Contains("rocket") ||
            id.Contains("atgm") || id.Contains("flamethrower") || id.Contains("flame_thrower"))
            return false;
        return id.Contains("mortar") || id.Contains("howitzer") || id.Contains("artillery") || id.Contains("indirect");
    }

    // Indirect fire comes from above, so it is weighed against the cover around the whole tile
    // and the inside-cover protection of the container, not the attacker's direction. No
    // squad-size multiplier here: averaging all directions already softens the value.
    private static float ResolveIndirectFireCover(Tile targetTile, Entity targetEntity, EntityProperties defender, bool pretendInside)
    {
        float final = 1.0f;

        if (CoverSettings.IndirectAverageSurroundingCover && targetTile != null)
        {
            float total = 0f;
            for (int d = 0; d < 8; d++)
                total += DirectionalMultiplier(targetTile, (Direction)d, targetEntity, defender);
            final = Math.Min(final, total / 8f);
        }

        if (CoverSettings.IndirectInsideCover)
            final = Math.Min(final, InsideCoverMultiplier(targetTile, targetEntity, pretendInside));

        return Math.Clamp(final, 0.01f, 1.0f);
    }

    private static float InsideCoverMultiplier(Tile targetTile, Entity targetEntity, bool pretendInside)
    {
        Entity container = targetEntity?.GetContainerEntity();
        if (container == null && pretendInside && targetTile != null)
            container = targetTile.GetEntity();

        InsideCoverTemplate inside = container?.GetTemplate()?.ProvidesCoverInside;
        return inside == null ? 1.0f : Math.Clamp(inside.AccuracyMult, 0.01f, 1.0f);
    }

    // Same shape as the vanilla GetCoverMult path: CoverEffectivenessMult is clamped to 0..1,
    // zero disables cover, otherwise the cover multiplier is divided by it.
    private static float ApplyCoverUsage(float multiplier, EntityProperties defender)
    {
        if (defender == null)
            return multiplier;
        float effectiveness = Math.Clamp(defender.CoverEffectivenessMult, 0f, 1f);
        if (effectiveness <= 0f)
            return 1.0f;
        return Math.Min(multiplier / effectiveness, 1.0f);
    }

    private static float ApplySquadSize(float multiplier, Entity entity)
    {
        if (!CoverSettings.SquadSizeScaling || entity == null || !entity.IsInfantry())
            return multiplier;

        int faction = entity.GetFactionID();
        if ((CoverSettings.ExcludeWildlife && faction == (int)FactionType.Wildlife) ||
            (CoverSettings.ExcludeConstructs && faction == (int)FactionType.Constructs))
            return multiplier;

        var elements = entity.GetElements();
        if (elements == null)
            return multiplier;

        int count = elements.Count;
        float squad = count <= 3 ? CoverSettings.SmallSquad
            : count <= 5 ? CoverSettings.MediumSquad
            : count <= 7 ? CoverSettings.LargeSquad
            : 1.0f;
        return multiplier * squad;
    }
}

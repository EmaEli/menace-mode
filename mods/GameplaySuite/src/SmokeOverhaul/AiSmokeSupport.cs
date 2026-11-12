using System;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.Skills;

namespace SmokeOverhaul;

public sealed partial class Plugin
{
    private const int MaxSupportActors = 64, MaxSupportMortars = 4;

    private static bool IsMortarSmokeSkillId(string? id)
        => id is "active.launch_mortar_smoke" or "active.mod.launch_mortar_smoke_autoloader" or
            "active.mod.twinfire.launch_mortar_smoke_autoloader";

    private static Skill? FindUsableMortarSmoke(Actor actor)
    {
        if (actor.IsContainedWithinAnotherEntity() || actor.IsStunned()) return null;
        var skills = actor.GetSkills()?.GetAllSkills();
        if (skills == null || skills.Count > 128) return null;
        foreach (var raw in skills)
        {
            if (raw == null || !IsMortarSmokeSkillId(raw.GetID())) continue;
            var skill = raw.TryCast<Skill>();
            if (skill != null && !skill.IsOutOfUses() && skill.IsUsable() && skill.IsAffordable()) return skill;
        }
        return null;
    }

    private static bool TryNearbySupportThreat(Agent agent, Tile origin, bool mortar, out int dx, out int dz)
    {
        dx = dz = 0;
        if (agent == null || origin == null) return false;
        var threat = (mortar ? _supportMortarSmokeThreat : _supportSmokeThreat)?.Invoke(agent);
        if (threat is not { Length: 2 }) return false;
        dx = threat[0] - origin.GetX(); dz = threat[1] - origin.GetZ();
        return SmokeAiRules.IsNearbyThreat(dx, dz);
    }

    private static bool HasForwardSmoke(Tile infantry, int dx, int dz)
    {
        if (!SmokeAiRules.TryGetLocalOffset(dx, dz, false, 0, 0, out int x, out int z)) return false;
        var map = TacticalManager.Get()?.GetMap();
        x += infantry.GetX(); z += infantry.GetZ();
        return map != null && map.IsInBounds(x, z) && TileHasSmoke(map.GetTile(x, z));
    }

    private static bool SupportActorReady(Actor actor)
        => actor != null && !actor.IsDying() && !actor.IsMoving() && !actor.IsTurnDone() &&
            !actor.IsContainedWithinAnotherEntity() && !actor.IsStunned() && actor.GetTile() != null &&
            actor.GetAgent() is { } agent && !agent.IsSleeping() && !agent.IsDeploymentPhase();

    private static Tile? SupportMortarTarget(Skill mortar, Tile source, Tile infantry, int dx, int dz)
    {
        var map = TacticalManager.Get()?.GetMap();
        if (map == null || HasForwardSmoke(infantry, dx, dz)) return null;
        // Same six local screening tiles as hand smoke; no map scan or speculative path.
        for (int reach = 0; reach < 2; reach++)
        for (int candidate = 0; candidate < 3; candidate++)
        {
            if (!SmokeAiRules.TryGetLocalOffset(dx, dz, false, reach, candidate, out int x, out int z)) continue;
            x += infantry.GetX(); z += infantry.GetZ();
            if (!map.IsInBounds(x, z)) continue;
            var tile = map.GetTile(x, z);
            if (tile != null && tile.IsEmpty() && !TileHasSmoke(tile) && tile.IsValidMovementDestination() &&
                mortar.IsUsableOn(tile, source)) return tile;
        }
        return null;
    }

    private static bool HasAvailableSupportMortar(Agent requester, Tile infantry, int dx, int dz)
    {
        var actors = requester.GetFaction()?.GetActors();
        if (actors == null || actors.Count > MaxSupportActors) return false;
        int checkedMortars = 0;
        foreach (var actor in actors)
        {
            if (!SupportActorReady(actor) || _smokeActedThisTurn.Contains(actor.Pointer)) continue;
            var mortar = FindUsableMortarSmoke(actor);
            if (mortar == null) continue;
            if (++checkedMortars > MaxSupportMortars) break;
            if (SupportMortarTarget(mortar, actor.GetTile(), infantry, dx, dz) != null) return true;
        }
        return false;
    }

    private static bool TrySupportMortarAction(Agent agent, Actor actor, Skill mortar)
    {
        var allies = agent.GetFaction()?.GetActors();
        if (allies == null || allies.Count > MaxSupportActors || !SupportActorReady(actor)) return false;
        _smokeActedThisTurn.Add(actor.Pointer);
        int requests = 0;
        foreach (var ally in allies)
        {
            if (ally == null || ally.Pointer == actor.Pointer || !ally.IsInfantry() || !SupportActorReady(ally)) continue;
            if (++requests > MaxSmokeThreatChecks) break;
            var infantry = ally.GetTile();
            if (TileHasSmoke(infantry) || !TryNearbySupportThreat(ally.GetAgent(), infantry, true, out int dx, out int dz)) continue;
            var target = SupportMortarTarget(mortar, actor.GetTile(), infantry, dx, dz);
            if (target == null) continue;
            // Execute only on the mortar's own native Agent.Execute activation.
            return mortar.Use(target, UsageParameter.AutoAudioVolume);
        }
        return false;
    }

    internal static bool AllowPreventiveHandSmoke(Skill skill)
    {
        if (!AiSmokeOnMainThread || !RuntimeSettings.Enabled || !RuntimeSettings.EnableAISmokeUsage ||
            !RuntimeSettings.SimpleAISmokeRules || !IsHandThrownSmokeSkillId(skill.GetID())) return true;
        var actor = skill.GetAPConsumer(); var agent = actor?.GetAgent();
        if (agent == null || agent.GetFaction()?.IsAlliedWithPlayer() != false) return true;
        // Actual incoming fire remains an emergency, even at long range.
        if (actor!.GetSuppressionState() != SuppressionState.None) return true;
        var origin = actor.GetTile();
        if (origin == null) return false;
        if (TryNearbySupportThreat(agent, origin, true, out int dx, out int dz) &&
            HasAvailableSupportMortar(agent, origin, dx, dz)) return false;
        bool nearby = TryNearbySupportThreat(agent, origin, false, out dx, out dz) ||
            (TryGetDetectedSmokeThreat(agent, actor, origin, false, out dx, out dz) && SmokeAiRules.IsNearbyThreat(dx, dz));
        return nearby && !HasForwardSmoke(origin, dx, dz);
    }

    internal static bool AllowPreventiveSmoke(Skill skill, Tile target)
    {
        if (!AllowPreventiveHandSmoke(skill)) return false;
        if (!AiSmokeOnMainThread || !RuntimeSettings.Enabled || !RuntimeSettings.EnableAISmokeUsage ||
            !RuntimeSettings.SimpleAISmokeRules || !IsMortarSmokeSkillId(skill.GetID())) return true;
        var agent = skill.GetAPConsumer()?.GetAgent();
        if (agent == null || agent.GetFaction()?.IsAlliedWithPlayer() != false) return true;
        var allies = agent.GetFaction().GetActors();
        if (allies == null || allies.Count > MaxSupportActors || target == null) return false;
        foreach (var ally in allies)
        {
            var tile = ally?.GetTile();
            if (tile == null || ally!.IsDying() || ally.IsContainedWithinAnotherEntity()) continue;
            int x = tile.GetX() - target.GetX(), z = tile.GetZ() - target.GetZ();
            if (x * x + z * z > 9) continue;
            if (TryNearbySupportThreat(ally.GetAgent(), tile, true, out _, out _) ||
                (TryGetDetectedSmokeThreat(agent, ally, tile, false, out int dx, out int dz) && SmokeAiRules.IsNearbyThreat(dx, dz))) return true;
        }
        return false;
    }
}
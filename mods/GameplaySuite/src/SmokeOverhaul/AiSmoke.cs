using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tools;

namespace SmokeOverhaul;

// Jiangyu adaptation of PR #12's 0.6.3 behavior, not a second AI scheduler.
public sealed partial class Plugin
{
    private static readonly string[] InfantrySmokeSkillIds =
        { "active.throw_smoke_grenade", "active.throw_smoke_grenade_limited" };
    private static readonly Dictionary<IntPtr, SkillTemplate> OriginalPinnedSmokePermissions = new();
    private static readonly HashSet<IntPtr> _smokeActedThisTurn = new();
    private const int MaxSmokeThreatChecks = 24;
    private static int _aiSmokeMainThreadId;
    internal static bool AiSmokeOnMainThread => Environment.CurrentManagedThreadId == _aiSmokeMainThreadId;
    private static bool _pinnedSmokeErrorLogged, _aiSmokeActionErrorLogged;
    private static int _aiSmokeExecutions, _aiSmokeEligible, _aiSmokeSkillsAvailable,
        _aiSmokeValidTargets, _aiSmokeChanceSkipped, _aiSmokeUsesSucceeded, _aiSmokeDirectionDebugLines;

    private static bool IsHandThrownSmokeSkillId(string? id)
        => string.Equals(id, InfantrySmokeSkillIds[0], StringComparison.OrdinalIgnoreCase) ||
           string.Equals(id, InfantrySmokeSkillIds[1], StringComparison.OrdinalIgnoreCase);

    private static void ApplyPinnedSmokePermission()
    {
        try
        {
            foreach (string id in InfantrySmokeSkillIds)
            {
                SkillTemplate? template = DataTemplateLoader.Get<SkillTemplate>(id, false);
                if (template == null || template.Pointer == IntPtr.Zero || template.IsUsableWhilePinnedDown)
                    continue;
                // Only false -> true mutations belong to us; already-true templates are untouched.
                if (!OriginalPinnedSmokePermissions.ContainsKey(template.Pointer))
                    OriginalPinnedSmokePermissions.Add(template.Pointer, template);
                template.IsUsableWhilePinnedDown = true;
            }
        }
        catch (Exception ex)
        {
            if (!_pinnedSmokeErrorLogged)
            {
                _pinnedSmokeErrorLogged = true;
                Error($"Failed to allow pinned smoke throws: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void RestorePinnedSmokePermission()
    {
        foreach (SkillTemplate template in OriginalPinnedSmokePermissions.Values)
        {
            try { template.IsUsableWhilePinnedDown = false; }
            catch { }
        }
        OriginalPinnedSmokePermissions.Clear();
    }

    internal static void ApplyPinnedSmokeApCost(Skill? skill, ref int cost)
    {
        if (!AiSmokeOnMainThread || !RuntimeSettings.Enabled || !RuntimeSettings.AllowSmokeWhilePinnedDown || skill == null || cost <= 0)
            return;
        try
        {
            Actor? consumer = skill.GetAPConsumer();
            if (consumer != null && consumer.GetSuppressionState() == SuppressionState.PinnedDown &&
                IsHandThrownSmokeSkillId(skill.GetID()))
                cost = 0;
        }
        catch (Exception ex)
        {
            if (!_pinnedSmokeErrorLogged)
            {
                _pinnedSmokeErrorLogged = true;
                Error($"Failed to determine pinned smoke AP cost: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void AiSmokeSceneChanged(string sceneName)
    {
        _smokeActedThisTurn.Clear();
        if (!string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
        {
            if (RuntimeSettings.DebugLogging && _aiSmokeExecutions > 0)
                DebugMsg($"AI smoke mission summary: execute={_aiSmokeExecutions}, pressure={_aiSmokeEligible}, skill={_aiSmokeSkillsAvailable}, target={_aiSmokeValidTargets}, chanceSkipped={_aiSmokeChanceSkipped}, used={_aiSmokeUsesSucceeded}.");
            return;
        }
        _aiSmokeExecutions = _aiSmokeEligible = _aiSmokeSkillsAvailable = _aiSmokeValidTargets =
            _aiSmokeChanceSkipped = _aiSmokeUsesSucceeded = _aiSmokeDirectionDebugLines = 0;
    }

    internal static void ResetAiSmokeTurn(Agent? agent)
    {
        if (!AiSmokeOnMainThread) return;
        try
        {
            Actor? actor = agent?.GetActor();
            if (actor != null && actor.Pointer != IntPtr.Zero)
                _smokeActedThisTurn.Remove(actor.Pointer);
        }
        catch { }
    }

    internal static bool TryAiSmokeAction(Agent? agent)
    {
        if (!AiSmokeOnMainThread || !RuntimeSettings.Enabled || !RuntimeSettings.EnableAISmokeUsage ||
            !RuntimeSettings.SimpleAISmokeRules || agent == null)
            return false;
        _aiSmokeExecutions++;
        try
        {
            if (agent.IsSleeping() || agent.IsDeploymentPhase() || TacticalManager.IsSkillBusy()) return false;
            Actor? actor = agent.GetActor();
            if (actor == null || !actor.IsInfantry() || actor.IsDying() || actor.IsMoving() ||
                actor.IsTurnDone() || actor.Pointer == IntPtr.Zero) return false;
            IntPtr actorKey = actor.Pointer;
            if (_smokeActedThisTurn.Contains(actorKey)) return false;
            Tile? origin = actor.GetTile();
            if (origin == null || TileHasSmoke(origin)) return false;
            SuppressionState pressure = actor.GetSuppressionState();
            bool pinned = pressure == SuppressionState.PinnedDown;
            bool suppressed = pressure == SuppressionState.Suppressed;
            if (pinned && !RuntimeSettings.AllowSmokeWhilePinnedDown) return false;
            bool advance = !pinned && !suppressed && origin.GetCoverMask() == 0 &&
                (agent.GetNumThreatsFaced() > 0 || agent.GetFaction()?.HasKnownOpponent() == true);
            if (!pinned && !suppressed && !advance) return false;

            // Record the decision before skill lookup/legality/chance: no rerolls this turn.
            _smokeActedThisTurn.Add(actorKey);
            _aiSmokeEligible++;
            var skills = actor.GetSkills()?.GetAllSkills();
            if (skills == null) return false;
            Skill? smoke = null;
            bool carriesFlamethrower = false;
            for (int i = 0; i < skills.Count; i++)
            {
                BaseSkill? raw = skills[i];
                if (raw == null) continue;
                string id = raw.GetID() ?? string.Empty;
                if (id.IndexOf("flamethrower", StringComparison.OrdinalIgnoreCase) >= 0) carriesFlamethrower = true;
                if (smoke != null || !IsHandThrownSmokeSkillId(id)) continue;
                try
                {
                    Skill? cast = raw.TryCast<Skill>();
                    if (cast != null && !cast.IsOutOfUses() && cast.IsUsable() && cast.IsAffordable()) smoke = cast;
                }
                catch { }
            }
            if (smoke == null || (advance && !carriesFlamethrower)) return false;
            _aiSmokeSkillsAvailable++;
            if (!TryGetDetectedSmokeThreat(agent, actor, origin, pinned || suppressed, out int dx, out int dz)) return false;
            Map? map = TacticalManager.Get()?.GetMap();
            if (map == null) return false;
            for (int reachIndex = 0; reachIndex < 2; reachIndex++)
            {
                for (int candidate = 0; candidate < 3; candidate++)
                {
                    if (!SmokeAiRules.TryGetLocalOffset(dx, dz, advance, reachIndex, candidate, out int offsetX, out int offsetZ)) continue;
                    int x = origin.GetX() + offsetX, z = origin.GetZ() + offsetZ;
                    if (!map.IsInBounds(x, z)) continue;
                    Tile? target = map.GetTile(x, z);
                    if (target == null || !target.IsEmpty() || TileHasSmoke(target) ||
                        (advance && !target.IsValidMovementDestination())) continue;
                    if (!smoke.IsUsableOn(target, origin)) continue;
                    _aiSmokeValidTargets++;
                    if (System.Random.Shared.Next(100) >= SmokeAiRules.Chance(pinned, suppressed))
                    {
                        _aiSmokeChanceSkipped++;
                        return false;
                    }
                    if (!smoke.Use(target, UsageParameter.AutoAudioVolume)) return false;
                    _aiSmokeUsesSucceeded++;
                    if (RuntimeSettings.DebugLogging && _aiSmokeDirectionDebugLines < 24)
                    {
                        _aiSmokeDirectionDebugLines++;
                        DebugMsg($"AI smoke thrown: origin=({origin.GetX()},{origin.GetZ()}) threat=({origin.GetX() + dx},{origin.GetZ() + dz}) target=({x},{z}) mode={(advance ? "advance" : "defensive")}");
                    }
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            if (!_aiSmokeActionErrorLogged)
            {
                _aiSmokeActionErrorLogged = true;
                Error($"AI emergency smoke decision failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return false; // Native MENACE AI remains the fallback, including failed Use.
    }

    private static bool TryGetDetectedSmokeThreat(Agent agent, Actor actor, Tile origin,
        bool defensive, out int dx, out int dz)
    {
        dx = dz = 0;
        AIFaction? faction = agent.GetFaction();
        var opponents = faction?.GetOpponents();
        if (faction == null || opponents == null) return false;
        Actor? attacker = defensive ? actor.GetLastAttacker()?.ToActor() : null;
        IntPtr attackerPointer = attacker?.Pointer ?? IntPtr.Zero;
        int nearest = int.MaxValue, attackerDistance = int.MaxValue;
        int nx = 0, nz = 0, ax = 0, az = 0;
        int count = System.Math.Min(opponents.Count, MaxSmokeThreatChecks);
        for (int i = 0; i < count; i++)
        {
            Opponent? opponent = opponents[i];
            if (opponent == null || !opponent.IsKnown()) continue;
            Actor? enemy = opponent.Actor;
            if (enemy == null || enemy.Pointer == actor.Pointer || enemy.IsDying() ||
                !enemy.IsAlive() || !enemy.IsDetectedByFaction(faction.GetIndex())) continue;
            Tile? enemyTile = enemy.GetTile(); // Never read hidden-opponent coordinates.
            if (enemyTile == null) continue;
            int ex = enemyTile.GetX() - origin.GetX(), ez = enemyTile.GetZ() - origin.GetZ();
            int distance = ex * ex + ez * ez;
            if (distance == 0) continue;
            if (distance < nearest) { nearest = distance; nx = ex; nz = ez; }
            if (attackerPointer != IntPtr.Zero && enemy.Pointer == attackerPointer)
            { attackerDistance = distance; ax = ex; az = ez; }
        }
        if (nearest == int.MaxValue) return false;
        if (defensive && attackerDistance != int.MaxValue && attackerDistance <= nearest * 4)
        { dx = ax; dz = az; }
        else { dx = nx; dz = nz; }
        return true;
    }
}

internal static partial class Patches
{
    public static bool Agent_Execute_Prefix(Agent __instance, ref bool __result, bool __runOriginal)
    {
        if (!__runOriginal || !Plugin.TryAiSmokeAction(__instance)) return true;
        __result = true;
        return false;
    }
    public static void Agent_OnTurnStart_Postfix(Agent __instance) => Plugin.ResetAiSmokeTurn(__instance);
    public static void Skill_GetActionPointCost_Postfix(Skill __instance, ref int __result)
        => Plugin.ApplyPinnedSmokeApCost(__instance, ref __result);
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LargerMaps;

public sealed partial class Plugin
{
    private static bool _aiRouteEnabled;
    private static int _aiRouteFailures;
    private static readonly AIRouteBudget _aiRouteBudget = new();

    // All probes are opt-in, read-only and independent of performance/deep logging.
    // No Collect/Evaluate/IsUsable/LoS/path search is invoked by diagnostics.
    private static void PatchAIRouteInvestigation(HarmonyLib.Harmony harmony)
    {
        if (!ReadBool("DiagnoseAIRoutes", false)) return;
        // Prepare observer helpers as well as callbacks: the first attack evaluation
        // can arrive on an IL2CPP worker rather than the managed game thread (#44).
        foreach (Type type in new[] { typeof(Plugin), typeof(AIRouteBudget), typeof(AIRouteBehaviorProbe) })
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (type != typeof(Plugin) || method.Name.StartsWith("AIRoute", StringComparison.Ordinal))
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
        _aiRouteEnabled = true;
        Type actor = FindType("Il2CppMenace.Tactical.Actor") ?? FindType("Menace.Tactical.Actor");
        Type behavior = FindType("Il2CppMenace.Tactical.AI.Behavior") ?? FindType("Menace.Tactical.AI.Behavior");
        Type skill = _skillType;
        Type attack = FindType("Il2CppMenace.Tactical.AI.Behaviors.Attack") ?? FindType("Menace.Tactical.AI.Behaviors.Attack");
        int ok = 0;
        ok += TryInstallRouteProbe(harmony, _tacticalManagerType, "OnInit", 1, null, nameof(AIRouteMissionStart));
        ok += TryInstallRouteProbe(harmony, _aiFactionType, "OnRoundStart", 0, null, nameof(AIRouteFactionRound));
        ok += TryInstallRouteProbe(harmony, _agentType, "OnTurnStart", 0, null, nameof(AIRouteAgentTurnPostfix));
        ok += TryInstallRouteProbe(harmony, _agentType, "PickBehavior", 0, null, nameof(AIRouteChoicePostfix));
        ok += TryInstallRouteProbe(harmony, _agentType, "Execute", 0, null, nameof(AIRouteAgentExecutePostfix));
        ok += TryInstallRouteProbe(harmony, behavior, "Execute", 1,
            nameof(AIRouteBehaviorProbe.AIRouteBehaviorPrefix), nameof(AIRouteBehaviorProbe.AIRouteBehaviorPostfix), typeof(AIRouteBehaviorProbe));
        ok += TryInstallRouteProbe(harmony, _moveBehaviorType, "OnExecute", 1, null, nameof(AIRouteMoveExecutionPostfix));
        ok += TryInstallRouteProbe(harmony, _moveBehaviorType, "OnEvaluate", 1, null, nameof(AIRouteMoveEvaluated));
        ok += TryInstallRouteProbe(harmony, _moveBehaviorType, "OnNewTurn", 0, null, nameof(AIRouteMoveNewTurn));
        ok += TryInstallRouteProbe(harmony, _moveBehaviorType, "HasUtility", 1, null, nameof(AIRouteMoveUtility));
        ok += TryInstallRouteProbe(harmony, actor, "OnTurnEnd", 0, null, nameof(AIRouteActorEnd));
        ok += TryInstallRouteProbe(harmony, actor, "OnMovementFinished", 0, null, nameof(AIRouteMovementFinished));
        ok += TryInstallRouteProbe(harmony, skill, "Use", 2, nameof(AIRouteSkillPrefix), nameof(AIRouteSkillPostfix));
        ok += TryInstallRouteProbe(harmony, skill, "ApplyToTile", 2, null, nameof(AIRouteSkillApplied));
        ok += TryInstallRouteProbe(harmony, attack, "OnEvaluate", 1, null, nameof(AIRouteAttackEvaluated));
        _log.Msg($"{ModName}: AI ROUTE v3 hooks={ok}/15; all rounds, per-actor budgets; repeated polls suppressed, limits explicit; no gameplay changes.");
    }

    private static int TryInstallRouteProbe(HarmonyLib.Harmony harmony, Type type,
        string name, int parameters, string prefix, string postfix, Type callbacks = null)
    {
        try
        {
            MethodInfo target = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            if (target == null) throw new MissingMethodException(type?.FullName, name);
            callbacks ??= typeof(Plugin);
            // Prepare our callbacks before an IL2CPP worker can be the first caller (#44).
            if (prefix != null) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(callbacks.GetMethod(prefix, flags).MethodHandle);
            if (postfix != null) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(callbacks.GetMethod(postfix, flags).MethodHandle);
            harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(callbacks.GetMethod(prefix, flags)),
                postfix: postfix == null ? null : new HarmonyMethod(callbacks.GetMethod(postfix, flags)));
            return 1;
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: AI ROUTE missing hook {type?.Name}.{name}/{parameters}: {ex.GetType().Name}: {ex.GetBaseException().Message}");
            return 0;
        }
    }

    private static void AIRouteFailure(Exception ex)
    {
        if (System.Threading.Interlocked.Increment(ref _aiRouteFailures) <= 5)
            _log.Warning($"{ModName}: AI ROUTE observation failed: {ex.GetType().Name}: {ex.Message}");
    }

    private static string AIRouteTemplate(object actor)
    {
        object template = InvokeNoArg(actor, "GetTemplate");
        return Safe(GetMember(template, "m_ID") ?? GetMember(template, "ID"));
    }

    private static void AIRouteMissionStart()
    {
        _aiRouteBudget.Reset();
        _aiRouteFailures = 0;
    }

    private static void AIRouteWrite(object actor, string kind, string detail, bool deduplicate = true, string budgetKind = null)
    {
        if (!_aiRouteEnabled || actor == null) return;
        object manager = InvokeStaticNoArg(_tacticalManagerType, "Get");
        long map = GetStableObjectKey(InvokeNoArg(manager, "GetMap"));
        int round = ToInt(GetMember(manager, "m_Round"), -1);
        long key = GetStableObjectKey(actor);
        string template = AIRouteTemplate(actor);
        // Civilian polls cannot consume a hostile's allowance. Unknown templates
        // receive the full budget too; do not silently omit non-enemy-prefixed foes.
        int limit = template.StartsWith("civilian", StringComparison.OrdinalIgnoreCase) ? 4 : 24;
        int result = _aiRouteBudget.Observe(map, round, key, budgetKind ?? kind, detail, limit, deduplicate);
        string label = $"map={map:X} round={round} id={key:X} template={template}";
        if (result == 1) _log.Msg($"{ModName}: AI ROUTE {kind} {label} {detail}");
        else if (result == 2) _log.Warning($"{ModName}: AI ROUTE LIMIT {label} kind={kind} limit={limit}; further distinct entries omitted this round; summaries retain counts.");
        else if (result == 3) _log.Warning($"{ModName}: AI ROUTE BUFFER LIMIT {label}; round coverage incomplete.");
    }

    private static string AIRouteActorState(object actor)
    {
        return $"tile={DescribeTile(InvokeNoArg(actor, "GetTile"))} ap={Safe(InvokeNoArg(actor, "GetActionPoints"))} " +
            $"faction={Safe(InvokeNoArg(actor, "GetFactionID"))} done={Safe(InvokeNoArg(actor, "IsTurnDone"))} " +
            $"moving={Safe(InvokeNoArg(actor, "IsMoving"))} dying={Safe(InvokeNoArg(actor, "IsDying"))} " +
            $"suppression={Safe(InvokeNoArg(actor, "GetSuppressionPct"))} morale={Safe(InvokeNoArg(actor, "GetMoralePct"))} " +
            $"tilesMoved={Safe(GetMember(actor, "TilesMovedThisTurn"))} stationary={Safe(GetMember(actor, "TurnsStationary"))}";
    }

    private static string AIRouteZones(object agent, object actor)
    {
        object zones = InvokeNoArg(agent, "GetZones");
        object tile = InvokeNoArg(actor, "GetTile");
        int tx = ToInt(InvokeNoArg(tile, "GetX"), -1), tz = ToInt(InvokeNoArg(tile, "GetZ"), -1);
        var parts = new List<string>();
        foreach (object zone in SnapshotCollection(zones, 16))
        {
            object area = GetMember(zone, "Area");
            int x = ToInt(GetMember(area, "m_XMin") ?? GetMember(area, "xMin"), -1);
            int z = ToInt(GetMember(area, "m_YMin") ?? GetMember(area, "yMin"), -1);
            int w = ToInt(GetMember(area, "m_Width") ?? GetMember(area, "width"), -1);
            int h = ToInt(GetMember(area, "m_Height") ?? GetMember(area, "height"), -1);
            double distance = AIRouteBudget.Distance(tx, tz, x, z, w, h);
            parts.Add($"{Safe(GetMember(zone, "Order"))}:({x},{z},{w},{h}) p={Safe(GetMember(zone, "Priority"))} d={distance.ToString("0.0", CultureInfo.InvariantCulture)} inside={Safe(GetMember(zone, "FactionActorsInside"))}");
        }
        return $"zones={GetCollectionCount(zones)} shown={parts.Count} [{string.Join(";", parts)}]";
    }

    private static void AIRouteSnapshot(object actor, object agent, string phase)
    {
        if (actor == null) return;
        object faction = InvokeNoArg(agent, "GetFaction");
        object role = InvokeNoArg(agent, "GetRole");
        AIRouteWrite(actor, "STATE", $"phase={phase} {AIRouteActorState(actor)} " +
            $"sleeping={Safe(InvokeNoArg(agent, "IsSleeping"))} agentDone={Safe(GetMember(agent, "IsTurnDone"))} " +
            $"state={Safe(InvokeNoArg(agent, "GetState"))} iterations={Safe(GetMember(agent, "m_Iterations"))} " +
            $"knownOpponents={Safe(InvokeNoArg(faction, "GetKnownOpponents"))} " +
            $"zoneWeight={Safe(GetMember(role, "ZoneImportanceWeight"))} distanceWeight={Safe(GetMember(role, "DistanceScale"))} " +
            AIRouteZones(agent, actor), budgetKind: "STATE-" + phase);
    }

    private static void AIRouteFactionRound(object __instance)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            object actors = InvokeNoArg(__instance, "GetActors");
            List<object> snapshot = SnapshotCollection(actors, 2048);
            foreach (object actor in snapshot)
                AIRouteSnapshot(actor, InvokeNoArg(actor, "GetAgent"), "round-start");
            object opponents = InvokeNoArg(__instance, "GetOpponents");
            var contacts = new List<string>();
            foreach (object opponent in SnapshotCollection(opponents, 64))
            {
                object target = GetMember(opponent, "Actor");
                contacts.Add($"{GetStableObjectKey(target):X}:{AIRouteTemplate(target)} known={Safe(InvokeNoArg(opponent, "IsKnown"))} ttl={Safe(GetMember(opponent, "TTL"))}");
            }
            object manager = InvokeStaticNoArg(_tacticalManagerType, "Get");
            _log.Msg($"{ModName}: AI ROUTE ROUND map={GetStableObjectKey(InvokeNoArg(manager, "GetMap")):X} round={Safe(GetMember(manager, "m_Round"))} faction={Safe(InvokeNoArg(__instance, "GetIndex"))} actors={GetCollectionCount(actors)} sampled={snapshot.Count} dead={GetCollectionCount(InvokeNoArg(__instance, "GetDeadActors"))} opponents={GetCollectionCount(opponents)} shown={contacts.Count} [{string.Join(";", contacts)}].");
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteAgentTurnPostfix(object __instance)
    {
        if (!_aiRouteEnabled) return;
        try { AIRouteSnapshot(InvokeNoArg(__instance, "GetActor"), __instance, "agent-start"); }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static string AIRouteBehavior(object behavior)
        => behavior == null ? "<none>" : $"{Safe(InvokeNoArg(behavior, "GetName"))}#{Safe(InvokeNoArg(behavior, "GetID"))}:score={Safe(InvokeNoArg(behavior, "GetScore"))}";

    private static void AIRouteChoicePostfix(object __instance, object __result)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            object actor = InvokeNoArg(__instance, "GetActor");
            object behaviors = InvokeNoArg(__instance, "GetBehaviors");
            var scores = new List<string>();
            object move = null;
            foreach (object behavior in SnapshotCollection(behaviors, 32))
            {
                scores.Add(AIRouteBehavior(behavior));
                move ??= AIRouteAsMove(behavior);
            }
            AIRouteWrite(actor, "CHOICE", $"{AIRouteActorState(actor)} chosen=[{AIRouteBehavior(__result)}] " +
                $"behaviors={GetCollectionCount(behaviors)} shown={scores.Count} scores=[{string.Join(";", scores)}] " +
                $"moveState=[{AIRouteMoveState(move)}]");
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteAgentExecutePostfix(object __instance, bool __result)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            object actor = InvokeNoArg(__instance, "GetActor");
            AIRouteWrite(actor, "EXECUTE", $"returned={__result} {AIRouteActorState(actor)} " +
                $"active=[{AIRouteBehavior(GetMember(__instance, "m_ActiveBehavior"))}] " +
                $"state={Safe(InvokeNoArg(__instance, "GetState"))} agentDone={Safe(GetMember(__instance, "IsTurnDone"))} " +
                $"iterations={Safe(GetMember(__instance, "m_Iterations"))} sleeping={Safe(InvokeNoArg(__instance, "IsSleeping"))}");
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    // Harmony keys __state by the callbacks' declaring type, not by method name.
    // Plugin already supplies AiPerfState for Behavior.Execute in the deep profiler.
    // Keep this string state in a separate type so both observers can coexist.
    private static class AIRouteBehaviorProbe
    {
        internal static void AIRouteBehaviorPrefix(object __instance, object __0, out string __state)
        {
            __state = null;
            if (!_aiRouteEnabled) return;
            try { __state = $"behavior=[{AIRouteBehavior(__instance)}] before=[{AIRouteActorState(__0)}]"; }
            catch (Exception ex) { AIRouteFailure(ex); }
        }

        internal static void AIRouteBehaviorPostfix(object __0, bool __result, string __state)
        {
            if (__state == null) return;
            try { AIRouteWrite(__0, "BEHAVIOR", $"{__state} returned={__result} after=[{AIRouteActorState(__0)}]"); }
            catch (Exception ex) { AIRouteFailure(ex); }
        }
    }

    private static object AIRouteAsMove(object behavior)
    {
        if (_moveBehaviorType == null || behavior == null) return null;
        if (_moveBehaviorType.IsInstanceOfType(behavior)) return behavior;
        // Lists of Behavior can return a base IL2CPP wrapper even for native Move.
        // Rewrap only the identified Move; never read derived fields on that base.
        if (Safe(InvokeNoArg(behavior, "GetID")) != "Move") return null;
        MethodInfo cast = behavior.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "TryCast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        return cast?.MakeGenericMethod(_moveBehaviorType).Invoke(behavior, null);
    }

    private static string AIRouteMoveState(object move)
    {
        if (move == null) return "<unavailable>";
        object target = GetMember(move, "m_TargetTile");
        return $"movementDone={Safe(GetMember(move, "m_IsMovementDone"))} hasMoved={Safe(GetMember(move, "m_HasMovedThisTurn"))} " +
            $"delaying={Safe(GetMember(move, "m_IsDelayingMovement"))} inert={Safe(GetMember(move, "m_IsInsideContainerAndInert"))} " +
            $"belowUtility={Safe(GetMember(move, "m_TurnsBelowUtilityThreshold"))} belowUtilityLastTurn={Safe(GetMember(move, "m_TurnsBelowUtilityThresholdLastTurn"))} " +
            $"candidates={GetCollectionCount(GetMember(move, "m_Destinations"))} target={DescribeTile(GetMember(target, "Tile"))} " +
            $"targetUtility={Safe(GetMember(target, "UtilityScore"))} targetAttackUtility={Safe(GetMember(target, "UtilityByAttacksScore"))} " +
            $"targetDistanceScore={Safe(GetMember(target, "DistanceScore"))}; stored target may be stale";
    }

    private static void AIRouteMoveEvaluated(object __instance, object __0, int __result)
    {
        if (!_aiRouteEnabled) return;
        try { AIRouteWrite(__0, "MOVE-EVAL", $"score={__result} {AIRouteActorState(__0)} {AIRouteMoveState(__instance)}"); }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteMoveNewTurn(object __instance)
    {
        if (!_aiRouteEnabled) return;
        try { AIRouteWrite(InvokeNoArg(InvokeNoArg(__instance, "GetAgent"), "GetActor"), "MOVE-RESET", AIRouteMoveState(__instance)); }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteMoveUtility(object __instance, object __0, bool __result)
    {
        if (!_aiRouteEnabled) return;
        try { AIRouteWrite(InvokeNoArg(InvokeNoArg(__instance, "GetAgent"), "GetActor"), "MOVE-UTILITY",
            $"hasUtility={__result} tiles={GetCollectionCount(__0)} {AIRouteMoveState(__instance)}"); }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteMoveExecutionPostfix(object __instance, object __0, bool __result)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            object chosen = GetMember(__instance, "m_TargetTile");
            AIRouteWrite(__0, "MOVE", $"{AIRouteActorState(__0)} target={DescribeTile(GetMember(chosen, "Tile"))} " +
                $"ultimate={DescribeTile(GetMember(chosen, "UltimateTile"))} returned={__result} " +
                $"pathNodes={GetCollectionCount(GetMember(__instance, "m_Path"))} candidates={GetCollectionCount(GetMember(__instance, "m_Destinations"))} " +
                $"movementDone={Safe(GetMember(__instance, "m_IsMovementDone"))} hasMoved={Safe(GetMember(__instance, "m_HasMovedThisTurn"))} " +
                $"delaying={Safe(GetMember(__instance, "m_IsDelayingMovement"))} inert={Safe(GetMember(__instance, "m_IsInsideContainerAndInert"))} " +
                $"belowUtility={Safe(GetMember(__instance, "m_TurnsBelowUtilityThreshold"))}");
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteMovementFinished(object __instance)
    {
        if (!_aiRouteEnabled) return;
        try { AIRouteWrite(__instance, "ARRIVED", $"{AIRouteActorState(__instance)} " + AIRouteZones(InvokeNoArg(__instance, "GetAgent"), __instance), false); }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteActorEnd(object __instance)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            AIRouteSnapshot(__instance, InvokeNoArg(__instance, "GetAgent"), "actor-end");
            AIRouteWrite(__instance, "COUNTS", _aiRouteBudget.Summary(GetStableObjectKey(__instance)));
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteSkillPrefix(object __instance, object __0, out string __state)
    {
        __state = null;
        if (!_aiRouteEnabled) return;
        try
        {
            object actor = InvokeNoArg(__instance, "GetActor");
            __state = $"skill={Safe(InvokeNoArg(__instance, "GetID"))} attack={Safe(InvokeNoArg(__instance, "IsAttack"))} " +
                $"target={DescribeTile(__0)} targetEntity=[{DescribeActor(InvokeNoArg(__0, "GetActor"))}] " +
                $"cost={Safe(InvokeNoArg(__instance, "GetActionPointCost"))} before=[{AIRouteActorState(actor)}]";
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteSkillPostfix(object __instance, bool __result, string __state)
    {
        if (__state == null) return;
        try
        {
            object actor = InvokeNoArg(__instance, "GetActor");
            AIRouteWrite(actor, "SKILL", $"{__state} accepted={__result} after=[{AIRouteActorState(actor)}]", false);
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteSkillApplied(object __instance, object __0)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            AIRouteWrite(InvokeNoArg(__instance, "GetActor"), "APPLIED",
                $"skill={Safe(InvokeNoArg(__instance, "GetID"))} target={DescribeTile(__0)} " +
                $"attack={Safe(InvokeNoArg(__instance, "IsAttack"))}; effect processing, not proof of hit/kill", false);
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }

    private static void AIRouteAttackEvaluated(object __instance, object __0, int __result)
    {
        if (!_aiRouteEnabled) return;
        try
        {
            object skill = InvokeNoArg(__instance, "GetSkill");
            object candidates = GetMember(__instance, "m_Candidates");
            var targets = new List<string>();
            foreach (object candidate in SnapshotCollection(candidates, 3))
                targets.Add($"{DescribeTile(GetMember(candidate, "Target"))}:score={Safe(GetMember(candidate, "Score"))}");
            AIRouteWrite(__0, "ATTACK-EVAL", $"skill={Safe(InvokeNoArg(skill, "GetID"))} score={__result} " +
                $"candidates={GetCollectionCount(candidates)} shown={targets.Count} [{string.Join(";", targets)}] " +
                $"ap={Safe(InvokeNoArg(__0, "GetActionPoints"))}");
        }
        catch (Exception ex) { AIRouteFailure(ex); }
    }
}
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.Skills;
using AttackBehavior = Il2CppMenace.Tactical.AI.Behaviors.Attack;

namespace MenaceWeaponMechanicsRebalanced;

public sealed partial class Plugin
{
    private static readonly MortarFireMissionState EnemyMortarMissions = new();
    private static bool _enemyMortarFirePriority = true, _enemyMortarAiInstalled, _enemyMortarAiErrorLogged;
    private static int _enemyMortarFireRounds = 3, _enemyMortarMainThread;

    private static void InstallEnemyMortarAi(global::HarmonyLib.Harmony harmony)
    {
        _enemyMortarMainThread = Environment.CurrentManagedThreadId;
        try
        {
            var evaluate = global::HarmonyLib.AccessTools.Method(typeof(AttackBehavior), "OnEvaluate", new[] { typeof(Actor) });
            var choose = global::HarmonyLib.AccessTools.Method(typeof(Agent), "PickBehavior", Type.EmptyTypes);
            if (evaluate == null || choose == null) throw new MissingMethodException("enemy mortar AI hooks");
            // Warm callback/state methods on Unity's main thread before any native
            // AI worker can enter the main-thread guard (shared-address/JIT issue #44).
            foreach (var type in new[] { typeof(Plugin), typeof(MortarFireMissionState) })
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (type != typeof(Plugin) || method.Name.Contains("EnemyMortar", StringComparison.Ordinal))
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
            harmony.Patch(evaluate, postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(EnemyMortarEvaluate)));
            harmony.Patch(choose, postfix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(EnemyMortarChoose)));
            _enemyMortarAiInstalled = true;
        }
        catch (Exception ex) { DisableEnemyMortarAi(ex); }
    }

    private static bool EnemyMortarAiActive => _enabled && _enemyMortarFirePriority && _enemyMortarAiInstalled &&
        Environment.CurrentManagedThreadId == _enemyMortarMainThread;

    private static bool EnemyMortarEligible(Agent? agent, Actor? actor) => agent != null && actor != null &&
        IsHostileMortarActor(actor) && agent.GetFaction()?.IsAlliedWithPlayer() == false &&
        !agent.IsSleeping() && !agent.IsDeploymentPhase() && !actor.IsDying() && !actor.IsMoving() &&
        !actor.IsTurnDone() && !actor.IsActionPointsSpent() && !actor.IsContainedWithinAnotherEntity();

    private static void EnemyMortarEvaluate(AttackBehavior __instance, Actor __0, ref int __result)
    {
        if (!EnemyMortarAiActive) return;
        try
        {
            Agent? agent = __instance.GetAgent();
            if (!EnemyMortarEligible(agent, __0)) return;
            Skill? skill = __instance.GetSkill();
            // HE only; smoke and other suppression weapons retain native decisions.
            if (skill == null || !MortarSkillSpecs.ContainsKey(skill.GetID()) || skill.IsOutOfUses()) return;
            var manager = TacticalManager.Get();
            var map = manager?.GetMap();
            Tile? origin = __0.GetTile();
            if (map == null || origin == null) return;
            EnemyMortarMissions.Observe(NativeKey(map), manager!.m_Round);
            var key = (NativeKey(__0), NativeKey(agent), NativeKey(skill));
            Tile? nativeTarget = __instance.m_TargetTile;
            bool nativeCandidate = false;
            var candidates = __instance.m_Candidates;
            if (nativeTarget != null && candidates != null)
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate.Target != null && NativeKey(candidate.Target) == NativeKey(nativeTarget) &&
                        float.IsFinite(candidate.Score) && candidate.Score > 0) { nativeCandidate = true; break; }
                }

            // A native-scored target may require native stance/setup actions. Do not
            // create deployment flags or make unusable weapons shoot by force.
            if (nativeCandidate && (__result > 0 || (skill.IsUsable() && skill.IsAffordable() && skill.IsUsableOn(nativeTarget!, origin))))
                EnemyMortarMissions.Acquire(key, nativeTarget!.GetX(), nativeTarget.GetZ(), _enemyMortarFireRounds);

            if (EnemyMortarMissions.TryGet(key, out var remembered))
            {
                Tile? target = map.GetTile(remembered.X, remembered.Z);
                bool ready = !__instance.m_DeployBeforeExecuting && !__instance.m_SetupBeforeExecuting &&
                    !__instance.m_RotateBeforeExecuting &&
                    !__instance.m_DontActuallyExecute && !__instance.m_IsExecuted;
                if (ready && target != null && skill.IsUsable() && skill.IsAffordable() &&
                    skill.IsUsableOn(target, origin) && EnemyMortarSafeArea(__instance, skill, __0, map, target))
                {
                    __instance.m_TargetTile = target;
                    __result = MortarFireMissionState.Priority(__result);
                    return;
                }
            }
            if (nativeCandidate && __result > 0)
                __result = MortarFireMissionState.Priority(__result);
        }
        catch (Exception ex) { DisableEnemyMortarAi(ex); }
    }

    private static bool EnemyMortarSafeArea(AttackBehavior attack, Skill skill, Actor actor, Map map, Tile target)
    {
        // Conservatively include scatter and native AoE. Never resolve structures
        // as Actors, and never inspect an unseen opponent's current coordinates.
        int radius = Math.Max(1, skill.GetTemplate().Scatter + Math.Max(0, attack.m_AdditionalRadius));
        if (radius > 12) return false;
        for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var tile = map.GetTile(target.GetX() + dx, target.GetZ() + dz);
                if (tile == null || !tile.HasActor()) continue;
                var occupant = tile.GetActor();
                if (occupant != null && occupant.IsAlliedWith((int)actor.GetFaction())) return false;
            }
        return true;
    }

    private static void EnemyMortarChoose(Agent __instance, ref Behavior __result)
    {
        if (!EnemyMortarAiActive) return;
        try
        {
            if (!EnemyMortarEligible(__instance, __instance.GetActor()) || __result == null) return;
            string chosen = __result.GetID().ToString();
            if (chosen != "Move" && chosen != "Idle") return; // Preserve emergencies/confusion/other actions.
            AttackBehavior? best = null;
            foreach (Behavior behavior in __instance.GetBehaviors())
            {
                var attack = behavior.TryCast<AttackBehavior>();
                if (attack == null || attack.GetScore() < 250 || attack.m_TargetTile == null ||
                    !MortarSkillSpecs.ContainsKey(attack.GetSkill().GetID())) continue;
                if (best == null || attack.GetScore() > best.GetScore()) best = attack;
            }
            if (best != null) __result = best; // Native Execute still owns deployment, AP, ammo and animation.
        }
        catch (Exception ex) { DisableEnemyMortarAi(ex); }
    }

    private static void DisableEnemyMortarAi(Exception ex)
    {
        _enemyMortarAiInstalled = false;
        EnemyMortarMissions.Clear();
        if (_enemyMortarAiErrorLogged) return;
        _enemyMortarAiErrorLogged = true;
        Warn($"Enemy mortar AI disabled; native decisions retained ({ex.GetType().Name}).");
    }
}
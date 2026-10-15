"""PR #12 -> Jiangyu guards. Native execution/performance still need game tests."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / "src/SmokeOverhaul/Plugin.cs").read_text()
AI = (ROOT / "src/SmokeOverhaul/AiSmoke.cs").read_text()
RULES = (ROOT / "src/SmokeOverhaul/SmokeAiRules.cs").read_text()


class LightweightSmokeTests(unittest.TestCase):
    def test_pinned_ap_only_for_actual_pinned_hand_thrown_smoke(self):
        cost = AI.split("internal static void ApplyPinnedSmokeApCost(", 1)[1].split("private static void AiSmokeSceneChanged(", 1)[0]
        for guard in ("!AiSmokeOnMainThread", "!RuntimeSettings.Enabled", "!RuntimeSettings.AllowSmokeWhilePinnedDown", "cost <= 0",
                      "skill.GetAPConsumer()", "GetSuppressionState() == SuppressionState.PinnedDown", "IsHandThrownSmokeSkillId(skill.GetID())"):
            self.assertIn(guard, cost)
        self.assertIn("cost = 0;", cost)
        self.assertNotIn("SetActionPoints(", AI)
        ids = AI.split("private static readonly string[] InfantrySmokeSkillIds =", 1)[1].split(";", 1)[0]
        self.assertEqual(ids.count('"active.'), 2)
        self.assertIn('"active.throw_smoke_grenade_limited"', ids)

    def test_native_checks_and_fallback_preserved(self):
        for required in ("agent.IsSleeping()", "agent.IsDeploymentPhase()", "TacticalManager.IsSkillBusy()", "actor.IsInfantry()",
                         "actor.IsDying()", "actor.IsMoving()", "actor.IsTurnDone()", "origin.GetCoverMask() == 0", "HasKnownOpponent()",
                         "cast.IsOutOfUses()", "cast.IsUsable()", "cast.IsAffordable()", "smoke.IsUsableOn(target, origin)",
                         "target.IsEmpty()", "target.IsValidMovementDestination()", "TileHasSmoke(origin)", "TileHasSmoke(target)"):
            self.assertIn(required, AI)
        self.assertIn("if (!smoke.Use(target, UsageParameter.AutoAudioVolume)) return false;", AI)
        self.assertIn("if (!__runOriginal || !Plugin.TryAiSmokeAction(__instance)) return true;", AI)

    def test_once_per_actor_turn_and_only_after_native_legality_roll(self):
        action = AI.split("internal static bool TryAiSmokeAction(", 1)[1].split("private static bool TryGetDetectedSmokeThreat(", 1)[0]
        self.assertEqual(action.count("Random.Shared.Next(100)"), 1)
        for earlier, later in (("_smokeActedThisTurn.Contains(actorKey)", "_smokeActedThisTurn.Add(actorKey)"),
                               ("_smokeActedThisTurn.Add(actorKey)", "Random.Shared.Next(100)"),
                               ("smoke.IsUsableOn(target, origin)", "Random.Shared.Next(100)"),
                               ("Random.Shared.Next(100)", "smoke.Use(target, UsageParameter.AutoAudioVolume)")):
            self.assertLess(action.index(earlier), action.index(later))
        self.assertIn("_smokeActedThisTurn.Remove(actor.Pointer)", AI)
        self.assertIn("AiSmokeSceneChanged(sceneName)", PLUGIN)
        self.assertIn("_smokeActedThisTurn.Clear();", PLUGIN)

    def test_detected_targets_only_and_fixed_local_budgets(self):
        threat = AI.split("private static bool TryGetDetectedSmokeThreat(", 1)[1]
        self.assertIn("private const int MaxSmokeThreatChecks = 24;", AI)
        self.assertIn("Math.Min(opponents.Count, MaxSmokeThreatChecks)", threat)
        self.assertLess(threat.index("opponent.IsKnown()"), threat.index("enemy.GetTile()"))
        self.assertLess(threat.index("enemy.IsDetectedByFaction(faction.GetIndex())"), threat.index("enemy.GetTile()"))
        self.assertIn("attackerDistance <= nearest * 4", threat)
        self.assertIn("reachIndex < 2", AI)
        self.assertIn("candidate < 3", AI)
        for forbidden in ("FindPath(", "GetPath(", "GetActors(", "ForEachActor(", "GetDirection(", "IgnoreUsabilityCheck", "NoUsesConsumed"):
            self.assertNotIn(forbidden, AI)

    def test_original_probabilities_are_not_rebalanced(self):
        for constant in ("PinnedChance = 60", "SuppressedChance = 45", "FlamethrowerChance = 30"):
            self.assertIn(constant, RULES)
        self.assertIn("SmokeAiRules.Chance(pinned, suppressed)", AI)
        self.assertIn("SmokeAiRules.TryGetLocalOffset(", AI)

    def test_late_prefix_respects_assault_and_worker_fallback(self):
        self.assertIn("pre.priority = global::HarmonyLib.Priority.Last;", PLUGIN)
        self.assertIn("bool __runOriginal", AI)
        self.assertIn("Environment.CurrentManagedThreadId == _aiSmokeMainThreadId", AI)
        self.assertIn("_aiSmokeMainThreadId = Environment.CurrentManagedThreadId;", PLUGIN)
        self.assertNotIn("Agent.Evaluate", PLUGIN.split("public void Initialize(", 1)[1].split("public void OnSceneLoaded(", 1)[0])

    def test_owned_permission_restore_and_debug_off(self):
        self.assertIn("template.IsUsableWhilePinnedDown)", AI)
        self.assertIn("OriginalPinnedSmokePermissions.Add(template.Pointer, template)", AI)
        self.assertIn("template.IsUsableWhilePinnedDown = false;", AI)
        self.assertIn("OriginalPinnedSmokePermissions.Clear();", AI)
        self.assertIn("RestorePinnedSmokePermission();", PLUGIN)
        self.assertIn('_aiSmokeDirectionDebugLines < 24', AI)
        self.assertIn('s.AddToggle("DebugLogging", "Debug logging", false)', PLUGIN)


if __name__ == "__main__":
    unittest.main()
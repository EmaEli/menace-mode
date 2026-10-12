"""MEN-001 source guards. Windows build and gameplay remain mandatory."""
import json
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SRC=(ROOT/"src/SmokeOverhaul/Plugin.cs").read_text()
def between(a,b):
    assert a in SRC and b in SRC
    return SRC.split(a,1)[1].split(b,1)[0]
class SmokeRegression(unittest.TestCase):
    def test_versions(self):
        self.assertIn('internal const string Version = "0.6.1"',SRC)
        self.assertIn('AssemblyVersion("0.6.1.0")',SRC)
        self.assertEqual(json.loads((ROOT/"packaging/standalone/SmokeOverhaul/modpack.json").read_text())["version"],"0.6.1")
        self.assertEqual(json.loads((ROOT/"packaging/suite/MenaceGameplaySuite/modpack.json").read_text())["version"],"0.33.1")
    def test_pinned_ap_only_smoke(self):
        x=between("internal static void ApplyPinnedSmokeApCost(","// One emergency smoke decision")
        self.assertIn("GetSuppressionState() != SuppressionState.PinnedDown",x)
        self.assertIn("IsHandThrownSmokeSkillId(skill.GetID())",x)
        self.assertIn("cost = 0;",x)
        self.assertNotIn("SetActionPoints(",x)
    def test_bounded_native_action(self):
        x=between("internal static bool TryAiSmokeAction(","private static bool _aiSmokeActionErrorLogged;")
        for required in ("agent.IsSleeping()","agent.IsDeploymentPhase()","TacticalManager.IsSkillBusy()",
                         "_smokeActedThisTurn.Contains(actorKey)","actor.GetSuppressionState()",
                         "origin.GetCoverMask() == 0","HasKnownOpponent()","GetAllSkills()",
                         "TryCast<Skill>()","cast.IsUsable()","cast.IsAffordable()",
                         "smoke.IsUsableOn(target, origin)","smoke.Use(target, UsageParameter.AutoAudioVolume)",
                         "TileHasSmoke(origin)","TileHasSmoke(target)"):
            self.assertIn(required,x)
        for forbidden in ("FindPath(","GetPath(","GetKnownOpponents(","GetVisibleOpponents(",
                          "GetActors(","ForEachActor(","IgnoreUsabilityCheck","NoUsesConsumed","SetActionPoints("):
            self.assertNotIn(forbidden,x)
    def test_ai_hook_and_local_limits(self):
        init=between("public void OnInitialize(","public void OnSceneLoaded(")
        self.assertEqual(init.count("hooks += TryPatch("),11)
        self.assertIn("nameof(Agent.Execute)",init)
        self.assertIn("nameof(Agent.OnTurnStart)",init)
        self.assertNotIn("Agent.Evaluate",init)
        self.assertIn("global::HarmonyLib.Priority.Last",SRC)
        self.assertIn("bool __runOriginal",SRC)
        self.assertIn("DefensiveSmokeOffsets = { 0, -1, 1, -2, 2, 4 };",SRC)
        self.assertIn("FlamethrowerSmokeOffsets = { 0, 0 };",SRC)
        self.assertIn("_smokeActedThisTurn.Remove(actor.Pointer)",SRC)
        self.assertIn('s.AddToggle("DebugLogging", "Debug logging", false)',SRC)
    def test_cleanup(self):
        self.assertIn("snapshot.Template.IsUsableWhilePinnedDown = snapshot.Original",SRC)
        self.assertIn("RestorePinnedSmokePermission();",SRC)
if __name__=="__main__": unittest.main()

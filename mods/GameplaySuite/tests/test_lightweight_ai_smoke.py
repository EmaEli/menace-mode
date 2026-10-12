"""MEN-001 source-level safeguards. Windows compilation and gameplay testing remain mandatory."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
S = (ROOT / "src/SmokeOverhaul/Plugin.cs").read_text()
def block(start,end):
    return S.split(start,1)[1].split(end,1)[0]

class TestMen001Smoke(unittest.TestCase):
    def test_smoke_only_pinned(self):
        x=block("internal static void ApplyPinnedSmokeApCost(","private static readonly Dictionary<IntPtr, bool> FlameSkillCache")
        self.assertIn("GetSuppressionState() != SuppressionState.PinnedDown",x)
        self.assertIn("IsHandThrownSmokeSkillId(skill.GetID())",x)
        self.assertIn("cost = 0;",x)
        self.assertNotIn("SetActionPoints(",x)
    def test_native_ai_scoping(self):
        x=block("internal static void BoostAiSmoke(","private static void ApplyNativeSmokeAiToMortars()")
        for word in ("!immediate","FastAISmokeSelection","IsHandThrownSmokeSkillId(skill.GetID())",
                     "agent.IsSleeping()","range > 2","TileHasSmoke(origin)","TileHasSmoke(target)",
                     "SuppressionState.PinnedDown","SuppressionState.Suppressed",
                     "HasKnownOpponent()","FlameSkillCache.TryGetValue"):
            self.assertIn(word,x)
        for word in ("GetPath(","FindPath(","GetMap(","skill.Use(","SetActionPoints("):
            self.assertNotIn(word,x)
    def test_hooks_and_cleanup(self):
        self.assertEqual(block("public void OnInitialize(","public void OnSceneLoaded(").count("hooks += TryPatch("),10)
        self.assertIn("snapshot.Template.IsUsableWhilePinnedDown = snapshot.Original",S)
        self.assertIn("RestorePinnedSmokePermission();",S)
        self.assertIn("FlameSkillCache.Clear();",S)
        self.assertIn('s.AddToggle("DebugLogging", "Debug logging", false)',S)
    def test_versions(self):
        self.assertEqual(json.loads((ROOT/"packaging/standalone/SmokeOverhaul/modpack.json").read_text())["version"],"0.6")
        self.assertEqual(json.loads((ROOT/"packaging/suite/MenaceGameplaySuite/modpack.json").read_text())["version"],"0.33")

if __name__=="__main__":
    unittest.main()

import importlib.util
import json
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("mission_log", Path(__file__).resolve().parents[1] / "tools/analyze_mission_log.py")
ANALYZER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ANALYZER)


class MissionLogTests(unittest.TestCase):
    def test_empty_log_is_not_a_pass(self):
        result = ANALYZER.analyze(b"")
        self.assertFalse(result["ai"]["consecutive"])
        self.assertIsNone(result["game_version"])
        self.assertEqual(result["wrr"]["range_pass_records"], 0)

    def test_actor_activations_are_not_rounds(self):
        result = ANALYZER.analyze(b"[12:00:00.000] AI PERF turn=#1 round=1 pending=0 droppedTasks=0 closedBy=finish\n"
                                  b"[12:00:01.000] AI PERF turn=#2 round=1 pending=0 droppedTasks=0 closedBy=next-activation\n"
                                  b"[12:00:02.000] AI PERF turn=#3 round=2 pending=1 droppedTasks=2\n")
        self.assertEqual(result["ai"]["activation_count"], 3)
        self.assertEqual(result["ai"]["rounds"], {1: 2, 2: 1})
        self.assertEqual(result["ai"]["pending"], {0: 2, 1: 1})
        self.assertTrue(result["ai"]["consecutive"])

    def test_post_fire_changes_use_both_numbers(self):
        result = ANALYZER.analyze(b"[ASSAULT_DEBUG] defensive-post hp=106->74 armor=225->174 suppression=3.33->9.31 morale=123.08->80.46 intendedMatch=False\n")
        self.assertEqual(result["assault"]["defensive_post_changes"]["hp"], {"observed": 1, "changed": 1})
        self.assertEqual(result["assault"]["managed_identity_false"], 1)

    def test_no_raw_private_text_is_emitted(self):
        private = "synthetic-private-marker-not-a-real-secret"
        result = ANALYZER.analyze((f"[WARNING] {private}\n[ERROR] {private}\nactor={private} save={private}\n").encode())
        self.assertNotIn(private, json.dumps(result))
        self.assertEqual(result["warnings"], [{"line": 1, "code": "unclassified"}])
        self.assertEqual(result["errors"]["total_lines"], 1)

    def test_ammunition_and_camera_errors(self):
        result = ANALYZER.analyze(b"[MEN-004 LEDGER] op=ConsumeUse uses=15->14 max=16\n"
                                  b"[ERROR] Automatic third-person event AttackStarted failed\n")
        self.assertEqual(result["ammo"]["consume_decrements"], {1: 1})
        self.assertEqual(result["errors"]["camera_failure_records"], 1)

    def test_black_market_headstart_is_not_the_custom_leader_failure(self):
        result = ANALYZER.analyze(b"[WARNING] Il2CppMenace.UI.Menu.NewGameWindow m_BlackMarketHeadstartToggle\n")
        self.assertEqual(result["warnings"][0]["code"], "starting_options_field_probe")

    def test_both_observer_and_target_snapshots_are_counted(self):
        result = ANALYZER.analyze(b"voVisionApplicationsInUpdateCall=1 voConcealmentApplicationsInUpdateCall=0 observerStats voVisionApplicationsInUpdateCall=1 voConcealmentApplicationsInUpdateCall=1\n")
        self.assertEqual(result["visibility"]["vision_application_counts"], {1: 2})
        self.assertEqual(result["visibility"]["concealment_application_counts"], {0: 1, 1: 1})


if __name__ == "__main__":
    unittest.main()
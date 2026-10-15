"""Cached-data/source contracts, NOT First Aid/repair/identity runtime tests."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/SquaddieVeterancy/ProbeRuntime.cs").read_text()
STORE = (ROOT / "src/SquaddieVeterancy/ExperienceStore.cs").read_text()
DATA = json.loads((Path(__file__).parent / "fixtures/healing_skills.json").read_text())
SKILLS = {s["name"]: s for s in DATA["skills"]}


class HealingContractTests(unittest.TestCase):
    def test_cached_first_aid_is_not_a_refill_squaddies_handler(self):
        first_aid = SKILLS["perk.first_aid"]
        self.assertFalse(first_aid["IsAttack"])
        self.assertIn("Regeneration", first_aid["handlerTypes"])
        self.assertNotIn("RefillSquaddies", first_aid["handlerTypes"])
        # Passive rescue chance is not the active fresh-member refill handler.
        self.assertIn("ChangeSquaddieRescue", first_aid["handlerTypes"])

    def test_cached_reinforcements_is_a_separate_nonattack_refill(self):
        reinforcements = SKILLS["perk.reinforcements"]
        self.assertFalse(reinforcements["IsAttack"])
        self.assertIn("RefillSquaddies", reinforcements["handlerTypes"])
        self.assertNotIn("Regeneration", reinforcements["handlerTypes"])

    def test_reliability_uses_real_attack_filter_not_every_nonattack_skill(self):
        classifier = SOURCE.split("private bool IsRealAttack(Skill skill)", 1)[1].split("private bool IsXpAttack(", 1)[0]
        self.assertIn("if (skill.IsAttack()) return true;", classifier)
        self.assertIn('return lower.StartsWith("active.fire_", StringComparison.Ordinal);', classifier)
        self.assertGreaterEqual(SOURCE.count("if (!c.IsRealAttack(__instance)) return;"), 2)
        self.assertIn("if (!c.IsRealAttack(_skill)) return;", SOURCE)

    def test_element_hp_creation_is_restricted_to_known_squaddie_identity(self):
        prefix = SOURCE.split("private static void EntityCreateElementPrefix(", 1)[1].split("private static void EntityCreateElementPostfix(", 1)[0]
        self.assertIn("if (sid <= 0 || baseHp <= 0 || baseHp > 1000) return;", prefix)
        guard = "if (!isPlayerUnit || !c._identities.Contains(sid)) return;"
        self.assertIn(guard, prefix)
        self.assertLess(prefix.index(guard), prefix.index("c.GetTeamHpBonus(sid)"))
        postfix = SOURCE.split("private static void EntityCreateElementPostfix(", 1)[1].split("private static", 1)[0]
        self.assertIn("!__state.PlayerSquaddie", postfix)
        self.assertIn("state.SquaddieId = __state.SquaddieId;", postfix)

    def test_roster_store_keys_xp_by_id_and_resets_mismatched_people(self):
        roster = STORE.split("public void SyncRoster(", 1)[1].split("public bool IsRosterCompatibleWithLoadedRecords(", 1)[0]
        self.assertIn("if (s.Id <= 0) continue;", roster)
        self.assertIn("_records.TryGetValue(s.Id, out var r)", roster)
        self.assertIn("new Record { Id = s.Id, Name = s.Name, Nickname = s.Nickname, Xp = 0 }", roster)
        self.assertIn("!string.Equals(r.Name, s.Name, StringComparison.Ordinal)", roster)
        self.assertIn("r.Xp = 0;", roster)
        self.assertIn("r.Nickname = s.Nickname;", roster)

    def test_cached_frk_nonattack_clones_do_not_match_fire_id_fallback(self):
        repair = DATA["thirdPartyRepairKit"]
        self.assertFalse(SKILLS[repair["cloneSourceId"]]["IsAttack"])
        self.assertTrue(repair["clearsNativeEventHandlers"])
        self.assertEqual(len(repair["cloneIds"]), 4)
        for skill_id in repair["cloneIds"]:
            self.assertFalse(skill_id.startswith("active.fire_"))
        # Metadata/source guard only: this does NOT instantiate third-party skills.

    def test_vehicle_pressure_does_not_treat_negative_damage_as_a_damaging_hit(self):
        guard = "if (!inflicted || damage <= 0 || destroyed || c._vehicleDamagePressureBump <= 0) return;"
        self.assertIn(guard, SOURCE)
        self.assertLess(SOURCE.index(guard), SOURCE.index('string dedup = $"vehicle-pressure-damage:'))


if __name__ == "__main__":
    unittest.main()
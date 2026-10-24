from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[3]
VETERANCY = ROOT / "mods/GameplaySuite/src/SquaddieVeterancy"


def method_body(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1:index]
    raise AssertionError(f"Unclosed method body for {signature}")


class VehicleCrewIdentityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.store = (VETERANCY / "ExperienceStore.cs").read_text()
        cls.runtime = (VETERANCY / "ProbeRuntime.cs").read_text()
        cls.api = (VETERANCY / "CompatibilityApi.cs").read_text()

    def test_person_id_is_persisted_and_copied_separately_from_pool_key(self):
        record = method_body(self.store, "public sealed class CrewMemberRecord")
        clone = method_body(self.store, "private static CrewMemberRecord CloneCrewMember")
        self.assertIn("public string Key", record)
        self.assertIn("public string PersonId", record)
        self.assertIn("PersonId = m.PersonId", clone)

    def test_normalization_repairs_missing_and_duplicate_ids_without_reassigning_xp(self):
        normalize = method_body(self.store, "private static bool NormalizeCrewRecord")
        self.assertIn("CrewPersonIdentity.EnsureUnique(member.PersonId, personIds)", normalize)
        self.assertIn("member.PersonId = personId", normalize)
        self.assertIn("changed = true", normalize)
        self.assertIn("if (member.Xp < 0) { member.Xp = 0;", normalize)
        self.assertEqual(normalize.count("member.Xp ="), 1)

    def test_death_replacement_changes_identity_but_wounding_does_not(self):
        death = method_body(self.store, "public CrewMutationResult KillAndReplaceCrewMember")
        wound = method_body(self.store, "public CrewMutationResult WoundCrewMember")
        self.assertIn("member.PersonId = CrewPersonIdentity.Create()", death)
        self.assertIn('member.Status = "Replacement"', death)
        self.assertNotIn("PersonId", wound)

    def test_compatibility_snapshot_uses_copied_non_mutating_store_reads(self):
        members = method_body(self.store, "public IReadOnlyList<CrewMemberRecord> GetPilotCrewMembersSnapshot")
        active = method_body(self.store, "public IReadOnlyList<string> GetActivePilotCrewKeysSnapshot")
        snapshot = method_body(self.runtime, "internal static VehicleCrewMemberSnapshot[] GetVehicleCrewForCompatibility")
        for body in (members, active, snapshot):
            self.assertNotIn("NormalizeCrewRecord", body)
            self.assertNotIn("_dirty", body)
            self.assertNotIn("SyncPilots", body)
        self.assertIn("GetPilotCrewMembersSnapshot", snapshot)
        self.assertIn("GetActivePilotCrewKeysSnapshot", snapshot)
        self.assertNotIn("GetPilotRoster(", snapshot)

    def test_vehicle_crew_api_has_independent_version_and_value_snapshot(self):
        self.assertIn("VehicleCrewApiVersion = 2", self.api)
        self.assertIn("GetVehicleCrewMembers()", self.api)
        self.assertIn("public string PersonId", self.api)
        self.assertIn("public bool IsActive", self.api)


if __name__ == "__main__":
    unittest.main()
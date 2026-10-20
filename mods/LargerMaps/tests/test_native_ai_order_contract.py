"""Optional byte-level evidence for the reviewed 0.7.17 order gate, not execution."""
import hashlib
import json
import os
from pathlib import Path
import struct
import unittest


class NativeOrderContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        filename = os.environ.get('MENACE_GAME_ASSEMBLY')
        if not filename:
            raise unittest.SkipTest('Set MENACE_GAME_ASSEMBLY for native 0.7.17 checks')
        cls.binary = Path(filename).read_bytes()
        builds = json.loads((Path(__file__).with_name('pathfinding-sites.json')).read_text())['builds']
        expected = next(b['sha256'] for b in builds if b['game_version'] == 'v0.7.17+22362')
        if hashlib.sha256(cls.binary).hexdigest() != expected:
            raise AssertionError('These instruction assertions apply only to the reviewed 0.7.17 binary')
        pe = struct.unpack_from('<I', cls.binary, 0x3c)[0]
        count = struct.unpack_from('<H', cls.binary, pe + 6)[0]
        table = pe + 24 + struct.unpack_from('<H', cls.binary, pe + 20)[0]
        cls.sections = [struct.unpack_from('<IIII', cls.binary, table + i * 40 + 8)
                        for i in range(count)]

    def read_rva(self, rva, count):
        for _, virtual, size, raw in self.sections:
            if virtual <= rva and rva + count <= virtual + size:
                offset = raw + rva - virtual
                return self.binary[offset:offset + count]
        self.fail(f'RVA {rva:X} not backed by section bytes')

    def assert_code(self, rva, hex_bytes):
        expected = bytes.fromhex(hex_bytes)
        self.assertEqual(self.read_rva(rva, len(expected)), expected, f'RVA {rva:X}')

    def assert_call(self, rva, target):
        instruction = self.read_rva(rva, 5)
        self.assertEqual(instruction[0], 0xE8)
        self.assertEqual(rva + 5 + struct.unpack_from('<i', instruction, 1)[0], target)

    def test_agents_read_shared_faction_operational_zones(self):
        # mov rax,[rcx+10]; null check; mov rax,[rax+70]. Not a private unit plan.
        self.assert_code(0x7B29B0, '4883ec28488b41104885c07409488b40704883c428c3')
        self.assert_call(0x7B0436, 0x7D72F0)  # AIFaction.Think -> StrategyData.Update

    def test_advance_distance_incentive_is_skipped_with_any_known_opponent(self):
        self.assert_call(0x7DB791, 0x7AE980)  # HasKnownOpponent, not IgnoreCivilians
        # inc r12d; test r13b,r13b; jne next-zone (0x7DB8D0).
        self.assert_code(0x7DB9E5, '41ffc44584ed0f85dffeffff')

    def test_advance_inside_zone_utility_is_skipped_too(self):
        self.assert_call(0x7DBE0C, 0x7AE980)
        # test bl,bl; jne next-zone (0x7DBEA0).
        self.assert_code(0x7DBEF1, '84db75ab41ffc7')

    def test_known_gate_uses_ttl_sign_without_civilian_filter(self):
        self.assert_code(0x7AEA3F, '8b4018c1e81f84c075d7')
        # The separate IgnoreCivilians method DOES skip faction index 3 first.
        self.assert_code(0x7AE908, '83794c0374d28b4018c1e81f84c075c8')

    def test_recovered_distance_uses_native_weights_not_arbitrary_bonus(self):
        # int distance * WEIGHTS.DistanceToZones * DistanceToAdvanceZones
        # * (role.ZoneImportanceWeight * zone.Priority), added to DistanceScore.
        self.assert_code(0x7DBA63, '660f6ec60f5bc0f30f594258f30f59425cf30f59c6f30f58c7f30f114724')

    def test_inside_utility_uses_native_threshold_and_zone_priority(self):
        self.assert_code(0x7DE823, 'f30f5ff70f287c2420f30f597018')  # max base/scaled * threshold scale
        self.assert_code(0x7DBE49, '885c2421f30f597028')  # * role.ZoneImportanceWeight
        self.assert_code(0x7DC01D, '0f28c6f30f594624f30f584730f30f114730')  # * priority, add UtilityScore

    def test_recovery_hook_phase_is_after_tile_scores_before_native_behavior_choice(self):
        # Attack tile evaluation, transform, criterion.PostProcess, final behavior
        # scoring, native randomized choice. The recovery adds no second AI pass.
        self.assert_call(0x7B1834, 0x7B8610)
        self.assert_call(0x7B1883, 0x7B37D0)
        self.assert_code(0x7B1943, 'ff90b8010000')
        self.assert_call(0x7B1A58, 0x7B8680)
        self.assert_call(0x7B1B02, 0x7B30B0)

    def test_ap_cost_is_uncomputed_at_recovery_and_owned_by_later_native_move(self):
        # TileScore.Reset: APCost=0, MinimumUtilityAPCost=1024.
        self.assert_code(0x7D7F61, '897b40c7434400040000')
        # Move.OnEvaluate treats APCost=0 as not processed, not unreachable.
        self.assert_code(0x7D198A, '41837d40000f85e2050000')
        self.assert_call(0x7D1CDC, 0x6AF3A0)  # FindPathForwardOrBackward
        self.assert_call(0x7D1E02, 0x6963A0)  # ClipPathToCost
        self.assert_code(0x7D1E30, '41897d40')  # APCost = clipped cost


if __name__ == '__main__':
    unittest.main()
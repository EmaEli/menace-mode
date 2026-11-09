"""Exact 0.7.18 phase/layout checks; skips when private game files are unavailable."""
import hashlib
from pathlib import Path
import struct
import unittest


class OffensiveNativeContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = Path(__file__).resolve().parents[3] / "game_files/v0.7.18/GameAssembly.dll"
        if not path.is_file():
            raise unittest.SkipTest("private reviewed 0.7.18 GameAssembly unavailable")
        cls.data = path.read_bytes()
        if hashlib.sha256(cls.data).hexdigest() != "494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9":
            raise AssertionError("unreviewed native binary")
        pe = struct.unpack_from("<I", cls.data, 0x3C)[0]
        count = struct.unpack_from("<H", cls.data, pe + 6)[0]
        optional = struct.unpack_from("<H", cls.data, pe + 20)[0]
        cls.sections = [struct.unpack_from("<IIII", cls.data, pe + 24 + optional + 40 * i + 8) for i in range(count)]

    def code(self, rva, length):
        for virtual_size, base, raw_size, raw in self.sections:
            if base <= rva and rva + length <= base + min(virtual_size, raw_size):
                return self.data[raw + rva - base:raw + rva - base + length]
        self.fail("RVA outside file-backed section")

    def call_target(self, rva):
        code = self.code(rva, 5)
        self.assertEqual(code[0], 0xE8)
        return rva + 5 + struct.unpack_from("<i", code, 1)[0]

    def test_get_tiles_aliases_working_dictionary_before_normalization(self):
        self.assertEqual(self.code(0x7BB8DF, 8), bytes.fromhex("48 8b 57 58 48 89 57 60"))
        self.assertEqual(self.code(0x5C2EE0, 5), bytes.fromhex("48 8b 41 60 c3"))
        self.assertEqual(self.code(0x7BE18E, 4), bytes.fromhex("49 8b 55 58"))

    def test_normalization_follows_initial_native_behavior_evaluation(self):
        self.assertEqual(self.call_target(0x7BC114), 0x7C1350)
        self.assertEqual(self.call_target(0x7BC163), 0x7BE090)

    def test_disabled_positioning_exits_native_attack_collect(self):
        self.assertEqual(self.code(0x7D4BF2, 4), bytes.fromhex("44 38 78 14"))
        self.assertEqual(self.code(0x7D4BF6, 2), bytes.fromhex("0f 84"))
        displacement = struct.unpack("<i", self.code(0x7D4BF8, 4))[0]
        self.assertEqual(0x7D4BFC + displacement, 0x7D6465)

    def test_exact_native_minimum_attack_ap_sentinel(self):
        self.assertEqual(self.code(0x7E2954, 7), bytes.fromhex("c7 43 44 00 04 00 00"))

    def test_contact_alert_setter_changes_only_the_sleep_flag(self):
        self.assertEqual(self.code(0x4D5ED0, 4), bytes.fromhex("88 51 51 c3"))

    def test_native_setup_rechecks_skills_costs_and_resources(self):
        self.assertEqual(self.call_target(0x7CB4FB), 0x769E90)  # deployment IsUsable
        self.assertEqual(self.call_target(0x7CB517), 0x763690)  # deployment AP
        self.assertEqual(self.call_target(0x7CB5D7), 0x7697F0)  # setup uses
        self.assertEqual(self.call_target(0x7CB5F3), 0x768C90)  # executing elements
        self.assertEqual(self.call_target(0x7CB717), 0x763690)  # setup AP + shot AP

    def test_native_move_owns_postmove_deployment_and_setup_queue(self):
        self.assertEqual(self.code(0x7DF20F, 7), bytes.fromhex("49 8b 8f 80 00 00 00"))
        self.assertEqual(self.code(0x7DF226, 4), bytes.fromhex("49 8b 57 58"))
        self.assertEqual(self.code(0x7DF2BD, 7), bytes.fromhex("49 8b 8f 80 00 00 00"))
        self.assertEqual(self.code(0x7DF2D0, 4), bytes.fromhex("49 8b 57 68"))
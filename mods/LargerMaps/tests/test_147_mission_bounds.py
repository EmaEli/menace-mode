"""Offline checks for v1.4.7's upstream MissionTemplate.GetBounds correction.

The actual IL2CPP execution requires Windows and MENACE. These checks
establish the exact native imm32 sites and guard against regressions where
post-processing receives an already-clipped zero-area rectangle.
"""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = (ROOT / 'src/LargerMaps/ExtendedPathfinding.cs').read_text()
PLUGIN = (ROOT / 'src/LargerMaps/Plugin.cs').read_text()
MANIFEST = json.loads((ROOT / 'tests/pathfinding-sites.json').read_text())
PATCHES = MANIFEST['sites']
BUILD_0715 = next(b for b in MANIFEST['builds'] if b['game_version'] == 'v0.7.15+21883')


def absolute_rva(site, build=BUILD_0715):
    return int(build['method_rvas'][site['method']], 16) + int(site['delta'], 16)


def clamp_reference_rect(rect, limit):
    """Illustrate how the old 42-wide mission bounds destroy distant rects."""
    x, y, w, h = rect
    end_x = max(x, min(limit, x + w))
    end_y = max(y, min(limit, y + h))
    return min(x, limit), min(y, limit), max(0, end_x-min(x, limit)), max(0, end_y-min(y, limit))


def patched_immediate(site, size):
    raw = bytearray(bytes.fromhex(site['expected_instruction_hex']))
    index = site['immediate_offset']
    raw[index] = size
    return bytes(raw)


class MissionBounds147Tests(unittest.TestCase):
    def test_game_hardcoded_42_bounds_identified_and_patched(self):
        sites = {s['purpose']:s for s in PATCHES if s['purpose'].startswith('mission-bound-')}
        self.assertEqual(set(sites), {'mission-bound-width','mission-bound-height'})
        self.assertEqual(absolute_rva(sites['mission-bound-width']),0x5F61C2)
        self.assertEqual(absolute_rva(sites['mission-bound-height']),0x5F61CF)
        for name, site in sites.items():
            original = bytes.fromhex(site['expected_instruction_hex'])
            self.assertEqual(site['immediate_offset'],3)
            self.assertEqual(original[3:7], (42).to_bytes(4,'little'))
            self.assertEqual(patched_immediate(site,80)[3:7], (80).to_bytes(4,'little'))
            self.assertEqual(patched_immediate(site,96)[3:7], (96).to_bytes(4,'little'))
            self.assertIn('mission-bound-', SRC)
            self.assertIn('StartsWith("mission-bound-"', SRC)
        self.assertEqual(len(PATCHES),26)

    def test_selected_map_sizes_fit_original_imm32_encoding_and_bounds(self):
        for size in (72,80,88,96):
            for site in PATCHES[:2]:
                native = patched_immediate(site,size)
                self.assertEqual(int.from_bytes(native[3:7],'little'),size)
                self.assertEqual(native[:3],bytes.fromhex(site['expected_instruction_hex'])[:3])

    def test_real_96x96_defend_collapses_only_under_original_42_limit(self):
        # Hypothetical 9x11 region at boundary 42 illustrates the observed 0x0 clip.
        # The actual source size before native clamping cannot be recovered from the log.
        original = (42,42,9,11)
        self.assertEqual(clamp_reference_rect(original,42),(42,42,0,0))
        self.assertEqual(clamp_reference_rect(original,96),original)
        self.assertEqual(clamp_reference_rect((28,27,9,11),42),(28,27,9,11))

    def test_native_64_baseline_still_unpatched(self):
        self.assertIn('if (requested > 64)',PLUGIN)
        self.assertIn('native 64x64 pathfinding selected (no native byte patches)',PLUGIN)

    def test_existing_backend_fail_closed(self):
        for term in ('SHA256.Create()', 'SupportedBuilds.FirstOrDefault(', 'if (build == null)',
                     'foreach (PatchSite site in Sites)', 'Environment.FailFast(',
                     'FlushInstructionCache(', 'VirtualProtect('):
            self.assertIn(term,SRC)

    def test_border_thickness_preserves_preexisting_64_reference(self):
        # At64 the original 1.3.12 border-expansion thickness was 5/6 for native 3/4.
        def bankers_round(x): return round(x)
        for source,at64 in ((3,5),(4,6)):
            self.assertEqual(bankers_round(source*64/42),at64)
            for size in (72,80,88,96):
                expected=bankers_round(at64*size/64)
                self.assertGreaterEqual(expected,at64)
                self.assertLessEqual(expected,size)
        self.assertIn('ScaleBorderThickness(spanW, mapWidth)', PLUGIN)
        self.assertIn('ScaleBorderThickness(spanH, mapHeight)', PLUGIN)

if __name__ == '__main__': unittest.main()

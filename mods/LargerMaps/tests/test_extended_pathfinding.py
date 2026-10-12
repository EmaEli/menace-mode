"""Offline safety tests for Larger Maps 1.4's version-locked native patch plan.

Set MENACE_GAMEASSEMBLY to the original v0.7.15+21883 GameAssembly.dll to
validate its real instruction bytes; otherwise tests use the embedded pattern
fixtures to validate the patcher's source manifest and arithmetic only.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parent.parent
DATA = json.loads((ROOT / 'tests' / 'pathfinding-sites.json').read_text())
SOURCE = (ROOT / 'src' / 'LargerMaps' / 'ExtendedPathfinding.cs').read_text()
PLUGIN = (ROOT / 'src' / 'LargerMaps' / 'Plugin.cs').read_text()
BINARY_PATH = os.getenv('MENACE_GAMEASSEMBLY', '')


class ExtendedPathfindingTests(unittest.TestCase):
    def test_sites_match_source_descriptors(self):
        pattern = r'new\(0x([0-9A-F]+), (\d+), "([0-9A-F]+)", 0x([0-9A-F]+), 0x([0-9A-F]+), "([a-zA-Z0-9_-]+)"\)'
        parsed = re.findall(pattern, SOURCE)
        self.assertEqual(len(parsed), len(DATA['sites']))
        self.assertEqual(len(parsed), 26)
        for raw, recorded in zip(parsed, DATA['sites']):
            rva, offset, original, at80, at96, purpose = raw
            self.assertEqual((int(rva,16),int(offset),original,int(at80,16),int(at96,16),purpose),
                             (int(recorded['rva'],16),recorded['immediate_offset'],recorded['expected_instruction_hex'],
                              int(recorded['replacement_80'],16),int(recorded['replacement_96'],16),recorded['purpose']))

    def test_sites_unique_nonoverlapping_and_keep_instruction_lengths(self):
        positions=[]
        spans=[]
        for site in DATA['sites']:
            rva=int(site['rva'],16)
            code=bytes.fromhex(site['expected_instruction_hex'])
            off=site['immediate_offset']
            self.assertLess(off,len(code))
            pos=rva+off
            positions.append(pos)
            spans.append((rva,rva+len(code)))
            self.assertNotEqual(code[off],int(site['replacement_80'],16))
            self.assertNotEqual(code[off],int(site['replacement_96'],16))
            if site['purpose'].startswith('stride-'):
                self.assertEqual(code[:1],bytes([0xC1]))
                self.assertEqual(code[off],6)
                self.assertEqual(int(site['replacement_80'],16),7)
        self.assertEqual(len(set(positions)),len(positions))
        spans.sort()
        self.assertTrue(all(a[1]<=b[0] for a,b in zip(spans,spans[1:])))

    def test_full_closed_bitmap_covered(self):
        names=[site['purpose'] for site in DATA['sites']]
        self.assertEqual(sum(x.startswith('stride-') for x in names),12)
        self.assertEqual(sum(x.startswith('ctor-') for x in names),2)
        self.assertEqual(sum(x.startswith('process-init-') for x in names),2)
        self.assertEqual(sum(x.startswith('manager-init-') for x in names),2)
        self.assertEqual(sum(x.startswith('node-grid-dim-') for x in names),2)
        for key in ('queue-capacity','closed-buffer-allocation','closed-buffer-clear','findpath-buffer-clear'):
            self.assertIn(key,names)

    def test_128_stride_collision_free_and_within_allocated_buffer(self):
        for width in (64,80,96):
            seen={x*128+z for x in range(width) for z in range(width)}
            self.assertEqual(len(seen),width*width)
            self.assertLess(max(seen),128*128)
            self.assertNotEqual(63*128+63,64*128+0)  # both sides of old 63/64 boundary
            if width > 64:
                seen_old={x*64+z for x in range(width) for z in range(width)}
                self.assertLess(len(seen_old),width*width)

    def test_80_96_immediates_remain_valid_x64_instructions(self):
        for size in (80,96):
            for site in DATA['sites']:
                name=site['purpose']
                original=bytes.fromhex(site['expected_instruction_hex'])
                newval=int(site[f'replacement_{size}'],16)
                patched=bytearray(original)
                patched[site['immediate_offset']]=newval
                self.assertEqual(len(original),len(patched))
                if '-loop' in name:
                    self.assertEqual(patched[-1],size)
                    self.assertLess(size,128)  # signed 8-bit instruction immediate
                if name.startswith('mission-bound-'):
                    self.assertEqual(patched[3], size)
                    self.assertEqual(patched[4:7], b'\x00\x00\x00')
                if name.startswith('node-grid-dim'):
                    self.assertEqual(patched[5],128)
                if name.startswith('stride-'):
                    self.assertEqual(patched[-1],7)
                if name in ('closed-buffer-allocation','closed-buffer-clear','findpath-buffer-clear'):
                    self.assertEqual(int.from_bytes(patched[1:], 'little'),16384)
                if name=='queue-capacity':
                    self.assertEqual(int.from_bytes(patched[1:],'little'),16384)

    def test_source_fallback_and_mandatory_patches_preserved(self):
        for expected in ('_activeMapSize = 64','ReadRequestedMapSize()',
                         'ExtendedPathfinding.TryInstall(requested, out string reason)',
                         '_activeMapSize = requested','PatchAiTaskLifetime(harmony)',
                         'PatchTerrainHoleIndexing(harmony)',
                         'if (Debug())','private static bool Debug() => false'):
            self.assertIn(expected,PLUGIN)
        self.assertIn('ExpectedGameAssemblySha256',SOURCE)
        self.assertIn('VirtualProtect(',SOURCE)
        self.assertIn('FlushInstructionCache(',SOURCE)
        self.assertIn('Environment.FailFast',SOURCE)

    @unittest.skipUnless(BINARY_PATH and Path(BINARY_PATH).exists(), 'Set MENACE_GAMEASSEMBLY to original game DLL')
    def test_real_native_binary_original_sites_and_fingerprint(self):
        raw=Path(BINARY_PATH).read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), DATA['expected_sha256'])
        # Current GameAssembly PE layout: native .il2cpp RVA = file offset + 0xC00.
        for site in DATA['sites']:
            off=int(site['rva'],16)-0xC00
            original=bytes.fromhex(site['expected_instruction_hex'])
            self.assertEqual(raw[off:off+len(original)],original,site['purpose'])
        # The selected byte changes have no overlaps and leave everything else alone.
        for size in (80,96):
            patched=bytearray(raw)
            for site in DATA['sites']:
                offset=int(site['rva'],16)-0xC00+site['immediate_offset']
                patched[offset]=int(site[f'replacement_{size}'],16)
            self.assertEqual(sum(a!=b for a,b in zip(raw,patched)),26)


if __name__=='__main__':
    unittest.main(verbosity=2)

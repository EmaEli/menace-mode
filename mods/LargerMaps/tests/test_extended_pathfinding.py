"""Offline safety tests for Larger Maps' version-locked native patch plan.

Set MENACE_GAMEASSEMBLY to an original GameAssembly.dll of any build listed in
tests/pathfinding-sites.json to validate its real instruction bytes; otherwise
tests use the embedded pattern fixtures to validate the patcher's source
manifest and arithmetic only.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import unittest

ROOT = Path(__file__).resolve().parent.parent
DATA = json.loads((ROOT / 'tests' / 'pathfinding-sites.json').read_text())
SOURCE = (ROOT / 'src' / 'LargerMaps' / 'ExtendedPathfinding.cs').read_text()
PLUGIN = (ROOT / 'src' / 'LargerMaps' / 'Plugin.cs').read_text()
BINARY_PATH = os.getenv('MENACE_GAMEASSEMBLY', '')
METHODS = ('MissionTemplateGetBounds', 'PathfindingProcessCtor', 'PathfindingProcessInit',
           'PathfindingManagerInit', 'PathfindingProcessFindPath', 'PathfindingProcessProcessNode',
           'PathfindingProcessClear')


def site_rva(site, build):
    return int(build['method_rvas'][site['method']], 16) + int(site['delta'], 16)


def rva_to_file_offset(raw, rva):
    """Map an RVA to a file offset using the PE section table (the layout differs per build)."""
    pe = struct.unpack_from('<I', raw, 0x3C)[0]
    section_count = struct.unpack_from('<H', raw, pe + 6)[0]
    optional_size = struct.unpack_from('<H', raw, pe + 20)[0]
    table = pe + 24 + optional_size
    for i in range(section_count):
        entry = table + i * 40
        virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from('<IIII', raw, entry + 8)
        if virtual_address <= rva < virtual_address + max(virtual_size, raw_size):
            return rva - virtual_address + raw_pointer
    raise AssertionError(f'RVA 0x{rva:X} is outside every PE section')


class ExtendedPathfindingTests(unittest.TestCase):
    def test_builds_match_source_table(self):
        pattern = r'new\("([^"]+)", "([0-9a-f]{64})",\s*((?:0x[0-9A-F]+,?\s*){7})\)'
        parsed = re.findall(pattern, SOURCE)
        self.assertEqual(len(parsed), len(DATA['builds']))
        self.assertGreaterEqual(len(parsed), 2)
        for (version, sha, rvas), recorded in zip(parsed, DATA['builds']):
            self.assertEqual(version, recorded['game_version'])
            self.assertEqual(sha, recorded['sha256'])
            values = [int(v, 16) for v in re.findall(r'0x[0-9A-F]+', rvas)]
            self.assertEqual(values, [int(recorded['method_rvas'][m], 16) for m in METHODS])
        self.assertEqual(len({b['sha256'] for b in DATA['builds']}), len(DATA['builds']))
        for build in DATA['builds']:
            self.assertEqual(tuple(build['method_rvas']), METHODS)

    def test_sites_match_source_descriptors(self):
        pattern = r'new\(NativeMethod\.(\w+), 0x([0-9A-F]+), (\d+), "([0-9A-F]+)", 0x([0-9A-F]+), 0x([0-9A-F]+), "([a-zA-Z0-9_-]+)"\)'
        parsed = re.findall(pattern, SOURCE)
        self.assertEqual(len(parsed), len(DATA['sites']))
        self.assertEqual(len(parsed), 26)
        for raw, recorded in zip(parsed, DATA['sites']):
            method, delta, offset, original, at80, at96, purpose = raw
            self.assertIn(method, METHODS)
            self.assertEqual((method,int(delta,16),int(offset),original,int(at80,16),int(at96,16),purpose),
                             (recorded['method'],int(recorded['delta'],16),recorded['immediate_offset'],recorded['expected_instruction_hex'],
                              int(recorded['replacement_80'],16),int(recorded['replacement_96'],16),recorded['purpose']))

    def test_v0715_absolute_rvas_unchanged(self):
        # The original 1.4.7 plan was authored as absolute v0.7.15 RVAs; the (method, delta)
        # form must resolve to exactly those addresses.
        build = next(b for b in DATA['builds'] if b['game_version'] == 'v0.7.15+21883')
        by_purpose = {}
        for site in DATA['sites']:
            by_purpose.setdefault(site['purpose'], []).append(site_rva(site, build))
        self.assertEqual(by_purpose['mission-bound-width'], [0x5F61C2])
        self.assertEqual(by_purpose['mission-bound-height'], [0x5F61CF])
        self.assertEqual(by_purpose['queue-capacity'], [0x69EF4E])
        self.assertEqual(by_purpose['closed-buffer-allocation'], [0x69EFE3])
        self.assertEqual(by_purpose['stride-disasm_processnode'], [0x69EEA5])
        self.assertEqual(by_purpose['stride-disasm_findpath'][0], 0x69C9AE)
        self.assertEqual(by_purpose['stride-disasm_findpath'][-1], 0x69DDF6)

    def test_sites_unique_nonoverlapping_and_keep_instruction_lengths(self):
        for build in DATA['builds']:
            positions=[]
            spans=[]
            for site in DATA['sites']:
                rva=site_rva(site, build)
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
        self.assertIn('SupportedBuilds.FirstOrDefault(b => string.Equals(sha, b.Sha256',SOURCE)
        self.assertIn('if (build == null)',SOURCE)
        self.assertIn('VirtualProtect(',SOURCE)
        self.assertIn('FlushInstructionCache(',SOURCE)
        self.assertIn('Environment.FailFast',SOURCE)

    @unittest.skipUnless(BINARY_PATH and Path(BINARY_PATH).exists(), 'Set MENACE_GAMEASSEMBLY to original game DLL')
    def test_real_native_binary_original_sites_and_fingerprint(self):
        raw=Path(BINARY_PATH).read_bytes()
        sha=hashlib.sha256(raw).hexdigest()
        build=next((b for b in DATA['builds'] if b['sha256']==sha), None)
        self.assertIsNotNone(build, f'GameAssembly.dll {sha[:12]} is not a build in the manifest')
        offsets={}
        for site in DATA['sites']:
            off=rva_to_file_offset(raw, site_rva(site, build))
            original=bytes.fromhex(site['expected_instruction_hex'])
            self.assertEqual(raw[off:off+len(original)],original,f"{site['purpose']} ({build['game_version']})")
            offsets[id(site)]=off
        # The selected byte changes have no overlaps and leave everything else alone.
        for size in (80,96):
            patched=bytearray(raw)
            for site in DATA['sites']:
                patched[offsets[id(site)]+site['immediate_offset']]=int(site[f'replacement_{size}'],16)
            self.assertEqual(sum(a!=b for a,b in zip(raw,patched)),26)


if __name__=='__main__':
    unittest.main(verbosity=2)

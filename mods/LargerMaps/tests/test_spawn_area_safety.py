"""Offline contract tests for 1.4.2 spawn areas and configurable sizes.

The geometric Python functions intentionally mirror the C# formulas. These tests
cannot emulate native MENACE actor-occupancy or the Harmony/IL2CPP hook runtime.
"""
import json
import re
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / 'src/LargerMaps/Plugin.cs').read_text()
NATIVE = (ROOT / 'src/LargerMaps/ExtendedPathfinding.cs').read_text()
CONFIG = json.loads((ROOT / 'modpack/LargerMaps/modpack.json').read_text())
SIZES=(64,72,80,88,96)

def normalize(x,y,w,h,n,min_size=5):
    if w >= min_size and h >= min_size and x >= 0 and y >= 0 and x + w <= n and y + h <= n:
        return x,y,w,h
    left=min(x,x+w)
    top=min(y,y+h)
    oldw=max(1,abs(w))
    oldh=max(1,abs(h))
    neww=min(n,max(min_size,oldw))
    newh=min(n,max(min_size,oldh))
    cx=left+(oldw-1)//2
    cy=top+(oldh-1)//2
    nx=max(0,min(cx-(neww-1)//2,n-neww))
    ny=max(0,min(cy-(newh-1)//2,n-newh))
    return nx,ny,neww,newh

def scale_area(x,y,w,h,n,min_size=5):
    # C# Math.Round uses banker's rounding, as does Python round.
    left=min(x,x+w)
    top=min(y,y+h)
    oldw=max(1,abs(w))
    oldh=max(1,abs(h))
    factor=n/42
    nx=round(left*factor)
    ny=round(top*factor)
    sw=max(1,round(oldw*factor))
    sh=max(1,round(oldh*factor))
    nw=max(min_size,sw)
    nh=max(min_size,sh)
    if nw>sw: nx-= (nw-sw)//2
    if nh>sh: ny-= (nh-sh)//2
    nw=min(n,nw)
    nh=min(n,nh)
    nx=max(0,min(nx,n-nw))
    ny=max(0,min(ny,n-nh))
    return nx,ny,nw,nh

class SpawnAreaSafetyTests(unittest.TestCase):
    def test_logged_failed_spawn_rectangles_are_usable_for_each_size(self):
        problem_cases={
            'civilian.worker':(42,41,0,1),
            'allied_local_forces.police_chief':(42,42,-1,-1),
            'enemy.pirate_outcasts':(32,42,10,0),
        }
        for n in SIZES:
            for name,rect in problem_cases.items():
                with self.subTest(n=n,actor=name):
                    x,y,w,h=normalize(*rect,n)
                    self.assertGreaterEqual(w,5)
                    self.assertGreaterEqual(h,5)
                    self.assertTrue(0<=x<=n-w and 0<=y<=n-h)
                    self.assertEqual(normalize(x,y,w,h,n),(x,y,w,h))
    def test_original_good_spawn_areas_remain_unchanged(self):
        for n in SIZES:
            for rect in [(10,10,10,12),(0,0,6,8),(20,30,10,20)]:
                self.assertEqual(normalize(*rect,n),rect)
    def test_edge_and_outside_map_areas_are_clamped(self):
        for n in SIZES:
            for rect in [(0,0,0,0),(n-1,n-1,0,0),(n-1,n-1,10,10),
                         (-10,-5,2,3),(n+5,n+8,-2,-8),(-10000,10000,2,1)]:
                with self.subTest(n=n,rect=rect):
                    x,y,w,h=normalize(*rect,n)
                    self.assertTrue(0<=x and 0<=y and w>=5 and h>=5)
                    self.assertTrue(x+w<=n and y+h<=n)
    def test_authored_rects_scaled_for_each_supported_size(self):
        for n in SIZES:
            for rect in [(5,8,8,6),(42,42,-1,-1),(32,42,10,0),(8,42,0,5),
                         (0,0,42,42),(21,8,10,25),(32,32,0,0)]:
                with self.subTest(n=n,rect=rect):
                    x,y,w,h=scale_area(*rect,n)
                    self.assertTrue(0<=x and 0<=y and w>=5 and h>=5)
                    self.assertTrue(x+w<=n and y+h<=n)
    def test_size_selector_and_exact_native_immediates(self):
        self.assertIn('"Tactical map size: 64 / 72 / 80 / 88 / 96',PLUGIN)
        self.assertIn('Math.Clamp(snapped, 64, 96)',PLUGIN)
        self.assertIn('mapSize < 72 || mapSize > 96 || (mapSize % 8) != 0',NATIVE)
        self.assertIn('Purpose.EndsWith("-loop", StringComparison.Ordinal)',NATIVE)
        self.assertIn('SupportedBuilds',NATIVE)
        for size in SIZES[1:]:
            self.assertLess(size,128)
            self.assertEqual(size%8,0)
            self.assertEqual(len({x*128+z for x in range(size) for z in range(size)}),size*size)
    def test_native_instruction_immediates_for_72_and_88(self):
        sites=json.loads((ROOT/'tests/pathfinding-sites.json').read_text())['sites']
        self.assertEqual(len(sites),26)
        for size in (72,88):
            for site in sites:
                original=bytearray.fromhex(site['expected_instruction_hex'])
                purpose=site['purpose']
                target=size if (purpose.endswith('-loop') or purpose.startswith('mission-bound-')) else int(site['replacement_80'],16)
                self.assertLess(target,128 if (purpose.endswith('-loop') or purpose.startswith('mission-bound-')) else 256)
                original[site['immediate_offset']]=target
                if purpose.endswith('-loop'):
                    self.assertEqual(original[-1],size)
                if purpose.startswith('stride-'):
                    self.assertEqual(original[-1],7)
                if purpose.startswith('node-grid-dim'):
                    self.assertEqual(original[5],128)
        self.assertIn('site.ForMap(mapSize)',NATIVE)
    def test_actual_mission_template_areas_across_sizes(self):
        # Optional file from the user's game; no game assets are distributed.
        import os
        extracted=os.getenv('MENACE_EXTRACTED')
        if not extracted:
            self.skipTest('Set MENACE_EXTRACTED for source mission template coverage')
        with zipfile.ZipFile(extracted) as z:
            missions=json.loads(z.read('ExtractedData/GenericMissionTemplate.json'))
        tested=0
        for mission in missions:
            area_groups=[mission.get('m_OppositionSpawnAreas') or [],
                         mission.get('m_MenacePresenceSpawnAreas') or []]
            area_groups += [a.get('SpawnAreas') or [] for a in (mission.get('Actors') or [])]
            for group in area_groups:
                for area in group:
                    if area.get('AreaType') not in (2,3,4,5,6,7,8,9,10):
                        continue
                    r=area['Rect']
                    rect=(r['m_XMin'],r['m_YMin'],r['m_Width'],r['m_Height'])
                    for size in SIZES:
                        x,y,w,h=scale_area(*rect,size)
                        self.assertTrue(x>=0 and y>=0 and w>0 and h>0 and
                                        x+w<=size and y+h<=size,
                                        (mission['name'],area['AreaType'],rect,size))
                        tested+=1
        self.assertGreater(tested,1000)
    def test_all_spawn_entry_points_are_guarded(self):
        self.assertIn('Mission.TryCreateInitialAiUnit/9',PLUGIN)
        self.assertIn('nameof(MissionInitialSpawnPrefix)',PLUGIN)
        self.assertIn('nameof(TacticalSpawnAreaPrefix)',PLUGIN)
        self.assertNotIn('"CreateInitialAiUnits", 3, nameof(EnemySpawnScopePrefix)',PLUGIN)
        self.assertIn('"OnCreateInitialAiUnits", 4, nameof(ObjectiveTargetSpawnScopePrefix)',PLUGIN)
        self.assertIn('args[2].ParameterType.Name.IndexOf("RectInt"',PLUGIN)
        self.assertIn('Math.Min((long)x, (long)x + w)',PLUGIN)
        self.assertIn('MinimumAiSpawnAreaSize = 5',PLUGIN)
        self.assertIn('PatchAiTaskLifetime(harmony)',PLUGIN)
        self.assertIn('PatchTerrainHoleIndexing(harmony)',PLUGIN)
    def test_archive_version(self):
        self.assertEqual(CONFIG['version'],'1.9')
        self.assertIn('64/72/80/88/96',CONFIG['description'])

if __name__ == '__main__':
    unittest.main(verbosity=2)

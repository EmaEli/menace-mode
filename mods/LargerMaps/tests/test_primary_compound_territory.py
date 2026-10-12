"""1.4.7 regression: no artificial whole-compound territory or deployment zone."""
import json
import re
import unittest
import zipfile
from pathlib import Path
from test_objective_zone_scaling import placed_area, away_round

ROOT=Path(__file__).resolve().parents[1]
SOURCE=(ROOT/'src/LargerMaps/Plugin.cs').read_text()
SIZES=(64,72,80,88,96)

class PrimaryCompoundReferenceTests(unittest.TestCase):
    def test_game_outpost_template_has_distinct_deployment_inset(self):
        p=Path('/mnt/data/ExtractedData(1).zip')
        if not p.exists():self.skipTest('Extracted game data not supplied')
        with zipfile.ZipFile(p) as z:
            missions=json.loads(z.read('ExtractedData/GenericMissionTemplate.json'))
        mission=next(x for x in missions if x['name']=='mission.pirates_repel_counterattack_cop_large_military_base_01')
        self.assertEqual(mission['m_DeploymentZones'][0]['AreaType'],0)
        self.assertEqual(mission['m_DeploymentZones'][0]['BaseExpand'],-3)
        self.assertEqual(mission['m_DeploymentZones'][0]['ClusterExpand'],3)
        templates=[k['Templates'][0]['Setpiece'] for k in mission['PrimarySetpieces']]
        dims=set()
        for template in templates:
            # The data extractor supplies setpiece references, not the full
            # setpiece blueprints. The suffix encodes intended dimensions.
            match=re.search(r'_(\d+)x(\d+)$',template)
            self.assertIsNotNone(match,template)
            dims.add(tuple(map(int,match.groups())))
        self.assertEqual(dims,{(20,22),(21,23)})

    def test_scales_original_native_zone_not_setpiece_size(self):
        # Arbitrary example: native deployment is smaller than the outpost.
        native=(40,37,12,13) # RectInt half-open
        for n in SIZES:
            resized=placed_area(*native,n,False,primary=(36,34,20,22))
            self.assertEqual(abs(resized[2]),away_round(12*n/64))
            self.assertEqual(abs(resized[3]),away_round(13*n/64))
            self.assertLess(abs(resized[2]),round(20*n/64))
            self.assertLess(abs(resized[3]),round(22*n/64))

    def test_64_never_changes_native_deployment_or_defend(self):
        for sample in ((40,35,12,13),(44,37,3,3),(15,15,-4,9)):
            self.assertEqual(placed_area(*sample,64,False,primary=(36,34,20,22)),sample)
            self.assertEqual(placed_area(*sample,64,True,primary=(36,34,20,22)),sample)

    def test_primary_is_only_relative_anchor_not_minimum(self):
        self.assertIn('desiredCx = anchorX + (desiredCx - anchorX) * sx;',SOURCE)
        self.assertIn('TryFindNearestPrimaryCompound(preview',SOURCE)
        self.assertNotIn('Math.Max(Math.Max(spanX, minimumSpan)',SOURCE)
        self.assertNotIn('target={desiredW}x{desiredH}, reference=64',SOURCE)

    def test_sized_zones_do_not_merge_multiple_compounds(self):
        base_a=(5,6,20,22);base_b=(65,60,20,22)
        xa=placed_area(9,10,8,11,96,False,base_a)
        xb=placed_area(70,65,8,11,96,False,base_b)
        self.assertLess(xa[0]+abs(xa[2]),xb[0])

    def test_builtin_roles_and_deployments_share_reference_math(self):
        self.assertIn('changed = TryScaleResolvedAnchoredRect(rect, preview, mapW, mapH,\n                        true, primaryOffset, oldReferenceMinimum',SOURCE)
        self.assertIn('changed = TryScaleResolvedAnchoredRect(rect, preview, mapW, mapH,\n                        false, primaryOffset, oldReferenceMinimum',SOURCE)
        self.assertIn('objectiveArea = role == ObjectiveAreaRole.ControlOrExtraction',SOURCE)
        self.assertIn('((objectiveArea && role == ObjectiveAreaRole.ControlOrExtraction) || deploymentScope)',SOURCE)

    def test_1_4_5_manifest_is_consistent(self):
        data=json.loads((ROOT/'modpack/LargerMaps/modpack.json').read_text())
        self.assertEqual(data['version'],'1.6.1')
        self.assertIn('1.6.1', (ROOT/'src/LargerMaps/LargerMaps.csproj').read_text())

if __name__=='__main__':unittest.main()

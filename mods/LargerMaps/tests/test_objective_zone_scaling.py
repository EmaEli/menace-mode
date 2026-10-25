"""Larger Maps 1.6: reference-based area geometry and source contract.

Pure simulations of the C# math; this does NOT execute IL2CPP/Harmony/MENACE.
"""
import json
import re
import unittest
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT/'src/LargerMaps/Plugin.cs').read_text()
SIZES = (64, 72, 80, 88, 96)


def away_round(v):
    return math.floor(v+0.5) if v>=0 else math.ceil(v-0.5)


def placed_area(x, y, w, h, n, inclusive=True, primary=None):
    if n == 64: return (x,y,w,h)
    extra = 1 if inclusive else 0
    if w==0 or h==0: return (x,y,w,h)
    sw = min(n,abs(w)+extra)
    sh = min(n,abs(h)+extra)
    sx = n/64
    targetw = max(1,min(n,away_round(sw*sx)))
    targeth = max(1,min(n,away_round(sh*sx)))
    cx,cy = x+w/2, y+h/2
    if primary:
        bx,by,bw,bh=primary
        acx=bx+(bw-1)/2
        acy=by+(bh-1)/2
        cx=acx+(cx-acx)*sx
        cy=acy+(cy-acy)*sx
    lowx=max(0,min(round(cx-(targetw-extra)/2),n-targetw))
    lowy=max(0,min(round(cy-(targeth-extra)/2),n-targeth))
    nx=lowx+targetw-extra if w<0 else lowx
    ny=lowy+targeth-extra if h<0 else lowy
    nw=extra-targetw if w<0 else targetw-extra
    nh=extra-targeth if h<0 else targeth-extra
    return (nx,ny,nw,nh)


def authored(x,y,dx,dy,n):
    if n==64:return (x,y,dx,dy)
    sx=n/64
    ax=round(x*sx);ay=round(y*sx)
    bx=round((x+dx)*sx);by=round((y+dy)*sx)
    nx=max(0,min(ax,n-1));ny=max(0,min(ay,n-1))
    ex=max(0,min(bx,n-1));ey=max(0,min(by,n-1))
    nw=ex-nx;nh=ey-ny
    if dx>0 and nw==0 and nx<n-1:nw=1
    if dx<0 and nw==0 and nx>0:nw=-1
    if dy>0 and nh==0 and ny<n-1:nh=1
    if dy<0 and nh==0 and ny>0:nh=-1
    return (nx,ny,nw,nh)


def native_border(area_type, x,y,w,h,n, inclusive=False):
    if n==64:return(x,y,w,h)
    if w==0 or h==0:return(x,y,w,h)
    extra=int(inclusive)
    sx,sy=min(n,abs(w)+extra),min(n,abs(h)+extra)
    tx=round(round(sx*64/42)*n/64) if area_type in (3,4,7,8,9,10) else sx
    ty=round(round(sy*64/42)*n/64) if area_type in (2,5,7,8,9,10) else sy
    tx=max(1,min(n,tx));ty=max(1,min(n,ty))
    lx=min(x,x+w);ly=min(y,y+h)
    if area_type in (3,7,8):lx=max(0,lx+sx-tx)
    if area_type in (5,7,10):ly=max(0,ly+sy-ty)
    lx=max(0,min(lx,n-tx));ly=max(0,min(ly,n-ty))
    return (lx+tx-extra if w<0 else lx,
            ly+ty-extra if h<0 else ly,
            extra-tx if w<0 else tx-extra,
            extra-ty if h<0 else ty-extra)


def bounds(rect,inclusive=True):
    x,y,w,h=rect
    return min(x,x+w),min(y,y+h),max(x,x+w),max(y,y+h)


class ReferenceScalingTests(unittest.TestCase):
    def test_all_area_types_use_their_correct_branch(self):
        self.assertIn('if (areaType == 0 || areaType == 1)',SOURCE)
        self.assertIn('TryScaleResolvedAnchoredRect',SOURCE)
        self.assertIn('else if (areaType == 6)',SOURCE)
        self.assertIn('TryScaleReferenceAuthoredRect',SOURCE)
        self.assertIn('TryScaleReferenceBorderRect',SOURCE)
        self.assertIn('mapW > ZoneReferenceSize && mapH > ZoneReferenceSize',SOURCE)
        self.assertNotIn('PatchAllObjectiveAreaScopes(harmony);',SOURCE)
        self.assertIn('_objectiveType.Assembly.GetTypes()',SOURCE)

    def test_64x64_is_untouched_in_every_transform(self):
        for rect in ((24,15,8,12),(55,40,-9,-7),(0,0,2,3),(37,50,1,1)):
            self.assertEqual(placed_area(*rect,64),rect)
            self.assertEqual(placed_area(*rect,64,False),rect)
            self.assertEqual(authored(*rect,64),rect)
            for kind in (2,3,4,5,7,8,9,10):
                self.assertEqual(native_border(kind,*rect,64),rect)
        self.assertIn('if (!enlarged && !inSpawnOrDeployment)',SOURCE)
        self.assertIn('unmodified 64-reference objective area',SOURCE)
        self.assertNotIn('Math.Max(Math.Max(spanX, minimumSpan),',SOURCE)
        self.assertNotIn('baseW * (mapWidth / (double)PrimaryCompoundReferenceSize)',SOURCE)

    def test_anchored_zone_scales_footprint_not_entire_compound(self):
        # Reference native area is only 9x11, compound itself is 20x22.
        src=(40,38,8,10) # inclusive 9x11
        for n in SIZES:
            out=placed_area(*src,n,True)
            self.assertEqual(abs(out[2])+1,away_round(9*n/64))
            self.assertEqual(abs(out[3])+1,away_round(11*n/64))
            self.assertLess(abs(out[2])+1,away_round(20*n/64))
            self.assertLess(abs(out[3])+1,away_round(22*n/64))

    def test_deployment_half_open_scales_differently(self):
        src=(30,27,14,16)
        for n in SIZES:
            out=placed_area(*src,n,False)
            self.assertEqual(abs(out[2]),away_round(14*n/64))
            self.assertEqual(abs(out[3]),away_round(16*n/64))

    def test_primary_offsets_stay_relative_to_base(self):
        src=(34,31,9,11)
        base=(32,30,20,22)
        bx,by,bw,bh=base
        acx=bx+(bw-1)/2;acy=by+(bh-1)/2
        src_cx=src[0]+src[2]/2;src_cy=src[1]+src[3]/2
        for n in SIZES:
            out=placed_area(*src,n,True,primary=base)
            ncx=out[0]+out[2]/2;ncy=out[1]+out[3]/2
            self.assertLessEqual(abs((ncx-acx)-(src_cx-acx)*n/64),0.6)
            self.assertLessEqual(abs((ncy-acy)-(src_cy-acy)*n/64),0.6)

    def test_negative_width_and_height_direction_preserved(self):
        for n in SIZES:
            for src in ((42,39,-8,-9),(10,37,5,-8),(33,20,-10,8)):
                for inclusive in (True,False):
                    out=placed_area(*src,n,inclusive)
                    self.assertEqual(out[2]<0,src[2]<0)
                    self.assertEqual(out[3]<0,src[3]<0)
                    l,t,r,b=bounds(out)
                    self.assertTrue(0<=l<=r<=n and 0<=t<=b<=n,(n,out))

    def test_authored_scales_both_center_and_footprint_from_64(self):
        for n in SIZES:
            a=authored(10,12,8,10,n)
            self.assertEqual(a[0],round(10*n/64))
            self.assertEqual(a[1],round(12*n/64))
            self.assertEqual(a[2],round(18*n/64)-a[0])
            self.assertEqual(a[3],round(22*n/64)-a[1])

    def test_border_percent_length_not_rescaled_twice(self):
        for n in SIZES:
            native_north=(0,n-10,n,10)
            out=native_border(5,*native_north,n)
            self.assertEqual(out[2],n)
            self.assertEqual(out[3], 10 if n == 64 else round(round(10*64/42)*n/64))
            native_west=(0,4,3,round(n*.8))
            out=native_border(4,*native_west,n)
            self.assertEqual(out[2], 3 if n == 64 else round(round(3*64/42)*n/64))
            self.assertEqual(out[3],round(n*.8))

    def test_all_eight_border_orientation_variants(self):
        for n in SIZES[1:]:
            for typ in (2,3,4,5,7,8,9,10):
                # inputs emulate rectangles already laid out on the selected map
                inp=(0,n-4,n,4) if typ in (2,5) else (n-4,0,4,n)
                out=native_border(typ,*inp,n)
                l,t,r,b=bounds(out)
                self.assertTrue(0<=l<=r<=n and 0<=t<=b<=n,(n,typ,out))

    def test_trigger_has_no_compound_minimum(self):
        for n in SIZES[1:]:
            r=placed_area(30,30,2,2,n,True)
            self.assertEqual(abs(r[2])+1,away_round(3*n/64))
            self.assertIn('role == ObjectiveAreaRole.Trigger', SOURCE)

    def test_no_double_scaling_objective_ai_rects(self):
        self.assertIn('if (role == ObjectiveAreaRole.AI && areaType == 6) continue;',SOURCE)
        self.assertIn('_scaledObjectiveAiZoneConfigs.Add(key)',SOURCE)
        self.assertIn('ObjectiveInitAiZonesPrefix',SOURCE)

    def test_legacy_spawn_and_native_ai_safety_preserved(self):
        self.assertIn('mapWidth / (float)VanillaSize',SOURCE)
        self.assertIn('PatchAiTaskLifetime(harmony)',SOURCE)
        self.assertIn('PatchTerrainHoleIndexing(harmony)',SOURCE)
        self.assertIn('private static bool Debug() => false;',SOURCE)
        self.assertIn('Math.Clamp(snapped, 64, 96)',SOURCE)

    def test_version_and_dump_class_coverage(self):
        self.assertNotIn('private const string Version', SOURCE)
        m=json.loads((ROOT/'../jiangyu/LargerMaps/jiangyu.json').read_text())
        self.assertEqual(m['version'],'2.3.2')
        dump=Path('/mnt/data/dump.cs')
        if dump.exists():
            text=dump.read_text()
            for name in ('PrimaryChunks','Chunk','SouthMapBorder','EastMapBorder',
                         'WestMapBorder','NorthMapBorder','Rect','NorthEastMapBorder',
                         'SouthEastMapBorder','SouthWestMapBorder','NorthWestMapBorder'):
                self.assertRegex(text,r'MissionAreaType '+name+r' = [0-9]+;')
            for klass,field in [('DefendArea','m_Areas'),('CaptureArea','m_CaptureAreaRects'),
                                ('EvacuateToArea','m_Areas'),('RecoverObject','m_PlayerDropOffAreaRects'),
                                ('RescueUnit','m_EvacuationAreaRects'),('SabotageArea','m_TargetAreaRects')]:
                ix=text.find('public class '+klass+' :')
                self.assertGreater(ix,0,klass)
                self.assertIn(field,text[ix:ix+2500])

if __name__=='__main__':unittest.main()

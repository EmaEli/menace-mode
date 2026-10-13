"""MEN-001: deterministic smoke-direction geometry regression examples.

This mirrors the six-candidate local filter in SmokeOverhaul 0.6.3.
It does not run MENACE or replace the Windows compilation/in-game test.
"""
from pathlib import Path
import unittest

SRC = (Path(__file__).resolve().parents[1] / "src/SmokeOverhaul/Plugin.cs").read_text()

def candidates(dx, dz, flamer=False):
    sx, sz = (dx > 0) - (dx < 0), (dz > 0) - (dz < 0)
    if sx == sz == 0:
        return []
    origin_distance = dx * dx + dz * dz
    result = []
    for reach in ((2, 1) if flamer else (1, 2)):
        for candidate in range(3):
            if candidate == 0:
                stepx, stepz = sx, sz
            elif sx and sz:
                stepx, stepz = (sx, 0) if candidate == 1 else (0, sz)
            elif sx:
                stepx, stepz = sx, (1 if candidate == 1 else -1)
            else:
                stepx, stepz = (1 if candidate == 1 else -1), sz
            x, z = stepx * reach, stepz * reach
            if (dx - x)**2 + (dz - z)**2 >= origin_distance:
                continue
            result.append((x, z))
    return result

class SmokeDirectionTests(unittest.TestCase):
    def test_source_uses_detected_known_opponents_not_facing(self):
        self.assertIn("opponent.IsKnown()", SRC)
        self.assertIn("enemy.IsDetectedByFaction(faction.GetIndex())", SRC)
        self.assertIn("private const int MaxSmokeThreatChecks = 24;", SRC)
        self.assertIn("actor.GetLastAttacker()?.ToActor()", SRC)
        self.assertIn("_aiSmokeDirectionDebugLines < 24", SRC)
        self.assertIn("AI smoke thrown: origin=", SRC)
        self.assertIn("attackerDist2 <= nearDist2 * 4", SRC)
        self.assertIn("for (int reachIndex = 0; reachIndex < 2; reachIndex++)", SRC)
        self.assertIn("for (int candidate = 0; candidate < 3; candidate++)", SRC)
        block = SRC.split("internal static bool TryAiSmokeAction(",1)[1].split("private static void ApplyNativeSmokeAiToMortars()",1)[0]
        self.assertNotIn("GetDirection()", block)
        self.assertNotIn("FindPath(", block)
        self.assertNotIn("GetVisibleOpponents(", block)

    def test_all_quadrants_never_throw_to_rear(self):
        bearings = [(7,0), (-7,0), (0,7), (0,-7), (9,1), (1,9),
                    (9,9), (-9,9), (9,-9), (-9,-9), (-2,0), (0,-2), (1,1), (-1,-1)]
        for flamer in (False, True):
            for dx,dz in bearings:
                with self.subTest(flamer=flamer, dx=dx, dz=dz):
                    tiles = candidates(dx,dz,flamer)
                    self.assertTrue(tiles)
                    self.assertLessEqual(len(tiles),6)
                    for x,z in tiles:
                        self.assertGreater(x*dx+z*dz,0)
                        self.assertLess((dx-x)**2+(dz-z)**2, dx**2+dz**2)
                        self.assertLessEqual(max(abs(x),abs(z)),2)
                    self.assertEqual(tiles[0][0] * dx + tiles[0][1] * dz > 0, True)

    def test_no_position_when_threat_coincident(self):
        self.assertEqual(candidates(0,0), [])
        self.assertEqual(candidates(0,0,True), [])

    def test_candidate_order_defense_near_flame_far(self):
        self.assertEqual(candidates(7,0)[0],(1,0))
        self.assertEqual(candidates(7,0,True)[0],(2,0))
        self.assertEqual(candidates(-7,0)[0],(-1,0))
        self.assertEqual(candidates(-7,0,True)[0],(-2,0))

if __name__=="__main__":
    unittest.main()

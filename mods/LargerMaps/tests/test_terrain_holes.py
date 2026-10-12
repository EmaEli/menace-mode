"""Pure-Python reference tests for the shape/sampling of the 1.3.12 fix.

These do not compile or execute C# or IL2CPP: in-game testing is still required.
"""
import math
import unittest


def clamp(n, lo, hi):
    return max(lo, min(n, hi))


def hole_region(cx, cz, radius, resolution=512, world_width=511.0):
    last = resolution - 1
    scale = last / world_width
    # Mathf.RoundToInt rounds midpoints to even, as does Python round().
    sx = clamp(round((cx - radius) * scale), 0, last)
    sz = clamp(round((cz - radius) * scale), 0, last)
    ex = clamp(round((cx + radius) * scale), 0, last)
    ez = clamp(round((cz + radius) * scale), 0, last)
    return sx, sz, ex - sx, ez - sz, scale


def apply_fix(cx, cz, radius, resolution=512, world_width=511.0):
    sx, sz, width, height, scale = hole_region(cx, cz, radius, resolution, world_width)
    holes = [[1] * width for _ in range(height)]  # Unity GetHoles(): [height,width]
    for x in range(width):
        dx2 = ((sx + x) / scale - cx) ** 2
        for z in range(height):
            if dx2 + ((sz + z) / scale - cz) ** 2 < radius ** 2:
                holes[z][x] = 0  # replacement: [z,x]
    return holes, (sx, sz, width, height)


class TerrainHoleTests(unittest.TestCase):
    def test_rectangular_x_border(self):
        holes, (_, _, w, h) = apply_fix(2, 100, 7)
        self.assertNotEqual(w, h)
        self.assertLess(w, h)
        self.assertEqual(len(holes), h)
        self.assertTrue(all(len(row) == w for row in holes))
        self.assertTrue(any(v == 0 for row in holes for v in row))
        self.assertTrue(any(v == 1 for row in holes for v in row))

    def test_rectangular_z_border(self):
        holes, (_, _, w, h) = apply_fix(100, 2, 7)
        self.assertNotEqual(w, h)
        self.assertGreater(w, h)
        self.assertEqual(len(holes), h)
        self.assertTrue(all(len(row) == w for row in holes))
        self.assertTrue(any(v == 0 for row in holes for v in row))

    def test_vanilla_wrong_order_could_fail(self):
        holes, (_, _, w, h) = apply_fix(100, 2, 7)
        self.assertGreater(w, h)
        with self.assertRaises(IndexError):
            _ = holes[w - 1][0]  # game's loop uses X for row index

    def test_centered_hole_remains_circular(self):
        holes, (_, _, w, h) = apply_fix(100, 100, 7)
        self.assertEqual(w, h)
        self.assertEqual(holes[0][0], 1)  # outside radius preserved
        self.assertEqual(holes[h // 2][w // 2], 0)

    def test_clipped_area_stays_in_terrain_range(self):
        for cx, cz in ((0, 0), (511, 511), (1, 300), (500, 2), (60, 470)):
            _, (sx, sz, w, h) = apply_fix(cx, cz, 20)
            self.assertGreaterEqual(sx, 0)
            self.assertGreaterEqual(sz, 0)
            self.assertGreaterEqual(w, 0)
            self.assertGreaterEqual(h, 0)
            self.assertLessEqual(sx + w, 511)
            self.assertLessEqual(sz + h, 511)


if __name__ == '__main__':
    unittest.main(verbosity=2)

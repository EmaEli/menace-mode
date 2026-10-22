"""Decode the supplied 07-09 PNGs without optional imaging dependencies."""
import pathlib
import struct
import unittest
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
SIZES = {
    'slot.png': (162, 162), 'slot_inactive.png': (162, 162), 'bark.png': (460, 480),
    'stand_left.png': (1024, 1714), 'stand_right.png': (1024, 1714),
    'stand_right_inactive.png': (1024, 1714),
}


def rgba_png(path):
    data = path.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('Not PNG: ' + str(path))
    offset, packed, dimensions = 8, bytearray(), None
    while offset < len(data):
        length = struct.unpack('>I', data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        content = data[offset + 8:offset + 8 + length]
        crc = struct.unpack('>I', data[offset + 8 + length:offset + 12 + length])[0]
        if zlib.crc32(kind + content) & 0xffffffff != crc:
            raise ValueError('Invalid PNG CRC: ' + str(path))
        if kind == b'IHDR':
            width, height, depth, color, compression, filtering, interlace = struct.unpack('>IIBBBBB', content)
            if (depth, color, compression, filtering, interlace) != (8, 6, 0, 0, 0):
                raise ValueError('Expected noninterlaced RGBA8: ' + str(path))
            dimensions = width, height
        elif kind == b'IDAT':
            packed.extend(content)
        elif kind == b'IEND':
            break
        offset += length + 12
    width, height = dimensions
    stride = width * 4
    raw = zlib.decompress(packed)
    if len(raw) != (stride + 1) * height:
        raise ValueError('Unexpected scanline length: ' + str(path))
    rows, previous = [], bytearray(stride)
    for y in range(height):
        start = y * (stride + 1)
        filter_type = raw[start]
        row = bytearray(raw[start + 1:start + 1 + stride])
        if filter_type not in range(5):
            raise ValueError('Unknown PNG filter')
        if filter_type:
            for x in range(stride):
                left = row[x - 4] if x >= 4 else 0
                up, upper_left = previous[x], previous[x - 4] if x >= 4 else 0
                if filter_type == 1:
                    predictor = left
                elif filter_type == 2:
                    predictor = up
                elif filter_type == 3:
                    predictor = (left + up) // 2
                else:
                    p = left + up - upper_left
                    a, b, c = abs(p - left), abs(p - up), abs(p - upper_left)
                    predictor = left if a <= b and a <= c else up if b <= c else upper_left
                row[x] = (row[x] + predictor) & 255
        rows.append(bytes(row))
        previous = row
    return dimensions, b''.join(rows)


class ReplacementPortraitTests(unittest.TestCase):
    def test_transparency_mirroring_and_inactive_alpha(self):
        for gender in ('male', 'female'):
            for index in ('07', '08', '09'):
                profile = f'promoted_{gender}_{index}'
                decoded = {}
                for filename, size in SIZES.items():
                    with self.subTest(profile=profile, file=filename):
                        path = ROOT / 'runtime_assets' / profile / filename
                        dimensions, pixels = rgba_png(path)
                        self.assertEqual(dimensions, size)
                        self.assertEqual((min(pixels[3::4]), max(pixels[3::4])), (0, 255))
                        self.assertEqual(path.read_bytes(), (ROOT / 'portrait_candidates' / profile / filename).read_bytes())
                        decoded[filename] = pixels
                for active, inactive in (('slot.png', 'slot_inactive.png'),
                                         ('stand_right.png', 'stand_right_inactive.png')):
                    with self.subTest(profile=profile, inactive=inactive):
                        self.assertEqual(decoded[active][3::4], decoded[inactive][3::4])
                        self.assertEqual(decoded[inactive][0::4], decoded[inactive][1::4])
                        self.assertEqual(decoded[inactive][1::4], decoded[inactive][2::4])
                left, right = decoded['stand_left.png'], decoded['stand_right.png']
                stride = SIZES['stand_left.png'][0] * 4
                with self.subTest(profile=profile, mirror=True):
                    for start in range(0, len(left), stride):
                        row = left[start:start + stride]
                        mirrored = b''.join(row[x:x + 4] for x in range(stride - 4, -1, -4))
                        self.assertEqual(mirrored, right[start:start + stride])


if __name__ == '__main__':
    unittest.main(verbosity=2)
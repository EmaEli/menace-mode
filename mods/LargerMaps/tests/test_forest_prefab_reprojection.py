"""Execute production forest-prefab placement with managed Unity/native stand-ins."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
MODULE = ROOT / 'mods/LargerMaps'


class ForestPrefabReprojectionTests(unittest.TestCase):
    def test_production_hook_behavior(self):
        dotnet = shutil.which('dotnet') or str(Path.home() / '.dotnet/dotnet')
        if not Path(dotnet).is_file():
            self.skipTest('Existing .NET SDK not available')
        result = subprocess.run([
            dotnet, 'run', '--project', str(MODULE / 'tests/ForestPrefabChecks'),
            '-c', 'Release', '--no-launch-profile', '--tl:off',
        ], cwd=ROOT, capture_output=True, text=True, timeout=120)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('Forest prefab production-hook checks passed: 102', result.stdout)

    def test_functional_hook_is_installed_outside_debug(self):
        source = (MODULE / 'src/LargerMaps/Plugin.cs').read_text()
        # The existing gameplay and GPU-spawner hooks delimit unconditional startup wiring.
        self.assertIn('PatchGameplayPointReprojection(harmony);\n'
                      '            PatchForestPrefabReprojection(harmony);\n'
                      '            PatchTerrainPointSpawnerHeightFix(harmony);', source)
        self.assertLess(source.index('PatchForestPrefabReprojection(harmony);'),
                        source.index('if (Debug())', source.index('PatchGameplayPointReprojection(harmony);')))


if __name__ == '__main__':
    unittest.main()
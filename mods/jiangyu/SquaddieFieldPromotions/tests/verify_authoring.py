import json
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = json.loads((ROOT / 'jiangyu.json').read_text(encoding='utf-8'))
CS = (ROOT / 'code' / 'PromotionLabSystem.cs').read_text(encoding='utf-8')
BRIDGE = (ROOT / 'code' / 'JiangyuCloneBridge.cs').read_text(encoding='utf-8')
KDL = (ROOT / 'templates' / 'control.kdl').read_text(encoding='utf-8')


class AuthoringTests(unittest.TestCase):
    def test_jiangyu_not_old_modpack(self):
        self.assertEqual(MANIFEST['name'], 'SquaddieFieldPromotions')
        self.assertEqual(MANIFEST['version'], '0.1')
        self.assertTrue(any(dep.startswith('Jiangyu ') for dep in MANIFEST['depends']))
        self.assertIn('JiangyuSystem', CS)
        self.assertNotIn('IModpackPlugin', CS)
        self.assertFalse((ROOT / 'modpack.json').exists())

    def test_readonly_control_clone(self):
        self.assertIn('clone "UnitLeaderTemplate"', KDL)
        self.assertIn('mod.squaddiefieldpromotions.control', KDL)
        self.assertNotIn('clone "SpeakerTemplate"', KDL)

    def test_no_automatic_runtime_mutations(self):
        body = CS.split('public override void OnInit()', 1)[1].split('public override void OnUnload()', 1)[0]
        self.assertIn('Hotkeys.OnKeyDown(KeyCode.F8, Inspect)', body)
        self.assertIn('Hotkeys.OnKeyDown(KeyCode.F9, GenerateTrial)', body)
        self.assertNotIn('AddHirableLeader(', body)
        self.assertIn('ENABLE_UNSAFE_LAB_TEST.txt', CS)
        self.assertIn('state.Roster.AddHirableLeader(leader)', CS)
        self.assertNotIn('RemoveRandomAlive', CS)
        self.assertNotIn('RemoveSquaddie', CS)

    def test_live_cloning_uses_jiangyu_internal_engine(self):
        for key in ['TryCloneTemplate', 'DeepCopyCollectionContainers',
                    'TryGetTemplateMap', 'RegisterCloneIntoSlot',
                    'MirrorCloneToAncestors']:
            self.assertIn(key, BRIDGE)
        self.assertIn('DataTemplateLoader.Get<UnitLeaderTemplate>(leaderId, false)', CS)
        self.assertIn('Il2CppStructArray<byte>(attrs)', CS)

    def test_optional_xp_and_fallback(self):
        self.assertIn('SquaddieProbe.CompatibilityApi', CS)
        self.assertIn('GetSquaddieXp', CS)
        self.assertIn('65, 65, 62, 50, 50, 5, 45', CS)
        self.assertIn('if (xp < 0) return attrs;', CS)

    def test_safety_warning_in_readme(self):
        readme = (ROOT / 'README.md').read_text(encoding='utf-8')
        self.assertIn('QUIT WITHOUT SAVING', readme)
        self.assertIn('not compiled or tested', readme.lower())


if __name__ == '__main__':
    unittest.main(verbosity=2)

import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = json.loads((ROOT / 'jiangyu.json').read_text(encoding='utf-8'))
CS = (ROOT / 'code' / 'PromotionSystem.cs').read_text(encoding='utf-8')
KDL = (ROOT / 'templates' / 'field-promotion-dossier.kdl').read_text(encoding='utf-8')


class AuthoringTests(unittest.TestCase):
    def test_jiangyu_not_old_modpack(self):
        self.assertEqual(MANIFEST['name'], 'SquaddieFieldPromotions')
        self.assertEqual(MANIFEST['version'], '0.8.4')
        self.assertTrue(any(dep.startswith('Jiangyu ') for dep in MANIFEST['depends']))
        self.assertIn('JiangyuSystem', CS)
        self.assertNotIn('IModpackPlugin', CS)
        self.assertFalse((ROOT / 'modpack.json').exists())

    def test_only_settings_is_required_beyond_jiangyu(self):
        self.assertEqual(MANIFEST['depends'], ['Jiangyu >= 1.4.8', 'MenaceModSettings >= 0.6.1'])
        project = (ROOT / 'code/SquaddieFieldPromotions.csproj').read_text()
        self.assertIn('<Private>false</Private>', project)
        self.assertIn('<Version>0.8.4</Version>', project)
        self.assertIn('$(MenaceModSettingsPath)', project)

    def test_no_recruitment_hotkeys_or_save_hooks(self):
        for name in ('PromotionSystem.cs', 'NativeCandidates.cs', 'PromotionRules.cs'):
            code = (ROOT / 'code' / name).read_text()
            for forbidden in ('Hotkeys.', 'KeyCode.', 'OnUpdate(', 'SaveSystem', 'AddHirableLeader(',
                              'HireLeader(', 'RemoveRandomAlive(', 'SetKilled(', 'JiangyuCloneBridge('):
                self.assertNotIn(forbidden, code)
        self.assertIn('"DebugLogging", "Diagnostic logging (OFF by default)", false', CS)
        self.assertIn('"EnableDossierPreview", "Offer 1-credit promotion-review dossier in Black Market", false', CS)

    def test_native_template_isolated_from_vanilla_unlocks(self):
        self.assertIn('clone "DossierItemTemplate" from="dossier.squad_leader"', KDL)
        self.assertIn('clear "m_UnlockedLeaders"', KDL)
        self.assertIn('set "BlackMarketMaxQuantity" 0', KDL)
        # BlackMarketUIScreen.FilterItems hides merchant items with GetTradeValue() < 1.
        self.assertIn('set "TradeValue" 1', KDL)
        self.assertIn('append "m_TradeValues" 1', KDL)
        self.assertNotIn('append "m_TradeValues" 0', KDL)
        self.assertIn('internal const int PreviewPrice = 1;', CS)
        self.assertNotIn('clone "UnitLeaderTemplate"', KDL)
        review = CS.split('private void Review(PatchInfo info)', 1)[1].split('private void Stock()', 1)[0]
        self.assertLess(review.index('info.Skip = true'), review.index('StrategyState.Get()'))
        self.assertIn('ui.AddSimpleDialog()', review)
        self.assertIn('enableRichText = false', review)

    def test_optional_xp_and_fallback(self):
        native = (ROOT / 'code/NativeCandidates.cs').read_text()
        rules = (ROOT / 'code/PromotionRules.cs').read_text()
        self.assertIn('SquaddieProbe.CompatibilityApi', native)
        self.assertIn('GetSquaddieXp', native)
        self.assertIn('GetMissionsParticipated()', native)
        self.assertIn('GetAllAlive()', native)
        self.assertIn('IReadOnlyCollection', native)
        self.assertIn('65, 65, 62, 50, 50, 5, 45', rules)

    def test_localized_fields_are_not_raw_template_strings(self):
        for field in ('Title', 'ShortName', 'Description'):
            self.assertNotIn(f'set "{field}"', KDL)
        for field in ('Title', 'ShortName'):
            self.assertIn(f'template.{field} = PreviewText("{field}",', CS)
        self.assertIn('template.Description = PreviewDescription(', CS)
        self.assertIn('new LocalizedLine(LocaCategory.Items, field, false)', CS)
        self.assertIn('new LocalizedMultiLine(LocaCategory.Items, "Description", false)', CS)
        self.assertIn('line.SetId(DossierId)', CS)
        self.assertIn('public override void OnTemplatesApplied()', CS)
        payload = ROOT / 'compiled/templates.json'
        if payload.exists():
            for patch in json.loads(payload.read_text())['templatePatches']:
                for operation in patch['set']:
                    self.assertNotIn(operation['fieldPath'], ('Title', 'ShortName', 'Description'))

    def test_safety_warning_in_readme(self):
        readme = (ROOT / 'README.md').read_text(encoding='utf-8')
        self.assertIn('not yet performed in MENACE', readme)
        self.assertIn('No leader is created', readme)
        self.assertIn('PREVIEW ONLY', readme)
        self.assertIn('before native roster load', readme)


if __name__ == '__main__':
    unittest.main(verbosity=2)

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
        self.assertEqual(MANIFEST['version'], '0.9.1')
        self.assertTrue(any(dep.startswith('Jiangyu ') for dep in MANIFEST['depends']))
        self.assertIn('JiangyuSystem', CS)
        self.assertNotIn('IModpackPlugin', CS)
        self.assertFalse((ROOT / 'modpack.json').exists())

    def test_only_settings_is_required_beyond_jiangyu(self):
        self.assertEqual(MANIFEST['depends'], ['Jiangyu >= 1.4.8', 'MenaceModSettings >= 0.6.1'])
        project = (ROOT / 'code/SquaddieFieldPromotions.csproj').read_text()
        self.assertIn('<Private>false</Private>', project)
        self.assertIn('<Version>0.9.1</Version>', project)
        self.assertIn('$(MenaceModSettingsPath)', project)

    def test_no_hotkeys_polling_or_source_soldier_removal(self):
        for path in sorted((ROOT / 'code').glob('*.cs')):
            code = path.read_text()
            for forbidden in ('Hotkeys.', 'KeyCode.', 'OnUpdate(', 'HireLeader(', 'RemoveRandomAlive(', 'SetKilled(',
                              'Squaddies.Remove', 'GetAllAlive().Remove'):
                self.assertNotIn(forbidden, code, f'{path.name} contains {forbidden}')
        self.assertIn('"DebugLogging", "Diagnostic logging (OFF by default)", false', CS)
        self.assertIn('AddToggle(EnableKey, "Offer 1-credit Field Promotion Dossier in Black Market", false', CS)
        self.assertIn('internal const string EnableKey = "EnableFieldPromotions";', CS)

    def test_leaders_rebuilt_before_native_roster_load(self):
        # Prefix on SaveSystem.ExecLoad: the sidecar is read and templates re-registered before deserialization.
        self.assertIn('Context.Patches.Prefix("Il2CppMenace.Strategy.SaveSystem", "ExecLoad", 1, Rehydrate)', CS)
        self.assertIn('RequireStatic(typeof(SaveSystem), "ExecLoad", new[] { typeof(SaveState) }, typeof(void))', CS)
        self.assertIn('ModStateSidecar', CS)
        self.assertIn('Context.State.Get<PromotionLedger>()', CS)
        self.assertIn('Context.State.Save()', CS)
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        self.assertIn('Roster.AddHirableLeader(leader)', forge)
        self.assertIn('new JiangyuCloneBridge()', forge)
        for name in ('NativeCandidates.cs', 'PromotionRules.cs', 'PromotionRecord.cs'):
            self.assertNotIn('AddHirableLeader', (ROOT / 'code' / name).read_text())
        record = (ROOT / 'code/PromotionRecord.cs').read_text()
        self.assertIn('public sealed class PromotionLedger', record)
        self.assertIn('public List<PromotionRecord> Records { get; set; }', record)
        self.assertIn('public IReadOnlyList<PromotionRecord> Validated()', record)

    def test_runtime_assets_bundled_outside_compiled(self):
        assets = ROOT / 'runtime_assets'
        for gender in ('male', 'female'):
            self.assertTrue((assets / gender / 'stand_right.png').is_file())
        self.assertTrue((assets / 'voice' / 'sfp_john_jane_voice.sfpbundle').is_file())
        self.assertTrue((assets / 'voice' / 'sfp_john_jane_voice.build.json').is_file())
        self.assertTrue((assets / 'voice' / 'voice_routes.json').is_file())
        kit = (ROOT.parents[1] / 'GameplaySuite/tools/build_kit.py').read_text()
        installer = (ROOT.parents[1] / 'GameplaySuite/tools/Install-IntegratedSuite.ps1').read_text()
        self.assertIn('tree(folder / "runtime_assets")', kit)
        self.assertIn("Join-Path $root 'runtime_assets'", installer)

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
        promote = CS.split('private void Promote(PatchInfo info)', 1)[1].split('private void Rehydrate(PatchInfo info)', 1)[0]
        self.assertLess(promote.index('info.Skip = true'), promote.index('StrategyState.Get()'))
        self.assertIn('ui.AddSimpleDialog()', promote)
        self.assertIn('enableRichText = false', promote)
        self.assertLess(promote.index('_forge.Install(state, record)'), promote.index('ledger.Records.Add(record)'))

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
        self.assertIn('source soldier is not removed', readme)
        self.assertIn('before native roster load', readme)
        self.assertIn('runtime_assets', readme)


if __name__ == '__main__':
    unittest.main(verbosity=2)

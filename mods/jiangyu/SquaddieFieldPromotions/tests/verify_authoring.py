import json
import pathlib
import re
import struct
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = json.loads((ROOT / 'jiangyu.json').read_text(encoding='utf-8'))
CS = (ROOT / 'code' / 'PromotionSystem.cs').read_text(encoding='utf-8')
KDL = (ROOT / 'templates' / 'field-promotion-dossier.kdl').read_text(encoding='utf-8')
PROGRESSION_KDL = (ROOT / 'templates' / 'generic-infantry-progression.kdl').read_text(encoding='utf-8')


class AuthoringTests(unittest.TestCase):
    def test_jiangyu_not_old_modpack(self):
        self.assertEqual(MANIFEST['name'], 'SquaddieFieldPromotions')
        self.assertEqual(MANIFEST['version'], '0.17.3')
        compiled_manifest = json.loads((ROOT / 'compiled/jiangyu.json').read_text(encoding='utf-8'))
        self.assertEqual(compiled_manifest['version'], MANIFEST['version'])
        self.assertTrue(any(dep.startswith('Jiangyu ') for dep in MANIFEST['depends']))
        self.assertIn('JiangyuSystem', CS)
        self.assertNotIn('IModpackPlugin', CS)
        self.assertFalse((ROOT / 'modpack.json').exists())

    def test_only_settings_is_required_beyond_jiangyu(self):
        self.assertEqual(MANIFEST['depends'], ['Jiangyu >= 1.4.8', 'MenaceModSettings >= 0.6.1'])
        project = (ROOT / 'code/SquaddieFieldPromotions.csproj').read_text()
        self.assertIn('<Private>false</Private>', project)
        self.assertIn(f"<Version>{MANIFEST['version']}</Version>", project)
        self.assertIn('$(MenaceModSettingsPath)', project)

    def test_no_hotkeys_polling_random_removal_or_fake_death(self):
        for path in sorted((ROOT / 'code').glob('*.cs')):
            code = path.read_text()
            for forbidden in ('Hotkeys.', 'KeyCode.', 'OnUpdate(', 'HireLeader(', 'RemoveRandomAlive(', 'SetKilled(',
                              'GetAllAlive().Remove'):
                self.assertNotIn(forbidden, code, f'{path.name} contains {forbidden}')
        self.assertIn('"DebugLogging", "Diagnostic logging (OFF by default)", false', CS)
        self.assertIn('AddToggle(EnableKey, "Offer Field Promotion Dossier in Black Market", false', CS)
        self.assertIn('internal const string EnableKey = "EnableFieldPromotions";', CS)

    def test_test_save_cleanup_is_one_shot_and_preflighted(self):
        cleanup = (ROOT / 'code/PromotionRosterCleanup.cs').read_text(encoding='utf-8')
        self.assertIn('AddHeader("!!! DANGER: BACK UP test.save BEFORE ENABLING !!!")', CS)
        self.assertIn('REMOVES ALL SFP LEADERS FROM EVERY ROSTER STATE AND DEPLOYMENT, THEN SAVES test.save.', CS)
        self.assertIn('AddToggle(CleanupTestSaveKey, "I HAVE A BACKUP - REMOVE ALL SFP LEADERS FROM test.save (ONE-SHOT)", false)', CS)
        self.assertIn('Path.GetFileName(_loadedSavePath), "test.save"', CS)
        self.assertIn('SaveSystem.Save(SaveStateType.Manual, _loadedSavePath', CS)
        self.assertIn('ModSettings.Set(Group, CleanupTestSaveKey, false)', CS)
        self.assertIn('string.IsNullOrEmpty(_loadedSavePath)', CS)
        self.assertIn('PromotionRosterCleanup.BuildPlan(leaderIds, ReadRosterEntries(roster))', CS)
        self.assertIn('PromotionRosterLocation.Hired', cleanup)
        self.assertIn('PromotionRosterLocation.Unburied', cleanup)
        self.assertIn('PromotionRosterLocation.Buried', cleanup)
        self.assertIn('RemoveTemplates(roster.m_HirableLeaders', CS)
        for state in ('m_HiredLeaders', 'm_DismissedLeaders', 'm_UnburiedLeaders', 'm_BuriedLeaders'):
            self.assertIn(f'RemoveLeaders(roster.{state}', CS)
        self.assertNotIn('A promoted leader is hired, awaiting burial, or buried', cleanup)
        self.assertIn('cleanup request disarmed before preflight', CS)

    def test_cleanup_waits_for_native_model_and_cleans_deployment(self):
        self.assertNotIn('Postfix("Il2CppMenace.Strategy.SaveSystem", "ExecLoad"', CS)
        self.assertIn('Postfix("Il2CppMenace.Strategy.Roster", "ProcessSaveState", 1, NativeRosterLoaded)', CS)
        self.assertIn('Postfix("Il2CppMenace.Strategy.SaveState", "Close", 0, NativeSaveClosed)', CS)
        self.assertIn('save.Pointer != _rosterLoadState.Pointer', CS)
        self.assertIn('!save.IsLoading()', CS)
        self.assertIn('!_nativeLoadCompleted', CS)
        self.assertIn('!IsLoadedRoster(StrategyState.Get()?.Roster)', CS)
        self.assertIn('RemoveDeployments(deployment, targets, removedDeployment)', CS)
        self.assertIn('RestoreEntries(deployment, removedDeployment)', CS)

    def test_leaders_rebuilt_before_native_roster_load(self):
        # Prefix on SaveSystem.ExecLoad: the sidecar is read and templates re-registered before deserialization.
        self.assertIn('Context.Patches.Prefix("Il2CppMenace.Strategy.SaveSystem", "ExecLoad", 1, Rehydrate)', CS)
        self.assertIn('Context.Patches.Postfix("Il2CppMenace.Strategy.SaveState", "Close", 0, NativeSaveClosed)', CS)
        self.assertIn('RequireStatic(typeof(SaveSystem), "ExecLoad", new[] { typeof(SaveState) }, typeof(void))', CS)
        self.assertIn('ModStateSidecar', CS)
        self.assertIn('Context.State.Get<PromotionLedger>()', CS)
        self.assertIn('Context.State.Save()', CS)
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        template_text = (ROOT / 'code/PromotionTemplateText.cs').read_text()
        self.assertIn('leader.SpeakerTemplate.Description = NewMultiLine', template_text)
        self.assertIn('leader.UnitDescription = NewMultiLine', template_text)
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

    def test_portrait_profiles_are_save_scoped_and_match_native_units(self):
        record = (ROOT / 'code/PromotionRecord.cs').read_text(encoding='utf-8')
        system = (ROOT / 'code/PromotionSystem.cs').read_text(encoding='utf-8')
        forge = (ROOT / 'code/LeaderForge.cs').read_text(encoding='utf-8')
        assets = (ROOT / 'code/StandalonePortraitAssets.cs').read_text(encoding='utf-8')
        self.assertIn('public string PortraitProfile { get; set; } = "";', record)
        self.assertIn('TryAssignRotatingProfile(ledger, record)', system)
        self.assertNotIn('duplicate portrait profile', record)
        self.assertIn('internal const int BrownSkinColor = 1;', record)
        self.assertIn('internal const int BlackSkinColor = 2;', record)
        self.assertIn('internal const int FemaleGender = 1;', record)
        self.assertIn('skinColor == LegacyWhiteSkinColor', record)
        self.assertIn('ArePortraitProfilesPresent(_modFolder)', forge)
        self.assertIn('portrait.ApplyTo(leader, speaker)', forge)
        self.assertIn('DynamicBadgeFactory.Apply(leader, art', forge)
        self.assertIn('PromotionPortraitAssignment.AllProfiles.All(', assets)
        pools = {
            ('MaleGender', 'BrownSkinColor'): ('MaleBrown01', 'MaleBrown04', 'MaleBrown06', 'MaleBrown07', 'MaleBrown10'),
            ('MaleGender', 'BlackSkinColor'): ('MaleBlack02', 'MaleBlack05', 'MaleBlack08', 'MaleBlack12'),
            ('MaleGender', 'WhiteSkinColor'): ('MaleWhite03', 'MaleWhite09', 'MaleWhite11'),
            ('FemaleGender', 'BrownSkinColor'): ('Female01', 'FemaleBrown04', 'FemaleBrown06', 'FemaleBrown07', 'FemaleBrown10'),
            ('FemaleGender', 'BlackSkinColor'): ('FemaleBlack02', 'FemaleBlack05', 'FemaleBlack08', 'FemaleBlack12'),
            ('FemaleGender', 'WhiteSkinColor'): ('FemaleWhite03', 'FemaleWhite09', 'FemaleWhite11'),
        }
        for (gender, skin), profiles in pools.items():
            for profile in profiles:
                self.assertIn(f'internal const string {profile} =', record)
                self.assertIn(f'({profile}, {gender}, {skin})', record)
        files = {
            'slot.png': (162, 162), 'slot_inactive.png': (162, 162), 'bark.png': (460, 480),
            'stand_left.png': (1024, 1714), 'stand_right.png': (1024, 1714),
            'stand_right_inactive.png': (1024, 1714),
        }
        for profile in [f'promoted_{gender}_{index:02d}' for gender in ('male', 'female') for index in range(1, 13)]:
            portrait_dir = ROOT / 'runtime_assets' / profile
            candidate_dir = ROOT / 'portrait_candidates' / profile
            for name, expected_size in files.items():
                data = (portrait_dir / name).read_bytes()
                candidate = (candidate_dir / name).read_bytes()
                self.assertGreaterEqual(len(data), 29, f'{profile}/{name}')
                self.assertEqual(data[:8], b'\x89PNG\r\n\x1a\n', f'{profile}/{name}')
                self.assertEqual(data[12:16], b'IHDR', f'{profile}/{name}')
                width, height, bit_depth, color_type, _, _, _ = struct.unpack('>IIBBBBB', data[16:29])
                self.assertEqual((width, height), expected_size, f'{profile}/{name}')
                self.assertEqual((bit_depth, color_type), (8, 6), f'{profile}/{name} must be 8-bit RGBA')
                if (profile, name) != ('promoted_male_01', 'slot.png'):
                    self.assertEqual(data, candidate, f'{profile}/{name} must match its candidate')

    def test_native_infantry_display_getters_are_overridden_only_for_sfp(self):
        for method, callback in (('GetUnitTitle', 'LeaderTitle'), ('GetUnitDescription', 'LeaderDescription')):
            self.assertIn(f'Context.Patches.Postfix("Il2CppMenace.Strategy.UnitLeaderTemplate", "{method}", 0, {callback})', CS)
        self.assertIn('StartsWith(PromotionRecord.LeaderPrefix, StringComparison.Ordinal)', CS)
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        self.assertIn('PromotionTemplateText.VerifyDisplay(record, leader)', forge)
        self.assertNotIn('InfantryUnitTemplate.UnitTitle =', forge)
        self.assertNotIn('InfantryUnitTemplate.UnitDescription =', forge)

    def test_native_template_isolated_from_vanilla_unlocks(self):
        self.assertIn('clone "DossierItemTemplate" from="dossier.squad_leader"', KDL)
        self.assertIn('clear "m_UnlockedLeaders"', KDL)
        self.assertIn('set "BlackMarketMaxQuantity" 0', KDL)
        # BlackMarketUIScreen.FilterItems hides merchant items with GetTradeValue() < 1.
        self.assertIn('set "TradeValue" 100', KDL)
        self.assertIn('append "m_TradeValues" 100', KDL)
        self.assertNotIn('append "m_TradeValues" 0', KDL)
        self.assertIn('internal const int DefaultPrice = 100;', CS)
        self.assertIn('internal const int PriceStep = 10;', CS)
        self.assertIn('settings.AddNumber(PriceKey, "Promotion dossier price (credits)", PriceStep, MaximumPrice, DefaultPrice, PriceStep)', CS)
        self.assertNotIn('clone "UnitLeaderTemplate"', KDL)
        promote = CS.split('private void Promote(PatchInfo info)', 1)[1].split('private void Rehydrate(PatchInfo info)', 1)[0]
        self.assertLess(promote.index('info.Skip = true'), promote.index('StrategyState.Get()'))
        self.assertIn('ui.AddSimpleDialog()', promote)
        self.assertIn('enableRichText = false', promote)
        self.assertLess(promote.index('_forge.Install(state, record, ForceGenericHeads)'), promote.index('ledger.Records.Add(record)'))
        self.assertLess(promote.index('_forge.Install(state, record, ForceGenericHeads)'), promote.index('source.Remove()'))
        self.assertLess(promote.index('source.Remove()'), promote.index('ledger.Records.Add(record)'))
        self.assertIn('source.Restore()', promote)
        self.assertIn('_forge.RollbackInstall(state, record)', promote)

    def test_generic_infantry_progression_and_perk_state_isolation(self):
        expected = [
            ('perk.scout', '1'), ('perk.athletic', '1'), ('perk.new_tricks', '1'),
            ('perk.hit_the_deck', '1'), ('perk.buff', '1'),
            ('perk.solid_grouping', '2'), ('perk.bags_belts', '2'),
            ('perk.mobile_infantry', '2'), ('perk.share_the_load', '2'), ('perk.firing_line', '2'),
            ('perk.steady_gun', '3'), ('perk.tankbuster', '3'), ('perk.fortify', '3'),
            ('perk.zig_zag', '3'), ('perk.full_send', '4'), ('perk.command_rally', '4'),
        ]
        entries = re.findall(
            r'append "Perks" \{\s+'
            r'set "Skill" ref="PerkTemplate" "([^"]+)"\s+set "Tier" ([1-4])\s+\}',
            PROGRESSION_KDL)
        self.assertEqual(entries, expected)
        self.assertIn('set "InitialPerk" ref="PerkTemplate" "perk.unique_sy_economics"', PROGRESSION_KDL)
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        self.assertIn('if (record.PerkSchemaVersion == 0) ApplyGenericProgression(leader);', forge)
        self.assertIn('else DynamicPerkTrees.VerifyOrApply(record, leader, _pinned);', forge)
        self.assertIn('ApplyProgression(record, existing)', forge)
        self.assertIn('ApplyProgression(record, leader)', forge)
        progression = forge.split('private static void ApplyGenericProgression', 1)[1].split(
            'internal bool RefreshIdentity', 1)[0]
        self.assertNotIn('BaseUnitLeader', progression)
        self.assertNotIn('m_Perks', progression)

    def test_dynamic_perk_trees_are_per_leader_saved_snapshots(self):
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        trees = (ROOT / 'code/DynamicPerkTrees.cs').read_text()
        generation = (ROOT / 'code/PerkGeneration.cs').read_text()
        mentors = (ROOT / 'code/MentorPerks.cs').read_text()
        self.assertIn('PerkGeneration.Generate(record, selection.UsesXp ? soldier.Xp : null', forge)
        self.assertIn('ScriptableObject.CreateInstance<PerkTreeTemplate>()', trees)
        self.assertIn('pinned.Add(tree)', trees)
        self.assertIn('HideFlags.DontUnloadUnusedAsset', trees)
        self.assertIn('string initial = PromotionProgression.InitialPerkId;', generation)
        self.assertNotIn('System.Random', generation)
        for forbidden in ('AddPerk(', 'TryRemoveLastPerk(', 'ResetAllPromotions(', 'm_Perks'):
            self.assertNotIn(forbidden, trees + generation + mentors)
        self.assertIn('leader.GetPerks()', mentors)
        for excluded in ('perk.command_rally', 'perk.power_in_numbers', 'perk.reinforcements', 'perk.first_aid'):
            self.assertNotIn(f'["{excluded}"]', generation)

    def test_provenance_remains_save_compatible_but_badges_use_only_bundled_black_art(self):
        record = (ROOT / 'code/PromotionRecord.cs').read_text()
        candidates = (ROOT / 'code/NativeCandidates.cs').read_text()
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        badges = (ROOT / 'code/DynamicBadgeFactory.cs').read_text()
        self.assertIn('public string SourceLeaderTemplateId { get; set; } = "";', record)
        self.assertIn('currentLeader?.LeaderTemplate ?? lastLeader', candidates)
        self.assertIn('RootSourceTemplateId(selection.Soldier.SourceLeaderTemplateId, ledger)',
                      (ROOT / 'code/PromotionSystem.cs').read_text())
        self.assertIn('DynamicBadgeFactory.Apply(leader, art, badgeLabel, _pinned)', forge)
        self.assertNotIn('GetSourceLeader(record)', forge)
        for field in ('Badge', 'BadgeMini', 'BadgeDragged', 'BadgeUnitWindow', 'SlotBadge', 'BigBadge'):
            self.assertIn(f'target.{field} = Generate(art.Get(', badges)
        self.assertIn('target.BadgeWhite = target.Badge;', badges)
        self.assertNotIn('OverlaySource', badges)
        self.assertNotIn('RenderTexture', badges)

    def test_tactical_badge_getters_override_shared_infantry_sprites_only_for_sfp(self):
        for method, callback in (('GetBadge', 'LeaderBadge'), ('GetBadgeWhite', 'LeaderBadgeWhite')):
            self.assertIn(f'Context.Patches.Postfix("Il2CppMenace.Strategy.UnitLeaderTemplate", "{method}", 0, {callback})', CS)
            self.assertIn(f'Require(typeof(UnitLeaderTemplate), "{method}", Type.EmptyTypes, typeof(UnityEngine.Sprite))', CS)
        self.assertIn('var leader = PromotedTemplate(info);', CS)
        for path in (ROOT / 'code').glob('*.cs'):
            self.assertNotIn('InfantryUnitTemplate.Badge =', path.read_text())
            self.assertNotIn('InfantryUnitTemplate.BadgeWhite =', path.read_text())

    def test_optional_xp_and_fallback(self):
        native = (ROOT / 'code/NativeCandidates.cs').read_text()
        rules = (ROOT / 'code/PromotionRules.cs').read_text()
        self.assertIn('SquaddieProbe.CompatibilityApi', native)
        self.assertIn('GetSquaddieXp', native)
        self.assertIn('GetMissionsParticipated()', native)
        self.assertIn('GetAllAlive()', native)
        self.assertIn('IReadOnlyCollection', native)
        self.assertIn('65, 65, 62, 50, 50, 5, 45', rules)
        readme = (ROOT / 'README.md').read_text(encoding='utf-8')
        self.assertIn('Elite **+12** at **8,000+ XP**', readme)

    def test_service_history_titles_and_generic_heads(self):
        identity = (ROOT / 'code/PromotionIdentity.cs').read_text()
        history = (ROOT / 'code/SquaddieServiceHistory.cs').read_text()
        forge = (ROOT / 'code/LeaderForge.cs').read_text()
        self.assertIn('Context.Patches.Postfix("Il2CppMenace.Strategy.Squaddies", "OnMissionEnd", 2, MissionEnded)', CS)
        self.assertIn('Context.Patches.Prefix("Il2CppMenace.Strategy.Squaddies", "OnMissionEnd", 2, MissionEnding)', CS)
        self.assertIn('AddToggle(GenericHeadKey, "Use generic heads for promoted leaders", true)', CS)
        self.assertIn('record.UnitTitle = TitleFor', identity)
        self.assertIn('WoundedMissions', history)
        self.assertIn('leader.CustomHead = null', forge)

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

    def test_isolated_persistent_voice_pitch_profiles(self):
        record = (ROOT / 'code/PromotionRecord.cs').read_text(encoding='utf-8')
        forge = (ROOT / 'code/LeaderForge.cs').read_text(encoding='utf-8')
        voice = (ROOT / 'code/FieldVoiceSystem.cs').read_text(encoding='utf-8')
        self.assertIn('public int VoicePitchPreset { get; set; }', record)
        self.assertIn('1.00f, 0.90f, 0.95f, 1.05f, 1.10f', record)
        self.assertIn('VoicePitchPreset = VoicePitchProfiles.ForBadge(badgeIndex)', forge)
        self.assertIn('record.VoicePitchPreset', forge)
        self.assertIn('pending.PitchPreset', forge)
        self.assertIn('GetProfile(bool female, int pitchPreset) => GetProfile(female, pitchPreset, "")', voice)
        self.assertIn('profile.SourceKey', voice)
        self.assertIn('RequireSet(selected, "fixedPitch", profile.Pitch)', voice)
        self.assertIn('RequireSet(variation, "fixedPitch", 1.00f)', voice)
        self.assertIn('for (int index = 0; index < VoicePitchProfiles.Count; index++)', voice)
        self.assertNotIn('UnityEngine.AudioSource', voice)

    def test_perk_promotion_barks_use_the_gendered_voice_bank(self):
        forge = (ROOT / 'code/LeaderForge.cs').read_text(encoding='utf-8')
        voice = (ROOT / 'code/FieldVoiceSystem.cs').read_text(encoding='utf-8')
        self.assertIn('leader.PromotionBarkSounds = ClonePromotionBarkSounds(voiceDonor.PromotionBarkSounds);', forge)
        self.assertIn('new PromotionBarkSound { PerkTier = bark.PerkTier, Sound = bark.Sound }', forge)
        self.assertIn('_voices.BindPromotionBarks(leader, female, pitchPreset)', forge)
        self.assertIn('new("success", "Lim/success_any", "success"', voice)
        self.assertIn('BarkSpec successBark = VoiceBarks.First(b => b.Key == "success");', voice)
        self.assertIn('(sourceBark.PerkTier - 1) % successBark.SourceItemIds.Length', voice)
        self.assertIn('GetCustomItemId(profile, successBark, sourceIndex)', voice)
        self.assertIn('Sound = new Il2CppStem.ID(profile.BankId, customId)', voice)
        self.assertIn('leader.PromotionBarkSounds = routedBarks;', voice)
        self.assertIn('bark.Sound.BankId != profile.BankId', voice)
        for gender in ('john', 'jane'):
            self.assertIn(f'["{gender}|success|0"] = "That\'s more like it."', voice)

    def test_ready_barks_match_contextual_recordings_and_subtitles(self):
        voice = (ROOT / 'code' / 'FieldVoiceSystem.cs').read_text(encoding='utf-8')
        routes = json.loads((ROOT / 'runtime_assets/voice/voice_routes.json').read_text(encoding='utf-8'))['routes']
        expected = {
            'ready_quiet': [
                ('Everyone hear that?', ('enemy_hidden', 0)),
                ("I don't like the sound of that.", ('enemy_hidden', 1)),
                ('Everyone hear that?', ('enemy_hidden', 0)),
            ],
            'ready_shouty': [
                ("I'm taking fire.", ('taking_fire', 0)),
                ('Taking heavy fire!', ('taking_fire', 1)),
                ('Might need a little backup.', ('suppressed', 0)),
                ("If you're up for it, I could use some help.", ('suppressed', 1)),
                ('Take cover!', ('taking_fire', 3)),
                ('Give us some cover fire!', ('ally_suppressed', 1)),
                ("Go, I'll cover you.", ('ally_suppressed', 0)),
                ('Find cover!', ('ally_taking_fire', 1)),
                ('Not good.', ('morale_wavering', 0)),
                ('Ah, hell.', ('morale_wavering', 1)),
            ],
        }
        for gender in ('john', 'jane'):
            for bark, entries in expected.items():
                for index, (caption, (source_bark, source_index)) in enumerate(entries):
                    self.assertIn(f'["{gender}|{bark}|{index}"] = "{caption}"', voice)
                    target_assets = routes[f'{gender}|{bark}|{index}']
                    source_assets = routes[f'{gender}|{source_bark}|{source_index}']
                    self.assertEqual(target_assets, source_assets, f'{gender} {bark} {index}')

    def test_normal_ready_variants_have_independent_captioned_click_routes(self):
        voice = (ROOT / 'code' / 'FieldVoiceSystem.cs').read_text(encoding='utf-8')
        routes = json.loads((ROOT / 'runtime_assets/voice/voice_routes.json').read_text(encoding='utf-8'))['routes']
        variants = {
            'ready_ack_right': ('Right.', ('move_done', 0)),
            'ready_ack_stay_sharp': ('Stay sharp.', ('move_done', 1)),
            'ready_ack_got_this': ("I've got this.", ('move_done', 2)),
            'ready_ack_alright_move': ("Alright, let's move!", ('moving', 0)),
            'ready_ack_move': ("Let's move!", ('moving', 2)),
        }
        for bark, (caption, (source_bark, source_index)) in variants.items():
            self.assertIn(f'new("{bark}", "Lim/click_bark", "click_bark_{bark}"', voice)
            for gender in ('john', 'jane'):
                self.assertIn(f'["{gender}|{bark}|0"] = "{caption}"', voice)
                target_assets = routes[f'{gender}|{bark}|0']
                source_assets = routes[f'{gender}|{source_bark}|{source_index}']
                self.assertEqual(target_assets, source_assets, f'{gender} {bark}')

    def test_reused_voice_routes_are_covered_by_full_bundle_receipt(self):
        voice_dir = ROOT / 'runtime_assets/voice'
        routes = json.loads((voice_dir / 'voice_routes.json').read_text(encoding='utf-8'))['routes']
        requested = {asset for assets in routes.values() for asset in assets}
        receipt = json.loads((voice_dir / 'sfp_john_jane_voice.build.json').read_text(encoding='utf-8'))
        bundled = [clip['assetName'] for clip in receipt['clips']]
        bundled_names = set(bundled)
        self.assertEqual(len(bundled), 429)
        self.assertEqual(len(bundled_names), len(bundled))
        self.assertTrue(requested <= bundled_names)
        self.assertLess(len(requested), len(bundled_names))
        loader = (ROOT / 'code/RuntimeVoiceBundle.cs').read_text(encoding='utf-8')
        voice = (ROOT / 'code/FieldVoiceSystem.cs').read_text(encoding='utf-8')
        self.assertIn('requested.Where(name => !_clips.ContainsKey(name))', loader)
        self.assertIn('_receipt.TryGetValue(name, out var meta)', loader)
        self.assertNotIn('required.Count != 429', voice)

    def test_xcom_missing_routes_are_muted_without_generic_voice_or_captions(self):
        voice = (ROOT / 'code/FieldVoiceSystem.cs').read_text(encoding='utf-8')
        configure = voice.split('private void ConfigureXcomProfile(', 1)[1].split('private sealed class VoiceRoutesFile', 1)[0]
        self.assertIn('definition.ResolveRoute(definitionKey)', configure)
        self.assertIn('if (route.IsSilent) _silentVoiceRoutes.Add(key)', configure)
        self.assertNotIn('fallback', configure.lower())
        self.assertIn('_voiceRouteSubtitles[key] = route.Subtitle', configure)
        self.assertIn('_silentVoiceRoutes.Contains(routeKey)', voice)
        self.assertIn('if (silent) MuteVoiceRecord(sound)', voice)
        self.assertIn('if (silent) MuteVoiceRecord(variation)', voice)
        self.assertIn('RequireSet(record, "randomizeVolume", false)', voice)
        self.assertIn('RequireSet(record, "fixedVolume", 0.0f)', voice)
        self.assertIn('Cannot replace donor voice clip.', voice)
        self.assertNotIn('AudioClip.Create', voice)

    def test_captioned_variants_keep_native_audio_and_text_together(self):
        voice = (ROOT / 'code/FieldVoiceSystem.cs').read_text(encoding='utf-8')
        models = (ROOT / 'code/PromotionVoiceProfiles.cs').read_text(encoding='utf-8')
        prefix = voice.split('public static bool CaptionedSayPrefix(', 1)[1].split('public static void ConversationManagerPrefix(', 1)[0]
        self.assertIn('pool.Next(system._voiceVariantRandom)', prefix)
        self.assertIn('ownedSay = CopyDataRecord(original)', prefix)
        self.assertIn('RequireSet(ownedSay, "Sound", new Il2CppStem.ID(bankId, choice.ItemId))', prefix)
        self.assertIn('RequireSet(ownedSay, "Text", choice.Subtitle)', prefix)
        self.assertIn('system._captionedSayExecute.Invoke(ownedSay, __args)', prefix)
        self.assertNotIn('RequireSet(original,', prefix)
        self.assertIn('variant-only Sound ID is not a pool key', prefix)
        self.assertIn('captioned == null ? clips : new[] { clips[0] }', voice)
        self.assertIn('CopyCollection(ownedVariations, CopyDataRecord)', voice)
        self.assertIn('ids.SetEquals(expectedIds)', voice)
        self.assertIn('public string[] AssetSubtitles', models)
        self.assertIn('captions[0] != route.Value.Subtitle', models)

    def test_safety_warning_in_readme(self):
        readme = (ROOT / 'README.md').read_text(encoding='utf-8')
        self.assertIn(f"## Acceptance test ({MANIFEST['version']} ", readme)
        self.assertIn('BACK UP `test.save` BEFORE ENABLING', readme)
        self.assertIn('exact source soldier is removed', readme)
        self.assertIn('normal market', readme)
        self.assertIn('before native roster load', readme)
        self.assertIn('runtime_assets', readme)


if __name__ == '__main__':
    unittest.main(verbosity=2)

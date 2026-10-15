"""Offline regression checks for the current Jiangyu Smoke Overhaul port.

These tests verify that the performance optimization is scoped to smoke AI
profiles and uses MENACE's existing `InfluencesPositioning` guard. They are
not a substitute for compiling under the user's game or playing an 80x80 turn.
"""
from pathlib import Path
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT.parent / "jiangyu/SmokeOverhaul"
PLUGIN = (ROOT / "src/SmokeOverhaul/Plugin.cs").read_text()
CONFIG = (PACKAGE / "code/SmokeOverhaul.csproj").read_text()
SMOKE_MANIFEST = json.loads((PACKAGE / "jiangyu.json").read_text())


def block(start: str, end: str) -> str:
    assert start in PLUGIN and end in PLUGIN, (start, end)
    return PLUGIN.split(start, 1)[1].split(end, 1)[0]


class FastSmokeSelectionTests(unittest.TestCase):
    def test_versions_and_manifests(self):
        version = SMOKE_MANIFEST['version']
        self.assertIn(f'internal const string Version = "{version}"', PLUGIN)
        self.assertIn(f'<Version>{version}</Version>', CONFIG)

    def test_disabled_performance_search_by_default(self):
        self.assertIn('s.AddToggle("FastAISmokeSelection"', PLUGIN)
        self.assertIn('bool fastAiSmoke = Setting("FastAISmokeSelection", true)', PLUGIN)
        self.assertIn('FastAISmokeSelection { get; private set; } = true;', PLUGIN)

    def test_only_smoke_skills_targeted(self):
        selection = block('private static readonly string[] FastSmokeAiSkillIds =', 'private static bool _fastSmokeAiSummaryLogged')
        ids = re.findall(r'"(active\.[^\"]+)"', selection)
        self.assertEqual(ids, [
            'active.throw_smoke_grenade',
            'active.throw_smoke_grenade_limited',
            'active.launch_mortar_smoke',
            'active.mod.launch_mortar_smoke_autoloader',
            'active.mod.twinfire.launch_mortar_smoke_autoloader',
        ])

    def test_native_only_no_heuristic_hook(self):
        apply = block('private static void ApplyFastAiSmokeSelection()', 'private static void RestoreFastAiSmokeSelection()')
        self.assertIn('profile.InfluencesPositioning = false;', apply)
        self.assertNotIn('harmony.Patch(', apply)
        self.assertNotIn('FindPath', apply.replace('Agent.Evaluate, CreateLOSBlocker.Collect, GetPath, or any per-frame callback', ''))

    def test_template_lookup_by_exact_id(self):
        apply = block('private static void ApplyFastAiSmokeSelection()', 'private static void RestoreFastAiSmokeSelection()')
        self.assertIn('DataTemplateLoader.Get<SkillTemplate>(id, false)', apply)
        self.assertIn('template?.AIConfig', apply)
        self.assertIn('if (!profile.InfluencesPositioning)', apply)

    def test_native_pointer_dedup(self):
        apply = block('private static void ApplyFastAiSmokeSelection()', 'private static void RestoreFastAiSmokeSelection()')
        self.assertIn('Dictionary<IntPtr, SmokeAiPositioningSnapshot>', PLUGIN)
        self.assertIn('IntPtr key = profile.Pointer;', apply)
        self.assertIn('OriginalSmokeAiPositioning.ContainsKey(key)', apply)
        self.assertIn('new SmokeAiPositioningSnapshot(profile, true)', apply)

    def test_restore_on_disable(self):
        setter = block('private static void ApplySmokeDeliveryProfiles()', 'private static void ConfigureSharedMortarSmokeSpawnEffect()')
        self.assertIn('if (RuntimeSettings.FastAISmokeSelection)', setter)
        self.assertIn('ApplyFastAiSmokeSelection();', setter)
        self.assertIn('RestoreFastAiSmokeSelection();', setter)
        restore = block('private static void RestoreFastAiSmokeSelection()', 'private static void ApplyNativeSmokeAiToMortars()')
        self.assertIn('entry.Behavior.InfluencesPositioning = entry.OriginallyInfluencedPositioning;', restore)
        self.assertIn('OriginalSmokeAiPositioning.Clear();', restore)

    def test_restore_on_full_unload(self):
        unload = block('public void OnUnload()', 'private static void RegisterSettings()')
        self.assertIn('RestoreSmokeDeliveryProfiles()', unload)
        full_restore = block('private static void RestoreSmokeDeliveryProfiles()', 'private enum SmokeFootprint')
        self.assertIn('RestoreFastAiSmokeSelection();', full_restore)
        self.assertLess(full_restore.index('RestoreFastAiSmokeSelection();'), full_restore.index('RestoreMortarSmokeAi();'))

    def test_settings_refresh_reapplies_for_toggle(self):
        refresh = block('private void RefreshSettings(bool force)', 'private static int TryPatch(')
        self.assertIn('previousFastAiSmoke != fastAiSmoke', refresh)
        self.assertIn('aiSmoke, fastAiSmoke, pinnedSmoke, simpleAiRules, debug', refresh)

    def test_cooldown_retained(self):
        mortar = block('private static void ApplyNativeSmokeAiToMortars()', 'private static void RestoreMortarSmokeAi()')
        self.assertIn('MortarAiCooldownAddedByUs.Add(id)', mortar)
        self.assertIn('mortarSmoke.AIConfig = infantrySmoke.AIConfig', mortar)

    def test_only_bounded_action_hooks_not_worker_evaluation(self):
        init = block('public void Initialize(', 'public void OnSceneLoaded(')
        self.assertEqual(init.count('hooks += TryPatch('), 11)
        self.assertNotIn('Agent.Evaluate', init)
        self.assertNotIn('CreateLOSBlocker', init)

    def test_one_normal_log_for_activation(self):
        apply = block('private static void ApplyFastAiSmokeSelection()', 'private static void RestoreFastAiSmokeSelection()')
        self.assertIn('&& !_fastSmokeAiSummaryLogged', apply)
        self.assertEqual(apply.count('_log?.Msg('), 1)
        self.assertNotIn('DebugMsg(', apply)

    def test_no_change_to_mortar_aim_code(self):
        self.assertIn('private static void ConfigureSharedMortarSmokeSpawnEffect()', PLUGIN)
        self.assertIn('private static void RestoreMortarSmokeAi()', PLUGIN)

    def test_readme_mentions_behavioral_tradeoff(self):
        for path in (ROOT / 'README.md', PACKAGE / 'README.md'):
            txt=path.read_text()
            self.assertIn('FastAISmokeSelection', txt)
            self.assertIn('current', txt.lower())


if __name__ == '__main__':
    unittest.main()

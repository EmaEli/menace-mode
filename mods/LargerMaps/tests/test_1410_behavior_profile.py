"""Behavior ID sampling must reuse existing profiler flags, and not change AI."""
import hashlib
import importlib.util
import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
PROF = (ROOT/'src/LargerMaps/AIProfiling.cs').read_text()
PLUGIN = (ROOT/'src/LargerMaps/Plugin.cs').read_text()
META = (ROOT/'modpack/LargerMaps/modpack.json').read_text()


class Behavior1410Tests(unittest.TestCase):
    def test_settings_are_exactly_two_existing_debug_flags(self):
        settings = re.findall(r'settings.AddToggle\("(DiagnoseAI(?:Performance|Pathfinding)[^" ]*)"', PLUGIN)
        self.assertEqual(settings, ['DiagnoseAIPerformance', 'DiagnoseAIPathfinding'])
        self.assertIn('if (!ReadBool("DiagnoseAIPerformance", false)) return;', PROF)
        self.assertIn('_aiPerfDeep = ReadBool("DiagnoseAIPathfinding", false);', PROF)
        self.assertIn('AddToggle("DiagnoseAIPerformance", "Aggregate AI task / unit timings; OFF by default", false)', PLUGIN)
        self.assertIn('AddToggle("DiagnoseAIPathfinding", "Extra AI operation and path timings (requires DiagnoseAIPerformance; OFF by default)", false)', PLUGIN)
        self.assertIn('"version": "1.9"', META)

    def test_reuses_existing_collect_hook_no_new_native_hooks(self):
        deep = PROF.split('if (_aiPerfDeep)', 1)[1].split('// With diagnostics OFF', 1)[0]
        self.assertIn('nameof(AiPerfBehaviorCollectStart), nameof(AiPerfBehaviorCollectEnd)', deep)
        self.assertEqual(deep.count('TryPatchAiPerf('), 8)
        self.assertEqual(PROF.count('TryPatchAiPerf('), 14)  # 13 calls + definition
        self.assertIn('GetID', PROF)
        self.assertIn('GetName', PROF)
        self.assertIn('BehaviorId = id', PROF)
        self.assertIn('Start = AiPerfNow(), Turn = turn, ActorKey = actor, BehaviorId = id', PROF)

    def test_buffer_bounds_and_safe_worker_path(self):
        self.assertIn('MaxAiPerfBehaviorKindsPerUnit = 32', PROF)
        self.assertIn('MaxAiPerfBehaviorLines = 8', PROF)
        self.assertIn('i < MaxAiPerfBehaviorLines', PROF)
        self.assertIn('unit.BehaviorCollects.GetOrAdd(state.BehaviorId,', PROF)
        self.assertIn('internal readonly ConcurrentDictionary<string, AiPerfAtomic> BehaviorCollects', PROF)
        self.assertIn('pair.Value.Calls == 0 || pair.Value.Max < Stopwatch.Frequency / 20', PROF)
        self.assertIn('ranking.Sort((left, right) =>', PROF)
        self.assertNotIn('.SelectMany(', PROF)
        self.assertNotIn('Unit = unit, Id = kv.Key, Counter = kv.Value', PROF)
        start = PROF.split('private static void AiPerfBehaviorCollectStart(', 1)[1].split('private static void AiPerfActor2Start(', 1)[0]
        end = PROF.split('private static void AiPerfBehaviorCollectEnd(', 1)[1].split('private static void AiPerfBehaviorExecuteEnd(', 1)[0]
        self.assertNotIn('_log.Msg(', start+end)
        self.assertNotIn('_log.Warning(', start+end)
        self.assertNotIn('Thread.Sleep(', PROF)
        self.assertNotIn('Task.Wait(', PROF)
        self.assertNotIn('.Cancel(', PROF)
        self.assertIn('kind == AiPerfKind.BehaviorCollect ? state.BehaviorId : null', PROF)
        self.assertIn('AI PERF behavior turn=#', PROF)

    def test_no_change_to_gameplay_or_native_instruction_sites(self):
        def digest(relative):
            return hashlib.sha256((ROOT/relative).read_bytes()).hexdigest()
        self.assertEqual(digest('src/LargerMaps/ExtendedPathfinding.cs'),
                         'e77b76734090a53094abc8b2cd329eb8fab07daab73e61ad8a962de11c9adfef')
        self.assertEqual(digest('tests/pathfinding-sites.json'),
                         'd4c3a952135a276a70be1218ec84a32e7eba8996afa8eef806ef8139259fee26')
        self.assertIn('PatchAiTaskLifetime(harmony);\n            PatchAiPerformance(harmony);', PLUGIN)

    def test_behavior_parser_old_and_new_lines(self):
        spec = importlib.util.spec_from_file_location('ai_parser_1410', ROOT/'tools/analyze_ai_perf.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        sample = [
            '[10:17] Larger Maps: AI PERF turn=#15 round=2 faction=0xAAA wall=30132.1ms pending=0 droppedTasks=0 scope=actor-activation actor=0x11 template=enemy.vehicle_rogue_army_heavy_tank closedBy=next-activation evaluate=n=2,sum=29958.3ms,max=18652.9ms task-observed=n=2,sum=30612.5ms,max=19087.8ms execute=n=2,sum=0.6ms,max=0.6ms.',
            '[10:17] Larger Maps: AI PERF behavior turn=#15 actor=0x11 template=enemy.vehicle_rogue_army_heavy_tank id=InflictDamage collect=n=2,sum=20000.0ms,max=17000.0ms.',
            '[10:17] Larger Maps: AI PERF behavior turn=#15 actor=0x11 template=enemy.vehicle_rogue_army_heavy_tank id=Move collect=n=1,sum=9100.0ms,max=9100.0ms.',
            '[10:17] Larger Maps: AI PERF slow turn=#15 op=BehaviorCollect actor=0x11 id=InflictDamage elapsed=17000.0ms.',
        ]
        result=module.parse_lines(sample)
        self.assertEqual(len(result[15]['behaviors']),2)
        self.assertEqual(result[15]['behaviors'][0]['id'],'InflictDamage')
        self.assertEqual(result[15]['behaviors'][1]['sum_ms'],9100.0)
        report=module.report(result)
        self.assertIn('behavior=InflictDamage',report)
        self.assertIn('behavior=Move',report)
        self.assertIn('30.13s',report)


if __name__ == '__main__':
    unittest.main()

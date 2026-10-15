"""Offline v1.5 diagnostic profile invariants; runtime requires Windows/MENACE."""
import hashlib
import importlib.util
import os
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = (ROOT / 'src/LargerMaps/Plugin.cs').read_text()
PROF = (ROOT / 'src/LargerMaps/AIProfiling.cs').read_text()
BASE_PLUGIN_HASH = '8d61e62c42db637f152314ee2a6f4ede5ed76baa8890dff6ba9dd15f2b5312bf'
# #45 reviewed baseline: only the verified 0.7.17 build mapping was added.
BASE_NATIVE_HASH = 'b3a149b45817259accf1f36674003d06b3218bc47f0d7590f961524c25d1f5ca'
BASE_PATCH_MANIFEST_HASH = '66baaaa89c8e643482c0b573311483bb9e815551a96219763d5f9b3390e139b1'


class AiProfiling148Tests(unittest.TestCase):
    def test_non_detaching_ai_task_source_is_byte_identical_to_1410(self):
        """Areas differ intentionally; the actual worker-task patch must not."""
        source = PLUGIN.split('private static void PatchAiTaskLifetime(', 1)[1].split(
            'private static void PatchAiDiagnostics(', 1)[0]
        self.assertEqual(hashlib.sha256(source.encode('utf-8')).hexdigest(),
                         '40d6a681ff2e6503a29ca18a74206dcccf16eac2f18b7813540932aea6920cba')
        self.assertIn('__result = Il2CppSystem.Threading.Tasks.Task.Run(__0);', source)
        self.assertIn('return false;', source)

    def test_native_code_and_patch_manifest_exactly_preserved(self):
        self.assertEqual(hashlib.sha256((ROOT/'src/LargerMaps/ExtendedPathfinding.cs').read_bytes()).hexdigest(),
                         BASE_NATIVE_HASH)
        self.assertEqual(hashlib.sha256((ROOT/'tests/pathfinding-sites.json').read_bytes()).hexdigest(),
                         BASE_PATCH_MANIFEST_HASH)

    def test_profiling_opt_in_and_hot_paths_deep_opt_in(self):
        self.assertIn('if (!ReadBool("DiagnoseAIPerformance", false)) return;', PROF)
        self.assertIn('_aiPerfDeep = ReadBool("DiagnoseAIPathfinding", false);', PROF)
        self.assertIn('if (_aiPerfDeep)', PROF)
        self.assertEqual(PROF.count('if (_aiPerfDeep)'), 2)
        self.assertIn('AddToggle("DiagnoseAIPerformance",', PLUGIN)
        self.assertIn('AddToggle("DiagnoseAIPathfinding",', PLUGIN)
        self.assertRegex(PLUGIN, r'AddToggle\("DiagnoseAIPerformance",[^\n]*false\)')
        self.assertRegex(PLUGIN, r'AddToggle\("DiagnoseAIPathfinding",[^\n]*false\)')
        deep = PROF.split('if (_aiPerfDeep)', 1)[1].split('// With diagnostics OFF', 1)[0]
        for method in ('FindPathForwardOrBackward', 'FindPath', 'MovementGetPath',
                       'AiPerfBehaviorCollectEnd', 'AiPerfPickBehaviorEnd'):
            if method == 'MovementGetPath':
                self.assertIn('nameof(AiPerfMovementGetPathEnd)', deep)
            else:
                self.assertIn(method, deep)

    def test_harmony_type_is_unambiguous_and_matches_existing_plugin(self):
        """Prevent CS0118: Harmony is also a namespace in this plugin's references."""
        self.assertIn('PatchAiPerformance(HarmonyLib.Harmony harmony)', PROF)
        self.assertIn('TryPatchAiPerf(HarmonyLib.Harmony harmony,', PROF)
        self.assertNotRegex(PROF, r'\b(?:PatchAiPerformance|TryPatchAiPerf)\(Harmony harmony\b')
        self.assertIn('InstallHooks(HarmonyLib.Harmony harmony)', PLUGIN)

    def test_task_lifetime_safety_unchanged(self):
        ai = PLUGIN.split('private static void PatchAiTaskLifetime(',1)[1].split('private static void PatchAiDiagnostics(',1)[0]
        self.assertIn('__result = Il2CppSystem.Threading.Tasks.Task.Run(__0);', ai)
        self.assertIn('return false;', ai)
        self.assertIn('PatchAiTaskLifetime(harmony);\n            PatchAiPerformance(harmony);', PLUGIN)
        self.assertIn('p.Task.IsCompleted', PROF)
        executable = re.sub(r'(?m)^\s*//.*$', '', PROF)
        for operation in ('Task.Wait(', '.Wait(', '.WaitAll(', '.Cancel(', 'Thread.Sleep(',
                          'Task.Delay(', 'Task.Run(__0)', 'Environment.FailFast(', 'ApplyPatch('):
            self.assertNotIn(operation, executable)

    def test_bounded_in_memory_profiling(self):
        for statement in ('MaxAiPerfTrackedTasks = 2048', 'MaxAiPerfSlowSamples = 12',
                          'MaxAiPerfUnitLines = 5', 'turn.Heartbeats) >= 6',
                          'Stopwatch.GetTimestamp()', 'Interlocked.Add(', 'ConcurrentQueue<AiPerfPendingTask>',
                          'p.Task.IsCompleted', 'Stopwatch.Frequency / 2',
                          'if (_aiPerfEnabled)'):
            self.assertIn(statement, PROF + PLUGIN)

    def test_methods_are_present_in_users_dump_if_supplied(self):
        file = os.environ.get('MENACE_DUMP_CS')
        if not file:
            self.skipTest('Set MENACE_DUMP_CS to verify optional game signatures')
        dump = Path(file).read_text(errors='replace')
        signatures = (
            'public class AIFaction : BaseFaction',
            'private static Task RunWithTimeout(Action _action, Actor _actor)',
            'public override void OnTurnStart()',
            'protected void Finish()',
            'public class Agent //',
            'private Task ScheduleCriterionEvaluation(int _i, int _split, Criterion _c)',
            'public void Evaluate()', 'public bool Execute()',
            'private void PostProcessTileScores()',
            'private Behavior PickBehavior()',
            'public bool Collect(Actor _actor)',
            'public static MovementResult GetPath(Actor _actor, Tile _dest',
            'public bool FindPath(Tile _start, Tile _dest, Actor _actor',
            'public bool FindPathForwardOrBackward(Tile _start, Tile _dest, Actor _actor',
        )
        for signature in signatures:
            self.assertIn(signature, dump, signature)

    def test_parser_handles_observed_tasks_and_operations(self):
        spec = importlib.util.spec_from_file_location('menace_ai_parser', ROOT/'tools/analyze_ai_perf.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        sample = [
            '[07:01] Larger Maps: AI PERF turn=#3 round=2 faction=0xAB wall=90500.0ms pending=0 droppedTasks=0 evaluate=n=1,sum=89000.0ms,max=89000.0ms task-observed=n=1,sum=89500.0ms,max=89500.0ms execute=n=1,sum=21.0ms,max=21.0ms.',
            '[07:01] Larger Maps: AI PERF ops turn=#3 collect=n=6,sum=80000.0ms,max=79000.0ms behavior-exec=n=1,sum=20.0ms,max=20.0ms movement-path=n=2,sum=15000.0ms,max=14000.0ms.',
            '[07:01] Larger Maps: AI PERF unit turn=#3 actor=0xFF template=enemy.rogue_army evaluate=n=1,sum=89000.0ms,max=89000.0ms task-observed=n=1,sum=89500.0ms,max=89500.0ms execute=n=1,sum=21.0ms,max=21.0ms.',
        ]
        turns = module.parse_lines(sample)
        self.assertEqual(turns[3]['wall_ms'], 90500)
        self.assertEqual(turns[3]['metrics']['task-observed']['max_ms'], 89500)
        self.assertEqual(turns[3]['ops']['collect']['calls'], 6)
        self.assertEqual(turns[3]['units'][0]['actor'], '0xFF')
        self.assertIn('90.50s', module.report(turns))


if __name__ == '__main__':
    unittest.main()

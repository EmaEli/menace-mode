"""Offline regression guards for the 1.4.8 AI activation profiler data-loss bug."""
import importlib.util
import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / 'src/LargerMaps/AIProfiling.cs').read_text()
GAME = (ROOT / 'src/LargerMaps/Plugin.cs').read_text()


class AiRollover149Tests(unittest.TestCase):
    def test_each_actor_activation_is_sealed_instead_of_discarded(self):
        self.assertIn('AiPerfTurn previous = Interlocked.Exchange(ref _aiPerfActive, turn);', SOURCE)
        self.assertIn('AiPerfSealTurn(previous, "next-activation");', SOURCE)
        self.assertIn('AiPerfSealTurn(turn, "finish");', SOURCE)
        self.assertNotIn('previous faction turn #', SOURCE)
        self.assertIn('Interlocked.CompareExchange(ref turn.Sealed, 1, 0)', SOURCE)
        self.assertIn('ConcurrentQueue<AiPerfTurn> _aiPerfSealedTurns', SOURCE)

    def test_closed_activation_waits_for_observed_task_nonblocking(self):
        self.assertIn('PollAiPerfTasks();\n        PublishAiPerfTurns();', SOURCE)
        self.assertIn('Volatile.Read(ref turn.Pending) != 0', SOURCE)
        self.assertIn('MaxAiPerfPendingReports = 64', SOURCE)
        self.assertIn('p.Task.IsCompleted', SOURCE)
        self.assertIn('scope=actor-activation actor=0x', SOURCE)
        self.assertIn('Interlocked.CompareExchange(ref turn.ScheduledActorKey, actorKey, 0)', SOURCE)
        bad = re.sub(r'(?m)^\s*//.*$', '', SOURCE)
        for forbidden in ('Task.Wait(', '.Wait(', '.WaitAll(', 'Thread.Sleep(', '.Cancel(', 'Task.Run(__0)', 'ApplyPatch('):
            self.assertNotIn(forbidden, bad)

    def test_original_ai_timeout_protection_is_untouched(self):
        safety = GAME.split('private static void PatchAiTaskLifetime(',1)[1].split('private static void PatchAiDiagnostics(',1)[0]
        self.assertIn('__result = Il2CppSystem.Threading.Tasks.Task.Run(__0);', safety)
        self.assertIn('return false;', safety)
        self.assertIn('PatchAiTaskLifetime(harmony);\n            PatchAiPerformance(harmony);', GAME)
        self.assertIn('private const string Version = "1.6.1";', GAME)

    def test_parser_preserves_actor_identity_and_duration(self):
        spec = importlib.util.spec_from_file_location('ai_performance_149', ROOT/'tools/analyze_ai_perf.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        rows = [
            '[09:46:21] Larger Maps: AI PERF turn=#7 round=1 faction=0xAB wall=21650.0ms pending=0 droppedTasks=0 scope=actor-activation actor=0x123 template=enemy.vehicle_rogue_army_heavy_tank closedBy=next-activation evaluate=n=3,sum=300.0ms,max=250.0ms task-observed=n=1,sum=21000.0ms,max=21000.0ms execute=n=0,sum=0.0ms,max=0.0ms.',
            '[09:46:21] Larger Maps: AI PERF ops turn=#7 collect=n=6,sum=15000.0ms,max=9000.0ms behavior-exec=n=1,sum=50.0ms,max=50.0ms.'
        ]
        data=module.parse_lines(rows)
        self.assertEqual(data[7]['template'], 'enemy.vehicle_rogue_army_heavy_tank')
        self.assertEqual(data[7]['closed_by'], 'next-activation')
        self.assertEqual(data[7]['wall_ms'], 21650)
        self.assertEqual(data[7]['metrics']['task-observed']['sum_ms'],21000)
        out=module.report(data)
        self.assertIn('21.65s',out)
        self.assertIn('heavy_tank',out)
        self.assertIn('AI activation #7',out)


if __name__ == '__main__':
    unittest.main()

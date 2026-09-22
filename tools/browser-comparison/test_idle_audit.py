"""Synthetic idle diagnostics tests; no host sampling or benchmark evidence."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import idle_audit
import measure


class IdleDiagnosticsTests(unittest.TestCase):
    def test_snapshot_retains_first_eight_counters_without_double_counting_guests(self):
        with patch('measure.Path.read_text', return_value='cpu 10 20 30 40 50 60 70 80 9 11\ncpu0 1 2 3\n'):
            self.assertEqual(measure.host_snapshot(), [10, 20, 30, 40, 50, 60, 70, 80])

    def verdict(self, end):
        with patch('measure.host_snapshot', side_effect=[[100]*8, [100+x for x in end]]), \
             patch('measure.time.monotonic', side_effect=[10, 12]), \
             patch('measure.time.sleep') as sleep, patch('measure.os.sysconf', return_value=100):
            result = measure.complete_host_idle()
            sleep.assert_called_once_with(2)
            return result

    def test_all_busy_categories_include_steal_but_not_idle_or_iowait(self):
        deltas = [10, 4, 20, 500, 300, 2, 6, 38]
        result = self.verdict(deltas)
        self.assertEqual(result['percentOfOneCore'], 40)
        self.assertTrue(result['accepted'])
        self.assertEqual(result['startCounters'], dict.fromkeys(measure.HOST_CPU_CATEGORIES, 100))
        self.assertEqual(result['endCounters']['steal'], 138)
        self.assertEqual(result['tickDeltas'], dict(zip(measure.HOST_CPU_CATEGORIES, deltas)))
        contributions = result['busyPercentOfOneCoreByCategory']
        self.assertEqual(sum(contributions.values()), result['percentOfOneCore'])
        self.assertEqual(contributions['steal'], 19)
        self.assertEqual(contributions['idle'], 0)
        self.assertEqual(contributions['iowait'], 0)
        self.assertEqual(result['ceiling'], 40)
        self.assertFalse(self.verdict([81, 0, 0, 0, 0, 0, 0, 0])['accepted'])
        self.assertTrue(self.verdict([79, 0, 0, 0, 0, 0, 0, 0])['accepted'])

    def test_audit_retains_refused_observation_without_accepting_benchmark(self):
        verdict = self.verdict([81, 0, 0, 0, 0, 0, 0, 0])
        with tempfile.TemporaryDirectory() as temporary, \
             patch('idle_audit.time.monotonic', side_effect=[0, 0, 2, 2]), \
             patch('idle_audit.measure.complete_host_idle', return_value=verdict):
            output = Path(temporary)/'audit'
            summary = idle_audit.audit(output, duration_seconds=2)
            self.assertTrue(summary['complete'])
            self.assertEqual(summary['refused'], 1)
            self.assertEqual(summary['observations'], 1)
            self.assertIn('not benchmark acceptance', summary['mode'])
            row = json.loads((output/'observations.jsonl').read_text())
            self.assertEqual(row['tickDeltas']['user'], 81)
            self.assertEqual(summary['totalTickDeltas']['user'], 81)
            self.assertEqual(summary['maximumBusyPercentOfOneCoreByCategory']['user'], 40.5)

    def test_failed_audit_preserves_partial_observations(self):
        verdict = self.verdict([1, 0, 0, 0, 0, 0, 0, 0])
        with tempfile.TemporaryDirectory() as temporary, \
             patch('idle_audit.time.monotonic', side_effect=[0, 0, 1, 2]), \
             patch('idle_audit.measure.complete_host_idle', side_effect=[verdict, OSError('read failed')]):
            output = Path(temporary)/'audit'
            with self.assertRaises(OSError): idle_audit.audit(output, duration_seconds=3)
            self.assertEqual(len((output/'observations.jsonl').read_text().splitlines()), 1)
            summary = json.loads((output/'summary.json').read_text())
            self.assertFalse(summary['complete'])
            self.assertEqual(summary['failure'], 'read failed')


if __name__ == '__main__':
    unittest.main()

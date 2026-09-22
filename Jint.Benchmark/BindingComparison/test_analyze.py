"""Synthetic-only tests; never reads a real benchmark directory."""
import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('paired', Path(__file__).with_name('analyze.py'))
paired = importlib.util.module_from_spec(spec)
spec.loader.exec_module(paired)

class AnalyzerTests(unittest.TestCase):
    def test_requires_full_commit_identity(self):
        with self.assertRaisesRegex(ValueError, 'full commit SHA'):
            paired.analyze(Path('/unused'), Path('/unused'), 'main')

    def test_statistics(self):
        self.assertEqual(paired.bootstrap([5.0] * 6), [5.0, 5.0])
        self.assertEqual(paired.bootstrap([-2, -1, 0, 1, 2, 3]), paired.bootstrap([-2, -1, 0, 1, 2, 3]))
        self.assertEqual(paired.statistics.median([1, 2, 3, 4, 5, 6]), 3.5)

    def test_units(self):
        for unit in ['us', '\u03bcs', '\u00b5s']:
            self.assertEqual(paired.mean_ns('1.25 ' + unit), 1250)
        self.assertEqual(paired.mean_ns('1,234.5 ns'), 1234.5)
        for invalid in ['NA', '0 ns', '-1 ns', '1 qs', 'NaN ns', 'Infinity ns']:
            with self.assertRaises(ValueError):
                paired.mean_ns(invalid)

    def fixture(self, root):
        provenance = {'gitHead': 'a' * 40, 'gitStatus': '', 'sdk': 'synthetic-sdk'}
        for arm in paired.ARMS:
            names = ['AngleSharp', 'BenchmarkDotNet', 'Acornima', 'Jint']
            if arm == 'Baseline': names.append('AngleSharp.Js')
            report = {'arm': paired.LABELS[arm], 'runtime': 'synthetic-runtime', 'os': 'synthetic-os',
                      'architecture': 'synthetic-arch', 'workloadSha256': 'b' * 64,
                      'assemblies': [{'name': name, 'version': 'synthetic-version', 'sha256': 'c' * 64} for name in names],
                      'results': [{'workload': workload, 'instance': instance, 'lane': 'cold' if call == 0 else 'warm', 'checksum': checksum}
                                  for workload, checksum in paired.CHECKSUMS.items() for instance in range(2) for call in range(3)]}
            provenance[arm] = report
            (root / f'{arm}.verify.log').write_text(json.dumps(report, separators=(',', ':')) + '\n')
        (root / 'verification.json').write_text(json.dumps(provenance))
        transcript = []
        for r in range(1, 7):
            for arm in (paired.ARMS if r % 2 else paired.ARMS[::-1]):
                transcript.append(f'Collecting round {r}: {arm}')
                folder = root / f'round-{r}' / arm
                (folder / 'results').mkdir(parents=True)
                log = '// Jint measurement environment: gate\n//   launches    : 3\n// tiering: production defaults (tiered compilation + dynamic PGO)\n// machine check: background CPU 10.0% of one core\n'
                log += ''.join(f'// Launch: {launch} / 3\n' for _ in range(8) for launch in (1, 2, 3))
                log += '// Global total time: synthetic, executed benchmarks: 8\n'
                (folder / 'run.log').write_text(log)
                with (folder / 'results' / 'Synthetic-report.csv').open('w', newline='') as stream:
                    writer = csv.writer(stream, delimiter=';' if r % 2 else ',')
                    writer.writerow(['Method', 'Workload', 'LaunchCount', 'Mean', 'Allocated'])
                    for method, workload in sorted(paired.KEYS):
                        writer.writerow([method, workload, 3, '100 ns' if arm == 'Baseline' else '80 ns', 'synthetic'])
        transcript.append(paired.COMPLETE)
        driver = root / 'driver.stdout.log'
        driver.write_text('\n'.join(transcript))
        return driver

    def test_complete_fixture(self):
        with tempfile.TemporaryDirectory(prefix='paired-synthetic-') as directory:
            root = Path(directory); driver = self.fixture(root)
            result = paired.analyze(root, driver, 'a' * 40)
            self.assertEqual(len(result['summary']), 8)
            for row in result['summary']:
                self.assertEqual(row['pairs'], 6)
                self.assertEqual(row['medianDeltaPercent'], -20)
                self.assertEqual(row['ci95Percent'], [-20, -20])
                self.assertEqual(row['verdict'], 'FASTER')
            self.assertEqual(len(result['armAudit']), 12)
            self.assertEqual(len(result['files']), 28)

    def test_reject_incomplete_before_reading_provenance(self):
        with tempfile.TemporaryDirectory(prefix='paired-synthetic-') as directory:
            root = Path(directory); driver = root / 'driver.stdout.log'; driver.write_text('Collecting round 1: Baseline')
            with self.assertRaisesRegex(ValueError, 'completion marker'):
                paired.analyze(root, driver, 'a' * 40)

    def test_reject_busy_gate(self):
        with tempfile.TemporaryDirectory(prefix='paired-synthetic-') as directory:
            root = Path(directory); driver = self.fixture(root)
            log = root / 'round-6' / 'Candidate' / 'run.log'
            log.write_text(log.read_text().replace('10.0%', '74.4%'))
            with self.assertRaisesRegex(ValueError, 'Busy idle verdict'):
                paired.analyze(root, driver, 'a' * 40)

    def test_reject_duplicate_csv_row(self):
        with tempfile.TemporaryDirectory(prefix='paired-synthetic-') as directory:
            root = Path(directory); driver = self.fixture(root)
            path = root / 'round-1' / 'Baseline' / 'results' / 'Synthetic-report.csv'
            lines = path.read_text().splitlines(); lines[2] = lines[1]; path.write_text('\n'.join(lines))
            with self.assertRaisesRegex(ValueError, 'Unexpected/duplicate row'):
                paired.analyze(root, driver, 'a' * 40)

if __name__ == '__main__':
    unittest.main()

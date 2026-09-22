#!/usr/bin/env python3
"""Analyze a completed six-round BindingComparison run; never launch builds/benchmarks.

Run after the measurement process exits successfully. The driver transcript must retain
both stdout and stderr; incomplete or rejected runs never produce statistics.

Matches measure-paired.ps1's statistic and percentile convention: six paired percent differences,
median, 5000 resampled medians, sorted zero-based indices 125 and 4875. Uses Python Random(20260816)
reset per row. Python's Mersenne Twister stream is NOT claimed identical to .NET System.Random.
The stricter completeness policy requires all six pairs for every one of the eight expected rows.
"""
import argparse
import csv
import hashlib
import io
import json
import math
from pathlib import Path
import random
import re
import statistics
import sys

ARMS = ('Baseline', 'Candidate')
LABELS = {'Baseline': 'AngleSharp.Js reflection', 'Candidate': 'main hand-shaped Node/Element/Document'}
CHECKSUMS = {'NodeRead': 13000, 'ElementReadWrite': 8500, 'DocumentLookup': 5000, 'InterpreterControl': 3500}
KEYS = {(method, workload) for method in ('Cold', 'Warm') for workload in CHECKSUMS}
UNITS = {'ns': 1, 'us': 1e3, '\u03bcs': 1e3, '\u00b5s': 1e3, 'ms': 1e6, 's': 1e9, 'm': 6e10, 'h': 3.6e12, 'd': 8.64e13}
COMPLETE = 'Complete paired raw artifacts saved. Analyze paired differences and confidence intervals before making claims.'
SEED = 20260816
RESAMPLES = 5000


def require(condition, message):
    if not condition:
        raise ValueError(message)


def mean_ns(cell):
    match = re.fullmatch(r'([0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)\s*(\S+)', cell.strip())
    require(match is not None, f'Unparseable mean: {cell!r}')
    unit = match[2]
    require(unit in UNITS, f'Unknown time unit: {unit!r}')
    value = float(match[1].replace(',', '')) * UNITS[unit]
    require(math.isfinite(value) and value > 0, f'Nonpositive/nonfinite mean: {cell!r}')
    return value


def bootstrap(values):
    rng = random.Random(SEED)
    medians = sorted(statistics.median([values[rng.randrange(len(values))] for _ in values])
                     for _ in range(RESAMPLES))
    return [medians[125], medians[4875]]


def analyze(artifacts, driver_log, expected_head):
    require(re.fullmatch(r'[0-9a-fA-F]{40}', expected_head) is not None, 'Expected a full commit SHA.')
    root = artifacts.resolve(strict=True)
    manifest = []

    def read(path):
        path = Path(path)
        require(path.is_file(), f'Missing artifact: {path}')
        content = path.read_bytes()
        manifest.append({'path': str(path.resolve()), 'sha256': hashlib.sha256(content).hexdigest()})
        return content.decode('utf-8-sig')

    # Completion, order and provenance validation precede all timing calculations.
    driver = read(driver_log)
    require(COMPLETE in driver, 'No final driver completion marker: incomplete run.')
    require('Comparison failed:' not in driver, 'Driver reported a failure.')
    observed_order = re.findall(r'Collecting round (\d+): (Baseline|Candidate)', driver)
    expected_order = [(str(r), arm) for r in range(1, 7) for arm in (ARMS if r % 2 else ARMS[::-1])]
    require(observed_order == expected_order, 'Expected exactly six complete alternating pairs in driver transcript.')
    actual_rounds = {p.name for p in root.glob('round-*') if p.is_dir()}
    require(actual_rounds == {f'round-{r}' for r in range(1, 7)}, 'Missing or extra round directories.')
    provenance = json.loads(read(root / 'verification.json'))
    require(provenance.get('gitHead') == expected_head, 'Provenance gitHead mismatch.')
    require(isinstance(provenance.get('gitStatus'), str) and not provenance['gitStatus'].strip(),
            'Dirty/missing git status requires separate review; this analyzer refuses it.')
    require(bool(provenance.get('sdk')), 'Missing SDK provenance.')
    for field in ('runtime', 'os', 'architecture', 'workloadSha256'):
        require(bool(provenance['Baseline'].get(field)), f'Missing {field}.')
        require(provenance['Baseline'][field] == provenance['Candidate'][field], f'Arms differ in {field}.')
    require(re.fullmatch(r'[0-9A-Fa-f]{64}', provenance['Baseline']['workloadSha256']) is not None,
            'Malformed workload hash.')
    assembly_maps = {}
    for arm in ARMS:
        report = provenance[arm]
        require(report.get('arm') == LABELS[arm], f'Incorrect arm identity: {arm}.')
        expected_checks = [{'workload': workload, 'instance': instance,
                            'lane': 'cold' if call == 0 else 'warm', 'checksum': checksum}
                           for workload, checksum in CHECKSUMS.items()
                           for instance in range(2) for call in range(3)]
        require(report.get('results') == expected_checks, f'Incomplete/incorrect checksum results: {arm}.')
        original = [json.loads(line) for line in read(root / f'{arm}.verify.log').splitlines()
                    if line.startswith('{"arm":')]
        require(len(original) == 1 and original[0] == report, f'Original verification differs: {arm}.')
        assemblies = report['assemblies']
        names = [entry['name'] for entry in assemblies]
        require(len(names) == len(set(names)), f'Duplicate assembly provenance: {arm}.')
        required = {'Jint', 'AngleSharp', 'Acornima', 'BenchmarkDotNet'}
        if arm == 'Baseline':
            required.add('AngleSharp.Js')
        require(required <= set(names), f'Missing required assembly provenance: {arm}.')
        for entry in assemblies:
            require(re.fullmatch(r'[0-9A-Fa-f]{64}', entry.get('sha256', '')) is not None,
                    f'Missing assembly hash: {arm}/{entry["name"]}.')
            require(bool(entry.get('version')), f'Missing assembly version: {arm}/{entry["name"]}.')
        assembly_maps[arm] = {entry['name']: entry for entry in assemblies}
    for name in ('AngleSharp', 'BenchmarkDotNet'):
        require(assembly_maps['Baseline'][name] == assembly_maps['Candidate'][name],
                f'Shared native/benchmark assembly differs: {name}.')

    raw_rounds = []
    arm_audit = []
    for round_number in range(1, 7):
        by_arm = {}
        for arm in ARMS:
            directory = root / f'round-{round_number}' / arm
            log = read(directory / 'run.log')
            require('// Jint measurement environment: gate' in log, f'Not gate mode: {directory}.')
            require(re.search(r'//\s+launches\s*:\s*3\b', log), f'Missing three-launch declaration: {directory}.')
            require('production defaults (tiered compilation + dynamic PGO)' in log,
                    f'Missing production-tiering declaration: {directory}.')
            require(not re.search(r'idle check skipped|Machine is not idle:|A debugger is attached|Benchmarks? with issues', log),
                    f'Rejected/overridden measurement: {directory}.')
            idle = re.findall(r'// machine check: background CPU ([0-9]+(?:\.[0-9]+)?)% of one core', log)
            require(bool(idle), f'Missing idle verdict: {directory}.')
            require(all(float(value) <= 40 for value in idle), f'Busy idle verdict: {directory}.')
            require(re.search(r'executed benchmarks:\s*8\b', log), f'Missing eight-benchmark completion: {directory}.')
            for launch in (1, 2, 3):
                markers = re.findall(rf'// Launch:\s*{launch}\s*/\s*3\b', log)
                require(len(markers) == 8, f'Incomplete launches: {directory}, launch {launch}, count {len(markers)}.')
            files = list((directory / 'results').glob('*-report.csv'))
            require(len(files) == 1, f'Expected exactly one result CSV: {directory}.')
            source = read(files[0])
            header = source.splitlines()[0]
            dialect = csv.Sniffer().sniff(header, delimiters=',;')
            reader = csv.DictReader(io.StringIO(source), dialect=dialect)
            require(len(reader.fieldnames) == len(set(reader.fieldnames)), f'Duplicate CSV headers: {files[0]}.')
            rows = list(reader)
            require(len(rows) == 8, f'Expected eight rows: {files[0]}.')
            mapping = {}
            for row in rows:
                require(None not in row and all(value is not None for value in row.values()),
                        f'Malformed CSV field count: {files[0]}.')
                key = row.get('Method'), row.get('Workload')
                require(key in KEYS and key not in mapping, f'Unexpected/duplicate row: {files[0]}, {key}.')
                require(row.get('LaunchCount') == '3', f'CSV launch count differs: {files[0]}, {key}.')
                require(row.get('Mean', '').strip() not in ('', 'NA', 'N/A', '?'), f'Failed mean: {files[0]}, {key}.')
                mapping[key] = row
            require(set(mapping) == KEYS, f'Missing workload/lane rows: {files[0]}.')
            by_arm[arm] = mapping
            arm_audit.append({'round': round_number, 'arm': arm, 'idlePercent': idle, 'csv': str(files[0])})
        raw_rounds.append(by_arm)

    # No statistics are emitted unless every arm/row passed the checks above and every mean parses.
    pairs = {key: [] for key in KEYS}
    for round_number, by_arm in enumerate(raw_rounds, 1):
        for key in sorted(KEYS):
            baseline_raw, candidate_raw = by_arm['Baseline'][key], by_arm['Candidate'][key]
            b, c = mean_ns(baseline_raw['Mean']), mean_ns(candidate_raw['Mean'])
            pairs[key].append({'round': round_number, 'baselineNs': b, 'candidateNs': c,
                               'deltaPercent': 100.0 * (c - b) / b,
                               'baselineRaw': baseline_raw, 'candidateRaw': candidate_raw})
    summary = []
    for key in sorted(KEYS):
        rounds = pairs[key]
        require(len(rounds) == 6, f'Incomplete pairs: {key}.')
        differences = [item['deltaPercent'] for item in rounds]
        ci = bootstrap(differences)
        summary.append({'row': '/'.join(key), 'pairs': 6,
                        'medianDeltaPercent': statistics.median(differences), 'ci95Percent': ci,
                        'positiveRounds': sum(value > 0 for value in differences),
                        'verdict': 'SLOWER' if ci[0] > 0 else 'FASTER' if ci[1] < 0 else 'unresolved',
                        'rounds': rounds})
    require(len(summary) == 8, 'Expected all eight rows, including both interpreter controls.')
    return {
        'method': {'statistic': 'median of six paired 100*(candidate-baseline)/baseline differences',
                   'resamples': RESAMPLES, 'seedPerRow': SEED, 'prng': 'Python random.Random / Mersenne Twister',
                   'pythonVersion': sys.version, 'percentileIndicesZeroBased': [125, 4875],
                   'matchesDotNetDrawStream': False, 'positiveRounds': 'number of slower candidate rounds'},
        'driverLogOrigin': 'persisted driver stdout and stderr',
        'limitations': ['Different interpreter/parser versions and unequal binding surfaces.',
                        'Cold means document-cold, not process startup.',
                        'End-to-end embedding-path comparison, not causal shape or equivalent-browser startup savings.',
                        'Interpreter controls are reported and never subtracted as a correction.',
                        'Intervals are per row, without multiple-comparison adjustment.',
                        'Idle checks are pre-arm observations, not proof of continuously idle execution.'],
        'provenance': provenance, 'armAudit': arm_audit, 'summary': summary, 'files': manifest}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--artifacts', type=Path, required=True)
    parser.add_argument('--driver-log', type=Path, required=True)
    parser.add_argument('--expected-head', required=True, help='Full committed revision recorded by verification.json')
    args = parser.parse_args()
    try:
        result = analyze(args.artifacts, args.driver_log, args.expected_head)
    except (ValueError, KeyError, TypeError, IndexError, OSError, csv.Error) as error:
        parser.exit(1, f'Analysis rejected: {error}\n')
    print(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False))


if __name__ == '__main__':
    main()

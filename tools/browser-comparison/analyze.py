#!/usr/bin/env python3
"""Analyze complete paired rounds; each round first aggregates its nested independent launches."""
import argparse
import hashlib
import itertools
import json
import math
from pathlib import Path
import random
import re
import statistics
from measure import LANES, WORKLOADS, schedule, validate_adapter_trx


def median_interval(values, seed=3575, count=10000):
    if len(values) < 6 or not all(math.isfinite(x) for x in values):
        raise ValueError('Require six finite paired rounds')
    rng = random.Random(seed)
    draws = sorted(statistics.median(rng.choices(values, k=len(values))) for _ in range(count))
    return statistics.median(values), [draws[int(count * .025)], draws[int(count * .975)]]


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def sha256(value):
    return isinstance(value, str) and re.fullmatch('[0-9a-fA-F]{64}', value) is not None


def identity_valid(identity):
    if not identity.get('VersionLabel') or not identity.get('Executable') or not isinstance(identity.get('Arguments'), list):
        raise ValueError('Incomplete executable identity')
    if identity.get('DependencyCoverage') != 'installation-and-system-library-trees' or not identity.get('DependencyRoots'):
        raise ValueError('Incomplete runtime dependency coverage')
    hashes = identity.get('FileSha256', {})
    if identity['Executable'] not in hashes or len(hashes) < 2 or not all(sha256(v) for v in hashes.values()):
        raise ValueError('Incomplete executable/runtime hashes')


def metrics(report):
    if report['SchemaVersion'] != 2 or report['Mode'] != 'raw-diagnostics-not-publication-quality':
        raise ValueError('Expected versioned raw runner records, not smoke output')
    result = report['Results'][0]
    if result['Lifecycle'] != 'new-process-per-row':
        raise ValueError('Borrowed browser is not comparable')
    values = result['Measurements']
    if not values:
        raise ValueError('Missing measurements')
    launch, start, end, final = [values[key] for key in ('ScopeAfterLaunch','ScopeBeforeWork','ScopeAfterWork','ScopeAfterTeardown')]
    if any(v is None for v in (launch, start, end, final)) or final['Populated'] or not all(v['Populated'] for v in (launch,start,end)):
        raise ValueError('Missing accounting or surviving descendants')
    counters = [v['CpuMicroseconds'] for v in (launch,start,end,final)]
    if not all(finite(v) and v >= 0 for v in counters) or counters != sorted(counters):
        raise ValueError('CPU scope counter regressed or unavailable')
    samples = values['ScopeMemorySamples']
    stages = ('launch','preparation','work','teardown')
    if any(not finite(s['ElapsedMilliseconds']) or s['ElapsedMilliseconds'] < 0 or not finite(s['ChargedBytes']) or s['ChargedBytes'] < 0 or s.get('Stage') not in stages for s in samples):
        raise ValueError('Nonfinite or invalid stage memory sample')
    if any(a['ElapsedMilliseconds'] > b['ElapsedMilliseconds'] or stages.index(a['Stage']) > stages.index(b['Stage']) for a,b in zip(samples,samples[1:])):
        raise ValueError('Unordered stage memory samples')
    by_stage = {stage:[s['ChargedBytes'] for s in samples if s['Stage']==stage] for stage in stages}
    if any(len(v) < 2 or max(v) <= 0 for v in by_stage.values()):
        raise ValueError('Missing launch/preparation/work/teardown memory samples')
    if not finite(final['LifetimePeakChargedBytes']) or final['LifetimePeakChargedBytes'] < max(s['ChargedBytes'] for s in samples):
        raise ValueError('Unavailable charged memory counters')
    wall = ('LaunchAndConnectMilliseconds','PageWorkMilliseconds','TeardownMilliseconds','PreparationMilliseconds','LifecycleMilliseconds')
    if any(not finite(values[k]) or values[k] <= 0 for k in wall) or samples[-1]['ElapsedMilliseconds'] > values['LifecycleMilliseconds']:
        raise ValueError('Nonfinite or inconsistent lifecycle clocks')
    return dict(launchMilliseconds=values['LaunchAndConnectMilliseconds'], workMilliseconds=values['PageWorkMilliseconds'],
                teardownMilliseconds=values['TeardownMilliseconds'], lifecycleMilliseconds=values['LifecycleMilliseconds'],
                workCpuMilliseconds=(end['CpuMicroseconds']-start['CpuMicroseconds'])/1000,
                lifecycleCpuMilliseconds=final['CpuMicroseconds']/1000,
                observedLaunchPeakChargedBytes=max(by_stage['launch']),
                observedPreparationPeakChargedBytes=max(by_stage['preparation']),
                observedWorkPeakChargedBytes=max(by_stage['work']),
                observedTeardownPeakChargedBytes=max(by_stage['teardown']),
                kernelLifetimePeakChargedBytes=final['LifetimePeakChargedBytes'])


def analyze(root):
    manifest = json.loads((root/'manifest.json').read_text())
    if manifest['schema'] != 1 or manifest['mode'] != 'gate' or not manifest['complete'] or manifest.get('failure'):
        raise ValueError('Only a complete, accepted gate manifest can be analyzed')
    required = ('gitHead','platform','architecture','sdk','machine','runnerSha256','configSha256','sourceHashes','dependencyManifests')
    if any(not manifest.get(k) for k in required) or not re.fullmatch('[0-9a-f]{40}', manifest['gitHead']):
        raise ValueError('Incomplete collection provenance')
    required_sources = ['Program.cs','OwnedRun.cs','ScopeAccounting.cs','Provenance.cs','Workloads.cs','measure.py','analyze.py']
    if any('tools/browser-comparison/'+name not in manifest['sourceHashes'] for name in required_sources) or 'Jint.Benchmark/MachineStateValidator.cs' not in manifest['sourceHashes']:
        raise ValueError('Incomplete driver source provenance')
    if hashlib.sha256((root/'input-config.json').read_bytes()).hexdigest() != manifest['configSha256']:
        raise ValueError('Input configuration provenance digest mismatch')
    if not sha256(manifest['runnerSha256']) or not sha256(manifest['configSha256']) or not all(sha256(v) for v in manifest['sourceHashes'].values()):
        raise ValueError('Invalid source/configuration hashes')
    if not manifest['platform'].startswith('Linux') or not manifest['machine'].get('logicalProcessors'):
        raise ValueError('Missing authoritative machine identity')
    if set(manifest.get('controls', {})) != {'idle','affinity','power','jit','gc','accounting'} or not all(manifest['controls'].values()):
        raise ValueError('Incomplete machine controls')
    if manifest.get('controls', {}).get('accounting') != 'Linux cgroup v2, CPU usage and charged scope memory; not RSS':
        raise ValueError('Missing authoritative accounting backend')
    if manifest['gitStatus'] or manifest['rounds'] < 6 or manifest['rounds'] % 6 or manifest['launches'] < 3:
        raise ValueError('Invalid source state or launch design')
    kernel = manifest.get('kernelValidation', {})
    if not kernel.get('passed') or hashlib.sha256((root/'kernel-validation.log').read_bytes()).hexdigest() != kernel.get('sha256'):
        raise ValueError('Missing or changed kernel validation evidence')
    if not sha256(kernel.get('adapterSha256')) or hashlib.sha256((root/'adapter-validation.log').read_bytes()).hexdigest() != kernel['adapterSha256']:
        raise ValueError('Missing actual-adapter cleanup validation evidence')
    if not sha256(kernel.get('adapterTrxSha256')) or hashlib.sha256((root/'adapter-tests/adapter.trx').read_bytes()).hexdigest() != kernel['adapterTrxSha256']:
        raise ValueError('Missing actual-adapter test-result provenance')
    validate_adapter_trx(root/'adapter-tests/adapter.trx')
    if not isinstance(manifest.get('externalIdentity'), dict):
        raise ValueError('Incomplete external identity inventory')
    adapters = manifest['config']['adapters']
    if manifest.get('calibrationOf'):
        copies=[{k:v for k,v in a.items() if k!='name'} for a in adapters]
        if len(copies)!=2 or copies[0]!=copies[1]:
            raise ValueError('Calibration requires identical owned configurations')
    elif sorted(a['kind'] for a in adapters) != ['chromium', 'jint', 'lightpanda']:
        raise ValueError('Missing required browser')
    if any(not a.get('executable') or not a.get('versionLabel') or a.get('endpoint') for a in adapters):
        raise ValueError('Incomplete owned adapter configuration')
    names = [a['name'] for a in adapters]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate adapter names')
    dependency_identities = {}
    for adapter in [*adapters, dict(name='__harness',kind='jint')]:
        provenance = manifest['dependencyManifests'].get(adapter['name'], {})
        before = None
        for phase in ('before','after'):
            if not provenance.get(phase) or not sha256(provenance.get(phase+'Sha256')):
                raise ValueError('Incomplete dependency provenance')
            path = root/provenance[phase]
            if hashlib.sha256(path.read_bytes()).hexdigest() != provenance[phase+'Sha256']:
                raise ValueError('Dependency provenance digest mismatch')
            identity = json.loads(path.read_text())
            identity_valid(identity)
            if before is not None and before != identity:
                raise ValueError('Runtime dependencies changed during collection')
            before = identity
        dependency_identities[adapter['name']] = before
        if adapter['kind']=='lightpanda':
            external = manifest['externalIdentity'].get(adapter['name'], {})
            release = external.get('releaseAsset', {})
            if not external.get('versionOutput') or not release.get('id') or not release.get('url') or not sha256(release.get('sha256')) or release['sha256'] != external.get('sha256'):
                raise ValueError('Incomplete external distribution identity')
    expected = list(schedule(manifest['rounds'], manifest['launches'], names))
    rows = manifest['rows']
    if len(rows) != len(expected):
        raise ValueError('Missing/extra paired rows')
    grouped, signatures, identities = {}, {}, {}
    for row, planned in zip(rows, expected):
        if any(row.get(k) != v for k,v in planned.items()):
            raise ValueError('Duplicate, missing, reordered or mismatched scheduled row')
        path = root / row['directory'] / 'result.json'
        if hashlib.sha256(path.read_bytes()).hexdigest() != row['resultSha256']:
            raise ValueError('Raw record digest mismatch')
        expected_idle = {prefix + name for prefix in ('', 'after/') for name in ('idle.log', 'idle-verdict.json', 'host-idle.json')}
        if set(row['idleHashes']) != expected_idle:
            raise ValueError('Incomplete idle provenance')
        for relative, hash_key in [('lifecycle.json','lifecycleSha256'),('collector-cleanup.json','cleanupSha256')]:
            if not sha256(row.get(hash_key)) or hashlib.sha256((path.parent/relative).read_bytes()).hexdigest() != row[hash_key]:
                raise ValueError('Missing or changed lifecycle/cleanup provenance')
        journal = json.loads((path.parent/'lifecycle.json').read_text())
        cleanup = json.loads((path.parent/'collector-cleanup.json').read_text())
        if not journal.get('Terminal') or not journal.get('CleanupSucceeded') or not journal.get('Events') or journal['Events'][-1]['State']!='completed' or not cleanup.get('succeeded') or cleanup.get('failure'):
            raise ValueError('Incomplete lifecycle cleanup')
        for relative, expected_digest in row['idleHashes'].items():
            if hashlib.sha256((path.parent/relative).read_bytes()).hexdigest() != expected_digest:
                raise ValueError('Idle provenance digest mismatch')
        for directory in (path.parent, path.parent/'after'):
            shared = json.loads((directory/'idle-verdict.json').read_text())
            if not shared['Accepted'] or shared['Errors']:
                raise ValueError('Shared idle guard failed')
            verdict = json.loads((directory/'host-idle.json').read_text())
            if not verdict['accepted'] or not math.isfinite(verdict['percentOfOneCore']) or not 0 <= verdict['percentOfOneCore'] <= 40:
                raise ValueError('Idle gate failed')
        report = json.loads(path.read_text())
        if report['Workload'] != row['workload'] or report['Lane'] != row['lane'] or len(report['Results']) != 1:
            raise ValueError('Workload/lane mismatch')
        if report['WarmupCount'] != 5 or report['Iterations'] != (1 if row['lane']=='cold' else 10) or report['MemorySampleIntervalMilliseconds'] != 10:
            raise ValueError('Invalid workload operation/sampling configuration')
        for suffix, field in [('html','FixtureSha256'),('js','AutomationSha256')]:
            if not sha256(report.get(field)) or hashlib.sha256((root/'workloads'/(row['workload']+'.'+suffix)).read_bytes()).hexdigest() != report[field].lower():
                raise ValueError('Mismatched workloads or missing exported source provenance')
        if (root/'workloads'/(row['workload']+'.expected.txt')).read_text() != report['AssertedResult']:
            raise ValueError('Mismatched workload correctness contract')
        result = report['Results'][0]
        if result['Name'] != row['adapter']:
            raise ValueError('Browser identity mismatch')
        if not result.get('BrowserVersion') or not result.get('UserAgent') or not result.get('EffectiveArguments') or not report.get('HarnessVersion') or not report.get('PuppeteerVersion'):
            raise ValueError('Incomplete runtime/command provenance')
        environment = report.get('Environment', {})
        if any(not environment.get(k) for k in ('FrameworkDescription','OSDescription','Architecture','ProcessorCount')):
            raise ValueError('Incomplete runtime environment')
        if any(result.get('EffectiveEnvironment', {}).get(k) != '0' for k in ('DOTNET_gcConcurrent','DOTNET_gcServer')):
            raise ValueError('Incomplete effective runtime controls')
        identity = result['Identity']
        if identity != dependency_identities[row['adapter']]:
            raise ValueError('Raw identity disagrees with dependency manifest')
        if journal.get('Accounting') != result['Measurements']['ScopeAfterTeardown'] or journal.get('ScopeMemorySamples') != result['Measurements']['ScopeMemorySamples']:
            raise ValueError('Lifecycle accounting disagrees with raw record')
        if identity['FileSha256'].get(identity['Executable'], '').lower() != manifest['binaryHashes'][row['adapter']].lower():
            raise ValueError('Executable changed during collection')
        # Profiles and accounting directories are per-launch; stable config arguments/dependencies must agree.
        identity_signature = json.dumps([identity, ['--user-data-dir=<fresh>' if a.startswith('--user-data-dir=') else a for a in result['EffectiveArguments']], result['BrowserVersion'], result['UserAgent'], report['Environment'],
                                         result['EffectiveEnvironment'], report['HarnessVersion'], report['PuppeteerVersion']], sort_keys=True)
        if row['adapter'] in identities and identities[row['adapter']] != identity_signature:
            raise ValueError('Browser dependencies, runtime or configuration changed between rounds')
        identities[row['adapter']] = identity_signature
        signature = tuple(report[k] for k in ('FixtureSha256','AutomationSha256','AssertedResult','Lane','WarmupCount','Iterations','MemorySampleIntervalMilliseconds'))
        key = (row['workload'], row['lane'])
        if key in signatures and signatures[key] != signature:
            raise ValueError('Mismatched workloads or operation counts between paired browsers')
        signatures[key] = signature
        key = (row['round'], row['workload'], row['lane'], row['adapter'])
        grouped.setdefault(key, []).append(metrics(report))
    comparisons=[]
    for left,right in itertools.combinations(names,2):
        for workload in WORKLOADS:
            for lane in LANES:
                for metric in next(iter(grouped.values()))[0]:
                    pairs=[]
                    for index in range(manifest['rounds']):
                        arms=[grouped[(index,workload,lane,n)] for n in (left,right)]
                        if any(len(arm)!=manifest['launches'] for arm in arms):
                            raise ValueError('Incomplete nested launches')
                        a,b=[statistics.median(r[metric] for r in arm) for arm in arms]
                        if not math.isfinite(a) or not math.isfinite(b) or a <= 0 or b < 0:
                            raise ValueError('Nonfinite/zero denominator or unavailable metric')
                        pairs.append(dict(round=index,left=a,right=b,differencePercent=100*(b-a)/a))
                    median,interval=median_interval([p['differencePercent'] for p in pairs])
                    comparisons.append(dict(left=left,right=right,workload=workload,lane=lane,metric=metric,
                        medianDifferencePercent=median,confidence95Percent=interval,
                        direction='higher' if interval[0]>0 else 'lower' if interval[1]<0 else 'unresolved',pairs=pairs))
    return dict(manifestSha256=hashlib.sha256((root/'manifest.json').read_bytes()).hexdigest(),
                method='Median of independent launches within each paired round; median round percentage difference; paired round percentile bootstrap, 10000 resamples, seed3575. No pooling launches as independent rounds and no interpreter-control subtraction.',
                comparisons=comparisons)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('artifacts',type=Path)
    print(json.dumps(analyze(parser.parse_args().artifacts),indent=2))

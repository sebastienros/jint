"""Synthetic collector/accounting/statistics checks. These values are never performance evidence."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch
import analyze
import measure


class ComparisonTests(unittest.TestCase):
    def test_missing_member_diagnostics_do_not_invalidate_kernel_counters(self):
        with tempfile.TemporaryDirectory() as directory:
            scope=Path(directory)
            for name,value in {'cpu.stat':'usage_usec 123','memory.current':'100','memory.peak':'200','cgroup.events':'populated 0'}.items():
                (scope/name).write_text(value)
            record=measure.scope_record(scope)
            self.assertEqual(record['cpu.stat'],'usage_usec 123')
            self.assertEqual(record['memory.peak'],'200')
            self.assertEqual(record['memberObservation']['error']['type'],'FileNotFoundError')

    def test_counter_read_failure_still_terminates_scope_and_records_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); scope=root/'scope';scope.mkdir()
            (scope/'cgroup.kill').write_text('0');(scope/'cgroup.events').write_text('populated 0')
            with patch('measure.scope_record',side_effect=OSError('counter unreadable')), patch.object(Path,'rmdir') as remove:
                with self.assertRaisesRegex(OSError,'counter unreadable'):
                    measure.cleanup_scope(scope, root/'cleanup.json')
                remove.assert_called_once()
            self.assertEqual((scope/'cgroup.kill').read_text(),'1')
            record=json.loads((root/'cleanup.json').read_text())
            self.assertFalse(record['succeeded']);self.assertEqual(record['failure']['type'],'OSError')

    def test_timeout_preserves_partial_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            error=subprocess.TimeoutExpired(['fake'], 180, output=b'partial result', stderr=b'partial diagnostic')
            with patch('measure.subprocess.run', side_effect=error):
                with self.assertRaises(subprocess.TimeoutExpired): measure.captured_run(['fake'], root, timeout=180)
            self.assertEqual((root/'result.json').read_text(), 'partial result')
            self.assertEqual((root/'stderr.log').read_text(), 'partial diagnostic')

    def test_schedule_balances_positions_and_predecessors(self):
        rows=list(measure.schedule(6,3,['a','b','c']))
        first=[r for r in rows if r['workload']==measure.WORKLOADS[0] and r['lane']=='cold']
        for name in ('a','b','c'):
            positions=[r['order'].index(name) for r in first if r['adapter']==name]
            self.assertEqual([positions.count(i) for i in range(3)],[6,6,6])
        self.assertEqual(len({tuple(r['order']) for r in first}),6)

    def test_gate_rejects_borrowed_and_insufficient_launches(self):
        config={'adapters':[dict(name=n,kind=n,executable='/fake',versionLabel='synthetic') for n in ('jint','chromium','lightpanda')]}
        for rounds,launches in [(1,3),(6,1),(7,3)]:
            with self.assertRaises(ValueError): measure.validate(config,True,rounds,launches,None)
        config['adapters'][2]['endpoint']='ws://127.0.0.1:9222'
        with self.assertRaisesRegex(ValueError,'owned'): measure.validate(config,True,6,3,None)

    def test_stats_use_paired_rounds_and_leave_zero_unresolved(self):
        self.assertEqual(analyze.median_interval([0]*6),(0,[0,0]))
        self.assertEqual(analyze.median_interval([-10]*6),(-10,[-10,-10]))
        with self.assertRaises(ValueError): analyze.median_interval([1]*3)

    def test_missing_kernel_counters_are_not_zero(self):
        report=synthetic_report('jint','parse-extract','cold')
        self.assertEqual(analyze.metrics(report)['workCpuMilliseconds'],1)
        report['Results'][0]['Measurements']['ScopeAfterWork']=None
        with self.assertRaises(ValueError): analyze.metrics(report)

    def test_analysis_rejects_nonfinite_timestamps_and_missing_stages(self):
        for value in [float('nan'), float('inf'), -1]:
            report=synthetic_report('jint','parse-extract','cold')
            report['Results'][0]['Measurements']['ScopeMemorySamples'][0]['ElapsedMilliseconds']=value
            with self.assertRaisesRegex(ValueError,'Nonfinite'): analyze.metrics(report)
        report=synthetic_report('jint','parse-extract','cold')
        report['Results'][0]['Measurements']['ScopeMemorySamples'].pop()
        with self.assertRaisesRegex(ValueError,'Missing launch'): analyze.metrics(report)

    def test_analysis_rejects_incomplete_provenance_before_statistics(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); original=synthetic_artifacts(root)
            for key in ['gitHead','sourceHashes','dependencyManifests','runnerSha256','sdk','machine']:
                manifest=copy.deepcopy(original);manifest.pop(key);measure.write(root/'manifest.json',manifest)
                with self.assertRaisesRegex(ValueError,'Incomplete'): analyze.analyze(root)
            manifest=copy.deepcopy(original);manifest['dependencyManifests']['lightpanda'].pop('after')
            measure.write(root/'manifest.json',manifest)
            with self.assertRaisesRegex(ValueError,'Incomplete dependency'): analyze.analyze(root)

    def test_live_descendants_and_counter_regression_invalidate_metrics(self):
        for key,value in [('Populated',True),('CpuMicroseconds',1)]:
            report=synthetic_report('jint','parse-extract','cold')
            report['Results'][0]['Measurements']['ScopeAfterTeardown'][key]=value
            with self.assertRaises(ValueError): analyze.metrics(report)

    def test_analysis_pipeline_rejects_missing_pairs_tampering_and_mixed_workloads(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);manifest=synthetic_artifacts(root)
            baseline=analyze.analyze(root)
            self.assertEqual(len(baseline['comparisons']),3*5*2*11)
            self.assertTrue(all(r['direction']=='unresolved' for r in baseline['comparisons']))
            manifest['rows'].pop();measure.write(root/'manifest.json',manifest)
            with self.assertRaisesRegex(ValueError,'Missing/extra'): analyze.analyze(root)
            manifest=synthetic_artifacts(root)
            path=root/'0000/result.json';report=json.loads(path.read_text());report['FixtureSha256']='different'
            measure.write(path,report)
            with self.assertRaisesRegex(ValueError,'digest'): analyze.analyze(root)
            manifest['rows'][0]['resultSha256']=measure.digest(path);measure.write(root/'manifest.json',manifest)
            with self.assertRaisesRegex(ValueError,'Mismatched workloads'): analyze.analyze(root)


def synthetic_identity(name):
    return dict(VersionLabel='synthetic-only',Executable='/fake/'+name,Arguments=['synthetic'],
                DependencyRoots=['/fake','/usr/lib'],DependencyCoverage='installation-and-system-library-trees',
                FileSha256={'/fake/'+name:'a'*64,'/fake/runtime':'b'*64})


def synthetic_report(name,workload,lane):
    scope=dict(CpuMicroseconds=1000,CurrentChargedBytes=1000000,LifetimePeakChargedBytes=2000000,Members=[],Populated=True)
    return dict(SchemaVersion=2,Mode='raw-diagnostics-not-publication-quality',Workload=workload,Lane=lane,
                WarmupCount=5,Iterations=1 if lane=='cold' else 10,MemorySampleIntervalMilliseconds=10,
                FixtureSha256=hashlib.sha256(workload.encode()).hexdigest(),AutomationSha256=hashlib.sha256(workload.encode()).hexdigest(),AssertedResult='synthetic-only',
                Environment=dict(FrameworkDescription='synthetic',OSDescription='synthetic',Architecture='synthetic',ProcessorCount=1),
                HarnessVersion='synthetic-only',PuppeteerVersion='synthetic-only',
                Results=[dict(Name=name,Lifecycle='new-process-per-row',BrowserVersion='synthetic-only',UserAgent='synthetic-only',
                Identity=synthetic_identity(name),EffectiveEnvironment=dict(DOTNET_gcConcurrent='0',DOTNET_gcServer='0'),EffectiveArguments=['synthetic'],
                Measurements=dict(LaunchAndConnectMilliseconds=1,PreparationMilliseconds=1,PageWorkMilliseconds=1,TeardownMilliseconds=1,LifecycleMilliseconds=4,
                    ScopeAfterLaunch=scope,ScopeBeforeWork=scope,ScopeAfterWork={**scope,'CpuMicroseconds':2000},ScopeAfterTeardown={**scope,'CpuMicroseconds':3000,'Populated':False},
                    ScopeMemorySamples=[dict(ElapsedMilliseconds=index/2,ChargedBytes=1000000,Stage=stage)
                        for index,stage in enumerate(['launch']*2+['preparation']*2+['work']*2+['teardown']*2)]))])


def synthetic_artifacts(root):
    names=['jint','chromium','lightpanda']
    manifest=dict(schema=1,mode='gate',complete=True,gitStatus='',rounds=6,launches=3,rows=[],binaryHashes={n:'a'*64 for n in names},
                  gitHead='a'*40,platform='Linux synthetic',architecture='synthetic',sdk='synthetic',machine=dict(logicalProcessors=1),
                  runnerSha256='a'*64,configSha256='a'*64,sourceHashes={**{'tools/browser-comparison/'+n:'a'*64 for n in ['Program.cs','OwnedRun.cs','ScopeAccounting.cs','Provenance.cs','Workloads.cs','measure.py','analyze.py']}, 'Jint.Benchmark/MachineStateValidator.cs':'a'*64},
                  externalIdentity={'lightpanda':dict(versionOutput='synthetic',sha256='a'*64,releaseAsset=dict(id=1,url='https://synthetic.invalid',sha256='a'*64))},
                  controls=dict(idle='synthetic',affinity='synthetic',power='synthetic',jit='synthetic',gc='synthetic',accounting='Linux cgroup v2, CPU usage and charged scope memory; not RSS'),
                  dependencyManifests={},config=dict(adapters=[dict(name=n,kind=n,executable='/fake/'+n,versionLabel='synthetic-only') for n in names]))
    (root/'workloads').mkdir(exist_ok=True)
    for workload in measure.WORKLOADS:
        for suffix in ['html','js']: (root/'workloads'/(workload+'.'+suffix)).write_text(workload)
        (root/'workloads'/(workload+'.expected.txt')).write_text('synthetic-only')
    (root/'input-config.json').write_text(json.dumps(manifest['config']))
    manifest['configSha256']=measure.digest(root/'input-config.json')
    for name in [*names,'__harness']:
        identity=synthetic_identity(name)
        measure.write(root/(name+'-identity.json'),identity)
        manifest['dependencyManifests'][name]=dict(before=name+'-identity.json',after=name+'-identity.json',
            beforeSha256=measure.digest(root/(name+'-identity.json')),afterSha256=measure.digest(root/(name+'-identity.json')))
    (root/'kernel-validation.log').write_text('SYNTHETIC KERNEL VALIDATION, NO MEASUREMENT')
    (root/'adapter-validation.log').write_text('SYNTHETIC ACTUAL-ADAPTER VALIDATION, NO MEASUREMENT')
    (root/'adapter-tests').mkdir(exist_ok=True)
    (root/'adapter-tests/adapter.trx').write_text('<TestRun><ResultSummary><Counters total="4" executed="4" passed="4" failed="0" notExecuted="0"/></ResultSummary></TestRun>')
    manifest['kernelValidation'] = dict(passed=True,sha256=measure.digest(root/'kernel-validation.log'),adapterSha256=measure.digest(root/'adapter-validation.log'),adapterTrxSha256=measure.digest(root/'adapter-tests/adapter.trx'))
    for index,row in enumerate(measure.schedule(6,3,names)):
        directory=root/f'{index:04d}';directory.mkdir(exist_ok=True);(directory/'after').mkdir(exist_ok=True)
        report=synthetic_report(row['adapter'],row['workload'],row['lane'])
        measure.write(directory/'result.json',report)
        counters=report['Results'][0]['Measurements']
        measure.write(directory/'lifecycle.json',dict(Terminal=True,CleanupSucceeded=True,Events=[dict(State='completed')],
            Accounting=counters['ScopeAfterTeardown'],ScopeMemorySamples=counters['ScopeMemorySamples']))
        measure.write(directory/'collector-cleanup.json',dict(succeeded=True,failure=None))
        hashes={}
        for sub in [directory,directory/'after']:
            measure.write(sub/'host-idle.json',dict(accepted=True,percentOfOneCore=0))
            measure.write(sub/'idle-verdict.json',dict(Accepted=True,Errors=[]))
            (sub/'idle.log').write_text('SYNTHETIC, NO MEASUREMENT')
            for name in ['host-idle.json','idle-verdict.json','idle.log']:
                path=sub/name;hashes[str(path.relative_to(directory))]=measure.digest(path)
        row.update(directory=directory.name,resultSha256=measure.digest(directory/'result.json'),idleHashes=hashes,
            lifecycleSha256=measure.digest(directory/'lifecycle.json'),cleanupSha256=measure.digest(directory/'collector-cleanup.json'))
        manifest['rows'].append(row)
    measure.write(root/'manifest.json',manifest)
    return manifest


if __name__=='__main__': unittest.main()

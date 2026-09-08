#!/usr/bin/env python3
"""Collect controlled browser comparisons; verification never emits measurements."""
import argparse
import hashlib
import itertools
import json
import os
from pathlib import Path
import platform
import subprocess
import shutil
import sys
import time
import uuid
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
WORKLOADS = ('parse-extract', 'mutate-query', 'event-form', 'fetch-update', 'interpreter-control')
LANES = ('cold', 'warm')


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n')
    temporary.replace(path)


def schedule(rounds, launches, names):
    orders = tuple(itertools.permutations(names))
    for round_index in range(rounds):
        for workload in WORKLOADS:
            for lane in LANES:
                for launch in range(launches):
                    # Six rounds cover all positions AND predecessor/successor orders.
                    order = orders[(round_index + launch) % len(orders)]
                    for name in order:
                        yield dict(round=round_index, launch=launch, workload=workload, lane=lane, adapter=name, order=list(order))


def validate(config, measure, rounds, launches, scope_root, calibration=False):
    names = [a['name'] for a in config['adapters']]
    if not names or len(names) != len(set(names)):
        raise ValueError('Unique, nonempty adapter names are required')
    if not measure:
        return
    if rounds < 6 or rounds % 6 or launches < 3:
        raise ValueError('Gate requires a multiple of six rounds and at least three launches per round')
    if calibration:
        copies=[{k:v for k,v in a.items() if k!='name'} for a in config['adapters']]
        if len(copies)!=2 or copies[0]!=copies[1]:
            raise ValueError('Calibration requires two identical owned adapter configurations')
    elif sorted(a['kind'] for a in config['adapters']) != ['chromium', 'jint', 'lightpanda']:
        raise ValueError('Gate requires exactly Jint, Chromium, and Lightpanda')
    if any(a.get('endpoint') or not a.get('executable') or not a.get('versionLabel') for a in config['adapters']):
        raise ValueError('Gate requires owned executables and exact version labels, never borrowed endpoints')
    if platform.system() != 'Linux' or scope_root is None:
        raise ValueError('Gate requires a dedicated Linux host and an explicitly delegated cgroup v2 root; other platforms are smoke/diagnostic only')
    root = scope_root.resolve(strict=True)
    require_cgroup_mount(root)
    if 'memory' not in (root / 'cgroup.subtree_control').read_text().split() or 'cpu' not in (root / 'cgroup.subtree_control').read_text().split():
        raise ValueError('The delegated cgroup root must already enable cpu and memory controllers; this tool does not change parent policy')
    if os.environ.get('JINT_BENCH_SKIP_IDLE_CHECK') in ('1', 'true'):
        raise ValueError('The comparison gate rejects idle overrides')
    for key in os.environ:
        if key.startswith(('LD_', 'DYLD_')):
            raise ValueError(f'Remove dynamic loader override {key}; it escapes the declared runtime trees')
        if key.startswith(('DOTNET_', 'COMPlus_')) and any(word.lower() in key.lower() for word in ('tier', 'jit', 'pgo', 'gc')):
            raise ValueError(f'Remove runtime tuning override {key}; production tiering/PGO is required')
    if os.environ.get('JINT_BENCH_FIXED_CLOCK') == '1':
        raise ValueError('This Linux gate does not implement fixed-clock changes; no power plan is changed')


def machine_identity():
    identity = dict(logicalProcessors=os.cpu_count())
    if platform.system() == 'Linux':
        for name in ('cpuinfo', 'meminfo', 'self/status', 'self/cgroup'):
            identity[name] = (Path('/proc') / name).read_text()
        identity['cpuOnline'] = Path('/sys/devices/system/cpu/online').read_text().strip()
        identity['clockPolicies'] = {str(p):p.read_text().strip() for p in
            Path('/sys/devices/system/cpu/cpufreq').glob('policy*/scaling_governor')}
    return identity


def captured_run(command, directory, **kwargs):
    """Preserve partial diagnostics even when the controller's wedge ceiling fires."""
    try:
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, **kwargs)
    except subprocess.TimeoutExpired as error:
        for name, value in [('result.json', error.stdout), ('stderr.log', error.stderr)]:
            (directory/name).write_text(value.decode(errors='replace') if isinstance(value, bytes) else value or '')
        raise
    (directory/'result.json').write_text(result.stdout)
    (directory/'stderr.log').write_text(result.stderr)
    result.check_returncode()
    return result


def host_snapshot():
    first = Path('/proc/stat').read_text().splitlines()[0].split()[1:9]
    values = [int(v) for v in first]
    # guest counters are already included in user/nice and must not be counted twice.
    return sum(values) - values[3] - values[4]


def complete_host_idle():
    start = host_snapshot()
    before = time.monotonic()
    time.sleep(2)
    elapsed = time.monotonic() - before
    load = 100 * (host_snapshot() - start) / os.sysconf('SC_CLK_TCK') / elapsed
    return dict(accepted=load <= 40, percentOfOneCore=load, elapsedSeconds=elapsed,
                source='/proc/stat aggregate busy ticks including protected/exited processes', ceiling=40)


def idle(dotnet, assembly, env, directory):
    # Reuses the exact checked-in BDN blocking validator (40%, 45s settling) in a fresh process.
    log = directory / 'idle.log'
    result = subprocess.run([dotnet, str(assembly), '--idle-check'], env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60)
    log.write_text(result.stdout)
    start = result.stdout.rfind('\n{')
    verdict = json.loads(result.stdout[start + 1:] if start >= 0 else result.stdout)
    write(directory / 'idle-verdict.json', verdict)
    if result.returncode or not verdict['Accepted']:
        raise ValueError(f'Idle validator refused; see {log}')
    # Fail closed on Linux process-enumeration blind spots rather than pretending inaccessible PIDs are idle.
    verdict = complete_host_idle()
    write(directory / 'host-idle.json', verdict)
    if not verdict['accepted']:
        raise ValueError(f'Aggregate host idle guard refused: {verdict}')


def require_cgroup_mount(root):
    mounts=[]
    for line in Path('/proc/self/mountinfo').read_text().splitlines():
        fields, filesystem = line.split(' - ',1)
        mount=Path(fields.split()[4].replace('\\040',' '))
        if root.is_relative_to(mount):
            mounts.append((len(mount.parts),filesystem.split()[0]))
    if not mounts or max(mounts)[1] != 'cgroup2':
        raise ValueError('Accounting root is not a kernel cgroup v2 filesystem')


def create_scope(root):
    path = root / ('jint-comparison-' + uuid.uuid4().hex)
    path.mkdir()
    try:
        for required in ('cpu.stat', 'memory.current', 'memory.peak', 'cgroup.kill', 'cgroup.procs', 'cgroup.events'):
            if not (path / required).exists():
                raise ValueError(f'Kernel accounting capability missing: {required}')
        if (path / 'cgroup.procs').read_text().strip():
            raise ValueError('New accounting scope is not empty')
        return path
    except BaseException:
        path.rmdir()
        raise


def scope_record(path):
    if path is None:
        return None
    counters = {name:(path/name).read_text() for name in ('cpu.stat','memory.current','memory.peak','cgroup.events')}
    try:
        counters['memberObservation'] = dict(value=(path/'cgroup.procs').read_text(), error=None)
    except OSError as error:
        counters['memberObservation'] = dict(value=None, error=dict(type=type(error).__name__, message=str(error)))
    return counters


def cleanup_scope(path, record_path=None):
    record = dict(attempted=path is not None, succeeded=False, before=None, after=None, failure=None)
    try:
        if path is not None:
            read_failure = None
            try:
                record['before'] = scope_record(path)
            except OSError as error:
                read_failure = error
            # Counter-read failure must not prevent termination of the exact owned UUID scope.
            (path / 'cgroup.kill').write_text('1')
            deadline = time.monotonic() + 10
            while 'populated 1' in (path / 'cgroup.events').read_text():
                if time.monotonic() >= deadline:
                    raise ValueError(f'Owned descendants did not exit: {path}')
                time.sleep(.02)
            try:
                record['after'] = scope_record(path)
            except OSError as error:
                read_failure = read_failure or error
            path.rmdir()
            if read_failure:
                raise read_failure
        record['succeeded'] = True
    except BaseException as error:
        record['failure'] = dict(type=type(error).__name__, message=str(error))
        raise
    finally:
        if record_path:
            write(record_path, record)


def validate_adapter_trx(path):
    counters = ET.parse(path).find('.//{*}Counters')
    if counters is None or any(counters.get(k) != v for k,v in {'total':'4','executed':'4','passed':'4','failed':'0','notExecuted':'0'}.items()):
        raise ValueError('All four actual-adapter cleanup cases must execute successfully')


def collect_identity(dotnet, assembly, adapter, env, directory):
    directory.mkdir(exist_ok=False)
    write(directory/'adapter.json', adapter)
    result = captured_run([dotnet, str(assembly), '--identity', str(directory/'adapter.json')], directory, env=env, timeout=900)
    identity = json.loads(result.stdout)
    write(directory/'identity.json', identity)
    return identity


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--measure', action='store_true')
    parser.add_argument('--rounds', type=int, default=6)
    parser.add_argument('--launches', type=int, default=3)
    parser.add_argument('--scope-root', type=Path)
    parser.add_argument('--calibrate', help='Run an empirical A/A comparison using two copies of this configured adapter')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    manifest = dict(schema=1, mode='gate' if args.measure else 'verification', complete=False,
                    createdUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), rows=[])
    write(output / 'manifest.json', manifest)
    try:
        (output/'input-config.json').write_bytes(args.config.read_bytes())
        config = json.loads(args.config.read_text())
        if args.calibrate:
            selected=[a for a in config['adapters'] if a['name']==args.calibrate]
            if len(selected)!=1 or not args.measure:
                raise ValueError('Calibration requires --measure and one matching configured adapter name')
            config={**config,'adapters':[{**selected[0],'name':args.calibrate+suffix} for suffix in ('-a','-b')]}
        manifest.update(config=config, platform=platform.platform(), architecture=platform.machine())
        write(output / 'manifest.json', manifest)
        validate(config, args.measure, args.rounds, args.launches, args.scope_root, bool(args.calibrate))
        manifest['calibrationOf']=args.calibrate
        git_head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        git_status = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
        if args.measure and git_status:
            raise ValueError('Gate requires a clean committed driver and workload checkout')
        env = os.environ.copy()
        if Path(args.dotnet).is_absolute():
            env['PATH'] = str(Path(args.dotnet).parent) + os.pathsep + env.get('PATH', '')
            env['DOTNET_ROOT'] = str(Path(args.dotnet).parent)
        build = subprocess.run([args.dotnet, 'build', str(HERE / 'BrowserComparison.csproj'), '-c', 'Release'], cwd=ROOT,
                               env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=300)
        (output / 'build.log').write_text(build.stdout)
        build.check_returncode()
        assembly = ROOT / 'artifacts/bin/BrowserComparison/release/BrowserComparison.dll'
        subprocess.run([args.dotnet, str(assembly), '--export-workloads', str(output/'workloads')], env=env, check=True, timeout=30)
        external_identity = {}
        for adapter in config['adapters']:
            if adapter['kind'] == 'lightpanda' and adapter.get('executable'):
                version_env = dict(env, LIGHTPANDA_DISABLE_TELEMETRY='true')
                binary_hash = digest(adapter['executable'])
                release = adapter.get('releaseAsset')
                if args.measure and (not release or release.get('sha256') != binary_hash or not release.get('id') or not release.get('url')):
                    raise ValueError('Gate requires Lightpanda release asset ID/URL and a digest matching the actual executable')
                external_identity[adapter['name']] = dict(sha256=binary_hash, releaseAsset=release,
                    versionOutput=subprocess.check_output([adapter['executable'], 'version'], env=version_env, text=True, timeout=30).strip())
        manifest.update(externalIdentity=external_identity, binaryHashes={a['name']:digest(a['executable']) for a in config['adapters'] if a.get('executable')}, gitHead=git_head, gitStatus=git_status, platform=platform.platform(), architecture=platform.machine(),
                        machine=machine_identity(), sdk=subprocess.check_output([args.dotnet, '--version'], env=env, text=True).strip(),
                        config=config, configSha256=digest(args.config), rounds=args.rounds if args.measure else 1,
                        launches=args.launches if args.measure else 1, runnerSha256=digest(assembly),
                        sourceHashes={str(p.relative_to(ROOT)):digest(p) for p in [*HERE.iterdir(), ROOT/'Jint.Benchmark/MachineStateValidator.cs'] if p.suffix in ('.cs', '.py', '.csproj')},
                        controls=dict(idle='shared BDN blocking validator plus complete Linux host CPU',
                                      affinity='unpinned: repository topology resolver does not pin Linux',
                                      power='unchanged; fixed clock unsupported and rejected',
                                      jit='production tiered compilation and PGO',
                                      gc='Jint blocking workstation; other browsers retain their native GC',
                                      accounting='Linux cgroup v2, CPU usage and charged scope memory; not RSS' if args.measure else 'none'))
        write(output / 'manifest.json', manifest)
        if args.measure:
            kernel_env = dict(env, JINT_BROWSER_COMPARISON_CGROUP_ROOT=str(args.scope_root.resolve()))
            adapter_check = subprocess.run([args.dotnet, 'test', str(ROOT/'tools/browser-comparison.Tests/BrowserComparison.Tests.csproj'), '-c', 'Release',
                                            '--filter', 'FullyQualifiedName~ActualAdapterFailureAlwaysRecordsAndCleansItsOwnedProcessScope',
                                            '--logger', 'trx;LogFileName=adapter.trx', '--results-directory', str(output/'adapter-tests')],
                                           cwd=ROOT, env=kernel_env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=180)
            (output/'adapter-validation.log').write_text(adapter_check.stdout)
            adapter_check.check_returncode()
            validate_adapter_trx(output/'adapter-tests/adapter.trx')
            kernel = subprocess.run([sys.executable, '-m', 'unittest', 'test_linux_accounting.py', '-v'],
                                    cwd=HERE, env=kernel_env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60)
            (output/'kernel-validation.log').write_text(kernel.stdout)
            kernel.check_returncode()
            if 'skipped' in kernel.stdout:
                raise ValueError('Kernel accounting validation must execute, not skip')
            manifest['kernelValidation'] = dict(passed=True, sha256=digest(output/'kernel-validation.log'), adapterSha256=digest(output/'adapter-validation.log'), adapterTrxSha256=digest(output/'adapter-tests/adapter.trx'))
            write(output / 'manifest.json', manifest)
        identity_adapters = [*config['adapters'], dict(name='__harness', kind='jint', executable=str(Path(shutil.which(args.dotnet) or args.dotnet).resolve()), arguments=[str(assembly)], versionLabel=git_head)]
        identities = {}
        manifest['dependencyManifests'] = {}
        for index, adapter in enumerate(identity_adapters):
            directory = output/f'identity-{index}-before'
            identities[adapter['name']] = collect_identity(args.dotnet, assembly, adapter, env, directory)
            manifest['dependencyManifests'][adapter['name']] = dict(before=str(directory.name+'/identity.json'), beforeSha256=digest(directory/'identity.json'))
            write(output/'manifest.json', manifest)
        names = [a['name'] for a in config['adapters']]
        adapters = {a['name']:a for a in config['adapters']}
        for index, row in enumerate(schedule(manifest['rounds'], manifest['launches'], names)):
            directory = output / f'{index:04d}'
            directory.mkdir()
            scope = None
            manifest['activeRow'] = dict(**row, directory=directory.name, state='planned')
            write(output/'manifest.json', manifest)
            try:
                if args.measure:
                    idle(args.dotnet, assembly, env, directory)
                    scope = create_scope(args.scope_root.resolve())
                adapter = dict(adapters[row['adapter']])
                adapter['identityFile'] = str(output/manifest['dependencyManifests'][row['adapter']]['before'])
                adapter['journalPath'] = str(directory/'lifecycle.json')
                write(directory/'lifecycle.json', dict(SchemaVersion=1, Terminal=False, Events=[dict(State='planned')]))
                if scope:
                    adapter['accountingDirectory'] = str(scope)
                invocation = dict(rounds=1, adapters=[adapter], workload=row['workload'], lane=row['lane'],
                                  warmupCount=5, iterations=1 if row['lane']=='cold' else 10)
                write(directory / 'config.json', invocation)
                child_env = dict(env)
                child_env.update(DOTNET_gcConcurrent='0', DOTNET_gcServer='0')
                result = captured_run([args.dotnet, str(assembly), '--config', str(directory / 'config.json'),
                                       '--collect' if args.measure else '--smoke'], directory, env=child_env, timeout=180)
                report = json.loads(result.stdout)
                if len(report['Results']) != 1 or report['Workload'] != row['workload'] or report['Lane'] != row['lane']:
                    raise ValueError('Runner returned mismatched rows')
                if args.measure:
                    from analyze import metrics
                    metrics(report)
                    counters = report['Results'][0]['Measurements']
                    if counters['ScopeAfterTeardown'] is None or counters['ScopeAfterTeardown']['Populated']:
                        raise ValueError('Incomplete owned process teardown or missing accounting')
                    after = directory / 'after'; after.mkdir()
                    idle(args.dotnet, assembly, env, after)
                if args.measure:
                    row['idleHashes'] = {str(p.relative_to(directory)):digest(p) for d in (directory, directory/'after') for p in (d/'idle.log', d/'idle-verdict.json', d/'host-idle.json')}
                row.update(directory=directory.name, resultSha256=digest(directory/'result.json'), lifecycleSha256=digest(directory/'lifecycle.json'))
                manifest['rows'].append(row)
                write(output / 'manifest.json', manifest)
                print(f"Verified {index+1}: {row['adapter']} {row['workload']} {row['lane']}", flush=True)
            except BaseException as error:
                write(directory/'failure.json', dict(type=type(error).__name__, message=str(error)))
                raise
            finally:
                try:
                    cleanup_scope(scope, directory/'collector-cleanup.json')
                finally:
                    cleanup = json.loads((directory/'collector-cleanup.json').read_text())
                    if (directory/'failure.json').exists() or not cleanup['succeeded']:
                        journal_path = directory/'lifecycle.json'
                        journal = json.loads(journal_path.read_text()) if journal_path.exists() else dict(SchemaVersion=1, Events=[])
                        if not journal.get('Terminal'):
                            journal.update(Terminal=True, CleanupSucceeded=cleanup['succeeded'], CollectorAccounting=cleanup,
                                           CollectorFailure=json.loads((directory/'failure.json').read_text()) if (directory/'failure.json').exists() else cleanup['failure'])
                            write(journal_path, journal)
                if row in manifest['rows']:
                    row['cleanupSha256'] = digest(directory/'collector-cleanup.json')
                    manifest.pop('activeRow', None)
                    write(output/'manifest.json', manifest)
        for index, adapter in enumerate(identity_adapters):
            directory = output/f'identity-{index}-after'
            final_identity = collect_identity(args.dotnet, assembly, adapter, env, directory)
            if final_identity != identities[adapter['name']]:
                raise ValueError('Executable/runtime dependency installation changed during collection')
            manifest['dependencyManifests'][adapter['name']].update(after=str(directory.name+'/identity.json'), afterSha256=digest(directory/'identity.json'))
            write(output/'manifest.json', manifest)
        manifest['complete'] = True
        write(output / 'manifest.json', manifest)
    except BaseException as error:
        manifest['failure'] = str(error)
        write(output / 'manifest.json', manifest)
        raise


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print(f'Comparison incomplete: {error}', file=sys.stderr)
        sys.exit(1)

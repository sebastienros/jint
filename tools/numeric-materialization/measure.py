"""Local diagnostic pairs; not Linux accounting or hosted acceptance.
Run under locked.py. Six rotations balance baseline/candidate/Lightpanda order.
"""
import json, os, pathlib, re, signal, subprocess as sp, sys, time
ROOT = pathlib.Path(__file__).resolve().parents[2]
BASE = ROOT / 'artifacts/numeric-materialization'
DRIVER = ROOT / 'artifacts/bin/NumericDriver/release/NumericDriver.dll'
SKIP_IDLE = '--skip-idle-check' in sys.argv
ENV = os.environ.copy()
for key in ENV:
    if key.upper() != 'DOTNET_ROOT' and key.upper().startswith(('DOTNET_', 'COMPLUS_', 'JINT_BENCH_')):
        raise RuntimeError('Review inherited runtime override before measurement: ' + key)
(BASE/'runtime-environment.json').write_text(json.dumps({k:v for k,v in ENV.items() if k.upper().startswith(('DOTNET_','COMPLUS_','JINT_BENCH_'))},indent=2))
ENV.update(DOTNET_gcConcurrent='0', DOTNET_gcServer='0', LIGHTPANDA_DISABLE_TELEMETRY='true')

def idle(folder, label):
    if SKIP_IDLE:
        (folder / (label + '-idle.txt')).write_text(json.dumps({'Accepted': False, 'Skipped': True, 'Reason': 'Explicit user instruction; noisy local diagnostics'}))
        return
    p = sp.run(['dotnet', str(DRIVER), '--idle-check'], env=ENV, text=True, stdout=sp.PIPE, stderr=sp.STDOUT, timeout=60)
    (folder / (label + '-idle.txt')).write_text(p.stdout)
    if p.returncode: raise RuntimeError('Idle guard refused: ' + p.stdout)

def run(arm, name, lane, label, mode='none', count=None):
    folder = BASE / label
    folder.mkdir()
    idle(folder, 'before')
    command = ([str(BASE/'lightpanda-aarch64-macos'), 'serve', '--host', '127.0.0.1', '--port', '0'] if arm == 'lightpanda' else
               ['dotnet', str(BASE/arm/'Jint.Browser.Tool.dll'), 'serve', '--port', '0'])
    n = count or (1 if lane == 'cold' else 10)
    out = open(folder/'browser.log', 'w')
    err = open(folder/'browser.err', 'w')
    started = time.monotonic()
    browser = sp.Popen(command, stdout=out, stderr=err, env=ENV, start_new_session=True)
    client = trace = None
    try:
        endpoint = None
        until = time.monotonic() + 60
        while time.monotonic() < until:
            output = (folder/'browser.log').read_text() + (folder/'browser.err').read_text()
            match = re.search(r'ws://[^\s\x1b]+', output)
            if match: endpoint = match.group(); break
            # Same official-nightly announcement supported by BrowserAdapter.ParseEndpoint.
            match = re.search(r'address=127\.0\.0\.1:(\d+)', output)
            if arm == 'lightpanda' and 'server running' in output and match:
                endpoint = 'ws://127.0.0.1:' + match.group(1)
                break
            if browser.poll() is not None: raise RuntimeError((folder/'browser.err').read_text())
            time.sleep(.05)
        if not endpoint: raise RuntimeError('No browser endpoint')
        launch = time.monotonic() - started
        with open(folder/'client.log','w') as log:
            client = sp.Popen(['dotnet',str(DRIVER), endpoint, str(browser.pid), name, str(n),str(folder),'0' if lane=='cold' else '5'],stdout=log,stderr=log,env=ENV)
            until = time.monotonic() + 90
            while not (folder/'ready').exists():
                if client.poll() is not None: raise RuntimeError((folder/'client.log').read_text())
                if time.monotonic() > until: raise RuntimeError('Preparation timeout')
                time.sleep(.025)
            if mode != 'none':
                with open(folder/'trace.log','w') as trace_log:
                    trace = sp.Popen(['dotnet-trace','collect','-p',str(browser.pid),'--profile',mode,'-o',str(folder/'profile.nettrace')],stdin=sp.PIPE,stdout=trace_log,stderr=trace_log,env=ENV)
                until = time.monotonic() + 30
                while 'Output File' not in (folder/'trace.log').read_text():
                    if trace.poll() is not None or time.monotonic() > until: raise RuntimeError('Trace not ready')
                    time.sleep(.05)
            (folder/'go').touch()
            client.wait(timeout=120)
            if client.returncode: raise RuntimeError((folder/'client.log').read_text())
            if trace:
                trace.send_signal(signal.SIGINT)
                trace.wait(timeout=45)
        rows = [r for r in json.loads((folder/'rows.json').read_text()) if r['index'] >= 0]
        summary = dict(quality='noisy-local-diagnostic' if SKIP_IDLE else 'idle-accepted-local',arm=arm,workload=name,lane=lane,mode=mode,iterations=n,launchReadyMs=launch*1000,
                       navMs=sum(r['navMs'] for r in rows)/n,evalMs=sum(r['evalMs'] for r in rows)/n,cpuMs=sum(r['cpuMs'] for r in rows)/n)
        (folder/'result.json').write_text(json.dumps(summary,indent=2))
        print(label, json.dumps(summary),flush=True)
    finally:
        for child in [client,trace]:
            if child and child.poll() is None:
                child.terminate()
                try: child.wait(timeout=10)
                except sp.TimeoutExpired: child.kill();child.wait()
        if browser.poll() is None:
            os.killpg(browser.pid, signal.SIGINT)
            try: browser.wait(timeout=10)
            except sp.TimeoutExpired: os.killpg(browser.pid,signal.SIGKILL);browser.wait()
        out.close();err.close()
    idle(folder, 'after')

if __name__ == '__main__':
    tag = sys.argv[2] + '-' if len(sys.argv) > 2 else ''
    if sys.argv[1] == 'profiles':
        for mode in ['cpu-sampling','gc-verbose']:
            for name in ['interpreter-control','mutate-query']:
                for arm in ['baseline','candidate']:
                    run(arm,name,'warm',f'{tag}profile-{mode}-{name}-{arm}',mode,300)
    else:
        orders = [('baseline','candidate','lightpanda'),('lightpanda','candidate','baseline'),
                  ('baseline','lightpanda','candidate'),('candidate','lightpanda','baseline'),
                  ('lightpanda','baseline','candidate'),('candidate','baseline','lightpanda')]
        for round_index, order in enumerate(orders):
            for name in ['interpreter-control','event-form','mutate-query']:
                for lane in ['cold','warm']:
                    for arm in order:
                        run(arm,name,lane,f'{tag}round{round_index+1}-{name}-{lane}-{arm}')

"""Correctness-only stock campaign smoke. Never emits measurements. Run under locked.py."""
import json, os, pathlib, shutil, subprocess as sp
root=pathlib.Path(__file__).resolve().parents[2]
base=root/'artifacts/numeric-materialization'
out=root/'docs/benchmarks/numeric-materialization-2026-09'
folder=base/'verification';folder.mkdir()
with (folder/'build.log').open('w') as log:
 sp.run(['dotnet','build','tools/browser-comparison/BrowserComparison.csproj','-c','Release','--source','https://api.nuget.org/v3/index.json'],check=True,stdout=log,stderr=sp.STDOUT)
exe=shutil.which('dotnet')
harness=root/'artifacts/bin/BrowserComparison/release/BrowserComparison.dll'
adapters=[dict(name=arm,kind='jint',executable=exe,arguments=[str(base/arm/'Jint.Browser.Tool.dll')],versionLabel=arm) for arm in ['baseline','candidate']]
adapters.append(dict(name='lightpanda',kind='lightpanda',executable=str(base/'lightpanda-aarch64-macos'),versionLabel='1.0.0-nightly.9638+962a13007'))
config=folder/'adapters.json';config.write_text(json.dumps(adapters))
with (folder/'identity.log').open('w') as log:
 sp.run([exe,str(harness),'--identities',str(config),str(folder/'identities')],check=True,stdout=log,stderr=sp.STDOUT,timeout=180)
for i,a in enumerate(adapters):a['identityFile']=str(folder/'identities'/f'{i}.json')
env=os.environ.copy();env.update(DOTNET_gcConcurrent='0',DOTNET_gcServer='0')
results=[]
for name in ['interpreter-control','event-form','mutate-query']:
 for lane in ['cold','warm']:
  config=folder/f'{name}-{lane}.json'
  config.write_text(json.dumps(dict(rounds=1,workload=name,lane=lane,warmupCount=5,iterations=1 if lane=='cold' else 10,adapters=adapters)))
  p=sp.run([exe,str(harness),'--config',str(config),'--smoke'],env=env,stdout=sp.PIPE,stderr=sp.PIPE,text=True,timeout=120)
  (folder/f'{name}-{lane}-stdout.json').write_text(p.stdout)
  (folder/f'{name}-{lane}-stderr.txt').write_text(p.stderr)
  p.check_returncode()
  report=json.loads(p.stdout)
  assert report['Mode']=='smoke-no-measurements'
  assert all(r['Measurements'] is None for r in report['Results'])
  results.append({k:report[k] for k in ['Mode','Workload','Lane','WarmupCount','Iterations','FixtureSha256','AutomationSha256','AssertedResult']}
   | {'browsers':[{'Name':r['Name'],'BrowserVersion':r['BrowserVersion'],'Measurements':r['Measurements']} for r in report['Results']]})
  print(name,lane,'all three checksums passed; no measurements',flush=True)
(out/'browser-verification.json').write_text(json.dumps(results,indent=2))

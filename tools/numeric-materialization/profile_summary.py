"""Run under locked.py after collection. Raw traces and ETLX remain ignored."""
import json, pathlib, subprocess as sp, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
BASE=ROOT/'artifacts/numeric-materialization'
OUT=ROOT/'docs/benchmarks/numeric-materialization-2026-09'
tag = sys.argv[1] + '-' if len(sys.argv) > 1 else ''
result={}
for folder in sorted(BASE.glob(tag+'profile-*')):
 if not (folder/'rows.json').exists() or not (folder/'profile.nettrace').exists(): continue
 sp.run(['dotnet',str(ROOT/'artifacts/bin/NumericReader/release/NumericReader.dll'),str(folder)],check=True)
 d=json.loads((folder/'summary.json').read_text())
 rows=[r for r in json.loads((folder/'rows.json').read_text()) if r['index']>=0]
 managed={k:v for k,v in d['stacks'].items() if '|Managed|' in k}
 alloc=d['allocations']
 gc=d['gc']
 pauses=[]
 for event in gc:
  if event['EventName']=='GC/SuspendEEStart' and 'Reason=SuspendForGC;' in event['payload']:
   end=next((x for x in gc if x['EventName']=='GC/RestartEEStop' and x['ms']>=event['ms']),None)
   if end:pauses.append(end['ms']-event['ms'])
 result[folder.name] = dict(iterations=len(rows),eventsLost=d['EventsLost'],
  sampledAllocatedBytes=sum(alloc.values()),
  sampledJsNumberBytes=sum(v for k,v in alloc.items() if '|Jint.Native.JsNumber|' in k),
  sampledMaterializationBytes=sum(v for k,v in alloc.items() if 'MaterializeUnboxedOrNull' in k),
  managedSamples=sum(managed.values()),
  materializationSamples=sum(v for k,v in managed.items() if 'MaterializeUnboxedOrNull' in k),
  constraintSamples=sum(v for k,v in managed.items() if 'CheckAmortizedConstraints' in k or 'OperationDeadlineConstraint' in k),
  pollGCSamples=sum(v for k,v in managed.items() if 'PollGC' in k),
  suspendForGCCount=len(pauses),gcPauseMs=sum(pauses),
  instrumentedWallMs=sum(r['navMs']+r['evalMs'] for r in rows))
(OUT/'profile-summary.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))

if len(result) != 8:
    raise SystemExit('Incomplete profile pairs; no profile verdict.')

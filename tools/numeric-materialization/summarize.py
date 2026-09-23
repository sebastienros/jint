"""Summarize only complete, idle-accepted local pairs, never subtract controls."""
import csv, json, pathlib, random, statistics, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
BASE=ROOT/'artifacts/numeric-materialization'
OUT=ROOT/'docs/benchmarks/numeric-materialization-2026-09'
OUT.mkdir(parents=True,exist_ok=True)
tag = sys.argv[1] + '-' if len(sys.argv) > 1 else ''
allow_skipped = '--skip-idle-check' in sys.argv
rows=[]
for folder in sorted(BASE.glob(tag+'round*')):
    result=folder/'result.json'
    if not result.exists() or not (folder/'after-idle.txt').exists(): continue
    checks = [(folder/f'{x}-idle.txt').read_text() for x in ['before','after']]
    if not all('"Accepted":true' in c or (allow_skipped and '"Skipped": true' in c) for c in checks): continue
    r=json.loads(result.read_text()); r['round']=int(folder.name[len(tag):].split('-')[0][5:]);r['pageMs']=r['navMs']+r['evalMs']
    r['htmlHash']=json.loads((folder/'workload.json').read_text())['HtmlSha256']
    r['scriptHash']=json.loads((folder/'workload.json').read_text())['ScriptSha256']
    rows.append(r)
if rows:
    with (OUT/'local-rows.csv').open('w') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
rng=random.Random(7756)
def interval(values):
    samples=sorted(statistics.median(rng.choices(values,k=len(values))) for _ in range(10000))
    return [statistics.median(values),samples[249],samples[9749]]
summary=[]
for workload in ['interpreter-control','event-form','mutate-query']:
 for lane in ['cold','warm']:
  for metric in ['pageMs','evalMs','navMs','cpuMs']:
   for first,second in [('baseline','candidate'),('lightpanda','baseline'),('lightpanda','candidate')]:
    paired=[]
    for i in range(1,7):
     arms={r['arm']:r for r in rows if r['round']==i and r['lane']==lane and r['workload']==workload}
     if first in arms and second in arms:
      a,b=arms[first],arms[second]
      assert a['htmlHash']==b['htmlHash'] and a['scriptHash']==b['scriptHash']
      paired.append(100*(b[metric]/a[metric]-1))
    if len(paired)==6:
     summary.append(dict(quality='noisy-local-diagnostic' if allow_skipped else 'idle-accepted-local',workload=workload,lane=lane,metric=metric,comparison=f'{second}/{first}-1',rounds=6,
                         medianAnd95PercentBootstrap=interval(paired),pairedPercent=paired))
(OUT/'paired-summary.json').write_text(json.dumps(summary,indent=2))
print(f'{len(rows)} included rows, {len(summary)} six-round comparisons')

if len(rows) != 108 or len(summary) != 72:
    raise SystemExit('Incomplete measurements: six complete rounds required; no performance verdict.')

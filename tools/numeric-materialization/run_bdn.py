"""Exploratory baseline/candidate default-job BDN runs; not a six-pair speed gate."""
import json, os, pathlib, subprocess as sp
root=pathlib.Path(__file__).resolve().parents[2]
source=root/'Jint/Runtime/Interpreter/Expressions/JintBinaryExpression.cs'
candidate=source.read_bytes()
baseline=sp.check_output(['git','show','4a68b51677bd9df1dd6dcde603cf64e9041bd870:Jint/Runtime/Interpreter/Expressions/JintBinaryExpression.cs'])
assert candidate!=baseline
base=root/'artifacts/numeric-materialization'
env=os.environ.copy();env.update(JINT_BENCH_MODE='gate',JINT_BENCH_SKIP_IDLE_CHECK='1',RestoreSources='https://api.nuget.org/v3/index.json')
try:
 for arm,data in [('baseline',baseline),('candidate',candidate)]:
  source.write_bytes(data)
  print('Default-job BDN '+arm+'; idle guard bypassed by user request',flush=True)
  command=['dotnet','run','--project','Jint.Benchmark/Jint.Benchmark.csproj','-c','Release','-f','net10.0','-p:RestoreSources=https://api.nuget.org/v3/index.json','--','--filter','*ModuloMaterializationBenchmark*','--exporters','json','csv','--artifacts',str(base/('bdn-requested-'+arm))]
  with (base/('bdn-requested-'+arm+'.log')).open('w') as log:
   sp.run(command,env=env,stdout=log,stderr=sp.STDOUT,check=True)
  reports=list((base/('bdn-requested-'+arm)/'results').glob('*-report-full*.json'))
  if not reports:
   raise RuntimeError('BDN emitted no full results: '+arm)
  rows=json.loads(reports[0].read_text())['Benchmarks']
  if len(rows)!=3 or any(not row.get('Statistics') for row in rows):
   raise RuntimeError('BDN did not measure all three rows: '+arm)
finally:
 source.write_bytes(candidate)

"""Run under locked.py; restores the candidate even when a baseline build fails."""
import hashlib, json, pathlib, shutil, subprocess as sp
root=pathlib.Path(__file__).resolve().parents[2]
base=root/'artifacts/numeric-materialization'
source=root/'Jint/Runtime/Interpreter/Expressions/JintBinaryExpression.cs'
candidate=source.read_bytes()
baseline=sp.check_output(['git','show','4a68b51677bd9df1dd6dcde603cf64e9041bd870:Jint/Runtime/Interpreter/Expressions/JintBinaryExpression.cs'])
if candidate == baseline:
 raise RuntimeError('Apply docs/benchmarks/numeric-materialization-2026-09/candidate.patch before building the experimental pair.')
manifest={'baselineSha':'4a68b51677bd9df1dd6dcde603cf64e9041bd870','source':{}}
def run(command,label):
 print(label,flush=True)
 with (base/(label+'.log')).open('w') as log:
  p=sp.run(command,stdout=log,stderr=sp.STDOUT)
 if p.returncode:
  print((base/(label+'.log')).read_text()[-6000:],flush=True)
  raise RuntimeError(label+' failed')
def build(project,label):
 run(['dotnet','build',project,'-c','Release','-f','net10.0','--source','https://api.nuget.org/v3/index.json'],label)
try:
 for arm,data in [('baseline',baseline),('candidate',candidate)]:
  source.write_bytes(data)
  manifest['source'][arm]=hashlib.sha256(data).hexdigest()
  build('Jint.Browser.Tool/Jint.Browser.Tool.csproj','build-'+arm)
  shutil.copytree(root/'artifacts/bin/Jint.Browser.Tool/release_net10.0',base/arm,dirs_exist_ok=True)
  manifest[arm]={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (base/arm).glob('*.dll')}
finally:
 source.write_bytes(candidate)
 (base/'build-manifest.json').write_text(json.dumps(manifest,indent=2))
build('tools/numeric-materialization/driver/NumericDriver.csproj','build-driver')
build('Jint.Benchmark/Jint.Benchmark.csproj','build-benchmark')
build('tools/numeric-materialization/reader/NumericReader.csproj','build-reader')
run(['dotnet','test','--project','Jint.Tests/Jint.Tests.csproj','-c','Release','-p:RestoreSources=https://api.nuget.org/v3/index.json','--filter',
 'FullyQualifiedName~ModuloOperandLaneTests|FullyQualifiedName~UnboxedBindingTests|FullyQualifiedName~NumberTests|FullyQualifiedName~SlotLocationCacheTests|FullyQualifiedName~FixedSlotLexicalBindingTests','--timeout','30s'],'tests-numeric')
run(['dotnet','test','--project','Jint.Tests.PublicInterface/Jint.Tests.PublicInterface.csproj','-c','Release','-p:RestoreSources=https://api.nuget.org/v3/index.json','--filter',
 'FullyQualifiedName~HostConstraintClockTests|FullyQualifiedName~HostCallLoopConstraintTests|FullyQualifiedName~HostConstraintReentrancyTests','--timeout','30s'],'tests-constraints')

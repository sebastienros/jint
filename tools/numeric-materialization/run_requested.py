"""User-requested diagnostic rerun without idle rejection; caller holds shared lock."""
import pathlib, subprocess as sp, sys
root=pathlib.Path(__file__).resolve().parents[2]
base=root/'artifacts/numeric-materialization'
tag=sys.argv[1] if len(sys.argv)>1 else 'requested'
commands=[
 ['python3','tools/numeric-materialization/build_pair.py'],
 ['python3','tools/numeric-materialization/measure.py','pairs',tag,'--skip-idle-check'],
 ['python3','tools/numeric-materialization/summarize.py',tag,'--skip-idle-check'],
 ['python3','tools/numeric-materialization/measure.py','profiles',tag,'--skip-idle-check'],
 ['python3','tools/numeric-materialization/profile_summary.py',tag],
]
if '--collect-only' in sys.argv:
 commands=commands[1:]
for command in commands:
 print('Running '+' '.join(command),flush=True)
 sp.run(command,check=True)

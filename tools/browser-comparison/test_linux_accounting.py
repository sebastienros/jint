"""Real kernel leg. Requires an explicitly delegated cgroup root; absence is a reported skip."""
import os
from pathlib import Path
import platform
import subprocess
import sys
import tempfile
import time
import unittest
import measure


@unittest.skipUnless(platform.system()=='Linux' and os.environ.get('JINT_BROWSER_COMPARISON_CGROUP_ROOT'),
                     'requires Linux and JINT_BROWSER_COMPARISON_CGROUP_ROOT delegation')
class KernelAccountingTests(unittest.TestCase):
    def test_exited_parent_does_not_hide_child_cpu_memory_or_cleanup(self):
        root=Path(os.environ['JINT_BROWSER_COMPARISON_CGROUP_ROOT']).resolve()
        measure.require_cgroup_mount(root)
        scope=measure.create_scope(root)
        try:
            with tempfile.TemporaryDirectory() as temporary:
                ready=Path(temporary)/'ready'
                proceed=Path(temporary)/'proceed'
                child_code="""
import os,sys,time
from pathlib import Path
while not Path(sys.argv[2]).exists(): time.sleep(.02)
payload=bytearray(64*1024*1024)
for index in range(0,len(payload),4096): payload[index]=1
assert sum(range(2000000))==1999999000000
Path(sys.argv[1]).write_text(str(os.getpid()))
while True: time.sleep(.02)
"""
                parent_code='import subprocess,sys; subprocess.Popen([sys.executable,"-c",sys.argv[1],sys.argv[2],sys.argv[3]], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)'
                parent=subprocess.Popen(['/bin/sh','-c','printf "%s\\n" "$$" > "$1/cgroup.procs" || exit; shift; exec "$@"',
                                         'accounting-test',str(scope),sys.executable,'-c',parent_code,child_code,str(ready),str(proceed)])
                self.assertEqual(parent.wait(timeout=10),0)
                exited_parent_cpu=int(dict(line.split() for line in (scope/'cpu.stat').read_text().splitlines())['usage_usec'])
                proceed.touch()
                deadline=time.monotonic()+30
                while not ready.exists():
                    self.assertLess(time.monotonic(),deadline,'child did not publish readiness')
                    time.sleep(.02)
                pid=ready.read_text()
                self.assertIn(pid,(scope/'cgroup.procs').read_text().split())
                self.assertIn('populated 1',(scope/'cgroup.events').read_text())
                self.assertGreaterEqual(int((scope/'memory.current').read_text()),64*1024*1024)
                cpu=dict(line.split() for line in (scope/'cpu.stat').read_text().splitlines())
                self.assertGreater(int(cpu['usage_usec']),exited_parent_cpu)
                before=int(cpu['usage_usec'])
                (scope/'cgroup.kill').write_text('1')
                deadline=time.monotonic()+10
                while 'populated 1' in (scope/'cgroup.events').read_text():
                    self.assertLess(time.monotonic(),deadline,'owned child survived cgroup.kill')
                    time.sleep(.02)
                final=dict(line.split() for line in (scope/'cpu.stat').read_text().splitlines())
                self.assertGreaterEqual(int(final['usage_usec']),before)
                self.assertGreaterEqual(int((scope/'memory.peak').read_text()),64*1024*1024)
                self.assertEqual((scope/'cgroup.procs').read_text().strip(),'')
        finally:
            measure.cleanup_scope(scope)


if __name__=='__main__': unittest.main()

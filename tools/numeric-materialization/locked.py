import fcntl, subprocess, sys
with open('/tmp/jint-browser-improvements.measurement.lock','a') as lock:
    print('Queued for shared CPU lock', flush=True)
    fcntl.flock(lock, fcntl.LOCK_EX)
    print('Acquired shared CPU lock', flush=True)
    try:
        sys.exit(subprocess.call(sys.argv[1:]))
    finally:
        fcntl.flock(lock, fcntl.LOCK_UN)

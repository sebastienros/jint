#!/usr/bin/env python3
"""Observe the unchanged aggregate idle guard; never accept or run a benchmark."""
import argparse
import json
from pathlib import Path
import time

import measure


def audit(output, duration_seconds=1800):
    output.mkdir(parents=True, exist_ok=False)
    started = time.monotonic()
    summary = dict(mode='diagnostics-only; not benchmark acceptance', complete=False,
                   requestedDurationSeconds=duration_seconds, observations=0, refused=0,
                   totalTickDeltas={name: 0 for name in measure.HOST_CPU_CATEGORIES},
                   maximumBusyPercentOfOneCoreByCategory={name: None for name in measure.HOST_CPU_CATEGORIES})
    try:
        with (output/'observations.jsonl').open('x') as stream:
            while time.monotonic() - started < duration_seconds:
                observation = measure.complete_host_idle()
                observation['observedUtc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
                stream.write(json.dumps(observation) + '\n')
                stream.flush()
                summary['observations'] += 1
                summary['refused'] += not observation['accepted']
                for name in measure.HOST_CPU_CATEGORIES:
                    summary['totalTickDeltas'][name] += observation['tickDeltas'][name]
                    previous = summary['maximumBusyPercentOfOneCoreByCategory'][name]
                    value = observation['busyPercentOfOneCoreByCategory'][name]
                    summary['maximumBusyPercentOfOneCoreByCategory'][name] = value if previous is None else max(previous, value)
        summary['complete'] = True
    except BaseException as error:
        summary['failure'] = str(error)
        raise
    finally:
        summary['elapsedSeconds'] = time.monotonic() - started
        measure.write(output/'summary.json', summary)
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    audit(parser.parse_args().output)

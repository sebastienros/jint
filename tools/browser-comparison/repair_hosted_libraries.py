#!/usr/bin/env python3
"""Inspect library links and remove only the known unused hosted LLDB18 package defect."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROOTS = ('/lib', '/lib64', '/usr/lib', '/usr/lib64')
PACKAGE = 'python3-lldb-18'
ALLOWED_REMOVALS = {PACKAGE, 'lldb-18', 'lldb'}
KNOWN_LINKS = {
    '/usr/lib/llvm-18/lib/python3.12/site-packages/lldb/_lldb.cpython-312-x86_64-linux-gnu.so': '../../../liblldb.so',
    '/usr/lib/llvm-18/lib/python3/dist-packages/lldb/libLLVM-18.1.3.so.1': '../../../../../x86_64-linux-gnu/libLLVM-18.1.3.so.1',
    '/usr/lib/llvm-18/lib/python3/dist-packages/lldb/libLLVM-18.so.1': '../../../../../x86_64-linux-gnu/libLLVM-18.1.3.so.1',
}


def command(args):
    return subprocess.run(args, capture_output=True, text=True, env=dict(os.environ, LC_ALL='C'), timeout=300)


def logged_command(args, path):
    try:
        result = command(args)
        path.write_text(result.stdout + result.stderr)
        return result
    except subprocess.TimeoutExpired as error:
        def text(value):
            return value.decode(errors='replace') if isinstance(value, bytes) else value or ''
        path.write_text(text(error.stdout) + text(error.stderr))
        raise


def inventory():
    links = []
    def fail(error):
        raise error
    roots = sorted({str(Path(root).resolve(strict=True)) for root in ROOTS if Path(root).is_dir()})
    for root in roots:
        for directory, dirs, files in os.walk(root, onerror=fail):
            for name in sorted(dirs + files):
                path = Path(directory, name)
                if path.is_symlink() and not path.exists():
                    owner = command(['dpkg-query', '-S', str(path)])
                    links.append(dict(path=str(path), target=os.readlink(path), owner=owner.stdout,
                                      ownerError=owner.stderr, ownerExitCode=owner.returncode))
    return sorted(links, key=lambda item: item['path'])


def validate_links(links):
    for link in links:
        if KNOWN_LINKS.get(link['path']) != link['target']:
            raise ValueError(f'Unrecognized dangling library link: {link["path"]}')
        owners = []
        for line in link['owner'].splitlines():
            package, separator, path = line.rpartition(': ')
            if not separator or path != link['path']:
                raise ValueError('Unrecognized dpkg ownership output')
            owners.append(package.split(':')[0])
        if link['ownerExitCode'] != 0 or owners != [PACKAGE]:
            raise ValueError(f'Dangling link is not exclusively owned by {PACKAGE}: {link["path"]}')


def removal_plan(output):
    removed = set()
    for line in output.splitlines():
        if re.match(r'^(Inst|Conf)\s', line):
            raise ValueError('Repair must not install or configure packages')
        if line.startswith('Remv '):
            fields = line.split()
            if len(fields) < 2:
                raise ValueError('Malformed apt removal plan')
            package = fields[1].split(':')[0]
            if package in removed or package not in ALLOWED_REMOVALS:
                raise ValueError(f'Unexpected apt removal: {package}')
            removed.add(package)
    if PACKAGE not in removed:
        raise ValueError('Plan must remove the defective LLDB Python package')
    return removed


def packages():
    result = command(['dpkg-query', '-W', '-f=${binary:Package}\t${Version}\t${Status}\n'])
    result.check_returncode()
    installed = {}
    for line in result.stdout.splitlines():
        name, version, status = line.split('\t', 2)
        if status == 'install ok installed':
            installed[name] = version
    return installed


def verify_package_changes(before, after, planned):
    removed = set(before) - set(after)
    if {name.split(':')[0] for name in removed} != planned:
        raise ValueError('Actual removals differ from the reviewed simulation')
    if set(after) - set(before) or any(before[name] != after[name] for name in set(before) & set(after)):
        raise ValueError('Repair unexpectedly installed or changed another package')
    return sorted(removed)


def write(directory, name, value):
    (directory / name).write_text(json.dumps(value, indent=2) + '\n')


def run(output, inspect):
    output.mkdir(parents=True, exist_ok=False)
    record = dict(complete=False, inspectOnly=inspect, mutationAttempted=False)
    try:
        before = inventory()
        write(output, 'links-before.json', before)
        versions = packages()
        write(output, 'packages-before.json', versions)
        if inspect:
            record['complete'] = True
            return
        validate_links(before)
        if before:
            args = ['apt-get', 'remove', '--no-auto-remove', PACKAGE]
            simulation = logged_command([*args, '--simulate'], output / 'apt-simulation.log')
            simulation.check_returncode()
            planned = removal_plan(simulation.stdout)
            record['plannedRemovals'] = sorted(planned)
            record['mutationAttempted'] = True
            write(output, 'repair.json', record)
            actual = logged_command(['sudo', '-n', *args, '--assume-yes'], output / 'apt-removal.log')
            # Preserve post-state even when apt reports failure.
            after_versions = packages()
            write(output, 'packages-after.json', after_versions)
            after = inventory()
            write(output, 'links-after.json', after)
            actual.check_returncode()
            record['actualRemovals'] = verify_package_changes(versions, after_versions, planned)
        else:
            after = inventory()
            write(output, 'links-after.json', after)
            write(output, 'packages-after.json', packages())
        if after:
            raise ValueError('Dangling system library links remain after setup')
        record['complete'] = True
    except BaseException as error:
        record['failure'] = dict(type=type(error).__name__, message=str(error))
        raise
    finally:
        write(output, 'repair.json', record)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--inspect', action='store_true', help='Record links and installed packages without mutating the host')
    args = parser.parse_args()
    run(args.output, args.inspect)


if __name__ == '__main__':
    main()

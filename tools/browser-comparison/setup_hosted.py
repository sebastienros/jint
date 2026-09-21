#!/usr/bin/env python3
"""Install reviewed browser assets before collection; never resolve mutable versions."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import urllib.parse
import urllib.request
import zipfile


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def validate_pins(pins):
    for name in ('chromium', 'lightpanda'):
        pin = pins[name]
        if not re.fullmatch(r'[0-9a-f]{64}', pin['sha256']):
            raise ValueError(f'{name}: a reviewed SHA256 is required')
        if name == 'chromium' and not pin['version'].strip():
            raise ValueError(f'{name}: an exact version label is required')
        parsed = urllib.parse.urlparse(pin['url'])
        if parsed.scheme != 'https' or parsed.query or parsed.fragment or parsed.username:
            raise ValueError('Use an exact HTTPS release asset URL')
    chrome = pins['chromium']
    expected = f"https://storage.googleapis.com/chrome-for-testing-public/{chrome['version']}/linux64/chrome-linux64.zip"
    if chrome['url'] != expected:
        raise ValueError('Chromium must be a versioned official full Chrome for Testing linux64 asset')
    light = pins['lightpanda']
    if not isinstance(light['id'], int) or isinstance(light['id'], bool) or light['id'] <= 0:
        raise ValueError('Lightpanda release asset ID is required')
    if light['url'] != f"https://api.github.com/repos/lightpanda-io/browser/releases/assets/{light['id']}":
        raise ValueError('Lightpanda must use the exact official release asset ID URL')


def download(pin, path):
    request = urllib.request.Request(pin['url'], headers={'Accept': 'application/octet-stream', 'User-Agent': 'jint-hosted-comparison'})
    with urllib.request.urlopen(request, timeout=120) as response, path.open('wb') as output:
        if urllib.parse.urlparse(response.url).scheme != 'https':
            raise ValueError('Asset redirect must remain HTTPS')
        shutil.copyfileobj(response, output)
    actual = sha256(path)
    if actual != pin['sha256']:
        raise ValueError(f'Asset digest mismatch for {path.name}: expected {pin["sha256"]}, got {actual}')
    return actual


def extract_chrome(archive, destination):
    with zipfile.ZipFile(archive) as bundle:
        for item in bundle.infolist():
            path = Path(item.filename)
            mode = item.external_attr >> 16
            if path.is_absolute() or '..' in path.parts or stat.S_ISLNK(mode):
                raise ValueError(f'Unsafe archive entry: {item.filename}')
        bundle.extractall(destination)
        for item in bundle.infolist():
            if not item.is_dir():
                # Preserve executable bits without accepting privileged archive permissions.
                (destination / item.filename).chmod(0o755 if item.external_attr >> 16 & 0o111 else 0o644)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pins', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    pins = json.loads(args.pins.read_text())
    validate_pins(pins)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    record = {'complete': False, 'pins': pins, 'assets': {}, 'hostBoundary': 'GitHub-hosted Ubuntu 24.04 VM; physical host contention is not observable'}
    manifest = output / 'setup-manifest.json'
    try:
        for name, filename in [('chromium', 'chrome-linux64.zip'), ('lightpanda', 'lightpanda')]:
            target = output / filename
            actual = download(pins[name], target)
            record['assets'][name] = {'downloadSha256': actual, 'path': str(target)}
        extract_chrome(output / 'chrome-linux64.zip', output)
        chrome = output / 'chrome-linux64/chrome'
        lightpanda = output / 'lightpanda'
        lightpanda.chmod(0o755)
        env = dict(os.environ, LIGHTPANDA_DISABLE_TELEMETRY='true')
        for name, command in [('chromium', [str(chrome), '--version']), ('lightpanda', [str(lightpanda), 'version'])]:
            record['assets'][name]['executableSha256'] = sha256(Path(command[0]))
            record['assets'][name]['versionOutput'] = subprocess.check_output(command, env=env, text=True, timeout=30).strip()
        if pins['chromium']['version'] not in record['assets']['chromium']['versionOutput']:
            raise ValueError('Chrome version output does not match the reviewed pin')
        dotnet = Path(shutil.which('dotnet')).resolve()
        root = Path(__file__).resolve().parents[2]
        head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
        config = {'adapters': [
            {'name': 'jint', 'kind': 'jint', 'executable': str(dotnet), 'arguments': [str(root / 'artifacts/bin/Jint.Browser.Tool/release_net10.0/Jint.Browser.Tool.dll')], 'versionLabel': head + '; framework-dependent Release'},
            {'name': 'chromium', 'kind': 'chromium', 'executable': str(chrome), 'versionLabel': record['assets']['chromium']['versionOutput']},
            {'name': 'lightpanda', 'kind': 'lightpanda', 'executable': str(lightpanda), 'versionLabel': record['assets']['lightpanda']['versionOutput'], 'releaseAsset': {key: pins['lightpanda'][key] for key in ('id', 'url', 'sha256')}}]}
        (output / 'config.json').write_text(json.dumps(config, indent=2) + '\n')
        record['complete'] = True
    finally:
        manifest.write_text(json.dumps(record, indent=2) + '\n')


if __name__ == '__main__':
    main()

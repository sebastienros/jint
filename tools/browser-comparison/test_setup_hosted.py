import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import setup_hosted


class HostedSetupTests(unittest.TestCase):
    def setUp(self):
        self.pins = json.loads(Path(__file__).with_name('hosted-pins.json').read_text())

    def test_reviewed_official_pins_are_valid(self):
        setup_hosted.validate_pins(self.pins)

    def test_mutable_chrome_url_or_missing_digest_refused(self):
        for field, value in [('url', 'https://example.org/latest.zip'), ('sha256', 'unverified')]:
            pins = copy.deepcopy(self.pins)
            pins['chromium'][field] = value
            with self.assertRaises(ValueError):
                setup_hosted.validate_pins(pins)

    def test_lightpanda_asset_id_must_match_url(self):
        self.pins['lightpanda']['id'] += 1
        with self.assertRaises(ValueError):
            setup_hosted.validate_pins(self.pins)

    def test_archive_cannot_escape_destination(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'chrome.zip'
            for name in ('../escape', '/absolute'):
                with zipfile.ZipFile(archive, 'w') as bundle:
                    bundle.writestr(name, 'data')
                with self.assertRaises(ValueError):
                    setup_hosted.extract_chrome(archive, Path(directory) / 'browser')

    def test_archive_preserves_executable_without_special_permissions(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'chrome.zip'
            info = zipfile.ZipInfo('chrome-linux64/chrome')
            info.external_attr = 0o104755 << 16
            with zipfile.ZipFile(archive, 'w') as bundle:
                bundle.writestr(info, 'binary')
            setup_hosted.extract_chrome(archive, Path(directory) / 'browser')
            self.assertEqual(0o755, (Path(directory) / 'browser/chrome-linux64/chrome').stat().st_mode & 0o7777)

    def test_digest_mismatch_stops_before_execution(self):
        import io
        class Response(io.BytesIO):
            url = 'https://example.org/binary'
        with tempfile.TemporaryDirectory() as directory, patch('urllib.request.urlopen', return_value=Response(b'changed asset')):
            with self.assertRaisesRegex(ValueError, 'digest mismatch'):
                setup_hosted.download(self.pins['lightpanda'], Path(directory) / 'asset')


if __name__ == '__main__':
    unittest.main()

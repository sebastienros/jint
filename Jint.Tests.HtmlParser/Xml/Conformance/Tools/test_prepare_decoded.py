#!/usr/bin/env python3
"""Source-only checks for explicit offline Japanese input preparation."""

import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import import_corpus as corpus


class PreparedInputsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lock = json.loads((corpus.ROOT / "corpus.lock.json").read_bytes())
        cls.files = corpus.archive_files(corpus.verify(corpus.ARCHIVE, corpus.ARCHIVE_SHA))
        cls.rows = corpus.prepared_rows(cls.lock, cls.files)

    def test_explicit_offline_preparation_is_binary_identical_twice(self):
        with patch.object(corpus.urllib.request, "urlopen", side_effect=AssertionError("network used")):
            corpus.prepare_decoded()
            first = {
                key: (corpus.CACHE / "DecodedJapanese" / (row["rawSha256"] + ".utf8")).read_bytes()
                for key, row in self.rows.items()
            }
            corpus.prepare_decoded()
        for key, row in self.rows.items():
            self.assertEqual(first[key], (corpus.CACHE / "DecodedJapanese" / (row["rawSha256"] + ".utf8")).read_bytes())
            self.assertEqual(corpus.digest(first[key]), row["decodedUtf8Sha256"])
            self.assertFalse(first[key].startswith(b"\xef\xbb\xbf"))
            self.assertIn(b"\r\n", first[key])

    def test_strict_decode_and_reviewed_hashes_reject_changed_source(self):
        row = self.rows["xmlconf/japanese/japanese.xml#weekly-euc-jp"]
        raw = self.files[row["inputPath"]]
        with self.assertRaises(ValueError):
            corpus.prepare_text(row, raw + b"\x00")
        with self.assertRaises(ValueError):
            corpus.prepare_text(row, b"\xef\xbb\xbf" + raw)
        with self.assertRaises(UnicodeDecodeError):
            corpus.prepare_text({**row, "rawSha256": corpus.digest(raw + b"\x8f")}, raw + b"\x8f")
        text, encoded = corpus.prepare_text(row, raw)
        self.assertEqual(corpus.digest(encoded), row["decodedUtf8Sha256"])
        with self.assertRaises(ValueError):
            corpus.prepare_text({**row, "utf16Length": row["utf16Length"] + 1}, raw)
        with self.assertRaises(ValueError):
            corpus.prepare_text({**row, "declared": "UTF-8"}, raw)
        changed = raw.replace(b"\r\n", b"\n")
        self.assertNotEqual(text, changed.decode(row["codec"], errors="strict"))
        with self.assertRaises(ValueError):
            corpus.prepare_text({**row, "rawSha256": corpus.digest(changed)}, changed)

    def test_registry_rejects_missing_duplicate_and_stale_source(self):
        entries = list(self.rows.values())
        with self.assertRaises(ValueError):
            corpus.prepared_rows({**self.lock, "preparedInputsSha256": "0" * 64}, self.files)
        for changed in (entries[:-1], entries[:-1] + [entries[0]],
                        [{**entries[0], "rawSha256": entries[1]["rawSha256"]}] + entries[1:]):
            with self.subTest(changed=changed[-1]["key"]):
                with tempfile.TemporaryDirectory() as directory:
                    root = Path(directory)
                    metadata = json.dumps(changed).encode()
                    (root / "prepared-inputs.json").write_bytes(metadata)
                    lock = {**self.lock, "preparedInputsSha256": corpus.digest(metadata)}
                    with patch.object(corpus, "ROOT", root):
                        with self.assertRaises(ValueError):
                            corpus.prepared_rows(lock, self.files)


if __name__ == "__main__":
    unittest.main()

#!/usr/bin/env python3
"""Verify the vendored html5lib pin offline; --download restores exact upstream bytes."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "Jint.Tests.HtmlParser/Html/TreeConstruction/Corpus"


def verify(corpus=CORPUS, download=False):
    lock = json.loads((corpus / "corpus.lock.json").read_text())
    expected = lock["files"] | lock["metadata"]
    if download:
        url = f'https://codeload.github.com/html5lib/html5lib-tests/tar.gz/{lock["revision"]}'
        with urllib.request.urlopen(url, timeout=60) as response:
            archive = response.read(10_000_001)
        if len(archive) > 10_000_000:
            raise ValueError("Upstream archive exceeds 10 MB ceiling")
        prefix = f'html5lib-tests-{lock["revision"]}/'
        # No archive extraction: only named regular files, each validated before any write.
        replacements = {}
        with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as tar:
            for name, digest in expected.items():
                source = prefix + ("LICENSE" if name == "LICENSE" else "tree-construction/" + name)
                member = tar.getmember(source)
                if not member.isfile():
                    raise ValueError(f"Not a regular file: {source}")
                data = tar.extractfile(member).read()
                if hashlib.sha256(data).hexdigest() != digest:
                    raise ValueError(f"Upstream digest mismatch: {source}")
                replacements[name] = data
        for name, data in replacements.items():
            path = corpus / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    actual = {p.relative_to(corpus).as_posix() for p in corpus.rglob("*.dat")}
    if actual != set(lock["files"]):
        raise ValueError(f"Corpus file set differs: {actual ^ set(lock['files'])}")
    for name, digest in expected.items():
        if hashlib.sha256((corpus / name).read_bytes()).hexdigest() != digest:
            raise ValueError(f"Vendored digest mismatch: {name}")
    print(f"Verified {len(lock['files'])} html5lib .dat files at {lock['revision']}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="restore from the pinned upstream revision")
    parser.add_argument("--check", action="store_true", help="verify offline (default)")
    args = parser.parse_args()
    verify(download=args.download)

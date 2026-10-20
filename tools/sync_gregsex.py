#!/usr/bin/env python3
"""Vendor GregsEx's ECMAScript flavor into Jint/Runtime/RegExp/GregsEx.

    python3 tools/sync_gregsex.py                      # regenerate from the newest GregsEx main
    python3 tools/sync_gregsex.py --revision <sha-or-ref>

The output is the upstream files, the mechanical transforms below, then tools/gregsex-sync/jint.patch for what stays
Jint's own. Nothing records what was synced: an unchanged upstream regenerates identical files, so `git diff` is the
whole answer to "is there anything to take". A hand edit to a vendored file is overwritten by the next sync; put it in
the patch instead (Jint/Runtime/RegExp/GregsEx/README.md says how).
"""
import argparse
import io
import json
import os
import re
import subprocess
import sys
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "Jint/Runtime/RegExp/GregsEx"
PATCH = ROOT / "tools/gregsex-sync/jint.patch"
REPOSITORY = "sebastienros/gregsex"

# The ECMAScript closure, by upstream path under src/GregsEx (license files from the repository root are mapped).
FILES = [
    "Contracts/Budgets.cs",
    "Contracts/LaneContracts.cs",
    "Contracts/EcmaScript/EcmaCaseTables.cs",
    "Contracts/EcmaScript/EcmaProgram.cs",
    "Contracts/EcmaScript/LaneContracts.cs",
    "Execution/Backtracking/EcmaAccelerator.cs",
    "Execution/Backtracking/EcmaExecutor.cs",
    "Execution/Backtracking/EcmaExecutorFactory.cs",
    "Execution/Backtracking/EcmaPositionMatcher.cs",
    "Flavors/EcmaScript/Parsing/EcmaCompiler.cs",
    "Flavors/EcmaScript/Parsing/EscapesAndClasses.cs",
    "Flavors/EcmaScript/Parsing/ParserStorage.cs",
    "Flavors/EcmaScript/Parsing/ResolvedSet.cs",
    "Flavors/EcmaScript/Unicode/EcmaUnicodeData.cs",
    "Flavors/EcmaScript/Unicode/GeneratedUnicode17Tables.cs",
    "Flavors/EcmaScript/Unicode/Unicode-LICENSE.txt",
    "Public/EcmaScript/EcmaScriptProfile.cs",
    "Public/EcmaScript/EcmaScriptTypes.cs",
    "Public/RegexOptions.cs",
    "Public/RegexProfile.cs",
    "Public/RegexTypes.cs",
    "Public/UnicodeVersion.cs",
    "Text/ValueStringBuilder.cs",
]
ROOT_FILES = {"LICENSE": "GregsEx-LICENSE.txt"}

# Files under TARGET that are Jint's own and never come from upstream.
OWN = {".editorconfig", "README.md", "IR/SourceRange.cs"}

# Types from System.Threading the copied code names; Jint's implicit usings do not import the namespace.
THREADING = re.compile(r"\b(CancellationToken|Lock|Volatile|Interlocked)\b")


def transform(path, text):
    """The mechanical edits every vendored C# file receives."""
    if not path.endswith(".cs"):
        return text
    text = re.sub(r"^namespace GregsEx\b", "namespace Jint.Runtime.RegExp.GregsEx", text, flags=re.M)
    text = re.sub(r"^using (static )?GregsEx\b", r"using \1Jint.Runtime.RegExp.GregsEx", text, flags=re.M)
    text = re.sub(r"^(using \w+ = )GregsEx\.", r"\1Jint.Runtime.RegExp.GregsEx.", text, flags=re.M)
    # Top-level declarations only: nested members of an internal type may stay public.
    text = re.sub(r"^public ", "internal ", text, flags=re.M)
    if not text.startswith("#nullable"):
        text = "#nullable enable\n" + text
    if THREADING.search(text) and "using System.Threading;" not in text:
        text = text.replace("#nullable enable\n", "#nullable enable\nusing System.Threading;\n", 1)
    return text


def headers(extra=None):
    # Anonymous API calls share a 60-an-hour limit per address; a token (GITHUB_TOKEN in CI, GH_TOKEN from `gh auth
    # token` locally) lifts it. The token goes to github.com only.
    result = dict(extra or {})
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token:
        result["Authorization"] = f"Bearer {token}"
    return result


def resolve(revision):
    url = f"https://api.github.com/repos/{REPOSITORY}/commits/{revision}"
    request = urllib.request.Request(url, headers=headers({"Accept": "application/vnd.github+json"}))
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)["sha"]


def download(sha):
    url = f"https://codeload.github.com/{REPOSITORY}/tar.gz/{sha}"
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers()), timeout=120) as response:
        archive = response.read(200_000_001)
    if len(archive) > 200_000_000:
        raise ValueError("Upstream archive exceeds 200 MB ceiling")
    prefix = f"gregsex-{sha}/"
    wanted = {prefix + "src/GregsEx/" + f: f for f in FILES} | {prefix + f: t for f, t in ROOT_FILES.items()}
    files = {}
    # No archive extraction: only the named regular files are read.
    with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as tar:
        for source, target in wanted.items():
            member = tar.getmember(source)
            if not member.isfile():
                raise ValueError(f"Not a regular file: {source}")
            files[target] = tar.extractfile(member).read().decode("utf-8-sig").replace("\r\n", "\n")
    return files


def vendored():
    return {p.relative_to(TARGET).as_posix(): p for p in TARGET.rglob("*") if p.is_file()
            and "/obj/" not in p.as_posix() and "/bin/" not in p.as_posix()}


def sync(revision, patch=True):
    sha = resolve(revision)
    files = download(sha)
    for name, path in vendored().items():
        if name not in OWN:
            path.unlink()
    for name, text in files.items():
        out = TARGET / name
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(transform(name, text), newline="\n")
    if patch and PATCH.exists() and PATCH.stat().st_size > 0:
        result = subprocess.run(["git", "apply", "--whitespace=nowarn", str(PATCH)], cwd=ROOT,
                                capture_output=True, text=True)
        if result.returncode != 0:
            raise SystemExit("tools/gregsex-sync/jint.patch no longer applies to GregsEx " + sha[:12] +
                             ":\n" + result.stderr + "Resolve it by hand and regenerate the patch (see the README).")
    print(f"Generated {len(files)} files from GregsEx {sha}")
    return sha


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--revision", default="main", help="GregsEx commit or ref to sync to (default: main)")
    parser.add_argument("--no-patch", action="store_true",
                        help="write the transformed upstream files without jint.patch (to regenerate the patch)")
    args = parser.parse_args()
    sync(args.revision, patch=not args.no_patch)

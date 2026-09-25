#!/usr/bin/env python3
"""Restore and inventory the pinned W3C XML test corpus. Never called by tests.

The metadata parser resolves only the 23 pinned catalog/DTD files listed below.
It never opens test documents through an XML resolver.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import posixpath
import re
import sys
import tarfile
import tempfile
import urllib.request
import xml.parsers.expat as expat
import zipfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "Cache"
VENDOR = ROOT / "Vendor"
ARCHIVE = CACHE / "xmlts20130923.tar.gz"
CLARK_ZIP = VENDOR / "xmltest.zip"
ARCHIVE_URL = "https://www.w3.org/XML/Test/xmlts20130923.tar.gz"
CLARK_URL = "ftp://ftp.jclark.com/pub/xml/xmltest.zip"
ARCHIVE_SHA = "9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f"
CLARK_SHA = "a919d7142fe6f72af51fc796b4df40732f385c9eb313b8993c6d39cc92acc410"

METADATA_PATHS = frozenset("""
xmlconf/xmlconf.xml
xmlconf/testcases.dtd
xmlconf/xmltest/xmltest.xml
xmlconf/japanese/japanese.xml
xmlconf/sun/sun-valid.xml
xmlconf/sun/sun-invalid.xml
xmlconf/sun/sun-not-wf.xml
xmlconf/sun/sun-error.xml
xmlconf/oasis/oasis.xml
xmlconf/ibm/ibm_oasis_invalid.xml
xmlconf/ibm/ibm_oasis_not-wf.xml
xmlconf/ibm/ibm_oasis_valid.xml
xmlconf/ibm/xml-1.1/ibm_invalid.xml
xmlconf/ibm/xml-1.1/ibm_not-wf.xml
xmlconf/ibm/xml-1.1/ibm_valid.xml
xmlconf/eduni/errata-2e/errata2e.xml
xmlconf/eduni/xml-1.1/xml11.xml
xmlconf/eduni/namespaces/1.0/rmt-ns10.xml
xmlconf/eduni/namespaces/1.1/rmt-ns11.xml
xmlconf/eduni/errata-3e/errata3e.xml
xmlconf/eduni/errata-4e/errata4e.xml
xmlconf/eduni/namespaces/errata-1e/errata1e.xml
xmlconf/eduni/misc/ht-bh.xml
""".split())

VENDORED_PREFIXES = (
    "xmlconf/eduni/errata-2e/",
    "xmlconf/eduni/errata-3e/",
    "xmlconf/eduni/errata-4e/",
    "xmlconf/eduni/namespaces/1.0/",
    "xmlconf/eduni/namespaces/errata-1e/",
)

# The root 20130923 catalog says eduni/namespaces/misc/, while the archive
# stores all nine input files beside eduni/misc/ht-bh.xml. Keep both spellings.
MISC_INPUT_IDS = frozenset(
    [f"hst-bh-{i:03}" for i in range(1, 7)]
    + [f"hst-lhs-{i:03}" for i in range(7, 10)]
)

JAPANESE_NAMES = (
    "pr-xml-euc-jp", "pr-xml-iso-2022-jp", "pr-xml-shift_jis",
    "weekly-euc-jp", "weekly-iso-2022-jp", "weekly-shift_jis",
)
JAPANESE_KEYS = frozenset("xmlconf/japanese/japanese.xml#" + name for name in JAPANESE_NAMES)
JAPANESE_CODECS = {
    "euc-jp": ("euc_jp", "prepared-euc-jp"),
    "iso-2022-jp": ("iso2022_jp", "prepared-iso-2022-jp"),
    "shift_jis": ("shift_jis", "prepared-shift-jis"),
}
HEX_SHA = re.compile(r"[0-9a-f]{64}\Z")
DECLARATION = re.compile(r"<\?xml\s+[^?]*?\bencoding\s*=\s*(['\"])([^'\"]+)\1", re.I)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def verify(path: Path, expected: str) -> bytes:
    if not path.is_file():
        raise SystemExit(f"Missing pinned corpus: {path}. Run Tools/import_corpus.py restore first.")
    data = path.read_bytes()
    actual = digest(data)
    if actual != expected:
        raise SystemExit(f"Pinned corpus digest mismatch for {path}: {actual}")
    return data


def restore_one(url: str, path: Path, expected: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".download")
    try:
        with urllib.request.urlopen(url, timeout=120) as response, temporary.open("wb") as target:
            while block := response.read(1024 * 1024):
                target.write(block)
        verify(temporary, expected)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def archive_files(data: bytes) -> dict[str, bytes]:
    import io

    files = {}
    with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as archive:
        for member in archive:
            name = member.name
            if name.startswith("/") or ".." in name.split("/") or "\\" in name:
                raise ValueError(f"Unsafe archive path: {name}")
            if member.isdir():
                continue
            if not member.isfile() or name in files:
                raise ValueError(f"Unsafe or duplicate archive member: {name}")
            stream = archive.extractfile(member)
            if stream is None:
                raise ValueError(f"Missing archive member: {name}")
            files[name] = stream.read()
    return files


def prepared_rows(lock: dict, files: dict[str, bytes]) -> dict[str, dict]:
    metadata = (ROOT / "prepared-inputs.json").read_bytes()
    if digest(metadata) != lock.get("preparedInputsSha256"):
        raise ValueError("Prepared input metadata digest mismatch")
    entries = json.loads(metadata)
    if len(entries) != 6 or {row["key"] for row in entries} != JAPANESE_KEYS:
        raise ValueError("Prepared input key registry drift or duplicate")
    locked = {row["path"]: row["sha256"] for row in lock["files"]}
    if len(locked) != len(lock["files"]):
        raise ValueError("Duplicate source member lock")
    result = {}
    for row in entries:
        name = row["key"].split("#", 1)[1]
        suffix = name.removeprefix("pr-xml-").removeprefix("weekly-")
        label = "Shift_JIS" if name == "weekly-shift_jis" else suffix
        path = "xmlconf/japanese/" + name + ".xml"
        if set(row) != {"key", "inputPath", "rawSha256", "codec", "decodedUtf8Sha256", "utf16Length", "declared", "decision"} or \
                row["inputPath"] != path or row["declared"] != label or \
                (row["codec"], row["decision"]) != JAPANESE_CODECS[label.lower()] or \
                not all(isinstance(row[field], str) and HEX_SHA.fullmatch(row[field])
                        for field in ("rawSha256", "decodedUtf8Sha256")) or \
                not isinstance(row["utf16Length"], int) or row["utf16Length"] <= 0 or \
                row["rawSha256"] != locked.get(path) or digest(files[path]) != row["rawSha256"]:
            raise ValueError(f"Prepared input identity drift: {row['key']}")
        result[row["key"]] = row
    return result


def prepare_text(row: dict, raw: bytes) -> tuple[str, bytes]:
    if digest(raw) != row["rawSha256"] or raw.startswith((b"\xef\xbb\xbf", b"\xfe\xff", b"\xff\xfe")) or \
            raw.startswith((b"\x00\x3c", b"\x3c\x00")):
        raise ValueError(f"Prepared raw input signature/hash mismatch: {row['key']}")
    text = raw.decode(row["codec"], errors="strict")
    declaration = DECLARATION.match(text)
    if not declaration or declaration.group(2) != row["declared"] or text.startswith("\ufeff"):
        raise ValueError(f"Prepared declaration mismatch: {row['key']}")
    encoded = text.encode("utf-8", errors="strict")
    if digest(encoded) != row["decodedUtf8Sha256"] or \
            len(text.encode("utf-16-le", errors="strict")) // 2 != row["utf16Length"]:
        raise ValueError(f"Prepared decoded digest/length mismatch: {row['key']}")
    return text, encoded


def prepare_decoded() -> None:
    lock = json.loads((ROOT / "corpus.lock.json").read_bytes())
    if lock.get("archiveSha256") != ARCHIVE_SHA or lock.get("clarkZipSha256") != CLARK_SHA:
        raise ValueError("Prepared archive pin drift")
    verify(CLARK_ZIP, CLARK_SHA)
    files = archive_files(verify(ARCHIVE, ARCHIVE_SHA))
    if len(files) != lock["fileCount"] or len(lock["files"]) != lock["fileCount"]:
        raise ValueError("Prepared source archive census mismatch")
    rows = prepared_rows(lock, files)
    destination_dir = CACHE / "DecodedJapanese"
    destination_dir.mkdir(parents=True, exist_ok=True)
    for row in rows.values():
        _, encoded = prepare_text(row, files[row["inputPath"]])
        destination = destination_dir / (row["rawSha256"] + ".utf8")
        temporary: Path | None = None
        try:
            with tempfile.NamedTemporaryFile(dir=destination_dir, prefix=".prepared-", delete=False) as output:
                temporary = Path(output.name)
                output.write(encoded)
            if temporary.read_bytes() != encoded or digest(temporary.read_bytes()) != row["decodedUtf8Sha256"]:
                raise ValueError(f"Prepared output write mismatch: {row['key']}")
            os.replace(temporary, destination)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    print(f"Prepared {len(rows)} exact Japanese inputs with Python {sys.version.split()[0]}")


def catalog_rows(files: dict[str, bytes]) -> tuple[list[dict], list[str]]:
    rows: list[dict] = []
    opened: set[str] = set()
    base_stack = ["xmlconf/"]
    test_stack: list[dict] = []

    def parse(path: str, parser: expat.xmlparser | None = None) -> None:
        if path not in METADATA_PATHS or path not in files:
            raise ValueError(f"Catalog resource denied: {path}")
        opened.add(path)
        current = parser or expat.ParserCreate()
        current.SetParamEntityParsing(expat.XML_PARAM_ENTITY_PARSING_ALWAYS)

        def start(name: str, attributes: dict[str, str]) -> None:
            base = base_stack[-1]
            if "xml:base" in attributes:
                base = posixpath.normpath(posixpath.join(base, attributes["xml:base"])) + "/"
            base_stack.append(base)
            if name == "TEST":
                row = {"catalog": path, "metadata": dict(attributes), "base": base, "description": ""}
                rows.append(row)
                test_stack.append(row)

        def end(name: str) -> None:
            if name == "TEST":
                test_stack.pop()
            base_stack.pop()

        def characters(data: str) -> None:
            if test_stack:
                test_stack[-1]["description"] += data

        def external(context: str, _base: str, system_id: str, _public_id: str) -> int:
            if not system_id or ":" in system_id or system_id.startswith(("/", "\\")) or "\\" in system_id:
                raise ValueError(f"Catalog URI denied: {system_id}")
            child = posixpath.normpath(posixpath.join(posixpath.dirname(path), system_id))
            if ".." in child.split("/"):
                raise ValueError(f"Catalog traversal denied: {system_id}")
            parse(child, current.ExternalEntityParserCreate(context))
            return 1

        current.StartElementHandler = start
        current.EndElementHandler = end
        current.CharacterDataHandler = characters
        current.ExternalEntityRefHandler = external
        current.Parse(files[path], True)

    parse("xmlconf/xmlconf.xml")
    if opened != METADATA_PATHS:
        raise ValueError(f"Catalog graph drift: {sorted(opened ^ METADATA_PATHS)}")
    if len(rows) != 2585:
        raise ValueError(f"Catalog row count drift: {len(rows)}")
    return rows, sorted(opened)


def decode_document(data: bytes) -> tuple[str | None, dict[str, str]]:
    # XML §4.3.3 signatures; strict fallbacks ensure invalid byte input never
    # turns into U+FFFD and a false parser rejection.
    if data.startswith(b"\xef\xbb\xbf"):
        encoding, payload, decision = "utf-8", data[3:], "utf-8-bom"
    elif data.startswith(b"\xfe\xff"):
        encoding, payload, decision = "utf-16-be", data[2:], "utf-16-be-bom"
    elif data.startswith(b"\xff\xfe"):
        encoding, payload, decision = "utf-16-le", data[2:], "utf-16-le-bom"
    elif data.startswith(b"\x00\x3c\x00\x3f") or data.startswith(b"\x00\x3c\x00"):
        encoding, payload, decision = "utf-16-be", data, "utf-16-be-signature"
    elif data.startswith(b"\x3c\x00\x3f\x00") or data.startswith(b"\x3c\x00"):
        encoding, payload, decision = "utf-16-le", data, "utf-16-le-signature"
    else:
        # ISO-8859-1 is an explicit, fully specified adapter for the one
        # legacy encoding used by selected namespace fixtures. The ASCII
        # declaration prefix can be inspected before decoding the body.
        header = re.match(rb"<\?xml\s+[^?]*?\bencoding\s*=\s*(['\"])([^'\"]+)\1", data[:512], re.I)
        declared_bytes = header.group(2).lower() if header else b""
        if declared_bytes == b"iso-8859-1":
            encoding, payload, decision = "iso-8859-1", data, "iso-8859-1-declaration"
        else:
            encoding, payload, decision = "utf-8", data, "utf-8-default"
    try:
        source = payload.decode(encoding, errors="strict")
    except UnicodeDecodeError as error:
        return None, {"decision": decision, "status": "strict-decode-error", "detail": str(error)}
    declaration = re.match(r"<\?xml\s+[^?]*?\bencoding\s*=\s*(['\"])([^'\"]+)\1", source, re.I)
    declared = declaration.group(2) if declaration else None
    if declared:
        # EncName grammar defects are lexical parser tests on the decoded string,
        # not reasons to suppress the parser invocation.
        if re.fullmatch(r"[A-Za-z][A-Za-z0-9._-]*", declared) is None:
            return source, {"decision": decision, "status": "decoded", "declared": declared}
        normalized = declared.lower().replace("_", "-")
        supported = ("utf-8", "utf8") if encoding == "utf-8" else (
            ("iso-8859-1",) if encoding == "iso-8859-1" else ("utf-16", encoding)
        )
        if normalized not in supported and normalized != "ascii":
            return None, {"decision": decision, "status": "declared-encoding-review", "declared": declared}
    return source, {"decision": decision, "status": "decoded", "declared": declared or ""}


def in_profile(metadata: dict[str, str]) -> tuple[bool, str]:
    recommendation = metadata.get("RECOMMENDATION", "XML1.0")
    if recommendation.startswith(("XML1.1", "NS1.1")):
        return False, "different-recommendation"
    if not recommendation.startswith(("XML1.0", "NS1.0")):
        raise ValueError(f"Unknown recommendation: {recommendation}")
    if metadata.get("VERSION") and "1.0" not in metadata["VERSION"].split():
        return False, "different-xml-version"
    if metadata.get("EDITION") and "5" not in metadata["EDITION"].split():
        return False, "different-edition"
    if metadata.get("NAMESPACE", "yes") == "no":
        return False, "namespace-disabled-case"
    if metadata.get("NAMESPACE", "yes") != "yes":
        raise ValueError(f"Unknown NAMESPACE value: {metadata['NAMESPACE']}")
    return True, ""


def import_corpus() -> None:
    archive_data = verify(ARCHIVE, ARCHIVE_SHA)
    zip_data = verify(CLARK_ZIP, CLARK_SHA)
    files = archive_files(archive_data)
    previous_lock = json.loads((ROOT / "corpus.lock.json").read_bytes())
    japanese = prepared_rows(previous_lock, files)
    used_japanese: set[str] = set()
    import io

    with zipfile.ZipFile(io.BytesIO(zip_data)) as original:
        zip_files = {"xmlconf/" + name: original.read(name) for name in original.namelist() if not name.endswith("/")}
    rows, metadata_paths = catalog_rows(files)
    boundary_entries = json.loads((ROOT / "input-boundary.json").read_text())
    boundary = {item["key"]: item for item in boundary_entries}
    if len(boundary_entries) != 18 or len(boundary) != 18:
        raise ValueError("Reviewed byte-boundary table must name exactly 18 distinct cases")
    used_boundary = set()
    if Counter(row["metadata"]["TYPE"] for row in rows) != Counter({"valid": 812, "invalid": 242, "not-wf": 1498, "error": 33}):
        raise ValueError("Upstream category census drift")

    for path, data in files.items():
        if path.startswith(VENDORED_PREFIXES):
            destination = VENDOR / "Edinburgh" / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists() and destination.read_bytes() != data:
                raise ValueError(f"Vendored byte drift: {destination}")
            destination.write_bytes(data)

    file_lock = []
    for path, data in sorted(files.items()):
        source = "edinburgh-vendor" if path.startswith(VENDORED_PREFIXES) else (
            "unchanged-clark-zip" if path in zip_files and data == zip_files[path] else "verified-cache"
        )
        file_lock.append({"path": path, "sha256": digest(data), "source": source})

    changed_clark = [
        {"path": path, "w3cSha256": digest(files[path]), "originalSha256": digest(zip_files[path])}
        for path in sorted(files.keys() & zip_files.keys())
        if path.startswith("xmlconf/xmltest/") and files[path] != zip_files[path]
    ]
    added_clark = [
        {"path": path, "w3cSha256": digest(files[path])}
        for path in sorted(files.keys() - zip_files.keys()) if path.startswith("xmlconf/xmltest/")
    ]
    original_only_clark = [
        {"path": path, "originalSha256": digest(zip_files[path])}
        for path in sorted(zip_files.keys() - files.keys())
    ]
    if (len(changed_clark), len(added_clark)) != (9, 13):
        raise ValueError("Clark/W3C delta census drift")

    cases = []
    keys = set()
    for row in rows:
        attributes = row["metadata"]
        case_id = attributes["ID"]
        key = (row["catalog"], case_id)
        if key in keys:
            raise ValueError(f"Duplicate (catalog, ID): {key}")
        keys.add(key)
        stable_key = row["catalog"] + "#" + case_id
        uri = attributes["URI"]
        if uri.startswith("/") or ":" in uri or "\\" in uri or ".." in uri.split("/"):
            raise ValueError(f"Unsafe test URI: {uri}")
        input_path = posixpath.normpath(posixpath.join(row["base"], uri))
        if case_id in MISC_INPUT_IDS:
            if row["catalog"] != "xmlconf/eduni/misc/ht-bh.xml":
                raise ValueError(f"Unexpected misc ID reuse: {key}")
            input_path = "xmlconf/eduni/misc/" + uri
        if input_path not in files:
            raise ValueError(f"Missing input {key}: {input_path}")
        output_uri = attributes.get("OUTPUT")
        output_path = posixpath.normpath(posixpath.join(row["base"], output_uri)) if output_uri else None
        if output_path is not None and output_path not in files:
            raise ValueError(f"Missing OUTPUT {key}: {output_path}")
        output3_uri = attributes.get("OUTPUT3")
        output3_path = posixpath.normpath(posixpath.join(row["base"], output3_uri)) if output3_uri else None
        if output3_path is not None and output3_path not in files:
            raise ValueError(f"Missing OUTPUT3 {key}: {output3_path}")
        profile, reason = in_profile(attributes)
        if stable_key in JAPANESE_KEYS:
            reviewed = japanese[stable_key]
            if input_path != reviewed["inputPath"]:
                raise ValueError(f"Prepared catalog input path drift: {stable_key}")
            source_text, _ = prepare_text(reviewed, files[input_path])
            decoder = {"decision": reviewed["decision"], "status": "decoded", "declared": reviewed["declared"]}
            used_japanese.add(stable_key)
        else:
            source_text, decoder = decode_document(files[input_path])
        signals = []
        if attributes.get("ENTITIES", "none") != "none":
            signals.append("ENTITIES=" + attributes["ENTITIES"])
        if source_text is not None and "<!DOCTYPE" in source_text and (
            "SYSTEM" in source_text or "PUBLIC" in source_text
        ):
            signals.append("external-identifier-lexical")
        if not profile:
            disposition = "outside-profile"
        elif attributes["TYPE"] == "error":
            disposition = "optional-error-review"
        elif decoder["status"] != "decoded":
            if stable_key in boundary:
                disposition = "outside-input-boundary"
                reason = boundary[stable_key]["reason"]
                used_boundary.add(stable_key)
            else:
                disposition = "input-boundary-review"
        else:
            disposition = "runnable"
        cases.append({
            "key": stable_key,
            "catalog": row["catalog"], "collection": row["catalog"].removeprefix("xmlconf/").rsplit("/", 1)[0],
            "id": case_id, "uri": uri, "inputPath": input_path,
            "description": row["description"], "sections": attributes["SECTIONS"],
            "category": attributes["TYPE"], "recommendation": attributes.get("RECOMMENDATION", "XML1.0"),
            "version": attributes.get("VERSION"), "edition": attributes.get("EDITION"),
            "namespace": attributes.get("NAMESPACE", "yes"), "entities": attributes.get("ENTITIES", "none"),
            "output": output_uri, "outputPath": output_path,
            "output3": output3_uri, "output3Path": output3_path,
            "decoding": decoder, "disposition": disposition, "reason": reason,
            "boundaryByteOffset": boundary[stable_key]["byteOffset"] if stable_key in used_boundary else None,
            "resourceProfile": "unreviewed-external-indication" if signals else "no-external-indication",
            "resourceSignals": signals,
        })

    if used_boundary != boundary.keys():
        raise ValueError(f"Stale or unapplied byte-boundary decisions: {sorted(used_boundary ^ boundary.keys())}")
    if used_japanese != JAPANESE_KEYS:
        raise ValueError(f"Prepared catalog row drift: {sorted(used_japanese ^ JAPANESE_KEYS)}")

    case_json = json.dumps(cases, ensure_ascii=False, indent=2) + "\n"
    lock = {
        "suite": "W3C XML Test Suite 20130923", "archiveUrl": ARCHIVE_URL, "archiveSha256": ARCHIVE_SHA,
        "clarkUrl": CLARK_URL, "clarkZipSha256": CLARK_SHA,
        "metadataPaths": metadata_paths, "fileCount": len(files), "rowCount": len(cases),
        "casesSha256": digest(case_json.encode("utf-8")),
        "preparedInputsSha256": digest((ROOT / "prepared-inputs.json").read_bytes()),
        "categories": dict(sorted(Counter(case["category"] for case in cases).items())),
        "clarkChanged": changed_clark, "clarkAdded": added_clark,
        "clarkOriginalOnly": original_only_clark,
        "files": file_lock,
    }
    (ROOT / "corpus.lock.json").write_text(json.dumps(lock, ensure_ascii=False, indent=2) + "\n")
    (ROOT / "cases.json").write_text(case_json)
    print("Rows:", len(cases), "files:", len(files))
    print("Dispositions:", dict(Counter(case["disposition"] for case in cases)))
    print("File sources:", dict(Counter(entry["source"] for entry in file_lock)))


if __name__ == "__main__":
    command = argparse.ArgumentParser(description=__doc__)
    command.add_argument("action", choices=("restore", "import", "prepare-decoded"))
    arguments = command.parse_args()
    if arguments.action == "restore":
        restore_one(ARCHIVE_URL, ARCHIVE, ARCHIVE_SHA)
        restore_one(CLARK_URL, CLARK_ZIP, CLARK_SHA)
        print("Pinned archives restored and verified")
        prepare_decoded()
    elif arguments.action == "prepare-decoded":
        prepare_decoded()
    else:
        import_corpus()

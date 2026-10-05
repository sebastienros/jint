#!/usr/bin/env python3
"""Check package contents, exact Browser pin, and fresh-feed restore provenance."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
FEED = ROOT / "artifacts/html-parser-package-consumer/feed"
CONSUMER = ROOT / "tools/html-parser-package-consumer"
IDS = ("Jint", "Jint.DevTools", "Jint.HtmlParser", "Jint.Browser")


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def packages():
    result = {}
    for path in FEED.glob("*.nupkg"):
        with zipfile.ZipFile(path) as package:
            nuspec = next(name for name in package.namelist() if name.endswith(".nuspec"))
            metadata = ET.fromstring(package.read(nuspec))
            # Nuspec namespaces vary with the pack SDK.
            for element in metadata.iter():
                element.tag = element.tag.split("}")[-1]
            package_id = metadata.findtext("metadata/id")
            require(package_id not in result, f"Duplicate package: {package_id}")
            result[package_id] = (path, metadata, set(package.namelist()))
    require(set(result) == set(IDS), f"Unexpected feed contents: {set(result)}")
    return result


def verify_feed(packed, version, frameworks):
    parser_version = packed["Jint.HtmlParser"][1].findtext("metadata/version")
    expected_parser = version if "-" in version else version + "-experimental-0"
    require(parser_version == expected_parser, f"Parser version policy changed: {parser_version}")
    for package_id, (_, metadata, contents) in packed.items():
        expected = expected_parser if package_id == "Jint.HtmlParser" else version
        require(metadata.findtext("metadata/version") == expected, f"Wrong version: {package_id}")
        for framework in frameworks:
            require(f"lib/{framework}/{package_id}.dll" in contents, f"Missing {package_id}/{framework} asset")
    _, browser, _ = packed["Jint.Browser"]
    for framework in frameworks:
        group = browser.find(f"metadata/dependencies/group[@targetFramework='{framework}']")
        require(group is not None, f"Missing Browser dependency group: {framework}")
        dependency = group.find("dependency[@id='Jint.HtmlParser']")
        require(dependency is not None and dependency.get("version") == f"[{parser_version}]",
                f"Browser must pin the actual parser version for {framework}")
    _, parser, contents = packed["Jint.HtmlParser"]
    require("README.md" in contents and "licenses/ValueStringBuilder.LICENSE.txt" in contents,
            "Parser package documentation/license missing")
    require({item.get("id") for item in parser.findall("metadata/dependencies/group/dependency")} == {"System.IO.Hashing"},
            "Parser package dependencies changed")
    return parser_version


def verify_restore(packed, frameworks, browser):
    assets = json.loads((CONSUMER / "obj/project.assets.json").read_text())
    expected_ids = set(IDS) if browser else {"Jint.HtmlParser"}
    actual_ids = {key.split("/")[0] for key in assets["libraries"] if key.split("/")[0].startswith("Jint")}
    require(actual_ids == expected_ids, f"Wrong restored Jint graph: {actual_ids}")
    for package_id in expected_ids:
        package, metadata, _ = packed[package_id]
        version = metadata.findtext("metadata/version")
        key = f"{package_id}/{version}"
        require(key in assets["libraries"], f"Wrong restored version: {package_id}")
        for framework in frameworks:
            require(key in assets["targets"][framework], f"Missing restored {package_id}/{framework}")
            runtime = assets["targets"][framework][key].get("runtime", {})
            require(f"lib/{framework}/{package_id}.dll" in runtime, f"Wrong runtime asset: {package_id}/{framework}")
        library = assets["libraries"][key]
        found = False
        for folder in assets["packageFolders"]:
            provenance_path = Path(folder) / library["path"] / ".nupkg.metadata"
            if not provenance_path.exists():
                continue
            provenance = json.loads(provenance_path.read_text())
            require(Path(provenance["source"]).resolve() == FEED.resolve(), f"Non-local source: {package_id}")
            digest = base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
            require(provenance["contentHash"] == digest, f"Stale package cache: {package_id}")
            found = True
        require(found, f"No restore provenance: {package_id}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("feed", "restore"))
    parser.add_argument("--version", required=True)
    parser.add_argument("--framework", action="append", required=True)
    parser.add_argument("--browser", action="store_true")
    args = parser.parse_args()
    packed = packages()
    parser_version = verify_feed(packed, args.version, args.framework)
    if args.stage == "feed":
        print(f"PARSER_PACKAGE_VERSION={parser_version}")
        print(f"BROWSER_PACKAGE_VERSION={args.version}")
    else:
        verify_restore(packed, args.framework, args.browser)
        print("PACKED PACKAGE PROVENANCE VERIFIED")


if __name__ == "__main__":
    main()

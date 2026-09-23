#!/usr/bin/env python3
"""Lock the current AngleSharp migration surface; no NuGet restore is needed."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
LOCK = Path(__file__).with_name("inventory.lock.json")
IGNORE_PARTS = {".git", "bin", "obj", "artifacts", "Vendor", "__pycache__"}
ANGLE = re.compile(r"\bAngleSharp\b")
SHAPE = re.compile(r"private static .*? Build([A-Za-z0-9_]+)\(\)")
MEMBER = re.compile(r'\.(Method|Accessor|Constant|PerRealmSlot)\(\s*(?:"([^"]+)"|global::Jint\.Native\.Symbol\.GlobalSymbolRegistry\.(\w+))')
REGISTRY = re.compile(r"internal static readonly DomInterfaceDefinition ([A-Za-z0-9_]+);")
CENSUS = re.compile(r"^\| (`[^`]+/`|\*\*total\*\*) \| ([\d,*]+) \| ([\d,*]+) \| ([\d,*]+) \| ([\d,*]+) \|$")


def digest(data: bytes) -> str:
    # Git may check text out with CRLF on Windows. Hash the same logical lines
    # everywhere while still detecting an added or changed source line.
    return hashlib.sha256(data.replace(b"\r\n", b"\n")).hexdigest()


def category(path: str) -> str:
    if path.startswith(("Jint.Tests.Browser/", "Jint.Tests/")):
        return "test"
    if path.startswith(("Jint.Benchmark/", "tools/browser-comparison/")):
        return "benchmark"
    if path.startswith("tools/dom-bindings/"):
        return "generator"
    return "runtime"


def owner(path: str) -> str:
    if path in {"Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"}:
        return "G2:dependency-removal"
    if path.startswith(("Jint.Benchmark/", "tools/browser-comparison/")):
        return "A3:comparison"
    if path.startswith("tools/dom-bindings/"):
        return "B1:binding-emitter"
    if path.startswith("Jint.Tests.Browser/"):
        if path.startswith("Jint.Tests.Browser/Wpt/"):
            return "G1:browser-wpt-gate"
        if path.startswith("Jint.Tests.Browser/Forms/"):
            return "B2:form-controls"
        if path.startswith("Jint.Tests.Browser/Parsing/"):
            return "R1:parser-coordinator"
        if path.startswith("Jint.Tests.Browser/Views/"):
            return "B5:css-binding"
        if "DomBindingsPinTests" in path or "DomBindingsStalenessTests" in path:
            return "B1:binding-emitter"
        path = path.replace("Jint.Tests.Browser/", "Jint.Browser/", 1)
    elif path.startswith("Jint.Tests/"):
        return "G1:shared-wpt-gate"
    if path.startswith("Jint.Browser/Dom/Views/"):
        if any(part in path for part in ("XPath", "DomParser", "Xml")):
            return "B4:detached-document"
        if any(part in path for part in ("Css", "Style", "Media")):
            return "B5:css-binding"
        return "B1:dom-binding"
    if path.startswith("Jint.Browser/Dom/Collections/"):
        return "B2:collections"
    if path.startswith("Jint.Browser/Dom/"):
        if any(part in path for part in ("Form", "Select", "Label", "Files")):
            return "B2:form-controls"
        if any(part in path for part in ("Frame", "Template")):
            return "R3:frames"
        if any(part in path for part in ("Selector",)):
            return "C3:selectors"
        return "B1:dom-binding"
    if path.startswith("Jint.Browser/Runtime/Parsing/"):
        if "PseudoClass" in path:
            return "C3:selectors"
        if "Styling" in path:
            return "R3:styles"
        if "ResourceLoader" in path:
            return "R3:resources"
        if "Module" in path or "Scripting" in path:
            return "R2:scripts"
        return "R1:parser-coordinator"
    if path.startswith("Jint.Browser/Runtime/"):
        if "Frame" in path:
            return "R3:frames"
        if "Form" in path:
            return "B2:form-controls"
        return "R4:page-runtime"
    for prefix, task in (
        ("Jint.Browser/CustomElements/", "B3:custom-elements"),
        ("Jint.Browser/Events/", "B3:events"),
        ("Jint.Browser/Observers/", "B3:observers"),
        ("Jint.Browser/Media/", "R3:images"),
        ("Jint.Browser/Accessibility/", "R4:accessibility"),
        ("Jint.Browser/Extraction/", "R4:extraction"),
        ("Jint.Browser/Layout/", "R4:layout"),
        ("Jint.Browser/DevTools/", "R4:protocol"),
        ("Jint.Browser.Tool/", "G1:tool-parity"),
        ("Jint.Browser.Mcp/", "G1:mcp-parity"),
        ("Jint.DevTools/", "R4:protocol"),
    ):
        if path.startswith(prefix):
            return task
    if path.startswith("Jint.Browser/"):
        if path.endswith("Jint.Browser.csproj"):
            return "G2:dependency-removal"
        return "R4:page-api"
    raise ValueError(f"No owner for {path}")


def interface_owner(name: str) -> str:
    if name.startswith(("CSS", "Style", "MediaQuery", "Screen")):
        return "B5:css-binding"
    if name.startswith(("SVG", "XML", "XPath")):
        return "B4:detached-document"
    if name.startswith(("HTMLScript", "HTMLLink", "HTMLStyle")):
        return "R3:dynamic-scripts-styles"
    if name.startswith(("HTMLImage", "HTMLPicture", "HTMLSource")):
        return "R3:images"
    if name.startswith(("HTMLFrame", "HTMLIFrame")):
        return "R3:frames"
    if name.startswith(("HTMLInput", "HTMLSelect", "HTMLOption", "HTMLTextArea", "HTMLForm", "HTMLLabel", "HTMLButton", "HTMLFieldSet", "HTMLOutput", "HTMLMeter", "HTMLProgress")):
        return "B2:form-controls"
    if name.startswith(("HTMLTable", "HTMLCaption")):
        return "B2:tables"
    if name.startswith("HTML"):
        return "B2:html-elements"
    if name.startswith(("Event", "Mutation", "CustomElement")):
        return "B3:events-observers"
    return "B1:core-and-other-interfaces"


def generated_member_owner(interface: str, kind: str, name: str) -> str:
    if interface != "CSSStyleDeclaration" or kind != "Accessor":
        return interface_owner(interface)
    if name in {"constructor", "cssText", "length", "parentRule"}:
        return "B5:cssom-binding"
    if name.startswith(("color", "background", "fill", "stroke", "opacity", "display", "visibility", "outline", "scrollbar", "filter", "mask")):
        return "C5:color-display-visibility"
    if name.startswith(("font", "text", "word", "letter", "line", "white", "writing", "direction", "unicode", "listStyle", "quotes", "vertical", "ruby", "glyph", "caption")):
        return "C5:font-text"
    if name.startswith(("animation", "transition", "transform", "perspective", "boxShadow", "textShadow", "cursor", "pointer", "zoom", "backface")):
        return "C5:motion-interaction"
    if name.startswith(("width", "height", "min", "max", "margin", "padding", "border", "flex", "layout", "align", "justify", "position", "overflow", "column", "break", "pageBreak", "top", "right", "bottom", "left", "inset", "box", "clear", "clip", "float", "cssFloat", "table", "zIndex", "order")):
        return "C5:box-layout"
    return "C5:legacy-svg-and-page"


def source_files() -> list[Path]:
    return sorted(path for path in ROOT.rglob("*")
                  if path.is_file() and path.suffix in {".cs", ".csproj", ".props", ".targets"}
                  and not set(path.relative_to(ROOT).parts).intersection(IGNORE_PARTS)
                  and not (path.relative_to(ROOT).as_posix().startswith("Jint.Browser/Dom/Generated/")
                           and path.name.endswith(".g.cs")))


def without_comments(source: str) -> str:
    # Preserve line breaks so references point at source lines. This only classifies
    # AngleSharp-bearing files; a full C# parser is not needed for the lock.
    source = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group().count("\n"), source, flags=re.S)
    return re.sub(r"//[^\n]*", "", source)


def without_xml_comments(source: str) -> str:
    return re.sub(r"<!--.*?-->", lambda m: "\n" * m.group().count("\n"), source, flags=re.S)


def source_inventory() -> list[dict]:
    result = []
    for path in source_files():
        relative = path.relative_to(ROOT).as_posix()
        contents = path.read_bytes()
        text = contents.decode("utf-8-sig")
        code = without_comments(text) if path.suffix == ".cs" else without_xml_comments(text)
        references = [line.strip() for line in code.splitlines() if ANGLE.search(line)]
        if not references:
            continue
        item = {"path": relative, "category": category(relative), "owner": owner(relative), "references": references}
        if item["category"] in {"runtime", "generator"}:
            # A using directive makes unqualified native calls invisible to text search.
            # Lock the whole hand-written file so each edit gets an ownership review.
            item["sha256"] = digest(contents)
        result.append(item)
    return result


def generated_inventory() -> dict:
    generated = ROOT / "Jint.Browser/Dom/Generated"
    registry_text = (generated / "DomInterfaces.g.cs").read_text()
    names = sorted(set(REGISTRY.findall(registry_text)))
    members = []
    extension_source = ROOT / "Jint.Browser/Dom/Collections/DomIterableMembers.cs"
    extension_names = re.findall(r'Add\(builder, "([^"]+)"\);', extension_source.read_text())
    if len(extension_names) != 4 or len(set(extension_names)) != 4:
        raise ValueError("Unexpected DomIterableMembers.ValueIterator surface")
    for path in sorted(generated.glob("DomShapes.*.g.cs")):
        text = path.read_text()
        matches = list(SHAPE.finditer(text))
        for index, match in enumerate(matches):
            end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
            block = text[match.end():end]
            for kind, named, symbol in MEMBER.findall(block):
                member = named or "@@" + symbol[0].lower() + symbol[1:]
                members.append({"interface": match.group(1), "kind": kind, "name": member, "owner": generated_member_owner(match.group(1), kind, member)})
            if "DomIterableMembers.ValueIterator(builder)" in block:
                for member in extension_names:
                    members.append({"interface": match.group(1), "kind": "ExtendedMethod", "name": member,
                                    "owner": generated_member_owner(match.group(1), "ExtendedMethod", member)})
    members.sort(key=lambda x: (x["interface"], x["kind"], x["name"]))
    missing = sorted(set(names) - {m["interface"] for m in members})
    # An empty interface can have a shape without a member; a manual shape has no Build method.
    overrides_path = ROOT / "tools/dom-bindings/overrides.json"
    overrides = json.loads(overrides_path.read_text())
    return {"registryInterfaces": names, "members": members, "withoutGeneratedMembers": missing,
            "files": {p.name: digest(p.read_bytes()) for p in sorted(generated.glob("*.g.cs"))},
            "overrideSource": overrides_path.relative_to(ROOT).as_posix(),
            "overrideSha256": digest(overrides_path.read_bytes()),
            "overrideEntries": {key: len(value) for key, value in overrides.items() if not key.startswith("$")},
            "iterableExtensionSource": extension_source.relative_to(ROOT).as_posix(),
            "iterableExtensionSha256": digest(extension_source.read_bytes())}


def package_inventory() -> dict:
    def package_nodes(path: Path, tag: str):
        for node in ET.parse(path).iter():
            if node.tag.rsplit("}", 1)[-1] == tag:
                name = node.get("Include") or node.get("Update")
                if name and ANGLE.fullmatch(name.split(".")[0]):
                    yield name, node.get("Version") or node.findtext("Version")

    versions = dict(package_nodes(ROOT / "Directory.Packages.props", "PackageVersion"))
    central_versions = []
    for path in sorted(ROOT.rglob("Directory.Packages.props")):
        if set(path.relative_to(ROOT).parts).intersection(IGNORE_PARTS):
            continue
        for package, version in package_nodes(path, "PackageVersion"):
            central_versions.append({"path": path.relative_to(ROOT).as_posix(), "package": package, "version": version})
    references = []
    for path in source_files():
        if path.suffix not in {".csproj", ".props", ".targets"}:
            continue
        relative = path.relative_to(ROOT).as_posix()
        for package, version in package_nodes(path, "PackageReference"):
            references.append({"path": relative, "package": package, "version": version,
                               "category": category(relative), "owner": owner(relative)})
    return {"versions": versions, "centralVersions": central_versions,
            "references": sorted(references, key=lambda x: (x["path"], x["package"])),
            "generatorPin": json.loads((ROOT / "tools/dom-bindings/pin.json").read_text())["packages"]}


def api_baseline() -> dict:
    browser = ROOT / "Jint.Browser"
    declarations = []
    members = []
    for path in sorted(browser.glob("*.cs")):
        lines = path.read_text().splitlines()
        is_public_type = any(re.match(r"^public (?:sealed |abstract |readonly |static |partial )*(?:class|record|struct|interface|enum|delegate)\b", line) for line in lines)
        for line in lines:
            if re.match(r"^public (?:sealed |abstract |readonly |static |partial )*(?:class|record|struct|interface|enum|delegate)\b", line):
                declarations.append({"path": path.relative_to(ROOT).as_posix(), "declaration": line.strip()})
            if is_public_type and re.match(r"^    public \S", line):
                members.append({"path": path.relative_to(ROOT).as_posix(), "declaration": line.strip()})
    verified = ROOT / "Jint.Tests.PublicInterface/Verify"
    return {"browserTopLevelDeclarations": declarations, "browserSourceMemberDeclarations": members,
            "jintPublicApiSnapshots": {p.name: digest(p.read_bytes()) for p in sorted(verified.glob("PublicApiTest_*.verified.txt"))}}


def wpt_baseline() -> dict:
    readme = ROOT / "Jint.Tests.Browser/Wpt/README.md"
    lines = readme.read_text().splitlines()
    rows = []
    for line in lines:
        match = CENSUS.match(line)
        if match:
            rows.append({"suite": match.group(1).strip("`*"), "documents": int(match.group(2).replace(",", "").replace("*", "")),
                         "synthesized": int(match.group(3).replace(",", "").replace("*", "")),
                         "tests": int(match.group(4).replace(",", "").replace("*", "")),
                         "notPassing": int(match.group(5).replace(",", "").replace("*", ""))})
    if len(rows) < 2 or rows[-1]["suite"] != "total":
        raise ValueError("Could not read the browser WPT census table")
    vendor = (ROOT / "Jint.Tests/Wpt/Vendor/README.md").read_text()
    commit = re.search(r"\b[0-9a-f]{40}\b", vendor)
    if commit is None:
        raise ValueError("Could not read the vendored WPT commit")
    return {"corpusCommit": commit.group(), "censusSource": readme.relative_to(ROOT).as_posix(), "rows": rows,
            "exclusionsSource": "Jint.Tests.Browser/Wpt/WptBrowserExclusions.cs",
            "exclusionsSha256": digest((ROOT / "Jint.Tests.Browser/Wpt/WptBrowserExclusions.cs").read_bytes())}


def inventory() -> dict:
    result = {"schema": 1, "source": source_inventory(), "generated": generated_inventory(),
              "packages": package_inventory(), "api": api_baseline(), "wpt": wpt_baseline()}
    if result["packages"]["generatorPin"] != {k: result["packages"]["versions"][k] for k in result["packages"]["generatorPin"]}:
        raise ValueError("Generator pin differs from central package versions")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update", action="store_true", help="write the reviewed migration lock")
    args = parser.parse_args()
    actual = inventory()
    rendered = json.dumps(actual, indent=2, ensure_ascii=False) + "\n"
    if args.update:
        LOCK.write_text(rendered)
        print(f"Wrote {LOCK.relative_to(ROOT)}")
        return 0
    if not LOCK.exists() or LOCK.read_text() != rendered:
        print("HTML parser inventory changed. Review the diff from --update; assign every new use before accepting it.", file=sys.stderr)
        return 1
    print(f"HTML parser inventory matches: {len(actual['source'])} AngleSharp source files, {len(actual['generated']['members'])} generated members, {len(actual['wpt']['rows']) - 1} WPT suites.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

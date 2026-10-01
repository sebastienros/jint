#!/usr/bin/env python3
"""Rewrite expectations.json and optional-policies.json in their canonical layout. Never called by tests.

Both files are reviewed by hand; run this after editing either one. Each projection entry is written on
one line with null members omitted (the harness reads an absent member as null). A projection with at
least SHARE_MIN entries that is identical to one stored earlier is replaced by a projectionSameAs
reference to that row: the same document in another encoding then costs one line instead of a copy.
Sharing is storage only. The harness compares the referencing row against exactly the same entries,
and the formatter expands every existing reference before sharing again, so its output is canonical.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FILES = ("expectations.json", "optional-policies.json")
SHARE_MIN = 100


def without_nulls(value: dict) -> dict:
    return {key: item for key, item in value.items() if item is not None}


def compact_entry(entry: dict) -> str:
    entry = without_nulls(entry)
    if entry.get("Attributes"):
        entry["Attributes"] = [without_nulls(attribute) for attribute in entry["Attributes"]]
    return json.dumps(entry, ensure_ascii=False, separators=(", ", ": "))


def expand(files: dict[str, list[dict]]) -> None:
    rows = {row["key"]: row for items in files.values() for row in items}
    for row in rows.values():
        source = row.get("projectionSameAs")
        if source is None:
            continue
        target = rows.get(source)
        if target is None or target.get("projection") is None or "projectionSameAs" in target:
            raise ValueError(f"{row['key']}: projectionSameAs must name a row storing its own projection")
        if row.get("projection") is not None:
            raise ValueError(f"{row['key']}: projection and projectionSameAs are exclusive")
        rebuilt = {}
        for key, value in row.items():
            if key in ("projection", "projectionSameAs"):
                rebuilt.setdefault("projection", target["projection"])
            else:
                rebuilt[key] = value
        row.clear()
        row.update(rebuilt)


def share(files: dict[str, list[dict]]) -> None:
    owners: dict[str, str] = {}
    for items in files.values():
        for index, row in enumerate(items):
            projection = row.get("projection")
            if projection is None or len(projection) < SHARE_MIN:
                continue
            identity = json.dumps([without_nulls(entry) for entry in projection], sort_keys=True)
            owner = owners.setdefault(identity, row["key"])
            if owner != row["key"]:
                items[index] = {
                    ("projectionSameAs" if key == "projection" else key): (owner if key == "projection" else value)
                    for key, value in row.items()
                }


def render(rows: list[dict]) -> str:
    lines = ["["]
    for row_index, row in enumerate(rows):
        lines.append("  {")
        members = list(row.items())
        for index, (key, value) in enumerate(members):
            comma = "," if index < len(members) - 1 else ""
            if key == "projection" and value:
                body = ",\n".join("      " + compact_entry(entry) for entry in value)
                lines.append(f'    "{key}": [\n{body}\n    ]{comma}')
            else:
                text = json.dumps(value, ensure_ascii=False, indent=2).replace("\n", "\n    ")
                lines.append(f'    "{key}": {text}{comma}')
        lines.append("  }" + ("," if row_index < len(rows) - 1 else ""))
    lines.append("]")
    return "\n".join(lines) + "\n"


def main() -> int:
    files = {name: json.loads((ROOT / name).read_text(encoding="utf-8")) for name in FILES}
    expand(files)
    share(files)
    for name, rows in files.items():
        (ROOT / name).write_text(render(rows), encoding="utf-8", newline="\n")
        print(f"{name}: {(ROOT / name).stat().st_size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())

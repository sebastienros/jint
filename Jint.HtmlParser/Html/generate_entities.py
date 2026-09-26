"""Regenerate HtmlEntities.g.cs from the WHATWG HTML named-reference table.

Pinned input: https://html.spec.whatwg.org/entities.json
Retrieved 2026-09-23; SHA-256 d741d877ac77c4194c4ad526b5b4a19aef8dfe411ab840a466891cdbb9f362e6.
Run: python3 generate_entities.py /path/to/entities.json
"""

import hashlib
import json
import pathlib
import sys

PIN = "d741d877ac77c4194c4ad526b5b4a19aef8dfe411ab840a466891cdbb9f362e6"


def csharp_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def main() -> None:
    source = pathlib.Path(sys.argv[1]).read_bytes()
    actual = hashlib.sha256(source).hexdigest()
    if actual != PIN:
        raise SystemExit(f"Expected WHATWG entities SHA-256 {PIN}, got {actual}")
    entities = json.loads(source)
    lines = [
        "// Generated from https://html.spec.whatwg.org/entities.json",
        f"// SHA-256: {PIN}",
        "using System.Collections.Generic;",
        "namespace Jint.HtmlParser.Html;",
        "internal static class HtmlEntities",
        "{",
        "    internal static readonly Dictionary<string, string> Values = new(System.StringComparer.Ordinal)",
        "    {",
    ]
    for key, value in sorted(entities.items()):
        lines.append(f"        [{csharp_string(key[1:])}] = {csharp_string(value['characters'])},")
    lines.extend([
        "    };",
        "    internal static readonly HashSet<string> Prefixes = BuildPrefixes();",
        "    private static HashSet<string> BuildPrefixes()",
        "    {",
        "        var prefixes = new HashSet<string>(System.StringComparer.Ordinal);",
        "        foreach (var key in Values.Keys)",
        "            for (var i = 1; i <= key.Length; i++) prefixes.Add(key[..i]);",
        "        return prefixes;",
        "    }",
        "}",
    ])
    pathlib.Path(__file__).with_name("HtmlEntities.g.cs").write_text("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()

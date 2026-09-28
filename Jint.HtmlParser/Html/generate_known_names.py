"""Generate fixed-vocabulary recognizers; run with --check to verify checked-in output."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import itertools
import json
from pathlib import Path


HTML_NAMES = """
a b i p s u
br em h1 h2 h3 h4 h5 h6 hr id li ol td th tr ul
alt div img rel src
body form head href html link meta name span type
class input label style table tbody tfoot thead title value
button option script select strong
content section template textarea
""".split()

HEADERS = """
Content-Type Content-Length Content-Encoding Content-Language Content-Location
Host User-Agent Accept
""".split()
KEYWORDS = "if for while return break switch continue throw try catch".split()
SHARED_PREFIXES = [
    "shared-prefix-" + suffix + "-shared-suffix"
    for suffix in ("red-a", "red-b", "red-c", "blu-a", "blu-b", "blu-c")
]


@dataclass(frozen=True)
class Check:
    offset: int
    width: int
    mask: int
    expected: int


@dataclass(frozen=True)
class Node:
    name: str | None = None
    checks: tuple[Check, ...] = ()
    offset: int = 0
    width: int = 0
    children: tuple[tuple[Check, Node], ...] = ()


def vocabulary(names: list[str], ignore_case: bool) -> list[str]:
    if not names or any(not name or not name.isascii() for name in names):
        raise ValueError("The vocabulary must contain nonempty ASCII strings.")
    keys = [name.lower() if ignore_case else name for name in names]
    if len(set(keys)) != len(keys):
        raise ValueError("Duplicate or case-ambiguous vocabulary entry.")
    return sorted(names, key=lambda name: (len(name), name))


def comparison(name: str, offset: int, width: int, proven: frozenset[int],
               unit_bits: int, ignore_case: bool) -> Check:
    assert 0 <= offset and offset + width <= len(name)
    mask = expected = 0
    for index in range(width):
        position = offset + index
        if position in proven:
            continue
        character = ord(name[position])
        unit_mask = (1 << unit_bits) - 1
        if ignore_case and ("A" <= name[position] <= "Z" or "a" <= name[position] <= "z"):
            unit_mask &= ~0x20
        mask |= unit_mask << (index * unit_bits)
        expected |= (character & unit_mask) << (index * unit_bits)
    return Check(offset, width, mask, expected)


def build(names: list[str], widths: tuple[int, ...], unit_bits: int,
          ignore_case: bool, proven: frozenset[int] = frozenset()) -> Node:
    length = len(names[0])
    assert all(len(name) == length for name in names)
    if len(names) == 1:
        checks = []
        remaining = set(range(length)) - proven
        while remaining:
            offset = min(remaining)
            width = next(width for width in widths
                         if set(range(offset, offset + width)) <= remaining)
            checks.append(comparison(names[0], offset, width, frozenset(), unit_bits, ignore_case))
            remaining.difference_update(range(offset, offset + width))
        return Node(name=names[0], checks=tuple(checks))

    choices = []
    for width in widths:
        for offset in range(length - width + 1):
            partitions: dict[Check, list[str]] = {}
            for name in names:
                key = comparison(name, offset, width, proven, unit_bits, ignore_case)
                partitions.setdefault(key, []).append(name)
            if len(partitions) < 2:
                continue
            new_positions = len(set(range(offset, offset + width)) - proven)
            score = (max(map(len, partitions.values())), -len(partitions), -new_positions,
                     offset % width != 0, width, offset)
            choices.append((score, offset, width, partitions))
    # Distinct equal-length ASCII strings always have a discriminating unit.
    if not choices:
        raise ValueError("Indistinguishable candidates; refusing to emit an incomplete recognizer.")
    _, offset, width, partitions = min(choices, key=lambda choice: choice[0])
    covered = proven | frozenset(range(offset, offset + width))
    children = tuple(
        (check, build(group, widths, unit_bits, ignore_case, covered))
        for check, group in sorted(partitions.items(), key=lambda item: (item[0].mask, item[0].expected))
    )
    return Node(offset=offset, width=width, children=children)


def trees(names: list[str], kind: str, ignore_case: bool) -> dict[int, Node]:
    names = vocabulary(names, ignore_case)
    if kind not in ("char", "byte"):
        raise ValueError("Expected char or byte input.")
    widths, bits = ((4, 2, 1), 16) if kind == "char" else ((8, 4, 2, 1), 8)
    return {length: build(list(group), widths, bits, ignore_case)
            for length, group in itertools.groupby(names, len)}


def emit_class(names: list[str], class_name: str, kind: str = "char",
               ignore_case: bool = False, *, results: dict[str, str] | None = None,
               return_type: str = "string?",                default_result: str = "null", include_values: bool = True) -> list[str]:
    names = vocabulary(names, ignore_case)
    if results is not None and set(results) != set(names):
        raise ValueError("Every recognized name must have exactly one result.")
    roots = trees(names, kind, ignore_case)
    bits = 16 if kind == "char" else 8
    serial = itertools.count()
    used_widths: set[int] = set()

    def constant(value: int, width: int) -> str:
        return f"0x{value:0{width * bits // 4}X}" + ("UL" if width * bits == 64 else "U")

    def load(offset: int, width: int) -> str:
        if width == 1:
            return f"(uint) input[{offset}]"
        used_widths.add(width)
        return f"Read{width}(input, {offset})"

    def condition(check: Check, value: str) -> str:
        if check.mask != (1 << (check.width * bits)) - 1:
            value = f"({value} & {constant(check.mask, check.width)})"
        return f"{value} == {constant(check.expected, check.width)}"

    def emit(node: Node, indent: str) -> tuple[list[str], bool]:
        if node.name is not None:
            result = f"return {results[node.name] if results is not None else json.dumps(node.name)};"
            if node.checks:
                test = " && ".join(condition(check, load(check.offset, check.width)) for check in node.checks)
                return [f"{indent}if ({test}) {result}"], False
            return [indent + result], True
        variable = f"chunk{next(serial)}"
        lines = [f"{indent}var {variable} = {load(node.offset, node.width)};"]
        full_mask = (1 << (node.width * bits)) - 1
        use_switch = all(check.mask == full_mask for check, _ in node.children)
        if use_switch:
            lines.extend([f"{indent}switch ({variable})", indent + "{"])
        for index, (check, child) in enumerate(node.children):
            if use_switch:
                edge_indent = indent + "    "
                lines.append(f"{edge_indent}case {constant(check.expected, check.width)}:")
            else:
                edge_indent = indent
                keyword = "if" if index == 0 else "else if"
                lines.append(f"{indent}{keyword} ({condition(check, variable)})")
            lines.append(edge_indent + "{")
            child_lines, returns = emit(child, edge_indent + "    ")
            lines.extend(child_lines)
            if use_switch and not returns:
                lines.append(edge_indent + "    break;")
            lines.append(edge_indent + "}")
        if use_switch:
            lines.append(indent + "}")
        return lines, False

    lines = [f"internal static class {class_name}", "{"]
    if include_values:
        lines.extend([
            "    private static readonly string[] Names =", "    [",
            *[f"        {json.dumps(name)}," for name in names],
            "    ];", "", "    internal static ReadOnlySpan<string> Values => Names;", "",
        ])
    else:
        lines.extend([f"    internal const int Count = {len(names)};", ""])
    lines.extend([
        f"    internal static {return_type} Match(ReadOnlySpan<{kind}> input)", "    {",
        "        switch (input.Length)", "        {",
    ])
    for length, root in roots.items():
        lines.extend([f"            case {length}:", "            {"])
        body, returns = emit(root, "                ")
        lines.extend(body)
        if not returns:
            lines.append("                break;")
        lines.append("            }")
    lines.extend(["        }", f"        return {default_result};", "    }"])
    for width in sorted(used_widths):
        integer = "ulong" if width * bits == 64 else "uint"
        lines.extend(["", "    [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                      f"    private static {integer} Read{width}(ReadOnlySpan<{kind}> input, int offset)"])
        if kind == "byte":
            lines.append(f"        => BinaryPrimitives.ReadUInt{width * 8}LittleEndian(input.Slice(offset, {width}));")
        else:
            lines.extend(["    {",
                          f"        var value = MemoryMarshal.Read<{integer}>(MemoryMarshal.AsBytes(input.Slice(offset, {width})));"])
            if width == 2:
                lines.append("        return BitConverter.IsLittleEndian ? value : (value << 16) | (value >> 16);")
            else:
                lines.extend([
                    "        return BitConverter.IsLittleEndian ? value",
                    "            : (value << 48) | ((value & 0xFFFF0000UL) << 16) |",
                    "              ((value >> 16) & 0xFFFF0000UL) | (value >> 48);",
                ])
            lines.append("    }")
    return lines + ["}"]


def generate(namespace: str, classes: list[tuple[list[str], str, str, bool]]) -> str:
    lines = [
        "// <auto-generated />",
        "// Generated by Jint.HtmlParser/Html/generate_known_names.py. Do not edit.",
        "#nullable enable",
        "using System;",
        "using System.Buffers.Binary;",
        "using System.Runtime.CompilerServices;",
        "using System.Runtime.InteropServices;",
        "", f"namespace {namespace};", "",
    ]
    for names, class_name, kind, ignore_case in classes:
        lines.extend(emit_class(names, class_name, kind, ignore_case))
        lines.append("")
    return "\n".join(lines)


def outputs() -> dict[Path, str]:
    directory = Path(__file__).resolve().parent
    return {
        directory / "HtmlKnownNames.g.cs": generate("Jint.HtmlParser.Html", [
            (HTML_NAMES, "HtmlKnownNames", "char", False),
        ]),
        directory.parent.parent / "Jint.Tests.HtmlParser/Html/GeneratedNameLookupFixtures.g.cs":
            generate("Jint.Tests.HtmlParser.Html", [
                (HEADERS, "HeaderByteNames", "byte", True),
                (HEADERS, "HeaderCharNames", "char", True),
                (KEYWORDS, "KeywordByteNames", "byte", False),
                (SHARED_PREFIXES, "SharedPrefixNames", "char", False),
            ]),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    for path, content in outputs().items():
        if args.check:
            if not path.exists() or path.read_text() != content:
                raise SystemExit(f"Stale generated recognizer: {path}")
        else:
            path.write_text(content)
        print(path.name)


if __name__ == "__main__":
    main()

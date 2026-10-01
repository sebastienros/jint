"""Generate fixed-vocabulary recognizers; run with --check to verify checked-in output."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import functools
import itertools
import json
import operator
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
# Under an ASCII-insensitive mask '@' and '`' (and '[' and '{') read as the letter 'a' (and 'b') does, and
# '-' leaves bit 0x20 unproven, so these exercise a masked switch case that re-checks its stricter bits.
PUNCTUATION_COLLISIONS = ["x@", "x`", "xa", "x-", "y[", "y{", "yb", "y-"]


# Comparisons one Match method may hold before its heaviest lengths move into their own methods; see
# known-name-lookup.md for the measurement it comes from.
INLINE_BUDGET = 64


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
        # Switch on the bits every edge compares, so an ASCII-insensitive node still compiles to a jump
        # table or binary search rather than a chain of masked comparisons. An edge whose own mask is
        # stricter (it holds punctuation where a sibling holds a letter) re-checks those bits in its case;
        # only edges whose punctuation differs from a sibling's letter by exactly 0x20 can share a case.
        common = functools.reduce(operator.and_, (check.mask for check, _ in node.children))
        cases: dict[int, list[tuple[Check, Node]]] = {}
        for check, child in node.children:
            cases.setdefault(check.expected & common, []).append((check, child))
        scrutinee = variable if common == (1 << (node.width * bits)) - 1 \
            else f"{variable} & {constant(common, node.width)}"
        lines.extend([f"{indent}switch ({scrutinee})", indent + "{"])
        for key, edges in sorted(cases.items()):
            case_indent = indent + "    "
            lines.extend([f"{case_indent}case {constant(key, node.width)}:", case_indent + "{"])
            for check, child in edges:
                if check.mask == common:
                    child_lines, returns = emit(child, case_indent + "    ")
                else:
                    lines.extend([f"{case_indent}    if ({condition(check, variable)})", case_indent + "    {"])
                    child_lines, _ = emit(child, case_indent + "        ")
                    child_lines.append(case_indent + "    }")
                    returns = False
                lines.extend(child_lines)
            if not returns:
                lines.append(case_indent + "    break;")
            lines.append(case_indent + "}")
        lines.append(indent + "}")
        return lines, False

    def weight(node: Node) -> int:
        if node.name is not None:
            return len(node.checks)
        return 1 + sum(weight(child) for _, child in node.children)

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
    # One method holding every length's tree defeats the JIT once the vocabulary is large: past its
    # inlining budget the ReadN helpers become real calls, and the extra span temporaries push it into
    # zeroing the frame in the prolog of every call, hit or miss. A heavy length therefore gets its own
    # method, so Match stays a jump table on the length and each tree is compiled in a small frame.
    weights = {length: weight(root) for length, root in roots.items()}
    split: set[int] = set()
    for length in sorted(weights, key=lambda length: (-weights[length], length)):
        if sum(weight for length, weight in weights.items() if length not in split) <= INLINE_BUDGET:
            break
        split.add(length)
    bodies: list[str] = []
    for length, root in roots.items():
        if length in split:
            lines.append(f"            case {length}: return Match{length}(input);")
            bodies.extend(["", f"    private static {return_type} Match{length}(ReadOnlySpan<{kind}> input)",
                           "    {",
                           # Restates what the caller's switch proved, so the JIT drops the bounds checks.
                           f"        if (input.Length != {length}) return {default_result};"])
            body, returns = emit(root, "        ")
            bodies.extend(body)
            if not returns:
                bodies.append(f"        return {default_result};")
            bodies.append("    }")
            continue
        lines.extend([f"            case {length}:", "            {"])
        body, returns = emit(root, "                ")
        lines.extend(body)
        if not returns:
            lines.append("                break;")
        lines.append("            }")
    lines.extend(["        }", f"        return {default_result};", "    }"])
    lines.extend(bodies)
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
                (PUNCTUATION_COLLISIONS, "PunctuationCharNames", "char", True),
                (PUNCTUATION_COLLISIONS, "PunctuationByteNames", "byte", True),
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

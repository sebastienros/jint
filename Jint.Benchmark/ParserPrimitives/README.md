# Markup parser primitive comparisons

Dedicated decision benchmarks for the native HTML/XML parser work. These compare equivalent small
operations, not Markdown against HTML or a scanner against a complete parser. They change no runtime
parser behavior. The newest supported benchmark TFM is **net10.0**, selected by the existing project;
do not add older runtime jobs or an installed preview runtime to these comparisons.

## What Markdig actually does

Source inspected at commit **56e9c238584a44a169f174c881855c049768634c**, dated 2026-09-20:

| Markdig source | Observed technique | Relevant candidate here |
| --- | --- | --- |
| [CharacterMap](https://github.com/xoofx/markdig/blob/56e9c238584a44a169f174c881855c049768634c/src/Markdig/Helpers/CharacterMap.cs) | 128-entry ASCII array, optional frozen dictionary for other characters; cached SearchValues for locating registered opening characters | ASCII membership table and cached delimiter search |
| [ParserList](https://github.com/xoofx/markdig/blob/56e9c238584a44a169f174c881855c049768634c/src/Markdig/Parsers/ParserList.cs) | Builds character-to-parser-array dispatch once | Reuse immutable lookup structures; do not build them per character/token |
| [CharHelper](https://github.com/xoofx/markdig/blob/56e9c238584a44a169f174c881855c049768634c/src/Markdig/Helpers/CharHelper.cs) | Cached SearchValues.Contains for punctuation/email sets; a bit-mask fast branch for ASCII whitespace and separate Unicode handling | Test Contains and a simple guarded bit mask against the existing small `or` pattern |
| [HtmlRenderer](https://github.com/xoofx/markdig/blob/56e9c238584a44a169f174c881855c049768634c/src/Markdig/Renderers/HtmlRenderer.cs) | IndexOfAny with a cached four-character SearchValues; a direct two-character overload for soft escaping | Compare scalar scanning, direct span IndexOfAny and cached SearchValues for the same set |
| [HtmlBlockParser](https://github.com/xoofx/markdig/blob/56e9c238584a44a169f174c881855c049768634c/src/Markdig/Parsers/HtmlBlockParser.cs) | Reads/lowercases into a short stack span, then CompactPrefixTree.TryMatchExact against predefined HTML names | Distinguish exact name lookup from tokenization; evaluate predefined sets against Jint's materialized-name `or` pattern |

The tree supports prefix operations, but this Markdig HTML call uses **exact** matching. Its case folding,
CommonMark tag grammar and whitespace semantics must not be imported into HTML/XML rules. The bit-mask
candidate here tests the same technique with HTML's five ASCII whitespace characters; it is not a copy
of Markdig's full Unicode whitespace helper. The bool array similarly specializes the ASCII indexing
idea; these rows do not claim to measure the generic CharacterMap itself. No Markdig source or runtime
dependency is vendored by this benchmark.

The current Jint comparison targets were inspected in integration commit `37c8a6712`:
`Html/HtmlTokenizer*.cs` character classification, `Html/HtmlInput.cs` input boundaries, and
`Html/TreeConstruction/HtmlTreeBuilder.Body.cs::IsBlockStart`. XML's whitespace excludes form feed, and
XML scalar/name validation has separate rules. These HTML kernels are not replacements for those checks.

## Equal work and units

All hot tables/configuration and input strings are created outside timing. Each method consumes the
same input and returns a count/checksum used by BenchmarkDotNet. No engine or mutable shared parse state
exists. Lookup input strings are freshly materialized rather than interned literal hits.

| Class | Compared methods | One reported operation | Inputs |
| --- | --- | --- | --- |
| MarkupCharacterMembershipBenchmark | `or` pattern, ASCII bool table, cached SearchValues.Contains, guarded bit mask | One UTF-16 character; 4,096 checks per invocation | Text-heavy and whitespace-heavy mixtures, including non-ASCII nonmatches |
| MarkupDelimiterScanBenchmark | Scalar `or` loop, span IndexOfAny, cached SearchValues | Scan one entire named buffer and checksum every delimiter position | Short text, long sparse text, dense markup, Unicode text |
| MarkupTagMembershipBenchmark | String `or` pattern, ordinal HashSet, ordinal FrozenSet | One exact tag lookup; 256 checks per invocation | Hits, misses, deterministic mixed distribution; prefix/suffix/case/non-ASCII misses |
| MarkupLookupConstructionBenchmark | Construct whitespace table/SearchValues or block-tag HashSet/FrozenSet | Build one named lookup | Compare only constructors for the same set |

Every class reports **Op/s** and **Allocated B/op** through BenchmarkDotNet. Construction rows separately
expose the cost hidden by hot static reuse; their allocation totals are not retained-heap measurements.
No-table patterns have no lookup construction to measure. Do not compare character Op/s to buffer Op/s,
or claim a parser throughput increase from these kernels alone. The delimiter set is exactly `<`, `&`,
CR and NUL; ordinary LF and Unicode are deliberately not equivalent delimiter substitutions.

The full suite currently has 33 parameterized rows. Small fixed sets can already be compiled efficiently
from `or`; the collection candidates are hypotheses, not a presumption that a lookup is faster. A frozen
set benefits from static lifetime and can lose for short names or miss distributions. Table initialization
must be included in a later end-to-end experiment if the proposed lifetime is per parse.

## Validation and measurement

Correctness-only, with a fresh Release build:

```sh
dotnet run -c Release -f net10.0 --project Jint.Benchmark/Jint.Benchmark.csproj -- --validate-markup-primitives
```

This checks every UTF-16 value for whitespace equivalence; sliced/randomized delimiter buffers,
including exact first-match offsets at every suffix, NUL, CR, surrogate code units and SIMD-sized
boundaries; exact tag cases; and each configured benchmark's result. A failure aborts independently
of timing. Each GlobalSetup also verifies its own input. These checks exercise search semantics,
not XML well-formedness or full HTML parser conformance.

Run on an otherwise idle machine, using the repository's benchmark environment instructions:

```sh
JINT_BENCH_MODE=gate dotnet run -c Release -f net10.0 --project Jint.Benchmark/Jint.Benchmark.csproj -- \
  --filter '*MarkupCharacterMembershipBenchmark*' '*MarkupDelimiterScanBenchmark*' \
           '*MarkupTagMembershipBenchmark*' '*MarkupLookupConstructionBenchmark*' \
  --exporters json csv
```

The local class config adds only Op/s; it does not replace the repository job, select another runtime,
disable tiering/PGO or bypass the busy-machine check. Do not use `--job short` results, `--no-build`,
concurrent builds/tests, or machine-idle overrides to decide a winner. Keep runtime/architecture, commit,
raw reports and environment metadata with results. Latest TFM alone does not establish portability
between x64 and Arm64. Treat small differences as inconclusive until the repository's repeat/paired
measurement requirements are met.

No timing results have been recorded yet. A coordinated idle measurement window is required while the
parser implementation work runs in other worktrees.

## Adoption gate

Keep a candidate only when it wins the relevant distributions without harmful allocation or startup
tradeoffs. Before changing a parser, prove the exact grammar/state set and retained character/offset
semantics. A vectorized scan must stop at the proper chunk boundary, charge all skipped work, poll
cancellation at the established cadence, respect quotas and preserve CRLF/NUL/Unicode handling.
Cached sets belong to immutable grammar data, not engine/document/host state.

Runtime feature owners make any resulting optimization as a separate reviewed change. Run the full
affected conformance tests and a paired equal-work whole-parser benchmark afterward. These primitive
rows inform that work; they do not replace the standalone API, Browser parity or production dependency
removal gates.

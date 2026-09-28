# Known-name recognition

`HtmlKnownNames.Match(ReadOnlySpan<char>)` recognizes the tokenizer's existing 56
well-known tag and attribute names. It returns the canonical string literal or
`null`; unknown names still use the bounded xxHash3 cache. HTML ASCII case folding,
copy-work accounting, cancellation checks and the 64-character cache limit remain
in the tokenizer.

## Generation and safety

`generate_known_names.py` is an offline source generator, following the existing
entity-table workflow rather than adding a Roslyn dependency to the standalone
parser. It emits the production recognizer and executable test examples:

```sh
python3 -B Jint.HtmlParser/Html/generate_known_names.py
python3 -B Jint.HtmlParser/Html/generate_known_names.py --check
python3 -B -m unittest discover -s Jint.HtmlParser/Html -p test_generate_known_names.py
```

The generator first partitions by exact length. For each remaining group it
considers every legal offset with widths of 4, 2 and 1 UTF-16 units, or 8, 4, 2
and 1 bytes. Discriminators are ranked by smallest largest partition, most distinct
partitions, most newly verified positions, natural alignment, cheaper width and
finally lower offset. Shared prefixes are not preferred merely because they
start at zero.

Each discriminator is loaded once. Successful edges record which positions have
been proven. Previously proven positions are masked out if a subsequent load
overlaps them; leaves check only the remaining contiguous units. Unique lengths
still require complete content verification. Duplicate, case-ambiguous or
non-ASCII vocabularies are rejected at generation time, never turned into partial
runtime matches. Input spans may contain arbitrary UTF-16 or bytes.

The production code uses checked span slices and `MemoryMarshal.Read` for
unaligned integer loads; every slice fits the enclosing exact-length branch.
There are no speculative reads past the span. Native UTF-16 loads are normalized
to low-word-first constants on big-endian hosts; that branch folds away on
little-endian hosts. Byte-mode examples use explicit little-endian reads.

Optional ASCII-insensitive generation uses per-position masks: `0xDF` for byte
letters or `0xFFDF` for UTF-16 letters, with every bit retained for punctuation.
UTF-16 high bits are preserved, so non-ASCII lookalikes cannot match ASCII names.
This is not Unicode case folding. The production HTML matcher remains
case-sensitive because the tokenizer already folds ASCII.

## Generated examples

`GeneratedNameLookupFixtures.g.cs` contains compiled examples for HTTP headers
(both byte and character input), parser keywords, and long shared prefixes:

| Vocabulary | Generated decision |
| --- | --- |
| `Content-Encoding`, `Content-Language`, `Content-Location` | Length 16, then the chunk at offset 8; verify the untouched prefix and suffix only after discriminating. |
| `if`, `for`, `while`, `return`, `break`, ... | Exact lengths and packed keyword chunks; a unique length never suffices alone. |
| `shared-prefix-red-a-shared-suffix`, `shared-prefix-blu-b-shared-suffix`, ... | Inspect the varying middle before checking the common prefix and suffix. |

Tests cover canonical identity, sliced/unaligned spans, all single-byte mutations
of every HTML name, deterministic random UTF-16 and byte inputs, ASCII case masks,
punctuation, non-ASCII rejection and zero steady-state lookup allocations. A
mutation that happens to produce another known name must return that name, rather
than being incorrectly classified as an unknown.

## Isolated comparison

Exploratory single-launch BenchmarkDotNet run, 2026-09-27, .NET 10.0.11 on Apple
M4 Pro: production tiering/PGO, blocking workstation GC, machine-idle check passed,
default warmup and at most 16 measurement iterations. Each invocation visits the
same reproducibly shuffled batch of 256 source slices. Values below are
nanoseconds per lookup, including the shared batch-loop overhead.

| Strategy | All known | Misses | Mixed |
| --- | ---: | ---: | ---: |
| Previous length-bucketed substring search | 6.010 | 5.391 | 5.440 |
| Generated discriminators | **2.915** | **2.428** | **2.704** |
| Length-bucketed `SequenceEqual` chain | 5.856 | 8.981 | 5.505 |
| `Dictionary` span alternate lookup | 4.584 | 3.588 | 4.125 |
| `FrozenDictionary` span alternate lookup | 5.059 | 4.375 | 4.942 |
| xxHash3 dispatch plus exact verification | 5.383 | 3.333 | 5.126 |

All rows allocated **0 bytes per lookup**. The previous substring search receives
already-padded input, so it is not penalized for preparing delimiters. Dictionary
comparisons do not materialize strings, and the hash row verifies its candidates.
The mixed batch emphasizes common names while retaining rare hits and misses;
the miss batch includes early/late differences, longer inputs and non-ASCII
characters. The xxHash3 miss row was multimodal, so its exact ranking is noisy.

The generated matcher won every measured distribution, reducing lookup time
approximately 50-55% relative to the previous search. It is now the tokenizer's
known-name path. This does **not** establish the same improvement in complete
parsing, nor a crossover point for larger vocabularies. Byte-mode examples and
ASCII-insensitive matchers have correctness coverage, not measurements in this comparison. A gating or
cross-platform performance claim needs separate repeated measurements.

The production generated source is 410 lines / 12,263 UTF-8 bytes, including
vocabulary, comments and read helpers. This trades code size for specialized
branches; it is not the native machine-code size. No disassembly, hardware branch
counters or broad vocabulary-size matrix was needed to select the winner for
this fixed parser vocabulary.

To repeat the isolated comparison:

```sh
JINT_BENCH_MODE=stable dotnet run --project Jint.Benchmark -c Release -- \
  --filter '*HtmlKnownNameLookupBenchmark*' --maxIterationCount 16
```

## Parser-wide recognition

`Parsing/parser-lookups.json` supplies 169 additional typed vocabularies.
`Parsing/generate_parser_lookups.py` reuses the same discriminator generator and
emits `ParserLookups.g.cs` plus independent reference cases for the native tests.
The original 56-name tokenizer recognizer and its measured vocabulary are unchanged.

| Area | Generated recognition |
| --- | --- |
| HTML/SVG | Tree-construction membership groups, table/template/fragment dispatch, namespace-adjusted foreign attributes, SVG tag/attribute spelling, input types, exact quirks public identifiers. |
| XML | Predefined entities, DTD catalog IDs and attribute types, declaration keywords and standalone values, literal reuse for common validated names. |
| CSS values | Units, wide keywords, math functions/constants/rounding strategies, named/contextual colors, color functions/spaces, transform descriptors, pending reference names. |
| CSS properties | Aliases, keyword sets, property indices shared by completed metadata, family obligations and shorthand effects; font-face descriptor names and keywords. |
| CSS rules/selectors | Pseudo-class/element/function names, media features and their keyword sets, container axes/functions, reserved names and at-rule dispatch. |

Entries map exact ASCII names to C# result expressions, with explicit return
types, defaults and case modes. Boolean groups return membership; enum groups
leave context-dependent actions in the caller. Keyword sets use a generated enum
rather than a runtime string-to-set dictionary. Their ASCII-insensitive matches
return canonical literals and preserve property-alias handling and the old
per-character work/checkpoint cadence. Media feature recognition no longer
constructs keyword arrays.

Existing named-color constants and the pinned property catalog remain their
source of truth. Property indices also include literal registrations in the
completed-metadata and shorthand builders; adding a new procedural registration
pattern requires teaching the extractor that pattern. Index construction fails
explicitly if any registration is absent. The 433 catalog obligations are not
silently promoted to implemented properties, and declaration-context checks
remain in the callers.

The broader matchers have no runtime vocabulary arrays or initialization work.
They return literals, primitives or existing result constructions; a descriptor
construction can still allocate as it did before. The expanded rollout has
correctness and allocation coverage, **not a new end-to-end speedup measurement**.
The isolated results above must not be generalized to these larger vocabularies.

Regenerate and verify from the repository root:

```sh
python3 -B Jint.HtmlParser/Parsing/generate_parser_lookups.py
python3 -B Jint.HtmlParser/Parsing/generate_parser_lookups.py --check
python3 -B -m unittest discover -s Jint.HtmlParser/Parsing -p test_generate_parser_lookups.py
```

Generator tests check currency, full input proof coverage and reachability from
parser code, including generated keyword-set dispatch. Native tests check every
vocabulary against a linear reference, mutations, arbitrary UTF-16, sliced input,
literal identity, metadata equivalence, zero-allocation keyword matching and
cancellation at each normalization checkpoint.

### Deliberate boundaries

Dynamic namespace/entity bindings, custom-property maps and runtime indexes stay
dynamic. Single-literal comparisons, character/state switches, and transformations
of already-recognized canonical values are not vocabulary lookups.

HTML entity parsing still needs longest-prefix matching and semicolon rules, so
it retains its immutable trie. XML's complete catalog-name lookup now uses that
same trie with an explicit terminal-semicolon edge instead of allocating
`name + ";"`. Markup/DTD and doctype prefix scans retain prefix semantics.
The reference-function wrapper retains its previous ordinal-ignore-case fallback
for non-ASCII input rather than silently imposing ASCII semantics on it.

XPath syntax is compiled by `System.Xml.XPath`, not a local Jint lexer. Its
`following::` guard and runtime namespace maps are therefore unchanged.

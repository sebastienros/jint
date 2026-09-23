# First independent parser feature slices

Dispatch contract following [the architecture](html-parser.md), 2026-09-23. These tasks can start
before DOM foundation merges: neither needs `Node`, `Document`, `Element`, bindings or a browser host.
They must integrate with the foundation before their final Release verification and commit handoff.
This document does not authorize a second package bootstrap or additional task creation.

## Shared ownership agreement

The integration/foundation owner alone edits project files, solution membership, central package
versions, signing/friend declarations, shared parse options/limits/errors, assembly conventions and
the package-wide README. Feature tasks add files under their allocated directories; SDK globs pick
them up. Do not duplicate those shared types under feature-specific names to avoid a merge dependency.
While foundation is pending, a temporary project outside the repository may link the new sources for
compilation; its scaffolding is not a deliverable. Final tests run against the real foundation.

Reserve these disjoint areas:

| Task | Production files | Test files | Shared facade |
| --- | --- | --- | --- |
| HTML cursor/tokenizer | `Jint.HtmlParser/Html/**` | `Jint.Tests.HtmlParser/Html/**` | None: tokenizer is internal |
| CSS syntax C1 | `Jint.HtmlParser/Css/Syntax/**` | `Jint.Tests.HtmlParser/Css/Syntax/**` | New `Jint.HtmlParser/MarkupParser.CssSyntax.cs` only |

If the foundation uses a different test project name, the coordinator maps these test directories once.
The foundation's `MarkupParser`, if already declared, must be `public static partial class` in namespace
`Jint.HtmlParser`. CSS supplies only four implemented syntax methods in its own partial file. It does
not add `ParseCss`, `ParseCssDeclarations`, `ParseCssValue`, selectors, placeholder document parsers or
throwing method stubs. There is no second competing public facade named `CssSyntaxParser`.

The common contracts below belong to a separately assigned A2 follow-up, not the DOM foundation
bootstrap. HTML and CSS can begin their algorithms internally while it is pending; neither publishes
alternate options/errors. The four public CSS methods land only after A2. CSS copies the immutable
limit values and the collector reference once at entry. There are no feature/strictness flags.

## A2 follow-up: exact minimal common contracts

The coordinator assigns this to one owner. Reserve `Jint.HtmlParser/Parsing/**` and
`Jint.Tests.HtmlParser/Parsing/**`; public types use namespace `Jint.HtmlParser`. This is a working
options/diagnostics/exception feature with tests, not a collection of future parser stubs. H1/H2 and
C1 consume these same definitions:

```csharp
public sealed class ParseLimits
{
    public static ParseLimits Unbounded { get; }
    public long MaxInputCharacters { get; init; } // default 0
    public int MaxTokenCharacters { get; init; }  // default 0
    public int MaxNestingDepth { get; init; }     // default 0
}

public enum ParseLimitKind { InputCharacters, TokenCharacters, NestingDepth }

public sealed class ParseLimitException : Exception
{
    public ParseLimitKind Kind { get; }
    public long Limit { get; }
    public long Observed { get; }
    internal ParseLimitException(ParseLimitKind kind, long limit, long observed);
}

public readonly struct ParseDiagnostic
{
    public string Code { get; }
    public long Offset { get; }
    internal ParseDiagnostic(string code, long offset);
}

public sealed class ParseDiagnosticCollector
{
    public ParseDiagnosticCollector(int capacity = 100);
    public int Capacity { get; }
    public IReadOnlyList<ParseDiagnostic> Items { get; }
    public bool IsTruncated { get; }
    public void Clear();
    internal void Add(string code, long offset);
}

public sealed class CssParseOptions
{
    public ParseLimits Limits { get; init; } = ParseLimits.Unbounded;
    public ParseDiagnosticCollector? Diagnostics { get; init; }
}

public sealed class CssParseException : Exception
{
    public string Code { get; }
    public long Offset { get; }
    internal CssParseException(string code, long offset);
}
```

The property-only sealed option types have public parameterless constructors. Init setters reject
negative bounds with `ArgumentOutOfRangeException` and null `Limits` with `ArgumentNullException`.
Zero means unbounded; a positive limit is inclusive. `Unbounded` is an immutable all-zero singleton.
Capacity must be positive. A null diagnostic collector is the allocation-free disabled path.
Exception constructors are internal because callers need to catch/inspect them, not manufacture them;
`Message` is a useful invariant description derived from the supplied fields, never the entire input.
No broad exception catch changes cancellation or unrelated failures into these errors.

Counting rules are shared and tested:

- `MaxInputCharacters` counts original UTF-16 units before newline/escape normalization. CSS checks
  the complete string; HTML counts all appended chunks with checked/saturating arithmetic. Reconsuming
  characters does not charge this bound twice. Exceeding a limit fails before accepting the excess.
- `MaxTokenCharacters` counts raw source units in an atomic lexical token, including delimiters.
  In H2 it bounds tags, comments, doctypes, processing instructions and character-reference scans; it does not count an
  arbitrarily grouped run of ordinary text. In CSS it bounds lexical tokens (including whitespace
  runs), not an entire nested rule/component subtree. Thus chunking/grouping does not change the limit.
- `MaxNestingDepth` bounds open CSS functions/simple blocks, with the first at depth 1; check before
  pushing. H1/H2 has no nested tree and does not use this field. HTML tree/open-element, XML entities
  and cumulative created-node/work budgets are separately specified when their implementation lands.
  Do not add speculative public fields now or map these bounds from Browser's `MaxDomNodes`.

`ParseLimitException.Observed` is the first measured quantity beyond the inclusive limit (or a known
whole-input length), not a promised allocation count. Diagnostic offsets are zero-based original UTF-16
positions, with EOF at input length. Codes are stable domain-prefixed identifiers such as
`html/unexpected-null-character` and `css/unexpected-eof`; keep the code vocabulary in each feature's
tests. A default `ParseDiagnostic` returns `Code == ""` and `Offset == 0`. No line-map, message callback,
runtime grammar extension or validation policy is part of A2.

`Items` is a read-only live view over bounded records; `Clear` empties it and resets truncation.
`Add` preserves arrival order up to capacity, then sets `IsTruncated` without retaining later records.
Each CSS entry and each new tokenizer session clears its supplied collector once, before parsing;
appending/resuming the same HTML session never clears it. Reuse across sequential calls replaces the
previous diagnostics; callers retaining them copy `Items`. A collector is single-owner mutable state,
not safe to share between concurrent sessions. There are no callbacks, threads, leases or hidden jobs.

The A2 tests cover immutable default values, invalid bounds, null limits, capacity/truncation/reset,
the read-only view and exception fields. Feature tests cover actual bound enforcement. Add only this
implemented shared surface to API snapshots. The feature owners do not edit these files; any necessary
adjustment is sent back to the coordinator so there remains one source of truth.

## HTML task: H1 and H2, with H3 as a separate follow-up

The first bounded deliverable is a resumable UTF-16 input cursor and a complete HTML **Data-mode**
tokenizer: data, tag open/end-tag/name, all attribute states, self-closing markers, character references,
markup declarations, all comment/bogus-comment states, doctype states and the five processing-instruction
states in the current HTML algorithm. This is useful, tested input
infrastructure. It is not a complete HTML document parser and must not expose `ParseHtml` yet.

H3 subsequently adds RCDATA, RAWTEXT, ScriptData and its escaped/double-escaped states, PLAINTEXT and
their EOF/end-tag transitions to the same tokenizer. Do not pretend Data mode handles those contexts.
Keeping H3 separate makes the first review finite; its work is also DOM-independent and may follow
immediately in the same assigned task after the H1/H2 commit has been reviewed.

### Internal contract

Namespace `Jint.HtmlParser.Html`; no public types in this slice.

```csharp
internal enum HtmlReadStatus { Token, NeedInput, Yielded, Complete }
internal enum HtmlTokenKind { Text, StartTag, EndTag, Comment, Doctype, ProcessingInstruction, EndOfFile }

internal sealed class HtmlTokenizer
{
    internal HtmlTokenizer(HtmlTokenizerContext context);
    internal void AppendInput(string chunk, bool isFinal = false);
    internal HtmlReadStatus Read(int workQuota, CancellationToken cancellationToken,
        out HtmlToken token);
}
```

`HtmlTokenizerContext` is feature-owned with this exact internal construction contract:

```csharp
internal readonly struct HtmlTokenizerContext
{
    internal HtmlTokenizerContext(ParseLimits? limits = null,
        ParseDiagnosticCollector? diagnostics = null, bool allowCData = false);
    internal ParseLimits Limits { get; }
    internal ParseDiagnosticCollector? Diagnostics { get; }
    internal bool AllowCData { get; }
}
```

Null limits and a default context mean `ParseLimits.Unbounded`. The tokenizer snapshots primitive bound
values into hot-path fields. Defaults support unrestricted tokenization with no observer or host. Do
not put a `Document` or engine in it. `AllowCData` is parsing context for standalone tokenizer cases;
when tree construction needs that
value to change between tokens, H7 promotes it to an explicit between-token setter. This is grammar
context, not an optional feature. No HTML fragment parsing or tree insertion mode lives here.

`AppendInput` retains immutable string segments, and `Read` releases consumed segments when no partial
token needs them. Empty nonfinal chunks are allowed. A final chunk marks the source closed; another
append throws `InvalidOperationException`. Null chunks throw `ArgumentNullException`. Cancellation
throws `OperationCanceledException` and makes the session terminal; later read/append calls throw
`InvalidOperationException`. A fatal parse-limit failure has the same terminal-session rule. Nonpositive work quota throws
`ArgumentOutOfRangeException` without consuming input. Work exhaustion returns `Yielded`, not a parse
error; repeated positive-budget calls make progress. The tokenizer needs no thread and no `IDisposable`.

`Read` uses a cooperative work quota for explicit scanner, validation and copying loops, including
character-reference lookahead and attribute-finalization preparation. Check quota/cancellation at a
bounded cadence in those loops; exhausting the quota suspends at the next safe state boundary. The
quota is **not** a hard per-call time, allocation or CPU bound. Safe managed APIs cannot yield halfway
through the final contiguous copy needed by an owned string/array, or a buffer-growth runtime copy.
Check token/input limits before accepting excess input and before result allocation, and poll
cancellation immediately before/after these unavoidable runtime materializations. They may exceed the
remaining quota by their copy length; there is no small-constant-overrun promise. Charge those lengths
in monotonic work accounting and suspend further explicit work when exhausted (an already finished
token may be returned). Perform each materialization once, retaining progress across Yielded/NeedInput;
never restart flattening or repeatedly copy growing prefixes. Preserve amortized-linear accumulation.
Do not add an unsafe or elaborate resumable flattening pipeline that still ends in a contiguous copy.
An internal work counter supports deterministic tests; it is not a public node budget. Verify bounded
explicit-loop progress, cancellation polls and linear copy counts, not stopwatch latency or a universal
`workDelta <= quota + constant` assertion across runtime allocation/copy operations.

When status is `Token`, `token` is valid. For other statuses it is default and must not be observed.
EOF is emitted as one `EndOfFile` token after final input and recovery; later reads return `Complete`.
Nonfinal exhaustion is `NeedInput`, never EOF. `Read` does not reenter callbacks or run user code.
Diagnostics append bounded records only, using A2's collector; there is no user callback to invoke.

Token data contract, independent of the eventual compact storage layout:

| Kind | Required data |
| --- | --- |
| Text | Owned `string Data` in source order after HTML preprocessing and state-specific reference handling |
| StartTag/EndTag | ASCII-lowercased `string Name`, ordered attributes, `bool SelfClosing`; preserve end-tag parse-error metadata when needed |
| Comment | Owned `string Data` |
| ProcessingInstruction | Owned case-preserved `string Name` for the target and `string Data`; neither is tag-name-normalized |
| Doctype | `string? Name`, `string? PublicIdentifier`, `string? SystemIdentifier`, `bool ForceQuirks`; missing differs from empty |
| EndOfFile | No payload |

`HtmlAttribute` contains owned `string Name` and `string Value`, in tokenizer order. Discard duplicate
attributes after the first using HTML's matching rule and report a diagnostic when enabled. Attributes
are not namespace-resolved by the tokenizer. Attribute collections may be an owned array exposed
read-only internally; never return pooled arrays that a later read mutates. Every returned token stays
valid after another read/append and after the tokenizer is collected. This owned contract makes initial
parser integration safe; a future borrowed fast lane is a distinct internal contract with tests.

Text token chunking is unspecified: a network/chunk boundary may split one character run into multiple
Text tokens. Tests compare exact non-text tokens and text after merging adjacent Text tokens. Splitting
must not change character/reference interpretation or introduce an EOF. Do not accumulate a whole
unbounded plain-text document merely to force one Text token. Buffer partial tags/comments/attributes
linearly, bounded by configured token/input limits. No repeated `existing + chunk` for growing content.

Preprocessing normalizes CR and CRLF correctly across input segments; a split surrogate pair remains
semantically identical to a contiguous pair. Raw UTF-16 source offsets count original input units,
not the normalized buffer. NUL handling and character-reference rules remain state-specific. Character
references include numeric validation/replacements, missing semicolons, attribute ambiguity and the
complete named reference table. Do not ship a five-entity shortcut. A generated table needs a pinned
primary-source input/provenance and deterministic regeneration; generator tooling belongs only to this
task's assigned subtree or a coordinator-reserved tool directory.

CDATA handling is selected by parsing context: HTML bogus-comment behavior by default, CDATA tokenization
when allowed. It may be implemented in H2 with the immutable context flag; do not infer it from seeing
`<svg>` because namespace/tree context is not the tokenizer's job.

The [current tag-open algorithm](https://html.spec.whatwg.org/multipage/parsing.html#tag-open-state)
routes `<?` through the five PI states: processing instruction open, processing instruction target,
after processing instruction target, processing instruction data, and processing instruction questionable.
They belong to H1/H2, including malformed-target conversion to a bogus comment, disallowed targets and
EOF recovery. Use the [PI state algorithms](https://html.spec.whatwg.org/multipage/parsing.html#processing-instruction-open-state);
do not reuse XML PI parsing or the obsolete blanket `<?`-becomes-comment rule. PI Name preserves case;
the separate tag-name rule still lowercases tag tokens. Cover `>` and `?>`, embedded question marks,
target/data boundaries, split input and EOF in all five states.

H3 adds, only when implemented:

```csharp
internal enum HtmlTextMode { Data, RcData, RawText, ScriptData, PlainText }
internal void SetTextMode(HtmlTextMode mode, string? appropriateEndTagName);
```

The setter is legal only between emitted tokens, before tokenization resumes, and never in a partial
token or terminal session. Require the appropriate end-tag name for RcData/RawText/ScriptData; the tree
builder supplies it. A matching end tag returns to Data as specified. H3 tests no premature close for
`</scriptx>`, script escapes/double escapes, entity decoding only in applicable states, and EOF in every
new state. Do not add this method in H2 as an unsupported-mode stub.

### H1/H2 acceptance

- Literal complete-input tokens for valid and malformed tags, attributes, comments and doctypes;
  expected tokens are written independently, not generated by the implementation under test.
- Every split position of short fixtures, including references, CRLF and supplementary characters;
  multiple deterministic chunk partitions and quota values produce equivalent token streams.
- NeedInput/final/EOF/Complete, repeated reads, final empty input, append-after-final and invalid quota.
- Duplicate attributes, missing/empty doctype identifiers, bogus comments, numeric references and
  longest named-reference matching in text versus attributes.
- Cancellation before work and deterministic cancellation/limit checks during long scans; terminal
  session handling; records with diagnostics disabled allocate no diagnostics collection.
- Growing token/text fixtures validate operation counts or allocation behavior for linear accumulation;
  no benchmark speed claim from a unit-test stopwatch.

Old pinned html5lib fixtures can predate HTML PI tokens. Annotate only the exact conflicting cases with
their fixture identity, the current-spec reason and a corresponding replacement test; preserve the
upstream payload. Do not discard a whole file, suppress unrelated diagnostics or claim those old
expectations now pass. Keep the executed/excluded/replacement census reviewable.

Port a bounded, pinned tokenizer fixture subset appropriate to these states; do not alter existing WPT
vendor data or claim the tree-construction corpus passes. Run new tests in Release on both frameworks.
Do not extend the benchmark project owned by the separate benchmark task; send it representative inputs.

## CSS task: C1 syntax, without CSSOM or property validation

Deliver the complete CSS Syntax token stream and component-value construction, then the four public
construct parsers. CSS parsing initially consumes one `string`; resumable network CSS is not required
for C1. Use an iterative stack for blocks/functions and bounded cancellation checks. CSS parsing must
not reference DOM classes, HTML tokenizer internals, `Browser`, Jint or AngleSharp.

The only public facade methods this task adds are:

```csharp
public static partial class MarkupParser
{
    public static CssRuleSyntax ParseCssRule(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default);
    public static CssDeclarationSyntax ParseCssDeclaration(string source,
        CssParseOptions? options = null, CancellationToken cancellationToken = default);
    public static CssComponentValue ParseCssComponentValue(string source,
        CssParseOptions? options = null, CancellationToken cancellationToken = default);
    public static CssComponentValueList ParseCssComponentValues(string source,
        CssParseOptions? options = null, CancellationToken cancellationToken = default);
}
```

Syntax types live in namespace `Jint.HtmlParser.Css`. They are immutable owned results. Expose read-only
collections that callers cannot cast to a writable backing array. Do not expose a pooled buffer, a
tokenizer enumerator whose values change on advance, or a source span that requires retaining an input
lease. The task owns their concrete representation; use value tokens and compact arrays where useful
instead of a heap object per punctuation token. The exact initial public getter contract is:

| Type | Observable data |
| --- | --- |
| `readonly struct CssSourceSpan` | `int Start`, `int Length` in original UTF-16 input |
| `readonly struct CssComponentValue` | `CssComponentKind Kind`, `CssSourceSpan Span`, `CssToken Token`, `string FunctionName`, `char OpeningDelimiter`, `CssComponentValueList Values` |
| `readonly struct CssToken` | `CssTokenKind Kind`, `CssSourceSpan Span`, `string Text`, `string NumberText`, `string Unit`, `char Delimiter`, `bool IsInteger`, `bool IsIdHash`, `int UnicodeRangeStart`, `int UnicodeRangeEnd` |
| `sealed class CssComponentValueList : IReadOnlyList<CssComponentValue>` | `int Count`, `CssComponentValue this[int index]`, enumeration in input order; empty valid |
| `sealed class CssRuleSyntax` | `CssRuleKind Kind`, `string Name`, `CssComponentValueList Prelude`, `CssComponentValue? Block`, `CssSourceSpan Span` |
| `sealed class CssDeclarationSyntax` | `string Name`, `CssComponentValueList Value`, `bool IsImportant`, `CssSourceSpan Span` |

All getters are read-only and construction is internal. `CssComponentKind` is `None` (default struct),
`Token`, `Function`, `SimpleBlock`. `Token` is readable only on token values; `FunctionName` only on
functions; `OpeningDelimiter` only on blocks; `Values` only on functions/blocks. A mismatched access
throws `InvalidOperationException`, including all payload reads from `None`. `Kind` and `Span` remain
readable on a default value. `CssRuleKind` has `AtRule` and `QualifiedRule`; `Name` is empty for a qualified
rule. Rule `Block`, when present, is a brace SimpleBlock. An at-rule ending with a semicolon has no block.

`CssTokenKind.None` represents a default token. Token fields not applicable to its kind return empty
strings, `'\0'`, false or zero. `Text` is the decoded identifier/name/string/url/hash or whitespace content;
numeric spelling is `NumberText`, with `Unit` only for dimensions. Punctuation uses `Delimiter` for
the character; multi-character CDO/CDC have their own kind. `IsInteger` is the tokenizer's number type,
not a later integrality calculation. `IsIdHash` is meaningful only for hash tokens. A source span
covers the actual token/component/rule including its delimiters where present; recovered EOF adds no
imaginary source units. Declaration span excludes surrounding whitespace and its optional terminator.

Token kinds cover CSS Syntax's ident/function/at-keyword/hash/string/bad-string/url/bad-url/delim,
number/percentage/dimension, `UnicodeRange` in its specified declaration context, whitespace,
CDO/CDC, colon/semicolon/comma and all bracket types. Internal EOF is not returned as a component value.
Decoded text and numeric spelling are separate: C1 need not round a CSS number to `double` or validate
units. The union access and token defaults above distinguish the absence of a payload from empty text.
Keep raw token spelling only where required for correct preservation/serialization, without pinning
an unrelated large source string through a tiny independently returned syntax object.

`UnicodeRangeStart` and `UnicodeRangeEnd` expose the integer endpoints from
[CSS Syntax §4.3.14](https://drafts.csswg.org/css-syntax-3/#consume-unicode-range-token), returning zero
for other kinds and default tokens. Do not clamp to Unicode's maximum or reorder reversed endpoints:
C1 reports syntax, and descriptor validity is later work. For this kind only, `Text` owns the exact
short raw token spelling (case, zeros and wildcard spelling included); `Span` uses original source
offsets. Other kinds retain their decoded Text contract; this adds no universal raw-token copy.

When a declaration's **decoded** name matches `unicode-range` ASCII-insensitively,
[§5.5.6](https://drafts.csswg.org/css-syntax-3/#consume-declaration) and
[§5.5.11](https://drafts.csswg.org/css-syntax-3/#consume-a-unicode-range-value) require retokenizing its
original value text with unicode ranges allowed. Retain source positions through that operation;
never reconstruct input by concatenating generic tokens/components. Ordinary component entry points,
other declarations and `--unicode-range` do not enable that mode. Preserve declaration importance
handling and comment/escape boundaries. Tests cover escaped/mixed-case declaration names, wildcard
and explicit endpoints, reversed/out-of-range endpoints, exact Text/Span, and the same input through
the ordinary component API producing ordinary tokens.

Rule/declaration names preserve spelling for custom syntax; grammar matching uses ASCII-insensitive
comparison only where specified. Unknown at-rules and property names remain inspectable syntax.
`red nonsense`, unsupported units and unknown properties may be structurally valid component values;
there is no `IsSupported` property on these syntax objects. C5 supplies property grammar results later.

Failure/recovery contracts:

- All required null inputs throw `ArgumentNullException`; cancellation and limit failures keep their
  shared exception types and are never converted to `CssParseException`.
- Single component: ignore surrounding whitespace for the cardinality check, parse one token/function/
  block, and throw `CssParseException` on no component or extra non-whitespace components.
- Component list: preserve whitespace tokens, omit comments according to tokenization, allow empty,
  and apply specified bad-string/bad-url/unmatched-block/EOF recovery. Diagnostics do not toggle it.
- Single rule: a structurally valid at-rule or qualified rule, with exactly one result and no trailing
  non-whitespace input. Preserve unknown at-rules. A missing required qualified-rule block fails;
  EOF closes a started block as the syntax algorithm prescribes. This is not CSSOM insertion validation.
- Single declaration: require identifier then colon, extract trailing `!important` as specified,
  preserve custom-property case, and enforce one declaration. The standalone wrapper permits one
  terminating semicolon and surrounding whitespace but rejects a second declaration/trailing content.
  It does not reject `color: not-a-color` merely because property validation will fail later.

### C1 acceptance

- All token categories and escapes, numeric exponents/dimensions, Unicode input, URL versus quoted-url
  function parsing, comments, strings/newlines and bad tokens, with explicit expected structures.
- Balanced/nested function and block construction, mismatched closers, EOF recovery, unknown at-rules,
  single-item cardinality, declaration importance/case, and preservation of whitespace/value order.
- Positive and negative structural tests showing invalid property values are still parseable syntax;
  no test labels that syntax success as `CSS.supports` or valid computed style.
- Caller-owned results survive subsequent parse calls, source release and GC; read-only collections
  cannot mutate another result. Independent parallel calls share no mutable parse state.
- Resource limits and cancellation on deep/long input; explicit-stack parsing avoids native-stack
  failure. Diagnostics-disabled and no-host operation use no Browser/DOM dependency.
- Narrow public API snapshot for these implemented methods/types only, plus a direct no-friend public
  usage test. Release tests on both target frameworks after merging the foundation.

## Handoff and integration order

Both tasks may write/test their independent algorithms now. Foundation lands first; each task rebases
or cherry-picks that commit, adapts only at the agreed shared context boundary, and runs its real project
tests before sending a feature commit. Integrate HTML and CSS independently. They do not wait on one
another and must not acquire a shared tokenizer utility merely because both scan text.

The coordinator owns the final method/type spelling reconciliation and package documentation. No public
`ParseHtml`/CSSOM API is published until its implementation is real. Tokenizer success is the H1/H2 gate;
syntax construction is the C1 gate. Neither is evidence that the browser is ready to replace AngleSharp.

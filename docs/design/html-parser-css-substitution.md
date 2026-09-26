# CSS V0c: substitution syntax and the execution boundary

Design for independent review, 2026-09-23. This refines V0c in
`html-parser-css-values.md` and C5/C6 in `html-parser-cssom.md`. Implement an internal
source-preserving analyzer first; it is useful without a property registry or cascade.
It does not implement computed styles or claim that a property containing its result is supported.

## 1. Evidence and an observable migration decision

Primary sources checked on this date:

- [CSS Variables 1 §§2–4](https://drafts.csswg.org/css-variables-1/): names are case-sensitive;
  empty values differ from guaranteed-invalid; CSS-wide keywords have cascade semantics.
  The current draft supports dynamically substituted variable names and short-circuit fallbacks.
  Exact custom-property spelling, including comments, needs retained source.
- [CSS Values 5 Appendix A](https://drafts.csswg.org/css-values-5/#arbitrary-substitution):
  argument grammar, early substitution/spread, guarded substitution contexts, replacement,
  deferred property validation and expansion limits. These are current algorithms, not the
  historical unconditional dependency-graph algorithm.
- [CSS Environment Variables 1 §3](https://drafts.csswg.org/css-env-1/#using): environment
  names, integer indices, fallback and property/descriptor applicability. Unknown names or
  unavailable indices select the fallback. The draft still has unresolved timing/context notes.
- [CSS Syntax 3](https://drafts.csswg.org/css-syntax-3/#typedef-declaration-value) and
  [Values 4 URLs](https://drafts.csswg.org/css-values-4/#urls): component grammar and unresolved URLs.

Existing consumers are `Jint.Browser/Dom/Views/CustomProperties.cs` and
`CssCascade.cs` (`Traversal.Of`, `Compute`, `ICssComputeContext` adapter). They combine
AngleSharp values with per-element inheritance, shorthand resolution, and request-local caching.
`CustomProperties.Enter` currently adds all fallback references to Tarjan's graph;
`ResolveValue` uses first-resolvable-reference behavior rather than general token substitution.
`Jint.Tests.Browser/Views/CustomPropertyTests.cs` lines 12–14 specifically invalidate unused
fallback cycles. Those expectations conflict with current short-circuit semantics: with
`--present:red; --a:var(--present,var(--a))`, the unused fallback must not make `--a` cyclic.
Record and test that deliberate correction during Browser migration; neither copy that graph nor
change Browser tests in this first slice. Parent aliases must still inherit their already-computed
value, rather than re-resolving against a child's overrides.

## 2. Finite dispatches and ownership

| Slice | Concrete deliverable | Dependency / boundary |
| --- | --- | --- |
| V0c1 (dispatch now) | Immutable source input, var/env argument analysis, custom-value classification, ordered potential references, exact source retention | Existing C1 and `CssValueWork`; no math/C4/Browser changes |
| V0c2 | Ordinary URL token and quoted `url()` matcher with unresolved decoded text; explicit modifier/function dispositions | V0c1 source input; separate finite URL grammar review |
| V0c3 | Source attachment to C1 declarations and C4 edits, exact custom specified serialization and pending shorthand attachment | Shared syntax/model owner; real C5 property/shorthand consumers |
| C6s (separate dispatch after review) | Bounded var/env replacement over an explicit per-element snapshot, dynamic guards, selected fallbacks, expansion accounting | V0c1; explicit execution contract below; no cascade implementation hidden inside it |
| Later C5/C6/R6 | Other arbitrary substitutions, registered properties, cascade/inheritance, taints, computed properties, Browser migration | Individual complete family slices and their actual host dependencies |

V0c1 groups var/env analysis because both need the same argument/range representation; it does
not implement env device values. This deliberately refines the earlier URL+var-first grouping.
Do not implement empty resolver methods or placeholder URL/registered-property classes.
Keep all production additions under `Jint.HtmlParser/Css/Values/References/` and tests under
`Jint.Tests.HtmlParser/Css/Values/References/`. Both net8.0 and net10.0 remain mandatory.
No public API or snapshot changes, project dependencies, mutable sheet references or Browser types.

## 3. V0c1 exact internal surface

New files: `CssReferenceInput.cs`, `CssReferenceAnalysis.cs`, `CssReferenceParser.cs`,
`CssReferenceOccurrence.cs`, `CssCustomPropertyValue.cs`. Types/members are internal.
Reuse `CssWideKeyword`, `CssSourceSpan`, `CssComponentValueList`, `CssValueWork` and C1 tokens.

```csharp
sealed class CssReferenceInput
{
    static CssReferenceInput Parse(string valueText, CssParseOptions? options,
        CancellationToken cancellationToken);
    string Source { get; }                 // immutable original UTF-16 text
    CssComponentValueList Components { get; }
}
enum CssReferenceUse { PropertyValue, CustomPropertyValue, DescriptorValue }
enum CssReferenceAnalysisKind { Uninitialized, Literal, Deferred, InvalidSyntax, PendingFeature }
readonly struct CssReferenceAnalysis
{
    CssReferenceAnalysisKind Kind { get; }
    CssReferenceProgram Program { get; }   // guarded: only Literal/Deferred
    CssSourceSpan Span { get; }            // offending construct for unsuccessful result
    string? PendingFunction { get; }       // only PendingFeature
}
static class CssReferenceParser
{
    static CssReferenceAnalysis Analyze(CssReferenceInput input,
        CssReferenceUse use, CssValueWork work);
    static CssCustomPropertyResult ParseCustomProperty(string decodedName,
        CssReferenceInput input, CssValueWork work);
}
```

Co-locate the program with analysis and the custom result with custom value. Do not add one file
per enum. `Parse` calls C1 `ParseComponentValues` once with existing original-input/token/depth
limits; it must retain that exact source and its immutable components. No per-child parsing.
Snapshot applicable limit values in the input; do not retain the caller's options or diagnostic
collector. `Analyze` uses that snapshot for its explicit frame bound. `CssValueWork` is supplied
per invocation, including by tests, and is never part of the immutable input.
The input is an isolated **priority-stripped value**, not a declaration or stylesheet. Thus
`!important` here is invalid value syntax; callers must obtain priority from declaration parsing.
Tests need only this real entry point, not a Browser or test-only tree builder. An internal
trusted capture overload for C1-owned components/source belongs to V0c3, not this dispatch.

`Analyze` is whole-input and syntax-only. Literal means no implemented substitution requiring
deferral, not that an unknown function or property grammar is valid. Deferred means supported
var/env argument syntax was found; it is not a resolved value. InvalidSyntax is author grammar
failure. PendingFeature is internal unfinished work, never CSS invalidity or a successful result.
Default structs cannot expose an accepted payload. Validate enum/programmer arguments separately;
cancellation/resource exceptions propagate rather than becoming any result kind.

`CssReferenceProgram` is a sealed immutable owner containing the input and an owned occurrence
array with indexed count/access (no array escape). One readonly occurrence records:
kind Var/Env; original function span; parent occurrence index (-1 if none); first-argument range;
fallback-present bit plus fallback range; optional statically valid decoded name; and flags for
dynamic header / early substitution / nested fallback. A range references a component list plus
start/count and a source span; it never copies a sibling prefix. Empty fallback and absent fallback
must remain distinguishable. The program owns all reachable sources/lists; no mutable work survives.
Occurrence order is depth-first source order, including nested headers and unused fallbacks.
Record duplicates, rather than building a hash set per function. A future consumer can deduplicate
once per operation. Static names are an optimization, never the sole representation of a header.

`CssCustomPropertyResult` has Uninitialized, Value, WideKeyword, InvalidSyntax and PendingFeature
states, guarded payloads and the offending span. Value carries the exact decoded name, input and
reference program, including a valid empty component sequence. WideKeyword carries the existing
keyword and retained source. There is no parse-time GuaranteedInvalid state: `initial` is a cascade
instruction, and an unknown variable is not looked up here. `decodedName` comes from an identifier
token/declaration name; require two leading hyphens and at least one following code point, preserving
case and Unicode exactly. It is not the `<custom-ident>` production.

## 4. Argument parsing and contextual decisions

Match function names using ASCII-insensitive comparison of decoded names; preserve original names
and spelling. Walk functions/simple blocks iteratively. Strings and URL tokens are atomic: quoted
`"var(--x)"` and URL data do not introduce references. Unknown ordinary functions remain ordinary,
but references inside their component lists are discoverable. Nothing authorizes `foo()` as a
supported property value. Function EOF closure already accepted by C1 is not newly rejected merely
because `IsClosed` is false; a raw unmatched closing token is different.

For var/env, split at the first immediate comma; all following components, including further commas,
are fallback. No comma means absent fallback. Require a nonempty first argument under the current
argument grammar, and validate declaration-value restrictions. Preserve nested first arguments:
`var(var(--name), red)` is Deferred. `var(foo,red)` has argument syntax but a statically wrong normal
name; it must not become an early InvalidSyntax just because final name parsing will fail.
The normal-name failure is handled during execution and can select fallback. Likewise keep env
headers whose name/index grammar requires substitution. Do not parse var's first argument as a
single identifier before arbitrary substitution.

Lexical bad-string/bad-url tokens, unmatched closers and forbidden declaration-value tokens are
failures. Top-level `!` and semicolon are forbidden in a declaration value, while balanced blocks
have their normal nested grammar. Check recognized nested var/env argument syntax without using
potential dependencies to make runtime cycle decisions. Empty custom values and empty fallback
are allowed; an empty required first argument is not. CSS-wide recognition applies only to the
entire custom value, ignoring CSS whitespace/comments, never to substrings or a var fallback.

Spread is three `.` delimiter tokens followed by a nested arbitrary function, with no intervening
whitespace token. Comments do not break this token adjacency: `var(.../**/var(--args))` and
`var(./**/../**/var(--args))` recognize spread; `var(... /**/var(--args))` does not.
Retain comments in source but never use raw source-span gaps to reject spread. Record its location and
retain the unsplit function children as the authoritative pre-execution input: early replacement
can change where the first comma lies. Static argument ranges are provisional when spread occurs;
do not report definitive argument-grammar failure that early replacement can repair. No early
replacement is executed by V0c1. Outside an arbitrary function's argument context, periods are
ordinary components. Add fixtures distinguishing these contexts.

Context is explicit: var is allowed in property/custom-property values; env also in descriptor
values. In a descriptor, any actual var occurrence fails this supported context. This does not
enable env in selectors, media syntax or rule construction; those require separate reviewed callers.
Known later families `attr`, `if`, `inherit`, `ident`, `random-item`, and dashed custom functions
return PendingFeature in this analyzer, including when nested. Keep a checked family inventory;
later scopes may refine contexts and lazy validation. Ordinary unknown functions do not receive
that classification. This gate is deliberately unavailable as a public CSS.supports answer.

Potential dependency data contains literal-name references when available and dynamic-header flags
otherwise. An env dependency includes the entire header range, not a truncated Int32 array: valid
nonnegative CSS integers can exceed machine indexing bounds. No per-device index availability or
existence check occurs here. C6s may fast-path bounded indices but must fall back honestly for large
ones. Potential references are useful for invalidation and inspection, not a proven execution graph.

## 5. Source retention and the actual C1/C4 prerequisite

Today C1 components retain decoded tokens and spans but discard comments; CssDeclarationSyntax
does not retain its source or a priority-stripped value span. C4 stores those declarations and
serializes their tokens. Therefore exact custom-property source cannot be recovered from C4 alone.
V0c1 retains its standalone value source; it must not pretend C4 already satisfies that contract.

V0c3 must attach immutable original source plus a precise value range before C1 trims components
or removes importance. The range includes retained comments and respects declaration whitespace
rules; it excludes `!important`, colon, semicolon and neighboring declarations. Each inserted or
replaced C4 declaration captures its own source owner, surviving later sheet edits. Empty values,
comment-only values, trailing comments before importance and EOF closure require explicit tests.
That work belongs to the existing syntax/model owner. V0c1 exposes raw source retention, not a
false CSSOM serializer. V0c3 must implement the custom serialization rule, including the empty-value
special case; ordinary C4 token normalization cannot silently substitute for it.

Mixed-source replacement must carry source provenance per shared piece; do not assign introduced
tokens fake offsets into the consuming declaration. Unresolved URLs stay unresolved until the
appropriate style/property context exists. Defining-sheet versus consuming-declaration base rules
must be pinned by URL/cascade tests before Browser attachment, not guessed from System.Uri.

## 6. C6s: bounded execution dispatch

Execution addendum, 2026-09-25; current Variables 1, Values 5 Appendix A and Env 1 §3 rechecked.
Implement var/env replacement now against immutable, already-selected snapshots. Browser supplies
the snapshots; this core has no DOM, AngleSharp, device callbacks, mutable sheets or cascade selection.
C4 source attachment is not a prerequisite for executing standalone `CssReferenceInput` values.
Successful output still requires the consuming C5 grammar; it does not choose a lower cascade winner.

### 6.1 Finite surface and ownership

All declarations below are **internal**, including constructors, factories and getters. Own only new
files under `Css/Values/References/` and matching tests. No public facade or C1/math signature change.

| File | Types and work |
| --- | --- |
| `CssSubstitutionSnapshot.cs` | `CssSubstitutionSnapshot`, readonly `CssSubstitutionBinding` and its state enum; copied custom-name table |
| `CssEnvironmentSnapshot.cs` | `CssEnvironmentSnapshot`, sealed `CssEnvironmentBinding`; owned exact index keys and literal source values |
| `CssSubstitutionResult.cs` | Result/context structs and result enum below; guarded payloads |
| `CssSubstitutedValue.cs` | Immutable segment owner, C1 projection and origin structs below; iterative final materialization |
| `CssSubstitutionExecutor.cs` | Execution frames, per-call memoization and active-context guards |
| `CssSubstitutionArguments.cs` | Argument splitting after early substitution, wrapper removal and final name/index matching |

Permit narrow extraction of existing predicates from `CssReferenceParser.cs` into the argument helper;
preserve every V0c1 outcome. Do not modify C1, C4, math, shared options or Browser in this dispatch.
The new factory surface is:

```csharp
enum CssSubstitutionBindingKind { Uninitialized, Specified, Computed, Invalid, Pending }
readonly struct CssSubstitutionBinding
{
    static CssSubstitutionBinding Specified(string name, CssReferenceInput input, bool animationTainted);
    static CssSubstitutionBinding Computed(string name, CssSubstitutedValue value, bool animationTainted);
    static CssSubstitutionBinding Invalid(string name, bool animationTainted);
    static CssSubstitutionBinding Pending(string name, string feature);
    // Kind, Name and guarded Input/Value/AnimationTainted/PendingFeature getters.
}
sealed class CssSubstitutionSnapshot
{
    static CssSubstitutionSnapshot Create(ReadOnlySpan<CssSubstitutionBinding> bindings, CssValueWork work);
    bool TryGet(string name, CssValueWork work, out CssSubstitutionBinding binding);
}
sealed class CssEnvironmentBinding
{
    static CssEnvironmentBinding Create(string name, ReadOnlySpan<string> indexSpellings,
        CssReferenceInput literalValue, CssValueWork work);
}
sealed class CssEnvironmentSnapshot
{
    static CssEnvironmentSnapshot Create(ReadOnlySpan<CssEnvironmentBinding> bindings, CssValueWork work);
    bool TryGet(string name, ReadOnlySpan<string> canonicalIndices,
        CssValueWork work, out CssReferenceInput? value);
}

enum CssSubstitutionResultKind { Uninitialized, Tokens, GuaranteedInvalid, PendingFeature }
readonly struct CssSubstitutionContext
{
    CssSubstitutionContext(string propertyName, CssReferenceUse use, bool isAnimatable);
    string PropertyName { get; }
    CssReferenceUse Use { get; }
    bool IsAnimatable { get; }
}
readonly struct CssSubstitutionResult
{
    CssSubstitutionResultKind Kind { get; }
    CssSubstitutedValue Value { get; }       // only Tokens
    string PendingFeature { get; }          // only PendingFeature
}
static class CssSubstitutionExecutor
{
    static CssSubstitutionResult Resolve(CssReferenceInput input,
        CssSubstitutionSnapshot customProperties, CssEnvironmentSnapshot environment,
        CssSubstitutionContext context, CssValueWork work);
}
```

Reject null/default payloads, duplicate normalized keys,
invalid decoded names and invalid enum/context arguments as programmer errors. An absent custom key
means missing; an `Invalid` binding is the explicit guaranteed-invalid value. `Computed` is an already
resolved inherited leaf, including empty output, with its source owners retained. It is never
re-evaluated against child overrides. Resolve CSS-wide custom declarations at the caller's cascade
boundary into inherited/computed, invalid or pending bindings; do not store `initial` as literal text.
Names arrive from accepted identifier tokens: custom names use V0c1's `--` plus nonempty suffix rule;
environment names must be nonempty and exclude CSS-wide keywords and `default` in ASCII case variants.
Do not reparse decoded spelling, which may legitimately contain escaped characters. Context names
are ASCII-canonical for ordinary properties and unchanged for custom properties; require a custom
name and `isAnimatable=true` for `CustomPropertyValue`, and false for `DescriptorValue`.

### 6.2 Actual component projection and diagnostic origins

Use C1's existing internal `CssToken` constructor, `CssComponentValue.FromToken/FromContainer` and
`CssComponentValueList` constructor. Copy token payload fields unchanged; reuse immutable decoded
strings. Rebuild replaced containers iteratively. No text serialization, token concatenation or
retokenization is involved. `var(--n)px` therefore remains two tokens.

```csharp
sealed class CssSubstitutedValue
{
    CssComponentValueList Components { get; }
    int TokenCount { get; }
    int SpellingLength { get; }
    CssSourceOriginRange OriginsFor(CssSourceSpan projectionSpan);
}
readonly struct CssProjectedTokenOrigin
{
    CssSourceSpan ProjectionSpan { get; }
    CssReferenceInput Source { get; }
    CssSourceSpan SourceSpan { get; }
    bool IsSyntheticCloser { get; }
}
readonly struct CssSourceOriginRange
{
    int Count { get; }
    CssProjectedTokenOrigin this[int index] { get; }
}
```

Projection spans use a **separate virtual coordinate space**: contiguous intervals starting at zero,
whose lengths are the spelling units defined below. They are neither source offsets nor serialized
CSS offsets. There is no backing virtual string. Every lexical occurrence has an ordered origin entry;
a rebuilt container's projection span covers its opener, children and closer. Original C1 nodes are
not mutated. The origin entry holds the real input owner and real source span; repeated insertions
have distinct projection intervals even when their original owner/span is identical. This explicitly
adapts section 5's provenance rule to existing C1 readers without counterfeiting consumer offsets.

`OriginsFor` validates bounds and returns an immutable indexed view of every token interval overlapping
a nonempty projection span, using binary searches; it never collapses a mixed-source range into one
original span. A zero-length diagnostic yields an empty view, including empty-input default spans;
the caller then uses its declaration anchor. An inserted EOF closer maps to a zero-length original
span at that source container's end and has `IsSyntheticCloser=true`. Other entries map to actual
token/opener/closer spans. Store no original ancestor-sized substring. Projection/origin arrays never
escape mutably; shared segments retain only immutable owners, not an execution builder or work state.

Feed `result.Value.Components` directly to `CssPrimitiveParser` and an appropriate function component
directly to `CssMathParser.ParseMath(component, context, work)`. Their returned `Span` and accepted
payload spans are now projection-relative. Retain the `CssSubstitutedValue` beside that parse result
and call `OriginsFor` before presenting an original-source diagnostic. Never slice `input.Source`
with such a span or hand this projection back to `CssReferenceInput.Parse`. Existing source-only
primitive/math callers retain their current span semantics. Test this adapter with both a token
error from an introduced value and a whole-function error spanning multiple original sources.

### 6.3 Expansion and work bounds

The internal ceilings are **65,536 lexical occurrences** and **1,048,576 spelling UTF-16 units**,
inclusive. This is the chosen implementation policy under
[Values 5's expansion limit](https://drafts.csswg.org/css-values-5/#long-substitution), not a public
option or XML resource setting. Define the metrics precisely:

- A leaf C1 token, including whitespace, contributes one occurrence and `Token.Span.Length` units.
- A retained function contributes one function opener token (its original raw name through `(`),
  its children's contributions, and one closer token. A simple block contributes one opener,
  children, and one closer. Each closer contributes one unit even when C1 accepted EOF closure.
- Determine each function opener's end once from its original input, using escaped-name-aware
  scanning; do not use the whole container span as its spelling length. Explicit brackets use one
  unit. EOF closers are synthetic as specified above. Discarded comments contribute neither metric.
- Removed substitution functions, spread periods and syntactic argument wrappers contribute nothing
  to their replacement. Every repeated output occurrence counts again despite segment sharing.

Apply both ceilings to each replacement and each completed root result, including literal roots and
environment leaves. Oversize replacement is guaranteed-invalid at that replacement point, so an
outside var fallback can recover; a root that remains oversized returns `GuaranteedInvalid`. Use
saturating addition at ceiling+1 before allocation/materialization. An oversize unused fallback is
not expanded merely to count it. Memoize segment lengths; do not flatten each intermediate alias.
One final iterative materialization builds the C1 projection and origins in bounded linear work.

Use the same `CssValueWork` for scans, snapshot copies, key comparisons, frames and publication;
charge examined characters/entries, poll around growth/copies and before returns. Long names/indices
must not hide an unpolled hash/equality scan: use charged ordinal hashing/comparison for these tables.
Snapshot factories copy supplied arrays/tables and retain immutable strings/inputs only. Cancellation
and existing `ParseLimitException` propagate. Store precomputed hashes/buckets without a comparer
capturing the factory's work state; subsequent lookup receives the current call's work explicitly.
The root input's `MaxNestingDepth`, if nonzero, also bounds the materialized component nesting;
an alias-call chain is not component nesting and uses
explicit frames. Exceeding that depth throws the existing nesting limit, not a CSS invalid result.

### 6.4 Lookup, pending work and taint

Snapshot identity plus decoded property name identifies an active substitution context; one Resolve
call owns its mutable guards/cache. Resolve early invocations before re-dividing arguments, preserving
the reviewed spread/ordinary-container distinction. Re-entry marks all active cycle participants;
their own fallback cannot rescue them, while an outside dependent can recover. Resolve dynamic
headers before final var/env name parsing. Ordinary failed name parsing can choose a fallback.
Memoize only completed custom-property results, never a provisional active frame. No static dependency
graph decides cycles and no memoized state survives into another Resolve call.

Environment keys compare decoded names ordinally and include the complete ordered index vector.
Accept only integer spellings `[+-]?[0-9]+`; remove `+` and leading zeros, normalize every spelling of
signed zero to `"0"`, reject negative nonzero integers, decimals and exponents. Store canonical digit
strings, never Int32/Int64 indices or delimiter-joined composite keys. Canonical duplicate keys are
factory errors. Runtime header grammar failure or absent exact key selects fallback. The binding
factory requires a V0c1 `Literal` value (including empty); substitutions, bad syntax and pending
functions in supplied device values are programmer errors. Share its immutable original input.
Lookup occurs when the env invocation is reached; no eager env pass. A new device state requires a
new snapshot. This defines the executor's boundary, not the unresolved Browser scheduling policy in
[Env 1](https://drafts.csswg.org/css-env-1/#using).

Analyze a reached source once per call using V0c1. `InvalidSyntax` supplied as a root/specified binding
is a caller precondition failure (`ArgumentException`); callers must perform declaration validation
before execution. A header that becomes invalid after early replacement instead yields
`GuaranteedInvalid`. A V0c1 `PendingFeature` source returns pending conservatively for that whole
source, even when the unsupported function is textually in an unused fallback. This preserves the
existing analysis gate; it is implementation debt, never CSS invalidity. A missing/invalid binding
may choose fallback; a reached pending binding/source must propagate pending without trying fallback.
Unreached pending bindings do not poison another request. Registered values enter as explicit
`Pending(name, "registered-property")`; no false unregistered execution. V0c1 remains unchanged.

`animationTainted` is **final transitive metadata supplied by the snapshot caller**, not merely a
local @keyframes bit. Preserve it on inherited bindings; do not infer it from only evaluated fallback
branches. If the Browser caller cannot determine it, supply a pending binding with feature
`"animation-taint"` rather than guessing false. The current
[Variables 1 replacement rule](https://drafts.csswg.org/css-variables-1/#using-variables) checks this
bit against whether the consuming property is animatable. A tainted lookup in a non-animatable
property yields guaranteed-invalid and may select fallback. Do not use an `animation-*` name test.
While resolving a specified custom binding, use that custom property's own animatable context;
apply the outer property's eligibility to the resulting var lookup. This prevents caller eligibility
from corrupting cached custom values. Descriptor context uses `isAnimatable=false`; V0c1 still rejects
var in descriptors. The Boolean for ordinary properties comes from real consumer metadata, not a
new incomplete registry inside this executor.

### 6.5 Acceptance and Browser handoff

New matching tests: `SubstitutionValueTests.cs`, `SubstitutionCycleTests.cs`,
`SubstitutionSpreadTests.cs`, `SubstitutionEnvironmentTests.cs`, `SubstitutionOwnershipTests.cs`,
`SubstitutionWorkTests.cs`, `SubstitutionConsumerTests.cs`; extend `FIXTURES.md` with actual provenance.
Require independent expected results for empty/missing/invalid/pending distinctions; case and dynamic
names; both provisional-wrapper regressions; selected/unselected cycles and outside recovery;
inherited aliases; exact env index normalization and dimensions; taint eligibility/transitive metadata;
and no invalidity-to-empty conversion. Test canonical-key duplicate rejection after caller mutation.

Probe exact ceilings and one past for both metrics, EOF closers, giant single tokens, repeated shared
segments, exponential fan-out and long alias chains. Verify nested output depth separately from alias
depth and cancellation during hash comparison, intermediate expansion and final publication. Check
mixed-source origin ranges, identical original offsets in different sources, repeated same-source
insertions, empty diagnostic ranges and immutable ownership after caller reassignment. Require direct
primitive/math consumption of substituted lengths and calc expressions, rejected dimensional/type
errors after substitution, and preserved percentages/ranges; never serialize/reparse for these tests.

Fresh Release focused and non-corpus parser tests on net8/net10, unchanged public API snapshots,
and independent review are required before integration. Browser's migration owner may then call this
core with its actual selected bindings/environment and revalidate the real consuming property;
`PendingFeature` must surface as a named native capability gap to fix, not a dropped declaration or
an AngleSharp fallback. CSSOM declaration source attachment and exact specified serialization remain
V0c3 work when that adapter needs them. Do not expand this dispatch into unrelated property families.

## 7. Work, tests and merge gates

V0c1 shares one `CssValueWork` across all analysis, header scans and result publication. Charge
examined components/characters and copied entries; poll long explicit loops at its existing cadence.
Poll before/after CLR allocations, growth and final copies, and before every return. Frame depth is
bounded by the C1 maximum against the original operation, not a fresh per-function allowance.
Source is immutable and can be shared; no substring per candidate, recursion, LINQ per token,
dictionary per node, parse-state retention or source rescans for each descendant. Require linear
work in source/components plus occurrences. Checkpoints are per invocation and retained by no result.

New tests: `ReferenceSyntaxTests.cs`, `CustomPropertyValueTests.cs`, `ReferenceOwnershipTests.cs`,
`ReferenceWorkTests.cs`, plus `FIXTURES.md` stating exact upstream assertions adapted. Cover:

- Case/escaped names, `--` rejection, composed/decomposed Unicode, empty/custom CSS-wide values;
  comments, literal URL/string contents, unknown ordinary functions, unmatched/bad tokens, EOF closure.
- `var(--a)`, `var(--a,)`, comma-rich/nested fallbacks; dynamic names; env scalar/indexed/dynamic
  headers; huge index spelling preserved; descriptor var rejection and env deferral.
- `var()`, `var(,red)`, header normal-grammar failure versus argument-grammar failure;
  spread adjacency and provisional splitting; known later functions report PendingFeature.
- Original source after input variable reassignment; no mutable array escape; default-result guards;
  linear work on doubled deep/wide inputs; cancellation during analysis and final publication using
  per-call deterministic checkpoints, not only pre-cancel tests; exact configured depth boundary.
- A source-backed census distinguishes literal/deferred/invalid/pending. The WPT pin remains
  `2136eb1501a106c42cd8977bb31c81b57b785bc8`; inspect actual cases before adaptation. Current draft
  dynamic-name/short-circuit/spread assertions are separately authored when absent from that pin.

C6s later adds direct token-boundary tests (`var(--n)px` cannot become a dimension by concatenation),
empty versus guaranteed-invalid fallbacks, selected/unselected cycles, alias inheritance, taint,
env misses and dimensions, expansion bounds, and actual primitive/math reparse. Preserve math
percentage context and ranges; do not invoke the current math parser before substitution and label
its failure final. Browser gates reuse CustomPropertyTests, cascade/layout mutation tests and
specified/computed CSSOM observations after the deliberate current-spec expectation corrections.

V0c1 acceptance is its complete internal parser/analysis contract on both TFMs with no public
surface change. URLs, execution, C4 exact source attachment, other substitutions, registered
properties and Browser adoption remain explicit work. No whole-CSSOM completion claim follows.

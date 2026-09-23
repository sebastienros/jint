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

## 6. C6s execution contract to refine before its dispatch

This section constrains the next design; add none of these executor types as V0c1 stubs.
Execution receives one immutable per-element snapshot after cascade selection: ordinal custom-name
to specified program or already-computed inherited value; explicit missing/guaranteed-invalid state;
animation-taint and the consuming property's eligibility; a separate immutable environment lookup.
No callbacks, DOM reads, parent traversal or cascade winner selection inside the token executor.
Registered values and other unsupported substitution families stop with internal PendingFeature,
not GuaranteedInvalid. The context identity distinguishes property + element snapshot.

Result states must distinguish Tokens (including zero tokens), GuaranteedInvalid and PendingFeature.
Successful replacement alone does not validate the consuming property's grammar. C5 reparses the
result against the actual longhand/shorthand grammar; failure is invalid at computed-value time,
not permission to retry a lower cascade candidate. Only C6 owns that fallback-to-initial/inheritance
behavior. Pending shorthands retain original identity until this real replacement and reparse.

Use explicit evaluation frames and an active-context index. Re-entry marks every active participant
in that cycle; an internal fallback cannot rescue a marked participant. An outside dependent can
recover with fallback. Evaluate only selected fallback branches. A conservative static dependency
graph cannot replace these guards. Resolve a dynamic header first, then parse its actual name/indices.
Inherited computed tokens are leaves, preventing child overrides from changing an inherited alias.
Environment timing remains a source-review gate for C6s because the current env draft has open notes;
do not introduce a global eager env pass that evaluates otherwise unused fallback branches.

The C6s dispatch must name a finite token-output ceiling and exact length metric. Count expanded
occurrences even when backing segments are shared; overflow of that CSS expansion ceiling yields
GuaranteedInvalid, not empty success. Host cancellation and parser resource errors still throw.
Before materialization, perform saturating length checks; use shared segments/DAGs and one final
linear flatten where required. No repeated prefix rebuilding across long alias chains. Tests must
include exponential fan-out, long chains, giant single-token source, and repeated references. This
is a new CSS execution bound, never XML's entity expansion setting or an unsolicited public option.

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

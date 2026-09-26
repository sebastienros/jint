# C5 common specified-value grammar foundation

Design for independent review, 2026-09-23. This refines V0 in
[the CSSOM design](html-parser-cssom.md), without changing its 433-registration completion baseline,
rule/descriptor obligations, or separation between syntax editors and validated CSSOM. The first
dispatch is **V0a: keyword, textual and numeric atoms**. It depends on merged C1 component values,
not C1's new list operations, C4a, selectors, DOM mutations, or Browser. All implementation APIs here
are internal. No new public facade, parser feature switch, CSSOM placeholder, or evaluator is implied.

## 1. Sources and the meaning of completion

Use the current primary specifications, checked on the date above, and record the source revision/date
beside each implemented grammar's fixture inventory:

- [CSS Values 4](https://drafts.csswg.org/css-values-4/): textual/numeric productions, unit tables,
  calculation syntax/type checking/ranges. Treat this as an evolving specification, not a browser
  compatibility table.
- [Cascade 5 explicit defaulting](https://drafts.csswg.org/css-cascade-5/#defaulting-keywords) and
  [all](https://drafts.csswg.org/css-cascade-5/#all-shorthand): the current draft includes `revert-rule`,
  in addition to `initial`, `inherit`, `unset`, `revert`, and `revert-layer`.
- [Variables 1](https://drafts.csswg.org/css-variables-1/),
  [Values 5 substitution](https://drafts.csswg.org/css-values-5/#arbitrary-substitution), and
  [Environment Variables 1](https://drafts.csswg.org/css-env-1/): substitution syntax and deferred values.
- [Color 4](https://drafts.csswg.org/css-color-4/) and
  [Color 5](https://drafts.csswg.org/css-color-5/): color forms and specified-value serialization.
- [CSSOM value serialization](https://drafts.csswg.org/cssom/#serialize-a-css-value) and
  [Syntax serialization](https://drafts.csswg.org/css-syntax/#serialization): distinct semantic and
  token-preserving responsibilities.

Keep a small checked inventory per finite task: production/function/unit name, source anchor, fixtures,
and implemented/pending disposition. Pending is implementation debt, never a successful rejection test.
Adopting `revert-rule` is an explicit current-spec addition to the original V0 keyword inventory;
recognizing it here does not implement its cascade behavior. New draft constructs encountered by later
owners must receive a named disposition, rather than accidentally inheriting an unknown-value fallback.

## 2. Representation and validation boundaries

Consume immutable C1 `CssComponentValueList`/`CssComponentValue` directly. Do not serialize and tokenize
again, split strings at commas, or add a parallel tokenizer. Read-only owned component lists can be
shared. Keep original `CssSourceSpan` provenance on resulting atoms/expressions. Small independently
retained values must not retain an entire stylesheet merely to obtain raw spelling.

Implement concrete readonly structs for atoms, sealed owned containers for variable-sized expressions,
and closed discriminants for unions. A default result is not success; a mismatched payload read throws.
No `object` payload dictionary, stringly typed expression tree, public subclasses, or extensibility
registry. Representation is specified-value state: relative units and percentages retain their meaning
until a real evaluator has the required environment.

There are two deliberately different result contracts:

1. A primitive matcher answers **Match/NoMatch for exactly its named production**, plus source span.
   It does not answer whether an entire property is supported or valid. `ParseNumericAtom(calc(...))`
   is NoMatch because the operation accepts an atom; no caller may turn that into a claim that `calc`
   is an invalid property value. Alternatives can try another production without exceptions.
2. The later property dispatcher answers `Valid`, `Deferred`, `Invalid`, `UnsupportedProperty`, or
   `UnimplementedGrammar`. `UnsupportedProperty` means a name not recognized in that declaration
   context; `UnimplementedGrammar` means a recognized obligation without its completed validator.
   The latter blocks public validated CSS completion and cannot be silently discarded as invalid.
   `Deferred` carries validated substitution syntax and the entire value, not a guessed typed value.

Do not implement the property dispatcher/result union as empty infrastructure in V0a. Add it when a
real property family and V0 substitution consumer exist. Exceptions remain reserved for programmer
arguments, resource limits and cancellation; malformed value alternatives use matcher results.

The eventual metadata key is `(declaration context, decoded name)`, not name alone. Ordinary names
and aliases use ASCII-insensitive lookup; custom-property names preserve case. Each completed family
contributes immutable entries with canonical name, aliases, grammar identity, permitted contexts,
inheritance/initial metadata where applicable, and shorthand longhands/reset-only members. Descriptor
entries do not fabricate inheritance or ordinary-property support. Style, keyframe, font-face, page,
margin, counter-style and registration contexts must not collapse into one global allowlist.
The enclosing context decides whether CSS-wide keywords and substitutions are permitted; recognizing
a keyword primitive must not automatically make it legal in a descriptor or every keyframe context.

Keep the 433-name assignment in the CSSOM appendix as the initial coverage authority. A checked
manifest joins each assigned name/context to its real implementation or explicit pending task. Do not
prepopulate missing initial values, validators returning false, or fake property IDs. `all` additionally
needs reset metadata: exclude `direction`, `unicode-bidi`, and custom properties. Expand against the
completed property registry later; V0a can test the exclusion predicate without inventing a registry.

## 3. First dispatch: V0a, complete atom primitives

Own only new files under `Jint.HtmlParser/Css/Values/` and tests under
`Jint.Tests.HtmlParser/Css/Values/`. Namespace: `Jint.HtmlParser.Css.Values`. Exact file ownership:

| File | Concrete responsibility |
| --- | --- |
| `CssPrimitiveResult.cs` | `readonly struct CssPrimitiveResult<T>`; `bool IsMatch`, guarded `T Value`, `CssSourceSpan Span`; internal success/no-match construction |
| `CssValueWork.cs` | One operation's cancellation/work counter, shared by sub-readers; no static state |
| `CssWideKeyword.cs` | Six-value keyword enum plus `None`; recognition and canonical keyword spelling |
| `CssTextValues.cs` | Identifier/string atom results; preserve decoded spelling and span |
| `CssNumber.cs` | Owned immutable decimal literal representation and exact comparison described below |
| `CssNumericAtom.cs` | Numeric category, unit identity, integer token flag, number, span |
| `CssUnits.cs` | Exhaustive finite unit table and categories for this dispatch |
| `CssNumericRange.cs` | Literal numeric bounds, open/closed endpoints; no computed-value clamping |
| `CssPrimitiveParser.cs` | Whole-list atom matchers below; no property dispatch |
| `CssAllReset.cs` | Case-aware `IsExcluded(string decodedPropertyName)` only |

The parser's exact internal entry points are static; `work` is the same instance for an enclosing
grammar operation. Inputs already belong to C1 and contain no EOF token:

```csharp
CssPrimitiveResult<CssWideKeyword> ParseWideKeyword(CssComponentValueList values, CssValueWork work);
CssPrimitiveResult<CssIdentifierValue> ParseIdentifier(CssComponentValueList values, CssValueWork work);
CssPrimitiveResult<CssIdentifierValue> ParseCustomIdentifier(CssComponentValueList values,
    ReadOnlySpan<string> excludedKeywords, CssValueWork work);
CssPrimitiveResult<CssIdentifierValue> ParseDashedIdentifier(CssComponentValueList values, CssValueWork work);
CssPrimitiveResult<CssStringValue> ParseString(CssComponentValueList values, CssValueWork work);
CssPrimitiveResult<CssNumericAtom> ParseNumericAtom(CssComponentValueList values, CssValueWork work);
```

Each permits surrounding whitespace and requires exactly one appropriate non-whitespace token;
extra tokens, functions, blocks, bad strings and bad URLs are NoMatch. Comments were handled by C1.
The NoMatch span is the first offending component. With no component, it is a default span indicating
no provenance; an enclosing production supplies its own empty-input diagnostic anchor. Never invent
a nonzero offset from an empty list or clear the caller's C1 diagnostic collector.

`CssIdentifierValue` and `CssStringValue` expose `Text` and `Span`. Recognition operates on decoded
text: keyword case folding is ASCII only; identifier/string result text retains case. Custom-identifier
matching excludes CSS-wide keywords, `default`, and the caller's finite exclusion set. Dashed-ident
matching implements its production; do not conflate it with the separate custom-property-name rule.
Use allocation-free comparisons for short keywords, without `ToLowerInvariant()` on every token.

`CssNumericAtom` exposes `Kind` (`Number`, `Percentage`, `Dimension`), `Number: CssNumber`,
`Unit: CssUnit`, `IsIntegerToken`, and `Span`. `CssUnit.None` accompanies number/percentage; a dimension
must map to a recognized unit. Unknown units yield NoMatch, without modifying C1's syntax result.
Unit categories are length, angle, time, frequency, resolution, and flex. The table contains:

- Absolute lengths: `px cm mm q in pt pc`.
- Font-relative lengths: `em rem ex rex cap rcap ch rch ic ric lh rlh`.
- Viewport lengths: `vw vh vi vb vmin vmax` and every `s`, `l`, `d` prefixed form of those six.
- Container lengths: `cqw cqh cqi cqb cqmin cqmax` (source: the
  [container units definition](https://drafts.csswg.org/css-conditional-5/#container-lengths)).
- `deg grad rad turn`; `s ms`; `hz khz`; `dpi dpcm dppx x`; `fr`.

Recognizing `fr` as a unit does not make it a length or allow it in every math context. This task
classifies literals; property-specific admissibility remains with the calling grammar. Preserve
token integer type: `1.0`/`1e0` are not integer tokens even when mathematically integral. Unitless zero
is still a number here; a length production may admit it, while time/angle productions must apply
their own rules. No global conversion of every zero to a dimension.

`CssNumber` exposes `Spelling` (reuse C1's owned NumberText), `Sign` (-1/0/1), `IsNegativeZero`, and
`CompareTo(CssNumber other, CssValueWork work)`. Parse the already validated CSS numeric spelling,
without locale-dependent parsing, `decimal` overflow or exponent-sized allocations. Internally use
significant-digit bounds/count and a normalized decimal exponent over that spelling. Skip the decimal
point while comparing digits; do not construct a huge `BigInteger`/power of ten. Saturate exponent
parsing only beyond a documented bound greater than all possible input-length adjustments; the input
is an Int32-sized string. Comparison must remain correct for arbitrarily long exponents, including
two distinct exponents beyond that bound: compare their normalized exponent digit sequences when
the saturated fast-path magnitude ties. Do not equate all enormous exponents.

`CssNumericRange` has optional lower/upper `CssNumber` bounds and inclusive flags, with
`bool Contains(CssNumber number, CssValueWork work)`. The range is in the numeric coordinate supplied
by its caller: it does not compare `1in` to `96px`, resolve percentages, or impose one property's
allowed range on another. This exact comparison prevents underflow turning a tiny negative literal
into acceptable zero and avoids rejecting a syntactically legal huge literal solely for CLR overflow.
Keep conversion to a finite implementation value and its dimension-specific clamping in the later
numeric evaluation/serialization task; do not introduce an unreviewed universal Double.MaxValue rule.

This lexical representation is a deliberate V0a boundary, not a requirement that C6 perform all math
in arbitrary precision. Numeric atoms retain source number spelling for syntax serialization. V0a
has no public or semantic number `Serialize()` method and makes no claim that spelling is canonical
CSSOM output. Keyword canonical spelling is fully implemented now.

## 4. V0b: math syntax, typing and specified serialization

Implement after V0a in `Css/Values/Math/`; tests in matching paths. First finite commit covers `calc`,
`min`, `max`, `clamp`, numeric constants, grouping and arithmetic. A second covers `round`, `mod`,
`rem`, trigonometric/inverse functions, `atan2`, `pow`, `sqrt`, `hypot`, `log`, `exp`, `abs`, and `sign`.
Keep an explicit function census; neither commit alone certifies the whole V0 math obligation.

Use a compact immutable node arena plus child-index storage, built with explicit parser frames. A
`CssMathValue` owns that arena, root index, resulting `CssNumericType`, original span and range
constraint. Types use fixed exponent fields for length/angle/time/frequency/resolution/flex/percentage
and a nullable percentage hint, not a heap dictionary per node. Use checked exponent arithmetic.
Nested readers share the operation's work state; no recursive parse/type-check/serialize walks.

The entry is `ParseMath(CssComponentValue value, CssMathContext context, CssValueWork work)`, returning
the primitive result for a complete recognized math production. Context carries expected numeric
type, percentage interpretation and destination range. It is passed by the property production;
it is not caller-configurable parser feature selection. Recognized but unfinished functions remain
named implementation debt in the dispatch manifest until their owner lands.

Validate operator whitespace, argument separators/arity and types before simplification. Do not erase
a type error by multiplying it by zero. Preserve mixed relative units and unresolved percentages in
the tree. Keep literal rejection and calculation result range handling distinct. Test dimensional
products/cancellation, percentage hints, signed zero, non-finite constants, integer-result rounding,
and function-specific domains against the adopted spec algorithms. In particular, do not use the old
blanket rule that division's divisor must be a number. Mathematical typing is not environment-based
evaluation. Property resolution, layout and cascade remain C6 work.

This stage owns specified numeric/math serialization under CSSOM/Values rules, including the chosen
supported finite range policy, unit spelling, precedence and deterministic invariant formatting.
It may consume C4a escaping helpers after integration, but cannot depend on mutable syntax editors.
Serialize iteratively, preserve type/meaning on reparse, and keep serialized offsets separate from
source spans. No arbitrary expression string is a substitute for a typed accepted math payload.

## 5. V0c: URLs, custom properties and deferred substitution

Own `Css/Values/References/` and matching tests. First commit implements ordinary URL token/quoted
`url()` forms and `var()` plus unregistered custom-property values; next commits implement `env()`
and the adopted Values 5 substitution forms (`attr()`, `if()`, and other named forms in the pinned
inventory). URL modifiers/functions require their own explicit grammar disposition. Unknown ordinary
functions do not become substitutions merely because validation is inconvenient.

A URL payload holds decoded unresolved text, original span and any validated modifiers. No fetch,
filesystem access, base inference or `System.Uri` acceptance shortcut. Base URL belongs to the later
stylesheet/declaration attachment, including values substituted from another declaration.

The substitution result keeps immutable original components, validated reference/fallback structure,
and dependency kinds. Check nested syntax with explicit frames. A recognized malformed substitution
fails; a valid one can defer the containing property grammar. Never accept every arbitrary function
or resolve missing variables to an empty string at parse time. Preserve custom-property name/value
case, empty fallbacks, nested fallback commas, significant value representation, importance removal,
and CSS-wide keyword semantics. Registered-property validation belongs to R6, not the unregistered
custom-property matcher. A pending shorthand retains its original value and shorthand identity until
real substitution; it is not expanded into fabricated empty longhands.

Substitution execution is later work: cycle/dependency handling, expansion quotas and invalid-at-
computed-value behavior require their own bounded task before C6 use. No reuse of XML's entity budget
for CSS. V0c parsing has no expansion and therefore must not add a speculative public expansion option.

## 6. V0d: shared color grammar

Own `Css/Values/Colors/` and matching tests. Use an immutable discriminated `CssColorValue` preserving
color space, typed channels, missing channels, alpha and contextual color identity. Do not collapse
everything to an RGBA byte tuple or resolve `currentcolor`/system colors without context.

Finite commits: (1) named/hex colors, transparent/currentcolor/system keywords, legacy and modern
rgb/hsl, hwb; (2) lab/lch/oklab/oklch and predefined `color()` spaces; (3) relative colors and the
Color 5 mixing/context-dependent functions in the adopted inventory. Each function needs grammar,
range/normalization, channel math, canonical specified serialization and negative tests. Dependencies
are V0a/V0b, plus V0c for values deferred before color parsing. V1 consumes this shared implementation;
it must not duplicate a smaller independent color parser.

Channel clamping/missing-value behavior follows each grammar's own stage. Accepting a color never
promises conversion into a device gamut, paint output, or a computed environmental color. Keep named
color data static and immutable; document provenance and license if vendored rather than authored.

## 7. Work, ownership, limits and verification

V0a's `CssValueWork(CancellationToken, Action? checkpoint = null)` is internal per-invocation state;
the optional checkpoint is only a deterministic test seam. `Charge(int utf16Units)` and
`CheckCancellation()` support explicit scans/comparisons/copies. Poll at entry, before every result
return, and at most 4096 authored work units. Check before/after unavoidable CLR bulk copies/growth;
their duration is cooperative, not a hard CPU deadline. No static hooks, timer tests or swallowed
OperationCanceledException. Repeated child calls share the state rather than restarting the cadence.

Production text entry points remain C1: check existing input/token/depth limits there once using the
original source. C5 never retokenizes subexpressions or resets limits by calling a fresh source parser
per child. Later explicit math/substitution/color frames carry the same maximum depth relative to the
original operation; semantic synthetic depth must not be confused with new input characters. Throw
existing ParseLimitException for resource limits, not NoMatch/Invalid. Do not add resource knobs to
primitive signatures. Tests can create work state after one C1 parse; no mutable diagnostic collector
is retained by accepted values.

Use indexed component iteration and compact structures on hot paths; no LINQ pipelines per token,
boxing enum keys, substring-per-probe, repeated prefix flattening, or ancestor-sized copies. Total
work for atom recognition/comparison is linear in examined spelling, including hostile exponents and
long identifiers. Keyword/unit rejection should check lengths before long comparisons. Arrays and
lists that escape builders must be owned immutable snapshots; no pooled buffer escapes. No references
to DOM, Browser, mutable sheets, host callbacks, or AngleSharp enter value payloads.

V0a acceptance files: `PrimitiveKeywordTests.cs`, `PrimitiveTextTests.cs`, `NumericAtomTests.cs`,
`NumericRangeTests.cs`, `ValueCancellationTests.cs`, and `AllResetTests.cs`. Cover all six keywords,
escapes/case/extra tokens, custom-ident exclusions, dashed identifiers versus custom-property names,
strings/bad strings, every unit category and table entry, integer token flags, percentage versus number,
signed zero, huge/tiny exponents and exact range endpoints. Include very long exponents differing late
in the spelling, negative underflow candidates, culture changes, and default-result payload guards.

Cancellation tests must reach numeric comparison/copy work after C1 scanning has finished, using the
per-invocation checkpoint; a pre-canceled test alone is insufficient. Verify no successful partial
result. For later stages, include deep expressions and long sibling lists, each existing quota at its
boundary and one past, malformed versus unsupported/unfinished distinctions, and fixed expected
canonical output followed by reparse invariants. Select primary WPT cases with exact upstream paths,
pin and license recorded; do not edit the existing shared WPT corpus/shim or call a small sample full
conformance. Handwritten assertions must independently state expected values, not mirror a helper.

Run newly built Release tests on net8.0 and net10.0:
`dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net8.0`
and the equivalent net10.0 command. Shared public API snapshots should remain unchanged. No benchmark
claims during concurrent builds; profile/measure later under the repository benchmark instructions.

## 8. Ready Sol dispatch

Implement **section 3's V0a only**, with section 7's work/ownership/test contract, in the listed new
files. Consume merged C1 getters and immutable lists. Do not edit C1, C4a, existing shared options,
public snapshots, Browser, or property registries. Add no unfinished math/color/substitution classes.
Report any actual missing C1 prerequisite instead of replacing its tokenizer. Deliver finite working
primitives and tests on both TFMs, with no claim that ParseCss/property support is complete.

The independent review checks that this dispatch is self-contained and that later V0b/V0c/V0d payloads
can consume its atoms without changing public contracts. Public C4b/C5 completion still requires the
full property/context manifest, all remaining V0 stages and every rule group in the parent design.

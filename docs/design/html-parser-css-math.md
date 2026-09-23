# V0b basic CSS math: implementation design

Independently reviewed design, 2026-09-23. Refines section 4 of
[the common value design](html-parser-css-values.md). V0a is integrated. The first math stage,
**V0b1**, implements basic math syntax, dimensional typing, specified-value simplification and
serialization together. It is internal infrastructure, independent of C4a and property registration.
Neither its completion nor a successful primitive result publishes validated CSS/property support.

## 1. Sources and finite coverage

Normative algorithms are the current [Values 4](https://drafts.csswg.org/css-values-4/) Editor's Draft
dated 20 August 2026, specifically [syntax](https://drafts.csswg.org/css-values-4/#calc-syntax),
[typing](https://drafts.csswg.org/css-values-4/#calc-type-checking),
[representation/simplification](https://drafts.csswg.org/css-values-4/#calc-internal),
[range handling](https://drafts.csswg.org/css-values-4/#calc-range), and
[serialization](https://drafts.csswg.org/css-values-4/#calc-serialize). Use the linked algorithms,
not an older scalar-divisor-only implementation. Type operations come from
[Typed OM numeric types](https://drafts.css-houdini.org/css-typed-om-1/#numeric-types).
Numeric text follows [CSSOM component serialization](https://drafts.csswg.org/cssom/#serialize-a-css-component-value).
The numerical implementation choices in section 5 are Jint policy proposed for review, not quotations
or alleged mandated bounds. Record these source dates with the implementation's fixture census.

| Stage | Owned grammar | Completion obligation |
| --- | --- | --- |
| V0b1, this dispatch | `calc`, `min`, `max`, `clamp`; `e`, `pi`, `infinity`, `-infinity`, `NaN`; parentheses and four arithmetic operators | Grammar, types, basic simplification, specified serialization, resource and ownership tests |
| V0b2 | `round`, `mod`, `rem` | Every current rounding strategy, including `line-width`, defaults, domains, infinities and serialization |
| V0b3 | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2`, `pow`, `sqrt`, `hypot`, `log`, `exp`, `abs`, `sign` | Per-function grammar/type/domain algorithms, signed-zero cases and simplification |
| Later consuming stages | Substitutions, context-defined numeric keywords, non-math numeric functions, newer Values 5 functions | V0c/V0d and property/rule owners must supply their actual grammar/context; no catch-all acceptance |

This splits the earlier second math commit into two finite follow-ups without deleting any obligation.
Maintain a test-side function census with these exact 21 Values 4 function names and their stage.
A pending census entry cannot become a successful invalid-value test or a public support claim.

## 2. Concrete internal contracts

Namespace `Jint.HtmlParser.Css.Values.Math`. All types/members have the narrowest internal visibility;
no public API, options, mutable editors, DOM references, property-name table, or feature configuration.
The entry consumes the existing C1 component directly, without text reconstruction:

```csharp
CssMathParseResult CssMathParser.ParseMath(
    CssComponentValue value, CssMathContext context, CssValueWork work);
string CssMathSerializer.SerializeSpecified(CssMathValue value, CssValueWork work);
```

`CssMathParseResult` is a readonly discriminated result. `Status` has `None` (default), `Match`,
`NoMatch`, `RequiresLaterGrammar`; only Match permits `Value: CssMathValue`. `Span` is the first
offending component or recognized pending construct. `PendingFunction: CssMathFunction` is populated
only for a known unfinished Values 4 function, otherwise None. NoMatch means failure of this named
basic-math production in the supplied context, not an unsupported property. A first bounded iterative
classification pass detects pending functions anywhere in the input and returns RequiresLaterGrammar
before basic parsing; this deliberately makes no validity assertion about the containing expression.
Unknown functions still fail this closed generic production. The eventual enclosing dispatcher must
route substitutions and other context-defined numeric functions before invoking this production.
Do not call that dispatcher's deferred-substitution status from V0b1.

This small result refinement, rather than using primitive NoMatch for known unfinished functions,
protects future family integration. No empty property dispatcher is needed. In V0b1 tests, naked
tokens, parenthesized top-level blocks, and `var()` alone are NoMatch for this function production;
they are not tests of property validity. A parent grammar checks whole-list cardinality itself.

`CssMathContext` is a readonly struct with a validating constructor and an initialization guard:

- `Expected: CssMathProduction`: Number, Integer, Percentage, NumberOrPercentage, Length, Angle,
  Time, Frequency, Resolution, Flex, and the six dimension-or-percentage productions. No `Any` mode.
- `Percentages: CssMathPercentageMode`: Forbidden, Raw, Length, Angle, Time, Frequency, Resolution,
  Flex. Raw covers percentages without a dimensional basis, including percentages of a number;
  it never converts their type to Number. Constructor rejects inconsistent Expected/mode pairs.
- `Range: CssMathRange`: nullable finite double lower/upper bounds, inclusive only, in the eventual
  destination's numeric coordinate; null means no grammar bound. Reject NaN, infinity and reversed
  endpoints as programmer errors. This is calculation-result metadata, not V0a's literal matcher.
- `MaximumNestingDepth`, `AncestorNestingDepth`: the original operation's captured limit and source
  nesting preceding this component. Zero maximum is unbounded. These internal source-scope fields
  are not per-property preferences. A future family passes the original operation's values.

No registry is required to instantiate Number/Raw or Length/Length contexts in focused tests.
`NumberOrPercentage` accepts two final alternatives; it does not make their addition legal. Integer
uses Number typing and records the destination's rounding obligation; it does not round nested nodes.
For union destinations the single Range applies to whichever alternative resolves, in that
alternative's coordinate (percentage points for Percentage); distinct ranges require distinct caller
alternatives. Range data remains attached, and is never applied during this specified-value parse.

`CssMathValue` is sealed, constructed only after complete success, and exposes internal readonly
`Type`, `Context`, `Span`, `NodeCount`, `RootIndex`, `GetNode(int)` and `GetChild(int)` accessors.
Its private exact-sized `CssMathNode[]` and `int[]` are immutable. Work state, components, stylesheet
source, cancellation token and diagnostics are not retained. A node is a readonly struct containing
Kind, Type, Span, child start/count, and the numeric payload only where meaningful. Kinds are Numeric,
Sum, Product, Negate, Invert, Min, Max, Clamp and AbsentBound. An AbsentBound is legal only at clamp's
first/third positions and contributes no type. Mismatched payload access throws. The final arena
contains only reachable nodes; no builder storage or pool buffer escapes.

`CssMathNumeric` holds a binary64 value, `CssNumericKind`, `CssUnit` and original Span. Optional owned
V0a `CssNumber` provenance may be retained on original literals; folded nodes do not invent spelling.
It is distinct from the exact V0a lexical atom. Percentage remains a percentage payload even where
its *type* has a length hint; a type hint never supplies a basis value.

## 3. Parsing and dimensional typing

Implement Values 4's grammar through indexed explicit frames and operator/operand stacks. Nested
components share context and work; only top-level destination checking happens after full typing.
Use C1's actual whitespace tokens for binary plus/minus requirements; discarded comments alone are
not whitespace. Recognize decoded function/constants with ASCII case-insensitive comparisons. C1
already handles escaped names, signed numeric tokens, and recovery at EOF. An EOF-recovered function
or parenthesized block is processed as C1's recovered component; do not reject solely for IsClosed=false.
There is no extra unary-delimiter grammar: numeric signs are part of tokens, and `-infinity` is a
keyword. Reject square/curly blocks, missing operands, extra separators and unused arguments.

Use function-specific arity. In particular clamp's outer arguments each admit `none`, while its
middle argument does not; `none` cannot leak into an arithmetic sum. Keep the two omitted-bound
positions explicit until simplification. A missing-token error is anchored at that frame's own end:
closing-token offset when closed, component end at recovered EOF; never the document's global EOF.
Type errors retain the responsible operator/argument span even when their children simplify to zero.

`CssNumericType` is a readonly value with seven signed Int32 exponents (length, angle, time,
frequency, resolution, flex, percent) and `CssPercentHint` (None plus those seven bases). Number is
the zero vector. Implement `TryAdd`, `TryMultiply`, `Invert`, and `Matches(context)` from Typed OM
with fixed fields; no maps or allocation per operation. Preserve hints when powers cancel to zero.
Every function boundary must have a permissible scalar result type, including nested calc/min/max;
intermediate arithmetic products can have composite powers. Thus grouping a composite product is
different from prematurely finishing it as a nested math function.

Terminal percentage typing follows the supplied context before type operations. Raw supplies the
Percent hint; dimensional modes supply the corresponding dimensional hint. Forbidden percentages
cannot be laundered by division into a final Number with a surviving hint. Use the complete matching
rules, not a comparison of exponent vectors alone. All types are validated before simplification.
Checked exponent arithmetic is required. With a true tree each exponent magnitude is bounded by the
number of input terminals; prohibit builder aliasing that duplicates algebraic subtrees exponentially.
For an impossible Int32-sized arena/exponent overflow, fail as a resource/capacity exception, never
NoMatch or wrapped arithmetic. Do not falsely report an existing lexical quota as its cause.

## 4. Simplification and linear storage

Apply the linked specified-value simplification algorithm, preserving all information not available
without an environment. Canonicalize absolute lengths to px, angles to deg, time to s, frequency to hz,
and resolution to dppx. Relative lengths and fr keep their unit. A percentage basis is never supplied
by this stage. Raw percentages can be compared numerically; basis-dependent percentages cannot be
ordered merely by their coefficients. A zero percentage term must survive where the algorithm keeps
its dependency. Do not cancel unresolved dimensional divisors on the assumption their basis is nonzero.

Do not implement simplification as repeated recursive rewriting or concatenate accumulated child
prefixes at every ancestor. Build a compact temporary binary/linked arena, establish types once,
then process maximal flattenable runs into final n-ary nodes. Within a run, use reusable fixed unit
buckets for numeric aggregation and preserve other child order. Treat scalar distribution over
all-numeric sums using accumulated factors/signs and one eventual materialization of each term.
No general distributive expansion of products of sums. Retain opaque boundaries such as unresolved
Min/Max rather than repeatedly traversing all their descendants. Parser, typing and simplifier work
must be O(input components + source numeric characters + emitted arena); memory O(input + arena).
The finite unit inventory permits bounded bucket ordering for serialization instead of a comparer
which recursively serializes children. Test deep alternating groups as well as a flat long sum.

Constant folding needs CSS-specific zero/NaN handling around CLR arithmetic: `Math.Min/Max` alone
is not the contract. Keep negative zero and non-finite intermediate results; nested calc must not
perform destination censorship. Clamp uses its specified ordering even when the lower bound exceeds
the upper, not CLR Math.Clamp's argument exception. `none` bounds simplify to the appropriate basic
operation. Raw source `-0` is unsigned CSS zero despite V0a preserving the spelling's negative sign.
Fold constants `e` and `pi` using the pinned binary64 Math.E/Math.PI values; normalize NaN payloads.

## 5. Finite numerical policy, separate from V0a

V0a comparisons remain exact and unchanged. V0b1 chooses binary64 arithmetic for semantic numeric
payloads, with IEEE intermediate infinities/NaN/negative zero. It does not run arbitrary-precision
arithmetic or allocate powers whose size follows a source exponent. No lexical overflow becomes
NoMatch. The following proposed finite representation policy is fixed internally, not an option:

1. Convert a nonzero decimal literal by a polled scan which rounds its significand to 17 decimal
   significant digits, ties to even, with guard/sticky information from the entire remaining spelling.
   Compute its adjusted decimal exponent without overflowing: saturate explicit exponent only beyond
   `source.Length + 4096` using Int64 arithmetic, then include the mantissa adjustment. This cap is
   ample to classify every binary64 extreme despite long mantissas. Feed only this bounded normalized
   spelling (at most 32 characters) to invariant `double.TryParse`. The documented precision policy
   allows this decimal rounding followed by binary64 rounding; do not claim correctly rounded binary64
   conversion of the original arbitrary-length literal. Retain exact lexical provenance separately.
2. Zero literals become positive zero. Nonzero negative values which underflow may become negative
   zero under this implementation precision policy. Underflow is not a grammar-range test: families
   must use V0a's exact range matcher before converting a bare literal.
3. Finite literal overflow and finite unit-conversion overflow saturate to the coordinate's finite
   endpoint, rather than becoming an explicit infinity constant. Number, percentage points, px, s,
   hz, dppx, fr and each retained relative-length coordinate use +/-Double.MaxValue. The angle
   coordinate uses +/-`Math.ScaleB(360d, 1014)` degrees, an exactly representable multiple of a full
   turn; all angle inputs are converted to degrees before saturation. Scale conversion in a way
   that avoids premature intermediate overflow/underflow (a bounded mantissa/exponent pair suffices).
   Incorporate the unit ratio before final finite saturation: for example, a huge ms coefficient
   can still be representable in seconds. Do not first clip the source coefficient and then convert.
   Saturation is for representation, distinct from property bounds. Do not use Double.MaxValue for
   angles or clamp an angle to a value that is not the documented whole-turn endpoint.
4. Arithmetic results inside the tree use binary64 operations without finite endpoint or property
   clamping after each operator. Explicit infinity and NaN remain distinct from finite source overflow.
   When a completed numeric result is later exported by C6, its destination uses the applicable finite
   endpoint plus grammar range, integer rounding (ties toward positive infinity), and special-value
   censorship. V0b1 retains those obligations in Context; it adds no computed-value export API.

These constants are representation bounds, not claims about Browser layout's eventual dimensions.
No family-specific narrower limits are invented. A later evaluator can impose its actual narrower
context bounds at the required stage. Review this finite policy explicitly before dispatch; changing
it later requires numerical/serialization fixtures, not a silent switch of CLR parsing routines.

## 6. Specified serialization

Serialize only an accepted immutable arena with an explicit work stack and a single growable output
buffer. Follow Values 4's function/tree serialization after specified simplification, including child
ordering, subtraction/inversion forms and function wrappers. Specified output retains a calc wrapper
around a folded scalar; it does not apply the destination's range or integer rounding. No recursive
string concatenation, per-child strings followed by stripping parentheses, or mutation of stored order.

For a finite numeric coordinate use invariant fixed notation with at most six fractional digits,
rounding ties to even as Jint's deterministic choice; remove trailing fractional zeros and a now-empty
decimal point, include a leading zero, and never emit exponent notation. `double.TryFormat("F6",
InvariantCulture)` into a fixed 384-character scratch span followed by in-place trimming is sufficient
for this binary64 bound; verify exact framework behavior with tests. Do not multiply by 10^6 first.
Zero serializes as `0`. Unit spelling is the canonical spelling of the stored unit (not enum spelling);
frequency serializes as lowercase `hz`, and x has already canonicalized to dppx. Non-finite numeric leaves use the math
keyword representation with their unit/type, never CLR Infinity/NaN spellings plus a dimension suffix.

Unresolved expressions containing an internally generated negative-zero leaf must retain its effect
inside the calculation, using a negative scalar times an unsigned zero of that leaf's unit where
needed; emitting lexical `-0` loses that sign on reparse. A folded top-level signed zero serializes
unsigned. Special-value serialization follows the math rules; specified `calc(infinity)` is not
serialized as a destination-clamped decimal. A later computed serializer is separate work.

CSSOM's six-fractional-digit output can lose numerical precision. Therefore reparse assertions require
the same grammar/type/context dependencies and the documented serialization-rounded values; they
must not assert arbitrary binary64 bitwise identity. Also assert serialization idempotence. Keep
original UTF-16 spans unchanged; this API returns text, not fabricated source-to-output offsets.

## 7. Work, limits and publication

Use the same V0a CssValueWork instance through classification, parsing, numeric conversion, typing,
simplification, freeze and serialization. Poll at entry, every success/failure return, and no later
than 4096 authored work units. Charge examined UTF-16 characters, visited components, node/edge work
and copied/emitted units. Poll before/after unavoidable CLR buffer growth, bulk copies, fixed-size
numeric formatting and final string/array materialization. Their duration is cooperative, not a hard
CPU quota. Do not pass an arbitrary-length source directly into an uninterruptible CLR number parser.

C1 alone accounts original MaxInputCharacters/MaxTokenCharacters; never retokenize each body or
reset its quotas. During iterative descent count actual source functions/parenthesized blocks from
AncestorNestingDepth, check the captured inclusive MaximumNestingDepth before pushing, and throw
existing ParseLimitException(NestingDepth, limit, observed) at limit+1. Synthetic arithmetic nodes
do not add source nesting. Classification must honor the same depth checks, including pending
subtrees. Standalone tests pass zero ancestor depth and the same limit used by the C1 operation.
Default unbounded limits still use explicit stacks; support at least the spec's 32 terms, arguments
and nesting levels without an artificial smaller cutoff. No new public quota or feature option.

Freeze only after grammar/type success and complete simplification. On cancellation/limit failure
discard temporary storage and publish nothing. Retained arrays have exact owned lengths; no per-node
lists, heap type dictionaries, captured callbacks or source component graphs. Avoid caching serialized
strings on values: the text may be large, and serialization receives the current caller's work state.

## 8. Exact first Sol dispatch and acceptance

Create only the following production files under `Jint.HtmlParser/Css/Values/Math/`, and matching
new tests under `Jint.Tests.HtmlParser/Css/Values/Math/`:

| File | Ownership |
| --- | --- |
| CssMathContext.cs | Production/percentage enums, guarded context, range metadata |
| CssNumericType.cs | Fixed type vector/hints and operations |
| CssMathParseResult.cs | Guarded result/status and finite function census enum |
| CssMathValue.cs | Immutable arena/node/numeric payload types |
| CssMathParser.cs | Classification and iterative grammar/type construction |
| CssMathSimplifier.cs | Work-bounded specified simplification and arena compaction |
| CssMathNumbers.cs | Conversion, coordinate bounds, unit ratios and special-value arithmetic |
| CssMathSerializer.cs | Iterative specified output and bounded number formatter |

Use the existing V0a atom/unit APIs or their underlying C1 getters without allocating a one-item list
for every token. Share unchanged V0a CssValueWork. Do not modify public snapshots, C1, C4a, property
registries, Browser, or lexical CssNumber semantics. Report a concrete missing prerequisite rather
than installing a parallel tokenizer. Add no classes for unfinished function implementations.

Required fixtures, with independently stated results:

- MathGrammarTests: every first-stage function/constant, escaped names, binary whitespace/comment
  boundaries, operator precedence, signed token versus unary delimiter, arity/commas, both `none`
  clamp bounds, closed/recovered nested frames and correct first-offending/terminal offsets.
- MathTypeTests: all seven dimensions, higher intermediate powers and cancellation, a nested-function
  composite result versus grouping, type-invalid terms multiplied by zero, raw versus dimensional
  percentages, forbidden-percentage laundering, number/percentage union and integer contexts.
- MathSimplificationTests: absolute canonical units, unresolved relative ratios, basis-dependent
  min/max percentages, partial min/max reductions, retained zero-percent dependencies, clamp with
  reversed bounds, negative zero, NaN, infinities and no premature nested censorship.
- MathNumberTests: long mantissa/exponents, late sticky rounding, positive/negative overflow,
  underflow, finite-overflow versus explicit infinity, full-turn angle saturation, scaled conversions,
  exact V0a negative-literal rejection versus accepted out-of-range calculation metadata.
- MathSerializationTests: fixed expected output, wrapper preservation, unit/order/precedence rules,
  six-decimal ties and extremes, `calc(1in + 2px)` -> `calc(98px)`, `calc(1 / 3)` -> `calc(0.333333)`,
  `calc(1kHz)` -> `calc(1000hz)` as required by [Values §7.3](https://drafts.csswg.org/css-values-4/#frequency),
  unresolved sign-sensitive zeros, culture independence, reparse and idempotence.
- MathResourceTests: wide and deep hostile input, original nesting boundary/one past, no duplicate
  input/token quota charging, cancellation after C1 during conversion, arena freeze and serialization.
  Use per-invocation checkpoints reaching those actual stages; pre-cancel or an unrelated entry check
  alone is insufficient. Use deterministic work counts to reject repeated-prefix/ancestor revisits;
  no wall-clock performance assertions. Check default result/context guards and immutable ownership.
- MathFunctionCensusTests: all 21 named functions accounted for; known pending nested functions return
  RequiresLaterGrammar, with no accepted payload and no claim of valid/invalid property semantics.

Select primary WPT specified-value math cases with exact upstream paths, revision and license recorded
in this new test area's fixture manifest. Separate computed/layout cases from the stage's assertions;
do not edit the shared WPT driver/corpus or call a small selection full conformance. First-stage tests
must cover the normative algorithms, not merely serialize whatever the implementation produced.

Run freshly built Release tests on net8.0 and net10.0. Public API snapshots must remain unchanged.
Deliver this finite basic-math stage for review before V0b2/V0b3. The full common-value and C4b/C5 gates
still require every remaining math, substitution, color, property and rule obligation in the parent
design; no internal milestone is a substitute for those gates.

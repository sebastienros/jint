# V0b2–V0b3: remaining CSS math dispatches

Design proposal for independent review, 2026-09-23. Extends the integrated
[basic math contract](html-parser-css-math.md), not the public CSS/property API.
The existing eight-file implementation already owns component parsing, numeric conversion,
typed immutable arenas, specified simplification and serialization. Reuse those operations.
The [value design](html-parser-css-values.md) continues to own substitutions, colors,
property registration and the final coverage gate.

## 1. Sources, consumers and finite stages

Authority is CSS Values 4, Editor's Draft 20 August 2026, checked 23 September 2026:
[stepped functions](https://drafts.csswg.org/css-values-4/#round-func),
[trigonometry](https://drafts.csswg.org/css-values-4/#trig-funcs),
[exponentials](https://drafts.csswg.org/css-values-4/#exponent-funcs),
[sign functions](https://drafts.csswg.org/css-values-4/#sign-funcs),
[typing](https://drafts.csswg.org/css-values-4/#calc-type-checking),
[simplification](https://drafts.csswg.org/css-values-4/#calc-internal), and
[serialization](https://drafts.csswg.org/css-values-4/#calc-serialize).
Use its actual algorithms and tables alongside the implementation choices below.
[CSSOM serialization](https://drafts.csswg.org/cssom/#serialize-a-css-component-value),
Editor's Draft 31 August 2026, supplies component formatting. Neither CLR Math nor AngleSharp
is the semantic oracle.

`Jint.Browser/Dom/Views/CssCascade.cs` currently delegates computation to AngleSharp for
computed styles, flat boxes, accessibility and DevTools. `Runtime/PageRenderDevice.cs`
supplies viewport/media facts. The calc-width case in `Views/ComputedStyleTests.cs` and the calc/custom-property
fixtures in `Views/CustomPropertyTests.cs` require retaining dependencies until their proper
evaluation stage. `Dom/Views/CustomProperties.cs` also handles unresolved var fallbacks;
that is V0c work, not permission to accept arbitrary functions here. No direct Browser consumer
enumerates these 17 functions; generic CSS consumption still requires the full inventory.
Browser supplies environmental facts eventually; all intrinsic arithmetic stays in HtmlParser.

| Slice | Functions implemented together | Count after merge |
| --- | --- | --- |
| Integrated V0b1 | calc, min, max, clamp | 4 implemented / 17 pending |
| **Next dispatch V0b2** | round (all five strategies), mod, rem | 7 / 14 |
| V0b3a | abs, sign | 9 / 12 |
| V0b3b | sin, cos, tan, asin, acos, atan, atan2 | 16 / 5 |
| V0b3c | pow, sqrt, hypot, log, exp | 21 / 0 |

These are sequential commits against the same math files, each independently reviewed. No
unfinished node kind, evaluator branch, success result, or public stub is introduced ahead of
its slice. The existing `CssMathFunction` census retains all 21 names throughout. In classification
replace the ordinal `function > Clamp` assumption and `IsBasic` checks with one explicit
implemented-function predicate shared by top-level and nested parsing. Known pending functions,
even malformed ones nested inside otherwise implemented grammar, retain RequiresLaterGrammar;
classification still scans the complete subtree for work/nesting limits. Unknown functions remain
NoMatch for this closed production. This does not establish validity of a containing property.

## 2. Shared contracts retained by every slice

Entry signatures remain `CssMathParser.ParseMath(CssComponentValue, CssMathContext, CssValueWork)`
and `CssMathSerializer.SerializeSpecified(CssMathValue, CssValueWork)`. No context option, DOM
reference, device object, new public type or alternate parser is added. Function arguments use
the existing calc-sum grammar; actual whitespace around binary +/− remains required. Names and
strategies use decoded ASCII-insensitive matching. Comments alone are not operator whitespace.
EOF recovery and UTF-16 source spans remain C1-owned. Missing arguments point to their own frame
end; an incompatible argument points to its argument span; an invalid strategy points to its token.
Runtime domains such as division by zero produce typed NaN, not a syntax failure.

Type all arguments before folding. Consistent input types use existing `TryAdd`, including
percent hints, never unit-string equality or magnitude coercion. A function result must be a
permissible scalar type even if an enclosing expression would cancel its dimensions. Only the
outermost result is checked against Expected. Range and Integer rounding remain destination
metadata; nested functions do not apply them.

V0b3a adds exactly this internal type operation when first needed:
`bool CssNumericType.TryMakeConsistent(CssNumericType input, out CssNumericType result)`.
Here the receiver is the required result base type (Number or Angle). Fail if both hints are
non-None and unequal; otherwise retain the receiver's exponents and copy the input hint only
when the receiver hint is None. Do not add the input's dimension vector to the output. This is
Values 4's make-consistent operation, distinct from TryAdd. Combine multi-argument input types
first. Do not erase a hint merely because division canceled the input's dimensions.

The basic contract's 17-significant-decimal conversion, finite literal saturation (including
the whole-turn angle bound), owned lexical provenance, IEEE intermediates, six-place specified
formatting and normalized NaN payload remain unchanged. Raw lexical `-0` is positive CSS zero;
tests requiring negative zero construct it arithmetically. No bare-literal range test is replaced
by rounded binary64 conversion. Transcendental kernels may use fixed-size CLR Math operations
with explicit CSS domain/sign guards; their finite last-bit accuracy is platform binary64,
not a claim of correctly rounded transcendental mathematics.

## 3. V0b2 exact parser, arena and numeric seam

Add only implemented node kinds Round, Mod, Rem. In `CssMathValue.cs` add internal
`CssRoundingStrategy { Nearest, Up, Down, ToZero, LineWidth }` and a guarded readonly
`CssMathNode.RoundingStrategy` getter valid only on Round. Carry this payload through the
temporary builder, freeze and serialization; non-Round access throws InvalidOperationException.
Round children are A then optional B; Mod/Rem have A then B. A one-child Round records omission
by child count, not a fabricated numeric token or AbsentBound. Explicit Number B=1 can be
canonicalized to the omitted form after successful typing; retain the Round span. No source
spelling, source component graph or work instance is retained on the value.

Grammar is `round([strategy,]? A [, B]?)`, `mod(A, B)`, `rem(A, B)`. A strategy is allowed only
as the first complete comma-delimited argument; accept nearest/up/down/to-zero/line-width and
no other keyword. Round without B requires Number A, except LineWidth which requires Length A
and permits omission. LineWidth with B also requires Length A and a consistent B. Use type
matching with the current percent hint; a length-hinted percentage remains unresolved data,
not a Number, and has no invented pixel value. No restriction that B be positive or nonzero
is imposed by grammar. Defaults belong to the operation: nearest, and Number step 1.

Create one new internal static class `CssMathStepped` in `CssMathStepped.cs` with these real
numeric operations, called by the simplifier where inputs are resolved and tested directly:

```csharp
double Round(double value, double step, CssRoundingStrategy strategy, CssValueWork work);
double Mod(double value, double step, CssValueWork work);
double Rem(double value, double step, CssValueWork work);
double RoundLineWidth(double valuePx, double? stepPx, double devicePixelSizePx, CssValueWork work);
```

All members internal; `Round` accepts the four non-LineWidth strategies, otherwise throws
ArgumentOutOfRangeException. The dedicated line-width operation is fully implemented in V0b2,
not a deferred success or delegate. Its device-pixel size must be positive finite; reject a
bad supplied environment with ArgumentOutOfRangeException. This numeric seam does not add an
environment to ParseMath or claim a computed-style evaluator exists. C6 later resolves operands
to px and supplies one device pixel's CSS-px size. No assumption that this size equals 1 and no
use of integer DPI from Browser inside the native operation.

Fold non-LineWidth operations with fully resolved numeric coordinates (Number, canonical absolute
dimension or raw percentage of consistent kind). Retain unresolved relative units, fr and
basis-dependent percentages as functional nodes even when all children are numeric payloads:
the node's full value is not known. In particular do not divide away an unknown zero step basis.
LineWidth always retains its functional node in specified parsing because device-pixel size is
absent. This is implemented syntax and an explicit evaluation dependency, not RequiresLaterGrammar.

### Numeric behavior and implementation choices

Implement the stepped algorithms and special-value table in Values 4 §10.3.1 directly. Tests
must cover the full Cartesian sign classes, not just normal positive samples. NaN is infectious.
For ordinary Round the sign of a nonzero step does not change its lattice. Exact multiples
retain A's sign of zero. Halfway nearest chooses the upper result; Up/Down choose the respective
neighbor and ToZero chooses the smaller magnitude. The zero below a positive interval is +0;
the zero above a negative interval is −0. Number-default and explicit step 1 agree.

Zero step yields NaN. Both infinite yields NaN. Infinite A with finite nonzero B retains A.
For finite A and infinite B: Nearest/ToZero produce A-signed zero; Up produces +infinity only
for positive nonzero A, otherwise the prescribed signed zero; Down is the sign-reversed case.
For Mod/Rem, infinite A or zero B yields NaN. Infinite B returns finite A except Mod with
opposite sign bits (including zero), which yields NaN. Finite Mod's zero has B's sign, finite
Rem's zero A's sign. Mod follows B's sign/range; Rem follows A's sign/range.

Do not compute these by `A - floor(A/B)*B`: quotient overflow and cancellation are avoidable.
Use bounded binary64 remainder and signed neighbor selection without source-sized big integers
or quotient loops. Compare distances without doubling a large remainder. Under this project's
finite precision policy, if a corrected nonzero Mod result rounds exactly to the excluded B
endpoint, use the adjacent representable value toward zero. Preserve zero sign separately.
This is a specified implementation rounding choice, not a new CSS range rule. Test smallest
subnormals, opposite-sign huge steps, quotient overflow and adjacent representable boundaries.

LineWidth first applies the step rule when B exists, with the nonzero-neighbor exception, then
always [snaps a line width](https://drafts.csswg.org/css-values-4/#snap-a-length-as-a-line-width).
With no B only snapping occurs. Exact multiples still undergo that final snap. Retain ±0;
nonzero magnitudes smaller than one device pixel become one pixel with their original sign;
larger non-integral magnitudes truncate toward zero in device-pixel coordinates. Already integral
pixel lengths stay unchanged. Avoid overflow from dividing a finite px length by a tiny pixel
size: use a remainder-based reduction, preserving a finite value when its sub-pixel correction
cannot change its binary64 representation. NaN/infinities pass through snapping unchanged.

Explicit interpretation for review: §10.3.1's infinite-B strategy list predates LineWidth.
Compose its stated nonzero-neighbor rule with snapping: finite nonzero A and infinite B yield
A-signed infinity; ±0 stays itself; both infinite or B=0 still yield NaN. This follows the
general rule, rather than silently treating LineWidth as Nearest. Record these fixtures as
this interpretation. [CSSWG issue 13794](https://github.com/w3c/csswg-drafts/issues/13794)
also establishes that the final snap is unconditional, including negative inputs.

## 4. Later finite families, without losing requirements

Each following slice adds its node kinds and one family kernel file only when implemented.
The immutable arena and shared ParseMath/SerializeSpecified signatures stay fixed. Kernels accept
resolved binary64 coordinates and CssValueWork; they never resolve fonts, percentages or layout.
For unresolved arguments keep the typed function. Same-unit relative values may simplify only
where the result is invariant for every still-possible basis, including zero; do not infer a
positive nonzero basis from a unit name.

| Slice / new file | Numeric seam | Grammar and result type |
| --- | --- | --- |
| V0b3a / CssMathSign.cs | `double Abs(double value, CssValueWork work)`; `double Sign(double value, CssValueWork work)` | Exactly one calculation; Abs retains its scalar type; Sign returns Number made consistent with input |
| V0b3b / CssMathTrigonometric.cs | `double Evaluate(CssMathFunction function, double value, bool isAngle, CssValueWork work)`; `double Atan2(double y, double x, CssValueWork work)` | sin/cos/tan: one Number (radians) or Angle; inverse trio: one Number; atan2: two consistent scalar inputs. Forward result Number; inverse/atan2 result Angle; make result consistent with input hints |
| V0b3c / CssMathExponential.cs | `double Pow(double value, double exponent, CssValueWork work)`; `double Sqrt(double value, CssValueWork work)`; `double Hypot(ReadOnlySpan<double> values, CssValueWork work)`; `double Log(double value, double? basis, CssValueWork work)`; `double Exp(double value, CssValueWork work)` | Pow two Numbers, Sqrt/Exp one Number, Log one or two Numbers (default e), Hypot one or more consistent scalar inputs; Hypot retains input type, others Number made consistent with combined inputs |

Kernel misuse (wrong enum, Angle for an inverse function, empty Hypot) is ArgumentException or
ArgumentOutOfRangeException, not NoMatch. Parser rejects those grammar/type conditions before
calling kernels. The isAngle bit in the trig numeric seam means canonical degrees, not permission
to interpret arbitrary dimensions as angles. All inverse/trig-angle results use degrees.

Abs maps −0 to +0 and infinities to +infinity. Sign retains either zero sign, returns ±1 for
nonzero finite/infinite input, and never calls Math.Sign on NaN. A percentage with unknown basis
cannot be folded to a sign from its coefficient. Abs can preserve an unresolved homogeneous
relative unit after removing the coefficient sign only where its basis is known nonnegative;
Sign still needs to distinguish a zero basis. Test retained percentage dependencies explicitly.

Trig domains, normalizations and the complete atan2 special-value matrix are mandatory fixtures.
Choose the recommended tangent convention: canonical-degree 90+360k gives +infinity and
−90+360k gives −infinity. Reduce finite degrees modulo 360 before converting to radians; this
also makes the existing finite whole-turn saturation meaningful. Recognize exact degree cardinals
without epsilon; ordinary radian inputs use CLR Math and do not acquire fuzzy cardinal tests.
Explicitly preserve the required −0 outputs; infinite forward arguments and out-of-domain asin/acos
yield NaN. atan(±infinity) yields ±90deg. For atan2 prefer the detailed §10.4.1 sign table at
the negative X-axis over the preceding prose's open lower bound: its −180deg special cases
remain −180deg. Pin that interpretation and all signed-zero/infinity combinations in tests.

Pow's integer/odd checks inspect the binary64 value without an Int64 cast; every representable
integer at magnitude >=2^53 is even. Implement the specified signed-zero/infinity table before
CLR Math.Pow; NaN dominates even exponent zero. Sqrt retains −0. Hypot uses a scaled sum of
squares in O(arguments) work without overflow for representable results, and scans all arguments
for NaN before returning infinity. Do not expand exponentiation into repeated multiplication.
Exp preserves its specified infinity endpoints. Log defaults to e. Apply the current §10.5.1
special cases in textual order before the ordinary `log(A)/log(B)` kernel: invalid base (1 or
negative) and negative A give NaN; A=±0 gives −infinity; A=1 gives +0; A=+infinity gives +infinity.
NaN remains dominant. B=0 is not explicitly rejected by that draft: ordinary positive finite
A uses the quotient with log(0)=−infinity. This deliberately records the draft's surprising
base<1 endpoint behavior rather than silently repairing it mathematically; independently review
these named source interpretations again before V0b3b/c dispatch. They do not block V0b2.

## 5. Serialization, work and semantic gates

Extend the existing iterative serializer's function-root and child handling, including nested
functions under arithmetic. Functional arguments preserve order; only Sum/Product use numeric
sorting. Use canonical lowercase function/strategy names, comma-space separators and the basic
contract's formatting, nonfinite and generated-negative-zero rules. Folded numeric roots use
calc; unresolved nodes keep their own function wrapper, not a blanket calc wrapper.

For Round omit default nearest, emit the other strategy followed by comma-space, and omit a
Number step equal to 1 after typing. Keep an absent LineWidth step absent; it is not equivalent
to an explicit 1px step. Non-default strategy is semantic payload and must survive serialization
and reparse. For later Log, omitted and explicit default e canonicalize to omission after typing.
Do not omit arguments on the basis of a six-place rounded serialization if their semantic value
differs. Assert dependency/type preservation and serialization idempotence, not arbitrary
binary64 bitwise preservation through the intentionally rounded CSSOM text.

Use the same CssValueWork through every phase. Charge every visited child/node, scanned literal
character, copied edge and emitted UTF-16 unit; keep the existing <=4096 work cadence. All new
numeric entry points check cancellation on entry and exit and bracket fixed CLR math calls;
Hypot additionally charges every argument. No unbounded CLR parse, recursive visit, argument
prefix copying, or source-exponent-sized arithmetic. Original C1 quotas and ancestor nesting
remain attached. Freeze only complete results; cancellation publishes nothing and kernels retain
no token, work object, callback, source or arena.

V0b2 owns the existing math parser/result/value/simplifier/serializer files, the new Stepped file,
and matching math tests plus FIXTURES.md. Numeric conversion/context files change only for a
demonstrated integration defect, not to add options. No C1, C4, native DOM, Browser, public
snapshot or dependency changes. Later families own the same math files sequentially and their
single kernel file; V0b3a additionally owns the new type operation.

Required V0b2 acceptance packet:

- Literal arity/strategy/whitespace/escaped-name/EOF fixtures with exact status/span; all five
  strategies, missing step type rules, mixed compatible units, incompatible dimensions and
  hinted/raw percentages. Nested pending functions must retain their census status.
- Hand-derived numeric tables covering zero/finite/infinite/NaN signs, negative steps, nearest
  ties ±2.5, Mod versus Rem, quotient overflow and representation endpoints. Assert zero bits.
  Examples: round(-2.5)=-2; mod(-18px,5px)=2px; rem(-18px,5px)=-3px.
- LineWidth independent kernel tests at pixel sizes 1, 0.5 and 2, positive/negative subpixels,
  exact step multiples needing a final snap, omitted versus explicit step, and invalid supplied
  pixel sizes. Parse/serialize tests prove that specified line-width never assumes a device.
- Very long literals/exponents inside these functions retain V0a exact provenance and the
  existing bounded conversion. Deep alternating functions and wide independent calls use
  iterative storage. Deterministic doubling work tests charge actual traversal/copy work and
  detect growing-prefix behavior; no elapsed-time assertions. Cancel at a scheduled checkpoint
  during classification, parse/fold/freeze and serialization, including pending/invalid input,
  and require cancellation at that checkpoint with no later observer call or returned result.
- Exact expected specified strings, round-trip dependency/type checks, culture independence,
  unchanged input/arena after concurrent independent calls, and both fresh Release target frameworks.

Each later slice repeats those gates for its family and adds the complete normative special-value
tables, hint propagation and unresolved-basis fixtures. For ordinary finite transcendental samples
use independently derived expected values with declared tolerances (absolute 1e-12 for bounded
unit-scale examples); exact cardinal/sign/domain cases remain exact assertions. Hypot includes
wide argument lists and huge/tiny representable results. Keep fixture attribution at the existing
WPT pin `2136eb1501a106c42cd8977bb31c81b57b785bc8`; verify actual upstream paths/assertions before
adapting them and preserve its BSD attribution. Draft-new line-width cases can be labeled authored
spec fixtures; absence from that older WPT pin is not an exclusion or a passing corpus claim.

The final gate is 21 implemented / 0 pending plus all family grammar, typing, numerical,
serialization and resource gates. It still does not complete V0c substitution, property validation,
C6 computed/used evaluation, CSSOM publication or Browser cutover. Those consumers must exercise
the resulting real native operations; no intrinsic math is reassigned to Browser to close the census.

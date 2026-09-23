# D7b3: numeric, temporal, range and color input values

Preparatory design for independent review, 2026-09-23. This document dispatches no implementation.
It refines [D7 form state](html-parser-form-state.md), the complete 22-state table in
[D7b1](html-parser-text-control-state.md), [D7b2](html-parser-checkable-state.md), and the color owner in
[CSS values V0d](html-parser-css-values.md). File selection remains D7b4. Pure helper slices can land
independently; the complete input state and its shared hooks remain D7b1d, after every family works.
No fallback-to-text, fake empty value, old simple-color parser, or temporary AngleSharp backing store
is an acceptable way to unblock that integration. Timing and machine sampling remain on hold.

## Evidence, scope and applicability

Primary sources inspected are the HTML Living Standard, updated 2026-09-22:
[input states and common APIs](https://html.spec.whatwg.org/multipage/input.html),
[floating-point microsyntax](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#floating-point-numbers),
[date/time microsyntax](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#dates-and-times),
and [CSS Color 4](https://drafts.csswg.org/css-color-4/), updated 2026-09-13, including color parsing,
conversion and serialization. ECMAScript supplies Number::toString and Date/TimeClip at the script
boundary. The native package must not reference Jint to implement those algorithms.

The selected upstream tests were read at the repository's exact WPT pin,
`6c7127bdd9f2cc6a3668fd9791757843e09d5a9e`, under
[html/semantics/forms/the-input-element](https://github.com/web-platform-tests/wpt/tree/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/forms/the-input-element).
Research used temporary copies, not a corpus edit or test run. Files inspected include the eight
family HTML tests, input-valueasnumber/date and their stepping tests, input-stepup/down/down-02,
color.window.js and color-attributes.window.js. A passing count is not claimed. The discrepancy table
below records cases where a test cannot simply be copied as the current normative algorithm.

Consumer inspection used the common integration tree through `4583bfe27`; subsequent implementation
must rebase against the current common head and reconcile reservations before editing shared files.

| Family | Number/step API | Date API | readonly/required | Stored value sanitization |
| --- | --- | --- | --- | --- |
| date | yes | yes | both | Valid date, otherwise empty |
| month | yes | yes | both | Valid month, otherwise empty |
| week | yes | yes | both | Valid week, otherwise empty |
| time | yes | yes | both | Valid time, otherwise empty |
| datetime-local | yes | no | both | Valid local date/time normalized, otherwise empty |
| number | yes | no | both | Valid finite HTML floating-point value, otherwise empty |
| range | yes | no | neither | Default, clamp and mandatory step rounding |
| color | no | no | neither | Current CSS color-well update algorithm |

All eight have value mode V; none has the public selection-range APIs. select() applies except to
range, but requires an actual selectable presentation buffer to do anything. Number has placeholder;
none acquires textual minlength/maxlength/pattern handling from a generic text-control base. min/max/
step apply to the seven numeric families. alpha/colorspace apply to color. Use HtmlInputTypes and
actual HTML namespace/local-name identity in both HTML and XML documents, never another classifier.

## Ownership and concrete pure seams

Place HTML helpers under `Jint.HtmlParser/Dom/Html/InputValues/`; CSS grammar/conversion stays under
`Css/Values/Colors/`. Every new type is internal until a complete real Browser consumer requires
promotion through the existing public-surface review. A helper must not hold Element, Engine,
JsValue, a stylesheet, a Browser callback or a culture. Input work/cancellation is passed per operation.
The names below describe complete capabilities, not a requirement to create empty classes now.

| Helper | Complete responsibility |
| --- | --- |
| HtmlInputNumberSyntax | Strict value grammar; separate HTML prefix-number parsing; finite conversion and HTML number serialization |
| HtmlInputCalendar | Gregorian civil day, month index and ISO week conversion, with mathematical floor division |
| HtmlInputTemporalSyntax | Strict family syntax, separately named microsyntax parsers, normalization and typed components |
| HtmlInputNumericConstraints | Parsed min/max, allowed step, base, underflow/overflow/mismatch and directional lattice operations |
| HtmlInputRangeValue | Range default and sanitizer, consuming the same constraints and arithmetic |
| HtmlInputColorValue | HTML source selection and color-well conversion/serialization, consuming the complete CSS color owner |

TryParse methods return an explicit success flag and typed result; zero is never a failure sentinel.
Keep syntax-invalid, finite-conversion failure and unsupported applicability distinct. A parsed temporal
record retains components or source ranges needed for normalization; it is not DateTime with an
invented timezone. No result retains a borrowed span beyond its owner's lifetime. Pure sanitizer
results can reuse the original immutable string when unchanged. Empty is a valid current value for
six families, but never a substitute result for an unfinished algorithm.

The shared state eventually exposes these internal operations on the complete HtmlInputState:

```csharp
internal double GetValueAsNumber(CancellationToken cancellationToken);
internal void SetValueAsNumber(double value, CancellationToken cancellationToken);
internal HtmlInputDateResult GetValueAsDate(CancellationToken cancellationToken);
internal void SetValueAsDate(double? utcMilliseconds, CancellationToken cancellationToken);
internal void StepUp(int count, CancellationToken cancellationToken);
internal void StepDown(int count, CancellationToken cancellationToken);
internal HtmlInputNumericFacts GetNumericFacts(CancellationToken cancellationToken);
```

DateResult must distinguish no Date result from a Date time value: a successful temporal parse is
not automatically an invalid parse merely because ECMAScript TimeClip would produce NaN. Native
receives a real Date's already-extracted UTC time value or null; it does not accept arbitrary objects.
Browser performs the object/Date validation with the exception ordering described below. Facts include
Applies, HasMinimum/Maximum, HasReversedRange, HasAllowedStep, ValueParses, Underflow, Overflow and
StepMismatch. They do not claim to be the complete ValidityState; D7f combines required, user bad input,
custom validity and validation candidacy. Pure facts do not dispatch invalid events or alter values.

## Number syntax and conversion are separate algorithms

The stored-number grammar uses ASCII digits and is equivalent to
`-?(digits+(\.digits+)?|\.digits+)([eE][+-]?digits+)?`. Reject leading plus, whitespace, trailing decimal
point, incomplete exponent, suffixes, NaN/Infinity, hexadecimal and Unicode digits. Empty remains
empty. A grammatically valid spelling that cannot convert to a finite HTML number is invalid for
number sanitization: the pinned number.html expects `2e308` to become empty.

Preserve a valid assigned spelling, including `-0`, `.1`, exponent case, leading zeros and integers
beyond binary64's exact integer range. In particular `9007199254740993` is not rewritten merely
because valueAsNumber rounds it. Direct value assignment is not number formatting.

The HTML floating-point parsing algorithm is deliberately more permissive. It skips leading ASCII
whitespace, accepts either sign and a numeric prefix, and does not require all input to be consumed.
Examples: `  +1.5x` parses as 1.5; `1.` and `1e+` parse as 1; `1.e2` parses as 100. An absent required
digit still fails. Use it for the places HTML calls it, including number/range conversion and step
attributes; do not feed those attributes through the stricter value grammar first.

Convert the mathematical decimal under HTML's nearest-binary64 rule, including ties-to-even and its
overflow-error rule; negative zero is excluded from the parser's result set. Underflow can consequently
produce positive zero without making the spelling invalid. A BCL conversion may be an implementation
primitive only after this grammar and result policy are established. Neither default Double.TryParse,
NumberStyles.Float, JS Number nor CSS numeric-token parsing defines HTML's grammar.

Formatting a numeric setter/step result uses HTML's best representation, hence ECMAScript number
serialization. Normalize negative zero to `0`; preserve the fixed/exponent boundary conventions,
shortest-round-trip digits and exponent spelling. A bare ToString("R", InvariantCulture) is not the
contract. Reuse a proven engine-independent formatter or an independently tested shortest-digit
primitive plus ECMA layout. Do not move engine code or add a package dependency in a helper task without
a separate ownership decision. Both supported TFMs must produce the same strings.

## Temporal grammar, calendar and number coordinates

Use a proleptic Gregorian calendar with year at least 1 and at least four ASCII digits. Month/day and
week numbers have exactly two digits. There is no sign, year zero, locale, trimming, BCE form or CLR
9999-year ceiling in valid HTML strings. Leap years are divisible by 4, except centuries not divisible
by 400. Validate the actual month's day count. A month has no day component. A week is `YYYY-Www`,
with uppercase W; ISO week 1 contains January 4 and starts on Monday. A year has week 53 only when
January 1 is Thursday, or Wednesday in a leap year. Week-year and civil year can differ.

A valid time is `HH:MM`, optionally `:SS`, optionally followed by a decimal point and one to three
digits. Hours are 0–23, minutes/seconds 0–59; no 24:00, leap second, timezone or offset. datetime-local
is a valid date, one T or ASCII space, and valid time. Normalize it to T and shortest time: omit zero
seconds when possible and trailing fractional zeros. Other valid temporal value assignments retain
their supplied valid spelling; do not normalize every time value because local date/time normalizes.

Keep the named microsyntax parsing algorithms distinct from validity predicates. In particular the
time-component parser accepts a longer fractional-seconds sequence than valid-time syntax. Thus
`12:00:00.1234` is not a valid stored time, yet must not be rejected by an attribute conversion solely
by imposing the stored-value three-digit limit. The local date/time parser uses that same component
algorithm. Test full consumption, separator positions and the parser's rejection of a trailing dot,
multiple dots and an unseparated third seconds digit.

| Family | Numeric coordinate | Default step | Scale | Default base |
| --- | --- | --- | --- | --- |
| date | UTC milliseconds to midnight of the date from 1970-01-01 | 1 | 86,400,000 | 0 |
| month | `12 * (year - 1970) + month - 1` | 1 | 1 | 0 |
| week | UTC milliseconds to Monday of the week from the epoch | 1 | 604,800,000 | -259,200,000 |
| time | Milliseconds since midnight | 60 | 1,000 | 0 |
| datetime-local | Milliseconds from naive 1970-01-01T00:00, ignoring zones | 60 | 1,000 | 0 |
| number/range | HTML number | 1 | 1 | 0 |

Date/week numeric formatting finds the UTC civil date/week containing the supplied instant; negative
times require floor division, not truncation towards zero. Time formatting wraps modulo 86,400,000:
negative one hour becomes 23:00. Pinned input-valueasnumber.html also expects the finite binary64 value
`2.7343337071894478e26` to produce `10:54:10.944` for time. Do not route this through TimeClip or an
Int64 cast; the integer remainder of a represented large binary64 value can be computed exactly.
Month numbers are month counts, never milliseconds. datetime-local ignores local timezone and DST.

Date-object getters use UTC midnight for date, first-of-month for month, Monday for week, and
1970-01-01 plus the time for time. Setters derive UTC fields from the Date, not the host timezone.
datetime-local intentionally has no valueAsDate. DateTime/DateOnly/ISOWeek can be test references for
their common representable subset, but cannot be the algorithm or impose their range on strings.

Implement the ordinary calendar path with checked integer arithmetic and a bounded fallback for long
years. Lexical validation can compute leap-year residues while scanning rather than allocate an
integer with one limb per source digit. Numeric conversion must distinguish an unrepresentable result
from a lexically invalid date. Never allocate a power of ten proportional to an exponent's value.

## Precision and conversion packet: a prerequisite, not an implicit default

The following small evidence/decision packet **b3p** must be reviewed before numeric constraints,
numeric formatting of temporal boundaries, or shared setters are dispatched. The inspected sources
do not establish one interoperable rule for every case. This design deliberately does not authorize
an implementer to fill those gaps using CLR behavior. Grammar/calendar slices do not depend on them.

| Question | Required finite evidence and decision |
| --- | --- |
| Decimal step lattice | Define the internal arithmetic and the rounding boundary between HTML's parsed binary64 values and decimal step multiples. Fixtures include base 0/step .1/value .3, 5.3/.5/6.7, 2^53 neighbors, subnormal steps, underflow-to-zero steps and overflow of step times scale. No arbitrary epsilon, blanket decimal type, or raw double remainder policy. |
| Fractional temporal conversion | Specify month values ±.5, negative sub-millisecond instants, fractional day/week/month steps and sub-millisecond time steps. Distinguish conversion rounding from optional UI rounding and from mandatory range rounding. |
| Temporal numeric domain | Specify boundary results for year 1, 9999/10000, ±8.64e15, adjacent values and finite values beyond Date's domain, independently for string validity, valueAsNumber, valueAsDate and numeric setters. The pinned huge time/datetime-local pair is a mandatory control: time wraps, local datetime becomes empty. |
| Setter/step state effects | Compare current normative wording with pinned coverage for dirty value, equal writes, and range valueAsNumber=NaN. Numeric/date setters and step methods say to set the internal value; they do not explicitly repeat the value-IDL setter's dirty/sanitization steps. Do not silently call that setter and assume equivalence. Test a subsequent value-attribute mutation and reset, with initially clean and dirty controls. |

b3p is a bounded design amendment with exact vectors, source/test references and chosen outcomes; it
is not a request for timing, broad browser exploration or a new implementation task here. If primary
texts leave a gap, label the selected compatibility policy and retain a focused regression. Record
upstream discrepancies separately from native implementation defects. No general HtmlInputState is
published with those cases returning placeholder values while the packet is outstanding.

Chromium's [StepRange implementation](https://chromium.googlesource.com/chromium/src/+/main/third_party/blink/renderer/core/html/forms/step_range.cc)
is useful comparison evidence: it has decimal arithmetic, special integral-step handling and tolerance
logic. Those choices are not themselves HTML normative requirements, and copying its epsilon or
rounding fractional steps is not an approved shortcut. Likewise CSS V0a's lexical decimal comparison
does not by itself define HTML's arithmetic after binary64 parsing.

## Bounds, base, mismatch and stepping

Parse min/max through each family's string-to-number algorithm. Missing or conversion-invalid
attributes provide no explicit bound. Range alone supplies defaults 0 and 100. Reflection preserves
the raw attribute string. Constraint facts use parsed applicability, not attribute presence.

For step, ASCII-insensitive `any` means no allowed value step; do not trim it. Missing step, parsing
failure or a nonpositive parsed value uses the family's default. Otherwise multiply the parsed step
by the scale in the table, using b3p's reviewed numeric policy. Base precedence is a successfully
parsed **min content attribute**, then **value content attribute**, then the family default base, then
zero. The current value is not the base. Range's implicit minimum does not override a parseable value
attribute in this precedence. A value-attribute mutation can change the base even while dirty value
prevents replacing the current value.

Mismatch means the parsed current number is not on the base-plus-integer-multiple-of-step lattice.
An empty/invalid current numeric value does not independently create underflow/overflow/mismatch.
Step any suppresses only mismatch; bounds still apply. Do not round number/date/month/week/time/local
datetime direct assignments to satisfy step. Where HTML permits optional rounding, this native model
chooses to retain the value and expose its constraint facts. Range's mandatory sanitizer is different.

Ordinary bounds are inclusive; below min is underflow and above max is overflow. Contradictory
nonperiodic bounds can make both true. Time alone has the periodic reversed-range rule: when max <
min, numbers strictly between max and min are both underflow and overflow, while the two outer
portions and endpoints are allowed. min=max is not a reversed range. Use the same facts for D7f and
:in-range/:out-of-range, with HTML selector eligibility applied separately.

Preserve the current HTML stepUp/stepDown ordering:

1. Reject inapplicable types, then step any, with InvalidStateError.
2. Return unchanged if min > max, or no lattice point exists within both bounds. The first condition
   applies even to time's reversed range; this method is not a circular UI spinner.
3. Parse current value, substituting zero on failure; save that number as the before value.
4. If off lattice, align strictly in the method's direction. Otherwise add step times n, negated for
   stepDown. Alignment and addition are alternative branches, not two operations on every call.
5. If below min, select the first lattice point at or above min. If above max, select the last at or
   below max. These are lattice bounds, not just Math.Clamp to the attribute values.
6. Return unchanged if stepDown would exceed the before value or stepUp would fall below it.
7. Format through the family's number-to-string algorithm and commit through its reviewed state path.

WebIDL long conversion and the omitted-argument default 1 belong in Browser; native receives Int32.
Handle negative counts and Int32.MinValue without signed-negation overflow. The literal algorithm's
direction checks are based on the method, not n's sign; its off-lattice branch also applies for n=0.
These observations require explicit edge tests and discrepancy accounting, not an unexamined rewrite
to `value += n * step` or an accidental integer loop proportional to count.

## Range sanitizer

Range always has a default value. With effective min <= max use the midpoint; with max < min use min.
Compute midpoint without overflow for finite opposite-sign extremes. Invalid or empty assigned
values take that default. Then enforce the lower bound, enforce the upper bound when max >= min,
and, if mismatched, choose the nearest in-bounds lattice point. Ties go towards positive infinity.
If no such lattice point exists, retain the value at this stage rather than inventing a valid point.
Use one reviewed lattice implementation for this and the step methods.

Required exact examples: default attributes and empty value give 50; min=0/max=100/step=20/value=50
gives 60; min=5.3/max=12/value=6.7 with default step gives 6.3, with step=.5 gives 6.8; min=0/max=5
with an invalid step gives default 3 after tie rounding. Test negative midpoint ties, max<min, any,
min=max and a value-attribute-derived base with no legal point in the interval.

Range re-sanitization follows the prescribed min/max/step attribute hooks as well as ordinary value,
initialization, reset and type-transition paths. Attribute-triggered normalization must not invent a
user edit or change dirty value merely because the stored string changes. Do not leave Browser's
current workaround that treats range as never out-of-range while reading an unclamped backing value.
The b3p range-NaN setter decision must be fixed before this sanitizer is wired to numeric setters.

## Current color well: shared CSS grammar and used-color conversion

alpha is boolean presence. colorspace recognizes limited-srgb and display-p3 ASCII-insensitively;
missing/invalid uses limited-srgb. Preserve the original content spelling; colorSpace IDL returns the
canonical state, and its setter uses normal DOMString reflection, including null becoming `"null"`.

On a color update, choose the value attribute when DirtyValue is false, otherwise the current stored
value. Parse a CSS color without a context element; failure yields opaque black. Serialize through
the following path. alpha/colorspace attribute changes trigger this update even without a value
write. A clean input whose attribute is `#ffffff08` can recover its alpha when alpha is added; a dirty
input reuses its already-sanitized current color and cannot recover discarded information magically.

| Color-well state | Conversion and serialization |
| --- | --- |
| limited-srgb without alpha | Force opaque; convert to sRGB, bound/quantize components to 8 bits; HTML-compatible lowercase six-digit hex |
| limited-srgb with alpha | Convert and quantize all components, including alpha; serialize via color(srgb ...) even if alpha is opaque |
| display-p3 without alpha | Force opaque; convert to display-p3 and serialize color(display-p3 ...), preserving extended channel range |
| display-p3 with alpha | Convert to display-p3 without the sRGB 8-bit quantization, preserving alpha and extended channels |

The CSS rounding link means nearest with ties towards positive infinity, not Math.Ceiling on every
fraction and not .NET's default ties-to-even. In limited-srgb, alpha .5 therefore becomes 128/255.
Do not gamut-clamp display-p3 channels merely because a channel lies outside 0..1. Missing channels
are resolved according to CSS's used-color conversion, not retained as arbitrary output `none`.

V0d supplies the shared grammar: named/hex/transparent/currentcolor/system colors, rgb/hsl/hwb,
lab/lch/oklab/oklch, predefined color() spaces, relative forms and dependent color functions. Inventory
the current table, including display-p3-linear, rather than freeze an older browser subset. The HTML
owner must not implement a second six-hex or rgb-only parser. V0d specified values alone are
insufficient: a finite CSS-owned conversion/used-color/serialization packet is a prerequisite here.

That packet resolves the context-free CSS parse with initial values, including currentcolor and system
colors. It never consults the input's computed style or calls Browser for an arbitrary environmental
color. Define a deterministic native initial system palette as part of that packet; do not classify
system keywords as invalid and turn all of them black. The pinned ActiveBorder test explicitly
expects a valid nonblack result. Currentcolor's initial-resolution test expects black. Relative and
math-dependent colors must use the CSS owner's complete resolution path; an unsupported valid color
is implementation debt, not a CSS parse failure to hide with the black fallback.

Selected exact controls: `#fff` becomes `#ffffff`; valid surrounding CSS whitespace is accepted;
embedded NUL/extra semicolon/junk fails; transparent without alpha becomes opaque black, with alpha
becomes `color(srgb 0 0 0 / 0)`. Direct P3 `color(display-p3 3 none .2 / .6)` with alpha becomes
`color(display-p3 3 0 0.2 / 0.6)`. Switching an already quantized dirty limited-srgb value to P3 keeps
that quantization; starting in P3 retains .6 alpha. Do not assume both histories yield the same string.
Conversion numerical tests may use the pinned harness's 0.0001 tolerance where it does; exact hex,
missing-channel handling, source selection and attribute reflection require exact assertions.

## State integration, editing and Browser migration

The eventual element-owned HtmlInputState remains the only current-value/dirty/provenance store.
Pure helpers do not attach another value field to a wrapper. Initial attributes are processed as a
complete parser batch; reset loads the content default, clears the prescribed flags and sanitizes.
Clone copies the current value and flags according to D7b1 rather than re-sanitizing from attributes;
adoption preserves the state. Derived numeric/color caches are rebuildable, not clone authority.
Type transitions retain D7b1's complete mode order, b2 group signaling and b4 file behavior. Entering
or leaving one of these families must clear obsolete UI edit state without losing prescribed value
or dirty flags. V→V does not clear dirtiness, including text→color and range→text.

valueAsNumber getter returns NaN for inapplicability/conversion error. Its setter rejects infinity
with TypeError before the applicability check; other values on a wrong type raise InvalidStateError.
NaN takes the empty-value branch, with range's subsequent state behavior settled by b3p. valueAsDate
getter returns null on inapplicability/parse failure; Browser creates a fresh actual Date object for a
date result. Its setter checks applicability first, then rejects non-null non-Date objects with
TypeError; null and an invalid Date take the empty-value branch. Do not call user valueOf/getTime
methods to extract a Date's internal time. Wrong-kind native elements remain argument errors.

Script setters and steps generate no input/change events and never claim User origin. Their exact
dirty behavior belongs to the b3p reviewed state table. Attribute sanitization preserves dirty flags.
All changed observable state, including facts changed by value/min/max/step attributes, must be
visible to selector/validity reads before any Browser event runs. Cache invalidation keys on this
element's relevant state changes, not every document mutation. Reads neither normalize nor mutate.

Number user editing needs a separate presentation buffer and bad-input state: an incomplete edit
such as `-` can be visible while the API value is empty. Empty UI is not bad input; rejected script
assignment is not a user bad-input edit. A completed valid edit atomically publishes value, dirty/user
origin, editing selection and bad-input facts after Browser beforeinput. Readonly/disabled checks
must be repeated at commit, since listeners can change them. Internal caret support does not grant
number public selection APIs. Avoid retaining old invalid UI text after script value/reset/type
replacement. Temporal picker/editor input likewise needs an explicit commit result, not a fabricated
valid date for partial fields. Browser owns localization, picker UI, events and focus-time baselines;
native family algorithms own accepted API strings and validation facts.

| Existing Browser consumer | Required cutover |
| --- | --- |
| Dom/Generated/DomShapes.Html.g.cs | Route valueAsNumber/date/step to native; correct generator/overrides, not generated output |
| Dom/DomConvert.cs Timestamp/NullableTimestamp | Current Date getter is a number and setter coerces arbitrary values; replace with actual JS Date/internal-slot conversion and correct exception order |
| Events/TextEditing.cs | Replace concatenation of sanitized Number.Value with an explicit numeric editing buffer/commit operation |
| Runtime/Parsing/PagePseudoClassSelectorFactory.cs | Replace bound-presence predicates and range workaround with native facts plus selector eligibility |
| Runtime/FormSubmitter.cs and accessibility | Consume native current API value; neither serializes a locale-formatted UI buffer |
| Picker/activation adapters | Preserve Browser security/user-activation checks; native value completion does not claim that a UI picker exists |

## Discrepancies, test gates and finite dispatch

| Evidence | Decision for review |
| --- | --- |
| input-stepdown-02.html: empty number, min=7, stepDown expects 7 | Current HTML substitutes zero, clamps to 7, then forbids increase for stepDown, leaving empty. Proposed policy follows the current algorithm; retain a named WPT/spec discrepancy and upstream evidence, not a silently changed expected value or broad exclusion. |
| input-valueasnumber-stepping.html accepts T or space for local datetime | Emit T and assert it exactly in native tests. The test's compatibility allowance does not weaken current normalization. |
| number.html retains large integer spelling but rejects 2e308 | Preserve value spelling; distinguish lexical validity, finite numeric conversion and exact integer representability. |
| input-valueasnumber.html accepts a huge time number but empties the same local datetime number | Preserve the pair as b3p boundary evidence; a shared DateTime/TimeClip path cannot implement both. |
| New color.window.js versus old simple-color assumptions | Implement current CSS color parsing, alpha and colorspace; no broad feature exemption based on old HTML or AngleSharp behavior. |

Inspect the complete relevant pinned WPT inventory before importing a corpus slice. Each excluded
case must name its assertion, observed result, current source and disposition. These research examples
are not permission to exclude unimplemented family behavior, label all color differences intentional,
or declare forms conformance on a few hand-written vectors.

| Slice | Owned files/capability | Completion gate |
| --- | --- | --- |
| b3a | New number syntax/format helpers and focused tests | Strict versus prefix parsing, finite boundaries, formatter vectors, no state hooks |
| b3b | New calendar and temporal grammar helpers/tests | Gregorian/ISO edges, arbitrary lexical years, strict versus parsing distinction; no unresolved numeric-setter policy |
| b3p | Follow-up evidence and exact numeric/state policy amendment | All four bounded questions above resolved before dependent dispatch |
| b3c | New numeric conversion/constraints/step/range helpers/tests | b3a/b3b/b3p complete; same lattice/facts across sanitizer and stepping |
| CSS prerequisite | CSS-owned used-color conversion/context-free resolution/serialization | V0d plus its math dependencies complete; all current color syntax accounted for |
| b3d | New HTML color-well adapter/tests | Full CSS prerequisite; clean/dirty source, alpha/P3 history and mutation vectors |
| b1d integration | Shared Element/state/attribute/parser/clone/reset/type hooks | b1+b2+b3+b4 ready, D6 and current shared owners handed off; all 22 types working |
| B2 migration | Browser bindings/editor/selectors/submission tests | Native public surface reviewed from real consumers, actual Date values, no AngleSharp state fallback |

Concrete tests include Gregorian century/400-year boundaries; week 53 and year-crossing Mondays;
ASCII/Unicode separators; >9999 and enormous leading-zero years; DST-independent local values;
min/value base precedence while dirty; reversed-time endpoints; off-lattice steps with 0/negative/min
Int32 counts; no bounded lattice point; range tie directions; clean/dirty color attribute histories;
clone/reset/type transitions; wrong-type exception precedence; and event-free script operations.
The shared transition suite must exercise V↔D/O/F and all family destinations, not just text↔number.

Native correctness runs freshly compiled Release tests on both net8.0 and net10.0, with deterministic
culture/timezone variants and hostile-length cancellation cases. No new TFM, obsolete target or
engine reference is introduced. Parser/color scanners check work while consuming input; expensive
conversion, comparisons and output copies retain checkpoints after tokenization. Count actual work,
not source length once per unrelated iteration. Never catch cancellation and return empty/black as
if the input were invalid. Compute replacement values before publishing coherent state.

Fast paths should scan spans, avoid regex/culture/lowercase copies, reuse unchanged strings and keep
short normal inputs allocation-free except their required output. Long digit/exponent/year handling
must remain bounded by actual input and conversion work. Color conversion must not parse a stylesheet
or construct an engine per value. Static data is immutable and shared; mutable scratch is invocation
owned. Unit tests assert semantics and counted work, not wall-clock performance.

Dedicated performance comparisons, when the coordinator releases timing, run only the most recent
benchmark TFM, currently net10.0, and report operations/second and allocated bytes under the benchmark
instructions. Compare number grammar/prefix conversion/formatting separately; short and hostile
temporal values; range constraints/sanitization; color parse/conversion; unchanged cached facts versus
attribute invalidation. Include construction cost and warm operations separately where relevant.
Use the prepared Markdig-inspired primitive harness to decide CharMap/SearchValues/token-set choices;
no syntax substitution or collection is a performance win by assertion. Do not use timing results
from concurrent implementation tests, short jobs or unsupported TFMs to justify a production change.

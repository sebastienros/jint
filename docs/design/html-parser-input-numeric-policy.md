# D7b3p: input numeric precision, conversion and write policy

Policy amendment for independent review, 2026-09-23. This is a new document only; it dispatches no
implementation and does not edit the reviewed [D7b3 family design](html-parser-input-value-families.md).
It supplies exact proposed outcomes for that document's four b3p gates and a concrete b3a formatter
owner. Approval of this packet, not the existence of this file, releases the dependent implementation
slices. No runtime edit, browser experiment, benchmark, machine sampling or repository test was run.

The decisions below distinguish HTML requirements, observed source behavior, and selected native
policies where those sources do not agree. A source inspection is not a browser execution result.
Arithmetic checks of the listed constants used small temporary Python calculations only.

## Source ledger

Normative references are [HTML input](https://html.spec.whatwg.org/multipage/input.html),
[HTML number/date microsyntax](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html),
[ECMA Number::toString](https://tc39.es/ecma262/multipage/ecmascript-data-types-and-values.html#sec-numeric-types-number-tostring),
[TimeClip](https://tc39.es/ecma262/multipage/numbers-and-dates.html#sec-timeclip), and
[WebIDL conversion](https://webidl.spec.whatwg.org/#js-type-mapping). HTML was updated 2026-09-22.

Implementation evidence was downloaded at fixed commits, not inferred from compatibility tables:

- Chromium `54ecad77f3eef797146ce32adc96b23e9f1904da`:
  [StepRange](https://github.com/chromium/chromium/blob/54ecad77f3eef797146ce32adc96b23e9f1904da/third_party/blink/renderer/core/html/forms/step_range.cc),
  [InputType](https://github.com/chromium/chromium/blob/54ecad77f3eef797146ce32adc96b23e9f1904da/third_party/blink/renderer/core/html/forms/input_type.cc),
  [HTMLInputElement](https://github.com/chromium/chromium/blob/54ecad77f3eef797146ce32adc96b23e9f1904da/third_party/blink/renderer/core/html/forms/html_input_element.cc), and
  [DateComponents](https://github.com/chromium/chromium/blob/54ecad77f3eef797146ce32adc96b23e9f1904da/third_party/blink/renderer/platform/text/date_components.cc).
- Firefox `08a61e1a51dbb4dbff0bb8e9ef97ad19c3afb156`:
  [HTMLInputElement](https://github.com/mozilla-firefox/firefox/blob/08a61e1a51dbb4dbff0bb8e9ef97ad19c3afb156/dom/html/HTMLInputElement.cpp) and
  [DateTimeInputTypes](https://github.com/mozilla-firefox/firefox/blob/08a61e1a51dbb4dbff0bb8e9ef97ad19c3afb156/dom/html/input/DateTimeInputTypes.cpp).
- .NET runtime release sources: [net8 formatter](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Private.CoreLib/src/System/Number.Formatting.cs),
  [net10 formatter](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Number.Formatting.cs),
  [net10 Dragon4 rounding](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Number.Dragon4.cs).
  The runtime's R mode requests shortest round-trippable digits; Dragon4's final tie is to even.
- WPT `6c7127bdd9f2cc6a3668fd9791757843e09d5a9e`:
  [input tests](https://github.com/web-platform-tests/wpt/tree/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/forms/the-input-element),
  specifically the family/valueAsNumber/valueAsDate/step files already listed in b3, plus range-2,
  number-constraint-validation, large-step-crash and the numeric/date TypeError tests.

The dotnet-inspect metadata query confirmed the span TryFormat signature on this machine's installed
net11 preview. That is not evidence for changing a project TFM. The supported net8/net10 API and
implementation claims above were checked in their release sources. Benchmarks remain net10-only
under the current project targets, and remain held; no preview-runtime measurement is authorized.

## 1. Concrete formatter ownership and contract

**Use the platform Double.TryFormat with R and InvariantCulture for shortest digits, followed by an
authored ECMA layout adapter in the parser. Do not copy or link Jint's Dtoa implementation.**

The existing `Jint/Native/Number/Dtoa/DtoaNumberFormatter.cs` imports Jint.Runtime for Throw; several
Dtoa files carry MPL-2.0 source notices. The broader implementation also supports precision/fixed
modes this input task does not need. A copy/extraction would add an unnecessary owner and license
maintenance surface when both parser TFMs already provide the required digit primitive. This packet
authorizes neither engine edits nor a new NuGet/project dependency. Existing notices remain untouched.

b3a owns a new internal `HtmlInputNumberFormatter` under `Dom/Html/InputValues/`, with
`FormatFinite(double value)` and an internal span-writing equivalent used by its tests/callers.
Reject nonfinite arguments as programmer errors; caller algorithms handle NaN/infinity first.
Both zero signs return `0`. For nonzero values:

1. Call `value.TryFormat(buffer, out length, "R", CultureInfo.InvariantCulture)` into a fixed stack
   buffer. A 32-character buffer holds binary64 R output; no culture/current-thread lookup.
2. Decode only that trusted invariant output into sign, a digit span and decimal point position n.
   Remove insignificant leading/trailing zeros, adjusting n for leading zeros only. Incorporate the
   signed exponent into n; do not reparse the number or apply another digit-rounding operation.
3. Let k be significant digit count. For k <= n <= 21 append zeros; for 0 < n <= 21 insert the point
   inside the digits; for -6 < n <= 0 emit `0.`, -n zeros and the digits. Otherwise use one leading
   digit, an optional fractional tail, lowercase e, and exponent n-1 with an explicit plus for
   nonnegative values and no leading exponent zeros. Prefix minus when appropriate.

The final ECMA output also fits 32 characters for finite binary64. The only heap allocation is the
required final string; span callers can avoid it. A TryFormat failure with the documented capacity is
an implementation invariant violation, not permission to emit a partial value or culture fallback.
Keep the platform's correctly rounded digit choice. G17 is not an alternative: it can emit more digits
than ECMA's minimum. R's notation itself is not the public result: thresholds and exponent spelling
belong to the authored adapter. b3a can land without numeric constraints or shared input state.

| Exact binary64 input or construction | Required string |
| --- | --- |
| +0, -0 | `0` |
| 0.1 | `0.1` |
| 1e-6 | `0.000001` |
| 1e-7 | `1e-7` |
| 1e20 | `100000000000000000000` |
| 1e21 | `1e+21` |
| Binary64 nearest to integer 1000000000000000128 | `1000000000000000100` |
| 9007199254740991; 9007199254740992; 9007199254740994 | Those respective integer strings |
| Bits 0000000000000001 | `5e-324` |
| Bits 0010000000000000 | `2.2250738585072014e-308` |
| Bits 7fefffffffffffff | `1.7976931348623157e+308` |

Add negative mirrors, both neighbors of the notation thresholds, binary powers and their neighbors,
and fixed-seed finite-bit-pattern differential vectors against an independent ECMA reference in the
test layer. Include halfway digit-choice vectors from the runtime/ECMA formatter tests with provenance.
Tests run on both supported TFMs after dispatch. No production reference to Jint is needed to compare
with its formatter in an appropriate test-only harness. A shortest-digit disagreement blocks b3a;
do not introduce a silent per-TFM string override. No formatter test or performance result is claimed
by this design packet.

## 2. Numeric precision: decimal intent and exact rounding cells

**Selected native precision policy:** HTML parsing first produces the prescribed finite binary64
value. Constraint arithmetic interprets the shortest decimal representation of that value exactly.
Step matching admits exactly the lattice points that round to the current binary64 value. This is
a named compatibility/numerical policy, not a claim that HTML spells out this representation.

This avoids both raw double `%` artifacts for .3/.1 and Chromium's much wider step/2^24 tolerance.
It also avoids declaring every sufficiently large quotient matched without checking. Firefox uses
decimal remainder; Chromium uses decimal arithmetic plus tolerance and a large-quotient shortcut.
Neither source establishes a shared exact extreme-value contract to copy wholesale.

Define the operations precisely:

- P(q) rounds an exact rational q under HTML's finite binary64 rule, nearest/ties-even, mapping either
  zero sign to +0 and reporting overflow instead of publishing infinity. This is a real arithmetic
  conversion; do not round-trip through a rounded decimal approximation first.
- D(x) is the exact decimal rational spelled by the b3a formatter for finite x. Original author
  spelling remains stored separately. Thus numeric parsing of `9007199254740993` yields x=2^53 and
  D(x)=9007199254740992, without changing the original value string.
- C(x) is P's preimage interval for x: endpoints are exact midpoints to adjacent binary64 values;
  midpoint inclusion follows the even-significand rule. Zero has one combined interval. At finite
  extremes use HTML's special ±2^1024 endpoints, with the overflow threshold excluded from C(max).
- B is D(parsed base); S is D(parsed positive step) times the family's scale, computed exactly.
  The mathematical grid is G={B+kS | k is an integer}. The representable grid is L={P(g) | g in G
  and P(g) succeeds}. StepMismatch(x) is true exactly when G has no point in C(x).

Compute the first/last possible k from rational ceil/floor of the cell endpoints minus B divided by
S, adjusting an integral endpoint when it is excluded. No scan proportional to k, no epsilon and no
materialized grid. L is monotone, and duplicate results from nearby grid points collapse to one value.
Do not use Math.BitIncrement results at infinity as arithmetic endpoints; construct the special
overflow threshold from the bit-level definition. Zero and powers of two have asymmetric neighbors.

Input parsing still owns HTML's prefix grammar and nearest conversion. `step=2e-324` parses to zero
and therefore selects the default; `step=5e-324` is positive and remains a real allowed step.
An overflowing step spelling such as `2e308` fails parsing and selects the default. In contrast a
finite parsed step whose multiplication by a temporal scale exceeds binary64 stays an exact positive
S; it does not turn into step any, zero or the default. All bound comparisons use parsed numbers;
no comparison may accidentally use the unsanitized author spelling as an arbitrary-precision value.

This bounded arithmetic is not a requirement to use BigInteger on ordinary inputs. Use checked small
integer/decimal-coefficient operations when exact; fall back to engine-independent System.Numerics
integer rational operations for the extreme cases. After finite parsing, significant decimal digits
are at most 17 and exponents come from binary64, plus fixed scale and Int32 step count. Rounding-cell
denominators are bounded by binary64's exponent range. Operand growth is consequently bounded
independently of the source's exponent spelling or number of redundant digits. Derive and test those
bounds; never allocate a power of ten using an unbounded parsed author exponent.

The same L must drive mismatch, range rounding and step alignment. For number/range, a matched x's
arithmetic anchor is the grid point in C(x) nearest D(x), with an exact-distance tie going upward.
Apply the count to that grid point and enforce bounds through the first/last members of L within
those bounds, then project once with P. A candidate beyond a finite bound can be bounded before a
projection that would otherwise overflow; never discard that usable bound. This prevents a successful
step producing a value the same policy immediately calls mismatched. An off-grid input aligns to the nearest strictly lower/higher member
of L according to the b3 method branch. The b3 rules about count, method direction and early returns
otherwise stay in force. Projection need not advance x when the step is below its resolution.

For range, measure nearest distances using D(candidate) and the exact decimal target; midpoint is
(D(min)+D(max))/2 with no floating overflow. Ties go upward. Find adjacent members of L by inverse
rounding-cell bounds and integer quotient operations, not by stepping through collapsed grid points.
Restrict to the sanitizer's applicable bounds. If there is no candidate, retain the clamped value as
HTML requires. For a script step with no finite projected candidate after applicable bound handling,
return unchanged. This finite-result guard is an explicit native policy for arithmetic overflow.

| Inputs, with explicit base 0 unless stated | Required outcome |
| --- | --- |
| value=.3, step=.1 | No mismatch; stepUp produces `0.4` |
| value=.30000000000000004, step=.1 | Mismatch; stepDown aligns to `0.3` |
| value=9007199254740991, step=2 | Mismatch |
| value string 9007199254740993, step=2 | Stored spelling retained, numeric 9007199254740992, no mismatch |
| value=9007199254740992, step=3 | No mismatch: grid point 9007199254740993 rounds to the even 2^53 value |
| value=5e-324, step=5e-324 | No mismatch; stepUp produces `1e-323` |
| value=5e-324, step=2e-324 | Default step 1, mismatch |
| value=1, step=2e308 | Default step 1, no mismatch |
| time step=1e308 | Exact S=1e311 ms; it neither overflows internally nor becomes the default |
| range min=5.3/max=12/value=6.7, default step | `6.3` |
| same range with step=.5 | `6.8` |
| range min=0/max=1/value=.15/step=.1 | `0.2`, using decimal distance and upward tie |
| range min=-1e308/max=1e308/step=any, empty | `0`, with no midpoint overflow |
| number max absent, value=1e308, step=1e308, stepUp | Unchanged, because the projected result overflows |

The 2^53/step=3 row intentionally distinguishes this policy from an exact-decimal-remainder-only
implementation. It must be independently reviewed as a compatibility choice. This packet is not a
license to characterize that newly selected behavior as an observed cross-browser result.

## 3. Fractional temporal values and steps

**Use mathematical floor for discrete calendar/time formatting; keep HTML's stated step scaling.**
Do not introduce round-to-nearest or truncation-to-zero through CLR casts. For date and week, find the
civil date/week containing the instant. For local datetime, floor to whole milliseconds. For time,
compute floor of the exact binary64 instant, then Euclidean modulo 86,400,000. This order preserves
negative sub-millisecond behavior and the large finite time vector in pinned WPT. For month, floor
the numeric month index, then use floor division/modulo by 12.

Chromium rounds calendar/local milliseconds and month counts to nearest (its C++ round tie is away
from zero); Firefox floors in these numeric-to-string paths. Flooring is the selected policy for
the month/sub-millisecond underspecification and matches the containing-date interpretation. It is
separate from Date's TimeClip, which truncates towards zero before an actual Date reaches the setter.

| valueAsNumber input | date | month | week | time | datetime-local |
| --- | --- | --- | --- | --- | --- |
| .5 | `1970-01-01` | `1970-01` | `1970-W01` | `00:00` | `1970-01-01T00:00` |
| -.5 | `1969-12-31` | `1969-12` | `1970-W01` | `23:59:59.999` | `1969-12-31T23:59:59.999` |
| 1.5 | `1970-01-01` | `1970-02` | `1970-W01` | `00:00:00.001` | `1970-01-01T00:00:00.001` |

These are minimal valid time strings. Numeric formatting must retain nonzero milliseconds even if
the step attribute is 60 seconds; step does not authorize dropping real value precision. The temporal
microsyntax parser's fractional attribute seconds are preserved through its prescribed numeric
conversion; the three-digit stored grammar does not truncate min/max before conversion.

Both inspected engines round positive fractional date/month/week step attributes to an integer of
at least one; Chromium also rounds scaled time/local steps to integer milliseconds. **Do not adopt
those step rewrites:** current HTML explicitly defines parsed positive step times the scale. Record
this as a named source divergence. It is not optional UI rounding, and cannot be imported without
changing mismatch facts even when the control has never been edited.

Consequences are deliberately explicit. Temporal stepping selects its numeric grid point under b3,
then formats its containing calendar/time value; it does not loop until a representable calendar point
is reached. Thus family projection may collapse a step or leave the result mismatched. This differs
from number/range's binary64-only projection, because calendar strings have additional granularity.

| Temporal fixture | Required outcome |
| --- | --- |
| date min=1970-01-01, step=1.5, value=1970-01-02 | Mismatch; stepUp aligns to 129600000 ms, formats `1970-01-02`, still mismatched |
| date base epoch, step=.5, value=1970-01-01, stepUp | Half-day point formats `1970-01-01`; a successful equal write |
| month min=1970-01, step=.5, value=1970-01, stepUp | Half-month index formats `1970-01`; a successful equal write |
| week min=1970-W01, step=.5, value=1970-W01, stepUp | Thursday of the same week formats `1970-W01` |
| time min=00:00, step=.0005, value=00:00, stepUp | .5 ms formats `00:00` |
| time min=00:00, step=.0015, value=00:00:00.001 | Mismatch; stepUp aligns to 1.5 ms, formats `00:00:00.001`, still mismatched |

These cases protect the chosen literal scaling plus floor projection; do not later "fix" them by
rounding the attribute or repeatedly advancing the grid in a performance refactor. If compatibility
review chooses engine-style step normalization instead, that is a policy change to this table and
the constraints contract, not a harmless implementation detail.

## 4. Temporal domains: strings, numeric getters, Date, numeric setters

**Keep unbounded positive-year lexical validity, finite numeric getters, and a separately named
compatibility ceiling for calendar numeric setters.** A single DateTime/TimeClip helper cannot serve
all four surfaces. D7b3's distinction between null and a Date containing NaN remains necessary.

| Operation | Selected domain/result |
| --- | --- |
| Assign date/month/week/local string | Validate the HTML positive-year Gregorian grammar without a 9999 or 275760 ceiling; preserve/normalize by family |
| String to numeric getter/min/max/base | Compute the mathematical coordinate, then nearest finite binary64. No TimeClip. On numeric overflow return conversion failure/NaN, without erasing a valid stored string |
| valueAsDate getter | Wrong type/parse error gives null. A successful date/month/week/time parse constructs a new Date with the coordinate subjected to ECMA TimeClip; outside TimeClip gives an invalid Date, not null |
| Calendar valueAsNumber setter: date/week/local | Require the supplied finite instant in [-62135596800000, 8640000000000000] ms, then apply the floor/containing-family conversion. Outside gives empty through the normal write |
| Month valueAsNumber setter | Floor month index; allow integer indices -23628 through 3285488 inclusive, corresponding to 0001-01 through 275760-09; otherwise empty |
| Time valueAsNumber setter | Every finite binary64 is accepted by floor/modulo; no TimeClip, Int64 or calendar-year ceiling |
| valueAsDate setter | Actual Date has already passed TimeClip; null/invalid Date gives empty. UTC date/week/month requiring a nonpositive HTML year gives empty; time extracts the valid Date's UTC time regardless of its year |

The calendar numeric-formatting domain in this table also applies when a step reaches its final
family formatter. An out-of-domain temporal candidate formats as empty and takes the successful
sanitized write path; it is distinct from arithmetic overflow, which returns before writing. For
example, a valid stored date `275760-09-14` with explicit epoch base and step=1 reaches an empty dirty
write on stepUp(0). This is another explicit consequence of the compatibility domain, not a parser
failure or an unfinished-family fallback.

The upper numeric-setter ceiling follows the inspected engine domain and the pinned enormous-local-
datetime setter test. It is a **compatibility choice**, not a lexical HTML year limit or a claim that
HTML's number-to-calendar prose explicitly prescribes TimeClip. In particular numeric getter then
setter need not round-trip a lexical date beyond that ceiling. Such asymmetry must be documented and
tested instead of silently accepting a CLR limit. Large lexical years retain their spelling even if
their numeric coordinate overflows; validation can compute leap/week residues without allocating a
BigInteger with the full author's year length.

For a valueAsDate getter with a valid astronomical year, compute whether the exact UTC coordinate is
within TimeClip before narrowing; out-of-range can return the invalid-Date result without constructing
an enormous integer. Underlying numeric getter conversion is a different operation. A leading-zero
year is not astronomical merely because it is long. Error/empty stays distinct from valid-but-unusable
numeric conversion. After an actual Date setter, Date's own truncation cannot be undone by flooring.

| Boundary vector | Exact expected behavior |
| --- | --- |
| date `0001-01-01` | Stored valid; numeric -62135596800000; valid Date |
| date numeric -62135596800001 | Empty; numeric getter then NaN; Date getter null |
| date `9999-12-31` / `10000-01-01` | Both valid; numeric 253402214400000 / 253402300800000 |
| date numeric +8640000000000000 | `275760-09-13` |
| local numeric +8640000000000000 | `275760-09-13T00:00` |
| calendar numeric +8640000000000001 or -8640000000000000 | Empty; the negative boundary is outside positive HTML years |
| date string `275760-09-14` | Preserved; numeric 8640000086400000; valueAsDate is an invalid Date |
| local string `275760-09-13T00:00:00.001` | Preserved; numeric 8640000000000001; valueAsDate getter null because inapplicable |
| month numeric 3285488.5 / 3285489 | `275760-09` / empty |
| month string `275760-10` | Preserved; numeric 3285489; valueAsDate invalid (first day coordinate 8640001555200000) |
| week string `275760-W38` | Preserved; Monday coordinate 8640000172800000; invalid Date |
| Date object with time -8640000000000000 assigned to time | `00:00`; the Date is valid even though its year cannot be an HTML date |
| numeric 2.7343337071894478e26 assigned to time / local | `10:54:10.944` / empty; the time integer remainder is 39250944 ms |

Numeric setters reject infinity with TypeError before HTML applicability, as b3 specifies. NaN is an
empty write, not an out-of-domain arithmetic exception. The above numeric domain restrictions do not
alter min/max reflection or turn out-of-domain but finite bounds into missing attributes.

## 5. Dirty state, sanitization and exceptions

**Successful numeric/date setters and successful script stepping use the same non-user sanitized
write primitive as value IDL assignment, including setting DirtyValue=true for an equal value.**
This selects the common source behavior where HTML's internal-value wording omits repeated setter
details. Chromium routes through SetValue/SetNonAttributeValue; Firefox routes through SetValue with
SetValueChanged and sanitization. Both sources distinguish attribute/reset paths from those writes.

That primitive computes the replacement first, then atomically publishes the sanitized current value,
dirty=true, NonUser origin and cleared obsolete presentation bad-input state. It preserves UserValidity
unless a distinct HTML algorithm resets it. It produces no input/change event and no fabricated DOM
attribute MutationRecord. State observers still see changes in dirty/origin/bad-input facts when the
string is equal. Selection/presentation synchronization follows the relevant family's internal editor
contract, never grants public selection APIs to number/date controls.

| Operation/path | Dirty flag and value effects |
| --- | --- |
| valueAsNumber finite, including out-of-domain temporal input | Dirty=true; format or empty, then sanitize |
| valueAsNumber NaN | Dirty=true; empty branch, then family sanitizer; range defaults/clamps/rounds |
| valueAsDate null, undefined after WebIDL, or invalid Date | Dirty=true; empty branch on applicable type |
| Successful step ending in same number/string, including on-grid count=0 | Dirty=true and NonUser, because final write is reached |
| Step early return: contradictory bounds, no permitted candidate, direction guard, overflow guard | No state change, including dirty/provenance/editor state |
| Any WebIDL, applicability or Date-brand exception | No native value state change; script conversion side effects before entry are separate |
| min/max/step attribute re-sanitization of range | Preserve dirty flag, apply required sanitizer, NonUser for a changed current value |
| value attribute while clean | Load new default and sanitize, retain dirty=false |
| value attribute while dirty | Retain current value; still update a value-derived step base/facts |
| Reset | Load latest content default, sanitize, clear DirtyValue and UserValidity, clear incomplete user presentation |

Required sequential vectors, each starting from a newly parsed clean input:

- `<input type=number value=1>`; set valueAsNumber=1; setAttribute(value,2): current remains `1`,
  defaultValue is `2`; reset produces `2`. This distinguishes an equal write from a no-op.
- Same input; stepUp(0); setAttribute(value,2): current remains `1`. With min=2/max=1 instead, the
  step returns before writing; the subsequent attribute mutation sets current to `2` while clean.
- `<input type=date value=1970-01-01>`; set valueAsDate=undefined: current empty and dirty=true;
  changing its value attribute does not restore current value until reset.
- `<input type=range min=0 max=100 step=20 value=40>`; set valueAsNumber=NaN: current `60`, dirty=true;
  subsequent value attribute `80` changes default/base as applicable but does not replace current.
  A plain fresh range instead produces `50` for NaN. Range's empty write never remains empty.
- A numeric user edit showing `-` with API value empty and badInput=true; set valueAsNumber=NaN:
  current remains empty but the presentation edit is cleared and badInput becomes false.

The reviewed WebIDL correction remains mandatory: valueAsDate is object?, so non-nullish primitives
raise TypeError before applicability; undefined/null convert to null, then HTML checks applicability,
then Date brand. Exact cases: text+1→TypeError; text+{}→InvalidStateError; date+{}→TypeError;
date+undefined→empty dirty write. Do not call user getTime/valueOf methods to test/extract a Date.
An actual cross-realm Date is accepted by its internal slot; a Proxy around a Date has no such slot.

## Review and implementation handoff

This packet closes the four policy questions by selecting outcomes; it does not claim all are literal
HTML or observed interoperability. Review must explicitly consider the rounding-cell lattice, retained
fractional temporal steps, unbounded lexical versus bounded numeric-setter calendar domain, and dirty
sanitized numeric writes. Keep the existing named stepDown/empty/min=7 WPT discrepancy from b3; the
inspected engines both special-case an initially invalid value, but this packet does not silently
change b3's selected current-HTML method ordering.

After review, b3a may own number grammar plus the concrete formatter above, b3b grammar/calendar,
and b3c the bounded arithmetic/conversion/constraints/range helpers. No shared input state is released
until b1/b2/b3/b4 and current file-owner prerequisites are complete. A later public Browser facade
must preserve Date-result null versus invalid-Date, exact WebIDL order and event-free script writes.

Validation must include both supported TFMs, formatter identity, rounding-cell inclusive/exclusive
endpoints, zero/subnormal/overflow cells, negative grid bases, Int32.MinValue counts without negation
overflow, midpoint extremes, all tables above and post-write attribute/reset sequences. Arithmetic
fallback cancellation and counted work need deterministic coverage. Source-driven decisions require
native regressions even when the selected WPT directory has no equivalent assertion. Do not import
whole implementation files or add broad WPT exclusions as part of applying this policy.

Performance validation remains a separate released phase: normal small-number allocation/throughput,
fallback numeric work and formatter construction versus steady operations, with op/s and allocated
bytes only on the newest benchmark TFM. No numerical choice above is asserted to be fast without that
measurement; no benchmark run is authorized by this document.

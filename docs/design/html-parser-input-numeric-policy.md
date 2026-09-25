# D7b3p: input numeric precision, conversion and write policy

Policy amendment independently reviewed and approved, 2026-09-25. This is a design document only; it dispatches no
implementation and does not edit the reviewed [D7b3 family design](html-parser-input-value-families.md).
It supplies exact approved outcomes for that document's four b3p gates and a concrete b3a formatter
owner. The independent review releases the dependent implementation slices under the ownership below. No runtime edit, browser experiment, benchmark, machine sampling or repository test was run.

The decisions below distinguish HTML requirements, observed source behavior, and selected native
policies where those sources do not agree. A source inspection is not a browser execution result.
Arithmetic checks of the listed constants used small temporary Python calculations only.

## Source ledger

Normative references are [HTML input](https://html.spec.whatwg.org/multipage/input.html),
[HTML number/date microsyntax](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html),
[ECMA Number::toString](https://tc39.es/ecma262/multipage/ecmascript-data-types-and-values.html#sec-numeric-types-number-tostring),
[TimeClip](https://tc39.es/ecma262/multipage/numbers-and-dates.html#sec-timeclip), and
[WebIDL conversion](https://webidl.spec.whatwg.org/#js-type-mapping). The relevant HTML algorithms were rechecked at the 2026-09-25 update.

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

## 2. Numeric precision: exact shortest-decimal remainder

**Use exact remainder on the shortest-decimal values of the parsed numbers.** This replaces the
rejected rounding-cell proposal: no projected lattice, ULP neighborhood, epsilon or large-quotient
exemption can change a nonintegral quotient into a match. The integral-multiple requirement in HTML
remains the predicate. Decimal representation is the selected numerical interpretation for cases
such as .3/.1; it is not a claim that HTML specifies every arithmetic representation detail.

Define the operations precisely:

- Parse each input through its HTML conversion, including nearest finite binary64 conversion where
  required. A failed conversion is not zero. Preserve the author string separately.
- D(x) is the exact decimal rational spelled by the b3a formatter for finite parsed x. Do not round
  it to CLR decimal. For example D(.3)=3/10 and D(.1)=1/10. A source spelling that parses to 2^53
  has D(x)=9007199254740992, including source `9007199254740993`; its stored spelling is unchanged.
- V=D(current), B=D(parsed base), S=D(parsed positive step) times the family's scale, computed
  exactly. StepMismatch is `(V-B)/S` not an integer. For a common decimal scale, subtract integer
  coefficients and use exact integer remainder. Negative values/bases obey the same zero-remainder
  test; use mathematical floor/ceiling rather than truncation for alignment.
- P(q) is nearest/ties-even conversion of an exact arithmetic result q to finite binary64, reporting
  overflow instead of publishing infinity. P is a publication operation, never a validity rule.

D is an explicit decimal arithmetic choice: it does not mean the exact base-two rational represented
by every x, nor the author's unrounded arbitrary-precision decimal. Keep that boundary visible. The
required integer 2^53 has its exact decimal value under D, so base=0/step=3 necessarily mismatches:
9007199254740992 = 3 * 3002399751580330 + 2. A neighboring multiple rounding back to 2^53 is irrelevant.
Chromium's tolerance/large-quotient shortcuts are not adopted. Firefox's decimal-remainder approach
is supporting implementation evidence, not proof that its precision matches this exact policy.

`step=2e-324` parses to zero and selects the default; `step=5e-324` remains positive. An overflowing
step spelling such as `2e308` fails parsing and selects the default. A finite positive parsed step
whose multiplication by a temporal scale exceeds binary64 remains exact S; multiplication alone
must not turn it into any, zero or the default. Default/base/min/max precedence remains b3's.

Arithmetic on finite parsed inputs has bounded significant-digit/exponent sizes, independent of
redundant source digits or an author's exponent spelling. Use checked small-integer coefficient
operations when exact and a bounded System.Numerics integer fallback otherwise. The 1024-bit bound in the calendar path below
does not apply here: aligning D(Double.MaxValue) with D(5e-324) already requires a 2098-bit integer. No power of ten
may use an unbounded author exponent, and no operation loops proportional to the grid index/count.
This replaces the former rounding-cell machinery with ordinary scaled integer remainder/division.

For both step methods and range sanitization use the mathematical grid B+kS. An off-grid value
aligns by exact floor/ceiling; an on-grid value adds exactly n*S in the applicable direction. Bounds
select the first/last mathematical grid point within them. Keep b3's ordering, including its separate
alignment branch, count semantics and direction guard. Range midpoint is exactly (D(min)+D(max))/2;
compare exact distances to its adjacent in-bounds grid points, taking the larger on a tie. An interval
with no grid point retains the clamped value as HTML specifies. No rounded neighboring point can
stand in for a missing grid point.

Only after this arithmetic does number/range publication apply P and FormatFinite. Temporal
publication uses its containing-family/floor conversion in section 3. Recompute later facts from the
published value by the same ordinary parser and remainder rule. Publication can collapse a step or
leave a result mismatched; do not grant it hidden validity, keep an unobservable exact-current-value
cache, repeatedly sanitize until convergence, or search for a different rounded grid. Each sanitizer
invocation performs the prescribed finite sequence once. A second invocation can therefore have a
separate effect in extreme precision cases; no idempotence shortcut is permitted without proof.

Bounds are applied before publication, so a finite bound can rescue an otherwise overflowing step
candidate. If the remaining number/range candidate cannot publish a finite value, the script step
returns unchanged (the retained explicit arithmetic-overflow policy). This guard is not a reason to
loosen mismatch. Range midpoint/clamping uses finite parsed bounds and exact arithmetic.

| Inputs, with explicit base 0 unless a min/base is stated | Required outcome |
| --- | --- |
| value=.3, step=.1 | No mismatch; stepUp produces `0.4` |
| value=.30000000000000004, step=.1 | Mismatch; stepDown aligns to `0.3` |
| value=-.3, step=.1 | No mismatch; stepDown produces `-0.4` |
| value=.3, min=.1, step=.1 | No mismatch: (.3-.1)/.1=2 |
| value=9007199254740991, step=2 | Mismatch |
| value string 9007199254740993, step=2 | Stored spelling retained, numeric 9007199254740992, no mismatch |
| value=9007199254740992, step=3 | Mismatch, remainder 2; stepUp selects 9007199254740993, publishes 9007199254740992, still mismatched |
| same value and step, stepDown | Selects/publishes 9007199254740990, no mismatch |
| same value and step, stepUp(0) | Off-grid branch still aligns; published number unchanged and mismatched; successful dirty write |
| value=9007199254740994, step=3 | Mismatch, remainder 1 |
| value=5e-324, step=5e-324 | No mismatch; stepUp produces `1e-323` |
| value=5e-324, step=2e-324 | Default step 1, mismatch |
| value=1, step=2e308 | Default step 1, no mismatch |
| time step=1e308 | Exact S=1e311 ms; multiplication does not select the default |
| range min=5.3/max=12/value=6.7, default step | `6.3` |
| same range with step=.5 | `6.8` |
| range min=0/max=1/value=.15/step=.1 | `0.2`, exact decimal upward tie |
| range min=-1e308/max=1e308/step=any, empty | `0`, no midpoint overflow |
| number max absent, value=1e308, step=1e308, stepUp | Unchanged because publication overflows |

These vectors are selected exact outcomes, not claims of a browser run. Include exact remainder
oracles in the future helper tests so a tolerance or double-remainder regression cannot pass merely
because serialization still looks plausible.

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
is reached. Thus family projection may collapse a step or leave the result mismatched. Calendar
strings add their own granularity to the publication-rounding effects described in section 2.

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

## 4. Full numeric calendar domain, with TimeClip only for Date

**Do not impose an upper calendar ceiling on numeric conversion.** HTML's date/week conversion
returns the date/week containing the supplied instant; month uses a month index; local datetime uses
a naive instant. Their valid-string grammars have positive years without an upper year limit. ECMA
TimeClip applies when constructing an actual Date, not when formatting these input values. There
is no standards-backed reason to copy a CLR or browser year ceiling into the native number-to-string
algorithm. This section replaces all caps and cap-driven empty writes in the initial policy draft.

| Operation | Domain/result |
| --- | --- |
| Assign date/month/week/local string | Positive-year HTML Gregorian grammar without an upper year ceiling; preserve/normalize by family |
| String to numeric getter/min/max/base | Compute its mathematical coordinate, then nearest finite binary64; no TimeClip. Numeric overflow is conversion failure/NaN and does not erase a valid stored string |
| valueAsDate getter | Wrong type/parse error gives null. A successful applicable parse constructs a fresh Date using ECMA TimeClip; outside TimeClip gives an invalid Date, not null |
| Date numeric formatter | Find the UTC date containing the exact finite instant; emit it if its civil year is positive, otherwise empty |
| Week numeric formatter | Find the ISO week containing the exact finite instant; emit it if its week-year is positive, otherwise empty; do not clip the original instant to Date's domain |
| Month numeric formatter | Floor the month index, use floor division/modulo by 12 and add 1970 to the year; emit when the resulting year is positive, otherwise empty |
| Local datetime numeric formatter | Floor the finite instant to milliseconds, split into Gregorian day and within-day time, emit when the civil year is positive, otherwise empty |
| Time numeric formatter | Every finite binary64 is accepted by exact floor/Euclidean modulo; no TimeClip, Int64 or year ceiling |
| valueAsDate setter | The actual Date has already passed TimeClip. Null/invalid Date gives empty; valid Date uses UTC family conversion. Time accepts its UTC time even when the Date's year cannot be an HTML date |

Infinity rejection and NaN's empty write retain b3's ordering. A finite negative instant does not
necessarily fail: 1969 is a valid positive year. A Date at -8.64e15 is valid as a Date but cannot become
an HTML date/month/week with a positive year; its time remains usable. Week-year is tested after ISO
conversion, not by treating the current civil year as its type. There is no upper-bound asymmetry
between numeric getters and setters introduced by native policy, although binary64 precision and
family granularity still prevent universal round trips.

### Concrete bounded calendar algorithm

The ordinary path uses checked Int64 arithmetic, with existing Gregorian/ISO component validation;
no DateTime/DateOnly/ISOWeek result or timezone conversion defines the domain. The exceptional path
uses System.Numerics.BigInteger only after the fast path cannot represent the intermediate result.
A finite binary64 integer part has at most 1024 bits: this fallback is bounded even for Double.MaxValue.
It never allocates an integer proportional to an unbounded author exponent or year spelling.

1. Obtain the exact floor integer from the sign/exponent/significand bits. For normals,
   x=(-1)^sign * (2^52+fraction) * 2^(exponentField-1075); for subnormals use fraction * 2^-1074.
   Shift left when integral; otherwise divide by the corresponding power of two, rounding towards
   negative infinity. Do not use formatter digits to reconstruct this integer: shortest decimal is
   not necessarily the exact integer represented by a large double. An Int64 cast without a proven
   range check is not this operation.
2. For date/local/week/time, floor-divide by 86400000 to obtain epoch day z and remainder r with
   0<=r<86400000. Time needs only r; a huge integer modulo remains bounded. For month, floor-divide
   the month index by 12, add 1970 to the quotient and use remainder+1 as month.
3. Convert z to Gregorian components with the 400-year cycle. Set a=z+719468, era=floor(a/146097),
   doe=a-era*146097; doe is in 0..146096. Then
   yoe=(doe-doe/1460+doe/36524-doe/146096)/365, where these divisions are nonnegative integer
   divisions; y=400*era+yoe; doy=doe-(365*yoe+yoe/4-yoe/100); mp=(5*doy+2)/153;
   day=doy-(153*mp+2)/5+1; month=mp+(mp<10 ? 3 : -9); add one to y when month<=2.
   Year zero/negative can exist in intermediate arithmetic; reject only the final family's
   nonpositive year. The cycle decomposition and inverse are explained in Howard Hinnant's
   [civil calendar algorithms](https://howardhinnant.github.io/date_algorithms.html#civil_from_days).
   This document specifies mathematics, not a source-code import; an implementation that ports
   source must retain its applicable notices. Verify inverse and boundary properties independently.
4. ISO weekday is floorMod(z+3,7), Monday=0. Convert the Thursday at z+3-weekday to obtain week-year.
   Compute the epoch day of January 4 of that year and subtract its weekday to obtain first Monday;
   week=(z-firstMonday)/7+1. The inverse Gregorian mapping uses the same exact 400-year cycle.
   Do not run a loop over years, months or weeks; no shared instant ceiling precedes this conversion.
5. Format the positive year in invariant base ten, padded to at least four digits and without a plus
   sign; remaining components are fixed width, with shortest valid time per section 3. Date/local/week
   years from a finite binary64 millisecond instant have at most 298 decimal digits; month indices
   can produce a 308-digit year. Even local output is bounded (at most 317 characters with full
   millisecond fields). A 384-character result buffer is sufficient for these numeric formatter
   paths. It is not a cap on arbitrary lexical strings supplied by the user.

Checked arithmetic guards the fast path, including epoch offsets and ISO Thursday/January-4 work;
fallback operates on the same mathematical equations. BigInteger temporaries remain roughly the
binary64 integer width plus small calendar constants. For exact decimal arithmetic step results,
first enforce the finite-number publication bound already required for script stepping, then carry
out the family's floor conversion without an intervening decimal-to-double rounding that could cross
a day/month boundary. The common path can use a value-type component result with ordinary integral
year storage; extended year storage is allocated only on the exceptional path. Avoid an always-BigInteger
result that adds work to every common date merely to support an extreme value.

For arbitrary lexical years, scan significant digits and compute Gregorian/ISO residues directly
while validating. Leading zeros do not make a small year enormous. Numeric conversion can classify
certain overflow from significant-digit count, then use a bounded integer for the remaining boundary
cases (309 significant year digits already suffice to distinguish all finite numeric coordinates;
longer positive years necessarily overflow). Do not parse a million-digit year into BigInteger merely
to discover numeric overflow. A valid stored string stays valid and retains its spelling. A Date getter
can classify TimeClip overflow even earlier without converting that year to binary64 or constructing
its full integer. Cancellation polls must cover scanning, validation and output work; bounded fallback
operations need checkpoints before/after, not source-length charging repeated per fixed arithmetic step.

### Source distinctions and required outcomes

The retained Chromium DateComponents source is useful evidence **against** a shared guard. Its date
and local helpers route through an ECMA-range check, but its week conversion does not: it converts the
instant and checks the resulting week. +8.64e15+1 therefore remains week 275760-W37, and that week lasts
through Sunday 275760-09-14. Chromium's WithinHtmlDateLimits and explicit maximum week/month predicates
also impose upper result limits. Those are engine limits, not HTML grammar constraints, and are not
adopted. The native arithmetic above supports the next day/week/month as required by the same HTML
conversion rules. No source inspection is presented as proof of all-engine interoperability.

| Boundary vector | Exact expected behavior |
| --- | --- |
| date `0001-01-01` | Stored valid; numeric -62135596800000; valid Date |
| date numeric -62135596800001 | Empty because the resulting civil year is zero; numeric getter then NaN |
| date `9999-12-31` / `10000-01-01` | Both valid; numeric 253402214400000 / 253402300800000 |
| numeric +8640000000000000 | date `275760-09-13`; local `275760-09-13T00:00`; week `275760-W37` |
| numeric +8640000000000001 | date `275760-09-13`; local `275760-09-13T00:00:00.001`; week `275760-W37` |
| numeric +8640000086400000 | date `275760-09-14`; local `275760-09-14T00:00`; week `275760-W37` |
| week numeric +8640000172799999 / +8640000172800000 | `275760-W37` / `275760-W38`, the Sunday/Monday boundary |
| date numeric -8640000000000000 | Empty because its resulting year is nonpositive, not because of an upper/absolute instant guard |
| date `275760-09-14`, default step, stepUp(0) | Same current date, successful dirty write; numeric 8640000086400000; valueAsDate invalid |
| week `275760-W37` or `275760-W38`, default step, stepUp(0) | Same respective week and a successful dirty write; W38's Monday coordinate is 8640000172800000 |
| local `275760-09-13T00:00:00.001`, step=.001, stepUp(0) | Same local string, successful dirty write; number 8640000000000001; Date getter null because inapplicable |
| month numeric 3285488.5 / 3285489 | `275760-09` / `275760-10` |
| month `275760-10`, default step, stepUp(0) | Same month, successful dirty write; numeric 3285489; invalid Date (first day coordinate 8640001555200000) |
| valid Date with time -8640000000000000 assigned to time | `00:00` |
| numeric 2.7343337071894478e26 assigned to time | `10:54:10.944`; exact integer remainder 39250944 ms |
| same numeric value assigned to local | `8664758583750640-12-03T10:54:10.944` |
| Double.MaxValue assigned to date/local/week/month | A nonempty positive-year result from bounded arithmetic; exact inverse/component vectors required, no overflow-to-empty shortcut |

Zero-count fixtures must actually be on their stated grids; an off-grid zero-count call still takes
b3's alignment branch. Success sets dirty state even when text is unchanged. No old ceiling may clear
these valid values. Subsequent actual Date construction can still give an invalid Date independently.

The pinned [input-valueasnumber.html](https://github.com/web-platform-tests/wpt/blob/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/forms/the-input-element/input-valueasnumber.html#L126)
expects empty for the enormous local numeric vector (independently retrieved and checked at integration). That conflicts
with uncapped HTML conversion; the native expected result above intentionally differs and must remain
a named WPT/spec discrepancy. This amendment supersedes the earlier b3/b3p empty expectation for that
specific row. Do not copy the WPT expectation into an arbitrary upper bound, silently edit the upstream
case, or broaden an exclusion. A future corpus entry records this exact assertion and normative/source
evidence for review. The time vector continues to agree with the same pinned file.

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
| valueAsNumber finite, including a nonpositive resulting calendar year | Dirty=true; format or empty, then sanitize |
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
HTML or observed interoperability. Review must explicitly consider exact shortest-decimal remainder,
retained fractional temporal steps, the full finite numeric calendar domain, and dirty
sanitized numeric writes. Keep the existing named stepDown/empty/min=7 WPT discrepancy from b3; the
inspected engines both special-case an initially invalid value, but this packet does not silently
change b3's selected current-HTML method ordering.

After review, b3a may own number grammar plus the concrete formatter above, b3b grammar/calendar,
and b3c the bounded arithmetic/conversion/constraints/range helpers. No shared input state is released
until b1/b2/b3/b4 and current file-owner prerequisites are complete. A later public Browser facade
must preserve Date-result null versus invalid-Date, exact WebIDL order and event-free script writes.

Validation must include both supported TFMs, formatter identity, exact decimal remainder and
publication-rounding boundaries, negative grid bases, Int32.MinValue counts without negation
overflow, midpoint extremes, all tables above and post-write attribute/reset sequences. Arithmetic
fallback cancellation and counted work need deterministic coverage. Source-driven decisions require
native regressions even when the selected WPT directory has no equivalent assertion. Do not import
whole implementation files or add broad WPT exclusions as part of applying this policy.

Performance validation remains a separate released phase: normal small-number allocation/throughput,
fallback numeric work and formatter construction versus steady operations, with op/s and allocated
bytes only on the newest benchmark TFM. No numerical choice above is asserted to be fast without that
measurement; no benchmark run is authorized by this document.

# Native input numeric state integration (N1)

This implements the approved numeric policy in the existing HtmlInputValueState, adding number,
date, month, week, time, datetime-local and range to its internally supported families. The current
string, dirty flag, origin, UserValidity and selection remain the same authoritative store. Color
and file remain unavailable here; this is not a public complete 22-state input surface or Browser
cutover. HTML §4.10.5 and the reviewed input-numeric-policy document govern the algorithms.

## Demand boundary

Parsing still creates zero input value components. First semantic creation sanitizes the current
family string, but non-range creation only records actual unqualified min/max/step/value Attr
identities. It neither creates exact numeric constraints nor retains numeric/Date coordinates.
Number string validation necessarily checks finite conversion; temporal string validation can
retain arbitrarily long valid years without requiring a coordinate. Range string sanitization
requires its actual bounds, base and lattice, so initialization may build those locally.

Non-range attribute mutations replace metadata identities and invalidate relevant caches without
parsing constraints. A lazy derived cache holds numeric coordinate, Date result, constraints and
facts only as their operations request them. Value changes invalidate value-dependent results;
value-default/min/max/step mutations invalidate constraints/facts, including default-base changes
while dirty. Effective type changes and clone/import drop derived caches. Unrelated document or
namespaced attribute changes do not invalidate them. Warm getters allocate nothing and never
change the mutation stamp.

Range min/max/step writes require retaining current-value history even on a previously cold input.
The before hook materializes the old logical state, then the ordinary after hook sanitizes once
under new constraints, preserving dirty state and using NonUser origin only for a changed value.
Value/reset/transition sanitization uses the same pure range helper and exact lattice. No loop
re-sanitizes a rounded publication until it converges. Warm range writes reuse constraints until
their actual inputs change.

## Internal operations and publication

The state supplies Get/SetValueAsNumber, Get/SetValueAsDate, StepUp/StepDown and GetNumericFacts with
the caller's CancellationToken. Numeric setters reject infinity with ArgumentException before
applicability, then wrong types use DomException InvalidStateError. NaN follows empty assignment
and the family's sanitizer (range becomes its bounded default). Date getter distinguishes absent
results from present invalid Dates using HtmlInputDateResult.HasDate plus NaN. Browser owns fresh
actual Date objects and WebIDL object?/Date-brand ordering; the setter receives only an already
clipped actual Date slot or null. It never accepts host date objects or invokes user conversions.

Successful numeric/Date/step writes share the ordinary prepared string write and set DirtyValue,
including equal writes, with NonUser origin and preserved UserValidity. Step early returns and
exceptions preserve every observable flag and value. Formatting, parsing, sanitizer callbacks and
cancellation checks precede publication. No input/change events or artificial attribute records
are produced. Existing required/read-only applicability comes from HtmlInputTypes; reversed-time
range facts are authoritative native facts. Number UI presentation/badInput and temporal picker
behavior are separate slices; private user editing still covers only the six text families.

Callback overloads on pure constraint parsing, temporal conversion and range sanitization propagate
the supplied work checkpoint through source scans. The native string setter uses them for number,
temporal and range preparation; stepping uses them for long constraint/current-value scans. These
callbacks may throw constraint exceptions, which are never translated into invalid/empty values.

## Verification

InputNumericStateTests covers all seven families, demand boundaries, warm cache allocation,
metadata/base invalidation, equal writes, unchanged and projected-equal steps, exact publication,
fractional temporal policy, TimeClip distinction, huge lexical years, range history, reflected
attributes, selection/type/mode transfer, clone/import/adoption and cancellation/budget failures.
Existing unsupported-family tests now use color, retaining the same unavailable-transition contract.
Fresh Release net10.0 broad gate: 3,270 passed, zero failures/skips, excluding Corpus/Conformance.
Final multi-framework validation is deferred to the coordinator's integration gate as requested.
Invocation callback overloads for cold state construction and numeric getters/selection reads are
a separate follow-up; this checkpoint does not claim full Browser engine-budget propagation.
No WPT execution, benchmark, public API promotion, Browser cutover or PR is claimed.

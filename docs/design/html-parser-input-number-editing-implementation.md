# Native number editing (N2)

This is the independently approved number editor policy, layered on N1's actual value state. The
existing _value remains the sole API/submission string. An optional _numberPresentation retains
the authored UI string only when it differs from _value; the existing _selection remains the one
selection store. There is no parser initialization, engine, CWT, alternate current-value store or
temporal picker. All members remain internal until reviewed real consumers require promotion.

HasEditingBuffer includes the six text families and number; HasTextBuffer, public selection and
TextLength applicability are unchanged. GetEditingValue returns presentation or API value.
BadInput is derived for number from nonempty presentation plus empty API value. It does not claim
that numeric constraint facts are a complete ValidityState.

The explicit ASCII UI policy accepts display strings, uses the full strict finite number grammar,
and formats successful conversions canonically through the reviewed formatter. Empty yields empty
API and no badInput. Other nonempty partial/invalid/overflow strings remain visible with empty API
and badInput. Prefix parsing cannot turn `1e` into 1. `0001` displays as authored while API is `1`.
Attribute/script value initialization still preserves valid author spelling under N1 sanitization;
only this explicit user editor policy canonicalizes successful numeric edits.

ApplyUserValue checks mutable/read-only/disabled state, prepares grammar/conversion/formatting,
string comparisons and display-length selection, then flushes the final checkpoint before commit.
One invocation counter/cadence spans ancestry, both parser scans and comparison work. Changed
presentation dirties and records User origin even if API remains empty, invalidating N1 derived
value results. UserValidity is preserved. Selection-only edits retain ordinary flag behavior.
No native operation dispatches input/change events or attribute mutation records.

Script value assignment (including equal API writes), successful numeric setters/steps and reset
clear presentation. A changed API value moves the editing caret to its end, matching text writes;
an equal write preserves selection unless dropping presentation requires clamping to API length.
Number select() selects the actual display length without granting public selection APIs.
Effective type changes discard number UI selection/presentation; same-state
type spelling changes preserve them. Failures, unchanged steps, unrelated attributes and dirty
default writes preserve the editor. Clone/import copy only API/dirty authority and reset interaction
metadata; adoption retains the actual state. Fresh parsed and cold cloned numbers stay cold.

InputNumberEditingTests exercises incremental partial edits, valid/invalid/overflow strings,
canonical API versus display, private selection, read-only/disabled ancestry, clear/preserve paths,
clone/import/adoption, counters and atomic cancellation/budget failures. N1's requested cold range
history clone/import regression is also added here. No Browser cutover, WPT execution, benchmark,
public complete input API, or temporal editor is claimed.

Verification: fresh Release net10.0 broad non-Corpus/non-Conformance suite passed 3,299 tests,
zero failures/skips. Final multi-framework validation remains with the coordinator's integration gate.

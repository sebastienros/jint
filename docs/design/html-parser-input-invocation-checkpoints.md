# Input invocation checkpoints

This separate checkpoint supplies the per-call engine-work seam requested after N1/N2. Native
state does not retain a callback, engine or execution context. Existing token-only overloads remain;
callback overloads take `Action<int>?` immediately before CancellationToken.

HtmlElementState.GetInputValueState(checkpoint, token) passes the caller through cold input
construction. One invocation-owned struct counts the constructor, actual Attr visits and family
sanitizer work, checking every 256 units and flushing the final tail before sidecar publication.
An exception leaves ExistingInputValue absent and the document stamp unchanged. Warm lookup still
checks the supplied callback/token. Input CopyFrom remains an O(1) copy of authoritative immutable
strings/flags; clone consumers must use this overload for cold target initialization.

HtmlInputValueState callback operations cover Get/SetValue, Get/SetDefaultValue, ResetValue,
Get/SetValueAsNumber/Date, GetNumericFacts, StepUp/StepDown, text length, public/private selection,
Select, both SetRangeText forms, GetEditingValue and GetFacts. Cached string/flag/offset operations
perform a bounded invocation check. Long operations pass one native work struct by ref through
the pure number, temporal, constraints and range helpers. Existing `Action<long>` pure-helper APIs
remain compatible. No helper restarts the numeric counter when entering another parser phase.

Text sanitization continues the same counter through its existing HtmlTextWork. Range replacement
checks actual characters while copying, continues from the completed copy count, then polls
sanitization/comparison and final selection preparation before flags/selection commit. It preserves
the prescribed IndexSizeError dirty behavior after the invocation check. Default setter preparation
compares under the caller's budget and reuses the current string reference on equality, avoiding a
second unpolled equality scan inside the after-attribute hook. Successful writes keep N1/N2's existing
dirty/origin, UI clearing and caret semantics. No preflight character scan or DOM-held callback is used.

Numeric reads and stepping propagate work through long constraint/current-value scans and bounded
conversion arithmetic; cached results remain derived. Their final tail check precedes publishing a
new requested result or observable flags. Already completed independent constraint caches may be
retained even if a later facts scan fails; no partial result is cached. Number user editing now uses
the native work struct directly rather than allocating an int-to-long callback adapter.

InputInvocationCheckpointTests verifies cold metadata/sanitizer/tail failures, original cancellation
tokens, no sidecar publication/stamp change, exact shared counts, long numeric reads, warm callback
zero-allocation paths, atomic text/default/reset/splice writes and selection/short numeric tails.
The N1 callback cancellation regression now cancels at the native 256-unit cadence, strengthening
its stop boundary rather than expanding a ceiling. Native raw DOM attribute hooks remain their
existing token-only routes; Browser binding/clone cutover, textarea read seams and checkedness engine
checkpoints are separate consumer/producer integration work. No complete Browser-budget, WPT,
benchmark, public API promotion or deployment claim is made here.

Verification: focused Release net10.0 gate passed 155 cases; the fresh broad native gate after the
separate select availability repair passed 3,325 cases, zero failures/skips, excluding Corpus and
Conformance. Further builds are held for the coordinator's common validation slot; final supported
TFM validation is deferred as requested.

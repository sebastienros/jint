# Native input value component checkpoint

This internal checkpoint implements the six text families (`text`, `search`, `tel`, `url`, `email`,
`password`), Default value mode (`hidden`, `submit`, `image`, `reset`, `button`), and DefaultOn mode
(`checkbox`, `radio`). It follows the coordinator's amendment permitting complete internal components
before the remaining input families land. It does not publish a complete public Input state or claim
whole-control reset, validity, numeric/temporal/range/color/file support, or Browser event dispatch.

## Ownership and consumer seam

`element.GetHtmlState()?.InputValue` returns one stable `HtmlInputValueState` for an actual HTML input;
other element kinds return null. The element owns the current string, dirty flag, change origin,
user-validity flag, and one UTF-16 selection. The default string remains the actual value attribute.
Cached type/multiple/readonly metadata and the attribute identity make repeated facts/default/value/
selection reads allocation-free and independent of unrelated document mutations.

The instance supplies `GetValue`, `SetValue`, `GetDefaultValue`, `SetDefaultValue`, `GetFacts`,
`GetTextLength`, nullable `GetSelection`, nullable-offset selection setters, `SetSelectionRange`,
`Select`, and both `SetRangeText` overloads. `GetEditingSelection`, `SetEditingSelection`, and
`ApplyUserValue` use the same selection and value; email has an internal editing buffer while its public
selection operations remain inapplicable. Readonly/disabled block user value edits, but do not block
selection movement or script value setters. All explicit value/editing operations accept cancellation.
`ResetValue` is only the value-component step and does not reset checkedness.

`HtmlInputTextOperations` reuses the existing sanitizer, selection structs and type applicability.
Programmatic equal-value assignment dirties the value without moving selection. Equal user edits do
not fabricate provenance changes. Replacement offsets count UTF-16 code units, including surrogate
halves. Range error dirties before throwing IndexSizeError; wrong-type applicability throws first.

## Native lifecycle

Ordinary attribute routes capture the old input state before publication, including attached Attr.Value,
replacement and removal. Type changes transfer value modes, signal the checkedness component once,
sanitize, and initialize selection in HTML order. Same-state type spelling changes preserve flags,
origin and selection. An outer attribute record is queued before input change steps so nested value
attribute writes appear afterward. Namespaced attributes do not supply HTML input metadata.

Parser batches prepare the final component from the complete staged attribute list, with the original
cancellation token, before publishing attributes. They do not simulate intermediate type/value writes.
Cloning/import copy the current value and dirty flag; origin/user validity/selection begin fresh.
Adoption retains the same owned component and its interaction state.

Unimplemented families are named unavailable. No unsupported getter fabricates an empty/text value,
and no history buffer restores stale text after a crossing. An unsupported Value-to-Value crossing
remains unavailable on return until a supported explicit SetValue or ResetValue establishes the current
value. A non-Value-to-Value transition loads the real default and clears dirty as specified. Ordinary
attribute reflection and independent checkedness remain usable. DefaultValue is truthful even while
the current value is unavailable. Numeric helpers must extend this backing store rather than create a
second value/dirty/provenance/selection store.

## Sources and verification

The living [HTML input algorithms](https://html.spec.whatwg.org/multipage/input.html) and
[text selection algorithms](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#textFieldSelection)
are authoritative. [DOM attribute change ordering](https://dom.spec.whatwg.org/#concept-element-attributes-change)
was checked for the nested mutation ordering. The companion design is
[html-parser-text-control-state.md](html-parser-text-control-state.md).

`InputValueComponentTests` covers the supported transition matrix, sanitizers, default modes,
UTF-16/null/max offsets, four replacement modes, cancellation, readonly/disabled, reflected attribute
routes, parser attribute permutations, unsupported crossings, origin preservation, owned clone/import/
adoption, nested mutation records, and allocation-free reads. These are authored native tests; they
are not a claim that the pinned WPT corpus has been executed or passed for input value/selection.

## User-edit work checkpoints

Input and textarea retain their CancellationToken-only ApplyUserValue signatures and add an overload
with Action<int>? checkpoint before the token. One invocation-owned counter spans native disabledness,
readonly checks, sanitizer scanning/copies, cold textarea child projection, string comparison and
selection preparation. Its final tail callback runs before any observable value/dirty/origin/selection
commit. Browser supplies NativeReadCheckpoint to check its engine constraints; exceptions propagate
unchanged. Read-only cold preparation does not publish textarea raw/API caches. The commit avoids
another unpolled long string comparison. No per-edit counter object or callback wrapper is allocated.

UserValueCheckpointTests covers cross-stage cadence, short-operation tail checks, ancestry/sanitizer/
comparison cancellation, cold child projection, and constraint-callback exceptions. These tests assert
coherent native state and original cancellation, with no timing assertion.

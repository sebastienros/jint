# Native styling boundary

HTML parsing retains a native tree and source strings. `NativeCssStyleSheets.Install` is the
Browser loader handoff: it stores a stylesheet owner's identity, source text, source/base URLs
and a resource revision. It does not parse inline style attributes, run the cascade, compute
values or perform layout. CSS syntax and property validation are requested separately by
`Get`; a pending grammar remains a named completion failure, never an accepted declaration.
Materialized stylesheet identity survives source replacement. DOM order determines source
order, independently of fetch completion order.

`NativeCssQuery` owns matching candidates and memoized answers for one synchronous query.
It verifies native document, resource, stylesheet and inline-block stamps. Mutable states
and caches are not stored on native nodes or shared between queries. Matching uses native
selector state and invocation work; conditional rules use an immutable media snapshot.
Only a requested property and its dependencies compute. Enumerating every supported
property is a separate, explicit demand. The registry is the only source of initial values
and inheritance metadata; pending catalog entries supply neither.

Custom-property snapshots have immutable parent layers. A binding resolves references in
the layer where it was specified, even when a descendant overrides one of those names.
The substitution executor keys cycle detection and memoization by scope and name. This
preserves inherited computed semantics without evaluating every ancestor variable first.
Projected component tokens feed property validators directly, without retokenizing their
concatenated spelling. Computed custom text uses token-boundary-safe syntax serialization.
Deferred shorthands share one substitution result and the declaration producer's expansion
and serialization algorithms. An invalid winning declaration defaults at computed-value
time; it never resurrects an earlier candidate.

This is the initial integration checkpoint. Origin/importance/inline/specificity/source-order
selection, inheritance/defaulting, origin/rule rollback, deferred values, media filtering,
source identity and invalidation have actual-source tests in `Jint.Tests.HtmlParser`.
Typed numeric computation converts absolute/viewport/font-metric lengths and simplifies the
shared math graph on demand. Unresolved percentage bases stay typed percentages/calculations;
font metrics must be supplied explicitly. Color dependencies are still being integrated. Layer
rollback is explicitly pending. Browser call sites have not switched to this producer yet;
the source harness is not evidence that `getComputedStyle`, layout or protocol coverage
already use it. Used geometry belongs to Browser layout, not CSS syntax or validators.

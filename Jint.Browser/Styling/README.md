# Native styling boundary

HTML parsing retains a native tree and source strings. `NativeCssStyleSheets.Install` is the
Browser loader handoff: it stores a stylesheet owner's identity, source text, source/base URLs
and a resource revision. It does not parse inline style attributes, run the cascade, compute
values or perform layout. CSS syntax and property validation are requested separately by
`Get`; a pending grammar remains a named completion failure, never an accepted declaration.
Materialized stylesheet identity survives source replacement. DOM order determines source
order, independently of fetch completion order.

`NativeCssQuery` owns matching candidates and memoized answers for one synchronous query.
It verifies native document, resource, imported stylesheet graph and inline-block stamps,
including children whose media does not match. Mutable states
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

Origin/importance/inline/specificity/source-order
selection, inheritance/defaulting, origin/rule rollback, deferred values, media filtering,
source identity and invalidation have actual-source tests in `Jint.Tests.HtmlParser`.
Typed numeric computation converts absolute/viewport/font-metric lengths and simplifies the
shared math graph on demand. Unresolved percentage bases stay typed percentages/calculations;
font metrics must be supplied explicitly. Browser supplies its existing 16px initial font-size model for em/rem; it supplies no glyph or line-height metrics. CurrentColor follows the inherited color dependency, and named colors compute to absolute
coordinates. System colors require an explicit immutable host palette; the Browser handoff
supplies an explicit neutral light/dark canvas palette; other system colors retain a missing-input failure. Layer rollback remains explicitly pending. Supported display keywords blockify/inlinify from their box context, and overflow axes compute jointly. Cascade, read-only
declaration, generated bindings, native node tracking and CSS protocol consumers now target
this producer. Native query and Browser tests cover these boundaries; the cutover remains
in progress until production validation is complete. Used geometry belongs to Browser layout,
not CSS syntax or validators.

Browser defaults are inputs to the cascade, not declaration fallbacks. The supported HTML
user-agent display rules are namespace-filtered and apply in every tree; author rules retain
their owner tree scope. Inheritance follows native shadow hosts and fresh slot assignments.
Document stylesheet lists exclude shadow sheets; matching and coverage can request both.
Inline source reads use the shared bounded descendant-text producer. Type and media owner
attributes reconcile at CSS demand, with fetched link text retained independently.

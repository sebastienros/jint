Container queries use the native ordered `CssContainerRule` model and the existing cascade.
The finite geometry policy is `FlatLayout.SizeQuery`, the same synthetic dimensions returned by
rectangle queries. It does not implement CSS containment or a second renderer.

Selectors and raw declaration effects establish relevance before conditions demand metadata or
metrics. Shorthand/reset effects and custom names use the declaration block's existing index without
materializing unrelated values. Coverage enumeration intentionally evaluates matched condition chains.
The closest eligible flat-tree ancestor is selected, never the subject. No container or no box is
unknown, including under `not`. Unknown features eliminate selection for the whole condition.

The implemented metric is physical width and horizontal inline-size in CSS pixels; absolute length
thresholds use the native unit conversion. Height, block-size, aspect/orientation, vertical writing,
style/scroll-state queries, relative units, range syntax and comma-separated condition lists retain
named C6 dependencies. List branches remain typed and their rule/children survive projection; the
legacy containerName/containerQuery getters return empty strings for multiple conditions. The current
ordinary-parent box provider explicitly refuses shadow flat-tree geometry. There is no authored-width
or viewport substitution for a selected container's metric.

One invocation owns the native query, traversal, optional size query, dependency guards and completed
metric cache. A cycle reports `C6:container-layout-cycle`; cross-layer depth has a named 64-entry limit.
Failure discards the invocation. Reuse must retain its captured DOM/CSS/resource/semantic/media/viewport
and layout witnesses; unfinished measurements are never cached.

`CssSubstitutionSnapshot.CreateQueryLayer` is an explicit invocation-affine adapter. Only a requested
custom binding selects its condition; unused variables and fallbacks remain cold. The actual defining
layer remains the substitution scope. Existing `Create`/`CreateLayer` snapshots stay callback-free,
copied and shareable, and reject query-bound parents or supplied binding scopes. No model, node or
reference program may retain a query layer. The executor and its scope-keyed cycle behavior are shared.

Specification: https://drafts.csswg.org/css-conditional-5/#container-rule
This source packet requires normal binding regeneration and Release validation by the compiler owner.

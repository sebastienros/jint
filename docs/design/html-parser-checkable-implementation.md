# D7b2 native checkedness implementation handoff

Implementation starts at `0b9b921de`, on `codex/native-input-checkedness`. The coordinator explicitly
transferred Element, HtmlElementState and NodeCloner after D6/H8 completed. The new hooks preserve
Range scopes, IsValue, script flags, textarea state, cloning and mutation records. No Browser, CSS,
HTML/XML tree-building algorithms or public Input facade is changed.

## Internal consumer surface

`HtmlCheckableState.Get(Element)` returns the one element-owned `HtmlInputCheckedState` for exact
HTML-namespace lowercase input, including inputs currently in noncheckable states. Other element kinds
return null without creating their HTML view. Its Checked, DirtyCheckedness and Indeterminate flags
are independent. DefaultChecked reflects actual unnamespaced checked presence.

The component supplies SetChecked(bool, CancellationToken), SetDefaultChecked(bool) and
SetIndeterminate(bool). Checked IDL always dirties its receiver; Algorithm writes and peer exclusion
preserve dirtiness. UserInteraction dirties the directly operated input only when checkedness changes.
The index neither normalizes duplicate flags nor manufactures a selection.

`HtmlCheckednessAlgorithms.Set(Element,bool,HtmlCheckedChangeOrigin,CancellationToken)` is the
event-free algorithm lane. ResetCheckedness is only the checkedness step of whole input reset, and
preserves indeterminate. CopyCheckedness copies exactly the three flags, never owner/index handles.

`HtmlCheckableState.GetRadioGroupFacts`, SameRadioGroup, FirstCheckedRadio and SnapshotRadioGroup
accept a CancellationToken. The snapshot is immutable ordinary preorder and retains its members.
FirstCheckedRadio returns the sole checked member directly, and walks ordinary preorder only for
multiple checked identities. Nonradio facts are empty/non-applicable, including self comparison.
MatchesChecked, MatchesUnchecked, MatchesIndeterminate and MatchesDefaultCheckable implement only
the checkbox/radio portion of their categories. Radio indeterminate uses checked-group count,
independent of its stored indeterminate flag. Empty/missing names are singleton groups; other names
are exact ordinal UTF-16 names, with no trimming or normalization.

Browser can consume this internal slice using its existing IVT grant. Browser still owns dispatch-local
activation records, provenance choice, event sequencing and exception cleanup. It should capture both
flags on every input before activation and retain FirstCheckedRadio only for pre-activation Radio.
Canceled Radio restoration checks SameRadioGroup at rollback time and uses Algorithm, never peer IDL
setters. The completed value/type/reset families, generated binding overrides, successful controls,
validation and whole-form reset remain separate owners' acceptance gates. The native type-change
signal currently lives in HtmlInputStateChanges.AttributeChanged; b1d transfers this single signal to
the prescribed point after value-mode changes and before sanitization, rather than adding another.

A concrete parser-owner gap remains outside this packet: the current HtmlTreeBuilder retains `_form`
(`HtmlTreeBuilder.Body.cs` and `HtmlTreeBuilder.Tables.cs`) but never calls AssociateFromParser.
`HtmlTreeBuilder.cs` InsertElement initializes complete attributes and then inserts immediately.
Ancestor form association works through native insertion and is exercised by the real parser fixture;
misnested/fostered nonancestor parser-pointer ownership still needs the H/X parser owner to insert
AssociateFromParser at the creation seam. This packet does not alter those tree-building algorithms.
Its native API and flags are ready for that call; full b2b parser-owner acceptance remains that gate.

The independent attribute prerequisite is `Element.GetAttributeAt(uint)`: direct unsigned-bounds-checked
access to the owned attribute list, preserving Attr identities across replacement and removal.

## Integration and work boundaries

Complete parser attribute batches initialize intrinsic state once, after all effective attributes are
published, including XML DTD defaults and empty batches. Parsed merges use ordinary attribute hooks.
The native parser ownership API is called after initialization and before insertion in its dedicated
fixture; it is not claimed to be fully wired into the current HTML builder. Clone copies attributes, then flags,
then uses ordinary insertion semantics. There is no parser winner cache or EOF repair.

Form-owner stores now notify every actual identity transition, including null intermediate stores.
Equal form assignments run reset; equal ID assignments do not become an ID change. ID changes and
ID-bearing insertion/removal reset every indexed listed control with form-attribute presence,
including empty values, in ordinary preorder. The form candidate index covers all listed controls.

The ordering fallback builds and caches one charged ordinary-root preorder of indexed candidates:
O(N + F), including unrelated nodes, plus real owner resolution/group work. Repeated ID resets reuse
that immutable order and enumerate F candidates. Only candidate membership/order changes invalidate
the order, clearing the old array immediately; equal form-value writes and unrelated edits preserve it.
This avoids repeated ancestor/sibling comparisons and repeated unrelated-tree scans. Do not describe
an ordering rebuild as subtree-only or O(F). Ordinary unrelated class/text mutations do not invalidate
radio membership.

A root-owned index bootstraps once and thereafter uses element membership handles. Root members and
checked peers use dense lists with swap-removal positions. Group selection visits current checked peers,
including after a large multiple-checked set collapses to one; hash-table high-water holes cannot turn
that path into an unaccounted scan. Named buckets retain member/checked/required counts. Type and
required facts and name are cached from real attributes at component creation, complete batch
initialization and all effective mutation hooks, so hot facts do not rescan attributes. DefaultChecked
uses the cached actual checked Attr identity/presence, never an inferred default boolean. Cold metadata
initialization walks the direct attribute index with bounded work; cancelable queries can stage that
component before publication. Names are hashed/compared only on build or membership transitions,
using bounded work.

Cold construction stages groups and handles, checking cancellation before publication. Root ascent,
all node visits (including text/comment nodes), name code units, snapshot traversal and checked-peer
preparation share one local counter per cancelable call, polling at most every 256 units and before
publication/flag commit. The final cold publication installs already-prepared member handles in an
O(M) uninterrupted semantic commit after the last check; it adds no cancellation point halfway through
those handles. Dense commit operations do not throw cancellation halfway through exclusion.
Native parser insertion retains its existing committed-operation contract: cancellation after insertion
is reported only after ownership, membership and flags are coherent. Synchronous DOM hooks have no
caller token; their cleanup/rekey/connection steps complete before the enclosing parser boundary.

Removal releases old ordinary-root members promptly, preserving membership in shadow roots whose
ordinary root is unchanged by host detachment. Moving an indexed detached root into another tree
retires its old index. Empty buckets release owner/name references. An empty stable root may retain
its empty index for future incremental insertion; it retains no removed members or names.

## Source evidence and remaining ownership gates

Read `radio.html` and `checkbox.html` at WPT pin
`6c7127bdd9f2cc6a3668fd9791757843e09d5a9e`, and the locally vendored
`html/semantics/selectors/pseudo-classes/indeterminate-radio.html` at that pin:

- https://github.com/web-platform-tests/wpt/blob/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/forms/the-input-element/radio.html
- https://github.com/web-platform-tests/wpt/blob/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/forms/the-input-element/checkbox.html
- https://html.spec.whatwg.org/multipage/input.html#radio-button-state-(type=radio)

Pinned radio cases cover non-ASCII names, orphan trees, distinct form owners/roots, empty names, peer
selection and canceled activation. Checkbox cases primarily exercise eventful activation/rollback.
The selector case distinguishes radio-group indeterminate from the stored IDL flag. These sources were
read as evidence, not executed as WPT passes. Authored native tests are not WPT passes. The two form
source files are not in the local vendored selection. They do not directly test the Living HTML
connected-insertion correction's disconnected multiple-checked construction; native fixtures separately
pin that newer rule. The new native code implements no event/disabled/trusted-versus-synthetic policy.

Native tests include attribute/dirtiness/type/name/namespace matrices, effective XML defaults, parser
owner and commit ordering, clone/import/adoption/document clone, fragments/templates/open/closed/nested
shadow roots, owner intermediates and complete ID reset candidates. A separate 400-step deterministic
slow oracle derives ordinary roots, names and owner identities from the tree and reads actual flags;
it never uses production membership/root/classifier helpers or normalizes flags.

Deterministic work gates cover 1/1,000/10,000 members, zero-allocation hot facts, dense 10,000-to-one
checked collapse, incremental append, linear wide-gap ID resets, non-element/deep/name cancellation,
canceled exclusion preparation, committed parser cancellation, and retained-document weak lifetime.
These are untimed correctness/work assertions, not benchmark numbers. Full Browser listener/type-change/
nested-dispatch/exception cleanup and public-consumer coverage remain later owner gates. No public
partial Input promise is made.

## Final validation

Fresh Release focused native/accessor run: 90 succeeded, zero failures or skips across net8.0/net10.0.
Fresh Release broad non-corpus native run: 4,240 succeeded, zero failures or skips across net8.0/net10.0
(2,120 per target framework). The broad filter excludes names containing Corpus or Conformance.
Both builds compile with warnings treated as errors; no --no-build was used. The local multi-source
NuGet configuration required a per-command RestoreSources override to the public NuGet source.
No WPT execution, Browser cutover, public partial Input API, benchmark or PR is claimed.

## Demand boundary for non-radio checkedness

The reviewed C1 amendment keeps all radio state eager to retain peer-exclusion history. Non-radio
inputs, including untouched checkboxes, remain cold during parsed initialization, unrelated/derived
attribute changes, insertion and form-owner changes, group-index construction, applicable selector
reads, and cold clone/import. No new field widens Element. A bounded read-only type classifier checks
applicability before allocating a component. Attribute predicates derive clean defaults while no
component exists; every explicit flag mutation materializes the same authoritative three-flag store.

An absent component proves there has been no explicit flag write or radio exclusion. First semantic
materialization loads Checked from actual unqualified checked-attribute presence, with dirty and
indeterminate false, after metadata preparation and before publication. This is logical initial
state, not a query-time SetCore repair or document mutation. Radio initialization still invokes the
existing equal-true group algorithm; equality never suppresses peer exclusion. Once created, a
component is retained even on leaving radio or returning to default flags. A clean radio loser with
a checked attribute therefore remains unchecked across later type/move/owner transitions.

CheckedLazyStateTests covers real parser allocation boundaries, all non-radio predicates, logical
initialization and cancellation, index isolation, reflected attribute forms, clone/import/adoption,
explicit flags on text inputs, radio loser history and seeded cold/warm mutation worlds. Existing
oracle, dense membership, lifetime and original cancellation ceilings remain in force. Clone's cold
source allocation test now tests a materialized source and cancellation inside its cold target,
since a cold non-radio clone deliberately has no checkedness constructor work to cancel.

C1 verification: the fresh Release broad native suite passed 6,070 tests across net8.0 and
net10.0 (3,035 per framework), excluding Corpus and Conformance. No WPT or benchmark claim.

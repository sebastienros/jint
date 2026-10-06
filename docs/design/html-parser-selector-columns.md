# C2d: HTML table-column selector evaluation

Design dispatch for independent review, 2026-09-23. This completes the column row in
[the selector contract](html-parser-selectors.md); it does not publish the selector API or change
Browser bindings. The native library needs no browser, stylesheet, layout, feature switch or table
configuration. Keep `||`, `:nth-col(An+B)` and `:nth-last-col(An+B)` in the accepted grammar.

## Evidence, baseline and decisions requiring review

The authoritative selector definitions are now [Selectors 5, section 9](https://drafts.csswg.org/selectors-5/#table-pseudos)
and [its grammar](https://drafts.csswg.org/selectors-5/#grammar), in the 18 August 2026 editor's draft.
They specify a column-element-to-cell relation and existential matching when a cell occupies several
columns. Membership comes from the document language, independently of presentation. Both nth forms
apply to cells; they do not select `col` elements. Column combinators have no specificity of their own;
each nth pseudo contributes one class component. No `of <selector-list>` argument is admitted here.
The move from Selectors 4 to 5 did not turn these already accepted constructs into syntax errors.

For HTML, use [forming a table](https://html.spec.whatwg.org/multipage/tables.html#forming-a-table),
including its malformed-input algorithm, not the `table.rows` collection ordering or CSS anonymous
table boxes. Source inspected 2026-09-23. A column projection of that algorithm suffices; header
association, visual geometry and a general accessibility table model are outside C2d.

The inspected common base is `04c75b18f`. `CompiledSelector` already contains `Combinator.Column`,
`PredicateKind.NthCol/NthLastCol` and exact `BigInteger` A/B operands. The common matcher deliberately
rejects them in its internal staging guard. C2c's `6567860cf` plus `438728ea1` adds the iterative
relational frame evaluator but is still under review: rebase on its accepted final version before
reserving matcher files. Its `BranchPosition.Next`, `LeadingMatches`, and `NextRelativeCandidate`
must all gain real column semantics; adding only a predicate switch is insufficient.

The pinned AngleSharp 1.8.2 source at `35b26db83557a6a74ce286907833e3b17806d9eb` is a compatibility
baseline, not the semantic oracle:

| Source | Observed behavior | Required correction |
| --- | --- | --- |
| [CssCombinator.GetColumnCells](https://github.com/AngleSharp/AngleSharp/blob/35b26db83557a6a74ce286907833e3b17806d9eb/src/AngleSharp/Css/Parser/CssCombinator.cs), [CombinatorCursor](https://github.com/AngleSharp/AngleSharp/blob/35b26db83557a6a74ce286907833e3b17806d9eb/src/AngleSharp/Css/Parser/CombinatorCursor.cs) | Walks peer cells at a sibling-colspan-derived index | `col` to participating cells, with actual table placement |
| [FirstColumnSelector](https://github.com/AngleSharp/AngleSharp/blob/35b26db83557a6a74ce286907833e3b17806d9eb/src/AngleSharp/Css/Dom/Internal/FirstColumnSelector.cs), [LastColumnSelector](https://github.com/AngleSharp/AngleSharp/blob/35b26db83557a6a74ce286907833e3b17806d9eb/src/AngleSharp/Css/Dom/Internal/LastColumnSelector.cs) | Counts spans among siblings, forward or backward | Include earlier rowspans and the completed table width |
| [CssSelector tests](https://github.com/AngleSharp/AngleSharp/blob/35b26db83557a6a74ce286907833e3b17806d9eb/src/AngleSharp.Core.Tests/Css/CssSelector.cs) | `ColumnCombinator_BasicTableSelection` expects `td:nth-child(2) || td` to match peer cells | Name this standards correction in cutover tests; do not import that expected result |

Current [CSSWG discussion](https://github.com/w3c/csswg-drafts/issues/10510#issuecomment-2197745436)
also illustrates `col:has(|| th[aria-sort])`. This supports a forward column relation inside `:has`;
it is not a separate pseudo-element or evidence of implemented browser interoperability.

Three choices must remain visible in independent review:

1. **Only participating HTML `col` elements supply the left side of `||`.** HTML distinguishes
   columns corresponding to `col` from column groups corresponding to `colgroup`. This dispatch
   interprets the selector's column-element wording using that distinction: `colgroup || td` has no
   match, while `colgroup.selected > col || td` works. An empty `colgroup span=3` affects numbering
   and table width but supplies no synthetic column element. This is an explicit interpretation of
   the underspecified host-language mapping, not a claim that Selectors spells out `colgroup` or
   that WPT settles it. Broader group membership must receive a source-backed design correction,
   not a hidden implementation convenience or option.
2. **Malformed DOM follows the algorithm, not parser repair.** A directly appended `col` under a
   table is ignored by the column-group phase. The spec's HTML-text example with bare `col` gets
   its implied `colgroup` from the HTML parser. Do not create that group during matching. A late
   `colgroup` after row processing begins is likewise not processed as an earlier declaration.
3. **Table model errors preserve assigned slots.** Overlapping cells retain both identities and
   intervals. Missing anchors/holes do not shrink dimensions or abort all matching. The algorithm
   returns its table after identifying errors; C2d neither repairs it nor throws a selector syntax
   exception. It need not expose diagnostics or compute header associations to obtain columns.

## Finite ownership and internal seam

One C2d owner implements the projection and matcher integration in two reviewable commits. Reserve
the C2c files only after that owner releases them. No edits to Element/Node/Document, parser insertion
modes, C1, Browser generated code, WPT vendoring, signing, public snapshots or benchmark files.

| Commit | Owned files | Deliverable |
| --- | --- | --- |
| C2d1 | New `Jint.HtmlParser/Dom/Html/HtmlTableGrid.cs`, `HtmlTableGrid.Builder.cs`, `HtmlTableGrid.Occupancy.cs`; new `Jint.Tests.HtmlParser/Html/TableGridTests.cs`, `TableGridWorkTests.cs` | Working internal, namespace-aware column projection with sparse occupancy and bounded cancellation |
| C2d2 | New `Jint.HtmlParser/Css/Selectors/SelectorMatcher.Columns.cs`; reserved `SelectorMatcher.cs` and `SelectorMatcher.Relational.cs`; new `Jint.Tests.HtmlParser/Css/Selectors/ColumnSelectorTests.cs`, `ColumnSelectorWorkTests.cs` | Column backtracking, both nth forms and complete relational candidate discovery |

All model types live in namespace `Jint.HtmlParser`, remain internal and are immutable after build.
Co-locate small model records with `HtmlTableGrid`; do not publish an AST or table-grid API. Exact seam:

```csharp
internal sealed partial class HtmlTableGrid
{
    internal Element Table { get; }
    internal long ColumnCount { get; }
    internal static HtmlTableGrid Build(Element table, CancellationToken cancellationToken);
    internal bool TryGetCellColumns(Element cell, out long start, out long endExclusive);
    internal bool TryGetColumnColumns(Element column, out long start, out long endExclusive);
    internal IEnumerable<Element> ColumnsOverlapping(long start, long endExclusive,
        CancellationToken cancellationToken);
    internal IEnumerable<Element> CellsOverlapping(long start, long endExclusive,
        CancellationToken cancellationToken);
}
```

Required null arguments throw `ArgumentNullException`. Build requires an HTML-namespace, exact
lowercase `table`, otherwise `ArgumentException`. TryGet methods return false and zero outputs for
elements absent from this model, including lookalikes or elements in another table. Range queries
require `0 <= start <= endExclusive <= ColumnCount`, otherwise `ArgumentOutOfRangeException`; an
empty interval yields nothing. Iterators validate on first enumeration and remain invocation-local.
They return each native identity once; columns in increasing column-start order, cells in model
processing order. Neither order replaces DOM query-result order. Model construction publishes only
after completion, including final cancellation checks. These are internal working operations, not
public promises or stubs for future layout functionality.

## HTML participation and projection algorithm

Use exact namespace `http://www.w3.org/1999/xhtml` and exact lowercase local names for every semantic
role. HTML tables inside XML/XHTML documents have these semantics too. No-namespace XML `table`, SVG
lookalikes, uppercase XML `TABLE` and CSS `display:table` elements do not. Namespace declarations and
selector namespace bindings do not change the stored namespace or confer a table role.

A candidate cell must be an HTML `td`/`th` directly under an HTML `tr`. That row must be directly under
the table, or directly under an HTML `thead`/`tbody`/`tfoot` directly under the table. A column must be
an HTML `col` directly under an HTML `colgroup` directly under the table, and the group must actually
be consumed by the table's column phase. Discover this fixed parent shape rather than repeatedly
walking arbitrary ancestors to a nearest table. Building a model uses only the specified direct
children. Irrelevant elements are skipped without traversing them. A wrapper around a row or cell
does not make it participate. Foreign nodes cannot act as transparent row/group wrappers.

This permits detached tables. A detached row, cell or group without its required table does not
acquire invented columns. Nested tables are independent models; their cells never enlarge the outer
table. A nested table remains independently queryable through ordinary traversal. Do not traverse
template content, shadow roots, slot assignments or Host links while building an outer table. A
complete table inside an explicitly queried fragment or shadow root works within that ordinary tree.

Translate the linked HTML algorithm into an explicit cursor/state machine. Preserve its advancement
and EOF jumps, with these implementation invariants:

| Phase | Projection invariant and traps |
| --- | --- |
| Initial scan and column groups | Only the initial processed groups contribute declarations. For a group with direct HTML `col` children, use their spans in order and ignore the group's own span. Otherwise the group's span reserves implied columns. Non-col children neither contribute nor hide later direct col children. Consecutive groups can be separated by ignored children. |
| Rows | Process direct rows and direct row-group children as the algorithm visits them. Queue every encountered `tfoot` for the final phase in tree order. Do not hoist a late `thead` ahead of earlier rows as `table.rows` would. Ignore irrelevant children without manufacturing a row. Empty tr elements still advance the row coordinate. |
| Cell placement | At each row start x=0. Before each direct td/th, find the first uncovered slot at or after x; place the cell there with its complete colspan, even if later slots in that width overlap earlier rowspans. Then advance x by that colspan. Do not search for a completely vacant rectangle, sum sibling indices, or discard a conflicting cell. |
| Heights and row groups | Positive rowspans extend the model height, including implied rows. Rowspan zero begins with height one and remains downward-growing until the algorithm ends that run/group. Ending a group grows these cells through the established height and clears the downward-growing set. A positive span is not truncated to the number of explicit tr children. |
| Final width | Maximum of declared column extent and all placed cell right edges, across all processed groups and pending footers. Missing cells and trailing declarations remain columns. No phantom Element is created for an implied column. |

The cursor's EOF transition deserves its own test. HTML's current advancement rule jumps straight
to **End**, which processes pending footers; it does not insert an extra end-row-group call first.
For a manually built table whose first child is `tfoot` containing F, followed by a direct `tr`
containing A with rowspan=2, the literal algorithm places A at x=0,y=0 and F at x=1,y=1 (width 2).
The direct-row span is still active when the queued footer starts. This is a malformed/model-error
edge, not a browser layout promise. Preserve that sourced control flow rather than silently inserting
a cleanup that moves F to x=0. If a future normative correction changes it, revise this fixture and
dispatch together. Ordinary explicit row groups end via their actual end-group step as usual.

Only namespace-less, exact local-name `span`, `colspan`, `rowspan` attributes are inputs. Parse with
[HTML's non-negative-integer rules](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-non-negative-integers):
parse a signed integer prefix after ASCII leading whitespace, then reject a negative result.
Thus `"  +2tail"` means 2, `"2.5"` means 2, and `"-0"` or `"-000tail"` means zero; `"-1"`, a sign
without digits, or non-ASCII digits fail. In particular, rowspan=`"-0"` grows downward too.
Missing/invalid/zero span and colspan give 1; clamp positive
values to 1000. Missing/invalid rowspan gives 1; zero is meaningful; clamp positives to 65534. Parse
arbitrarily long digit prefixes with a saturation threshold sufficient for these decisions, poll
while reading them, and never use overflow as a fallback to 1. No quirks-mode exception for rowspan=0.
Reflecting IDL setters and WebIDL integer conversion are separate Browser/native-DOM work; matching
reads the resulting content attributes and does not emulate those setters.

## Matching, scopes and relational candidates

Give each top-level matcher invocation one lazy transient table-model dictionary shared by all
branches, candidates and nested frames. The compiled selector retains none of it. A query builds
each participating table at most once; a non-column program allocates no table cache. Build the
whole relevant table even when the requested cell or query root is earlier or smaller: a later row
or footer can change `:nth-last-col`. This is justified table-semantic materialization, not permission
to snapshot unrelated document subtrees. Subsequent calls rebuild from current native state, so
span edits, Attr.Value, insertion/removal, reorder, clone/import and adoption cannot reuse stale grids.
Concurrent mutation during evaluation remains unsupported. No document cache or mutation hook is added.

For a cell interval `[s,e)` and table width W:

* `:nth-col(A n+B)` succeeds when the integer interval `[s+1,e]` contains some A*n+B with n>=0.
* `:nth-last-col(A n+B)` uses `[W-e+1,W-s]`.
* `left || right` evaluates `left` against each participating col whose interval intersects `[s,e)`.
  The right-hand subject must itself be a participating td/th at that combinator position.

All nth indices are positive and one-based. A colspan cell can satisfy both odd and even, first and
last, or two column predicates in the same compound via different occupied columns. No single
column witness is shared between predicates. The relation is directional, not symmetric: `td || col`
and `td || td` do not gain a relation, nor does `col || col`. A following ordinary combinator still
works: `col.hot || td > em` selects the em after matching its parent cell at the column edge.
`colgroup:nth-child(2) > col || :is(td,th)` tests the group via ordinary ancestry, not group-as-column.

Use an interval/An+B intersection calculation over exact compiler operands, not a loop over W or
floating-point arithmetic. For positive A, intersect n>=0 with
`ceil((L-B)/A) <= n <= floor((H-B)/A)`. For negative A, set D=-A and use
`ceil((B-H)/D) <= n <= floor((B-L)/D)`. For A=0, check L<=B<=H. Division must implement mathematical
floor/ceiling for negative operands, not C# truncation. Reuse the existing exact numeric representation;
do not narrow W or A/B to int. Charge/check work around arithmetic and input-dependent preparation.

Column predecessor state belongs to a branch position, including source cell and resumable column
cursor. It cannot be reconstructed from the previously returned col alone. Backtracking must try
other overlapping col elements when one fails the preceding compound or its left-hand chain. Each
col identity is considered once even when its span and the cell intersect at several slots.
Preserve the original `:scope`, namespace rules and effective matching-list specificity. Query roots
restrict returned candidates, not the table's model or the normal left-side matcher: a query rooted
at tbody may match a col outside that subtree. Closest remains an ordinary ancestor walk.

Extend C2c **both** for a leading column in `:has(|| td.x)` and for columns later in a relative branch,
such as `table:has(> colgroup > col.hot || td > em)`. The existing descendant/sibling region heuristic
cannot simply be retained for these branches: cells related to a col are outside the col's subtree.
`LeadingMatches(first, anchor, Column)` means anchor is the actual participating col and first is
one of its cells, not that anchor is another cell in the same column.

For relative branches containing a column edge, use an iterative forward structural candidate walk
from the anchor: ordinary forward combinators use child/descendant/following-sibling links, and a
column edge uses this model's CellsOverlapping. Candidate discovery may over-include predicates,
then let the existing backwards frame evaluator prove the complete anchored selector. A visited
set keyed by (relative branch position, node identity) prevents repeated structural paths from
expanding the same state. It is per invocation/anchor and includes position, not only node. Do not
scan an entire document independently for every col anchor. Do not traverse Host/composed links.
Virtual fragment scope supplies no columns of its own, but its descendant tables remain reachable
through ordinary relative edges. `:has` still has its existing strict/nesting/pseudo-element rules.

Final QuerySelectorAll results remain unique native identities in ordinary DOM preorder, not column
order, slot order, model row order or match-branch order. QuerySelector returns the first such match.
TryMatch uses the highest specificity of matching top-level branches; span multiplicity cannot
increase it. The table cache is shared through nested `:is/:where/:not/:has` and nth-child `of` filters.
Remove only the three column staging refusals after all these paths work; other unfinished kinds
must retain their explicit staging guard until their owners finish them.

## Bounded storage, work and cancellation

Store one half-open column interval per participating cell and explicit col, plus identity maps and
query indexes. Do not allocate width*height slots, expand rowspan*colspan rectangles, create one
entry per implied row/column, or materialize all col-cell edges. Use long coordinates and checked
arithmetic; a width contribution is at most 1000 per participating declaration/cell and a height
contribution at most 65534 per processed cell/row, so document the representable-node bound instead
of narrowing, wrapping or silently clamping table dimensions. No new parse limit or table-size flag.

A concrete admissible occupancy implementation is a sparse segment tree over column coordinates,
holding the exclusive row-expiry of occupancy. A cell performs range-chmax of its expiry over its
column interval. A subtree stores minimum expiry plus a lazy maximum floor; a search for the first
position whose expiry<=currentY skips wholly occupied ranges. Overlap never erases a longer-lived
cell. Use a distinct forever value for zero-rowspan cells until the actual end-group step; do not
let ordinary integer overflow manufacture it. Each range operation is O(log U), where U bounds the
coordinate domain, with explicit bounded stacks. At end-group, jump currentY to height and clear
the active structure; there are no remaining ordinary spans beyond that height. This algebraic
jump is equivalent to growing through implied empty rows and avoids iterating them. Do not perform
that clear on any additional path, including the EOF edge described above.

Other sparse structures are allowed only with a work bound and equivalent overlap tests. A flat
list whose insertion shifts all later intervals or whose next-free search scans all previous cells
per row is not an acceptable substitute. Model build should be O((nodes inspected + cells + cols)
log U), independent of total slot area. Plain geometric list/array growth and index building must
remain amortized and cancellation-aware; no repeated whole-table sort per candidate.

Columns are disjoint ordered intervals, so reverse relation lookup uses a binary-search start plus
the overlapping run. Forward lookup for `:has` needs an augmented interval index over cell ranges
with O(log N + K) reporting (or a documented comparable bound), not N-cell scanning for each of N
col anchors. Build this index once per table/invocation and lazily if no forward query needs it.
Keep storage O(N log U) or better, not O(N squared) relation lists. Intrinsically numerous matching
paths can cost their output; avoid inventing a blanket linear guarantee for arbitrary `:has`.

Check cancellation on entry and before successful return, and at most every 256 work steps: DOM
links including skipped foreign/text nodes, attribute characters, occupancy/index operations,
model list copies, relative-state discovery, failed predecessors, enumeration and final result
freezing. Keep each component's polls bounded and pass the invocation's original token; never reset
engine constraints or swallow cancellation as no match. Large array clears/sorts/copies must not
hide unbounded work between polls: use segmented storage or bounded loops/iterative algorithms.
Models and partially built indexes are local until complete and discarded on cancellation. Clear
pooled node references in bounded chunks; otherwise prefer ordinary invocation-local storage.
Test checkpoints/counters may be passed per invocation internally, never stored on programs or DOM.

## Finite correctness and work gates

Use native construction for malformed structures so HTML parser repair cannot erase the test case.
Use parser-produced fixtures separately when H5/H6 supplies the required modes. Each test asserts
identities/intervals, not only result counts. Concrete anchor fixtures:

| Fixture | Required result |
| --- | --- |
| Spec example: col span=2 then col.selected; rows A/B/C, D(colspan=2)/E, F/G(colspan=2), under a real colgroup | `col.selected || td` gives C,E,G in tree order; G is returned once |
| Row A(rowspan=2)/B, then C | A occupies column 1; B and C column 2; C does not match nth-col(1) |
| Row A/B/C, then D only | Width 3; nth-last-col(1) gives C, not D; D matches nth-last-col(3) |
| Declared span 5, single cell A | Width 5; A matches nth-last-col(5), not nth-last-col(1) |
| Row A/C(rowspan=2), then B(colspan=2) | B occupies [0,2), overlapping C at column 2; both match nth-col(2); B is not shifted to column 3 |
| Zero-rowspan A plus two rows in tbody1, then a cell in tbody2 | Later tbody1 cells skip A's column; tbody2 starts at column 1; same in quirks mode |
| Empty colgroup span=3 then group with col X span=2 | X occupies [3,5); no synthetic left-side col for the first three columns; neither group itself is a column predecessor |
| Table with pending footer F followed by a direct row A(rowspan=2) | The exact EOF/model-error placement described above; no injected group-ending repair |

Add finite cases for zero/negative-zero/missing/invalid/plus/trailing-junk/huge decimal spans and both clamps;
namespaced attributes; group span ignored when col children exist; comments/foreign children between
cols; direct/late/wrapped colgroup/col; empty rows/groups; repeated thead/tbody/tfoot and footer
deferral; positive rowspans exceeding explicit group height; zero and positive rowspans overlapping;
multiple zero spans; holes; a completely empty table; zero-height declared columns; and no declarations.
Test HTML namespaces inside XML, uppercase/no-namespace/SVG exclusions, detached and nested tables,
template/shadow boundaries, style/hidden/dir/display changes having no column-placement effect.

Matcher gates cover `||` at multiple chain positions, failed first predecessor followed by a match,
negative/zero/large A/B, both ends of a colspan, conjunctions with different column witnesses,
strict/forgiving functional contexts, `:has` leading and internal columns, universal left-side
selectors, cells containing nested tables, tbody-scoped queries with external cols, closest, branch
specificity and immutable result order. Preserve compiler tests for `||` versus namespace `|`,
forbidden whitespace between the pipes, comments/token adjacency, nth argument errors and forbidden
`of`. No compiler weakening or additional syntax catalogue is part of C2d.

Run the same compiled program before/after span edits through SetAttribute/Attr.Value/removal,
row/group/col reorder, adoption into another table, detach/reattach and clone/import. Assert source
and destination answers, and that an earlier returned result list is unchanged. A retained compiled
program must not keep queried documents alive after calls finish.

Deterministic work counters must establish one model build per table/query, no work proportional to
rowspan*colspan or implied height, and no all-cells scan per column for `:has`. Double flat row/cell,
column and overlap-heavy inputs; distinguish expected log factors and reported K from quadratic
rescans. Cancel in a long invalid-attribute digit run, skipped nodes, occupancy search/update, index
construction, unsuccessful relative discovery, final copies and cleanup. Pre-cancelled empty/no-match
paths throw too. These are correctness/work tests, not stopwatch benchmarks.

## WPT evidence and completion

Repository pin: `6c7127bdd9f2cc6a3668fd9791757843e09d5a9e`, from `Jint.Tests/Wpt/Vendor/README.md`.
The local vendored HTML/JS search contains no dedicated nth-col/column-combinator cases. Inspection
of upstream `css/selectors` and its `old-tests`, `selectors-4`, and `parsing` directory listings at
that pin found no directly named column suite; do not turn that limited discovery into a claim of
comprehensive upstream column conformance. Selectors 5 section 9 links no test block at inspection.

The pinned upstream [table processing-model fixtures](https://github.com/web-platform-tests/wpt/tree/6c7127bdd9f2cc6a3668fd9791757843e09d5a9e/html/semantics/tabular-data/processing-model-1)
were read: `col-span-limits.html`, `span-limits.html`, `rowspan-0.html`, `rowspan-0-quirks.html`.
They support the clamps and zero-span behavior, including quirks. Their geometry assertions and
IDL reflection checks are not column-selector assertions and cannot be counted as passing via a
native interval adaptation. Add attributed native fixtures for those structural consequences;
vendoring or expanding the Browser WPT census remains a separate owner-controlled change.

C2d1/C2d2 run fresh Release tests on net8.0 and net10.0 using
`dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release --framework <tfm>`.
Re-run the full native selector suite after C2c integration. No runtime benchmark or Browser cutover
is authorized here. Completion requires the model-error and colgroup interpretation above to have
independent review, all three accepted column constructs to evaluate through ordinary and relational
paths, and the named AngleSharp corrections to be carried into later Browser cutover regressions.
C2 publication still depends on the remaining native/environment/shadow predicate families.

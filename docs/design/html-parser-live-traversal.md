# D6: live ranges and traversal across native mutations

Independently reviewed design, 2026-09-23. Completes the range/iterator part of D6 in
[the architecture](html-parser.md), alongside [shadow ownership](html-parser-shadow.md),
[D5 mutations](html-parser-mutations.md), [native follow-ups](html-parser-native-followups.md),
and [form state](html-parser-form-state.md). This is a dispatch contract, not implemented behavior.
The inspected native tree has links, adoption, templates, character data, parser mutation lanes and
observers, but no live ranges, splitText/normalize fixups, NodeIterator or TreeWalker.

The authoritative algorithms are current [DOM ranges](https://dom.spec.whatwg.org/#ranges),
[mutation algorithms](https://dom.spec.whatwg.org/#mutation-algorithms),
[CharacterData](https://dom.spec.whatwg.org/#interface-characterdata),
[Text](https://dom.spec.whatwg.org/#interface-text), and
[traversal](https://dom.spec.whatwg.org/#traversal). Browser selection follows the separate
[Selection API](https://w3c.github.io/selection-api/). Use the current candidate-reference iterator
algorithm; older versions with one iterator position are insufficient.

## Consumers and ownership

The [A1 inventory](../../tools/html-parser-inventory/inventory.lock.json) and actual call sites require:

| Existing consumer | Required replacement and owner |
| --- | --- |
| `Dom/Views/DomNodeIterator.cs`, `DomViewMembers.cs`, generated NodeIterator | Native root/reference/before/candidate and pre-removal repair; Browser retains the exact JS filter object and WebIDL callback conversion. The two AngleSharp iterator workaround and its million-step SyncLimit disappear after native identity assignments work. |
| `Dom/Views/DomTreeWalker.cs`, generated TreeWalker | All seven movement algorithms, currentNode assignment outside root, filter state and termination. No removal retargeting: TreeWalker current stays the same node. Preserve the existing fix for previousNode's infinite loop. |
| Generated Range; `DomRangeMembers.cs` | Boundary getters/setters, collapse, selectNode/Contents, four comparisons, point/intersection checks, cloneRange, delete/extract/clone contents, insertNode, surroundContents, stringification and no-op detach. Native algorithms replace AngleSharp plus the point-order/template/namespace/PI repairs; do not leave a second range model in Browser. |
| `DomStaticRange.cs` | Immutable endpoint snapshot. Construction accepts reversed, out-of-bounds and different-root points; it rejects Attr and DocumentType. B2 corrects the current start-first dictionary reads to WebIDL's lexicographic getter/conversion order: endContainer, endOffset, startContainer, startOffset. Preserve unsigned offsets; D6r1 native construction performs no JavaScript conversion. |
| `Dom/Views/JsSelection.cs`, `Events/ContentEditing.cs` | Selection retains actual native range identity. Editing uses that range, not a second caret copy. Direction, anchor/focus interpretation, association with a browsing document and JS errors are Browser state. |
| `Events/SelectionChange.cs`, `SelectionChangeTests.cs` | Native endpoint changes need a scheduling-only signal, including mutations through getRangeAt and automatic tree fixups. Current missed notifications are an explicit standards correction at B3 cutover, not a retained limitation. |
| `DomProcessingInstructionAttributes.RangeDataReplacement`, template cloning helpers | Data replacement and partial/full clone semantics become native. PI pseudo-attribute invalidation is owned by its native metadata follow-up and must run for equal-value replacement too; Browser repair is removed only when that prerequisite works. |
| Range geometry, contextual fragments | `getClientRects`/`getBoundingClientRect` stay with Browser's layout/CSSOM View owner; no native fabricated rectangles. `createContextualFragment` is a required H7/X2/X3 + B2 follow-up even though not in today's generated Range members. Native live traversal does not parse markup. |

Existing gates include `Views/NodeIteratorTests`, `Views/TraversalTests`, `Views/SelectionTests`,
`Dom/RangePointTests`, `Dom/StaticRangeTests`, `Dom/ProcessingInstructionRangeTests`, template range
cases in `Dom/HtmlSerializationTests`, and `Events/SelectionChangeTests`. The documented
`Range-adopt-test.html` debt (ranges stop updating after adoption) must be fixed, not translated.
Preserve current Browser one-range selection behavior. Its documented loss of backward selection
direction is separate B3/Selection work: record it explicitly during migration; do not make native
Range directionful to preserve it or claim complete Selection conformance with it outstanding.
The inspected JsSelection also mutates the existing range in collapseToStart/End, silently ignores a
non-associated removeRange, and implements containsNode as intersection without reading its partial-
containment argument. B3 must correct these deliberately with old/new identity and error fixtures;
they are not compatibility modes in native Range. Inventory current Selection bindings against the
full current interface, including extend/setBaseAndExtent, direction, modify and getComposedRanges;
missing members remain named B3 work. Shadow rescoping for getComposedRanges produces StaticRanges
at that API boundary and never changes the live range's ordinary-tree endpoints.

## D6r1: real, independent boundary primitives

This slice can run while the shadow/form owners reserve existing native files. It adds only
`Dom/LiveTraversal/DomNodeIdentity.cs`, `BoundaryPoint.cs`, `BoundaryOrder.cs`, `DomStaticRange.cs`,
and tests under `Jint.Tests.HtmlParser/LiveTraversal/`. Everything is internal in `Jint.HtmlParser`.
No Node/Attr/Document/CharacterNodes edits, registrations, public snapshots, or live Range stub.

```csharp
internal readonly struct DomNodeIdentity : IEquatable<DomNodeIdentity>
{
    internal DomNodeIdentity(Node node);
    internal DomNodeIdentity(Attr attribute);
    internal Node? Node { get; }
    internal Attr? Attribute { get; }
    internal bool IsValid { get; }
    public bool Equals(DomNodeIdentity other);
    public override bool Equals(object? other);
    public override int GetHashCode();
}
internal readonly record struct BoundaryPoint(DomNodeIdentity Container, uint Offset);
internal static class BoundaryOrder
{
    internal static uint GetLength(DomNodeIdentity node);
    internal static DomNodeIdentity GetRoot(DomNodeIdentity node, CancellationToken cancellationToken);
    internal static int Compare(BoundaryPoint left, BoundaryPoint right,
        CancellationToken cancellationToken); // -1, 0, +1; different roots => WrongDocumentError
}
internal sealed class DomStaticRange
{
    internal DomStaticRange(BoundaryPoint start, BoundaryPoint end);
    internal BoundaryPoint Start { get; }
    internal BoundaryPoint End { get; }
    internal bool Collapsed { get; }
    internal bool IsValid(CancellationToken cancellationToken);
}
```

The identity is one existing Node **or** Attr, never an attribute owner, adapter DOM node, arbitrary
object, or wrapper. Constructors reject null with ArgumentNullException; default(struct) is invalid.
Operations accepting an invalid identity throw ArgumentException before work. Equality uses underlying
reference identity; default identities compare equal and hash to zero. Valid hash codes use reference
hashing. No implicit conversions are needed. This small union is necessary because native Attr is not
a Node, but existing Browser RangePointTests require its sole boundary point and traversal accepts it
as a root/current node. Do not change Attr inheritance or broaden NodeType for this task.

BoundaryPoint stores values without validating current length/order; that is essential for StaticRange.
Length is UTF-16 code units for Text, CDataSection, Comment and PI, zero for Attr/DocumentType, and
ChildCount for containers. Attr's root is itself even when attached; it has no traversal parent or
children. Other roots follow ordinary ParentNode only. Hosted contents are separate trees.
Compare validates current points (DocumentType => InvalidNodeTypeError; offset > length =>
IndexSizeError), then requires a common root; higher-level methods whose mandated error order differs
perform their own preliminary checks. Compare uses iterative ancestor paths and sibling links; no
recursive ancestor-of-ancestor search, live temporary ranges, mutation or permanent order cache.

DomStaticRange construction only checks valid identities and excludes Attr/DocumentType with
InvalidNodeTypeError. IsValid explicitly checks the current tree/length/order without repairing the
stored points; unrelated roots produce false, not WrongDocumentError. No live registration or mutation
stamp invalidation. Initial D6r1 tests prove genuine independent functionality: cross-branch and
ancestor point order, boundary equality, unsigned extremes, Attr roots, PI/CDATA lengths, stale static
points, detached/template roots, and cancellation during deep ascent/sibling scans. Add native
DomException names via direct construction in these new files; shared exception helpers can follow.
For deterministic polling tests, add internal overloads of GetRoot and Compare with an invocation-local
`Action<int>? workCheckpoint` immediately before their CancellationToken argument; production overloads
pass null. A checkpoint counts actual work and is never retained. Existing Text.Data may materialize
owned parser storage; check around that CLR operation. D6r2 adds a raw internal character-data length
accessor so endpoint bookkeeping never forces such materialization.

## D6r2: authoritative live endpoints and existing mutation lanes

Wait for exclusive ownership of Node/Document/Attr/CharacterNodes and shadow integration. Add internal
`DomRange` and `LiveTraversalTracking`, then wire **every existing** mutation entry before claiming a
usable live range. No public API or Browser switch in this commit.

```csharp
internal sealed partial class DomRange
{
    internal DomRange(Document document); // both endpoints (document, 0)
    internal BoundaryPoint Start { get; }
    internal BoundaryPoint End { get; }
    internal bool Collapsed { get; }
    internal DomNodeIdentity GetCommonAncestor(CancellationToken cancellationToken);
    internal void SetStart(DomNodeIdentity node, uint offset);
    internal void SetEnd(DomNodeIdentity node, uint offset);
    internal void SetStartBefore(DomNodeIdentity node);
    internal void SetStartAfter(DomNodeIdentity node);
    internal void SetEndBefore(DomNodeIdentity node);
    internal void SetEndAfter(DomNodeIdentity node);
    internal void Collapse(bool toStart = false);
    internal void SelectNode(DomNodeIdentity node);
    internal void SelectNodeContents(DomNodeIdentity node);
    internal DomRange CloneRange();
    internal void Detach(); // specified no-op; not unregistration
    internal int CompareBoundaryPoints(ushort how, DomRange source,
        CancellationToken cancellationToken);
    internal int ComparePoint(DomNodeIdentity node, uint offset, CancellationToken cancellationToken);
    internal bool IsPointInRange(DomNodeIdentity node, uint offset, CancellationToken cancellationToken);
    internal bool IntersectsNode(DomNodeIdentity node, CancellationToken cancellationToken);
    internal string GetText(CancellationToken cancellationToken);
}
```

Offsets remain uint through validation; never cast an oversized offset to int first. Start/end setters
validate then apply the spec's opposite-endpoint collapse on reversed order or different roots; they do
not reject a valid different-root point. Attr zero is legal here. Before/after and SelectNode require
an ordinary parent, not OwnerElement or Host. CompareBoundaryPoints validates the ushort selector first
(0/1/2/3 have the DOM meanings), then roots. ComparePoint and IsPointInRange test roots before type and
offset; the former throws WrongDocumentError and the latter returns false on root mismatch. Existing
RangePointTests are literal acceptance cases. GetText includes Text and CDATA, not Comment/PI data;
it is a selected-text operation, not concatenated container TextContent.

Range points are native state. Browser getters, selection and editing use them directly. Every change
rewires native registrations before returning, including automatic opposite-endpoint changes. Direct
endpoint setters do not mutate the DOM or create MutationRecords. CloneRange copies points into a new
independently live object; CloneNode/ImportNode never copy ranges, iterator positions or registrations.

### Registrations and lifetime

Use lazy endpoint buckets on actual Node/Attr identities. A bucket holds weak references to DomRange
plus the endpoint discriminator; the range owns removable handles for its two entries. Mutating one
data node or inserting into one parent consults that bucket directly. Empty buckets are released.
For subtree removal, keep a document-local sparse list of these buckets with **weak node keys**;
consider each live bucket once, using ordinary ancestry to select affected endpoints. This deliberately
costs O(B * H + A) for B registered containers, maximum ancestor depth H, and A affected endpoints,
not O(document node count), and makes no constant-time-removal claim. Do not scan a removed million-node
subtree merely to discover that it contains one range endpoint, or scan each range separately for each
node in that subtree. Parent offset repair uses its direct bucket. Selection objects usually contribute
two entries; no live references means the existing inexpensive empty-state path and no allocations.

The document list must not hold its bucket's node, a range, iterator, wrapper or callback strongly.
Remove entries on explicit point reassignment; prune dead weak targets during registration and mutation,
with amortized sweeping so repeated create/drop cycles cannot grow an unbounded list between mutations.
Do not rely on finalizers or expose disposal as a condition for DOM correctness. Retaining a range
legitimately retains its current endpoint nodes; dropping it permits collection even if its document
remains alive. No static document registry. When adoption changes a registered node's owner document,
move that bucket's document index at the same native ownership boundary. Detached Attr buckets need no
offset mutation hook (their length stays zero), but adoption/ownership indices must stay coherent.

Snapshot affected endpoint handles before editing buckets, then update each relevant endpoint once per
specified adjustment pass. Scratch work must not retain old endpoints after the operation. Include
same-document detach, cross-document moves and already-destination-owned shadow descendants from D6s1;
a document creation identity is not a range's permanent registration owner.

### Mutation ordering

These are semantic hooks inside native algorithms, not MutationObserver consumers. Observer suppression
never suppresses endpoint or iterator repair. Prevalidation failure leaves both tree and live state
unchanged. Run range pre-removal repair and iterator pre-removal repair before unlinking, while old
parents/siblings still exist. Insert, fragment drain, replaceChild, replace-all and same-node moves
retain their specified intermediate removal/insertion behavior; do not derive repairs from a final diff.
For an inserted fragment, adjust destination offsets by its child count at the algorithm's single
insertion point, not repeated contradictory per-child shifts. Remove its children with the required
pre-remove semantics even on suppressed-record paths.

Wire public Data setters as replace-data of the whole old string. Full replacement can move interior
points to zero even when the assigned string is equal. Parser AppendParsedData is insertion at old
length: points exactly at that boundary stay before the appended data. Keep its prepared-storage,
cancellation-atomic commit and amortized growth; do not materialize Data solely to measure length or
update endpoints. Comment, CDATA and PI use the same UTF-16 endpoint rules despite separate CLR types.
Node textContent/nodeValue and later character-data convenience APIs must funnel into this boundary.
Existing parser link helpers and clone insertion lanes also take the applicable fixups; source range
registrations are never copied into new nodes.

Removal moves endpoints inside the removed **ordinary** subtree to the old parent/index and adjusts
later offsets in that parent. It does not move endpoints inside an attached shadow tree or hosted
template contents merely because the host was removed. Those roots remain distinct. Adoption performs
the removal fixups first and rehomes remaining registrations with the actual nodes; do not invalidate
or freeze live objects on a MutationStamp change. An iterator rooted within an adopted subtree follows
that same root identity. No host or composed-tree ascent is used for range comparison or iterator walks.

## D6r3: character-data methods, split and normalize

Separate finite commit after D6r2, with exclusive CharacterNodes/Node ownership. Implement internal
`NativeCharacterData.GetLength(Node)`, `SubstringData(Node,uint,uint)`,
`ReplaceData(Node,uint,uint,string)`, `SplitText(Node,uint) : Text`, and
`Normalize(Node) : void` under LiveTraversal. Invalid receiver kind is ArgumentException; split accepts
Text **and CDATA**, and returns a new Text. Browser methods append/insert/delete data are thin argument
translations to ReplaceData; public Data setters share its bookkeeping. Do not add a new base class.

The dedicated [split algorithm](https://dom.spec.whatwg.org/#concept-text-split) and
[normalize algorithm](https://dom.spec.whatwg.org/#dom-node-normalize) need their additional endpoint
relocations at the stated steps; ordinary ReplaceData/Remove alone do not implement them. Test offsets
equal to and greater than the split, parent offsets immediately after the original node, and detached
split (no transfer into the new detached node). Normalize merges **exclusive Text** only; CDATA blocks
a run. Move endpoints in merged text and at intervening parent boundaries before removal can collapse
them elsewhere. WholeText, when added by B2, includes adjacent Text/CDATA and is a separate read.

Build each normalization run's combined data once, retain cumulative old lengths for point repair, and
remove siblings in order. Avoid repeated growing-prefix strings or repeated sibling-index scans.
Existing observer old values and record ordering must still match replace-data followed by removals.
PI replacement must cooperate with the actual PI pseudo-attribute implementation; flag that dependency
as incomplete until present, rather than copying Browser's repair table into the native range class.

## D6r4: iterator and TreeWalker, with explicit filter execution

Native types own positions and active state. Browser owns WebIDL conversion, the original filter object,
callback this-value/realm, and exception wrapping. Pass an invocation-local filter delegate explicitly
to traversal; never store a Browser closure on a node, root, registry or long-lived native traverser.

```csharp
internal delegate ushort TraversalFilter(DomNodeIdentity node);
internal sealed class DomNodeIterator
{
    internal DomNodeIterator(DomNodeIdentity root, uint whatToShow);
    internal DomNodeIdentity Root { get; }
    internal uint WhatToShow { get; }
    internal DomNodeIdentity Reference { get; }
    internal bool PointerBeforeReference { get; }
    internal DomNodeIdentity? Next(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? Previous(TraversalFilter? filter, CancellationToken cancellationToken);
    internal void Detach(); // no-op
}
internal sealed class DomTreeWalker
{
    internal DomTreeWalker(DomNodeIdentity root, uint whatToShow);
    internal DomNodeIdentity Root { get; }
    internal uint WhatToShow { get; }
    internal DomNodeIdentity Current { get; set; }
    internal DomNodeIdentity? Parent(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? FirstChild(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? LastChild(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? PreviousSibling(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? NextSibling(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? Previous(TraversalFilter? filter, CancellationToken cancellationToken);
    internal DomNodeIdentity? Next(TraversalFilter? filter, CancellationToken cancellationToken);
}
```

Constructors validate identity and initialize current/reference to root, with iterator before=true.
All uint mask bits are retained; nonexistent node kinds simply never occur. Attribute roots are real
single-node iterator collections and valid TreeWalker current values. Attributes of elements are not
children and SHOW_ATTRIBUTE does not invent an attribute axis. WhatToShow suppresses callbacks for
excluded nodes. The active-state check belongs at the specified filtering step before the mask check;
it is not a blanket ban on property reads, setters, or a traversal that finds no candidate. Filter
results are ushort, not a three-value validating enum: unknown values take each DOM algorithm's actual
branches. Iterator reject does not prune descendants; TreeWalker reject/skip are distinct.

The iterator has separate stored reference and nullable in-flight candidate pointers. Pre-removal
adjusts both, even while the filter runs; acceptance copies the **adjusted** candidate but returns the
node actually passed to the filter, which can now be detached. Failed/throwing traversal clears its
own candidate without overwriting the already repaired reference or an outer invocation's active flag. Candidate initialization
and promotion are constant-time assignments, not a synchronization traversal or arbitrary guard limit.
Use the current [adjust-pointer algorithm](https://dom.spec.whatwg.org/#concept-nodeiterator-pre-remove)
and preserve its before/after and removed-ancestor-of-root cases. Test nested filter traversal and
exception cleanup, including filters that mutate before throwing; do not roll back those DOM mutations.

Register iterators weakly by their root identity in a document-local index; adoption of the actual root
rehomes that index. At pre-removal, consider only iterators whose root's current node document matches
the removed node's document, then apply the algorithm's ordinary ancestry tests. Dead iterator slots
are swept under the same bounded-lifetime rules as endpoint buckets. Pre-remove never invokes a filter.
TreeWalker has no such registration: removal must not retarget Current. Its Current setter accepts any
valid identity, including another tree/document. Methods use their own root checks, not a new containment
restriction. Reuse the already-correct Browser algorithms as implementation evidence, with native links
and explicit cancellation; do not port the AngleSharp workaround or weaken the termination tests.

Traversal callbacks execute synchronously only at explicit traversal calls, with coherent native state,
outside atomic mutation bookkeeping. Read links afresh after callbacks; they may mutate/adopt/create
ranges or traversers. No mutation stamp makes a live traversal fail solely because its filter mutated.
Only the filter invocation that sets the active flag clears it in its own finally, following DOM
filtering steps 1 and 5–7. A nested traversal rejected by the active check must leave the outer flag
set. Test an outer filter that catches two successive reentrant traversal attempts: both must throw
InvalidStateError, and traversal must work again after the outer filter returns or throws. Native code
propagates callback errors unchanged and polls before and after callback invocation. Browser retains the filter in its wrapper; native traversal retains it only
for the call. A standalone caller can supply its own delegate under the same reentrancy contract.

## D6r5: content operations, then D6r6 selection notifications and publication

Implement DomRange DeleteContents(), ExtractContents():DocumentFragment, CloneContents():DocumentFragment,
InsertNode(DomNodeIdentity), and SurroundContents(DomNodeIdentity) as a separate commit. Use iterative
frames for partial ancestor chains and shared native clone/mutation algorithms. Fully contained moved
nodes preserve identity; clones do not. Partial template copies have empty ordinary contents while a
fully cloned template applies template cloning steps. Clonable shadow hosts still apply D6s1 cloning
rules; range membership itself never descends into a shadow tree. Preserve namespaces, owner documents,
mutable PI data and attribute identities without Browser repair passes. CharacterData boundary cases
include PI/comment/CDATA; range stringification still excludes PI/comment. Preserve specified validation
and failure order rather than promising transaction rollback for an entire multi-step range operation.
Range insert prevalidates before splitting; surround's partial-non-Text refusal treats CDATA as Text.

D6r6/B3 supplies notifications for Selection without user callbacks from native mutations. Use an
internal disposable `RangeChangeSubscription` obtained from
`DomRange.ObserveChanges(Document selectionDocument)`. It holds native identities only, has
`bool TakePendingChange()` and `Disconnect()`/Dispose(), and is referenced weakly by the range.
On endpoint change, set its pending bit; the selection document's optional trusted
`Action? PendingRangeChanges` sink may only schedule Browser processing, analogous to D5. It must not
invoke JS, read partially updated endpoints, or mutate anything. Notify after the completed coherent
native operation, not between endpoint assignments. Multiple subscriptions are independent; one
selection's detach does not disconnect another. No sink means no task/undrainable record queue.
Browser owns subscriptions, disconnects on selection reassociation/document teardown, and schedules
the standard selectionchange task for its associated document. Callback delivery/event dispatch is
always Browser work. No public callback API or inferred native Selection object is introduced.

At publication, promote only implemented DomNodeIdentity/BoundaryPoint, DomStaticRange, DomRange,
DomNodeIterator/DomTreeWalker and TraversalFilter members needed by unsigned consumers; keep tracking,
selection subscriptions and sinks internal. Add Document.CreateRange() as the factory for its initial
(document,0) pair; traverser constructors explicitly take their real root, not a spurious creating
document. CT parameters on read/traversal methods become optional defaults; synchronous mutators keep
their existing non-cancellable model. Public API snapshots, XML docs, packed-consumer identity/mutation
probes and A1 regeneration are a separate reviewed commit. No public partial Range whose missing methods
return empty/null/false, and no Browser cutover before every row in the consumer table has an owner and
passing integration evidence. Geometry/Selection/contextual-fragment work remains separate explicit debt.

## Cancellation, performance and gates

Read/comparison/traversal loops use an invocation-local work counter: check entry/exit and at most every
256 links, scans, copied characters or frame transitions, including final parent ascent. No static
test hook, stored CT, host retention or CLR recursion on adversarial trees. Browser passes its real
operation token and bounds its own filter callbacks; native polling cannot interrupt arbitrary user
code between calls. GetText uses one builder/materialization rather than prefix concatenation.

Existing synchronous DOM mutations and endpoint changes finish their semantic bookkeeping without
mid-commit cancellation. Optional future cancellable clone-content work may discard unpublished output,
but cannot leak copy-generated slot signals on cancellation. Do not add cancellation to half of
normalize/extract and advertise atomicity. Account for sparse registration scans honestly; work-counter
tests vary live endpoints independently from DOM size. A no-range/no-iterator mutation allocates no
tracking structures. Repeated append with a fixed number of parent endpoints is linear in append count;
wide fragment operations do not repeat growing sibling-index scans. GC tests retain the document while
dropping ranges/iterators, then prove both live objects and formerly referenced detached subtrees die.

| Slice | Gate before proceeding |
| --- | --- |
| D6r1 independent | Exact boundary/static behavior including Attr, uint extremes, deep iterative order and deterministic cancellation; only new files. |
| D6r2 existing lanes | Insert/remove/replace-all/replaceChild/fragment drain/same-node movement, equal Data writes, parser append/insert, suppressed observers, live clone independence and all adoption/template/shadow boundaries; failures leave live state unchanged where prevalidation requires it. |
| D6r3 text | Split at 0/end/interior, detached and CDATA split, text-only normalization, empty text, PI/comment replacement, UTF-16 surrogate boundaries, observer order and allocation/work growth. |
| D6r4 traversal | Every movement/mask/filter branch, out-of-root TreeWalker, actual Attr roots, all four existing removal-during-filtering cases, candidate exception/reentrancy cleanup, adoption, no-op detach, GC and deep ascent cancellation. |
| D6r5 contents | Collapsed/same-data/cross-branch/partial ancestors, doctype errors, clone versus move identities, nested template/shadow cloning, PI/CDATA data, insert splitting and surround validation; iterative deep-tree runs. |
| D6r6/B2/B3 | Selection-through-getRangeAt and automatic-fixup notifications, subscription teardown/multiple associations, generated binding inventory, existing Browser suites and exact WPT range/traversal census; public snapshots and unsigned packed consumer. B2 StaticRange dictionary tests assert endContainer/endOffset/startContainer/startOffset getter and conversion order, including a throwing conversion that prevents later getters. |

Shared native mutations are reserved by the shadow and form owners; only reviewed D6r1 may dispatch
independently. D6r2 onward needs an explicit shared-file handoff and must preserve completed D5/slot/form
bookkeeping. Tests run freshly compiled Release on net8.0 and net10.0 with
`dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release --framework <tfm>`.
Browser cutover also runs the cited suites and relevant pinned WPT, with exhaustive census and exact
remaining debt. Passing the independent primitives is not completion of live traversal or D6.

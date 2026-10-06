# D5 native mutation tracking dispatch

Contract following [native ownership tasks](html-parser-native-followups.md), 2026-09-23. Implement
against merged metadata/D3b/template commits; do not concurrently rewrite their Node/Document files.
The native core owns matching/records; Browser retains microtasks, callbacks and exception reporting.
No callback convenience API, worker, automatic polling or JavaScript execution is added here.

## Exact standalone API

All types are in namespace `Jint.HtmlParser`; new implementation files use `Dom/Mutations/**`.
The D5 owner also edits existing Node/Document/Element/Attr/character-data mutation paths and dedicated
`Jint.Tests.HtmlParser/Mutations/**` tests. The coordinator owns project/signing files and API snapshot
acceptance. Add these real members, with internal constructors except the options' public default one:

```csharp
// Document: convenience factory; target need not be connected or belong to this document.
public MutationSubscription ObserveMutations(Node target, MutationObserverOptions options);

public sealed class MutationObserverOptions
{
    public bool ChildList { get; init; }
    public bool Subtree { get; init; }
    public bool? Attributes { get; init; }
    public bool? CharacterData { get; init; }
    public bool? AttributeOldValue { get; init; }
    public bool? CharacterDataOldValue { get; init; }
    public IReadOnlyList<string>? AttributeFilter { get; init; }
}

public sealed class MutationSubscription : IDisposable
{
    public void Observe(Node target, MutationObserverOptions options);
    public IReadOnlyList<MutationRecord> TakeRecords();
    public IReadOnlyList<MutationRecord> TakeRecordsForDelivery();
    public void Disconnect();
    public void Dispose();
}

public enum MutationRecordKind { ChildList, Attributes, CharacterData }

public sealed class MutationRecord
{
    public MutationRecordKind Kind { get; }
    public Node Target { get; }
    public IReadOnlyList<Node> AddedNodes { get; }
    public IReadOnlyList<Node> RemovedNodes { get; }
    public Node? PreviousSibling { get; }
    public Node? NextSibling { get; }
    public string? AttributeName { get; }
    public string? AttributeNamespace { get; }
    public string? OldValue { get; }
}
```

Options describe observation requests, not parser feature switches. At Observe, snapshot/validate the
flags and copy the filter: later mutation of the caller's collection cannot change a registration.
Null target/options throws ArgumentNullException, null filter entries ArgumentException. A null filter
means no filter; an empty filter matches no attributes. Local-name comparisons are ordinal, without
lowercasing, wildcard or namespace guessing; a supplied filter excludes namespaced attributes.

Nullable booleans distinguish omission from explicit false. Apply DOM observe defaulting: presence of
AttributeOldValue or AttributeFilter implies Attributes when omitted; presence of CharacterDataOldValue
implies CharacterData when omitted. Then reject all-three-disabled, old-value true with its category
false, or a filter with Attributes false, using ArgumentException. Explicit old-value **false** does
not itself conflict with an explicitly false category when another category is enabled. Browser maps
these validation errors to TypeError. Validate before replacing registrations or clearing transients.

One subscription can Observe several targets across documents under the caller's serialized ownership.
Reobserving the same target replaces its options and removes transients derived from that registration;
it preserves already queued records. Two matching registrations on one subscription produce one record
per underlying mutation; OldValue is included if any matching registration requests it. Other
subscriptions receive independent queues and their own old-value projection.

Records are immutable snapshots of lists/values, with stable references to mutable native nodes. Empty
node lists are shared immutable empties; nonempty collections cannot expose writable backing arrays.
AttributeName is the local name and AttributeNamespace its namespace (null for none); both are null
for non-attribute records. Non-child-list records have empty node lists/null siblings. OldValue is
null when unrequested or an attribute was absent. There is no NewValue field or fabricated CSSOM kind.

## Draining, delivery and lifetime

`TakeRecords()` returns and clears only this subscription's queue. It does **not** clear transient
registrations or complete delivery; this is the operation Browser's JavaScript takeRecords uses.
`TakeRecordsForDelivery()` atomically drains that queue and removes this subscription's transient
registrations before returning. Standalone polling callers use it as their explicit delivery boundary,
including when no records remain after an earlier drain. Other subscriptions are unaffected. No user
callback runs in either method. The two methods distinguish
[DOM draining from notification](https://dom.spec.whatwg.org/#notify-mutation-observers).

Disconnect clears normal/transient registrations and queued records; it is reusable by Observe and
must not resurrect prior targets. Dispose performs the same cleanup and permanently closes the
subscription: later Observe/TakeRecords/TakeRecordsForDelivery throw ObjectDisposedException;
Disconnect/Dispose remain idempotent. Returned records remain usable after any cleanup.

Nodes own their lazy registration lists. A subscription tracks registered/transient nodes weakly so
holding it alone does not pin an otherwise unreachable tree. Queued records legitimately retain their
referenced nodes until drained/disconnected; retained returned records belong to the caller. No global
strong dictionary keeps every document, detached node or subscription alive. Registration follows
node identity through adoption, and clone/import copies no registration, queue or host signal.

On subtree removal, establish transient registrations from applicable ancestor registrations before
losing the path. Track further mutations in that removed subtree until delivery/reobserve/disconnect,
including after cross-document adoption. Ordinary traversal does not cross template Host or a future
shadow boundary. Observing a template does not observe its content; observe TemplateContent explicitly.

For B3, retain a narrow internal pending-record signal on each subscription (a nullable trusted host
sink, allocation-free when absent). Signal when matching records are queued; the host deduplicates and
schedules its microtask. Never deliver records/call script from the mutation stack. Browser snapshots
its pending-observer set at checkpoint start, then calls TakeRecordsForDelivery immediately before each
observer's callback. Callback mutations can schedule the next checkpoint; another pending observer
can still see mutations queued before its turn in the current checkpoint. Draining with TakeRecords
must not erase pending transient cleanup. B3 owns the actual host sink wiring/JS adaptation; D5 tests
queueing and explicit delivery using native consumers, without adding a Browser dependency.

## Mutation algorithm coverage

Put production at the semantic operation boundary, not only in Browser wrappers or setters on elements.
Parser/native/host writes share it. Capture old sibling boundaries and old values before changing them;
snapshots describe that operation even if nodes subsequently move. Enforce validation first: failed
operations enqueue nothing and leave ownership, registrations and links unchanged.

Required paths include existing AppendChild/InsertBefore/RemoveChild/ReplaceChild, D3b AdoptNode and
ReplaceChildren, attribute append/change/replace/remove (including SetAttributeNode and attached
Attr.Value), and Data assignments on Text/Comment/CDATA/PI. Detached Attr.Value has no observed Element
target. Equal-value assignments still follow the DOM mutation algorithm; absent-attribute removal and
setting the identical attribute object where DOM specifies an early return remain no-ops. Do not infer
whether to queue solely by comparing final tree/value equality.

Tree cases must use [DOM queueing rules](https://dom.spec.whatwg.org/#queue-a-tree-mutation-record):

| Operation | Required structure |
| --- | --- |
| Insert ordinary node | Its old-parent removal, if attached, then destination addition |
| Insert nonempty fragment | One fragment removal snapshot, then one destination addition snapshot; no per-child duplicates |
| Insert empty fragment | No child-list record |
| ReplaceChild | Normal adoption/source removal where required, then one target replacement record; fragment removal remains observable |
| ReplaceChildren | One target record with original removed children and added children; null siblings; emit only if either list is nonempty |
| AdoptNode | Normal source removal if attached; ownership change alone has no record kind |

Suppress-observers flags suppress only the specified child-list record. They must not suppress transient
registrations, internal invalidation or semantic fixups. For ReplaceChildren whose replacement is inside
a removed subtree, later extracting it can also produce a record on its former parent. Same-node
InsertBefore/ReplaceChild follows the algorithm, even if it produces remove/add records with unchanged
final order. Tests assert source/fragment/destination order, not merely a final record count.

Clone/import can use their unpublished-copy linking lane: source subscriptions see no changes, and
registrations are not copied. That lane must not become a generic record-suppression route for live
parser nodes. Newly constructed data values are initialization, not mutations of a published node.
Attribute replacement queues an attribute record on its element, preserving the replaced value and
actual namespace/local-name key; both Attr identities and their OwnerElement transitions stay correct.

Internal synchronous semantic fixups remain separate from observer records. Reserve before-remove,
after-insert, attribute-change, character-data-change and ownership-change boundaries, carrying the
actual native nodes/old document or value. Only implement consumers already present; do not publish
general hooks or insert no-op promises for ranges/custom elements. D6/B3 add those consumers at these
same boundaries before claiming their feature complete. Observer suppression cannot bypass them.

## Cheap normal path and verification

With no registrations/host, writes allocate no observer queue, record, old-value copy, dictionary or
added/removed snapshot. Keep an inexpensive conservative observer-presence summary so that path can
skip matching; prove adoption cannot leave a false-negative destination summary. Existing immutable
string values can be referenced as old values when requested. Allocate matching maps/record lists only
when necessary, and reuse immutable structural snapshots across observers without sharing mutable queues.
Do not retain a Browser or callback in ordinary standalone nodes/documents.

Maintain an internal document mutation stamp for later collection/cache consumers, including detached
nodes. It changes before a subsequent read after a relevant mutation; source/destination ownership
changes invalidate both documents. Exact increment count is not public. Use a saturating unsigned stamp:
once saturated, cache consumers treat it as always invalid, never equality-as-valid. Constructors and
unpublished clone/import assembly do not invalidate unrelated existing trees. CSSOM uses its own stamp.
There is no public journal, counter option, record cap or silent truncation.

Split implementation into two commits within one D5 task:

1. Internal options normalization/subscription/record machinery, ordinary mutation paths, multiple
   registrations, draining, disconnect/dispose and no-observer behavior. Keep the subscription facade
   internal until the next commit completes transient/aggregate behavior; do not publish partial methods.
2. Complete fragment/replace-all/adoption ordering, transient lifetime, weak retention and template
   boundaries; native tests and API snapshots. D5 is complete only with all these cases, regardless of
   how the owner groups the internal commits. B3 microtask/JS integration remains its separately owned task.

Acceptance includes nullable-option cases, filter copying/case/namespaces, overlap dedup/old values,
all attribute write paths, same-value writes, Data types, fragment tables, validation rollback,
reobservation, draining versus delivery, records after removal/adoption before delivery, independent
subscriptions, weak lifetime after queue release, clone/import silence, and mutation-stamp saturation.
Use controlled native sequences, not sleep/timing assertions. Native observer tests do not certify
Browser callback scheduling; B3 must preserve the existing observer/Promise ordering fixtures.

Run `dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net8.0`
and the same for net10.0, rebuilding both. No benchmark speed claims during parallel builds.

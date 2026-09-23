# D6 shadow trees and slots

Preparatory design for independent review, 2026-09-23; not an implementation dispatch or completion
claim. This splits the shadow-tree part of [D6](html-parser.md#11-phased-implementation-and-bounded-tasks)
into finite native tasks. It follows [native ownership](html-parser-native-followups.md),
[construction](html-parser-construction.md), [D5 mutation tracking](html-parser-mutations.md), and
[H6d/H6f](html-parser-tree-construction-followups.md#7-h6dh6f-templates-inert-ownership-and-applicable-newer-branches).
D6 range/iterator fixups remain a separate required task. H6f also owes content patching; shadow support
does not discharge that obligation. No parser feature switch, alternative DOM, or Browser dependency
is introduced into Jint.HtmlParser.

## 1. Evidence and boundaries

Normative references are the current [DOM shadow trees](https://dom.spec.whatwg.org/#shadow-trees),
[ShadowRoot](https://dom.spec.whatwg.org/#interface-shadowroot),
[attachment](https://dom.spec.whatwg.org/#dom-element-attachshadow),
[cloning](https://dom.spec.whatwg.org/#concept-node-clone),
[adoption](https://dom.spec.whatwg.org/#concept-node-adopt),
[HTML slot](https://html.spec.whatwg.org/multipage/scripting.html#the-slot-element), and
[HTML template parsing](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inhead).
These were checked against DOM dated 2026-08-25 and the current HTML Living Standard. Current HTML
includes `shadowrootslotassignment`, `shadowrootcustomelementregistry`, and template `for` patching.
Implement those algorithms rather than assuming older declarative-shadow examples exhaust the syntax.

The [A1 lock](../../tools/html-parser-inventory/inventory.lock.json) is the binding inventory. These
concrete consumers determine native prerequisites; its broad B1/B2 ownership labels do not mean all
of their semantics belong in the binding generator:

| Existing surface or consumer | Native requirement / later owner |
| --- | --- |
| Generated Element `attachShadow`, `shadowRoot`, Element/Text `assignedSlot`; ShadowRoot `host`, `mode`, inherited fragment operations | D6 identity, guarded attachment, open-only exposure, internal closed-root access, native tree algorithms; B1 conversion/shape/prototype/brand |
| Generated ShadowRoot `activeElement`, `innerHTML`, `styleSheets` | B3 focus retargeting; H7/H8 fragment parsing plus X3 serialization; C4/C6/B5 stylesheet ownership. D6 supplies the actual root, not fixed null/empty implementations |
| Generated HTMLSlotElement `name`, `assignedNodes`, `assignedElements`, `getDistributedNodes` | D6 assignment and flattening; B2 reflection/options/WebIDL arrays. Preserve `getDistributedNodes()` as a Browser compatibility alias for unflattened assigned nodes, explicitly documented/tested; no legacy distribution engine |
| `Dom/Views/DomViewMembers.cs` | Current helpers call GetDistributedNodes and ignore flatten options. Replacement must implement standard `flatten` and `assign`, not perpetuate this limitation |
| `DomNodeObject.cs`, `DomNodeMembers.cs`, `DomParentNodeMembers.cs`, `DomSelectors.cs` | Tree root versus shadow-including root, host-inclusive cycles, selector scope, event traversal. Current event AssignedSlot is deliberately absent and its non-composed shadow-parent check is too broad for slotted light-tree events; B3 must correct both |
| `HtmlDirectionality.cs`, `HtmlFormOwner.cs`, `AriaElementReferences.cs`, `Events/ActivationBehaviors.cs` | D7/B2/B3 consume explicit appropriate tree relations; do not replace every ParentNode walk with a flattened-tree walk |
| `CustomElements/CustomElementRegistry.Reactions.cs`, `DomRealm.cs` | Disable-shadow checks, shadow-including reaction/realm traversal, cross-document registry and wrapper identity; B3 owns registry definitions and reactions |
| `DevTools/DomDomain.Events.cs` | Closed/open native root discovery for the inspector; visibility restrictions belong to each API, not loss of the native link |
| `Views/ShadowDomTests.cs`, `DomEventTests.cs`, `DomCollectionTests.cs`, `EmptyNamespaceSelectorTests.cs`, `Forms/DirectionNameTests.cs`, `Events/ActivationBehaviorTests.cs`, `Dom/HtmlSerializationTests.cs`, `Parsing/ChildFrameTests.cs` | Existing identity/query/slot/event/activation/serialization/frame regressions remain gates in `Jint.Tests.Browser`; native tests do not replace them |

Ordinary DOM children, shadow-including traversal, host-including cycle detection, and flattened
slottables are distinct operations. D4 ordinary queries/collections and mutation observer ancestry
do not enter a host's shadow tree or a template's contents. A query rooted at a ShadowRoot traverses
its ordinary children. Selectors' host/slotted semantics remain owned by C2/C3, using D6 facts.

## 2. D6s1: native root, attachment and ownership

One native owner reserves `Dom/Shadow/**`, `Element.cs`, `CharacterNodes.cs`, `Document.cs`, `Node.cs`,
`NodeCloner.cs`, and dedicated `Jint.Tests.HtmlParser/Shadow/**` tests. Coordinate these shared files
with D6 range work; do not concurrently edit them. The integration owner handles API snapshots.
Everything below is internal unless already public. The first task adds real internal functionality,
with tests as its consumer; it does not publish an incomplete attachShadow facade.

Unseal the existing public DocumentFragment solely to allow the internal sealed ShadowRoot subtype;
retain its internal constructor, so this does not introduce an external subclassing constructor.
Element remains sealed. Put the new types in namespace `Jint.HtmlParser`, one coherent family under
`Dom/Shadow/`. Exact signatures (bodies omitted here solely to specify the design):

```csharp
internal enum ShadowRootMode { Open, Closed }
internal enum SlotAssignmentMode { Named, Manual }
internal readonly record struct ShadowRootInit(ShadowRootMode Mode,
    bool DelegatesFocus = false, bool Serializable = false,
    SlotAssignmentMode SlotAssignment = SlotAssignmentMode.Named, bool Clonable = false);

// Identity and native facts only: never an Engine, JsValue, definition, delegate or realm.
internal sealed class CustomElementRegistryIdentity
{
    internal CustomElementRegistryIdentity(bool isScoped);
    internal bool IsScoped { get; }
}
internal readonly record struct ShadowAttachmentContext(
    CustomElementRegistryIdentity? Registry, bool DisableShadow,
    bool HostIsCustomOrPrecustomized);

internal sealed class ShadowRoot : DocumentFragment
{
    internal new Element Host { get; }
    internal ShadowRootMode Mode { get; }
    internal bool DelegatesFocus { get; }
    internal bool Serializable { get; }
    internal SlotAssignmentMode SlotAssignment { get; }
    internal bool Clonable { get; }
    internal bool Declarative { get; }
    internal bool AvailableToElementInternals { get; }
    internal CustomElementRegistryIdentity? CustomElementRegistry { get; }
    internal bool KeepCustomElementRegistryNull { get; }
}

// On Element; setters are private to the native algorithms.
internal ShadowRoot? AttachedShadowRoot { get; } // both modes
internal ShadowRoot? OpenShadowRoot { get; }     // open mode only
// On Document and Element: native identity populated by the registry-owning algorithms.
internal CustomElementRegistryIdentity? CustomElementRegistry { get; }
internal void SetCustomElementRegistry(CustomElementRegistryIdentity? registry);

internal static class ShadowTree
{
    internal static ShadowRoot Attach(Element host, ShadowRootInit init,
        ShadowAttachmentContext context);
    internal static Node GetRoot(Node node, bool composed, CancellationToken cancellationToken);
    internal static bool IsConnected(Node node, CancellationToken cancellationToken);
    internal static void SetDeclarativeTemplateContent(Element template, ShadowRoot root,
        bool keepCustomElementRegistryNull);
}
```

Constructors/setters for ShadowRoot are confined to ShadowTree/clone/adopt helpers; no caller can swap
its host. The base fragment Host and ShadowRoot.Host reference the same element. ParentNode and sibling
links remain null, NodeType is DocumentFragment, and the host's ChildCount excludes the root. Creation
uses the host's node document, never its template contents-owner document. A detached host still has
its root. Mode is not inferred from attributes after attachment. OpenShadowRoot filters closed roots;
native ownership, inspector access, algorithms and returned attach results retain the closed identity.

`Attach` takes **resolved algorithm inputs**, not options to bypass validation. It validates enum
values before changes (ArgumentOutOfRangeException); null host is ArgumentNullException. Its native
checks implement the DOM attachment algorithm and throw the existing DomException/NotSupportedError:
HTML namespace, valid shadow host name, resolved disable-shadow, and the existing-root rules. The host
name check uses the actual HTML valid-custom-element-name production plus the DOM fixed built-in list;
neither a hyphen test nor CreateElement's more permissive name validation suffices. Test uppercase,
reserved custom names and supplementary characters. Resolve DisableShadow from the **host's** custom
definition; do not look it up in the newly requested registry. Standalone callers have no definitions
and supply false; B3 resolves the existing registry before entering this non-reentrant operation.
Use the current [valid custom element name](https://html.spec.whatwg.org/multipage/custom-elements.html#valid-custom-element-name)
definition, which references DOM's current valid-element-local-name rules, rather than copying the
older PCENChar production into this new validator.

The native identity type is allocated only for a real registry association. Standalone documents,
elements and roots initially have null registry; a null identity is not an implicit global registry.
B3 owns the identity-to-Browser-registry mapping outside native nodes and sets document/element
associations through internal native setters with their working registry integration. Browser's
attachShadow dictionary defaulting and the prohibition on supplying another document's non-scoped
global registry happen before `Attach`; its resolved Registry is otherwise allowed to differ from the
host's registry. Native tests use identities to exercise propagation without constructing a Browser.
SetCustomElementRegistry stores only that identity, accepts null, and invalidates the owning document
when the association changes; it neither performs registration nor invokes custom constructors or
reactions. Unpublished metadata initialization avoids unrelated document-stamp/observer work; this
does not suppress the copied tree's assignment or slot signals described below. B3 owns
when the registry algorithms invoke the setter, not a second mutable association in a wrapper.

If an existing root is declarative and its mode matches, remove its children individually through the
normal removal algorithm, clear Declarative, and return **that same root**. Preserve its other flags
and registry; supplied new flags do not reinitialize it. Any other existing-root case rejects before
changes. New roots initialize AvailableToElementInternals from the resolved host state and Declarative
and KeepCustomElementRegistryNull to false. No observer record invents a shadow root as a child of its
host; root attachment nevertheless invalidates native document state used by shadow-aware consumers.

`GetRoot(false)` follows ParentNode only. `GetRoot(true)` additionally crosses ShadowRoot.Host, not an
ordinary template fragment's Host; `IsConnected` asks whether the latter root is a Document. Cycle
validation continues to cross **both** kinds of hosted fragment. Reparenting a host into its own root,
through nested roots, or through a template/shadow combination fails before any mutation.

Clone/import/adoption belong in D6s1, not deferred wrappers. In particular:

* A direct ShadowRoot.CloneNode or Document.ImportNode(root) throws NotSupportedError; direct
  Document.AdoptNode(root) throws HierarchyRequestError. Test exact errors before changes.
* Cloning a host with a clonable root copies that root and its **whole** subtree even for a shallow
  host clone; shallow applies to the host's light children. A non-clonable root is omitted. Preserve
  mode/delegatesFocus/serializable/slotAssignment/clonable/declarative/keep-null as specified, and apply
  attachment's initialization of AvailableToElementInternals rather than blindly copying it.
* Clone/import creates fresh identities for nodes/attributes, preserves nested template owners, and
  copies no observer registrations, queued signals, manual assignments, or source-node assignment
  caches. New-copy insertions nevertheless perform their own assignment and signal steps. Once
  D6s2/D6s3 land, AppendClonedChild cannot be a link-only bypass: retain its trusted validity fast path,
  but execute equivalent insertion semantics on copied nodes in the specified clone order. A copied
  slot receiving cloned light children, or fallback children while its assigned list is empty, can
  generate **new** slot signals. Those signals name copied slots; they are not copies of source
  queued signals. Source registrations, source observer records and existing source-slot signals are
  unchanged. Quiet source handling and avoiding unrelated stamps do not imply a signal-free copy.
  Explicit stack frames must process a host's root even when the light-tree `deep` argument is false:
  the existing NodeCloner early return cannot remain ahead of this work.
* Both shallow and deep **Document** clones preserve a scoped document registry by the same identity,
  before cloning children. Null/global source registries do not take that scoped-copy branch: use
  ordinary new-document initialization (null for standalone documents; the creation realm's applicable
  default global association for Browser-created documents). Do not allocate a fresh scoped registry,
  blindly copy every source registry, or forcibly clear a Browser creation default. Document cloning
  and importing nodes into an existing destination remain different operations. Tests cover scoped
  identity retention with both depths, standalone null/global-source initialization, and the separate
  Browser creation-default path; deep clones also assert element/root registry propagation against
  the new document's resulting association.
* Changed-document adoption walks shadow-including descendants, including roots and attributes,
  while template content still uses its separate inert-owner adoption boundary. Preserve root and
  host identities, and node subscriptions; invalidate both old/new documents. Same-document adoption
  still performs the specified removal, but does not rerun changed-document ownership hooks.
* A global source registry becomes the destination document registry's effective global identity
  (null for a scoped/null destination). Scoped identities remain scoped. Adoption also fills a null
  shadow registry when KeepCustomElementRegistryNull is false; explicit keep-null survives. Clone
  preserves a null root registry and keep-null according to the cloning algorithm, without passing
  the light-tree fallback registry into shadow-child clone calls. Element association propagation
  follows the same DOM clone/adopt algorithms; no Browser object is copied into native storage.
* A shadow root is still a fragment argument for insertion/replacement algorithms: do not turn the
  direct adoptNode restriction into a blanket ban on draining its children. Preserve D3's distinctions
  between append/replace-all and ReplaceChild's internal fragment adoption, including empty roots.
  Host links do not change. In particular the internal ReplaceChild adoption can change a fragment
  root's node document; do not impose an extra perpetual host/root OwnerDocument-equality guard.

## 3. D6s2: slot algorithms and synchronous mutation integration

After D6s1, the same native owner implements `Dom/Shadow/SlotAssignment.cs` and hooks the existing D5
semantic mutation boundaries. HTML-namespace, exact lowercase `slot` elements have slot state; keep
Element sealed. Slottables are Elements, Text and CDataSection: CDATA implements Text in DOM even
though this native model represents it as a separate sealed Node type. Do not use only `node is Text`
as the slottable test. Comments and PI are not slottables. Slot names and `slot` attribute values compare ordinally, with
missing values normalized to empty. Namespaced lookalike attributes do not control distribution.

```csharp
internal static class SlotAssignment
{
    internal static Element? FindSlot(Node slottable, bool openOnly,
        CancellationToken cancellationToken);
    internal static Element? GetAssignedSlot(Node slottable); // stored, unfiltered semantic state
    internal static IReadOnlyList<Node> AssignedNodes(Element slot, bool flatten,
        CancellationToken cancellationToken);
    internal static IReadOnlyList<Element> AssignedElements(Element slot, bool flatten,
        CancellationToken cancellationToken);
    internal static void Assign(Element slot, ReadOnlySpan<Node> nodes);
}
```

Null receivers throw ArgumentNullException; wrong slot receiver and non-slottable/null Assign entries
throw ArgumentException before changes. Browser performs WebIDL brand/union conversion first.
FindSlot on a non-slottable returns null. Returned collections are immutable snapshots of stable node
references, not backing lists or live views; subsequent assignment/mutation cannot rewrite a result.
An ordinary light-tree slot has no assigned nodes; flatten does not make its fallback children into
an assigned result. Element/Text IDL assignedSlot computes FindSlot with openOnly=true. Native event
and mutation algorithms use GetAssignedSlot, which returns the node's **stored assigned slot**, with
no root-mode filter and no fresh lookup. Null input throws ArgumentNullException; a non-slottable
returns null. These are distinct semantic facts, not two cached variants of the same query.
AssignedNodes/AssignedElements with flatten=false read the slot's **stored assigned nodes** (filtering
elements for the latter), whereas flatten=true runs find-flattened-slottables using freshly found
slottables. Do not rebuild stored assignment as a side effect of a read or use FindSlot(false) as the
event seam. See [Node get-parent](https://dom.spec.whatwg.org/#interface-node),
[IDL assignedSlot](https://dom.spec.whatwg.org/#dom-slottable-assignedslot), and
[HTML assign](https://html.spec.whatwg.org/multipage/scripting.html#dom-slot-assign).

Named mode chooses the first matching slot in the root's ordinary tree order and the host's immediate
slottable children in child order. Nested shadow roots and template contents do not enter that search.
Manual mode uses each slot's ordered manual set, filtered to nodes whose parent is this root's host.
Assign deduplicates input by identity in first-occurrence order, clears old manual links, removes a
node from its previous slot's manual set, and recomputes assignments at the HTML-specified tree step.
Calling Assign outside a manual root is allowed: manual intent is stored even when it does not control
current distribution. Preserve manual intent across detach/reattach and adoption; do not confuse it
with stored assigned nodes. Reassign only the trees specified by the algorithm, not every root whose
manual set was edited. Weak references are permitted for the two manual-intent associations;
do not use a global strong node registry. Mutation can change effective assignment without Assign.

Required cross-root fixture: create manual roots R1/R2 and their slots s1/s2; n is a light child of
R1's host. After `s1.assign(n)` then `s2.assign(n)`, n's manual assignment points to s2 and n is absent
from s1's manual set. Only R2's tree is reassigned by the second call. Therefore FindSlot(n, false)
is null, while GetAssignedSlot(n) still returns s1 and unflattened s1.assignedNodes still contains n.
Flattened s1 lookup instead finds no slottables and takes its own fallback. Assert that native event
parent selection still uses s1, and that merely reading either query does not reconcile R1. A later
operation that actually runs R1 assignment updates its stored lists according to that algorithm.
This deliberate distinction must survive indexes, invalidation and Browser bindings.

Flattening uses explicit frames for nested slots in shadow trees, includes slottable fallback children
only when no nodes are assigned, preserves order, and filters elements **after** flattening for
AssignedElements. Do not globally deduplicate flattened output or substitute a generic composed-tree
walk. Materialize results with bounded cancellation checks; no CLR recursion for nested fallback.

Integrate insertion/removal/replace-all/fragment draining, same-node moves, adoption, slot `name`,
slottable `slot`, SetAttributeNode replacement/removal and attached Attr.Value. Observer-suppressed
operations still assign/signal. Evaluate the specified intermediate removal/insertion points: replacing
or moving out and back can signal even if the final assigned sequence equals the initial sequence.
Signal on ordered identity-list changes, and on the specified fallback child-list changes for empty
slots. A text Data edit alone is not an assigned-list change or a fabricated slotchange. Each stored
state and computed answer follows its own algorithm immediately, before any Browser microtask runs;
"synchronous integration" is not permission to globally reconcile states that the spec keeps distinct.

Use root-local indexes: first-slot-by-name and ordered assignments/manual membership, plus lazy state
only on relevant elements/roots. A full rebuild may scan an affected tree once after a structural
change, but never scan every host child separately for every slot. The common repeated append of
same-name light children must not repeatedly copy the growing assigned prefix; keep owned mutable
internal lists/links and create snapshots only on reads. An unrelated attribute/text mutation must
not scan a document for slots. Tests count traversal/assignment work for doubled append workloads,
duplicate-name slot insertion/removal, and bulk fragment insertion. Do not claim every arbitrary
insertion is O(1), or hide quadratic work behind a cached final query.

## 4. Declarative metadata and D6s3 Browser signal boundary

D6s1 implements the contents setter below, which gives its declarative clone/adopt tests a real
creation path. D6s3 adds the signal sink after assignment and ownership are real:

```csharp
// ShadowTree: only for a fresh parser-created, stack-only template and its attached target.
internal static void SetDeclarativeTemplateContent(Element template, ShadowRoot root,
    bool keepCustomElementRegistryNull);
// Document: trusted B3 notification sink, analogous to D5 PendingRecord.
internal Action<Element>? SlotChangeSignal { get; set; }
```

SetDeclarativeTemplateContent requires an HTML template with no ordinary children, no parent, and
its original empty inert contents; the parser has not exposed that template. Invalid trusted-seam
use throws InvalidOperationException before changes. It sets the template's contents reference to
the real root, Declarative and AvailableToElementInternals to true, and the specified keep-null state.
It does not change the root's host to the template or its owner to the inert document. This is the
narrow amendment to ordinary templates' stable-content contract: ordinary creation/XML/H6d templates
still keep their original hosted inert fragment; the parser-only declarative template has the actual
ShadowRoot as TemplateContent. No second fake fragment receives children. The old empty fragment can
be released. No general public setter, root swapping, or post-publication retargeting is introduced.

H6f owns token eligibility, enum/default handling, the parser's internally selected permission state,
stack-only template creation, and fallback insertion. It calls Attach only on the branch selected by
the current algorithm, with manual/named slot assignment and all other declaration flags. Existing
host roots take the parser's ordinary-template fallback instead of imperative declarative-root reuse.
Invalid attachment similarly falls back; catch only the expected native attachment DOM error, never
cancellation, quota, allocation, or arbitrary implementation exceptions. The topmost adjusted-current-
node and disallowed-context cases remain ordinary templates as HTML requires. With no registry host,
both an absent registry and explicit keep-null initially hold null, but the keep-null bit must survive
so subsequent adoption can distinguish them. XML parsing never activates declarative HTML templates.
H7/Browser fragment entry points carry the actual destination ShadowRoot and the algorithm-selected
context element; they must not substitute an inert template owner or lose root registry metadata.
X3 owns shadow-aware serialization: ordinary innerHTML/outerHTML omit attached shadow trees;
standards-defined getHTML inclusion uses Serializable and explicit selected root identities, including
the applicable closed-root rules. D6's Serializable flag alone is not a completed serializer.

SlotChangeSignal receives the actual slot at each required native signal point, including detached
slots that just lost assignments. It is absent by default, so standalone operations allocate no
undrainable event queue and retain no Browser. An installed sink may only add the slot to the Browser
agent's ordered signal set and schedule its mutation-observer microtask; no script, mutation, event
dispatch or user callback is allowed from it. B3 owns installation/removal and clears it on document
teardown, as with D5 sinks. Neither native static state nor parser sessions retain it.

B3 must use one agent-level signal set across that agent's documents, snapshot and clear it at the
specified checkpoint point, deliver pending mutation observers first, then fire bubbling, non-composed
slotchange at the snapshotted slots. Signals produced during delivery belong to the next checkpoint.
Cross-document adoption does not move or lose an already queued agent signal. D6 tests use a trusted
collector to assert signal order/identity; B3 tests prove actual microtask coalescing and delivery.

The existing Jint event dispatcher remains the only event system. B3 reads stored slot assignment
through GetAssignedSlot (not computed FindSlot),
ordinary root/parent, ShadowRoot.Host and Mode from native nodes, and implements DOM's event-dependent
shadow parent rule using the first path item's invocation-target root. A non-composed event on a
slotted light node must not be stopped merely because its path visits a shadow root. Keep closed-tree
path flags, retargeting, relatedTarget filtering and composedPath visibility in that dispatcher.
Native nodes never hold Event, JsValue, Engine, PageRuntime, listeners or focus state. DelegatesFocus
is native metadata; actual focus delegation, activeElement retargeting, custom reactions and element
internals registry/availability consumers belong to B3/D7, with working integration tests.

H6f content patching remains a separate native/parser prerequisite. It must operate on actual fragment
or shadow targets with insertion/start/end markers, cleanup and moved/missing marker semantics; it
cannot be implemented by this contents setter or by string replacement. H6f is not complete until both
branches in the tree-construction design pass, and the Templates stop cannot be removed earlier.

## 5. Cancellation, limits and acceptance

Existing public DOM mutators are synchronous and do not take CancellationToken. Attach and Assign
follow that contract; do not introduce a public cancellation/configuration overload. Validate input
before mutation, run each native operation to coherent completion, and let parser callers check at
their established semantic boundaries. Do not throw cancellation between link changes and required
slot/observer bookkeeping. Parser loops still poll between completed native operations; indexes above
prevent routine per-token assignment from becoming an unbounded whole-document scan.

New root/assignment queries take explicit CancellationToken, check entry and before returning, and
poll at most every 256 work steps, counting parent-link ascent, slot candidates, list copies and frame
transitions. Existing parser cancellation/ParseLimits remain authoritative: parser-created root nodes
and materialized contents consume the same node/work budget; do not reset a budget at a shadow or
template boundary or invent a shadow-depth quota. H6f adds root creation to its accounting before
publication. The native owner may add a private/internal cancellation-aware clone traversal for parser
callers; unpublished clone cancellation discards the whole pending result and never mutates source.
Such a cancellation-aware lane also stages copy-generated slot signals until successful completion,
then publishes them in algorithm order; cancellation must not leak otherwise discarded copied slots
through the host signal queue. This staging does not excuse omitting signals on successful clones.
Native public clone/adopt behavior is not changed into a partially cancellable operation.

Required staged gates:

| Commit | Acceptance before the next dispatch |
| --- | --- |
| D6s1 roots/ownership | All exact attachment/error/identity cases above; ordinary/template/shadow host cycles; closed root access; true/false composed roots; detached connectivity; shallow/deep host clone, import, nested roots/templates, same/changed-document adoption and empty/nonempty fragment-operation distinctions; registry null/global/scoped/keep-null matrix; real declarative-template setter tests. No slot/public/browser completion claim |
| D6s2 slots/mutations | Named/manual/default/duplicate-name behavior; rename/reorder/remove/replace/adopt; manual duplicates, cross-root computed-versus-stored fixture and detached intent; fallback and multi-level flattening; returned snapshot immutability; mixed Text/Element/PI; namespaced attributes; suppressed-record paths; intermediate-change signaling; linear append work and unchanged no-shadow hot path |
| D6s3 signal seam | Ordered native signal capture, fallback/intermediate-mutation signal cases and no sink allocation/retention; assignment through real declarative roots; cloned clonable hosts with assigned light children and copied fallback slots signal copied identities in insertion order, with source records/registrations/signals untouched; H6f owner can now integrate native prerequisites, subject to its separate patch task |
| H6f + B1/B2/B3 + X3/C2/C4/C6 | Parser eligibility/fallback and patch corpus; all A1 bindings/consumers ported; slot.assign/flatten/closed assignedSlot options and legacy alias; events/microtasks/registry/focus; actual root serialization rules and stylesheet ownership; scoped selector/flat-tree consumers. No blanket null/empty/false implementations accepted |

Add deep-chain and deep nested-fallback tests with deterministic cancellation checkpoints (including
final ascent with no new nodes), allocation/identity tests on retained snapshots, and weak-lifetime
tests after manual intent/queues/host sinks are released. Test saturation of any cache stamp: saturated
means always invalid, never equality-as-valid. No global cache or enumeration may pin an unrelated
document. Record observer ancestry in a host versus observing its root explicitly, source clone silence,
and correct source/destination invalidation. Public surface promotion waits for implemented behavior
and reviewed snapshots; changing DocumentFragment's sealed modifier itself requires snapshot review.

Run the focused and full HtmlParser Release suites on net8.0 and net10.0 with `dotnet test --project
Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release --framework <tfm>`. Browser cutover also
runs the named existing Browser tests and relevant pinned shadow/slot/custom-element WPT with an
exhaustive census and exact failures; omissions are debt, not successful empty subsets. D6 is not marked
complete until the separately dispatched Range/NodeIterator fixups pass their own mutation tests.

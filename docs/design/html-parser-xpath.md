# X4: native XPath navigator and Browser handoff

Independently reviewed design, 2026-09-23. Supplements [the architecture](html-parser.md), task X4
(X1 and D4 dependencies), and the B4/R4 migration assignments in
[A1](../../tools/html-parser-inventory/README.md). No runtime implementation or performance claim.
Integration inspected at `7792a9152`; XML, ordinary traversal, template ownership and mutation stamps
already exist. Keep the BCL XPath 1.0 engine and replace only its tree adapter. Do not serialize/reparse
into XmlDocument, copy the tree, or introduce an XPath interpreter.

## Evidence and exact boundary

`Jint.Browser/Dom/Views/JsXPath.cs` compiles BCL XPathExpression, supplies an IXmlNamespaceResolver,
clones the expression for evaluation, then materializes an XPathNodeIterator. Its node extraction
currently depends on HtmlDocumentNavigator.CurrentNode and List<INode>. Its scalar coercions include
reading the first selected node's TextContent. `DevTools/DomDomain.Events.cs:XPathMatches` uses the
same navigator, materializes nodes, and treats XPath/argument errors as an unsuccessful search arm.
Both explicitly request `ignoreNamespaces: true`. Browser also deliberately preserves materialized
iterator results across mutation. Preserve those two documented divergences during migration;
independent changes need independent Browser tests. Neither becomes the native default.

Native Attr is a stable object separate from Node. Namespace-axis positions have no native node at
all. Node-only conversion would therefore silently lose valid results. Template contents have a
separate fragment identity/owner; traversal follows ordinary child links, never template/shadow hosts.

Primary authorities checked for this design:

- [XPath 1.0](https://www.w3.org/TR/1999/REC-xpath-19991116/#data-model) defines the logical tree,
  expanded names and scalar semantics. Scope is 1.0, matching the retained engine.
- [DOM XPath mapping](https://www.w3.org/TR/DOM-Level-3-XPath/xpath.html) specifies native text-run
  representatives and namespace positions; this supplies mapping details, not a claim of full DOM
  XPath conformance for the existing Browser divergences.
- [XPathNavigator implementation contract](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.xpathnavigator?view=net-10.0),
  [UnderlyingObject identity](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.xpathnavigator.underlyingobject?view=net-10.0),
  and [MoveToId](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.xpathnavigator.movetoid?view=net-10.0).

The dotnet-inspect skill was attempted; its dnx restore failed on NuGet DNS. Microsoft documentation
and installed .NET 8.0.18/10.0.10 reference XML establish the BCL surface instead. Implementation must
compile against both project TFMs, net8.0/net10.0, without new packages or reflection/dynamic code.

## Surface: internal milestone, then narrow promotion

Use namespace `Jint.HtmlParser`. The first dispatch adds these internal entry points and types:

```csharp
internal static class NativeXPath
{
    internal static XPathNavigator CreateNavigator(Node context, CancellationToken cancellationToken);
    internal static XPathNavigator CreateNavigator(Attr context, CancellationToken cancellationToken);
}

internal sealed class NativeXPathNavigator : XPathNavigator { /* read-only cursor */ }

internal sealed class XPathNamespaceBinding
{
    internal Element OwnerElement { get; }
    internal string Prefix { get; }       // empty means default binding
    internal string NamespaceUri { get; } // binding value, not navigator.NamespaceURI
}
```

NativeXPathNavigator construction is private/internal; no public constructor or feature-options bag.
The two factories allocate one read session, position a cursor, and return it. Clone copies only cursor
state and shares that session. NamespaceBinding instances are immutable session-owned descriptors.
There is no generic native result registry or callback interface.

At X4 completion, promote only NativeXPath plus its two factories, and XPathNamespaceBinding plus its
three getters. Public factories receive `CancellationToken cancellationToken = default`. Add a third
factory `CreateNavigator(XPathNamespaceBinding context, CancellationToken cancellationToken = default)`
at promotion: it creates a fresh session and locates that owner/prefix only if the captured URI still
matches; otherwise throw InvalidOperationException. Do not expose the navigator implementation,
read-session caches, or Browser namespace-erasure mode. This public proposal is gated by the completion
prerequisites below, public snapshots, XML documentation and an unsigned packed consumer.

`UnderlyingObject` returns the actual Node, actual Attr, or shared XPathNamespaceBinding for its
position. Repeated visits and clones in the same session return the same object. It never returns
the attribute owner in place of an attribute, nor an xmlns Attr for a namespace position. Across fresh
sessions namespace descriptors need not be reference-identical; logical equality is owner identity,
prefix and bound URI. Ordinary nodes/attributes retain their global native identities across sessions.

No convenience Evaluate/Select result hierarchy is required: consumers retain BCL expression/result
types. X4 tests perform the exact node-set conversion used by future Browser callers through
UnderlyingObject, including all three alternatives, without requiring Jint.Browser.

## Cursor and logical-tree contract

Implement the required abstract members listed by Microsoft and explicitly implement Value, XmlLang,
UnderlyingObject, HasAttributes, HasChildren, MoveToRoot, ComparePosition and CanEdit=false. Audit
inherited optimized movement helpers against the logical model; override where their defaults would
lose cancellation, text coalescing, or namespace identity. Failure of a MoveTo* returning false leaves
the original position unchanged. Clone/movement never mutates DOM state or emits mutation records.

| Native position | XPath kind and identity |
| --- | --- |
| Document | Root; same Document |
| Element | Element; exact namespace/local name/prefix |
| attached ordinary Attr | Attribute; same Attr, XPath parent is OwnerElement |
| contiguous Text/CDATA run | Text; first nonempty member is the representative |
| Comment / ProcessingInstruction | corresponding kind; same native object |
| in-scope namespace | Namespace; descriptor owned by the queried element |
| DocumentType | absent from the XPath tree |

Skip empty text runs; coalesce across empty Text/CDATA siblings, stopping at another visible node.
If unsupported invisible nodes intervene (e.g. a manually constructed doctype), determine adjacency
in the projected child sequence, not by making an extra XPath text position. Value is the complete
run, and entering through any nonempty member selects its representative. Reject an empty text/CDATA
context with ArgumentException. Expose all nonempty text as Text, including whitespace; the native
tree has no validated ignorable-whitespace classification. Root/element Value concatenates descendant
Text/CDATA data, omitting comments/PI and hosted content. Attribute Value is its actual stored value;
PI Value is data and LocalName is its target. Other unnamed kinds return empty Name/LocalName/Prefix
and namespace URI; absent native namespace/prefix becomes the BCL empty string. Do not lowercase XML
names or use HTML case-insensitive comparisons in the standard adapter.

Native extension for detached trees: find the highest ordinary parent and keep that actual object as
the navigation boundary. A detached element remains an Element at that boundary; do not synthesize
or attach a Document. DocumentFragment is exposed as Root with that fragment as UnderlyingObject.
`/` and MoveToRoot use this boundary, never OwnerDocument to jump into another tree. Template contents
can thus be queried explicitly, without being traversed from their host. Apply the same boundary to
future native shadow roots. X4a rejects detached Attr contexts with NotSupportedException; their final
parentless-position/mutation contract is a completion item, not silently redirected to OwnerDocument.
Reject direct Attr contexts in the actual XMLNS namespace with ArgumentException, including default
and prefixed declarations, empty undeclarations and conflicting declarations. Such an Attr has no
XPath attribute position; do not convert it to a namespace descriptor or its owner element. This
follows the [DOM XPath attribute mapping](https://www.w3.org/TR/DOM-Level-3-XPath/xpath.html#AttributeNodes).
A no-namespace Attr merely spelled xmlns remains an accepted ordinary attribute context.
Reject DocumentType and unknown native context kinds explicitly.

IsEmptyElement is false: the native tree does not preserve the source distinction between `<e/>` and
`<e></e>`. HasChildren answers the projected children. BaseURI is empty while the native model has no
base/document-URI contract; never infer a file URI from test paths or follow xml:base resources.
XmlLang searches real XML-namespace lang attributes on the current/nearest ancestor element, using
the attribute/namespace position's owner as its start; no-namespace lang is not xml:lang.

## Attribute and namespace axes

Attribute traversal retains insertion order but excludes actual XMLNS-namespace declarations. A
no-namespace attribute merely spelled xmlns in a manually constructed DOM remains an ordinary Attr;
do not recognize namespace declarations by qualified spelling alone. Defaulted attributes are normal
attributes. Attributes have no children/siblings; MoveToNextAttribute is a separate operation.

Namespace scope is determined from the owner element outward. Include xml's reserved binding,
explicit declarations, and DOM's implicit element prefix/default binding. At each level prefer the
element's own expanded-name binding over a conflicting explicit declaration on that same element,
then use nearer bindings over ancestors. Empty declarations block inheritance but do not become
namespace positions; never expose xmlns as a namespace prefix. NamespaceURI/Prefix of the navigator
on a namespace position are empty; LocalName/Name are the bound prefix, and Value is the bound URI.
The descriptor's NamespaceUri is that Value. This distinction is intentional.

`All`, `ExcludeXml`, and `Local` scopes must work. Local contains effective declarations introduced at
the queried element (including its implicit name binding); inherited bindings and the automatic xml
binding are excluded, unless xml is explicitly declared locally. A next-namespace operation honors
the requested scope without moving to another owner's namespace positions. Default undeclaration,
shadowed prefixes, explicit xml, and same URI under multiple prefixes need direct cursor tests.
LookupNamespace/LookupPrefix/GetNamespacesInScope must agree with this model, with deterministic
prefix selection where multiple answers are allowed. Expression namespace resolution is separate:
unprefixed element tests target no namespace, not the document's default namespace.

Scope computation must not walk the complete ancestor prefix independently at every element in a
deep namespace-free chain. Build uncached ancestor paths iteratively; share immutable scope storage
when no local binding changes. Do not allocate a dictionary for every DOM node. Per-owner namespace
descriptors are lazy, only for positions actually visited. Namespace declarations can be linear
in the number of applicable bindings, but repeated MoveToNextNamespace cannot rebuild the scope.

## Order, storage and mutation lifetime

ComparePosition uses logical identity first, then the order: element, its namespace positions, its
attributes, its ordinary descendants. Namespace prefixes sort ordinally for a deterministic local
order; attributes retain native insertion order. A text run occupies only its representative's
position. Return Unknown for unrelated tree roots, foreign navigator implementations or incompatible
projection modes. MoveTo across native sessions is allowed for the same root/projection and valid
versions, rebinding namespace descriptors to the receiver's session; do not replace its token/session.

Do not implement comparisons by repeated root-to-node scans. On the first actual order comparison
that needs it, build one iterative preorder index of ordinary nodes for this read session, excluding
hosted trees. Attributes/namespaces use their owner's index plus local phase/index; reserve children
after both phases. Subsequent comparisons are constant work. This is a lazy index of native identities,
not a second DOM, and ordinary forward evaluation need not allocate it. Build/check it with cancellation.

Likewise, an attribute cursor must not restart Element.Attributes and skip an increasing prefix on
every step. A lazy session-local array of Attr references per element visited on this axis gives a
linear first scan and constant next steps without changing Element's public API. Expose no mutable
array. Cache a text-run Value only after it is requested; avoid repeated growing-prefix concatenation.
Element/root Value is an iterative traversal plus one StringBuilder materialization, proportional to
visited nodes and returned characters. Do not eagerly cache every descendant string-value.

One session captures the ordinary root, actual owner document, and Document.MutationStamp before
building caches. All navigators/clones/iterators derived from it are read-only views valid only while
that document's stamp and owner relationship remain unchanged. Check at each native API boundary,
inside long authored loops, and before publishing a computed value. Any mutation invalidates the
session, including changes elsewhere in that owner document: throw InvalidOperationException rather
than returning stale indexes. Reject a saturated ulong.MaxValue stamp at session creation/use; equality
of a saturated stamp cannot prove freshness. Native objects remain single-owner/thread-affine; these
checks do not make concurrent mutation safe. No session is stored on a Document or global cache.

Namespace descriptors returned to a materialized consumer retain the binding value captured at
evaluation and the same owner element. They are not writable DOM nodes. Their OwnerElement can later
be adopted; do not copy its document into a stale field. Native node/Attr references in an already
materialized result likewise remain usable; only further cursor traversal is invalidated.

## Work and BCL error boundaries

The shared per-session work object holds the cancellation token and a bounded 256-unit poll cadence;
clones share progress. Charge node visits, attribute/binding scans, name/data characters processed,
index construction and copied result units. Check at entry, successful return, and before/after
unavoidable CLR allocation/copy. NameTable is session-local, shared by clones; atomize names/URIs
lazily with checks around BCL work, never globally intern strings. Tests use a per-invocation internal
checkpoint, not timers or static hooks. No parser input/entity limit is reused as a query budget.

This bounds authored adapter work cooperatively. BCL compilation, scalar functions, comparisons,
sorts, expression recursion and arbitrary caller resolvers are not preemptible via navigator polling.
Do not advertise a hard query timeout, an instruction quota, or cancellation of a constant expression
that never calls the navigator. Callers check their token before/after Compile, SetContext, Evaluate
and iterator MoveNext, but those checks cannot interrupt the BCL interval. No worker thread, catch-all,
hidden expression-length cap or XPath reparser is introduced to pretend otherwise.

Leave BCL syntax/type errors as XPathException/ArgumentException; cancellation and mutation failure
retain their own exception types. No MarkupParseException is manufactured. Browser keeps its existing
syntax translation/search fallback filters, with cancellation and mutation failures outside them.
Resolver callbacks can execute script: compile/bind before creating the read session, or detect a
stamp change afterward and fail. Never hold an observer callback or invoke script from native movement.
Standard XPath core evaluation has no fetch or external-document loader in this adapter.

## Finite dispatch and completion blockers

**X4a — dispatch now, entirely internal.** Add only `XPath/NativeXPath.cs`,
`XPath/NativeXPathNavigator.cs` (partial files for axes/values/order are allowed),
`XPath/XPathReadSession.cs`, `XPath/XPathNamespaceBinding.cs`, and focused tests under
`Jint.Tests.HtmlParser/XPath/`. Implement the standard cursor/model, BCL evaluation integration,
namespaces, identity, ordering, text-run conversion and cooperative lifetime rules above. No Browser,
native DOM, XML scanner, API snapshot or package dependency edits. For MoveToId, explicitly throw
NotSupportedException in this internal milestone and test that refusal; do not return an empty set
and pretend the currently missing metadata means that no ID exists. X4a is not X4 completion.

**X4b — prerequisite decision and finite native/XML follow-up.** Current XmlAttributeDeclaration retains
only CData versus non-CData, discarding whether a declaration was ID. XPath MoveToId needs effective
ID typing from declarations actually read, plus defined clone/import/adopt/remove/value-change behavior.
Have native/XML owners review a minimal typed-attribute identity contract before implementation;
do not reparse stored input/doctype, treat every attribute called id as DTD-typed, or expose a public
DTD registry. Duplicate values select the first element in tree order. Decide XML xml:id support
explicitly against its separate recommendation and existing parser scope. Browser HTML ID lookup is
its own compatibility rule. Implement MoveToId and detached Attr contexts, including detached value
mutation (which currently does not advance the document stamp), before public promotion. BaseURI
remains explicitly empty until a separate native URI contract exists; XPath 1.0 query evaluation
must not gain network access merely to populate it.

**X4c/B4/R4 — promotion and consumer migration.** Add a distinct internal
`CreateBrowserCompatibilityNavigator` factory after pinned AngleSharp.XPath 2.0.6 differential fixtures
have established its name/prefix/namespace-axis behavior. Its namespace erasure preserves `//div`
matching and resolved `svg:circle` not matching, including XML documents as today's Browser does.
The standard public factories never call it. Do not add a public ignoreNamespaces switch. Remaining
legacy axis quirks must be recorded as explicit Browser compatibility decisions, not copied silently
into the standard navigator. No changes to generated bindings are needed merely to replace the
hand-written evaluator calls; A1 hashes/dependency removal still require the migration owner.

Browser node-set extraction must switch on UnderlyingObject, wrap actual Attr identities, and provide
a namespace-result wrapper or explicit independently reviewed DOM limitation. It must not silently
drop either kind. Scalar coercion of a node-set uses its first navigator's complete Value, never
representative.TextContent for a coalesced text run or owner text for an attribute/namespace. Preserve
native identity and ordered/first-node results. Capture a materialized result completely before script
resumes; retain today's iterator-across-mutation behavior until a separately reviewed invalidation fix.
DevTools must define attribute/namespace search-result handling rather than pretending either is an
element with its own backend node ID. These conversion decisions block the production switch, not X4a.

## Acceptance evidence

Run freshly compiled Release tests on net8.0 and net10.0. No timing claim or Browser dependency in X4a.
Use literal expected identity/value/order fixtures plus a BCL XmlDocument differential oracle for
well-formed XML supported by both models (resolver disabled; no external resources). A differential
comparison must not erase native Attr identity or normalize away mapping differences.

Required groups: every forward/reverse axis and failed-movement position; union duplicate suppression;
relative/absolute paths, predicates, count/string/number/boolean and first-node conversion; namespaced
elements/attributes and xmlns exclusion; direct XMLNS Attr rejection for default, prefixed, empty and
conflicting declarations, with a no-namespace xmlns Attr accepted as a control; all three namespace
scopes, local shadowing/default removal,
implicit name bindings and per-owner namespace identity; adjacent Text/CDATA/empty runs in both
directions and all context members; PI/comment/doctype handling; exact clone/MoveTo/ComparePosition
semantics across sessions and disconnected trees; template boundaries and detached fragment roots;
xml:lang; mutation/adoption invalidation, saturation, and read-only methods. Independently test deep
namespace-free trees and wide attribute walks with work counters to prevent quadratic prefix scans.
Cancellation tests reach name/value scans, namespace/index construction and result materialization;
removing the relevant loop poll must make the probe fail before the final-return check can mask it.

Completion adds ID typing/defaults/duplicates and mutations, detached Attr, external packed consumers,
API snapshots and Browser XPathTests/DevTools search/htmx/PublishedToolTests. The production package
switch removes AngleSharp.XPath only after both consumer paths and their result-conversion gates pass.
Passing X4a alone is not evidence that Browser XPath or the complete standalone XPath surface is ready.

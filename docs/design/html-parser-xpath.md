# X4: native XPath navigator and Browser handoff

Independently reviewed design, 2026-09-23. Supplements [the architecture](html-parser.md), task X4
(X1 and D4 dependencies), and the B4/R4 migration assignments in
[A1](https://github.com/sebastienros/jint/blob/main/tools/html-parser-inventory/README.md). No runtime implementation or performance claim.
Integration inspected at `7792a9152`; XML, ordinary traversal, template ownership and mutation stamps
already exist. Keep the BCL XPath 1.0 engine, replace its tree adapter, and apply the bounded owned
compilation amendment below. Do not serialize/reparse into XmlDocument, copy the tree, or introduce
an XPath interpreter.

**Current implementation:** owned `NativeXPath` compile/evaluate/select entry points, expression
and typed result handles, and immutable namespace bindings are public. Node, attribute and namespace
contexts have both prepared-expression and string overloads. A namespace context is rebound in a
fresh session and rejected if its URI is no longer in scope. Navigators, BCL expressions, checkpoints
and namespace-erasure policies remain internal. Public snapshots and the unsigned packed consumer
cover this promotion; the milestone descriptions below retain the original design evidence.
See [the completion tracker](html-parser-completeness.md) for remaining standalone API work.

## Evidence and exact boundary

`Jint.Browser/Dom/Views/JsXPath.cs` compiles BCL XPathExpression, supplies an IXmlNamespaceResolver,
clones the expression for evaluation, then materializes an XPathNodeIterator. Its node extraction
currently depends on HtmlDocumentNavigator.CurrentNode and `List<INode>`. Its scalar coercions include
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

The evaluator amendment below supersedes the original proposal to promote CreateNavigator. Keep
these cursor factories internal, including the X4c namespace-context factory. That factory creates
a fresh session and locates the captured owner/prefix only if its URI still matches; otherwise throw
InvalidOperationException. Promote the owned compile/evaluate/select surface below and
XPathNamespaceBinding's three getters instead. Do not expose read-session caches or Browser namespace
erasure. Completion prerequisites, public snapshots, XML documentation and an unsigned packed consumer
still gate promotion.

`UnderlyingObject` returns the actual Node, actual Attr, or shared XPathNamespaceBinding for its
position. Repeated visits and clones in the same session return the same object. It never returns
the attribute owner in place of an attribute, nor an xmlns Attr for a namespace position. Across fresh
sessions namespace descriptors need not be reference-identical; logical equality is owner identity,
prefix and bound URI. Ordinary nodes/attributes retain their global native identities across sessions.

Internal cursor tests still exercise UnderlyingObject, including all three alternatives, without
requiring Jint.Browser. Public consumers receive materialized native results through the amendment
below; BCL expressions, navigators and iterators do not cross that boundary.

## X4b3 evaluator amendment: parentless attributes and owned compilation

This amendment also governs X4b's detached-Attr evaluation and packed-consumer requirements in
[the identifiers design](html-parser-xpath-identifiers.md). It preserves that document's actual Attr
root, Attribute NodeType, false/unchanged failed movement and fail-on-mutation cursor semantics.
It changes the not-yet-public evaluator surface, not those cursor facts. Implementation remains a
finite XPath-owner task; no native DOM, XML scanner or Browser runtime edits belong in X4b3.

The current BCL [FollowingQuery.Advance](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.Xml/src/System/Xml/XPath/Internal/FollowingQuery.cs)
ignores a failed MoveToParent for an Attribute/Namespace input and repeatedly selects descendants
from the unchanged position. It does not call MoveToFollowing. Read-only probes reproduced this on
.NET 8.0.18 and 10.0.10, including `count(following::node()) + 1`; deterministic cancellation ended
the retry loop, but cancellation is not a successful answer. The truthful parentless cursor cannot
fix this through movement overrides. Never invent a parent, change NodeType after failed movement,
report successful movement without moving, or omit following expressions from acceptance.

### Exact surface and result ownership

Implement the following internally first, then promote these members with default cancellation
tokens only after all X4 completion gates. All constructors remain internal. NativeXPathNavigator,
XPathReadSession and CreateNavigator remain internal permanently under this proposal.

```csharp
internal sealed class NativeXPathExpression
{
    internal string Source { get; }                 // exact caller source, never rewritten text
    internal XPathResultType ReturnType { get; }    // BCL's static expression result type
}

internal sealed class NativeXPathResult
{
    internal XPathResultType ResultType { get; }    // Number, String, Boolean or NodeSet only
    internal double NumberValue { get; }
    internal string StringValue { get; }
    internal bool BooleanValue { get; }
    internal IReadOnlyList<object> Nodes { get; }
    internal string FirstNodeStringValue { get; }
}

// Add these to NativeXPath; retain its cursor factories only for internal use.
internal static NativeXPathExpression Compile(string source,
    IXmlNamespaceResolver? resolver, CancellationToken cancellationToken);
internal static NativeXPathResult Evaluate(Node context, NativeXPathExpression expression,
    CancellationToken cancellationToken);
internal static NativeXPathResult Evaluate(Attr context, NativeXPathExpression expression,
    CancellationToken cancellationToken);
internal static IReadOnlyList<object> Select(Node context, NativeXPathExpression expression,
    CancellationToken cancellationToken);
internal static IReadOnlyList<object> Select(Attr context, NativeXPathExpression expression,
    CancellationToken cancellationToken);
```

For both Evaluate and Select, add string-source overloads for each context kind with parameters
`(context, string source, IXmlNamespaceResolver? resolver, CancellationToken cancellationToken)`.
They compile/bind before constructing a read session, then use the same owned-expression path.
At public promotion, resolver parameters default to null and tokens to default. X4c adds the four
corresponding XPathNamespaceBinding-context overloads, using the existing captured-binding freshness
rule. Do not add an unimplemented namespace-context overload in X4b3.

The expression privately owns its prepared BCL expression. Its configuration is fixed after Compile;
there is no public SetContext, Clone, AddSort, BCL-expression getter or accepting-BCL-expression
overload. Supply a resolver when compiling a different namespace context. Each evaluation clones
private BCL query state, so the same prepared handle can be reused against fresh sessions without
binding it to a document. Caller-provided resolvers/extension contexts retain their normal BCL
semantics and any caller-owned affinity; this is not a promise that such callbacks are thread-safe.
Custom sort configuration is not part of XPath 1.0 or either Browser consumer. If added separately,
its sort expressions must use this same owned compilation boundary.

ResultType describes the actual evaluated result; do not return Any, Error or a Navigator result.
Each scalar getter succeeds only for its exact kind. Nodes and FirstNodeStringValue succeed only for
NodeSet. A wrong-kind getter throws InvalidOperationException; getters do not perform conversions.
Nodes is an immutable, materialized, ordered snapshot whose only member types are actual Node,
actual Attr and immutable XPathNamespaceBinding. No nulls, wrapper elements or cursor objects appear.
The object union is restricted to this result boundary; it adds no object-typed native tree API.
Select requires a node-set, otherwise throws XPathException, and returns the same identity/order
projection without calculating node string values. Evaluate captures the complete first node's
XPath string-value for FirstNodeStringValue, or empty for an empty set, while its session is valid.
This is needed for Browser node-set scalar conversion: a coalesced Text/CDATA run's value cannot be
reconstructed from its representative's TextContent. Do not eagerly calculate every selected node's
string-value. Identity-only consumers should use Select to avoid that extra first-value traversal.

After successful publication, scalar values, captured first string-value and collection membership
remain stable across DOM edits. Contained native references remain the real editable objects, as in
existing materialized Browser results. The result stores no live read session or mutation callback.
Null context/source/expression throws ArgumentNullException; the existing unsupported-context and
XMLNS-Attr rules remain unchanged. Preserve BCL syntax/type errors as XPathException/ArgumentException,
cancellation as OperationCanceledException, and session invalidation as InvalidOperationException.
An unsupported BCL extension result type throws XPathException; do not coerce it to a string or an
empty node-set. The supported result domain is XPath 1.0's four standard kinds.

### Compilation rule and opaque BCL fence

Validate the original source with BCL XPathExpression.Compile before transforming it, so a rewrite
cannot make invalid syntax acceptable. The validation compile need not bind a resolver. A single
token-aware pass then prefixes every actual `following::` axis step with
`self::node()[parent::node()]/`, preserving its original node test and predicates. Recognize XPath 1.0
quoted literals, name/token boundaries and legal XML whitespace between the axis name and `::`;
do not replace text inside strings, a QName, `following-sibling`, or an ordinary element/function
name. Retain original source for diagnostics and Source. Reuse the original compiled expression
when no guard is required; otherwise compile the guarded source. Bind the supplied resolver only
to the expression retained for evaluation, avoiding duplicate resolver callbacks from validation.

The guard retains the same context node when it has a parent. Every parentless context has an empty
following axis, so discarding that context before evaluating the following step preserves its result.
This applies inside functions, predicates, unions and qualified-name tests; it is not an expression
whitelist or a detached-context special-case answer. Query evaluation, namespace resolution, values,
ordering and functions remain BCL-owned. Use a bounded scanner/builder, not a second XPath parser or
interpreter, regex replacement, reflection into the private BCL query tree or dynamic code generation.

An arbitrary already-compiled BCL expression cannot be recompiled safely from Expression: its bound
namespace/XsltContext and added sort expressions are opaque. XPathExpression's constructor is also
internal, so the owned handle cannot subclass it. Do not trust a source-text marker, global registry
or object identity cache to certify mutable BCL expressions across Clone/AddSort.

Fence the internal cursor too. Override Compile and every public Evaluate/Select/SelectSingleNode/
Matches overload (string, string-plus-resolver, compiled expression and expression-plus-context where
present) to check cancellation/freshness and throw NotSupportedException directing the caller to
NativeXPath.Compile/Evaluate/Select. Include `Evaluate(XPathExpression, XPathNodeIterator?)` and
`Matches(XPathExpression)`; inherited string helpers must not compile an opaque query before reaching
a later refusal. None may execute an opaque query or hang. These are internal unsupported evaluator
entry points, not partially supported public XPath expressions. A narrowly internal EvaluatePrepared
entry alone calls the two-argument `base.Evaluate(privateExpression.Clone(), null)`; the one-argument
base overload would dispatch back through the fenced virtual overload. It must not accept an arbitrary
caller-supplied BCL expression.
No public method returns a BCL cursor/iterator that would expose a bypass.
The supported-runtime surface was verified on .NET 8.0.18 and 10.0.10: all 13 listed entry points
(one Compile, four Evaluate, three Select, three SelectSingleNode, two Matches) are virtual and
non-final. Override them directly; no compilation-before-refusal fallback is necessary.

### Work, freshness and migration gates

Poll the compilation scan and authored output-building loops at the existing bounded work cadence;
build the guarded source linearly with bounded expansion per axis token, without flattening a growing
prefix. Bracket BCL compilation/binding/query evaluation and unavoidable final allocation/copy with
cancellation checks. BCL's non-preemptible intervals remain exactly the practical limitation described
below; do not claim that this adapter supplies a hard query timeout. No hidden expression-length cap.
Create the DOM read session after compile/bind callbacks; check it around evaluation and each iterator
advance, all authored result-copy loops, first-node string-value calculation and final publication.
Use per-invocation deterministic checkpoints. Cancellation/mutation discards the entire pending result;
no partial collection escapes. A fresh evaluation with the same compiled handle can then succeed.

Browser migration remains X4c/B4/R4-owned. In `Dom/Views/JsXPath.cs`, replace the BCL CompiledExpression
alias with NativeXPathExpression, compile with the current NamespaceResolver, retain that handle in
JsXPathExpression, and remove the external Clone/SetContext sequence. Run uses the internal Browser
compatibility projection with the owned evaluation path. Coerce switches on NativeXPathResult kinds;
node-set scalar conversion uses captured FirstNodeStringValue, never representative.TextContent.
Requested node-only result kinds can use Select; preserve existing type-error translation for scalars.
`DevTools/DomDomain.Events.cs:XPathMatches` uses owned Select and explicitly maps all three native
identity kinds. Namespace erasure, Attr/namespace wrappers and DevTools backend IDs remain their
existing independent compatibility gates. Neither caller may retain AngleSharp merely to evaluate
detached attributes, nor silently drop a result kind. Generated bindings need no incidental change.

Acceptance adds literal expected tests for detached Attr `/`, self, all empty axes and `id()`, including
`following::node()`, `following::*`, `count(following::node()) + 1`, nested function/predicate/union
uses, and guarded expressions combined with position()/last(). Check ordinary documents, attached
Attrs, namespace positions, detached elements/fragments and actual native reference identity. Test
resolver binding and prepared-handle reuse across different trees; unknown prefixes/errors must not
vanish merely because a guarded context is empty. Strings containing `following::`, whitespace around
`::`, QName lookalikes, following-sibling and invalid source must prove token/source fidelity. Cover
all fenced overloads, wrong-kind getters and Select-on-scalar. Counter-based tests cover long source,
many guards, materialization cancellation and a mutation between evaluation and final publication.
Every valid following query must finish with its correct answer without requiring cancellation.

Update the unsigned local-only packed consumer and API snapshots to this owned surface, superseding
the identifiers document's proposed public MoveToId/UnderlyingObject cursor probe. Parse ID-declared
XML, evaluate id(), inspect actual result references, edit/import the typed Attr and evaluate afresh;
preserve internal direct MoveToId/session-invalidation tests. Exercise every typed result, detached
following expressions, resolver reuse and stable published snapshots through the packed package on
both TFMs without friend access or ProjectReference. A separate Native AOT pack/run remains required
before claiming AOT support. The existing full parser and Browser migration gates remain mandatory.

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
The standard public Evaluate/Select paths never call it. Do not add a public ignoreNamespaces switch. Remaining
legacy axis quirks must be recorded as explicit Browser compatibility decisions, not copied silently
into the standard navigator. No changes to generated bindings are needed merely to replace the
hand-written evaluator calls; A1 hashes/dependency removal still require the migration owner.

Native result materialization switches on UnderlyingObject; Browser extraction switches on those
materialized identities, wraps actual Attr identities, and provides
a namespace-result wrapper or explicit independently reviewed DOM limitation. It must not silently
drop either kind. Scalar coercion of a node-set uses captured FirstNodeStringValue, never
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

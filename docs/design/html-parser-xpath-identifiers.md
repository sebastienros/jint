# X4b: typed identifiers and detached attribute XPath contexts

Design for independent review, 2026-09-23. Completes the prerequisite decisions in
[X4](html-parser-xpath.md); it does not promote or implement the navigator. Inspected common native
code through `2d7a13c0d`, including the reviewed X4a implementation `e978af316` + `c1635d86e`.
Native DOM mutation files remain reserved by D7a1. This document authorizes no overlapping dispatch.

The result is a working standard `MoveToId`, parsed ID provenance that survives native identity
operations, and a parentless detached-Attr cursor with the same fail-on-mutation rule as X4a. Keep
the BCL XPath 1.0 evaluator, empty BaseURI, no network access, and no new package dependency.

## Sources, existing seams, and deliberate scope

[XPath 1.0 §§4.1, 5.2.1, 5.3](https://www.w3.org/TR/1999/REC-xpath-19991116/#unique-id) supplies
DTD-based IDs, first-in-document-order handling of duplicate values, and defaulted attributes.
[XML 1.0 §§3.3, 5.1](https://www.w3.org/TR/xml/#attdecls) governs effective declarations and the
nonvalidating parser's normalization/default processing and unread-parameter-entity boundary.
[DOM attribute mapping](https://www.w3.org/TR/DOM-Level-3-XPath/xpath.html#AttributeNodes) uses the
owner element as an attribute's XPath parent and excludes namespace declarations from that axis.
[Microsoft's MoveToId contract](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.xpathnavigator.movetoid?view=net-10.0)
requires success to position on the element and a miss to leave the cursor unchanged. These are
separate from [modern DOM's ID concept](https://dom.spec.whatwg.org/#concept-id), which uses the
no-namespace `id` attribute. Do not substitute a DOM/CSS ID lookup for the standard XPath adapter.

`XmlTreeParser.Dtd.cs` already keeps effective per-element/per-attribute declarations, first declaration
wins, but `XmlAttributeDeclaration` retains only `CData`, default value and fixed status. Start-tag
parsing applies those declarations before namespace resolution, supplies defaults, then hands one
duplicate-free batch to `Element.InitializeParsedAttributes`. This is the point to preserve ID typing;
reparsing a doctype, retaining parser dictionaries, or consulting a destination document is unnecessary.
The recognized XHTML local catalog currently supplies character entities, not ATTLIST declarations.
Its public identifier alone therefore cannot make every XHTML `id` DTD-typed.

`Attr` is a stable object distinct from Node. `NodeCloner.CloneAttribute` is shared by Attr.Clone,
Document.ImportAttribute and element attribute copies, including deep document/import operations.
`Element.SetAttributeNode`, its append/replacement lanes, and `AdoptAttributes` change attribute owners.
There is no public AdoptAttribute entry; moving a detached Attr through SetAttributeNode is sufficient.
`Attr.Value` currently marks a mutation only when attached. A detached edit therefore escapes X4a's
document-stamp invalidation, which is why its factory deliberately refuses detached attributes.

`XPathReadSession` owns caches and captures a document stamp; clones share the session. X4a's Root is
a Node and MoveToId throws NotSupportedException. Both are internal completion seams, not public debt
to disguise as an empty result. Browser's `JsXPath` and DevTools `XPathMatches` still use
AngleSharp.XPath with namespace erasure and node-only extraction. Their production switch stays in
X4c/B4/R4; the existing Browser XPath tests do not establish typed-ID behavior.

The dotnet-inspect skill was attempted again for XPathNavigator.MoveToId; dnx failed resolving
api.nuget.org. The public Microsoft contract and the existing compiled X4a override establish the
required signature. No tooling failure is evidence that an unsupported method is complete.

## Effective typing: immutable provenance on each Attr

Add only the following internal metadata surface, in namespace `Jint.HtmlParser`:

```csharp
// On Attr; initialized by its internal constructor, never changed after construction.
internal bool IsDtdId { get; }

// Extend the existing internal constructor with a trailing default-false argument.
internal Attr(Document ownerDocument, string? namespaceUri, string localName,
    string? prefix, string value, bool isDtdId = false);

// On ParserAttribute; extend its constructor in the same way.
internal bool IsDtdId { get; }
```

Extend private XmlAttributeDeclaration with `bool IsId`, set only for the exact `ID` type token.
Keep its existing CData distinction rather than replacing it with a public DTD type hierarchy. In the
final resolved-attribute loop, look up the raw qualified attribute name in the already effective
declarations for the raw qualified element name and pass `declaration.IsId` to ParserAttribute.
This covers explicit and defaulted attributes identically. InitializeParsedAttributes copies the flag
into the newly allocated Attr before publishing the batch. All existing HTML and factory call sites
default to false; no HTML tag/attribute spelling may set it.

Matching uses the DTD's lexical qualified names, case-sensitively, before namespace expansion. Equal
namespace URIs reached through different prefixes do not make different DTD names interchangeable.
The flag records the declaration that applied at parse time. It is not a claim that the current tree
passes DTD validation, and does not retain the declaration's strings, parser, input or source document.
An actual XMLNS attribute can validly be declared ID and contributes its typed value to its element's
identifier. XPath's exclusion of namespace declarations from the attribute axis does not erase that
element-ID contribution. Preserve the flag and include it in FindId, while retaining the existing
XMLNS context/attribute-axis rejection. Enumerate native attributes for ID indexing, not the filtered
XPath attribute-axis cache.

Keep normalization and default insertion in their current parser algorithms. ID is already non-CDATA;
adding the flag must not perform normalization a second time or change character-reference handling.
No default provenance bit or public Attr.Specified is required for XPath. This task does not add DTD
validation: an accepted ID declaration with a literal/#FIXED default still marks its supplied Attr,
although such a default violates XML's ID validity constraint. #IMPLIED/#REQUIRED supplies no absent
attribute. Missing required attributes and multiple declared ID attributes remain validation issues.

Typing obeys exactly the current declaration-acceptance boundary. A declaration encountered after an
unread PE cannot add ID typing when its defaults and normalization would be suppressed; earlier
effective declarations remain. Exercise the standalone=yes exception independently. Fully read
internal PE declarations participate normally. Unknown external subsets/PEs contribute no inferred
typing, even for familiar names or matching public identifiers. Preserve existing omission records
and fatal well-formedness checks. No resolver, file access or network fallback is introduced.

XML fragment parsing starts with its existing empty DTD declaration state: the context's document and
doctype do not confer ID typing on newly parsed fragment attributes. Serialization/reparse without
the declaration also loses provenance. This is expected and must be a negative fixture.

## xml:id decision

X4b does **not** implement the separate [xml:id Recommendation](https://www.w3.org/TR/xml-id/).
An undeclared XML-namespace `xml:id` is an ordinary attribute for this adapter. If an effective DTD
declaration actually gives it type ID, it participates through the same provenance path as any other
declared attribute. Neither literal `id`, literal `xml:id` in no namespace, namespace URI alone, nor
DocumentKind selects an additional typing rule.

This is an explicit XPath 1.0/DTD scope decision, not a claim of xml:id conformance. That Recommendation
requires normalization, ID assignment even on erroneous values, constraint checking and application
reporting; a spelling check in MoveToId would implement only a misleading fragment. A future xml:id
slice must choose its nonfatal diagnostics surface, parse-time and DOM-edit processing, normalization
visibility, and interactions with DTD typing as one reviewed change. It is not needed to complete the
already specified standard X4 surface. Add positive DTD-declared xml:id and negative undeclared xml:id
fixtures so promotion cannot accidentally imply the broader feature.

## Identity and mutation lifecycle

The following are native provenance decisions for an editable, nonvalidating tree, not destination-DTD
revalidation or an implementation of historical DOM Level 3 TypeInfo/setIdAttribute APIs:

| Operation | Identity and effective typing after the operation |
| --- | --- |
| Assign Attr.Value, or SetAttribute/SetAttributeNS updating an existing Attr | Same Attr, same flag, exact new stored value. No DTD normalization or revalidation on a DOM write. |
| Remove an attribute | Returned/detached Attr keeps the flag; the former element no longer contributes its value. No automatic default resurrection. |
| Replace with SetAttributeNode | Incoming Attr's flag wins; outgoing Attr keeps its own flag after detachment. The property name does not transfer typing. |
| Remove, then create a new attribute with the same name | New object is untyped, even on the original element in the original document. |
| Reattach a retained typed Attr to another element | It remains typed on its actual new owner, regardless of that element's name. |
| Attr.Clone / ImportAttribute | Fresh detached identity, copied flag/value/name, requested owner document. |
| Element/deep subtree/document clone or import | Every copied attribute gets a fresh identity and its original flag. No lookup against either doctype. |
| Adopt an element/subtree, or attach an Attr across documents | Preserve actual Attr identity and flag; update OwnerDocument consistently. |
| Remove/replace a DocumentType | No retagging: typing belongs to the already parsed attribute, not an active document schema. |

The clone rule also applies inside existing template and clonable-shadow copying algorithms; XPath's
ordinary-axis boundary remains separate. Do not change those traversal/ownership algorithms to copy
a flag. No document ID table is maintained during mutations, and no observers run inside lookup.

Use the existing saturating Document.MutationStamp to fix detached freshness. Every successful Value
assignment advances OwnerDocument's stamp once, including detached and equal-value writes. Attached
writes still queue exactly the existing mutation record with its original old value; detached writes
queue none. Do not add a second mark on the attached branch. A failed null assignment changes nothing.
The conservative invalidation of other XPath sessions in that document is intentional and matches
X4a's document-wide rule. No per-attribute version counter, subscriptions or session registry is needed.

Centralize changed attribute document ownership behind one internal method:

```csharp
// OwnerDocument retains its public getter but its setter becomes private.
internal void Rehome(Document document);
```

Rehome is a no-op for the same document; otherwise it updates OwnerDocument and marks both the old and
new documents using their saturating stamps. It emits no mutation record. Route existing
SetAttributeNode append/replacement and AdoptAttributes assignments through it. Fresh construction and
clone initialization set the initial owner directly and do not invalidate a source read session merely
because a copy was made. Existing tree adoption may already advance the same stamps; more than one
increment is harmless and no exact increment count becomes public. Keep observer order unchanged.

Marking the old document on cross-document attachment is required even when a root/owner check could
catch the current mismatch: move the Attr away and back before a later read and those identities match
again. The old session must still fail. Same-document attachment/removal already marks that document.
Audit every writable Attr owner/prefix site when implementing; any future observable prefix change
must also invalidate sessions. Do not expand this task into unrelated namespace setter fixes.

## MoveToId and its cost

Add internal `Element? XPathReadSession.FindId(string id)` and an `IdIndex` XPathWorkStage. The navigator
checks freshness/cancellation, rejects null with ArgumentNullException, calls FindId, and changes its
position only after a successful final check. Empty input returns false. A missing ID leaves position
and axis state intact; allocation, cancellation and mutation failure must not partially move a cursor.

Build an ordinal string-to-Element dictionary lazily on the first nonempty lookup. Walk the session's
ordinary root in preorder, including that root if it is an Element. For each element, enumerate its
actual attributes once; accept IsDtdId with a nonempty stored value, including XMLNS attributes. Insert with TryAdd so the
earliest element retains each key. Build into a local dictionary and publish only after Check succeeds.
No eager index on parser completion, no DOM-wide registry, and no scan per requested ID token.

The index belongs to this read session and is shared by clones. A fresh session after mutation rebuilds
it; old sessions throw, rather than repairing in place. Tree reordering, value edits, removal,
replacement, adoption and import insertion therefore all change the next query naturally. An unchanged
session's subsequent lookup costs the query string hash/equality and expected constant table access.
First construction has expected O(N + A + C) cost, for visited ordinary nodes, attributes and candidate characters;
retained space is O(U), distinct candidate values, with existing strings/element references as entries.
No per-node allocation is needed for an iterative child/sibling/parent preorder walk.

FindId uses the session root, not OwnerDocument as an escape hatch. For detached element/fragment
roots this is X4's existing native extension: only elements inside that ordinary tree can match. Shadow
and template contents are excluded from their host's query and participate when separately queried.
The owner Document is a freshness domain, not a lookup root. Detached Attr roots have no elements and
always miss, even when that Attr is typed or its OwnerDocument contains a matching element.

MoveToId compares one exact value; it does not split or trim its argument. The BCL implements XPath
id()'s tokenization, node-set conversion, duplicate suppression and result ordering. Test those through
Evaluate as well as calling MoveToId directly. Do not implement another id() function in native code.

For invalid parsed data or arbitrary later DOM writes, keep a deterministic native extension: every
nonempty stored value of a typed Attr is an exact candidate, without another Name validator.
Two ID-typed attributes on one element can supply two keys. A value containing whitespace can match
direct MoveToId with that exact string but not a single token produced by id(). Empty values contribute
nothing. These cases do not establish XML validity; do not add fatal parser checks to make the index
simpler. For valid documents, this policy coincides with the required ID model.

Charge and poll all authored node/attribute/candidate-character work with X4a's shared 256-unit cadence,
entry/exit checks and deterministic per-invocation checkpoint. Checks bracket dictionary hashing,
equality and allocation. CLR string hashing/copy and BCL evaluation remain non-preemptible intervals;
do not describe Work(value.Length) as an interruptible character loop. Add bounded-progress fixtures
with many short IDs and large untyped attribute lists so a final Check cannot hide missing loop polls.
If implementation introduces an authored character loop, that loop must poll too. No timing claims
or benchmarks run as part of this dispatch.

## Detached Attr read sessions

Add an internal XPathReadSession constructor accepting Attr. Attached contexts keep the existing
ordinary-root path; detached contexts capture that actual Attr as their boundary and its OwnerDocument
and stamp. Do not create an Element/Document wrapper, temporarily attach it, or reuse the value's owner
document as a navigable root. Avoid coupling this task to D6r1's new identity type or editing its files.
The session's private representation can hold two mutually exclusive fields with these internal views:

```csharp
internal Node? TreeRoot { get; }             // null for a detached-Attr session
internal Attr? DetachedAttributeRoot { get; } // otherwise null
internal object RootIdentity { get; }        // the actual Node or actual Attr
```

Replace X4a's Node-only Root uses deliberately: ordinary indexing/traversal uses TreeRoot; root movement,
session compatibility and equality use RootIdentity. No public object-typed tree API is added. Check
for a detached session verifies the captured document stamp is unchanged and not saturated, the Attr
still has that document, and OwnerElement is still null. Normal sessions retain their existing root
checks. Perform the same checks for properties, movement, Clone, MoveTo, IsSamePosition, ComparePosition,
namespace lookup and every inherited helper that reaches this adapter. Sessions never become live again
after an attach/remove or cross-document round trip.

| Detached Attr operation | Result |
| --- | --- |
| NodeType / UnderlyingObject / name and value getters | Attribute / the identical Attr / its native fields; Value remains checked. |
| MoveToRoot / XPath `/` | Stay on and return that same Attribute position. This parentless-root behavior is a native extension, not a synthetic XPath Root node. |
| Parent, child, next/previous sibling, first/next attribute, namespace-axis movement | False, unchanged position. No OwnerDocument or former-owner traversal. |
| HasChildren / HasAttributes / IsEmptyElement | False. |
| XmlLang / BaseURI | Empty. No inherited former-owner state or URI inference. |
| MoveToId | False for non-null arguments; no owner-document scan. |
| Clone / fresh factory for the same detached Attr | Same logical position; Clone shares session, fresh factory makes its own current session. |
| ComparePosition / IsSamePosition / MoveTo across valid sessions | Same/true/success only for identical root Attr; distinct detached attributes are unrelated even when their names/values/documents match. |

Namespace lookup retains X4a's context-free reserved bindings: xml and xmlns resolve to their reserved
URIs, empty prefix to empty, unknown prefixes to null; GetNamespacesInScope(All) contains xml only,
Local/ExcludeXml are empty. The attribute's own prefix is not an in-scope declaration; NamespaceURI
still reports its actual expanded name. Namespace axis positions do not appear on an attribute.
Direct actual XMLNS Attr contexts remain rejected, attached or detached; no-namespace `xmlns` remains
an ordinary Attr. ComparePosition must resolve identical detached roots before any owner-index lookup.

Materialized UnderlyingObject references survive invalidation as ordinary native objects. Subsequent
cursor reads throw; creating a fresh navigator after editing, attachment, detachment or adoption is the
way to observe the new tree. Do not poison the Attr itself or keep a session back-reference on it.

## Finite implementation and public gates

| Slice | Exact ownership and exit condition |
| --- | --- |
| X4b1, native prerequisites | After D7a1 explicit handoff only: Attr.cs, ParserAttribute.cs, Element.cs, NodeCloner.cs, focused Dom tests. Immutable flag, clone/import propagation, detached Value stamps and centralized Rehome. Document.MarkMutation already suffices; no public Document API or Node algorithm rewrite. Reconcile D7/form, D5/shadow, D6/live-state hooks with their owner before touching shared lanes. |
| X4b2, XML assignment | After X4b1 and XML-owner handoff: XmlTreeParser.cs and .Dtd.cs plus focused Xml tests. Preserve ID token and apply it to final explicit/defaulted ParserAttributes using accepted declarations. No public parse options, DTD registry, fetching or corpus pin edits. |
| X4b3, navigator completion | After X4b1/X4b2: existing XPath files, optional XPathReadSession.Ids.cs partial, and XPath tests. Implement FindId/MoveToId, dual root representation and detached factory; remove internal milestone refusals only when positive/negative/cancellation tests pass. No native mutation file edits in this slice. |
| X4c, reviewed public promotion | Separate coordinator-owned slice after all X4 acceptance gates: factories and namespace binding described below, XML docs, both public snapshots and unsigned packed-consumer probes. B4/R4 production migration remains independently gated. |

There is no independent X4b1 shared-file dispatch while D7a1 holds its reservation. The design is the
only present change. Internal test access does not justify promoting IsDtdId, Rehome, constructors,
FindId, work stages or read-session storage. No public setter for ID typing and no public declaration
collection are required. No additional dependencies or Browser/generated/A1 snapshot edits belong to
X4b1–X4b3.

The public proposal remains exactly X4's `public static NativeXPath`, returning BCL XPathNavigator
from CreateNavigator(Node, CancellationToken = default), CreateNavigator(Attr, CancellationToken =
default), and CreateNavigator(XPathNamespaceBinding, CancellationToken = default), plus the immutable
public XPathNamespaceBinding with OwnerElement, Prefix and NamespaceUri getters and no public
constructor. The namespace-context overload must create a fresh session and verify the captured
owner/prefix/URI, as X4 specifies. Navigator implementation, ID provenance and Browser compatibility
mode stay internal. Promote these together only after the complete surface works; there is no public
MoveToId NotSupportedException milestone. BaseURI stays documented as empty.

Run freshly compiled Release HtmlParser tests on net8.0 and net10.0. X4b1 must cover existing native
mutation/clone/import/adoption suites, not just the new bit; X4b2 must preserve XML omission/default
fixtures; X4b3 runs the whole XPath suite. No --no-build and no timing run. Required literal probes:

- Explicit `key ID` beats untyped `id`; CDATA/IDREF/IDREFS do not type; first repeated declaration wins
  in both ID/CDATA orders; prefixed DTD names and same-URI different-prefix controls.
- Parse `<!DOCTYPE r [<!ELEMENT r EMPTY><!ATTLIST r xmlns:p ID #IMPLIED>]><r xmlns:p="key"/>`:
  MoveToId("key") and id('key') select the actual r element, while attribute::* excludes xmlns:p and
  a direct navigator for that XMLNS Attr is rejected. Its relative namespace URI is deprecated, not
  a validity or namespace-well-formedness failure; see [Namespaces in XML §2.2](https://www.w3.org/TR/xml-names/#iri-use).
- Defaulted typed Attr identity/value, explicit override, #IMPLIED absence, invalid-but-accepted ID
  default, declaration before/after unread PE, standalone=yes, read internal PE, unknown external subset
  and local-character-catalog negative control. No-fragment-DTD-inheritance and no-fetch assertions.
- Duplicate winners change on sibling reorder/removal; edits lose old keys; replacement with untyped
  Attr removes eligibility; reattachment restores it. Clone/import/adopt tests compare actual Attr
  references, copied flags and fresh query results, including conflicting destination declarations.
- Value and owner round trips invalidate every old clone/session, including detached equal writes and
  mutation elsewhere in the captured document. Null writes fail without invalidation. Saturation is
  rejected. Detached edits emit no records; attached oldValue/order remains unchanged.
- Direct MoveToId miss preserves an attribute/namespace cursor; id('b a b') returns native elements in
  document order, node-set arguments work, and two lookups share a single index (including misses).
  Detached fragment/element, template and shadow boundaries cannot leak into another ordinary tree.
- Detached Attr `/`, self::node(), string(.), count(../*), ancestor/preceding/following and failed
  movement, clone/fresh-session equality, unrelated attributes, own prefix versus namespace lookup,
  XMLNS refusals, attachment and cross-document round trips.
- Deterministic cancellation during index visits/attribute scans and before index publication; retry
  with a fresh session succeeds. Work-count growth for doubled wide/deep inputs is linear; repeated
  hits/misses cannot rescan the tree. Dropped sessions must not stay alive through a Document/Attr.

At promotion, extend `tools/html-parser-package-consumer` against a newly packed local-only package,
with no ProjectReference, signing key or friend access. A meaningful public probe parses an ID-declared
document, calls MoveToId and Evaluate(id(...)), and checks ReferenceEquals on UnderlyingObject. It then
changes the actual typed Attr, observes invalidation, and resolves only the new value with a fresh
factory. Import into another document and compare fresh identities; query a detached Attr and prove
its edit invalidates the old cursor. Include untyped id/xml:id negatives and duplicate order. Both
net8.0 and net10.0 consumer runs, package dependency inspection and reviewed API snapshots are promotion
gates, not substitutes for native tests. Do not claim new AOT support without its separate pack/run leg.

Finally B4/R4 must independently decide HTML/no-namespace ID compatibility against pinned
AngleSharp.XPath behavior, attribute/namespace result conversion, and Browser error translation.
They may not change standard FindId to preserve a Browser quirk. Existing materialized Browser results
may retain native identities across mutations; native cursors still fail once their session is stale.

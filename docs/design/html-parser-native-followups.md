# Native DOM follow-up contracts

Dispatch against integration `3ca355b77` (D3a clone/import), 2026-09-23. These are finite follow-ups to
[the architecture](html-parser.md), not permission to edit other active tasks' files. Land the XML
shared-owner `Document` edit first; then one native owner stages metadata, adoption/replace-all, and
template ownership as separate commits. D5 follows those native edits. Each commit updates the real
public API snapshots and runs Release tests on net8.0 and net10.0 with MTP `--project`.
The [trusted parser construction seam](html-parser-construction.md) is a separate immediate native
commit: fresh append and validated bulk attributes avoid repeated ancestor/duplicate scans while
retaining shared semantic bookkeeping. It also defines parser-only XML name/CDATA creation.

## Document metadata: immediate XML prerequisite

The native owner edits `Dom/Document.cs`, `NodeCloner.cs` and dedicated metadata tests. Exact additions:

```csharp
// Document
public string ContentType { get; }
public string CharacterSet { get; }
public static Document CreateXml(string contentType);
internal Document(DocumentKind kind, string contentType);
```

Keep the existing constructor/factories. `new Document(Html)`/`CreateHtml()` use `text/html`;
`new Document(Xml)`/parameterless `CreateXml()` use `application/xml`. The overload accepts a canonical
lowercase XML MIME essence (`text/xml`, `application/xml`, or a valid type/subtype ending `+xml`),
without parameters. Null throws `ArgumentNullException`; noncanonical/non-XML values throw
`ArgumentException`. Validate ASCII MIME-token characters and the single slash; do not add a MIME
sniffer, root-name inference or feature option. The internal constructor accepts trusted document
metadata (including the template-document combination below).

`CharacterSet` is `UTF-8` for these factories and all current string parser entries. A string is already
decoded; XML declaration encoding and HTML meta charset do not re-decode it or change this metadata.
Do not add a public encoding setter/selection flag. A future byte decoder must supply actual encoding
through a reviewed internal creation seam before claiming byte-input support. Browser aliases
`charset` and `inputEncoding` read the same property. Document cloning preserves Kind, ContentType and
CharacterSet; import/adoption preserve destination document metadata, not source-document metadata.

`CreateElement(name)` lowercases only for Kind Html. Its namespace is HTML when Kind Html **or**
ContentType equals `application/xhtml+xml`; otherwise null. `CreateAttribute` lowercasing remains
Kind-Html-only. `CreateElementNS` remains explicit. Thus XHTML `CreateElement("FOO")` yields HTML
namespace/local name `FOO`, and `CreateElement("template")` can acquire template contents below.
These are [DOM document/createElement rules](https://dom.spec.whatwg.org/#dom-document-createelement).

XML owner uses `CreateXml()` for ParseXml and `CreateXml("image/svg+xml")` for strict ParseSvg. Reserve
one internal parser seam in `Xml/XmlDocumentParser.cs`:

```csharp
internal static Document Parse(string source, Document document,
    XmlParseOptions? options, CancellationToken cancellationToken);
```

It requires a fresh, empty XML document supplied by its caller, builds it without changing metadata,
and returns that same identity; reject a nonempty/non-XML target before mutation. This is not a public
parse-into-existing-tree API. Public facades create the document and call it. B4 creates the destination
with the caller/response MIME essence before parsing, preserving `image/svg+xml` generic XML semantics.
The integration owner grants signed friend access to Browser when B1/B4 actually consumes internal
parser/DOM host seams; neither feature task edits signing files independently. No public MIME option
or temporary mutable setter is needed to let the current XML facade finish.

Acceptance: default/overloaded metadata, invalid input, all four DOMParser MIME essences and custom
`+xml`, XHTML case/namespace versus HTML/XML, encoding declarations on string input, clone/import
metadata, and the parser seam's preconditions once XML integrates. This commit need not wait for D3b.

## D3b: explicit adoption and replace-all

Native owner reserves `Dom/Node.cs`, `Document.cs`, a private helper if needed, and
`Jint.Tests.HtmlParser/AdoptionTests.cs`/`ReplacementTests.cs`. Add only these working public members:

```csharp
// Document
public Node AdoptNode(Node node);
// Node: a single replacement node/fragment, or clear; not a variadic conversion API.
public void ReplaceChildren(Node? replacement = null);
```

AdoptNode returns the same identity, removes an attached node even in the same document, and changes
ownership of the full subtree and attached attributes when documents differ. Reject Document with
NotSupportedError before changes; null is ArgumentNullException. Preserve spelling, data and identities;
do not round-trip through destination factories. Use an explicit traversal stack. Attr is a separate
native type: this task updates attributes owned by adopted elements, without inventing an Attr-as-Node
hierarchy. Independent attribute adoption is assigned with its binding contract when needed.

Preserve these distinctions from [DOM insertion/replacement](https://dom.spec.whatwg.org/#concept-node-insert):

| Operation with a foreign fragment | Fragment OwnerDocument | Children |
| --- | --- | --- |
| AppendChild / InsertBefore | Unchanged, including empty fragment | Drained and adopted into destination |
| ReplaceChild | Fragment itself adopted, even when empty | Drained and adopted into destination |
| ReplaceChildren (replace-all insertion) | Unchanged, including empty fragment | Drained and adopted into destination |
| Document.AdoptNode(fragment) | Adopted, including empty fragment | Stay inside fragment; all descendants adopted |

ReplaceChildren validates receiver (Document/Element/DocumentFragment), cycles and the complete final
Document child shape before removing anything or adopting source nodes. Its public safety wrapper is
necessary because the spec's internal replace-all assumes caller validation. Null clears; empty fragment
clears while retaining its own document. Snapshot incoming children/order before mutation. Handle a
replacement already within the target, including a descendant of a removed child. Preserve source and
destination on hierarchy/not-found validation failure. Allocation failure is not a transaction promise.

Then perform common removal/insertion algorithms with replace-all record suppression semantics, not
repeated public ReplaceChild calls. D5 produces the aggregate target record; suppression never disables
range/intrinsic/transient-registration work. Same-node InsertBefore/ReplaceChild must follow algorithm
steps even where final links are unchanged; an identity shortcut cannot erase required D5 records.
Ownership changes alone are not a new MutationRecord kind. Adoption removal uses its normal child-list
record and internal semantic hooks. No Browser callback/JS execution is introduced in this task.

Tests cover every existing native node kind, same/foreign document, attached/detached input, attached
attribute identity, deep chains, nonempty/empty fragments for every row above, descendant replacement,
document element/doctype order, invalid cross-document input leaving both trees and owners unchanged,
and later D5 record integration. Do not mark template adoption complete before the next commit.

## Native template contents: required before full HTML/XML facades

The same native owner then extends `Element`, `Document`, `DocumentFragment`, Node's adoption/cycle
logic and `NodeCloner`; new focused template tests stay separate. Exact native surface/seams:

```csharp
// Element: null for anything except HTML-namespace, lowercase-local-name template.
public DocumentFragment? TemplateContent { get; }
// Document, internal
internal Document GetTemplateContentsOwnerDocument();
// DocumentFragment, internal; null for ordinary fragments.
internal Element? Host { get; }
```

Keep Element sealed; this intrinsic state does not require a public subtype family. Matching is exact
namespace/local name, even in XML. Create one stable hosted fragment when the matching element is
created, including through clone internals. A normal document lazily creates one inert owner document;
all its templates share that owner. A template created in an inert owner reuses that same document.
Follow [HTML template ownership](https://html.spec.whatwg.org/multipage/scripting.html#appropriate-template-contents-owner-document):
the inert owner has the outer document's Kind, default content type `application/xml` and UTF-8 encoding;
it is not a clone of the outer document's MIME metadata. It has no browser host, tree or observers.

TemplateContent is not an ordinary child: its ParentNode remains null, it does not change ChildCount,
and ordinary ancestor walks/observation do not cross Host. Cycle checks **do** follow host-including
ancestry, so inserting a template into its own content (including nested hosts) fails before mutation.
Public AppendChild on a template still creates an ordinary child. Only the appropriate parser/template
algorithms redirect into TemplateContent; do not put unconditional redirection in Node insertion.

On template adoption, retain the fragment identity and adopt it plus its contents into the new outer
document's appropriate inert owner. Ordinary children still adopt into the new outer document. Nested
templates use the same iterative work traversal, not recursive adoption calls. Deep clone/import clones
both ordinary children and template contents into their proper destination owners; shallow clone makes
an empty, distinct hosted fragment. Cloning a fragment directly creates an ordinary unhosted fragment.
No insertion marker, declarative shadow root or content-patching implementation is smuggled into this
ownership task; those parser features consume this foundation in their own algorithm tasks.

Acceptance includes HTML and XHTML templates, SVG/no-namespace and uppercase XML lookalikes,
SameObject content, shared/reused inert owner, ordinary-child versus content insertion, ancestor-cycle
rejection, explicit adoption and all fragment insertion rows, nested deep clone/import, shallow clone,
and no source mutation. XML/HTML owners then add tests proving parser children reach this destination.
Template ownership is a concrete prerequisite of completed X1/H7, not a deferred optional capability.

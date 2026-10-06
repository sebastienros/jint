# Creation-time element `is` metadata for X3c

Independently reviewed contract, 2026-09-25; implementation deferred at the requested pause. This finite native prerequisite supplies HTML
serialization with creation-time metadata. It does not implement custom-element definitions,
upgrade, constructors, reactions, or a Browser migration. No public API promotion is required.

## Sources and boundary

[DOM element creation](https://dom.spec.whatwg.org/#concept-create-element) takes a nullable
`is` argument; the ordinary no-definition branch stores it, including for non-HTML namespaces.
The algorithm does not validate that argument as a custom-element name. Preserve null versus
empty and the exact string; do not infer registration or normalize it. Future autonomous-element
construction selects null before native allocation; that Browser algorithm is outside this task.
[DOM cloning](https://dom.spec.whatwg.org/#concept-node-clone) passes the source slot into creation.
Adoption changes ownership without reconstructing the element.

[HTML token creation](https://html.spec.whatwg.org/multipage/parsing.html#create-an-element-for-the-token)
uses the creation token's `is` attribute before installing attributes. Formatting recreation uses
its saved creation token. [XML's DOM mapping](https://html.spec.whatwg.org/multipage/xhtml.html#parsing-xhtml-documents)
requires equivalent creation from XML data. Thus XML parsing also captures a no-namespace,
case-sensitive `is`, including a processed DTD default; it is not restricted to XHTML elements.
[HTML serialization](https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments)
consults the slot to emit a synthetic escaped `is` only when the corresponding attribute is absent.
XML serialization does not synthesize it. Attribute edits never redefine the creation slot.

Current `CustomElementRecord.IsValue` and `CustomElementRegistry.CarryIsValues` demonstrate Browser's
separate historical storage. Its attribute fallback and registry-dependent creation fast path are
not the native contract. This task reads no Browser record, calls no Browser hook, and adds no global
table. Browser migration must later supply the creation argument even without registered definitions.

## Exact native seam

In `Dom/Element.cs` add `internal string? IsValue { get; }`, assigned by the existing constructor:

```csharp
internal Element(Document owner, string? namespaceUri, string localName,
    string? prefix, string? isValue = null)
```

No initializer, setter, lazy attribute lookup, second state object, or registry coupling. A single
immutable string reference is sufficient. Construction publishes no mutation record/version change
for this slot. Existing ordinary attributes still generate their existing notifications.

In `Dom/Document.cs`, keep every existing public signature and add internal overloads:

```csharp
internal Element CreateElement(string localName, string? isValue)
internal Element CreateElementNS(string? namespaceUri, string qualifiedName, string? isValue)
internal Element CreateParsedElement(string? namespaceUri, string localName,
    string? prefix, string? isValue = null)
```

Public factories delegate with null. Internal validated overloads share exactly the existing name
validation, HTML case conversion, and namespace choice; they only pass the extra constructor argument.
The parser factory retains its trusted validated-name contract. Neither overload adds an `Attr`.
Do not impose HTML-namespace, document-kind, hyphen, case, or nonempty restrictions on `isValue`.
No public creation-options type or custom-element registry implementation is introduced.

`NodeCloner.CopySingle` passes `original.IsValue` to its element constructor before its existing
attribute copy. This covers shallow/deep clone and import, template contents and clonable shadows
through their existing frames. Preserve registry selection, DTD-ID attribute flags and document
XML provenance. No extra traversal is needed. `Node.Adopt` requires no change: the same element
retains its immutable slot while the existing owner/template/shadow/attribute traversal runs.

## Parser integration, without a new attribute scan

In `HtmlTreeBuilder.cs`, capture `_preparedIsValue` while `PrepareTokenAttributes` already visits
accepted tokenizer attributes. Reset it in `SetToken`, set it for exact name `is`, and pass it through
`InsertTokenElement` into a trailing optional `isValue` parameter on `InsertElement`. Tokenizer name
folding and duplicate elimination already determine the winning value. Implied nodes call the same
helper with null; processing or merging a later `html`/`body` token must never set an existing slot.

In `HtmlTreeBuilder.Formatting.cs`, reconstruction passes `entry.Element.IsValue` explicitly to
`InsertElement`. In `HtmlTreeBuilder.AdoptionAgency.cs`, `CreateFromFormattingEntry` passes the same
slot into the parser factory. This is safe because it is immutable and every replacement carries it;
no extra formatting-entry field is needed. Continue using saved attributes for recreated attributes,
not the live attribute list. Do not modify equivalence keys or the Noah's Ark comparison.

Audit all current `CreateParsedElement` call sites before completion. Any foreign-content creation
added by the active H7 owner must pass its prepared token value too, without namespace filtering.
A synthetic fragment root uses null. Coordinate that small call-site change with its file owner;
do not duplicate or rewrite foreign/template insertion algorithms.

In `XmlTreeParser.cs`, collect `isValue` in the existing namespace-resolution/duplicate-check loop
that builds `parsedAttributes`, after `ApplyDtdAttributes`. Select null-namespace local name `is`;
`IS`, `p:is`, and `xmlns:is` do not qualify. Allocate the element after that loop and pass the slot
before `InitializeParsedAttributes`/`AppendParsedChild`. Preserve `CurrentDocument` (including the
inert template owner), input offsets, namespace bindings, default normalization, `IsDtdId`, limits
and cancellation. This adds no scan of raw source or live attributes and no external-DTD access.

The extra work is constant per existing attribute visit or element creation. Reuse immutable owned
strings; do not flatten/copy them or add token-length charging for an already owned reference.
Existing cooperative work accounting and polling remain; no new unbounded loop is justified.

## Finite dispatch and gates

Dispatch two ordered implementation commits with separate file ownership:

1. The native/XML prerequisite owns only `Dom/Element.cs`, `Dom/Document.cs`,
   `Dom/NodeCloner.cs`, and `Xml/XmlTreeParser.cs` under `Jint.HtmlParser/`, plus
   `Jint.Tests.HtmlParser/ElementIsValueTests.cs`. Implement the real immutable slot, validated and
   trusted factories, clone/import preservation, and complete XML capture together. This commit
   does not edit TreeConstruction or claim that HTML capture or the full X3c prerequisite is complete.
2. After that reviewed signature is integrated, the existing H6e owner supplies a separate HTML
   capture/recreation commit owning `Html/TreeConstruction/HtmlTreeBuilder.cs`,
   `HtmlTreeBuilder.Formatting.cs`, `HtmlTreeBuilder.AdoptionAgency.cs`, and
   `Jint.Tests.HtmlParser/Html/TreeConstruction/HtmlIsValueTests.cs`. It implements the token capture,
   implied-node/merge behavior, reconstruction, and adoption-agency propagation specified above.
   Keep this metadata commit separate from frameset behavior; no second worker edits TreeConstruction
   concurrently. If H6e has completed before this handoff, explicitly transfer those files to the
   HTML integration owner before dispatch.

If H7 introduces a separate allocation site, coordinate its one argument explicitly with that owner;
the foreign/fragment family cannot be completed with missing creation metadata. No incomplete factory
stub, live-attribute substitute, or public metadata claim is permitted in either prerequisite commit.
Neither commit edits `Node.cs`, Attr, form state, slot assignment, shadow algorithms, Browser,
serializer or API snapshots. Preserve every existing insertion/attribute/clone/adoption hook and
source annotation.

Required literal assertions, with native/factory/identity/XML cases in `ElementIsValueTests.cs` and
token/reconstruction/adoption-agency cases in the H6e-owned `HtmlIsValueTests.cs`:

- Validated and parsed factories retain null, empty, mixed-case and non-name strings, without creating
  attributes. HTML, XML, SVG and no-namespace elements behave consistently; existing name failures stay.
- Setting/replacing/removing/reattaching `is` attributes preserves the slot; ordinary factory plus
  later `is` attribute stays null. Attribute mutation records retain their original counts/content.
- Clone/import preserve slot and independently copy attributes even when the two values disagree or
  the attribute was removed. Cover shallow/deep, XML-to-HTML import, template contents and clonable
  shadow descendants. Adoption retains identity/slot across documents, including hosted descendants.
- HTML start tokens capture decoded values, first duplicate wins, case folding, empty/missing values;
  implied roots remain null after later attribute merging. Reconstruction and adoption-agency
  replacement retain the saved slot after pausing and changing/removing the original live attribute.
  Use the existing session seam, quotas 1/3/large and representative every-chunk-split fixtures.
- XML document and fragment inputs capture explicit/entity-expanded/DTD-defaulted unprefixed `is`,
  including under template contents; prefixed/case variants remain null. Omitted unread defaults must
  not manufacture a slot. Assert unchanged source offsets, omissions and DTD-ID metadata where relevant.

Run each commit's tests and affected suites in fresh Release on net8/net10; the first commit covers
native/XML suites, and the second covers HTML plus the integrated metadata regressions. No timings.
The complete prerequisite and X3c completion gate require both reviewed commits integrated: supported
parse paths must capture the slot and native identity operations must retain it. The first commit
alone does not release that gate. X3c's independent HTML walker adds synthetic-is output, escaping,
attribute-absence/ordering tests and XML-no-synthesis controls. Larger textarea state and Browser
custom-element migration remain separately owned; this metadata task supplies no success stubs for them.

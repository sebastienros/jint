# XML and SVG implementation dispatch

Decision supplement to [the architecture](html-parser.md), 2026-09-23. This resolves the X1 backend
spike and scopes X1/X2 on the native DOM. It does not change the active A2 shared-contract assignment.

## Backend decision and evidence

Use an owned XML scanner and direct native tree builder. Keep `System.Xml.XmlReader` as a differential
test reference for its supported intersection, not a production dependency or a conformance oracle.
This is a contract decision, not a measured performance claim. No benchmark was run for this decision.

`dotnet-inspect` inspected `XmlReaderSettings` in the installed .NET 8.0.30 and 10.0.11 reference packs.
Both expose document/fragment conformance, DTD processing, whitespace preservation, resolver control,
and document/entity character quotas. These are useful capabilities, but do not close these gaps:

| Requirement | XmlReader frontend assessment |
| --- | --- |
| Direct native tree | Feasible: consume events without `XmlDocument`/`XDocument`. |
| Namespaces, CDATA, PI, internal DTD | Substantial existing implementation; strongest reason to reuse it. |
| Original UTF-16 token limits | No raw token boundary/length API; decoded values and reader buffering cannot implement A2's count. An additional lexical scanner is necessary. |
| Cancellation during entity expansion | `Read()` has no cancellation argument. A cancellable TextReader checks refills, not all buffered expansion work. |
| Distinct budget failure | Entity quotas throw ordinary `XmlException`; public fields do not identify that failure independently of localized message text. |
| Current XML names | Microsoft documents fourth-edition XML conformance; fifth-edition name cases require independent verification, including in native DOM factories. |

The [reader API](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreader.create?view=net-10.0)
describes normalization, expansion and conformance. The
[.NET 8 implementation](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlTextReaderImpl.cs)
shows entity-quota failure through `XmlException` in `RegisterConsumedCharacters`. Do not classify it
by `Message`, HResult, private reflection or a second unbounded parse. `MaxCharactersInDocument` is
also not A2's original-input count. Choosing a scanner avoids maintaining a source guard and a second
parser with different positions and failure policies.

## Existing browser obligations

The integration tree was inspected at `3ea5d878c`; the native foundation API was inspected from
`5f7c3a854`, whose correctness fixes are owned separately. Relevant evidence is in
`Jint.Tests.Browser/Views/DomParserTests.cs`, `Parsing/XmlDocumentLoadTests.cs`,
`Dom/DocumentCreationTests.cs`, `Jint.Browser/Dom/Views/JsDomParser.cs` and
`Jint.Browser/Runtime/Parsing/PageDocumentFactory.cs`.

- All four XML MIME types accept a well-formed `<root/>`, including `image/svg+xml`. Browser DOMParser
  must call generic XML parsing and retain the requested MIME type. It must not call strict `ParseSvg`.
- XML/XHTML navigation and frames preserve namespaces and case. XHTML `createElement("FOO")` uses the
  HTML namespace without lowercasing. Whitespace following its root does not become body content.
- Detached documents report UTF-8 and have no browsing location. Input is already decoded text;
  declaration `encoding` does not reinterpret its UTF-16 characters.
- Malformed DOMParser input yields a document containing a nonempty Mozilla-namespace `parsererror`,
  without reporting a page script error. Direct parser APIs throw instead.
- Existing XML navigation scripts are inert. Keep that documented browser divergence during migration;
  adding XML script execution is separate browser work. Native parsing never fetches or executes.

## Public boundary, assigned through the shared owner

The final facade remains in namespace `Jint.HtmlParser`, using the existing partial `MarkupParser`:

```csharp
public static Document ParseXml(string source, XmlParseOptions? options = null,
    CancellationToken cancellationToken = default);
public static DocumentFragment ParseXmlFragment(string source, Element context,
    XmlParseOptions? options = null, CancellationToken cancellationToken = default);
public static Document ParseSvg(string source, XmlParseOptions? options = null,
    CancellationToken cancellationToken = default);

public sealed class XmlParseOptions
{
    public ParseLimits Limits { get; init; } = ParseLimits.Unbounded;
}

public sealed class MarkupParseException : Exception
{
    public string Code { get; }
    public long Offset { get; }
    internal MarkupParseException(string code, long offset);
}
```

Options have a public parameterless constructor and reject null `Limits`. No strictness flag, resolver,
DTD switch, MIME selector, diagnostic collector or public backend selection is needed. XML has fatal
syntax errors; a recoverable-diagnostic channel adds no capability here. `Code` is a stable `xml/…`
identifier and `Offset` is an original-input UTF-16 position, EOF at input length. For an error within
replacement text, use the outermost invoking reference's source position; do not pretend it is an
exact position inside the original string. Messages describe the error without including the input.

Required initial codes: `xml/unexpected-eof`, `xml/invalid-character`, `xml/invalid-name`,
`xml/invalid-declaration`, `xml/invalid-markup`, `xml/mismatched-end-tag`, `xml/duplicate-attribute`,
`xml/namespace-error`, `xml/undeclared-entity`, `xml/recursive-entity`, `xml/invalid-document`, and
`xml/svg-root-required`. Additional DTD-specific codes may be added with fixtures. No English message
matching and no catch-all conversion. Null arguments are argument failures; cancellation remains
`OperationCanceledException`; shared budget failures remain `ParseLimitException`.

Do not edit A2 files in the XML feature task. The shared owner adds the two types above and, before
DTD completion, the one required budget extension:

```csharp
// Addition to ParseLimits; same nonnegative/zero-unbounded rules as its other members.
public long MaxEntityExpansionCharacters { get; init; } // default 0
// Addition to ParseLimitKind:
EntityExpansionCharacters
```

This is a required X1 follow-up contract, not an instruction to interrupt the active A2 commit. There
is no hidden fixed entity limit. Browser chooses finite values when its integration policy is assigned;
standalone `Unbounded` remains unbounded. Do not map Browser's finished-document `MaxDomNodes` to this.

## Semantics and limits

Implement XML 1.0 fifth-edition well-formedness and Namespaces in XML 1.0 third-edition rules, using
explicit stacks. Parse declarations, exact names, start/end tags, attributes, comments, PI, CDATA and
text. Enforce document shape, duplicate expanded attribute names, namespace reservations and legal
characters. Normalize XML line endings and attribute values at their specified stages; preserve case,
attribute order, CDATA node kind and comments/PI. XML declaration is metadata, not a PI node. Reject
unsupported XML versions. The nonvalidating parser does not enforce a DTD content model or schema.
See [XML](https://www.w3.org/TR/xml/) and [Namespaces](https://www.w3.org/TR/xml-names/).

Build native nodes only. A successful document has one element; document-level XML whitespace is not
stored as forbidden Text children or moved inside the root. A fragment permits text and multiple roots.
Empty document/SVG input is a syntax failure; empty fragment succeeds; `Document.CreateXml()` still
creates a valid empty DOM container. Do not publish a partial document when parsing fails.

Fragments inherit namespace bindings from the current DOM context, including its element namespace,
ancestor declarations, shadowing and explicit default-namespace reset. Prefixes are case-sensitive;
unprefixed attributes remain outside the default namespace. Build detached children owned by the
context's document; never mutate the context or copy its attributes. An equivalent virtual context
frame avoids constructing/escaping synthetic markup. Reject declarations, doctypes and unmatched
closing tags in fragment input. A fragment does **not** inherit its document's DTD/entities; only the
predefined XML entities and character references are available. This follows the
[XML fragment algorithm](https://html.spec.whatwg.org/multipage/xhtml.html#parsing-xhtml-fragments).

Limits extend A2's definitions without changing them:

- Input counts the supplied string once before normalization. Check length before constructing nodes.
- Token counts lexical source units including delimiters: each tag, comment, PI, CDATA section,
  declaration and reference. A doctype is one token, including its subset. Plain text grouping is not
  a token bound. Scan incrementally; do not allocate a complete oversized token before checking.
- Nesting counts open element frames, first source element at 1; the virtual fragment context is not
  charged. Check before push. Entity replacement containing markup contributes to this same depth.
- Expansion counts replacement-text UTF-16 units consumed on each general/parameter entity invocation,
  including nested invocations. Charge before consuming; repeated references charge repeatedly. Raw
  source input and expansion work are separate. Predefined/numeric character references do not charge
  the DTD expansion budget. Use overflow-safe counters and report first measured exceedance.
- Poll cancellation at entry, before return, and at a bounded work cadence across every scanning,
  name/value copying and entity-processing loop (initial implementation: at most 4096 work units).
  Each consumed/copied UTF-16 unit and entity-stack transition is work. Use explicit entity stacks
  and cycle detection; no recursion or refill-only polling. This is cooperative cancellation, not a
  wall-clock deadline guarantee.

DTD handling is necessary X1 work. Preserve doctype name/public/system identifiers. Process the internal
subset's general/parameter entities, attribute declarations/defaults/normalization and declaration
syntax under the nonvalidating rules. Keep source-order specified attributes before supplied defaults.
Never resolve arbitrary file/network entities. The known public identifiers in the HTML XML section
use a pinned local named-entity catalog; unknown external subsets/entities are not fetched. Their
skip/undeclared-reference outcomes must follow XML's nonvalidating rules, with fixtures for standalone
and external-subset cases. Do not substitute a blanket `DtdProcessing.Prohibit` policy.

The catalog contains only declared character entities, not a validating XHTML DTD. Pin the upstream
entity data and license, generate deterministically, and share authoritative data with H2 when practical;
XML still requires semicolons and has no HTML ambiguous-ampersand recovery. The catalog requirement
comes from [HTML XML parsing](https://html.spec.whatwg.org/multipage/xhtml.html#parsing-xhtml-documents).

## Native DOM and browser seams

The foundation owner supplies namespace-aware factory correctness before XML integration. Do not work
around rejected legal names by silently changing them or bypassing node invariants. Fifth-edition name
fixtures must exercise both scanner and DOM creation; the existing `XmlConvert` use needs this audit.

The shared DOM owner also supplies document content-type/encoding metadata consumed by Browser and
case-preserving XHTML element creation. Default standalone XML type is `application/xml`; strict SVG
is `image/svg+xml`. Browser applies the caller/response type through its internal creation path, before
nodes are built. Do not infer type from an `html`/`svg` root, and do not expose a MIME option just to wire
Browser. XML declaration metadata may be retained internally; no new public declaration API is needed.

Parsing uses native insertion/attribute primitives so intrinsic element state and mutation invariants
are maintained. Detached parsing schedules no host event. XHTML template children require the native
template-content destination once that intrinsic exists; coordinate that seam with the HTML/native
owner, without creating a parallel template implementation. Missing intrinsic support blocks final X1
completion, not development of the scanner. Browser still owns reactions, wrappers and delivery.

Browser catches only `MarkupParseException` when constructing its established parsererror document,
with the requested content type and inert UTF-8 metadata. Error text is assigned as text, never parsed
as markup. It must not convert cancellation, `ParseLimitException`, programming exceptions or allocation
failures into successful parsererror documents. XML fragment bindings translate syntax errors to their
required DOM exception, without changing the standalone exception contract.

`ParseSvg` calls the XML document core and requires root local name `svg` and exact SVG namespace.
It does not inject that namespace, repair HTML-style markup or add typed SVG state. Prefixed SVG roots
are valid. Inline SVG and HTML foreign-content recovery remain HTML work. The facade/root-check part
of X2 can land with X1; completing X2's typed SVG/browser inventory still depends on the native HTML
and binding work listed in the architecture.

## Finite implementation assignment

Assign one Sol owner the XML directory and tests. Reserve `Jint.HtmlParser/Xml/**`,
`Jint.Tests.HtmlParser/Xml/**`, and the new `Jint.HtmlParser/MarkupParser.Xml.cs`. No project/shared-type,
DOM-foundation, HTML-tokenizer, CSS or Browser production edits in this assignment.

1. **X1 core commit:** internal source cursor/scanner, namespace stack and direct tree builder for
   document/fragment markup without a DTD; predefined/numeric references; all source limits and
   cancellation. Add explicit unsupported-DTD handling internally. Exercise native creation and
   fragment ownership. This is a reviewable internal milestone, not completed X1; no public facade
   or claim of full XML support yet.
2. **X1 DTD commit:** internal subset/declaration processing, expansion stack/cycle and work accounting,
   defaults, pinned external catalog and nonvalidating external-reference behavior. Remove the
   temporary DTD rejection. Merge the shared options/error/expansion-budget and native metadata seams.
   Add the three real public methods, including the small strict SVG check. No placeholder APIs.
3. **Integration handoff:** report fixtures, remaining native-template/SVG-state dependencies, and exact
   Release results. X1 stays open until its intrinsic/native seams and full required cases work;
   AngleSharp.Xml removal and Browser error mapping are the later B4 owner's change.

Acceptance fixtures cover nested/prefixed/default-reset namespaces; duplicate expanded attributes;
XML fifth-edition and supplementary names; CR/CRLF and attribute/reference normalization; comments,
CDATA, PI and declaration placement; empty/multiple roots; fragment context shadowing, text and no
DTD inheritance; internal/nested/recursive/parameter entities; defaulted attributes; external subsets
and the pinned catalog; strict SVG roots and generic XML roots; malformed entities/declarations; and
input/token/depth/expansion limits at boundary and one past it. Include deterministic cancellation
checks within a long lexical token and expansion work, using an internal test seam instead of a
wall-clock speed assertion. Assert no context mutation on fragment failure and no file/network access.

Run `dotnet test Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net8.0` and the same
command for `net10.0`, always rebuilding. Use the coordinator's actual test-project name if changed.
No benchmarks during parallel builds, and no performance claim from these functional tests. Before
package sign-off, add licensed upstream XML/namespace conformance fixtures and browser comparisons
where XML-to-DOM behavior is underspecified; BCL agreement alone is insufficient.

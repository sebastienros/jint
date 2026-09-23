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
DTD switch, MIME selector, diagnostic collector or public backend selection is needed. Skipped entities
are always reported in immutable document metadata below; they are not optional diagnostics or syntax
failures. `Code` is a stable `xml/…`
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
attribute order, CDATA node kind and comments/PI. XML declaration is metadata, not a PI node. Validate
its version using the fifth-edition rule below. The nonvalidating parser does not enforce a DTD content
model or schema.
See [XML](https://www.w3.org/TR/xml/) and [Namespaces](https://www.w3.org/TR/xml-names/).

**Version correction from the full corpus:** [XML §2.8](https://www.w3.org/TR/xml/#sec-prolog-dtd)
defines `VersionNum` as ASCII `1.` followed by one or more ASCII digits. Accept `1.0`, `1.1`, `1.7`,
`1.00` and other matching strings, processing all of them with this implementation's XML 1.0 rules.
Do not parse the suffix as an integer, cap its numeric value, or switch grammar based on it. Scanning
still observes token limits and cancellation. Reject `1.`, `2.0`, `01.0`, signs, whitespace within the
value, non-ASCII digits and trailing characters through the existing declaration diagnostic.
This replaces the earlier blanket instruction to reject unsupported versions.

Accepting a `1.1` declaration does **not** implement XML 1.1. Under this XML 1.0 processor, `&#x1;`
remains illegal, and literal NEL/U+2028 remain ordinary characters rather than additional normalized
line endings. Keep declaration ordering, quoting and encoding/standalone checks. Declaration-free
documents also use XML 1.0. Preserve any retained declaration metadata without inventing a public API.
Regression coverage includes `eduni/errata-4e/errata4e.xml#x-rmt-008b`, the version boundary strings,
and the `1.1` character/line-ending distinctions. Do not exclude that valid upstream case or change
its expectation to accommodate the former `version != "1.0"` implementation.

Build native nodes only. A successful document has one element; document-level XML whitespace is not
stored as forbidden Text children or moved inside the root. A fragment permits text and multiple roots.
Empty document/SVG input is a syntax failure; empty fragment succeeds; `Document.CreateXml()` still
creates a valid empty DOM container. Do not publish a partial document when parsing fails.
Use the [trusted construction seam](html-parser-construction.md) for fresh validated nodes/attributes:
public mutation/factory calls both repeat scans and impose DOM-API restrictions distinct from XML
parsing. The XML scanner still owns well-formedness, namespace, duplicate, shape and limit checks.

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
use a pinned local named-entity catalog. Do not substitute a blanket `DtdProcessing.Prohibit` policy.

### Observable no-fetch entity handling

[XML §4.4.3](https://www.w3.org/TR/xml/#include-if-valid) requires notification when an external parsed
entity is recognized but not read; §4.4.8 covers parameter-entity inclusion. Preserve nonvalidating
no-fetch parsing and report omissions on the returned document. Do not turn skipped content into a
syntax error. [WHATWG XML parsing](https://html.spec.whatwg.org/multipage/xhtml.html#parsing-xhtml-documents)
provides the recognized local-catalog policy; other external content is not retrieved.

The shared/native owner adds this exact public result surface in namespace `Jint.HtmlParser`, alongside
the XML options/errors follow-up. The enum/struct belong in `Parsing/XmlSkippedEntity.cs`; the property
and internal publication seam belong to the existing `Dom/Document.cs`. XML implementation owns neither
file and must coordinate this prerequisite rather than add a second result wrapper:

```csharp
public enum XmlSkippedEntityKind { General, Parameter, ExternalSubset }

public readonly struct XmlSkippedEntity
{
    public XmlSkippedEntityKind Kind { get; }
    public string Name { get; }
    public string? PublicId { get; }
    public string? SystemId { get; }
    public long Offset { get; }
    internal XmlSkippedEntity(XmlSkippedEntityKind kind, string name,
        string? publicId, string? systemId, long offset);
}

// Addition to Document; no setter and never null.
public IReadOnlyList<XmlSkippedEntity> SkippedXmlEntities { get; }
```

`Name` omits `&`, `%` and `;`; it is empty for the external subset. Identifiers retain parsed declaration
values without URI resolution; null means unavailable/absent, distinct from a declared empty string.
A reference with no available declaration has both identifiers null. `Offset` is the original-input
UTF-16 position of `&`/`%`, or the doctype's `<` for its external subset. Nested replacement references
use the outermost source invocation position, as syntax errors do. Default struct values are safe:
General, empty Name, null identifiers, offset zero. Returned records are notifications, not tree nodes.

Record every skipped occurrence in processing order, including repeated references; do not silently
truncate or deduplicate. Record an unread external subset once when its processing is skipped. Merely
declaring an unused external entity adds no record. Read the recognized catalog locally and add no
skip record for it. An unresolved declaration is not claimed to be an external declaration: its null
identifiers explicitly indicate what the parser does not know.

The normal path has a null backing field and returns a shared immutable empty list. Allocate a builder
only at the first omission, reuse declaration strings, and freeze it once before returning the document.
Expose a genuinely read-only view, not a mutable array through `IReadOnlyList`; retain no builder,
source string, reader, resolver, callback or host object. No notification data is attached per node
or allocated for ordinary tokens.
There is no notification option or event. Limits/cancellation still cover processing repeated references.

This is immutable parse provenance: later DOM mutations do not rewrite it. Fresh/HTML documents are
empty; cloning a Document preserves the immutable snapshot (sharing is allowed), while importing or
adopting individual nodes does not transfer it. Fragment parsing cannot introduce a DTD and never
changes the owner's snapshot. Failure exposes no partial document. Browser keeps this native metadata
without adding a JavaScript property, Page.Errors entry, host callback or parsererror for an omission.

Behavioral rules:

- A recognized external parsed general/parameter reference without local content contributes no
  replacement characters and appends its record. Continue parsing. Known external references in
  attribute values remain a fatal XML error, including indirect references; omission is no recovery
  from well-formedness failures.
- An unread external subset is retained in doctype identifiers and recorded. Missing entity declarations
  are handled under XML's Entity Declared constraint: throw where it is a well-formedness requirement;
  where it is only a validity requirement, omit with a General/Parameter record and null identifiers.
  Do not invent declarations from the unread subset. Cover `standalone='yes'` separately.
- After an unread parameter entity, keep checking internal-subset well-formedness but stop processing
  subsequent entity/attribute-list declarations unless `standalone='yes'`, as XML §5.1 requires. This
  changes default attributes and normalization, so it needs explicit fixtures, not just entity text tests.

For `<!DOCTYPE r [<!ENTITY ext SYSTEM 'missing.xml'>]><r>a&ext;b</r>`, return text `ab` and one General
record named `ext` with SystemId `missing.xml` at `&ext;`. Removing the reference yields no record.
`<!DOCTYPE r SYSTEM 'missing.dtd'><r/>` returns one ExternalSubset record. Without a standalone-yes
constraint, adding `&unknown;` also records a General occurrence with null identifiers. The corresponding
undeclared reference without a DTD is fatal. Neither no-fetch example becomes a parsererror.

Tests must assert record order, repeated/nested references and offsets, parameter-entity declaration
processing, defaults before/after a skip, standalone conditions, namespace/attribute restrictions,
local catalog, immutable exposure, empty fast path and clone/import/adoption provenance. B4 adds
old/new Browser fixtures for these cases before deleting AngleSharp.Xml; any discovered discrepancy
is reviewed explicitly rather than silently narrowing accepted input. No performance claim is implied.

The catalog contains only declared character entities, not a validating XHTML DTD. Pin the upstream
entity data and license, generate deterministically, and share authoritative data with H2 when practical;
XML still requires semicolons and has no HTML ambiguous-ampersand recovery. The catalog requirement
comes from [HTML XML parsing](https://html.spec.whatwg.org/multipage/xhtml.html#parsing-xhtml-documents).

## Native DOM and browser seams

The foundation owner supplies namespace-aware factory correctness before XML integration. Do not work
around rejected legal names by silently changing them or bypassing node invariants. Fifth-edition name
fixtures must exercise both scanner and DOM creation; the existing `XmlConvert` use needs this audit.

### PI target correction and trusted construction

The full corpus exposed a concrete boundary defect: `ParseProcessingInstruction` accepts fifth-edition
names, then `CreateProcessingInstruction` reaches `XmlConvert.VerifyName` in `CharacterNodes.cs` and
rejects legal targets. `NodeCloner` reaches the same validation. The seven
`ibm-invalid-P89-ibm89n06.xml` through `ibm89n12.xml` cases in the errata catalog exercise U+0EC7,
U+3006, U+3030, U+3036, U+309C, U+309F and U+30FF; all are fifth-edition `NameStartChar` values.
The historical filenames do not override the selected catalog's current expected outcome.

The native owner supplies this concrete extension to [trusted construction](html-parser-construction.md):

```csharp
// Document: target and data already validated by the calling parser.
internal ProcessingInstruction CreateParsedProcessingInstruction(string target, string data);
```

It creates a fresh detached PI in the receiver's node document, preserving both owned strings exactly.
It does not re-run `XmlConvert`, XML name scans or delimiter scans, and invokes no host code. The XML
caller proves fifth-edition Name syntax, the namespace restriction forbidding a colon in a PI target,
the reserved case-insensitive `xml` exclusion, legal characters, PI termination and resource bounds
before the call. The existing parsed append operation performs actual insertion/bookkeeping. Any
intrinsic PI initialization must share native semantics rather than being bypassed; future unbounded
initialization needs the established cancellation/work protocol. No generic skip-validation boolean,
public unchecked factory, target rewriting, or catch-and-relabel of `DomException` is an alternative.

The trusted seam alone is not the complete fix. The native owner also replaces the public PI factory's
outdated validator with exact scalar-aware fifth-edition Name validation, including supplementary
characters and rejection of lone surrogates. The [DOM PI initialization rule](https://dom.spec.whatwg.org/#interface-processinginstruction)
uses **Name**, not QName/NCName: the public factory accepts colon-containing names and the name `xml`;
those additional XML parsing restrictions stay in the scanner. Empty/invalid names and data containing
`?>` still fail with the existing DOM exception; null argument behavior stays unchanged. Do not route
this through the newer, more permissive DOM element/attribute-local-name grammar.
This public validation correction is **PI-only**. Leave `QualifiedName.Parse`, element/attribute
factories, namespace reservation checks and doctype validation untouched. A narrowly scoped internal
XML-Name predicate may supply PI validation; do not redirect other public factories through it or
refactor the XML scanner as part of the native prerequisite.

Clone/import must retain these legal targets and exact data, including data subsequently edited to
contain `?>`; cloning existing node state is not a fresh public PI construction. Cover the seven scalar
regressions through public factory, XML document/fragment parsing and clone/import; also test U+10000
and U+EFFFF, rejected U+F0000/lone surrogates/invalid first characters, and public-versus-parser colon/
`xml` differences. Long targets remain cancellable in the XML scanner without a second hidden scan.
The clone path's proof is existing valid node identity/state, distinct from the parsed factory's
well-formed-input proof; share private allocation/storage as appropriate without pretending edited
clone data satisfies the parsed factory's preconditions. Keep ownership and mutation semantics intact.
Assign native changes first; the XML owner then consumes the seam and reruns the complete pinned corpus.

### Document metadata and integration

The shared DOM owner also supplies document content-type/encoding metadata consumed by Browser and
case-preserving XHTML element creation. Default standalone XML type is `application/xml`; strict SVG
is `image/svg+xml`. Browser applies the caller/response type through its internal creation path, before
nodes are built. Do not infer type from an `html`/`svg` root, and do not expose a MIME option just to wire
Browser. XML declaration metadata may be retained internally; no new public declaration API is needed.
The [native follow-up dispatch](html-parser-native-followups.md) now gives the exact metadata/factory
and fresh-document parser seam, followed by the concrete template-content ownership commit. These
prerequisites land before the complete XML facade, under the native owner rather than the XML task.

Parsing uses native insertion/attribute primitives so intrinsic element state and mutation invariants
are maintained. Detached parsing schedules no host event. XHTML template children require the native
template-content destination once that intrinsic exists; coordinate that seam with the HTML/native
owner, without creating a parallel template implementation. Missing intrinsic support blocks final X1
completion, not development of the scanner. Browser still owns reactions, wrappers and delivery.

Browser catches only `MarkupParseException` when constructing its established parsererror document,
with the requested content type and inert UTF-8 metadata. Error text is assigned as text, never parsed
as markup. Skipped-entity metadata is not a syntax failure. It must not convert cancellation,
`ParseLimitException`, programming exceptions or allocation
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
   defaults, pinned external catalog and immutable skipped-entity notifications. Remove the
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

Run `dotnet test --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release -f net8.0` and the same
command for `net10.0`, always rebuilding. Use the coordinator's actual test-project name if changed.
No benchmarks during parallel builds, and no performance claim from these functional tests. Before
package sign-off, add licensed upstream XML/namespace conformance fixtures and browser comparisons
where XML-to-DOM behavior is underspecified; BCL agreement alone is insufficient.

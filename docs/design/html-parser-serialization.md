# X3: native HTML and XML serialization

Design for independent review, 2026-09-23; integration inspected at `2d7a13c0d`.
This refines X3 in [the architecture](html-parser.md). Implement against the native tree, without
AngleSharp formatters, cloning, temporary parent insertion, reparsing, or a second DOM. Implementation
stages remain internal until the complete surface below is implemented. H7 supplies additional parsed
fixtures; native factories already support independent algorithm tests. This document changes no
existing Browser behavior and authorizes no shared native-file edits.

## Evidence and consumer decisions

The [A1 inventory](../../tools/html-parser-inventory/README.md) assigns serialization to B4 and leaf
callers to R4. Actual calls are:

| Caller | Current behavior | Migration requirement |
| --- | --- | --- |
| `DomHostHooks.GetInnerHtml` and `DomHtmlMarkupFormatter.InnerHtml` | Child serialization; templates use `Content`; HTML formatter even on XML-owned nodes | Preserve this deliberately tested compatibility behavior at initial migration, through explicit HTML entry points |
| `DomHostHooks.GetOuterHtml` | Element-inclusive HTML formatting | Same decision; setters remain fragment parsing plus mutation algorithms |
| `Views/JsDomParser.cs`, `JsXmlSerializer.SerializeToString` | `XhtmlMarkupFormatter`, through an `INode` argument | Replace with XML serialization only after exact-output differences and Attr argument conversion are reviewed |
| `Page.ContentAsync` | Entire document including doctype | Use document-inclusive HTML serialization on the page loop; retain XML-page behavior initially |
| `DevTools/DomDomain.cs`, outer markup | Element, or Document's document element only | Preserve its root-selection rule; do not accidentally add doctype |
| Collection/observer `ToHtml(TextWriter, IMarkupFormatter)` implementations | Interface adapters required by AngleSharp | They do not establish a standalone streaming requirement; remove with the replaced interfaces |

There is currently no `getHTML` binding in Browser or the binding override inventory. Its new binding
belongs to B4/B1 after X3's shadow path, not an assertion that today's Browser already has it.
`DOMParser` consumes strings and separately chooses HTML versus XML parsing; its inertness, MIME list,
and parsererror-document behavior remain parsing/binding contracts. A serializer never runs scripts,
custom-element reactions, event handlers, loaders, resolvers, observers, or fetches an external ID.

Regression anchors are `Jint.Tests.Browser/Dom/HtmlSerializationTests.cs` and
`Views/DomParserTests.cs`. They deliberately expect HTML formatting for XML markup getters, `&nbsp;`
there, and the old XML formatter's missing namespace declarations and empty-element spelling.
For example the current XMLSerializer result for a live HTML div/br omits the XHTML declaration.
The standard native XML algorithm must not inherit that omission. B4 must change such exact assertions
only with a documented standards correction, corresponding browser/WPT evidence, and reviewed consumer
impact. Do not add a public legacy formatter switch to make old assertions pass.

Primary algorithm references, checked for this design:

- [HTML fragment serialization](https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments)
  and its escaping algorithm (HTML Living Standard, 22 September 2026).
- [HTML serialization API selection](https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#html-serialization-methods),
  including getHTML, innerHTML and outerHTML.
- [DOM Parsing XML serialization](https://w3c.github.io/DOM-Parsing/#xml-serialization).
- [XML 1.0 fifth edition](https://www.w3.org/TR/xml/), for names, characters and lexical productions.
- [Upstream XMLSerializer tests](https://github.com/web-platform-tests/wpt/blob/master/domparsing/XMLSerializer-serializeToString.html),
  reviewed as primary test evidence, not yet vendored or pinned here. Known draft conflicts are below.

## Exact surface and format selection

Use `Jint.HtmlParser`. The following is the final proposed public surface; use internal equivalents
while implementing. Existing `MarkupParser` files are not serialization entry points.

```csharp
public static class MarkupSerializer
{
    public static string ToHtml(Node node, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default);
    public static string ToHtmlChildren(Node parent, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default);
    public static string ToXml(Node node, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default);
    public static string ToXml(Attr attribute, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default);
    public static string ToXmlChildren(Node parent, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default);
}
```

`HtmlSerializationOptions` is sealed and immutable: constructor arguments
`bool scriptingEnabled = false`, `bool serializableShadowRoots = false`,
`IEnumerable<ShadowRoot>? shadowRoots = null`; get-only properties `ScriptingEnabled`,
`SerializableShadowRoots`, `IReadOnlyList<ShadowRoot> ShadowRoots`. Snapshot the supplied roots into
private storage once, preserve identities, reject null entries, and deduplicate by identity. Expose a
read-only wrapper, not the backing array. Constructor enumeration is caller work, outside serializer
cancellation; enumeration exceptions propagate. No delegate or host is retained. Null options means
the shared all-false/empty default. The shadow type promotion dependency is explicit below.

`SerializationLimits` is sealed with `long MaxOutputCharacters { get; init; }`, default zero/unbounded;
negative values throw ArgumentOutOfRangeException. Null selects the all-zero instance. Add only
`SerializationLimitException : Exception` with get-only `long Limit` and `long Observed` and an
internal constructor; there is one limit, so no speculative limit-kind enum. It is distinct from
ParseLimitException: output expansion has neither an input offset nor an entity-expansion budget.

These inputs select the requested serialization operation/context; they do not enable optional
grammar modules. Do not add pretty printing, quote policies, prefix callbacks, namespace erasure,
attribute sorting, entity tables, sanitization, omission of comments, or parser feature switches.
Both formats are always implemented. `ToHtml` always means HTML, including on an XML-owned node;
`ToXml` always means XML, including SVG, MathML and HTML-owned nodes. ContentType/CharacterSet and
an element's namespace never choose the format. Browser decides its operation explicitly.

Whole-node methods include an Element's own tags, but Document/DocumentFragment have no wrapper and
emit their children. Whole Text/CDATA/Comment/PI/DocumentType are accepted. The XML Attr overload
returns the standard empty string, for attached or detached attributes; it is not an attribute-value
escaping helper. HTML has no Attr overload. Children methods accept only Element, Document and
DocumentFragment (including ShadowRoot); other kinds throw ArgumentException. Null arguments throw
ArgumentNullException. Empty output remains subject to entry/final cancellation checks.

For XML children, select the effective child list (template contents when applicable), then serialize
it as one synthetic fragment, without creating a native node. Each top-level child's namespace context
starts with the normal empty/default plus reserved xml binding; the generated-prefix counter is shared
across the call. Ancestors outside the serialized range contribute no declarations. The result must
be usable without their namespace context; necessary declarations are written into the output.
For HTML, a whole Text/CDATA node uses its real parent for raw-text treatment; detached text has no
raw-text parent. Children serialization retains the actual parent/context without moving children.

Return a .NET UTF-16 string. No XML declaration, byte-order mark, encoding conversion, newline
normalization, indentation, or platform newline substitution is introduced. No Stream/TextWriter or
async overload in X3: the actual callers consume strings. A future writer API needs a separate partial
output, exception, reentrancy and encoding contract; do not introduce it by routing through StringWriter.

## HTML implementation boundary

Implement the linked HTML algorithm with an explicit enter/children/exit stack. Choose local versus
qualified element/attribute spelling by the algorithm's namespace rules; keep native attribute order.
The HTML escaping kernel handles ampersand, NBSP, angle brackets, and quotes in attribute mode in one
pass. Do not HTML-normalize constructed names. Raw text is namespace-sensitive and uses the specified
parent names, with the noscript condition supplied by context. Void suppression includes obsolete
serialization-only names; SVG/MathML names that resemble HTML void/raw-text names are controls.
Template contents replace ordinary children. Serialize CDATA as the Text-interface case, comments
and PI literally with their delimiters, and HTML doctypes by name only. Do not insert an extra initial
LF for pre/textarea/listing. This is serialization, not repair of arbitrary DOM topology.

The implementation must transcribe the source's exact tag sets into reviewed internal tables; do not
reuse a tokenizer raw-text table or XML empty-element table whose sets differ. Test all members and
foreign-namespace controls from literal native trees. Persist the emitted closing-tag name on the
frame instead of recomputing or lowercasing it when returning from children.

The scripting input applies to the initial node document. Content traversed in a distinct template
contents owner document is inert and uses false. Attached shadow content normally shares the original
document's setting. A direct serialization of a template-content fragment uses the caller's specified
setting (Browser supplies false for inert owners); the serializer must not create an inert document
merely to discover its identity. Browser passes the actual target document's scripting state, not
the active window's state for every detached document. This is an escaping context, never permission
to execute or a reconstruction of how the node was originally parsed.

Custom built-in `is` state is a real prerequisite. Native Element currently stores registry identity
but no creation-time IsValue; Browser's `CustomElementRecord.IsValue` is separate from the live `is`
attribute. X3 must not infer a removed IsValue from the current attribute, call Browser, or invent a
global side table. Before completing HTML serialization, the native/custom-element owner must provide
an internal creation-time `string? IsValue` with create/clone/import/adopt semantics, consumed read-only
here. HTML writes the synthetic attribute only in the algorithm's absence case. Ordinary attribute
mutations must neither overwrite this slot nor duplicate the synthetic output. XML has no analogous
synthetic-is requirement.

## Shadow and hosted content

Ordinary calls omit attached shadow trees. Inclusion is the union of the serializable flag selection
and explicit root identity selection. Explicitly supplied closed roots are eligible; roots outside
the traversed host range do not get appended elsewhere. Reach a nested root only through content the
walk already includes. An explicit root does not automatically select its unlisted descendants.
Build one per-call identity set; never linearly scan the options list for every element.

Emit a selected root as a declarative template before its host's light children, with exact current
HTML attribute order/values: mode, delegates-focus, serializable, manual slot-assignment, clonable,
then the conditional custom-element-registry marker. Read the native fields; do not synthesize an
Element or mutate the host. Registry-marker selection compares the document/root null/global/scoped
states, not object inequality alone. The draft's shadow-host step names an out-of-scope `current node`;
interpret that step as the container whose contents are being serialized, as required by getHTML's
root-host case, and pin that interpretation with a direct host fixture.

A ShadowRoot passed directly emits only its contents, without wrapping itself or reentering its host;
its nested selected roots still work. Never flatten slots, walk assigned nodes, or cross upward to a
host. XML never includes attached roots through the HTML selection mechanism, but an explicit
ShadowRoot argument is serialized as a DocumentFragment. XML templates also use TemplateContent.

Current native storage already has AttachedShadowRoot, Host, Mode, DelegatesFocus, Serializable,
SlotAssignment, Clonable and registry identity. ShadowRoot and its acquisition APIs are still internal.
Public HtmlSerializationOptions must use the real ShadowRoot type, not accept arbitrary fragments,
opaque handles or a second wrapper. D6's narrow ShadowRoot identity/promotion is a publication
prerequisite, assigned to its owner; X3a/b/c do not edit or expose native shadow storage themselves.

## XML implementation and standards discrepancies

Implement DOM Parsing's element, namespace-recording, attribute, leaf and container subalgorithms;
the serializer owns its namespace environment and generated-prefix counter. The default namespace
starts empty and xml is predefined. Record the entire attribute list before choosing output names,
so a later declaration can affect an earlier attribute. Use current expanded names, including
defaulted attributes; prefix repair changes output only. Keep generated names on exit frames.
Templates use their actual content. XML declarations, internal DTD text, XmlNotations and
SkippedXmlEntities are not synthesized: those provenance snapshots are not DOM doctype content.

Preserve stable native attribute order with algorithm-inserted declarations at their prescribed
positions. Track duplicate expanded attribute names using a set, not a growing-prefix scan. Maintain
prefix-to-effective-URI lookup and URI-to-ordered-prefix candidates with reversible binding changes;
an element frame records an undo boundary. No dictionary copy per element and no ancestor rewalk per
attribute. On exit restore bindings, while the generated-prefix counter remains call-wide. Attribute
iteration must remain O(attribute count), without repeated Element.GetAttribute calls.

The XML writer has separate value escaping and scalar/name checks. Escape attribute TAB/LF/CR as
character references; do not reuse HTML escaping or emit named HTML entities for NBSP. Text, CDATA,
comments, PI, public/system identifiers and HTML-namespace empty elements each have distinct rules.
For example HTML void XML syntax uses the specified space before `/>`, non-HTML empty elements use
`/>`, and empty nonvoid HTML elements retain both tags. Do not use XmlWriter as an unspecified substitute.

`requireWellFormed` is the DOM Parsing algorithm argument, not a DTD validator. False is the
XMLSerializer operation; true requests its explicit error checks and is needed by standards-oriented
XML markup getters. Use `DomException("InvalidStateError", ...)` for serialization validity failures.
Never turn OperationCanceledException, SerializationLimitException, mutation failure or OOM into that
DOM error. Unknown native kinds/invalid CLR arguments stay argument errors. No catch-all around the
entire operation. Neither mode promises byte preservation or arbitrary serialize/reparse identity.

The referenced XML draft contains visible unresolved issues. Fix the following exact policy in tests
rather than choosing opportunistically during implementation:

| Issue | X3 decision |
| --- | --- |
| Attribute `>`: prose replacement list omits it, accompanying note and WPT require it | Emit `&gt;`; follow the primary test and note |
| Generated nsN can collide with existing names ([issue 44](https://github.com/w3c/DOM-Parsing/issues/44)) | Skip in-scope and locally reserved prefixes; never generate duplicate declaration names |
| URI-to-prefix history can select a prefix currently rebound to a different URI ([issue 45](https://github.com/w3c/DOM-Parsing/issues/45)) | Only reuse an effective binding to the required URI; otherwise generate one. Namespace preservation wins over that known inconsistent expected string |
| Null namespace/default declaration inconsistencies ([issue 47](https://github.com/w3c/DOM-Parsing/issues/47)) | Preserve the Element's actual namespace, reset the default when necessary, and omit redundant/conflicting declarations according to the repaired algorithm |
| requireWellFormed is not a complete general-purpose XML validation pass | Follow its explicitly specified checks; do not silently add document/DTD validation or claim that every emitted standalone leaf is an XML document |

For the last row, CDATA's draft subalgorithm simply emits its data even in the true mode. Do not
claim the flag repairs a subsequently mutated `]]>` sequence. Likewise source doctype-name limitations
and text CR round-tripping must have explicit fixtures, not an unqualified well-formed-output promise.
A stronger standalone validating serializer, if wanted, is a separate reviewed contract rather than
an undocumented change to XMLSerializer. Record affected WPT rows individually; never discard every
prefix-repair test because the draft has errors. X3b review must include literal expected strings for
all policy rows plus namespace/value projections after reparsing cases intended to round-trip.

## Work, limits and ownership

One operation owns its writer, traversal stack, namespace tables and shadow set. No shared mutable
formatter singleton, global namespace counter, node cache or document-attached serialization state.
Results are independent immutable strings. Serialization emits no native mutation record or host
signal. Concurrent mutation is unsupported; capture owner/stamp information for each encountered
document (including template owners), reject saturated stamps, check active/root ownership and stamps
at polls, and verify every captured document before returning. Do not snapshot/copy the tree. A native
owner change or detected mutation throws InvalidOperationException and discards the unpublished result.
Check only the root and current frame's document at an ordinary poll, plus a document when entering
or resuming its frame; do not scan all ancestors or all captured documents at every checkpoint. The
final distinct-document verification is one separately polled linear pass. Detached Attr output is
empty and therefore needs entry/final cancellation, not a fictitious owner mutation stamp.

Use one shared 256-unit cooperative work cadence for visited nodes, attributes/bindings, inspected
name/data units, escaped output units, stack/index construction and final copying. Every authored
loop, including duplicate checks, prefix generation, undo and option-set construction, charges it.
Copy ordinary runs in bounded spans; do not append one character at a time when no escaping is needed.
Poll before/after unavoidable CLR growth/materialization. No recursive serialization, chained Replace,
LINQ string construction, quadratic prefix flattening, recursive node TextContent, or partial strings
for each subtree. Name hashing/comparison must be accounted for, not hidden in an unpolled long-key scan.

The output bound counts UTF-16 units of the exact emitted string, including escapes, repaired
declarations, synthetic attributes and shadow wrappers. Check using overflow-safe long arithmetic
before increasing capacity/appending; equality is accepted. Report the first append's known total
exceeding the bound as Observed. No suffix truncation, partial return, or output reserved at six times
input length. With no explicit bound, CLR string capacity remains a platform resource constraint.
No implicit small depth cap: the stack must handle deep legal native trees independently of parser limits.

An internal staged checkpoint can be supplied per invocation for tests; no public/static hook.
Cancel inside traversal, long escape/name validation, prefix search, shadow selection and materialization.
A test must identify its target stage and fail when that loop's polls disappear; a final-return check
must not mask the regression. This is cooperative cancellation, not a hard elapsed-time guarantee.

## Finite dispatch and gates

All initial production files go under new `Jint.HtmlParser/Serialization/`; tests go under new
`Jint.Tests.HtmlParser/Serialization/`. No edits to native owners, parsing, XPath, Browser, snapshots,
or existing corpus exclusions in these implementation slices.

1. **X3a, first Sol dispatch:** `SerializationWriter.cs`, `SerializationWork.cs`,
   `SerializationLimits.cs`, `XmlScalarSerializer.cs`; tests `SerializationWriterTests.cs` and
   `XmlScalarSerializerTests.cs`. Implement internal writer append/run/escape/materialize operations,
   exact output accounting and stage polling; implement XML scalar text/comment/CDATA/PI/doctype and
   attribute-value kernels under both requireWellFormed values. These are typed internal helpers,
   not a partial MarkupSerializer facade or placeholder element serializer. Depends only on native
   data access; no H7, IsValue or ShadowRoot promotion dependency. Test literal lexical edge cases,
   astral/unpaired surrogate handling, exact quotas on expanded escapes, and stage-specific cancellation.
2. **X3b, XML walker:** `XmlMarkupSerializer.cs`, `XmlNamespaceScope.cs`, XML tests. Implement all
   accepted node/Attr kinds, iterative namespace repair and containers using X3a. Add internal whole
   and children entry points with the final semantics above. Namespace conflict, declaration ordering,
   prefix-counter and wide/deep work-count tests are review gates. No partial public API.
3. **X3c, HTML walker:** `HtmlMarkupSerializer.cs`, `HtmlSerializationOptions.cs`, HTML tests. Implement
   ordinary/template HTML output and the current native shadow metadata path; use internal settings
   until D6 promotion. Native IsValue support is a separate owner prerequisite before declaring this
   slice complete; a tested internal ordinary-tree milestone may land earlier without a public facade.
   No stubbed IsValue callback or silently skipped metadata path.
4. **X3d, publication/round-trip gate:** new `MarkupSerializer.cs`, fully implemented public options,
   limits/errors and all five entry points; shared owner updates actual API snapshots and unsigned
   packed-consumer tests. Requires X3b/c, IsValue support, real ShadowRoot promotion, and H7's relevant
   context fixtures. Keep parser conformance and serializer correctness reported separately.
5. **B4/R4 integration:** select operations per the consumer table; update A1/binding outputs only
   through their owners. Record XML getter compatibility, XMLSerializer standards corrections and
   getHTML addition explicitly. No production switch based solely on native unit-test success.

Standalone tests first construct native trees and assert exact strings plus unchanged identities,
parents, attributes, stamps and mutation queues. Include hostile-but-constructible names, namespace
conflicts, duplicate expanded-name rejection, namespace-free wide/deep trees, null/empty/default
bindings, declaration-after-use, generated prefix collisions, HTML/SVG/MathML, obsolete void names,
raw-text versus RCDATA/noscript, templates with independently manipulated ordinary children, and
all shadow selection combinations (including closed and unrelated roots). Cover attribute order and
quote/ampersand/angle/NBSP/CR/LF/TAB differences in both formats. Test malformed comments, CDATA and PI
after Data mutation under both XML modes, not just constructor-accepted states.

Reparse only designated round-trip-safe fixtures: compare expanded names, attribute values, ordinary
children and template contents, not original prefixes/quotes or original DTD text. Keep counterexamples
for raw-text end-tag data, comments with terminators, initial LF and XML text CR. Native XmlNotations
and skipped-resource provenance are outside the serialized DOM infoset; never substitute the corpus
SecondCanonicalForm serializer for this implementation.

Use primary WPT serialization cases with an exact reviewed pin/license/manifest in the new native test
area; do not expand or alter the existing WPT vendor tree as an incidental serializer change. Browser
tests must cover HTML/XML markup getters, DOMParser/XMLSerializer, Page.ContentAsync, DevTools root
selection, templates and getHTML. Build/test Release on net8.0 and net10.0, without timings or skipped
public methods. No completed-X3 claim while any named metadata, policy, publication or consumer gate
is still an unimplemented placeholder.

# Jint.HtmlParser

Jint.HtmlParser is an experimental native markup package for .NET 8 and .NET 10. Its implemented public surface includes:

- A mutable document tree with elements, attributes, text, comments, CDATA and processing instructions.
- Inert HTML document and contextual fragment parsing through `MarkupParser.ParseHtml` and `ParseHtmlFragment`, with lexical limits and bounded diagnostics.
- XML document and fragment parsing through `MarkupParser.ParseXml` and `ParseXmlFragment`, and strict SVG-root parsing through `ParseSvg`.
- CSS Syntax parsing of whole stylesheets, rules, declarations and component values. These methods return syntax, without CSS property validation or computed styles.
- Native mutation subscriptions with explicit record draining through `Document.ObserveMutations`.
- Explicit HTML/XML serialization through `MarkupSerializer`, with output limits, cancellation and selected native shadow roots.
- XPath 1.0 compilation, evaluation and selection through `NativeXPath`, preserving namespaces and native result identities.

```csharp
using Jint.HtmlParser;

var document = MarkupParser.ParseXml("<root><item/></root>");
var root = document.DocumentElement!;
var fragment = MarkupParser.ParseXmlFragment("<next/>", root);
root.AppendChild(fragment);
```

XML results expose immutable `SkippedXmlEntities`, `XmlNotations` and
`XmlDtdProcessingInstructions` inventories. DTD instructions retain target, data and original UTF-16
offsets in encounter order (including repeated parameter-entity invocations). They are parse metadata,
not DOM children: DOM serialization and XPath do not relocate them into the document. Document
clones preserve these inventories; importing/adopting nodes or parsing a fragment does not transfer
or overwrite them. No external resolver is exposed.

HTML parsing recovers malformed markup and creates implied document structure. Fragment results belong to the context owner document and leave existing children untouched. `HtmlParseOptions.ScriptingEnabled` changes grammar without executing scripts. Native HTML parsing performs no network requests.

Whole-sheet CSS parsing returns immutable syntax and the exact original source:

```csharp
using Jint.HtmlParser;
using Jint.HtmlParser.Css;

CssStyleSheetSyntax sheet = MarkupParser.ParseCss("@future x; p { color: var(--theme); }");
foreach (CssRuleSyntax rule in sheet.Rules)
{
    string originalRule = sheet.Source.Substring(rule.Span.Start, rule.Span.Length);
    Console.WriteLine(originalRule);
}
```

`ParseCss` consumes the whole input with standard CSS Syntax recovery; empty input succeeds.
Rules expose immutable preludes and nested token, function and block components with original UTF-16 spans.
Unknown at-rules, unsupported properties, invalid property values and pending substitutions remain syntax.
Parsing does not validate selectors or properties, resolve URLs, load imports, attach styles, or compute a cascade or geometry.
`CssParseOptions` supplies the existing limits and optional diagnostic collector; cancellation is supported.
The result retains no parser, options or collector. Public mutable CSSOM promotion and equivalent CSSOM benchmarks are separate work.

Serialization selects its format explicitly, without moving nodes or fetching resources:

```csharp
string html = MarkupSerializer.ToHtml(document);
string xml = MarkupSerializer.ToXml(document, requireWellFormed: true,
    limits: new SerializationLimits { MaxOutputCharacters = 100_000 });
```

`ToHtmlChildren` and `ToXmlChildren` omit the container's tags and use template contents where appropriate.
`requireWellFormed` enables the DOM Parsing algorithm's checks; it is not general XML/DTD validation.
Output-limit failures throw `SerializationLimitException` without returning partial output.
Normal HTML output omits attached shadow roots. `Element.AttachShadow` returns the real native
`ShadowRoot` (even for closed mode); `OpenShadowRoot` exposes only open roots. Pass roots explicitly
in `HtmlSerializationOptions`, or pass `serializableShadowRoots: true` to include reachable roots marked
serializable. Attachment does not execute Browser custom-element reactions or resolve its definitions.

XPath expressions can be reused across documents:

```csharp
NativeXPathExpression expression = NativeXPath.Compile("//item");
IReadOnlyList<object> nodes = NativeXPath.Select(document, expression);
double count = NativeXPath.Evaluate(document, "count(//item)").NumberValue;
```

Unprefixed XPath element tests match no namespace; use a namespace resolver for HTML/XML namespaces.
Contexts can be a `Node`, an attached/detached non-XMLNS `Attr`, or a captured `XPathNamespaceBinding`.
Results contain only native nodes, attributes and namespace bindings. Membership and captured scalar
values remain stable after mutation; the selected native objects themselves are not frozen.
Reusing a namespace context rejects a binding whose URI is no longer in scope. No BCL cursor or
mutable expression escapes. Cancellation brackets BCL work but is not a hard timeout during its
non-preemptible compilation/evaluation intervals.

`Jint.Browser` now uses this native tree, parser and CSS model. Neither production packages nor tests
depend on AngleSharp. AngleSharp, AngleSharp.Css and AngleSharp.Xml remain only in `Jint.Benchmark`
as comparison controls; dependency removal is not a claim of complete behavioral parity.

## Parser layers and demand

Parsing a document builds the structure needed by that document grammar, not every language embedded
in its text and attributes. Grammar implementations may live in this package; the browser consumer
decides when to invoke them. This follows the separation in Lightpanda's
[CSS parser](https://github.com/lightpanda-io/browser/blob/5932638bafb5c6584457c0cae037e6b395b265e5/src/browser/css/Parser.zig)
and [style manager](https://github.com/lightpanda-io/browser/blob/5932638bafb5c6584457c0cae037e6b395b265e5/src/browser/StyleManager.zig),
without adopting its narrower grammar support or copying its implementation.

| Input | Structural layer | Consumer that demands interpretation |
| --- | --- | --- |
| HTML | Tokenization, tree construction, decoded attributes and text, required parser/control history | Browser script preparation/execution, resource scheduling, reflected attributes and control operations. Event handlers compile on first use, not while reading their attributes. |
| XML and SVG | Well-formedness, namespaces, entities and native nodes | Processing-instruction pseudo-attributes are parsed when read. Embedded CSS, JavaScript, URLs and SVG data remain text; no stylesheet or script execution occurs in the native parser. |
| CSS | Rule kinds and raw source spans; a streaming lexical scan finds balanced boundaries without retaining tokens or component trees | Browser CSSOM reads or styling explicitly parse the requested rule lists. Conditional bodies remain raw until applicable, or until explicitly inspected through CSSOM. |
| Media queries | Raw condition text | Matching, serialization or indexed CSSOM reads explicitly parse the condition with the current operation's work budget. Import loading does not need that interpretation. |
| Selectors and XPath | Document construction does not compile either language | Query APIs compile their arguments; stylesheet selector compilation happens when its containing rule list is demanded. |
| CSS declarations, functions and typed values | Retained declaration syntax | Property reads, substitution and computation demand the relevant grammar. Explicit setters and validation APIs still validate their supplied input. |

The internal `CssParser` entrypoints make these boundaries explicit:

```csharp
var rule = CssParser.ParseMediaRule(rawRuleText, work); // Raw prelude and body slices.
var media = CssParser.ParseMediaQueryList(rule.Prelude, work);
var children = CssParser.ParseRuleList(rule.Body.Value.Text, work);
var declarations = CssParser.ParseDeclarationList(rawDeclarations, work);
```

String and source-slice overloads let the next parser run independently while preserving original
UTF-16 offsets. `ParseMediaRule` does not interpret its condition or body. Browser integration uses
the block-context overload of `ParseRuleList` for mixed declaration/nesting recovery. The existing
public `MarkupParser.ParseCss*` APIs still return the explicitly requested complete syntax; the new
layer-specific entrypoints and mutable CSSOM remain internal.

`NativeCssParsing` in `Jint.Browser` owns parse-result caches, source generations and invalidation.
Native rule-list and media-list getters only read already parsed data; they have no `IsDeferred`
state and cannot invoke parsers. Raw slices retain only their source string and offsets, not a token
tape, parser snapshot, diagnostic collector, cancellation token or engine callback. They can keep the
source string alive, like source-backed HTML text.
The initial scan still checks lexical limits, nesting, cancellation and diagnostics.

Browser stylesheet installation retains source; import discovery promotes only the prefix needed to
find valid imports, not unrelated rules or nested bodies. The explicit internal validated-sheet parser
and CSSOM insertion still validate the requested rule tree. A known incomplete grammar in an inactive
body therefore fails when that body is actually demanded, not merely because a document loaded it.
Unknown-rule recovery is unchanged.

Each explicit parse uses the current operation's budget, publishes completed state only, preserves rule/list
identity and source offsets, and does not advance mutation stamps. Source replacement invalidates
pending work; detached descendants retain their historical parent links without mutating the old sheet.
Required history is not optional work: HTML repair, namespace/entity processing, radio/select state,
script ordering and resource discovery must still happen at their specification-defined points.

## HTML scanning

The UTF-16 tokenizer uses cached `SearchValues<char>` sets to append ordinary
text, names, attribute values and comment runs in bulk. Each run stays within
the current input slice and work quota; CR/LF preprocessing, diagnostics,
token limits and insertion markers retain their scalar handling. Names still
use HTML's ASCII-only case folding. Common names use generated, length-first
decision trees with discriminating UTF-16 positions and wide integer comparisons.
Every character is verified before returning the canonical string literal;
no input string is materialized. A bounded, parse-local cache reuses other short
names before allocating strings. The generator, safety model and isolated
comparison are documented in [Known-name recognition](Html/known-name-lookup.md).
The cache uses xxHash3 over the UTF-16 bytes without an encoding allocation.
Long names and cache collisions remain correct without process-global interning.

Generated recognition also covers fixed HTML/SVG tree-construction names, XML
keywords and catalog identifiers, and CSS values, properties, selectors, media
features and at-rules. CSS keyword sets return canonical literals without
allocating a normalized string or scanning a space-delimited list. Property
metadata uses generated indices while keeping its existing context restrictions.
The vocabularies and regeneration commands are documented in
[Parser-wide recognition](Html/known-name-lookup.md#parser-wide-recognition).

Parser dispatch uses `switch` statements or expressions for alternatives on the
same token-kind, mode, grammar or status enum. Guarded cases retain their original
priority and fallback behavior. Independent checks and sequential state updates
remain separate when more than one branch must execute; XML's character and
prefix recognition does not introduce enums solely to replace those checks.

Ordinary text and attribute values carry internal immutable source slices from
the tokenizer into the DOM. Each slice owns a reference to its source string and
exposes a `ReadOnlySpan<char>` internally. Entities, line normalization and
noncontiguous input fall back to owned decoded storage; only adjacent ranges of
the same source can coalesce without copying. Caller arrays and reusable
tokenizer buffers are never retained as immutable values.

Public `Text.Data` and `Attr.Value` remain strings. Their first read materializes
and caches the current value when needed; source-backed values then release their
reference to the larger input. Internal span consumers, including HTML
serialization, can read without materialization. Mutation, cloning and old-value
notifications preserve the existing string and ownership contracts.

This trades fewer copies for source retention: an unread value can keep its whole
input string alive, including on a detached node. Shared text/attribute storage
also has a layout cost for already materialized values such as XML input.
Source slices are not a new public API or raw-markup provenance contract.
`HtmlParserValueAccessBenchmark` measures complete parsing plus string or span
consumption separately, so parse-only measurements cannot hide deferred work.

Named character references advance through a shared immutable trie built from
the pinned WHATWG table. Prefix recognition does not allocate candidate strings;
longest-match recovery and the attribute-specific semicolon rules remain part
of the tokenizer. `Html/generate_entities.py` verifies the pinned input hash
when regenerating the table.

## Temporary parsing buffers

CSS token values and XML attribute/line normalization use a stack-backed
`ValueStringBuilder` with 128-character initial buffers and pooled growth.
Only synchronous, method-local buffers use it; builders retained between HTML
tokenizer yields or XML entity frames remain heap-backed. Completed values own
their strings, and pooled buffers are returned on success, errors and cancellation.
The implementation is copied from [.NET's pinned source](https://github.com/dotnet/dotnet/blob/9cc5eb8d49d3381ff9890b959faca397b8d537e7/src/runtime/src/libraries/Common/src/System/Text/ValueStringBuilder.cs),
with only namespace and formatting changes. Its [MIT license](Parsing/ValueStringBuilder.LICENSE.txt)
is included in the package. The parser does not acquire a dependency on the Jint engine
to reuse its separate, engine-specific builder.

## Remaining replacement work

The main gaps are above HTML tokenization and tree construction:

| Area | Current boundary |
| --- | --- |
| CSS property grammars | `CssPropertyRegistry` implements a subset of `CssPropertyCatalog`. Content alignment, gap, legacy grid-gap aliases, SVG fill/stroke paint, clip references/geometry boxes, background image references and `all` resets have declaration/computation support. Typed URLs resolve without fetching or rendering; shapes, gradients, advanced colors and URL modifiers retain named pending boundaries. Borders/background shorthands, grid tracks, font-family/line-height, animation/transition and other SVG properties remain pending. A catalog entry is an obligation, not implemented support. |
| CSS rules and nesting | Native media, supports, container, imports, font-face, keyframes, layer blocks/statements and primitive `@property` registrations exist. Layers preserve origin/shadow scope, nesting, conditional order and important/rollback semantics. Registrations validate descriptors and expose readonly CSSOM metadata; URL/image/transform registration grammars remain incomplete. Namespaces, scope, page/counter-style, import `layer()`/`supports()` conditions, nested conditional rules and interleaved declarations remain pending. |
| Computed and resolved values | Registered primitive values compute before substitution and inheritance; invalid values reset according to the registration. Advanced colors, ordinary-property registration cycles, typed `attr()` and other substitution functions, some container metrics and used-value dependencies remain named completion failures. Browser also still documents incomplete stylesheet BOM/charset and MIME handling. |
| Standalone APIs | HTML/XML serialization and owned XPath APIs are public. Selectors, mutable CSSOM/typed values and incremental HTML sessions remain internal. Public parsing accepts decoded strings, not streams or byte inputs. |
| Selector language and direction | Internal `:lang()` matching supports inherited HTML `lang`/XML `xml:lang` and extended language ranges. HTTP/document language metadata fallback remains open. `:dir()` uses Browser's bounded directionality facts; a standalone caller must supply a directionality producer. |
| Shadow selectors | Stylesheet `:host`/`:host(...)` matching preserves featureless hosts, tree boundaries, argument specificity and encapsulation precedence. DOM queries do not gain stylesheet host context. `:host-context()` remains unsupported. |
| Acceptance | The complete pinned XML profile has zero unresolved cases and compares all 386 eligible outputs. Swagger completes its real interaction under unchanged budgets; Scalar gets past registrations and `:host`, then stops on `V7:clip`. The canonical Windows WPT census and remaining public API/benchmark gates remain separate obligations. |

Unknown syntax and known-but-unimplemented semantics are deliberately different: raw CSS syntax can
be retained lazily, but demanding an incomplete grammar raises a named failure rather than inventing a
computed value. Passing tests for that failure boundary does not establish support for the feature.
HTML/XML comparisons have untimed structural checks; equivalent CSSOM comparison and paired
performance acceptance remain separate work.

The dependency-ordered [completion tracker](../docs/design/html-parser-completeness.md) separates
implemented slices from still-open CSS families, public API gates and acceptance debt.
It also records the registration slice's published CSS Properties and Values API Level 1 contract;
the newer editor's-draft descriptor defaults and multiple-name syntax are not implemented.

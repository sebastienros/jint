# Jint.HtmlParser

Jint.HtmlParser is an experimental native markup package for .NET 8 and .NET 10. Its public API is provisional until **ACC-06 (external package and API acceptance)** in the
[acceptance tracker](https://github.com/sebastienros/jint/blob/main/docs/design/html-parser-completeness.md)
is explicitly closed. Internal Browser use and API snapshots do not close that gate. Public signatures,
behavior and value-type layouts may change between prereleases, including positional record structs such
as `ShadowRootInit` and `BoundaryPoint`; pin a tested package version when integrating.

The package remains prerelease even when Jint releases GA. Existing prerelease inputs are preserved
(for example `5.0.0-preview-123`); stable version inputs become `5.0.0-experimental-0` by default,
or `5.0.0-experimental-<BuildNumber>` when a build number is supplied. This applies to explicit
`Version` and `PackageVersion` overrides as well as the normal prefix/suffix calculation. The numeric
base follows the repository release; it is not a promise that this package's API has frozen.
Promotion to a stable parser package requires a separate reviewed change removing the build policy
and recording the external acceptance evidence. Jint.Browser can retain its repository release version
while declaring the calculated prerelease parser dependency.

The [API usage guide](https://sebastienros.github.io/jint/packages/jint-htmlparser/) covers installation,
parsing, tree manipulation, CSS syntax, XPath, serialization, limits and threading.

Its implemented public surface includes:

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

XML parsing defaults to a ceiling of 10,000,000 UTF-16 entity replacement units, including when
other limits are supplied through `new ParseLimits()`. Select `ParseLimits.Unbounded` or explicitly
set `MaxEntityExpansionCharacters = 0` only for trusted inputs. Exceeding a limit raises
`ParseLimitException`, separately from XML syntax errors.

XML results expose immutable `SkippedXmlEntities`, `XmlNotations` and
`XmlDtdProcessingInstructions` inventories. DTD instructions retain target, data and original UTF-16
offsets in encounter order (including repeated parameter-entity invocations). They are parse metadata,
not DOM children: DOM serialization and XPath do not relocate them into the document. Document
clones preserve these inventories; importing/adopting nodes or parsing a fragment does not transfer
or overwrite them. No external resolver is exposed.

HTML parsing recovers malformed markup and creates implied document structure. Fragment results belong to the context owner document and leave existing children untouched. `HtmlParseOptions.ScriptingEnabled` changes grammar without executing scripts. Native HTML parsing performs no network requests.

Input length and token limits default to zero (disabled); CSS nesting also defaults to zero.
The finite XML entity ceiling is not an overall memory or time budget. Supply bounds suited to your input
and a cancellation token for cooperative cancellation. For example:

```csharp
var bounded = MarkupParser.ParseHtml("<p>Hello</p>", new HtmlParseOptions
{
    Limits = new ParseLimits { MaxInputCharacters = 100_000, MaxTokenCharacters = 20_000 }
});
```

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

## Threading

After parsing finishes and the tree is safely published to other threads,
concurrent immutable inspection of tree links, node names, character data and
attribute values is supported, including cold `Text.Data`/`Attr.Value` getters,
text-content reads and serialization with separate per-call state. Keep the tree
unchanged for the entire read operation. Parsing sessions, mutation, adoption,
live ranges/iterators and mutation subscriptions require one writer and external
synchronization with all readers; one writer alone does not make overlapping
reads safe. Operations that initialize mutable DOM state or indexes (including
selector/id queries, form-control state and processing-instruction attribute
views) also require external synchronization. This is not a blanket thread-safety
promise for every method that looks like a read.

Jint.Browser page trees retain their page-loop affinity: access them through the
page's scheduled operations. The standalone parser's immutable-read support does
not permit reading a live Browser DOM from another thread while its page can run
script, parse or mutate it.

## Contributor documentation

Implementation contracts, regeneration, corpus maintenance and package verification belong in the
[contributor guide](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/CONTRIBUTING.md).
The [acceptance tracker](https://github.com/sebastienros/jint/blob/main/docs/design/html-parser-completeness.md)
records remaining work; dependency removal does not establish complete behavioral parity.

## Renderless CSS boundary

The [renderless CSS contract](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/CONTRIBUTING.md#renderless-css-boundary)
defines Browser's text-value scope and the deliberately limited declaration grammar.

## Browser integration contract

The [Browser integration contract](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/CONTRIBUTING.md#browser-integration-contract)
defines reviewed internal hooks, snapshots and parser/Browser ownership.

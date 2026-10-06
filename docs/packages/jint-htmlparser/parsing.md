# Parsing Markup

Use `using Jint.HtmlParser;` for the examples on this page. Public entry points accept an owned .NET
`string` and an optional `CancellationToken`. Decode byte input before calling them; they do not expose
a public stream, byte-encoding detector or incremental parser session.

## HTML documents and fragments

`MarkupParser.ParseHtml(source, options, cancellationToken)` returns a `Document`. It recovers malformed
HTML and inserts implied structure. `HtmlParseOptions.ScriptingEnabled` defaults to `false`; setting it
changes scripting-dependent grammar, including `noscript`, without running JavaScript.

Use an existing element as the context for `ParseHtmlFragment`. The context controls grammar and
namespaces: a table context can imply a `tbody`, and an SVG context preserves foreign namespaces.

<!-- snippet: parser-html-fragment -->
```csharp
var document = Document.CreateHtml();
var table = document.CreateElement("table");
document.AppendChild(table);
var fragment = MarkupParser.ParseHtmlFragment("<tr><td>Hello</td></tr>", table);
table.AppendChild(fragment);
Console.WriteLine(MarkupSerializer.ToHtml(table));
```
<!-- endSnippet -->

The returned fragment belongs to the context's owner document and is detached. Parsing leaves the
context's existing children unchanged; appending the fragment moves its children into the context.
Template context results also belong to the context owner and do not populate existing template content.
Native parsing leaves declarative shadow markup as inert template elements.

## XML and SVG

`ParseXml` parses a complete XML document; malformed XML raises `MarkupParseException`. It preserves
namespaces and reports zero-based offsets in the original UTF-16 input. `ParseSvg` additionally requires
an `svg` root in `Namespaces.Svg`. An unnamespaced `<svg/>` is not accepted as strict SVG.

`ParseXmlFragment` inherits namespace bindings from its element context:

<!-- snippet: parser-xml-fragment -->
```csharp
var document = MarkupParser.ParseXml("<catalog xmlns='urn:catalog'><item/></catalog>");
var root = document.DocumentElement!;
var fragment = MarkupParser.ParseXmlFragment("<next/>", root);
root.AppendChild(fragment);
Console.WriteLine(((Element) root.LastChild!).NamespaceUri); // urn:catalog
```
<!-- endSnippet -->

The example produces `<next>` in `urn:catalog`. As with HTML, parsing does not replace the context's
children. Use `Document.CreateXml()` or `CreateHtml()` when constructing a tree without parsing.

## Entity handling and XML metadata

XML supports internal entity declarations and applies an entity-expansion ceiling of 10,000,000 UTF-16
replacement units by default. It does not fetch external subsets or expose an external resolver.
The resulting `Document` provides immutable inventories:

| Property | Meaning |
| --- | --- |
| `SkippedXmlEntities` | Entity content omitted by the parser's external-entity policy |
| `XmlNotations` | Retained notation declarations |
| `XmlDtdProcessingInstructions` | DTD instructions, with target, data and original-input offsets |

These are parse metadata, rather than additional DOM children. Cloning a document preserves the
inventories. Fragment parsing does not overwrite them; importing or adopting a node does not transfer
a source document's inventories. DOM serialization and XPath do not relocate DTD instructions into the tree.

For input/token limits, cooperative cancellation and bounded HTML diagnostics, see
[Limits and threading](./limits-and-threading.md).

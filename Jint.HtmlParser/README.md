# Jint.HtmlParser

Jint.HtmlParser is an experimental native markup package for .NET 8 and .NET 10. Its implemented public surface includes:

- A mutable document tree with elements, attributes, text, comments, CDATA and processing instructions.
- XML document and fragment parsing through `MarkupParser.ParseXml` and `ParseXmlFragment`, and strict SVG-root parsing through `ParseSvg`.
- CSS Syntax parsing of rules, declarations and component values. These methods return syntax, without CSS property validation or computed styles.
- Native mutation subscriptions with explicit record draining through `Document.ObserveMutations`.

```csharp
using Jint.HtmlParser;

var document = MarkupParser.ParseXml("<root><item/></root>");
var root = document.DocumentElement!;
var fragment = MarkupParser.ParseXmlFragment("<next/>", root);
root.AppendChild(fragment);
```

Public HTML document parsing, selector matching, full CSSOM, serialization and Browser integration remain in development. This package does not yet establish conformance or performance claims for replacing the current Browser parser.

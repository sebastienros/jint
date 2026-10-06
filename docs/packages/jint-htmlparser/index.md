# Jint.HtmlParser

`Jint.HtmlParser` parses and manipulates HTML, XML, SVG and CSS from .NET. It provides a native mutable
DOM, XPath 1.0, mutation subscriptions, live ranges and explicit HTML/XML serialization. It targets
.NET 8 and .NET 10 and has no dependency on the Jint JavaScript engine or AngleSharp.

Parsing is inert: it does not execute scripts, fetch resources, load CSS imports or resolve external XML
entities. Use [Jint.Browser](../jint-browser/) when you need JavaScript, navigation or a page event loop.
The standalone parser does not compute styles or render a page.

## Install

Follow [Using Jint 5 Preview Packages](../../guide/preview-packages.md) to register the development feed:

```bash
dotnet add package Jint.HtmlParser --prerelease
```

Pin the resolved version in your application. The parser API is **experimental**: public signatures,
behavior and value-type layouts can change between prereleases. Its package remains prerelease even
when Jint releases GA; a stable version input becomes `experimental-<BuildNumber>` (default `0`),
while an existing prerelease suffix is preserved. Browser packages pin their calculated parser dependency.
[External package and API acceptance](../../design/html-parser-completeness.md) remains open.

## Parse your first document

The main APIs and native tree types are in `Jint.HtmlParser`. Add `using Jint.HtmlParser;`:

<!-- snippet: parser-first-document -->
```csharp
var document = MarkupParser.ParseHtml("<main><p>Hello</p></main>");
var body = document.DocumentElement!.LastChild!;
Console.WriteLine(MarkupSerializer.ToHtml(body.FirstChild!));
```
<!-- endSnippet -->

This prints `<main><p>Hello</p></main>`. HTML parsing creates the implied `html`, `head` and `body`
structure, so the first body child is the `main` element. Parsing a script element would retain its text
without executing it.

## Choose an API

| Task | Entry point | Guide |
| --- | --- | --- |
| Parse HTML, XML, SVG or contextual fragments | `MarkupParser` | [Parsing markup](./parsing.md) |
| Create, traverse, mutate, observe or select a range in a tree | `Document`, `Node`, `Element`, `DomRange` | [Working with trees](./trees.md) |
| Inspect CSS syntax or query a tree with XPath | `MarkupParser.ParseCss*`, `NativeXPath` | [CSS syntax and XPath](./css-and-xpath.md) |
| Emit HTML or XML, with optional shadow roots | `MarkupSerializer` | [Serialization](./serialization.md) |
| Bound work, collect diagnostics and coordinate readers | `ParseLimits`, cancellation tokens | [Limits and threading](./limits-and-threading.md) |

The package is marked AOT-compatible; the unsigned fresh-package consumer also runs as Native AOT in
CI. See [Native AOT and Trimming](../../reference/native-aot.md) for the distinction between this library
and the broader Browser package. API snapshots and internal Browser use do not freeze this provisional API.

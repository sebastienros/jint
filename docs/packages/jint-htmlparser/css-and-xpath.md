# CSS Syntax and XPath

## CSS syntax

Use `using Jint.HtmlParser;` and `using Jint.HtmlParser.Css;` to parse and inspect CSS syntax:

<!-- snippet: parser-css -->
```csharp
CssStyleSheetSyntax sheet = MarkupParser.ParseCss("@future value; p { color: var(--theme); }");
foreach (CssRuleSyntax rule in sheet.Rules)
{
    Console.WriteLine(sheet.Source.Substring(rule.Span.Start, rule.Span.Length));
}
```
<!-- endSnippet -->

This prints the two original rules. `CssStyleSheetSyntax.Source` retains the exact input; rule spans and
component spans use UTF-16 offsets into it. Rules, preludes, functions, blocks and token components are
immutable syntax objects. Component types also live in `Jint.HtmlParser.Css.Syntax`.

| Method | Result |
| --- | --- |
| `ParseCss` | `CssStyleSheetSyntax` for a complete stylesheet, with standard recovery |
| `ParseCssRule` | One `CssRuleSyntax` |
| `ParseCssDeclaration` | One `CssDeclarationSyntax` |
| `ParseCssComponentValue` | One token, function or block component |
| `ParseCssComponentValues` | A `CssComponentValueList` |

All accept `CssParseOptions` and cancellation. Empty stylesheets succeed. A single-construct method
that cannot produce its requested construct raises `CssParseException`, exposing `Code` and `Offset`.
An optional `ParseDiagnosticCollector` retains bounded recoverable errors.

Parsing retains unknown at-rules, unsupported properties, invalid property values and unresolved
substitutions as syntax. It does not validate properties or selectors, resolve URLs, load imports,
attach style sheets, compute a cascade or expose public mutable CSSOM.

## XPath 1.0

`NativeXPath.Compile` prepares an expression for reuse. `Evaluate` returns a captured scalar or node-set;
`Select` requires a node-set and returns native identities. Prefixes are resolved through
`IXmlNamespaceResolver`; use `XmlNamespaceManager` from `System.Xml`:

<!-- snippet: parser-xpath -->
```csharp
var document = MarkupParser.ParseXml("<catalog xmlns='urn:catalog'><item id='one'/></catalog>");
var namespaces = new XmlNamespaceManager(new NameTable());
namespaces.AddNamespace("c", "urn:catalog");
var expression = NativeXPath.Compile("//c:item", namespaces);
IReadOnlyList<object> items = NativeXPath.Select(document, expression);
Console.WriteLine(items.Count);
Console.WriteLine(NativeXPath.Evaluate(document, "count(//c:item)", namespaces).NumberValue);
```
<!-- endSnippet -->

This prints `1` twice. Unprefixed XPath element tests match the empty namespace, so both HTML's namespace
and XML default namespaces require an explicit query prefix. Query prefixes need not equal source prefixes.

`NativeXPathResult.ResultType` determines which getter to read: `NumberValue`, `BooleanValue`,
`StringValue`, or `Nodes` and `FirstNodeStringValue`. Reading a getter of another result kind raises
`InvalidOperationException`; invalid expressions or scalar expressions passed to `Select` raise
`System.Xml.XPath.XPathException`.

Contexts may be a `Node`, an attached or detached non-XMLNS `Attr`, or a `XPathNamespaceBinding` captured
from the namespace axis. Results expose those native identities, with no mutable BCL cursor escaping.
Node-set membership and captured scalar values are snapshots; selected objects can still be mutated.
A namespace context whose URI is no longer in scope is rejected when reused.

Compiled expressions can be reused across documents. Keep trees synchronized during queries and mutation.
Cancellation is checked around BCL compilation/evaluation; it cannot interrupt a non-preemptible BCL
interval and is not a hard wall-clock deadline.

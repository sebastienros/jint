# Serialization

Use `using Jint.HtmlParser;`. Select the output format through the method you call; the input document
kind does not choose it automatically.

<!-- snippet: parser-serialization -->
```csharp
var document = MarkupParser.ParseXml("<root><item/></root>");
string xml = MarkupSerializer.ToXml(document, requireWellFormed: true,
    limits: new SerializationLimits { MaxOutputCharacters = 100_000 });
Console.WriteLine(xml);
```
<!-- endSnippet -->

The example prints `<root><item/></root>`. XML serialization repairs namespace prefixes in the output
without modifying the source tree. Strings include no added encoding declaration or byte-order mark;
encode them explicitly when writing bytes.

| Method | Content |
| --- | --- |
| `ToHtml(node)` | The node as HTML |
| `ToHtmlChildren(parent)` | Children of an element, document or fragment as HTML |
| `ToXml(node, requireWellFormed)` | The node as XML |
| `ToXmlChildren(parent, requireWellFormed)` | Children with self-contained XML namespace declarations |
| `ToXml(attribute, requireWellFormed)` | DOM Parsing's empty serialization of an attribute |

Child serialization omits the container's tags and uses template contents where applicable.
`requireWellFormed` applies DOM Parsing serialization checks; it does not perform general XML or DTD
validation. Invalid trees can raise `DomException`. Pass `SerializationLimits.MaxOutputCharacters`
to bound output and a cancellation token to cooperate with host cancellation. The output ceiling
counts UTF-16 units and defaults to zero (disabled); a failure raises `SerializationLimitException`
without publishing partial output.

## Shadow roots

Normal HTML serialization omits attached shadow roots. Select explicit roots, including closed ones,
or include reachable roots whose `Serializable` flag is set:

<!-- snippet: parser-shadow-serialization -->
```csharp
var document = Document.CreateHtml();
var host = document.CreateElement("div");
document.AppendChild(host);
var shadow = host.AttachShadow(new ShadowRootInit(ShadowRootMode.Closed, Serializable: true));
shadow.AppendChild(document.CreateTextNode("shadow content"));
string html = MarkupSerializer.ToHtml(host,
    new HtmlSerializationOptions(shadowRoots: new[] { shadow }));
Console.WriteLine(html);
```
<!-- endSnippet -->

`HtmlSerializationOptions` copies and deduplicates an explicit root sequence. Roots that are not reachable
from the serialized subtree are not appended. Shadow output uses declarative shadow templates.
`serializableShadowRoots: true` selects reachable serializable roots without supplying the sequence.
XML serialization does not take HTML shadow-selection options.

The constructor's `scriptingEnabled` parameter controls `noscript` escaping; it never executes script.
Keep it consistent with the scripting context used to parse or construct the tree. Serialization does
not mutate nodes, execute script or fetch resources. Keep the tree unchanged for the entire operation;
detected concurrent mutation raises `InvalidOperationException` and discards unpublished output.

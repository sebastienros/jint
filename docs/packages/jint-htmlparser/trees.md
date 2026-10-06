# Working with Trees

Use `using Jint.HtmlParser;` for native tree types. A `Document` owns its nodes. Walk ordinary children
through `FirstChild`, `NextSibling`, `ParentNode`, or `ChildNodes`; `DocumentElement` returns the root
element. Native `Node` objects are CLR objects and require no JavaScript engine.

## Creation and mutation

Create nodes with the owning document: `CreateElement`, `CreateTextNode`, `CreateComment`,
`CreateCDataSection`, `CreateProcessingInstruction`, `CreateAttribute` and `CreateDocumentFragment`.
Use `CreateElementNS` and `CreateAttributeNS` for explicit namespaces; `Namespaces` supplies common URIs.
CDATA creation requires an XML document.

`AppendChild`, `InsertBefore`, `ReplaceChild`, `RemoveChild` and `ReplaceChildren` mutate the tree.
Appending a fragment moves its children and empties the fragment. `Element.GetAttribute` returns `null`
for a missing attribute; `SetAttribute` and `RemoveAttribute` update it. Their `*NS` variants distinguish
namespace URI and local name. An `Attr` has an owner element and is not an ordinary child node.

`CloneNode(deep: true)` copies a subtree. `ImportNode(source, deep: true)` copies it into another document;
`AdoptNode(source)` moves ownership while retaining native identity. Keep all tree access synchronized
while performing these operations. `Normalize` merges adjacent ordinary text nodes and repairs live endpoints.

## Mutation subscriptions

Observe changes with `Document.ObserveMutations`. The standalone subscription queues records for explicit
draining; it does not schedule a JavaScript callback or a browser microtask.

<!-- snippet: parser-mutations -->
```csharp
var document = MarkupParser.ParseXml("<root/>");
var root = document.DocumentElement!;
using var subscription = document.ObserveMutations(root, new MutationObserverOptions
{
    ChildList = true,
    Attributes = true,
    AttributeOldValue = true,
    Subtree = true
});
var item = document.CreateElement("item");
root.AppendChild(item);
item.SetAttribute("id", "one");
foreach (var record in subscription.TakeRecords())
{
    Console.WriteLine(record.Kind);
}
```
<!-- endSnippet -->

This prints `ChildList` and `Attributes`. Enable the record categories you need, and `Subtree` to include
descendants. `AttributeOldValue` and `CharacterDataOldValue` request previous values; `AttributeFilter`
restricts attribute names. `TakeRecords()` returns an independent, read-only record collection and empties
the queue; referenced nodes remain mutable. `Disconnect()` removes registrations; disposing the
subscription also releases it. Use `TakeRecordsForDelivery()` when implementing a delivery cycle that
must also clear transient registrations associated with removed subtrees.

## Live ranges

`DomNodeIdentity` represents a node or an attribute. `BoundaryPoint` combines that identity with an
unsigned offset: UTF-16 units for character data, child positions for containers.
`Document.CreateRange()` creates a live `DomRange`; endpoints repair as the tree changes.

<!-- snippet: parser-range -->
```csharp
var document = MarkupParser.ParseXml("<root/>");
var text = document.CreateTextNode("abcd");
document.DocumentElement!.AppendChild(text);
var range = document.CreateRange();
range.SelectNodeContents(new DomNodeIdentity(text));
text.ReplaceData(1, 1, "B");
Console.WriteLine(range.GetText()); // aBcd
```
<!-- endSnippet -->

This prints `aBcd`. Set individual endpoints with `SetStart`/`SetEnd`, or choose a node or its contents.
`Collapse`, point/boundary comparisons and `IntersectsNode` support selection logic.
`CloneContents` copies selected content; `ExtractContents` moves it; `DeleteContents` removes it.
`InsertNode` and `SurroundContents` edit at the selected boundaries and apply DOM validity checks.
Invalid operations raise `DomException`, whose `Name` identifies the DOM error.

`DomStaticRange` records endpoints without following subsequent edits; use `IsValid()` to check them.
It does not freeze the referenced nodes. `DomRange.Detach()` is a compatibility no-op.

## Iterators and tree walkers

`DomNodeIterator` walks tree order and repairs its reference when nodes are removed. `DomTreeWalker`
provides parent, child, sibling and forward/backward movement with a mutable `Current` identity.
Both take a DOM node-type bitmask and an optional synchronous `TraversalFilter` per movement call:

<!-- snippet: parser-iterator -->
```csharp
var document = MarkupParser.ParseXml("<root><item/></root>");
var iterator = new DomNodeIterator(new DomNodeIdentity(document), whatToShow: 1);
while (iterator.Next(filter: null) is { } identity)
{
    Console.WriteLine(((Element) identity.Node!).LocalName);
}
```
<!-- endSnippet -->

A mask of `1` selects elements; `uint.MaxValue` permits all types. Filters return `1` for accept,
`2` for reject or `3` for skip. Recursive filtering raises `InvalidStateError`. Traversal remains an
ordinary tree walk: template contents and shadow trees are separate roots that must be traversed explicitly.

## Templates and shadow roots

A template element's `TemplateContent` holds its detached content fragment. An attached shadow root has
its own descendant tree. `Element.AttachShadow(new ShadowRootInit(...))` returns that native root,
including in closed mode; `OpenShadowRoot` exposes only open roots. Attachment performs no Browser
custom-element reactions. See [Serialization](./serialization.md#shadow-roots) for explicit inclusion.

Selector matching and browser CSSOM helpers are internal integration APIs. Use native traversal or the
public [XPath API](./css-and-xpath.md#xpath-1-0) for standalone tree queries.

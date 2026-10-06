# Limits and Threading

## Bound parsing work

Limits are inclusive and count UTF-16 units rather than bytes. Zero disables an individual bound.
`new ParseLimits()` retains a finite XML entity-expansion ceiling even when other bounds are omitted.

| Property | Scope | Default |
| --- | --- | --- |
| `MaxInputCharacters` | Original markup or CSS input | `0` |
| `MaxTokenCharacters` | One atomic lexical token | `0` |
| `MaxNestingDepth` | Nested CSS functions and simple blocks; not DOM depth | `0` |
| `MaxEntityExpansionCharacters` | XML general/parameter entity replacement units | `10,000,000` |

Use `HtmlParseOptions`, `XmlParseOptions` or `CssParseOptions` to supply bounds. Public parse entry points
also accept a cancellation token. With `using Jint.HtmlParser;`:

<!-- snippet: parser-limits -->
```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var diagnostics = new ParseDiagnosticCollector(capacity: 20);
var options = new HtmlParseOptions
{
    Limits = new ParseLimits { MaxInputCharacters = 100_000, MaxTokenCharacters = 20_000 },
    Diagnostics = diagnostics
};
var document = MarkupParser.ParseHtml("<p>Hello</p>", options, cancellation.Token);
Console.WriteLine(MarkupSerializer.ToHtmlChildren(document.DocumentElement!.LastChild!));
```
<!-- endSnippet -->

`ParseLimitException` exposes `Kind`, `Limit` and `Observed`. Handle it separately from
`MarkupParseException` (XML/SVG syntax) or `CssParseException` (a CSS construct that cannot be parsed).
Cancellation raises `OperationCanceledException`. A failed operation returns no result; the context of
fragment parsing is not replaced by a successful or failed parse.

The entity ceiling is not a complete allocation or execution-time budget. Input and token ceilings and
cooperative cancellation should be chosen for the application's workload. `ParseLimits.Unbounded`
disables every bound, including entity expansion; reserve it for inputs whose work your host already controls.
Serialization has a separate [output limit](./serialization.md).

## Collect recoverable diagnostics

HTML and CSS options accept a `ParseDiagnosticCollector` with a positive capacity. Its `Items` view is
read-only and live, and `IsTruncated` records overflow. Diagnostics have domain-prefixed `Code` values
and original UTF-16 offsets. Reuse after `Clear()` only when earlier readers no longer depend on the
collector's live view. Recovery diagnostics do not turn HTML parsing into strict XML parsing.

## Share immutable inspection safely

After parsing completes and a tree is safely published, concurrent immutable inspection of links,
names, character data and attribute values is supported, including cold text/attribute getters,
text-content reads and serialization with separate per-call state. Keep the tree unchanged throughout
all of these operations.

Mutation, adoption, parsing sessions, live ranges/iterators and mutation subscriptions require external
synchronization with **all** readers. One writer alone does not permit overlapping reads.
Operations that initialize mutable state or indexes, including selector/id queries, form-control state
and processing-instruction attribute views, also need synchronization. A method that looks like a read
is not automatically safe concurrently.

Trees owned by [Jint.Browser](../jint-browser/) retain page-loop affinity. Use scheduled Page operations
to access those trees; standalone immutable inspection does not authorize reading a live browser DOM
from another thread while the page can run script, parse or mutate it.

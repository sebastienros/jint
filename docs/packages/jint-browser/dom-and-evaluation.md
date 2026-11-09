# DOM and evaluation

The script-visible DOM is generated from an explicit native binding contract over the [Jint.HtmlParser](../jint-htmlparser/) tree. Interface objects and prototype chains are installed so ordinary checks such as `node instanceof Element` work. `window` is the global object and inherits from `Window.prototype`.

Use `SetContentAsync` for supplied markup and `ContentAsync` for serialized HTML:

```csharp
await page.SetContentAsync("<main><h1>Hello</h1></main>");
string html = await page.ContentAsync();
```

## Evaluating JavaScript

```csharp
string? title = await page.EvaluateAsync<string>("document.title");
```

`EvaluateAsync` converts the completion value to a CLR value on the page thread. Do not return the whole `window` or other cyclic object graphs; project the value you need.

Pass data separately from source when the script needs host values:

```csharp
string? text = await page.EvaluateAsync<string>(
    "arg => document.querySelector(arg.selector)?.textContent",
    new { selector = "[data-name='recipe']" });
```

The argument-taking overloads take a **function expression**, invoked with exactly one argument.
Use an object or array for multiple values. JSON-serializable host data is snapshotted before posting;
the immutable snapshot is converted directly to JavaScript data on the page thread, without a JSON
text parse or exposing CLR objects. Quoting, newlines and Unicode in the data cannot alter the source.
Snapshotting still has serialization/allocation costs; changing the caller's data afterwards does not
change the queued argument.

These overloads return detached JSON snapshots: objects are `Dictionary<string, object?>`, arrays are
`object?[]`, numbers are `double`, and null/undefined are null. The generic overload accepts compatible
CLR types and the same invariant-culture scalar conversions as ordinary evaluation; it does not
deserialize arbitrary DTOs. JSON stringify rules apply, including `toJSON`, omitted function members,
and errors for cycles and BigInt. Primitive results convert directly without JSON serialization/parsing;
structured results still serialize and parse JSON. Non-finite numbers become null, as in JSON.
Project only the data needed; DOM objects and callable delegates are not returned across the page thread.
The existing single-string overloads retain their existing conversion behavior.

A promise is not implicitly awaited by `EvaluateAsync`. Use `EvaluateAndAwaitAsync` when the completion value may be a promise:

```csharp
var value = await page.EvaluateAndAwaitAsync<string>(
    "fetch('/data').then(r => r.text())");
```

The page loop remains available to process timers, microtasks, and network completions while the promise is pending.

Promise-returning functions also accept a separate data argument:

```csharp
int count = await page.EvaluateAndAwaitAsync<int>(
    "async arg => { const response = await fetch(arg.url); return (await response.json()).length; }",
    new { url = "/data" },
    cancellationToken);
```

Both `EvaluateAndAwaitAsync(string function, object? argument, CancellationToken cancellationToken = default)`
and its generic overload use the argument snapshot and detached result semantics described above.
Immediate values are converted directly; promise fulfillment converts on the page loop, with a direct
primitive fast path and JSON snapshots for structured results. Rejection faults the task with the existing
getter-free rejection description; conversion/serialization failures also fault the task.
Cancellation, page closure or replacement of the owning document ends a pending wait. Unlike predicate
polling, a pending promise is not retried or rebound after navigation. Existing script/token overloads
keep their existing result conversion behavior. Cancellation observes pending waits; synchronous script
execution remains bounded by the page's normal execution budget.

`WaitForAsync` repeatedly evaluates a condition without blocking the loop:

```csharp
await page.WaitForAsync(
    "document.querySelectorAll('.row').length >= 3",
    TimeSpan.FromSeconds(5));
```

An evaluation failure is treated as “not yet” while polling. If the timeout wins and the last evaluation threw, that JavaScript failure is reported instead of a bare `false`.

Safe data binding is also available for polling:

```csharp
await page.WaitForAsync(
    "arg => document.querySelector(arg.selector)?.textContent.includes(arg.text)",
    new { selector = ".status", text = "ready" },
    TimeSpan.FromSeconds(5));
```

Both expression and function waits prepare their source once per document with that engine's parser
options. Each poll reevaluates it against the current document; function predicates receive fresh
JavaScript data from the same host snapshot. Navigation prepares against the replacement engine rather
than retaining old globals, functions or elements. Polls do not hold the loop while waiting. A false
timeout returns `false`; invalid script and final JavaScript errors remain exceptions, and execution
budget failures are not retried. Closing an already-running wait ends it with `false`; starting an
operation on a closed page throws.

## Native queries and waits

For common DOM conditions, avoid JavaScript source and per-row page-loop calls:

```csharp
int count = await page.CountAsync(".recipe", visibleOnly: true);
string[] rows = await page.TextsAsync(".recipe", visibleOnly: true);
bool ready = await page.WaitForCountAsync(".recipe", 3, TimeSpan.FromSeconds(5), visibleOnly: true);
bool shown = await page.WaitForVisibleAsync(".status", TimeSpan.FromSeconds(5));
```

`CountAsync` and `TextsAsync` accept CSS selectors and throw for invalid selectors. `TextsAsync`
returns one document-order snapshot of **textContent** strings in a single request: no trimming,
rendered whitespace normalization or exclusion of hidden descendant text. `visibleOnly` filters the
matched elements, not the text descendants.

`IsVisibleAsync(target, index = 0)` and `InnerTextAsync(target, index = 0)` accept the same CSS selectors
or accessibility `ref=` targets as input operations. Missing targets return `false` and `null`,
respectively. `InnerTextAsync` uses the DOM's existing renderless innerText extraction rather than
textContent; it is not visual line wrapping.

Visibility is exactly `element.checkVisibility({ visibilityProperty: true })`: detached elements,
`hidden`/`display:none` ancestors, `display:contents` targets and computed `visibility:hidden` are not
visible. A descendant can override inherited visibility. Opacity, `aria-hidden`, viewport intersection
and occlusion are not visibility tests. The synthetic box model is not a rendering engine.

`WaitForCountAsync` requires an exact nonnegative count; zero is useful for disappearance.
`WaitForVisibleAsync(target, timeout, visible: false, index: 0)` succeeds for hidden or missing targets.
`WaitForTextAsync(target, text, timeout, index = 0)` checks a literal ordinal substring of innerText
and requires a matching element. These waits observe mutations and navigation without retaining DOM
handles. Timeout returns `false`, including a zero timeout whose first probe fails.

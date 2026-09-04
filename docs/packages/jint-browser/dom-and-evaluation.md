# DOM and evaluation

The script-visible DOM is generated from AngleSharp's WebIDL-like metadata. Interface objects and prototype chains are installed so ordinary checks such as `node instanceof Element` work. `window` is the global object and inherits from `Window.prototype`.

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

A promise is not implicitly awaited by `EvaluateAsync`. Use `EvaluateAndAwaitAsync` when the completion value may be a promise:

```csharp
var value = await page.EvaluateAndAwaitAsync<string>(
    "fetch('/data').then(r => r.text())");
```

The page loop remains available to process timers, microtasks, and network completions while the promise is pending.

`WaitForAsync` repeatedly evaluates a condition without blocking the loop:

```csharp
await page.WaitForAsync(
    "document.querySelectorAll('.row').length >= 3",
    TimeSpan.FromSeconds(5));
```

An evaluation failure is treated as “not yet” while polling. If the timeout wins and the last evaluation threw, that JavaScript failure is reported instead of a bare `false`.

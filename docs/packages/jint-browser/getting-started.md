# Getting started

Install the package:

```bash
dotnet add package Jint.Browser
```

Create and dispose a browser asynchronously:

```csharp
using Jint.Browser;

await using var browser = new Browser();
var page = await browser.NewPageAsync();

var response = await page.NavigateAsync("https://example.org/");
Console.WriteLine($"{response?.Status} {await page.TitleAsync()}");
Console.WriteLine(await page.TextAsync());
```

`NavigateAsync` accepts `http:`, `https:`, `about:`, and `data:` URLs. To load markup directly, use `SetContentAsync`:

```csharp
await page.SetContentAsync(
    "<p id='answer'></p><script>answer.textContent = 6 * 7</script>",
    "https://app.example/");

var answer = await page.EvaluateAsync<int>(
    "Number(document.querySelector('#answer').textContent)");
```

The optional base URL supplies the document URL and origin, and is used to resolve relative URLs. Without it, the document is `about:blank` with an opaque origin.

Every `Page` owns a thread and a Jint engine. Public page methods post work to that thread and return ordinary CLR values; they never return engine-owned `JsValue` instances or AngleSharp nodes.

Next, read [Lifecycle](./lifecycle), [Navigation](./navigation), and [DOM and evaluation](./dom-and-evaluation).

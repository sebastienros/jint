// Playwright for .NET walking the canonical scenario over Chromium.ConnectOverCDPAsync(http url).
using System.Reflection;
using System.Text.Json;
using Histogram;
using Microsoft.Playwright;

var arguments = ScenarioIo.ParseArguments(args);
var proxy = arguments["proxy"];
var fixture = arguments["fixture"];
// The package version, stamped in by the project file: Microsoft.Playwright ships an assembly whose assembly,
// file and informational versions all read 1.0.0, and "which client version sent this" is the whole
// point of the recording.
var version = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "ClientPackageVersion")?.Value ?? "unknown";

var io = new ScenarioIo(
    proxy,
    arguments["meta"],
    client: "playwright-dotnet",
    clientVersion: $"Microsoft.Playwright {version.Split('+')[0]} (.NET {Environment.Version})",
    entryStyle: "Chromium.ConnectOverCDPAsync(http url)");

IPlaywright playwright;
try
{
    playwright = await Playwright.CreateAsync();
}
catch (Exception error)
{
    // The bundled Node driver could not start on this machine. Say so in the output rather than pretending
    // the client sends nothing.
    Console.Error.WriteLine($"Playwright driver did not start: {error}");
    await io.WriteAsync($"Playwright.CreateAsync() failed: {error.GetType().Name}: {error.Message}");
    return 1;
}

await io.MarkAsync("connect");
var browser = await playwright.Chromium.ConnectOverCDPAsync(proxy);

await io.MarkAsync("newContext");
var context = await browser.NewContextAsync();

await io.MarkAsync("newPage");
var page = await context.NewPageAsync();

await io.MarkAsync("goto");
await page.GotoAsync(fixture, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

await io.StepAsync("querySelector", async () =>
{
    var h1 = await page.QuerySelectorAsync("h1");
    io.Notes["querySelector"] = h1 is null ? "not found" : "found";
});

await io.StepAsync("querySelectorAll", async () =>
{
    var links = await page.QuerySelectorAllAsync("a");
    io.Notes["querySelectorAll"] = $"{links.Count} anchors";
});

await io.StepAsync("evaluateTitle", async () =>
    io.Notes["evaluateTitle"] = await page.EvaluateAsync<string>("() => document.title"));

await io.StepAsync("evaluateObject", async () =>
    io.Notes["evaluateObject"] = (await page.EvaluateAsync<JsonElement>("() => ({ a: 1, b: [1, 2] })")).ToString());

await io.StepAsync("click", () => page.ClickAsync("#btn"));

await io.StepAsync("waitForSelector", async () => await page.WaitForSelectorAsync("#late"));

await io.StepAsync("type", () => page.Locator("#name").PressSequentiallyAsync("abc"));

await io.StepAsync("clickCheckbox", () => page.ClickAsync("input[type=checkbox]"));

await io.StepAsync("selectOption", async () => await page.SelectOptionAsync("select", "b"));

await io.StepAsync("content", async () => io.Notes["content"] = $"{(await page.ContentAsync()).Length} chars");

await io.StepAsync("title", async () => io.Notes["title"] = await page.TitleAsync());

await io.StepAsync("cookies", async () =>
{
    var cookies = await context.CookiesAsync();
    io.Notes["cookies"] = string.Join(",", cookies.Select(c => c.Name));
});

await io.StepAsync("setCookie", () => context.AddCookiesAsync([new Cookie { Name = "x", Value = "y", Url = fixture }]));

await io.StepAsync("evaluateLocalStorage", async () =>
    io.Notes["evaluateLocalStorage"] = await page.EvaluateAsync<string>("() => localStorage.getItem('k')"));

var console = new List<string>();
await io.StepAsync("consoleEvent", async () =>
{
    page.Console += (_, message) => console.Add(message.Text);
    await page.EvaluateAsync("() => console.log('from-scenario')");
    await Task.Delay(200);
    io.Notes["consoleEvent"] = string.Join("|", console);
});

await io.StepAsync("gotoPage2", async () =>
    await page.GotoAsync(new Uri(new Uri(fixture), "page2.html").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Load }));

// Commit rather than the default Load: going back lands on a page Chrome restores from the back/forward
// cache, and a restored page fires no load event, so Load waits out its 30 s timeout.
await io.StepAsync("goBack", async () => await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Commit }));

await io.StepAsync("screenshot", async () => io.Notes["screenshot"] = $"{(await page.ScreenshotAsync()).Length} bytes");

await io.StepAsync("pdf", async () => io.Notes["pdf"] = $"{(await page.PdfAsync()).Length} bytes");

await io.StepAsync("interception", async () =>
{
    var intercepted = 0;
    await page.RouteAsync("**/api.json", route =>
    {
        Interlocked.Increment(ref intercepted);
        _ = route.ContinueAsync();
    });
    await page.EvaluateAsync("() => fetch('/api.json').then(r => r.json())");
    await Task.Delay(300);
    io.Notes["interception"] = $"{intercepted} intercepted";
});

await io.MarkAsync("close");
await page.CloseAsync();
await context.CloseAsync();
await browser.CloseAsync();
playwright.Dispose();

await io.WriteAsync();
return 0;

// PuppeteerSharp walking the canonical scenario over Puppeteer.ConnectAsync(BrowserWSEndpoint).
using System.Reflection;
using System.Text.Json;
using Histogram;
using PuppeteerSharp;

var arguments = ScenarioIo.ParseArguments(args);
var proxy = arguments["proxy"];
var fixture = arguments["fixture"];
// The package version, stamped in by the project file: PuppeteerSharp ships an assembly whose assembly,
// file and informational versions all read 1.0.0, and "which client version sent this" is the whole
// point of the recording.
var version = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "ClientPackageVersion")?.Value ?? "unknown";

var io = new ScenarioIo(
    proxy,
    arguments["meta"],
    client: "puppeteersharp-dotnet",
    clientVersion: $"PuppeteerSharp {version.Split('+')[0]} (.NET {Environment.Version})",
    entryStyle: "Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint })");

using var http = new HttpClient();
var versionJson = JsonDocument.Parse(await http.GetStringAsync($"{proxy}/json/version"));
var webSocketDebuggerUrl = versionJson.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!;

await io.MarkAsync("connect");
var browser = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint = webSocketDebuggerUrl });

await io.MarkAsync("newContext");
var context = await browser.CreateBrowserContextAsync();

await io.MarkAsync("newPage");
var page = await context.NewPageAsync();

await io.MarkAsync("goto");
await page.GoToAsync(fixture, WaitUntilNavigation.Load);

await io.StepAsync("querySelector", async () =>
{
    var h1 = await page.QuerySelectorAsync("h1");
    io.Notes["querySelector"] = h1 is null ? "not found" : "found";
});

await io.StepAsync("querySelectorAll", async () =>
{
    var links = await page.QuerySelectorAllAsync("a");
    io.Notes["querySelectorAll"] = $"{links.Length} anchors";
});

await io.StepAsync("evaluateTitle", async () =>
    io.Notes["evaluateTitle"] = await page.EvaluateFunctionAsync<string>("() => document.title"));

await io.StepAsync("evaluateObject", async () =>
    io.Notes["evaluateObject"] = (await page.EvaluateFunctionAsync<JsonElement>("() => ({ a: 1, b: [1, 2] })")).ToString());

await io.StepAsync("click", () => page.ClickAsync("#btn"));

await io.StepAsync("waitForSelector", async () => await page.WaitForSelectorAsync("#late"));

await io.StepAsync("type", () => page.TypeAsync("#name", "abc"));

await io.StepAsync("clickCheckbox", () => page.ClickAsync("input[type=checkbox]"));

await io.StepAsync("selectOption", async () => await page.SelectAsync("select", "b"));

await io.StepAsync("content", async () => io.Notes["content"] = $"{(await page.GetContentAsync()).Length} chars");

await io.StepAsync("title", async () => io.Notes["title"] = await page.GetTitleAsync());

await io.StepAsync("cookies", async () =>
{
    var cookies = await context.GetCookiesAsync();
    io.Notes["cookies"] = string.Join(",", cookies.Select(c => c.Name));
});

await io.StepAsync("setCookie", () =>
{
    // PuppeteerSharp 25 moved cookies to the browser context and its CookieData has no Url, so the origin
    // is spelled out rather than derived by the client.
    var origin = new Uri(fixture);
    return context.SetCookieAsync(new CookieData { Name = "x", Value = "y", Domain = origin.Host, Path = "/" });
});

await io.StepAsync("evaluateLocalStorage", async () =>
    io.Notes["evaluateLocalStorage"] = await page.EvaluateFunctionAsync<string>("() => localStorage.getItem('k')"));

var console = new List<string>();
await io.StepAsync("consoleEvent", async () =>
{
    page.Console += (_, e) => console.Add(e.Message.Text);
    await page.EvaluateFunctionAsync("() => console.log('from-scenario')");
    await Task.Delay(200);
    io.Notes["consoleEvent"] = string.Join("|", console);
});

await io.StepAsync("gotoPage2", async () => await page.GoToAsync(new Uri(new Uri(fixture), "page2.html").ToString(), WaitUntilNavigation.Load));

// No WaitUntil: going back lands on a page Chrome may restore from the back/forward cache, and a restored
// page fires no load event.
await io.StepAsync("goBack", async () => await page.GoBackAsync());

await io.StepAsync("screenshot", async () => io.Notes["screenshot"] = $"{(await page.ScreenshotDataAsync()).Length} bytes");

await io.StepAsync("pdf", async () => io.Notes["pdf"] = $"{(await page.PdfDataAsync()).Length} bytes");

await io.StepAsync("interception", async () =>
{
    var intercepted = 0;
    await page.SetRequestInterceptionAsync(true);
    page.Request += async (_, e) =>
    {
        if (e.Request.Url.EndsWith("/api.json", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref intercepted);
        }

        try
        {
            await e.Request.ContinueAsync();
        }
        catch (PuppeteerException)
        {
            // Already handled by the time we got here; the recording is what matters, not the outcome.
        }
    };
    await page.EvaluateFunctionAsync("() => fetch('/api.json').then(r => r.json())");
    await Task.Delay(300);
    io.Notes["interception"] = $"{intercepted} intercepted";
});

await io.MarkAsync("close");
await page.CloseAsync();
await context.CloseAsync();
// Disconnect, not DisposeAsync: the browser was connected to, not launched, and disposing would try to
// dispose the context a second time over a connection that is already gone.
browser.Disconnect();

await io.WriteAsync();
return 0;

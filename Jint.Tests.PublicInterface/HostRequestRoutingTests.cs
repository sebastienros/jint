#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// An embedder answering a page's requests in process through <c>Page.RouteAsync</c> and
/// <c>BrowserContext.RouteAsync</c>, with nothing but Jint.Browser's public surface.
/// </summary>
public sealed class HostRequestRoutingTests
{
    [Test]
    public async Task AHostCanFulfilAbortAndUnrouteWithoutAServer()
    {
        await using var browser = new Jint.Browser.Browser();
        var context = await browser.NewContextAsync(new BrowserContextOptions { UrlFilter = uri => uri.IsLoopback });

        RequestResourceType? scriptType = null;
        await context.RouteAsync(
            uri => uri.AbsolutePath.EndsWith("/track.js", StringComparison.Ordinal),
            request =>
            {
                scriptType = request.ResourceType;
                return request.FulfillAsync(status: 200, contentType: "application/javascript", body: "window.__stubbed = true");
            });

        var page = await context.NewPageAsync();
        Func<Uri, bool> api = uri => uri.AbsolutePath == "/api";
        string? method = null;
        await page.RouteAsync(api, request =>
        {
            method = request.Method + " " + request.PostData + " " + request.HeaderValue("content-type");
            return request.AbortAsync("connectionrefused");
        });

        await page.SetContentAsync("<!doctype html><script src=/track.js></script>", "http://127.0.0.1:9/");

        (await page.EvaluateAsync<string>("String(window.__stubbed)")).Should().Be("true");
        scriptType.Should().Be(RequestResourceType.Script);

        (await page.EvaluateAndAwaitAsync<string>(
            "fetch('/api', { method: 'POST', body: 'x', headers: { 'content-type': 'text/plain' } }).then(() => 'resolved', e => e.name)"))
            .Should().Be("TypeError");
        method.Should().Be("POST x text/plain");

        await page.UnrouteAsync(api);
        await page.RouteAsync(api, request => request.ContinueAsync(new RouteContinueOverrides { Method = "GET" }));

        // Continued to a port nothing listens on, so the network itself refuses it this time.
        (await page.EvaluateAndAwaitAsync<string>("fetch('/api').then(() => 'resolved', e => e.name)")).Should().Be("TypeError");
    }
}
#endif

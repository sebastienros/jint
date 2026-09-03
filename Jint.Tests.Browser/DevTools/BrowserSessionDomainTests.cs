using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The two things a client asks the <b>browser</b> session about a page rather than asking the page.
/// </summary>
/// <remarks>
/// Both were found by driving Playwright for .NET, which is the client that does everything at the browser
/// level that Puppeteer does at the page level, and neither was reachable from the recorded handshake: the
/// recording covers what a client sends, and both of these are about what it is <i>told</i>.
/// </remarks>
public class BrowserSessionDomainTests
{
    /// <summary>
    /// Every page target names a browser context, the default one included.
    /// </summary>
    /// <remarks>
    /// Chrome's default browser context has an identifier and every page in it reports one. Playwright
    /// asserts <c>targetInfo.browserContextId</c> on every target it attaches to and <b>kills its driver
    /// process</b> without it, so a page created with no <c>browserContextId</c> — which is what
    /// <c>connectOverCDP</c> then <c>newPage</c> does — has to name the default context rather than nothing.
    /// </remarks>
    [Test]
    public async Task APageInTheDefaultContextStillNamesAContext()
    {
        await using var session = await PageSession.CreateAsync();

        var targetId = (await session.ResultAsync("Target.createTarget", """{"url":"about:blank"}"""))
            .GetProperty("targetId").GetString()!;

        var targets = (await session.ResultAsync("Target.getTargets", """{"filter":[{"type":"page"}]}"""))
            .GetProperty("targetInfos").EnumerateArray().ToArray();

        var created = targets.Single(info => info.GetProperty("targetId").GetString() == targetId);
        created.GetProperty("browserContextId").GetString().Should().NotBeNullOrEmpty();

        // …and it is not one Target.getBrowserContexts lists, because that command answers the contexts a
        // client created rather than the one it was given. Puppeteer's own bookkeeping depends on it.
        (await session.ResultAsync("Target.getBrowserContexts"))
            .GetProperty("browserContextIds").EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// <c>Storage.getCookies</c> answers on the browser session, addressed by context rather than by page.
    /// </summary>
    /// <remarks>
    /// Puppeteer reads a page's cookies on that page's own session and Playwright reads a context's on the
    /// browser session, so a server that registered the domain in one place answered one client and gave the
    /// other <c>-32601</c>. Both are registered now, over the same jar.
    /// </remarks>
    [Test]
    public async Task CookiesAreReadableFromTheBrowserSessionToo()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Jar</title></head><body>one</body></html>");

        await using var session = await PageSession.CreateAsync(new global::Jint.Browser.BrowserContextOptions { UrlFilter = origin.Owns });

        var page = await session.Pages.DefaultContext.NewPageAsync();
        await page.NavigateAsync(origin.Url("/one"));
        await page.EvaluateAsync("document.cookie = 'from=the-page; path=/'");

        // No browserContextId: the default context, which is what a client that created none is asking about.
        var cookies = (await session.ResultAsync("Storage.getCookies", "{}"))
            .GetProperty("cookies").EnumerateArray().ToArray();

        cookies.Should().Contain(cookie => cookie.GetProperty("name").GetString() == "from");

        // And the client can write one the page then reads back, which is the login flow in miniature.
        await session.ResultAsync(
            "Storage.setCookies",
            """{"cookies":[{"name":"from","value":"the-client","url":"URL"}]}""".Replace("URL", origin.Url("/one"), StringComparison.Ordinal));

        (await page.EvaluateAsync<string>("document.cookie")).Should().Contain("from=the-client");

        await session.ResultAsync("Storage.clearCookies", "{}");
        (await page.EvaluateAsync<string>("document.cookie")).Should().BeEmpty();
    }
}

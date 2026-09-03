using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// <see cref="BrowserOptions.UserAgent"/> is what <c>navigator.userAgent</c> answers <b>and</b> what every
/// request the page makes carries, so the two can never disagree.
/// </summary>
/// <remarks>
/// The lanes are the reason this is a suite rather than one case: a document, a subresource the markup
/// referenced, a script's <c>fetch</c> and <c>XMLHttpRequest</c>, and a worker's own module load and fetch
/// each compose their request in a different place, and the header was missing from every one of them until
/// the policy the transport reads carried it (#3720).
/// </remarks>
public sealed class UserAgentTests
{
    private const string Agent = "TestAgent/9.9 (jint)";

    [Test]
    public async Task EveryLaneOfAPageCarriesTheConfiguredUserAgent()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server
                .Map("/style.css", _ => LoopbackResponse.Css("body { color: red }"))
                .Map("/app.js", _ => LoopbackResponse.Script("globalThis.__ran = true;"))
                .Map("/data.json", _ => LoopbackResponse.Json("""{"ok":true}"""))
                .Map("/rows.txt", _ => LoopbackResponse.Text("one"))
                .Map("/api", _ => LoopbackResponse.Text("from the worker"))
                .Map(
                    "/worker.js",
                    _ => LoopbackResponse.Bytes(
                        """
                        self.addEventListener('message', function () {
                          fetch('/api')
                            .then(function (r) { return r.text(); })
                            .then(function (t) { self.postMessage(t); });
                        });
                        """,
                        "text/javascript"))
                .MapHtml(
                    "/page",
                    """
                    <html><head>
                      <link rel="stylesheet" href="/style.css">
                      <script src="/app.js"></script>
                    </head><body>
                      <script>
                        window.replies = [];
                        fetch('/data.json');
                        var request = new XMLHttpRequest();
                        request.open('GET', '/rows.txt');
                        request.send();
                        var worker = new Worker('/worker.js', { type: 'module' });
                        worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                        worker.postMessage('go');
                      </script>
                    </body></html>
                    """),
            configureBrowser: options => options.UserAgent = Agent);

        await fixture.Page.NavigateAsync(fixture.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await WaitForAsync(fixture, "window.replies.length > 0");

        string[] paths = ["/page", "/style.css", "/app.js", "/data.json", "/rows.txt", "/worker.js", "/api"];
        foreach (var path in paths)
        {
            var request = fixture.Server.Received.SingleOrDefault(received => received.Path == path);
            request.Should().NotBeNull(path + " should have been requested");
            request!.Header("User-Agent").Should().Be(Agent, path + " should carry the page's user agent");
        }
    }

    [Test]
    public async Task ThePageDefaultNamesJintBrowserRatherThanNothingAtAll()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<html><body>ok</body></html>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        var request = fixture.Server.Received.Single(received => received.Path == "/page");
        request.Header("User-Agent").Should().Be(await fixture.Page.EvaluateAsync<string>("navigator.userAgent"));
        request.Header("User-Agent").Should().Contain("Jint.Browser");
    }

    private static async Task WaitForAsync(LoopbackPage fixture, string expression)
    {
        (await fixture.Page.WaitForAsync(expression, TimeSpan.FromSeconds(10)))
            .Should().BeTrue(expression + " should have become true");
    }
}

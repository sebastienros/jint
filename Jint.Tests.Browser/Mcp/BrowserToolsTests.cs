using System.Text.Json;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Mcp;

/// <summary>
/// The Model Context Protocol server, driven by a real client over a real transport.
/// </summary>
/// <remarks>
/// Every one of these is a round trip: the arguments are serialized against the schema the server published,
/// the answer comes back as content blocks, and a failure comes back as <c>isError</c> rather than as an
/// exception on the client's thread. That is the whole point of testing this through a client rather than by
/// calling <c>BrowserAgent</c> directly, which the fixture could just as easily have done.
/// </remarks>
public sealed class BrowserToolsTests
{
    private const string Home = """
        <!doctype html>
        <html>
        <head><title>Home</title></head>
        <body>
          <h1>Welcome</h1>
          <a id="about" href="/about">About us</a>
          <form action="/search"><input name="q" id="q"><button type="submit">Search</button></form>
        </body>
        </html>
        """;

    [Test]
    public async Task TheServerPublishesTheToolsAnAgentNeeds()
    {
        await using var fixture = await McpFixture.CreateAsync();

        var tools = await fixture.Client.ListToolsAsync();
        var names = tools.Select(tool => tool.Name).ToArray();

        names.Should().Contain([
            "navigate", "back", "forward", "reload",
            "snapshot",
            "click", "fill", "type", "press", "select", "hover", "scroll",
            "evaluate", "wait_for", "network_requests", "cookies", "set_cookie", "close",
        ]);

        tools.Should().AllSatisfy(tool => tool.Description.Should().NotBeNullOrWhiteSpace(
            "a tool an agent cannot tell from its neighbour is a tool it calls wrongly"));
    }

    [Test]
    public async Task NavigateAnswersWhereThePageEndedUp()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);

        var result = await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        result.IsError.Should().NotBe(true, McpFixture.TextOf(result));

        var answer = JsonDocument.Parse(McpFixture.TextOf(result)).RootElement;
        answer.GetProperty("title").GetString().Should().Be("Home");
        answer.GetProperty("status").GetInt32().Should().Be(200);
        answer.GetProperty("url").GetString().Should().Be(fixture.Url("/"));
    }

    [Test]
    public async Task SnapshotAnswersTheAccessibilityTreeWithReferencesByDefault()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var result = await fixture.CallAsync("snapshot");
        var answer = JsonDocument.Parse(McpFixture.TextOf(result)).RootElement;

        answer.GetProperty("mode").GetString().Should().Be("ax");

        var content = answer.GetProperty("content").GetString()!;
        content.Should().Contain("heading \"Welcome\"").And.Contain("link \"About us\"").And.Contain("[ref=");
    }

    [Test]
    public async Task SnapshotAnswersMarkdownAndTextToo()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var markdown = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("snapshot", ("mode", "markdown")))).RootElement;
        var text = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("snapshot", ("mode", "text")))).RootElement;

        markdown.GetProperty("content").GetString().Should().Contain("# Welcome");
        text.GetProperty("content").GetString().Should().Contain("Welcome").And.NotContain("#");
    }

    [Test]
    public async Task AModeThatNamesNoRepresentationIsAnErrorObject()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var result = await fixture.CallAsync("snapshot", ("mode", "screenshot"));

        result.IsError.Should().BeTrue();
        McpFixture.TextOf(result).Should().Contain("markdown, text and ax");
    }

    [Test]
    public async Task ClickFollowsALinkAndTheAnswerSaysWhereThePageIs()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var result = await fixture.CallAsync("click", ("target", "#about"));
        var answer = JsonDocument.Parse(McpFixture.TextOf(result)).RootElement;

        answer.GetProperty("done").GetBoolean().Should().BeTrue();
        answer.GetProperty("url").GetString().Should().EndWith("/about");
    }

    [Test]
    public async Task ClickTakesAReferenceFromASnapshot()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var snapshot = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("snapshot"))).RootElement
            .GetProperty("content").GetString()!;

        var line = snapshot.Split('\n').First(l => l.Contains("link \"About us\"", StringComparison.Ordinal));
        var reference = line[(line.IndexOf("[ref=", StringComparison.Ordinal) + 5)..];
        reference = reference[..reference.IndexOf(']', StringComparison.Ordinal)];

        var result = await fixture.CallAsync("click", ("target", "ref=" + reference));
        var answer = JsonDocument.Parse(McpFixture.TextOf(result)).RootElement;

        answer.GetProperty("done").GetBoolean().Should().BeTrue();
        answer.GetProperty("url").GetString().Should().EndWith("/about");
    }

    [Test]
    public async Task FillAndPressEnterSubmitTheForm()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        await fixture.CallAsync("fill", ("target", "#q"), ("text", "headless"));
        var result = await fixture.CallAsync("press", ("key", "Enter"));

        var answer = JsonDocument.Parse(McpFixture.TextOf(result)).RootElement;
        answer.GetProperty("url").GetString().Should().Contain("/search?q=headless");

        fixture.Server.Received.Should().Contain(request => request.Query.Contains("q=headless", StringComparison.Ordinal));
    }

    [Test]
    public async Task ATargetThatMatchesNothingSaysWhatToDoInstead()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var answer = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("click", ("target", "#absent")))).RootElement;

        answer.GetProperty("done").GetBoolean().Should().BeFalse();
        answer.GetProperty("note").GetString().Should().Contain("ax snapshot");
    }

    [Test]
    public async Task ARefusedUrlIsAnErrorObjectRatherThanAThrow()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);

        var result = await fixture.CallAsync("navigate", ("url", "https://somewhere-else.invalid/"));

        result.IsError.Should().BeTrue();
        McpFixture.TextOf(result).Should().Contain("URL filter");
    }

    [Test]
    public async Task AUrlThatIsNotOneIsAnErrorObjectNamingWhatToGive()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);

        var result = await fixture.CallAsync("navigate", ("url", "example.com"));

        result.IsError.Should().BeTrue();
        McpFixture.TextOf(result).Should().Contain("absolute http: or https: URL");
    }

    [Test]
    public async Task AnExpressionThatThrowsIsAnErrorObject()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var ok = await fixture.CallAsync("evaluate", ("expression", "document.title"));
        var bad = await fixture.CallAsync("evaluate", ("expression", "nowhereToBeFound()"));

        McpFixture.TextOf(ok).Should().Be("\"Home\"");
        bad.IsError.Should().BeTrue();
        McpFixture.TextOf(bad).Should().Contain("nowhereToBeFound");
    }

    [Test]
    public async Task BackAndForwardMoveThroughTheHistory()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));
        await fixture.CallAsync("navigate", ("url", fixture.Url("/about")));

        var back = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("back"))).RootElement;
        back.GetProperty("done").GetBoolean().Should().BeTrue();
        back.GetProperty("url").GetString().Should().Be(fixture.Url("/"));

        var forward = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("forward"))).RootElement;
        forward.GetProperty("url").GetString().Should().EndWith("/about");
    }

    [Test]
    public async Task WaitForSeesWhatATimerAdds()
    {
        await using var fixture = await McpFixture.CreateAsync(server => server.MapHtml("/late", """
            <!doctype html><title>Late</title>
            <script>setTimeout(() => { const p = document.createElement('p'); p.id = 'ready'; p.textContent = 'done'; document.body.appendChild(p); }, 150);</script>
            """));

        await fixture.CallAsync("navigate", ("url", fixture.Url("/late")));

        var answer = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("wait_for", ("selector", "#ready")))).RootElement;
        answer.GetProperty("done").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task WaitForWithNeitherASelectorNorTextIsAnErrorObject()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);

        var result = await fixture.CallAsync("wait_for");

        result.IsError.Should().BeTrue();
        McpFixture.TextOf(result).Should().Contain("selector or some text");
    }

    [Test]
    public async Task TheRequestLogIsWhatThePageFetched()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var log = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("network_requests"))).RootElement;

        log.GetArrayLength().Should().BeGreaterThan(0);
        log[0].GetProperty("url").GetString().Should().Be(fixture.Url("/"));
        log[0].GetProperty("initiator").GetString().Should().Be("Document");
    }

    [Test]
    public async Task ACookieSetThroughTheToolIsSentByThePage()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        await fixture.CallAsync("set_cookie", ("name", "session"), ("value", "abc"));
        await fixture.CallAsync("reload");

        fixture.Server.Received.Last().Header("cookie").Should().Contain("session=abc");

        var cookies = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("cookies"))).RootElement;
        cookies.EnumerateArray().Should().Contain(cookie => cookie.GetProperty("name").GetString() == "session");
    }

    [Test]
    public async Task TwoSessionsShareNoCookiesAndNoPage()
    {
        await using var first = await McpFixture.CreateAsync(Routes);
        await using var second = await McpFixture.CreateAsync(Routes);

        await first.CallAsync("navigate", ("url", first.Url("/")));
        await first.CallAsync("set_cookie", ("name", "who"), ("value", "first"));

        await second.CallAsync("navigate", ("url", second.Url("/about")));

        var theirs = JsonDocument.Parse(McpFixture.TextOf(await second.CallAsync("cookies"))).RootElement;
        theirs.GetArrayLength().Should().Be(0, "a context per session is the whole isolation");

        var snapshot = JsonDocument.Parse(McpFixture.TextOf(await second.CallAsync("snapshot", ("mode", "text")))).RootElement;
        snapshot.GetProperty("url").GetString().Should().EndWith("/about", "and the other session's page is not this one's");
    }

    [Test]
    public async Task CloseEndsTheSessionAndTheNextNavigateStartsAFreshOne()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);

        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));
        await fixture.CallAsync("set_cookie", ("name", "who"), ("value", "before"));

        var closed = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("close"))).RootElement;
        closed.GetProperty("done").GetBoolean().Should().BeTrue();

        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));
        var cookies = JsonDocument.Parse(McpFixture.TextOf(await fixture.CallAsync("cookies"))).RootElement;

        cookies.GetArrayLength().Should().Be(0, "the cookies went with the context");
    }

    [Test]
    public async Task ThePageIsAResourceAsWellAsATool()
    {
        await using var fixture = await McpFixture.CreateAsync(Routes);
        await fixture.CallAsync("navigate", ("url", fixture.Url("/")));

        var resources = await fixture.Client.ListResourcesAsync();
        resources.Select(resource => resource.Uri).Should().Contain(["jint://page/markdown", "jint://page/requests"]);

        var markdown = await fixture.Client.ReadResourceAsync("jint://page/markdown");
        markdown.Contents.OfType<global::ModelContextProtocol.Protocol.TextResourceContents>()
            .Single().Text.Should().Contain("# Welcome");
    }

    private static void Routes(LoopbackServer server)
    {
        server.MapHtml("/", Home);
        server.MapHtml("/about", "<!doctype html><title>About</title><h1>About us</h1>");
        server.MapHtml("/search", "<!doctype html><title>Results</title><h1>Results</h1>");
    }
}
